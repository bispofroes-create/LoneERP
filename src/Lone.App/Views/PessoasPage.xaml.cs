using Lone.App.Controles.Grade;
using Lone.Cliente.Grade;
using Lone.Cliente.Navegacao;
using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.App.Views.Pessoas;

namespace Lone.App.Views;

/// <summary>
/// Só aparência. Pessoas usa uma tela de cada vez em qualquer largura (lista em tabela → ficha em tela cheia), por isso
/// fica sempre no "modo compacto" do mestre-detalhe. A tabela é uma <see cref="GradeLista"/> (P2-B2): o nome preso à
/// esquerda, as colunas rolando para o lado e as larguras calculadas na própria grade. Tela estreita (celular): só o nome.
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
        // Linha de filtro da tabela: a grade pede o campo de cada coluna pela chave; a tela monta com o filtro da coluna.
        Tabela.CriarFiltro = CriarFiltroDaColuna;
        // Prévia aberta ou fechada: a coluna dela aparece ou some (o ViewModel já ajustou as colunas da tabela).
        // "+ Adicionar motivo": o campo abre já com o cursor nele.
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PessoasViewModel.MotivoAberto) && _viewModel.MotivoAberto)
                Dispatcher.Dispatch(() => CampoMotivo.Focus());
        };
        _viewModel.Previa.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PreviaPessoa.Visivel)) AjustarColunaPrevia();
        };
#if DEBUG
        // B2Temp: diagnóstico dos campos cortados nas fichas (03/10/2026). Só observa. Remover com o laboratório.
        LabGradeB2Temp.DiagCampos.Ligar(this, () => _viewModel.Editando);
        // B2Temp: diagnóstico 3.0 do botão de colunas (P2-B2). Só observa. Remover com o laboratório.
        LabGradeB2Temp.Diag30.Ligar(this, _viewModel, BotaoColunasB2Temp, Lista,
            () => _viewModel.Filtros.Aberto && DeviceInfo.Idiom != DeviceIdiom.Phone ? LarguraPainelFiltros : 0);
#endif
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

    /// <summary>
    /// Campo de filtro de uma coluna da tabela (o mesmo de antes: texto, número ou data num campo digitado; lista numa
    /// escolha), ligado ao filtro da coluna. Coluna sem filtro: nada (a grade deixa o espaço vazio).
    /// </summary>
    private View? CriarFiltroDaColuna(ColunaGradeDef definicao)
    {
        var grade = _viewModel.Grade;
        var coluna = definicao.Chave == grade.Nome.Id ? grade.Nome : grade.Visiveis.FirstOrDefault(c => c.Id == definicao.Chave);
        if (coluna?.Filtro is not { } filtro) return null;

        View campo;
        if (coluna.FiltroDigitado)
        {
            var texto = new Entry { FontSize = 13, ClearButtonVisibility = ClearButtonVisibility.WhileEditing, Placeholder = filtro.Dica };
            texto.SetBinding(Entry.TextProperty, new Binding(nameof(FiltroColuna.Texto), BindingMode.TwoWay, source: filtro));
            campo = texto;
        }
        else if (coluna.FiltroEscolha)
        {
            var escolha = new Picker { FontSize = 13, ItemsSource = filtro.Escolhas };
            escolha.SetBinding(Picker.SelectedItemProperty, new Binding(nameof(FiltroColuna.Escolha), BindingMode.TwoWay, source: filtro));
            campo = escolha;
        }
        else return null;
        SemanticProperties.SetDescription(campo, $"Filtrar {coluna.Nome}");
        // Moldura única: o campo nativo fica sem a dele (03/10/2026); a borda de fora fica azul enquanto o campo está em uso.
        Plataforma.CampoSemMoldura.Aplicar(campo);

        var borda = new Border { Content = campo };
        if (Application.Current?.Resources.TryGetValue("CampoFiltroColuna", out var estilo) == true && estilo is Style s) borda.Style = s;
        // Valor que não dá para usar (ex.: data inválida): borda de erro, como antes.
        if (Application.Current?.Resources.TryGetValue("Erro", out var erro) == true && erro is Color corErro)
        {
            var invalido = new DataTrigger(typeof(Border)) { Binding = new Binding(nameof(FiltroColuna.Invalido), source: filtro), Value = true };
            invalido.Setters.Add(new Setter { Property = Border.StrokeProperty, Value = corErro });
            borda.Triggers.Add(invalido);
        }
        return borda;
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
