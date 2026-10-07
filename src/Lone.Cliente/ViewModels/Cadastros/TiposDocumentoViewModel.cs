using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Documentos;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Lone.Cliente.Grade;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da tabela de tipos de documento (Tipo · Em uso · Ativo).</summary>
public sealed class LinhaTipoDocumento
{
    public LinhaTipoDocumento(TipoDocumentoDto tipo) => Tipo = tipo;

    public TipoDocumentoDto Tipo { get; }
    public Guid Id => Tipo.Id;
    public string Nome => Tipo.Nome;
    public string Validade => Tipo.ExigeValidade ? $"Exige · aviso {Tipo.DiasAvisoVencimento} dia(s)" : $"Opcional · aviso {Tipo.DiasAvisoVencimento} dia(s)";
    public string Sistema => Tipo.TipoSistema is null ? string.Empty : "do sistema";
    public string Usos => Tipo.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Tipo.Ativo ? "Sim" : "Não";
}

/// <summary>
/// Ficha de um tipo de documento. As regras finais (nome único, nome e alcance fixos dos tipos de sistema, número repetido)
/// são da API; aqui só a forma da tela (P1-8B: "Quem pode usar", "Campos do documento", "Número e duplicidade", "Validade").
/// </summary>
public sealed partial class TipoDocumentoEdicao : ObservableObject
{
    private TipoDocumentoEdicao(Guid id, bool novo)
    {
        Id = id;
        Novo = novo;
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;
    public int QuantidadeUsos { get; private set; }

    /// <summary>RG, CNH, Passaporte, Documento estrangeiro, Outro: nome e "quem pode usar" fixos (D1); o resto configurável.</summary>
    public bool DoSistema { get; private set; }

    public bool PodeEditarIdentidade => !DoSistema;

    // ---- Opções com rótulos para o usuário (sem termos de banco) ----

    public static readonly Opcao<UsoCampoDocumento>[] Usos =
    [
        new(UsoCampoDocumento.Oculto, "Não usar"),
        new(UsoCampoDocumento.Opcional, "Opcional"),
        new(UsoCampoDocumento.Obrigatorio, "Obrigatório")
    ];

    public static readonly Opcao<FormatoNumeroDocumento>[] Formatos =
    [
        new(FormatoNumeroDocumento.Livre, "Livre (qualquer texto)"),
        new(FormatoNumeroDocumento.Alfanumerico, "Letras e números"),
        new(FormatoNumeroDocumento.SomenteDigitos, "Só números")
    ];

    /// <summary>Sem opção por país (não existe cadastro estruturado de países).</summary>
    public static readonly Opcao<UnicidadeDocumento>[] Unicidades =
    [
        new(UnicidadeDocumento.Nenhuma, "Não verificar"),
        new(UnicidadeDocumento.Aviso, "Avisar (deixa gravar)"),
        new(UnicidadeDocumento.PorTipo, "Bloquear para este tipo"),
        new(UnicidadeDocumento.PorTipoEUf, "Bloquear para este tipo e UF")
    ];

    /// <summary>As listas para os seletores da tela (o Picker precisa de IList).</summary>
    public Opcao<UsoCampoDocumento>[] ListaUsos => Usos;
    public Opcao<FormatoNumeroDocumento>[] ListaFormatos => Formatos;
    public Opcao<UnicidadeDocumento>[] ListaUnicidades => Unicidades;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private string _ordem = string.Empty;
    [ObservableProperty] private bool _exigeValidade;
    [ObservableProperty] private string _diasAvisoVencimento = "30";

    [ObservableProperty] private bool _aplicaPessoaFisica = true;
    [ObservableProperty] private bool _aplicaPessoaJuridica = true;
    [ObservableProperty] private bool _aplicaEstrangeiro = true;
    [ObservableProperty] private Opcao<UsoCampoDocumento> _usoOrgaoEmissor = Usos[0];
    [ObservableProperty] private Opcao<UsoCampoDocumento> _usoUf = Usos[0];
    [ObservableProperty] private Opcao<UsoCampoDocumento> _usoEmissao = Usos[1];
    [ObservableProperty] private Opcao<FormatoNumeroDocumento> _formatoNumero = Formatos[0];
    [ObservableProperty] private string _tamanhoMinimoNumero = string.Empty;
    [ObservableProperty] private string _tamanhoMaximoNumero = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExplicacaoUnicidade))]
    private Opcao<UnicidadeDocumento> _unicidade = Unicidades[0];

    /// <summary>O que acontece com o número repetido (antes de ligar o bloqueio, a consequência fica à vista).</summary>
    public string ExplicacaoUnicidade => Unicidade.Valor switch
    {
        UnicidadeDocumento.Aviso =>
            "Ao gravar um número igual ao de outra pessoa (pontos, traços e espaços não contam), aparece um aviso de possível duplicidade; a gravação continua.",
        UnicidadeDocumento.PorTipo =>
            "Bloqueia: o mesmo número não pode estar em duas pessoas neste tipo. Ao salvar, o sistema confere os documentos atuais " +
            "e recusa se já houver repetidos (nada é corrigido automaticamente).",
        UnicidadeDocumento.PorTipoEUf =>
            "Bloqueia o mesmo número em duas pessoas na mesma UF (exige a UF como obrigatória). Ao salvar, o sistema confere os " +
            "documentos atuais e recusa se já houver repetidos (nada é corrigido automaticamente).",
        _ => "Não confere número repetido entre pessoas. Na mesma pessoa, o mesmo número do mesmo tipo nunca é aceito."
    };

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo tipo" : Nome;
    public string SituacaoTexto => Novo ? "Novo tipo" : Ativo ? "Ativo" : "Desativado (não aparece para novas escolhas; continua nos documentos que já o têm)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} documento(s) ativos com este tipo.";

    public static TipoDocumentoEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static TipoDocumentoEdicao De(TipoDocumentoDto t) => new(t.Id, novo: false)
    {
        Versao = t.Versao,
        Ativo = t.Ativo,
        QuantidadeUsos = t.QuantidadeUsos,
        DoSistema = t.TipoSistema is not null,
        ExigeValidade = t.ExigeValidade,
        DiasAvisoVencimento = t.DiasAvisoVencimento.ToString(CultureInfo.InvariantCulture),
        Nome = t.Nome,
        Ordem = t.Ordem.ToString(CultureInfo.InvariantCulture),
        AplicaPessoaFisica = t.AplicaPessoaFisica,
        AplicaPessoaJuridica = t.AplicaPessoaJuridica,
        AplicaEstrangeiro = t.AplicaEstrangeiro,
        UsoOrgaoEmissor = Opcao.De(Usos, t.UsoOrgaoEmissor),
        UsoUf = Opcao.De(Usos, t.UsoUf),
        UsoEmissao = Opcao.De(Usos, t.UsoEmissao),
        FormatoNumero = Opcao.De(Formatos, t.FormatoNumero),
        TamanhoMinimoNumero = t.TamanhoMinimoNumero?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        TamanhoMaximoNumero = t.TamanhoMaximoNumero?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        Unicidade = Opcao.De(Unicidades, t.Unicidade)
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome do tipo.");
        if (!string.IsNullOrWhiteSpace(Ordem) && !TextoTela.TentarInteiro(Ordem, out _)) erros.Add("Ordem: use um número inteiro.");
        if (!TextoTela.TentarInteiro(DiasAvisoVencimento, out var dias) || dias is null or < 0)
            erros.Add("Dias de aviso: use um número inteiro (0 = avisar só quando vencer).");
        if (!TextoTela.TentarInteiro(TamanhoMinimoNumero, out _) || !TextoTela.TentarInteiro(TamanhoMaximoNumero, out _))
            erros.Add("Tamanho do número: use um número inteiro (vazio = sem limite).");
        return erros;
    }

    public TipoDocumentoDto ParaDto()
    {
        TextoTela.TentarInteiro(Ordem, out var ordem);
        TextoTela.TentarInteiro(DiasAvisoVencimento, out var dias);
        TextoTela.TentarInteiro(TamanhoMinimoNumero, out var minimo);
        TextoTela.TentarInteiro(TamanhoMaximoNumero, out var maximo);
        return new TipoDocumentoDto
        {
            Id = Id,
            Versao = Versao,
            Nome = Nome.Trim(),
            Ordem = ordem ?? 0,
            Ativo = Ativo,
            ExigeValidade = ExigeValidade,
            DiasAvisoVencimento = dias ?? 30,
            AplicaPessoaFisica = AplicaPessoaFisica,
            AplicaPessoaJuridica = AplicaPessoaJuridica,
            AplicaEstrangeiro = AplicaEstrangeiro,
            UsoOrgaoEmissor = UsoOrgaoEmissor.Valor,
            UsoUf = UsoUf.Valor,
            UsoEmissao = UsoEmissao.Valor,
            FormatoNumero = FormatoNumero.Valor,
            TamanhoMinimoNumero = minimo,
            TamanhoMaximoNumero = maximo,
            Unicidade = Unicidade.Valor
        };
    }
}

/// <summary>Tipos de documento (RG, CNH, Alvará...). Nada é excluído: desativar esconde das escolhas novas.</summary>
public sealed partial class TiposDocumentoViewModel : CadastroViewModelBase<LinhaTipoDocumento>
{
    /// <summary>Lista em colunas (padrão de tela de cadastro, 03/10/2026).</summary>
    protected override GradeCadastro<LinhaTipoDocumento> CriarGradeDaLista() => new(
        "Tipo de documento", l => l.Id, l => l.Nome, l => l.Validade,
        ColunaCadastro<LinhaTipoDocumento>.Curto("emuso", "Em uso", l => l.Usos, 130),
        ColunaCadastro<LinhaTipoDocumento>.Situacao(l => l.Ativo == "Sim"));

    private readonly TiposDocumentoApi _api;

    public TiposDocumentoViewModel(TiposDocumentoApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private TipoDocumentoEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaTipoDocumento item) => item.Nome;

    protected override async Task<IReadOnlyList<LinhaTipoDocumento>> ListarAsync() =>
        (await _api.ListarAsync(incluirInativos: true))
            .OrderBy(t => !t.Ativo).ThenBy(t => t.Ordem)
            .Select(t => new LinhaTipoDocumento(t)).ToList();

    protected override async Task AbrirAsync(LinhaTipoDocumento item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = TipoDocumentoEdicao.Criar();
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

    private async Task<TipoDocumentoEdicao> ObterAsync(Guid id) =>
        TipoDocumentoEdicao.De(await _api.ObterAsync(id) ?? throw new ValidacaoException(["Este tipo não existe mais."]));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        TipoDocumentoDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(formulario.ParaDto())))
            return;

        Formulario = TipoDocumentoEdicao.De(salvo!);
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Novo ? "Tipo criado. Ele já pode ser escolhido nos documentos (reabra a tela de Pessoas)." : "Alterações salvas.",
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
            Mostrar("Salve ou descarte as alterações antes de " + (desativar ? "desativar" : "reativar") + " o tipo.", TipoMensagem.Aviso);
            return;
        }

        TipoDocumentoDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync(formulario.Id, formulario.Versao)
                : await _api.ReativarAsync(formulario.Id, formulario.Versao)))
            return;

        Formulario = TipoDocumentoEdicao.De(gravado!);
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Tipo desativado." : "Tipo reativado.", TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
    }
}
