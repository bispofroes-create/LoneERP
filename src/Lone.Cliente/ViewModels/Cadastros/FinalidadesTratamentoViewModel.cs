using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Lone.Cliente.Grade;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Textos das bases legais e classificações (os mesmos na tela de finalidades e na aba Privacidade).</summary>
public static class TextosPrivacidade
{
    public static string BaseLegal(BaseLegal b) => b switch
    {
        Domain.Enums.BaseLegal.Consentimento => "Consentimento",
        Domain.Enums.BaseLegal.ExecucaoContrato => "Execução de contrato",
        Domain.Enums.BaseLegal.ObrigacaoLegal => "Obrigação legal ou regulatória",
        Domain.Enums.BaseLegal.LegitimoInteresse => "Legítimo interesse",
        Domain.Enums.BaseLegal.ExercicioRegularDireitos => "Exercício regular de direitos",
        _ => "Não definida"
    };

    public static string Classificacao(ClassificacaoCanal c) => c switch
    {
        ClassificacaoCanal.Marketing => "Uso para marketing (só e-mail)",
        _ => "Nenhuma"
    };

    public static string Canal(CanalComunicacao? c) => c is { } canal ? NomesPessoa.Canal(canal) : "Qualquer canal";
}

/// <summary>Linha da tabela de finalidades (Finalidade · Em uso · Ativa).</summary>
public sealed class LinhaFinalidadeTratamento
{
    public LinhaFinalidadeTratamento(FinalidadeTratamentoDto finalidade) => Finalidade = finalidade;

    public FinalidadeTratamentoDto Finalidade { get; }
    public Guid Id => Finalidade.Id;
    public string Nome => Finalidade.Nome;
    public string Categoria => Finalidade.SomenteHistorico ? "Somente histórico" : TextosPrivacidade.BaseLegal(Finalidade.BaseLegal);
    public string Usos => Finalidade.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Finalidade.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha de uma finalidade de tratamento. As regras finais (código imutável, de sistema, nome único) são da API.</summary>
public sealed partial class FinalidadeTratamentoEdicao : ObservableObject
{
    /// <summary>Só as bases legais que já têm regra no Lone (hoje, Consentimento). As outras estão modeladas, sem regra.</summary>
    public static readonly Opcao<BaseLegal>[] BasesDisponiveis = [new(Domain.Enums.BaseLegal.Consentimento, "Consentimento")];

    public static readonly Opcao<ClassificacaoCanal>[] ClassificacoesDisponiveis =
    [
        new(ClassificacaoCanal.Nenhuma, TextosPrivacidade.Classificacao(ClassificacaoCanal.Nenhuma)),
        new(ClassificacaoCanal.Marketing, TextosPrivacidade.Classificacao(ClassificacaoCanal.Marketing))
    ];

    private FinalidadeTratamentoEdicao(Guid id, bool novo)
    {
        Id = id;
        Novo = novo;
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;
    public bool DoSistema { get; private set; }
    public bool SomenteHistorico { get; private set; }
    public int QuantidadeUsos { get; private set; }

    /// <summary>Base legal e classificação só mudam em finalidade criada pelo usuário.</summary>
    public bool PodeMudarRegras => !DoSistema;

    /// <summary>O código só é informado ao criar (as regras usam o código; ele não muda).</summary>
    public bool CodigoSomenteLeitura => !Novo;

    [ObservableProperty] private string _codigo = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private string _descricao = string.Empty;
    [ObservableProperty] private string _ordem = string.Empty;
    [ObservableProperty] private Opcao<BaseLegal>[] _basesLegais = BasesDisponiveis;
    [ObservableProperty] private Opcao<BaseLegal> _baseLegal = BasesDisponiveis[0];
    [ObservableProperty] private Opcao<ClassificacaoCanal> _classificacao = ClassificacoesDisponiveis[0];

    public Opcao<ClassificacaoCanal>[] Classificacoes => ClassificacoesDisponiveis;

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Nova finalidade" : Nome;
    public string SituacaoTexto =>
        Novo ? "Nova finalidade"
        : SomenteHistorico ? "Somente histórico: guarda os consentimentos de antes das finalidades; não se concede, não se revoga e não autoriza comunicação."
        : Ativo ? (DoSistema ? "Ativa (de sistema: código, base legal e classificação não mudam)" : "Ativa")
        : "Desativada (não aparece para novas concessões; continua no histórico)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} período(s) de consentimento com esta finalidade.";

    public static FinalidadeTratamentoEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static FinalidadeTratamentoEdicao De(FinalidadeTratamentoDto f)
    {
        var e = new FinalidadeTratamentoEdicao(f.Id, novo: false)
        {
            Versao = f.Versao,
            Ativo = f.Ativo,
            DoSistema = f.DoSistema,
            SomenteHistorico = f.SomenteHistorico,
            QuantidadeUsos = f.QuantidadeUsos,
            Codigo = f.Codigo,
            Nome = f.Nome,
            Descricao = f.Descricao ?? string.Empty,
            Ordem = f.Ordem.ToString(CultureInfo.InvariantCulture)
        };
        // A gravada aparece mesmo que ainda não tenha regra (ex.: "Não definida" do Registro anterior).
        var gravada = BasesDisponiveis.FirstOrDefault(b => b.Valor == f.BaseLegal);
        if (gravada is null)
        {
            gravada = new Opcao<BaseLegal>(f.BaseLegal, TextosPrivacidade.BaseLegal(f.BaseLegal));
            e.BasesLegais = [gravada, .. BasesDisponiveis];
        }
        e.BaseLegal = gravada;
        e.Classificacao = ClassificacoesDisponiveis.FirstOrDefault(c => c.Valor == f.ClassificacaoExigida) ?? ClassificacoesDisponiveis[0];
        return e;
    }

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (Novo && string.IsNullOrWhiteSpace(Codigo)) erros.Add("Informe o código da finalidade (ex.: PESQUISA_SATISFACAO).");
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome da finalidade.");
        if (!string.IsNullOrWhiteSpace(Ordem) && !TextoTela.TentarInteiro(Ordem, out _)) erros.Add("Ordem: use um número inteiro.");
        return erros;
    }

    public FinalidadeTratamentoDto ParaDto()
    {
        TextoTela.TentarInteiro(Ordem, out var ordem);
        return new FinalidadeTratamentoDto
        {
            Id = Id,
            Versao = Versao,
            Codigo = Codigo.Trim(),
            Nome = Nome.Trim(),
            Descricao = TextoTela.Nulo(Descricao),
            BaseLegal = BaseLegal.Valor,
            ClassificacaoExigida = Classificacao.Valor,
            Ordem = ordem ?? 0,
            Ativo = Ativo,
            DoSistema = DoSistema,
            SomenteHistorico = SomenteHistorico
        };
    }
}

/// <summary>
/// Finalidades de tratamento (LGPD): para quê a pessoa é contatada. Nada é excluído: desativar esconde das concessões
/// novas. As de sistema (Marketing, Registro anterior) não mudam de código nem são desativadas.
/// </summary>
public sealed partial class FinalidadesTratamentoViewModel : CadastroViewModelBase<LinhaFinalidadeTratamento>
{
    /// <summary>Lista em colunas (padrão de tela de cadastro, 03/10/2026).</summary>
    protected override GradeCadastro<LinhaFinalidadeTratamento> CriarGradeDaLista() => new(
        "Finalidade", l => l.Id, l => l.Nome, l => l.Categoria,
        ColunaCadastro<LinhaFinalidadeTratamento>.Curto("emuso", "Em uso", l => l.Usos, 130),
        ColunaCadastro<LinhaFinalidadeTratamento>.Situacao(l => l.Ativo == "Sim", feminino: true));

    private readonly FinalidadesTratamentoApi _api;

    public FinalidadesTratamentoViewModel(FinalidadesTratamentoApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private FinalidadeTratamentoEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true, DoSistema: false };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaFinalidadeTratamento item) => item.Nome;

    protected override async Task<IReadOnlyList<LinhaFinalidadeTratamento>> ListarAsync() =>
        (await _api.ListarAsync(incluirInativas: true))
            .OrderBy(f => !f.Ativo).ThenBy(f => f.Ordem)
            .Select(f => new LinhaFinalidadeTratamento(f)).ToList();

    protected override async Task AbrirAsync(LinhaFinalidadeTratamento item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = FinalidadeTratamentoEdicao.Criar();
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

    private async Task<FinalidadeTratamentoEdicao> ObterAsync(Guid id) =>
        FinalidadeTratamentoEdicao.De(await _api.ObterAsync(id) ?? throw new ValidacaoException(["Esta finalidade não existe mais."]));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        FinalidadeTratamentoDto? salva = null;
        if (!await ExecutarAsync(async () => salva = await _api.SalvarAsync(formulario.ParaDto())))
            return;

        Formulario = FinalidadeTratamentoEdicao.De(salva!);
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Novo ? "Finalidade criada. Ela já pode receber consentimento (reabra a tela de Pessoas)." : "Alterações salvas.",
            TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
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
            Mostrar("Salve ou descarte as alterações antes de " + (desativar ? "desativar" : "reativar") + " a finalidade.", TipoMensagem.Aviso);
            return;
        }

        FinalidadeTratamentoDto? gravada = null;
        if (!await ExecutarAsync(async () => gravada = desativar
                ? await _api.DesativarAsync(formulario.Id, formulario.Versao)
                : await _api.ReativarAsync(formulario.Id, formulario.Versao)))
            return;

        Formulario = FinalidadeTratamentoEdicao.De(gravada!);
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Finalidade desativada." : "Finalidade reativada.", TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
    }
}
