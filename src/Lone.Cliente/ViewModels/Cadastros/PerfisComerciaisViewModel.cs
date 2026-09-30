using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Comercial;
using Lone.Contracts.Comum;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da tabela de perfis comerciais.</summary>
public sealed class LinhaPerfilComercial
{
    public LinhaPerfilComercial(PerfilComercialDto item) => Item = item;

    public PerfilComercialDto Item { get; }
    public Guid Id => Item.Id;
    public string Nome => Item.Nome;
    public string Detalhe => string.Join(" · ", new[] { Item.DescontoMaximo is { } d ? $"desconto até {d:0.##}%" : "", Item.LimiteCredito is { } l ? "limite " + l.ToString("C", TextoTela.Brasil) : "" }.Where(t => t.Length > 0));
    public string Usos => Item.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Item.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha de um perfil comercial. As regras finais (nome único, árvore) são da API.</summary>
public sealed partial class PerfilComercialEdicao : ObservableObject
{
    private PerfilComercialEdicao(Guid id, bool novo)
    {
        Id = id;
        Novo = novo;
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;
    public int QuantidadeUsos { get; private set; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private string _limiteCredito = string.Empty;
    [ObservableProperty] private string _descontoMaximo = string.Empty;
    [ObservableProperty] private string _diasMaximoAtraso = string.Empty;
    [ObservableProperty] private Opcao<bool?> _exigeAprovacao = Lone.Cliente.ViewModels.Pessoas.OpcoesComercial.SimNao[0];
    public IReadOnlyList<Opcao<bool?>> ListaSimNao => Lone.Cliente.ViewModels.Pessoas.OpcoesComercial.SimNao;
    [ObservableProperty] private Opcao<Guid?>[] _condicoes = [Lone.Cliente.ViewModels.Pessoas.OpcoesComercial.Nenhum];
    [ObservableProperty] private Opcao<Guid?> _condicao = Lone.Cliente.ViewModels.Pessoas.OpcoesComercial.Nenhum;
    private Guid? _condicaoGravada;

    public void DefinirCondicoes(IReadOnlyList<CondicaoPagamentoDto> lista)
    {
        var atual = Condicao.Valor ?? _condicaoGravada;
        Condicoes = Lone.Cliente.ViewModels.Pessoas.OpcoesColaborador.Lista(lista, c => c.Id, c => $"{c.Nome} ({c.Parcelas})", c => c.Ativo, _condicaoGravada);
        Condicao = Condicoes.FirstOrDefault(c => c.Valor == atual) ?? Condicoes[0];
    }

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo perfil comercial" : Nome;
    public string SituacaoTexto => Novo ? "Novo perfil comercial" : Ativo ? "Ativo" : "Desativado (não aparece para novas escolhas; continua onde já está)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} conta(s) de cliente com este perfil.";

    public static PerfilComercialEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static PerfilComercialEdicao De(PerfilComercialDto t) => new(t.Id, novo: false)
    {
        Versao = t.Versao,
        Ativo = t.Ativo,
        QuantidadeUsos = t.QuantidadeUsos,
        LimiteCredito = TextoTela.Decimal(t.LimiteCredito),
        DescontoMaximo = TextoTela.Decimal(t.DescontoMaximo),
        DiasMaximoAtraso = TextoTela.Inteiro(t.DiasMaximoAtraso),
        ExigeAprovacao = Lone.Cliente.ViewModels.Pessoas.OpcoesComercial.SimNao.First(o => o.Valor == t.ExigeAprovacaoAcimaLimite),
        _condicaoGravada = t.CondicaoPagamentoId,
        Nome = t.Nome
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome.");
        if (!TextoTela.TentarDecimal(LimiteCredito, out _)) erros.Add("Limite de crédito inválido.");
        if (!TextoTela.TentarDecimal(DescontoMaximo, out _)) erros.Add("Desconto máximo inválido.");
        if (!TextoTela.TentarInteiro(DiasMaximoAtraso, out _)) erros.Add("Dias de atraso inválidos.");
        return erros;
    }

    public PerfilComercialDto ParaDto() => new()
    {
        Id = Id,
        Versao = Versao,
        Nome = Nome.Trim(),
        LimiteCredito = TextoTela.TentarDecimal(LimiteCredito, out var limite) ? limite : null,
        DescontoMaximo = TextoTela.TentarDecimal(DescontoMaximo, out var desconto) ? desconto : null,
        DiasMaximoAtraso = TextoTela.TentarInteiro(DiasMaximoAtraso, out var dias) ? dias : null,
        ExigeAprovacaoAcimaLimite = ExigeAprovacao.Valor,
        CondicaoPagamentoId = Condicoes.Length > 1 ? Condicao.Valor : _condicaoGravada,
        Ativo = Ativo
    };
}

/// <summary>Perfis comerciais (cadastro comercial). Nada é excluído: desativar esconde das escolhas novas.</summary>
public sealed partial class PerfisComerciaisViewModel : CadastroViewModelBase<LinhaPerfilComercial>
{
    private readonly ComercialApi _api;
    private List<CondicaoPagamentoDto> _condicoes = [];

    public PerfisComerciaisViewModel(ComercialApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private PerfilComercialEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaPerfilComercial item) => item.Nome + " " + item.Detalhe;

    protected override async Task<IReadOnlyList<LinhaPerfilComercial>> ListarAsync()
    {
        _condicoes = await _api.ListarAsync<CondicaoPagamentoDto>(Rotas.Comercial.Condicoes, incluirInativos: true);
        return (await _api.ListarAsync<PerfilComercialDto>(Rotas.Comercial.Perfis, incluirInativos: true))
            .OrderBy(t => !t.Ativo).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new LinhaPerfilComercial(t)).ToList();
    }

    private PerfilComercialEdicao Preparar(PerfilComercialEdicao f)
    {
        f.DefinirCondicoes(_condicoes);
        return f;
    }

    protected override async Task AbrirAsync(LinhaPerfilComercial item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = Preparar(PerfilComercialEdicao.Criar());
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override object? FichaObservada => Formulario;
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Novo: false } formulario) return;
        Formulario = await ObterAsync(formulario.Id);
    }

    private async Task<PerfilComercialEdicao> ObterAsync(Guid id) =>
        Preparar(PerfilComercialEdicao.De(await _api.ObterAsync<PerfilComercialDto>(Rotas.Comercial.Perfis, id) ?? throw new ValidacaoException(["Este cadastro não existe mais."])));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        PerfilComercialDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(Rotas.Comercial.Perfis, formulario.Id, formulario.ParaDto())))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(PerfilComercialEdicao.De(salvo!));
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Novo ? "Cadastro criado." : "Alterações salvas.", TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private Task DesativarAsync() => AlterarSituacaoAsync(desativar: true);

    [RelayCommand]
    private Task ReativarAsync() => AlterarSituacaoAsync(desativar: false);

    private async Task AlterarSituacaoAsync(bool desativar)
    {
        if (Formulario is not { Novo: false } formulario) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes de " + (desativar ? "desativar" : "reativar") + ".", TipoMensagem.Aviso);
            return;
        }

        PerfilComercialDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync<PerfilComercialDto>(Rotas.Comercial.Perfis, formulario.Id, formulario.Versao)
                : await _api.ReativarAsync<PerfilComercialDto>(Rotas.Comercial.Perfis, formulario.Id, formulario.Versao)))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(PerfilComercialEdicao.De(gravado!));
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Desativado." : "Reativado.", TipoMensagem.Sucesso);
    }
}
