using Lone.Cliente.Navegacao;
using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.App.Views.Pessoas;

namespace Lone.App.Views;

/// <summary>
/// Só aparência. Pessoas usa uma tela de cada vez em qualquer largura (lista em tabela → ficha em tela cheia), por isso
/// fica sempre no "modo compacto" do mestre-detalhe. Na tabela, o nome fica preso à esquerda e as colunas escolhidas rolam
/// para o lado; o cabeçalho acompanha a rolagem lateral das linhas. Tela estreita (celular): só o nome.
/// </summary>
public partial class PessoasPage : ContentPage
{
    private readonly PessoasViewModel _viewModel;
    private readonly AberturaDePessoa _abertura;
    private bool _carregado;

    public PessoasPage(PessoasViewModel viewModel, AberturaDePessoa abertura)
    {
        _abertura = abertura;
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.ModoCompacto = true; // lista OU ficha, nunca lado a lado
        _viewModel.AbrirConfiguracoesDoModulo = () => GerenciadorNavegacao.Padrao.IrParaTelaAsync(ModulosConfiguracao.Rota(ModulosConfiguracao.Pessoas), OrigemNavegacao.Link);
        // Botão "Configurações" da tela (os cadastros de Pessoas saíram do menu lateral).
        _viewModel.AbrirTela = rota => GerenciadorNavegacao.Padrao.IrParaTelaAsync(rota, OrigemNavegacao.Link);
        // Ações rápidas da linha: discador/Teams ("tel:"), WhatsApp (https://wa.me) e o programa de e-mail ("mailto:").
        // OpenAsync (e não TryOpenAsync): no Android 11+ a consulta "algum aplicativo abre?" responde não sem declarar cada
        // esquema no manifesto; sem aplicativo, OpenAsync lança e a tela avisa.
        _viewModel.AbrirEndereco = async endereco =>
        {
            if (!await Launcher.Default.OpenAsync(new Uri(endereco)))
                throw new InvalidOperationException("Nenhum aplicativo abre " + endereco);
        };
        SizeChanged += (_, _) => AjustarLarguras();
        // Painel de filtros aberto ao lado ocupa parte da largura: as colunas da tabela se ajustam.
        _viewModel.Filtros.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(PainelFiltrosPessoas.Aberto)) return;
            MontarPainelFiltros();
            AjustarLarguras();
        };
        // Cabeçalho (títulos + linha de filtro) acompanha a rolagem lateral das colunas.
        // Nos dois sentidos: o cabeçalho também rola sozinho (arrasto no touch, Shift+roda, Tab num filtro fora da vista).
        RolagemColunas.Scrolled += (_, e) =>
        {
            if (Eco(ref _alvoColunas, e.ScrollX) || Math.Abs(RolagemCabecalho.ScrollX - e.ScrollX) <= 0.5) return;
            _alvoCabecalho = e.ScrollX;
            _ = RolagemCabecalho.ScrollToAsync(e.ScrollX, 0, false);
        };
        RolagemCabecalho.Scrolled += (_, e) =>
        {
            if (Eco(ref _alvoCabecalho, e.ScrollX) || Math.Abs(RolagemColunas.ScrollX - e.ScrollX) <= 0.5) return;
            _alvoColunas = e.ScrollX;
            _ = RolagemColunas.ScrollToAsync(e.ScrollX, 0, false);
        };
        _viewModel.Grade.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GradePessoas.MostrarColunas)) AjustarColunaNome();
        };
        // Prévia aberta ou fechada: a coluna dela aparece ou some (o ViewModel já ajustou as colunas da tabela).
        _viewModel.Previa.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PreviaPessoa.Visivel)) AjustarColunaPrevia();
        };
    }

    /// <summary>
    /// Painel de filtros sob demanda: entrar em Pessoas não monta o painel (fechado, ninguém o vê); a primeira abertura
    /// monta, e ele fica. Os filtros em si (chips, contagem, condições) vivem no ViewModel e funcionam sem o painel.
    /// </summary>
    private void MontarPainelFiltros()
    {
        if (HospedeFiltros.Content is not null || !_viewModel.Filtros.Aberto) return;
        HospedeFiltros.Content = new PainelFiltrosView { BindingContext = _viewModel.Filtros };
    }

    private void AjustarColunaPrevia() =>
        ColunaPrevia.Width = new GridLength(_viewModel.Previa.Visivel ? PessoasViewModel.LarguraComPrevia + PessoasViewModel.EspacoPrevia : 0);

    // Posição que o código pediu a cada rolagem: o "Scrolled" que volta dela é só o eco e não é repassado (sem ping-pong,
    // e sem perder a rolagem do usuário que chegar no meio).
    private double? _alvoCabecalho;
    private double? _alvoColunas;

    private static bool Eco(ref double? alvo, double x)
    {
        if (alvo is not { } pedido) return false;
        if (Math.Abs(pedido - x) > 0.5) return false;
        alvo = null;
        return true;
    }

    /// <summary>Com colunas, o nome tem largura fixa (presa); sem elas (celular), o nome ocupa a linha toda.</summary>
    private void AjustarColunaNome()
    {
        var comColunas = _viewModel.Grade.MostrarColunas;
        var largura = comColunas ? new GridLength(GradePessoas.LarguraNome) : GridLength.Star;
        var resto = comColunas ? GridLength.Star : new GridLength(0);
        ColunaNomeCabecalho.Width = largura;
        ColunaNomeLinhas.Width = largura;
        ColunaRestoCabecalho.Width = resto;
        ColunaRestoLinhas.Width = resto;
    }

    /// <summary>Largura do painel de filtros aberto ao lado da tabela (340 do painel + 16 de espaço).</summary>
    private const double LarguraPainelFiltros = 356;

    private void AjustarLarguras()
    {
        // Painel de filtros ao lado (fora do celular): coluna de largura fixa; a tabela fica com o resto.
        var painel = _viewModel.Filtros.Aberto && DeviceInfo.Idiom != DeviceIdiom.Phone ? LarguraPainelFiltros : 0;
        ColunaFiltros.Width = new GridLength(painel);
        _viewModel.DefinirLarguraDaLista(Width - Lista.Padding.HorizontalThickness - painel);
        AjustarColunaPrevia();
        // Largura útil da ficha (margens de 32 de cada lado, até o máximo de 1600): decide se o resumo vai para o lado.
        _viewModel.Resumo.DefinirLargura(Math.Min(Width - 64, 1600));
    }

    /// <summary>Carrega na primeira vez; depois, abre a ficha pedida por outra tela ("Abrir ficha"), se houver.</summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_carregado)
        {
            _carregado = true;
            await _viewModel.CarregarCommand.ExecuteAsync(null);
        }
        // "Abrir ficha" de outra tela: chegar a Pessoas e abrir a ficha é um passo só no histórico (Voltar retorna à origem).
        if (_abertura.Retirar() is { } id) _ = _viewModel.AbrirPorLinkAsync(new ReferenciaRegistro(PessoasViewModel.TipoPessoa, id, string.Empty));
    }

    /// <summary>Botão voltar do Android com a ficha aberta: volta para a lista (pergunta se houver alterações).</summary>
    protected override bool OnBackButtonPressed()
    {
        if (!_viewModel.Editando) return base.OnBackButtonPressed();
        _viewModel.FecharFichaCommand.Execute(null);
        return true;
    }
}
