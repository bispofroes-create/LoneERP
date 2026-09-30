using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.Sessao;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Profissoes;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da tabela de profissões (Profissão · CBO · Cadastros · Ativa).</summary>
public sealed class LinhaProfissao
{
    public LinhaProfissao(ProfissaoDto profissao) => Profissao = profissao;

    public ProfissaoDto Profissao { get; }
    public Guid Id => Profissao.Id;
    public string Nome => Profissao.Nome;
    public string Cbo => Profissao.OcupacaoCboId is { } codigo ? codigo.ToString("0000-00", CultureInfo.InvariantCulture) : "—";
    public string Cadastros => Profissao.QuantidadePessoas.ToString("N0", TextoTela.Brasil);
    public string Ativa => Profissao.Ativo ? "Sim" : "Não";
}

/// <summary>Ficha de uma profissão. As regras finais (nome único sem maiúsculas/acentos, CBO existente) são da API.</summary>
public sealed partial class ProfissaoEdicao : ObservableObject
{
    private ProfissaoEdicao(Guid id, bool nova)
    {
        Id = id;
        Nova = nova;
    }

    public Guid Id { get; }
    public bool Nova { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;
    public int QuantidadePessoas { get; private set; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private string _descricao = string.Empty;

    /// <summary>Ocupação da CBO (autocompletar pelo título ou pelo código).</summary>
    public SeletorDeLista Cbo { get; } = new() { Dica = "Digite parte do título ou o código e escolha" };

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Nova profissão" : Nome;
    public string SituacaoTexto => Nova ? "Nova profissão"
        : Ativo ? "Ativa"
        : "Desativada (não aparece para novas escolhas; continua nos cadastros que já a têm)";
    public string UsoTexto => Nova ? string.Empty
        : QuantidadePessoas switch { 0 => "Nenhum cadastro com esta profissão.", 1 => "1 cadastro com esta profissão.", var n => $"{n.ToString("N0", TextoTela.Brasil)} cadastros com esta profissão." };

    public static ItemSeletor ItemCbo(OcupacaoCboDto o) =>
        new(o.Codigo.ToString(CultureInfo.InvariantCulture), o.Texto, o.Codigo.ToString("000000", CultureInfo.InvariantCulture));

    public static ProfissaoEdicao Criar(IReadOnlyList<ItemSeletor> cbo)
    {
        var f = new ProfissaoEdicao(IdSequencial.Novo(), nova: true);
        f.Cbo.DefinirItens(cbo);
        return f;
    }

    public static ProfissaoEdicao De(ProfissaoDto p, IReadOnlyList<ItemSeletor> cbo)
    {
        var f = new ProfissaoEdicao(p.Id, nova: false)
        {
            Versao = p.Versao,
            Ativo = p.Ativo,
            QuantidadePessoas = p.QuantidadePessoas,
            Nome = p.Nome,
            Descricao = p.Descricao ?? string.Empty
        };
        f.Cbo.DefinirItens(cbo);
        if (p.OcupacaoCboId is { } codigo)
        {
            var chave = codigo.ToString(CultureInfo.InvariantCulture);
            f.Cbo.Definir(cbo.FirstOrDefault(i => i.Chave == chave) ?? new ItemSeletor(chave, p.OcupacaoCboTexto ?? chave));
        }
        return f;
    }

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome da profissão.");
        if (Cbo.Validar("Ocupação CBO") is { } cbo) erros.Add(cbo);
        return erros;
    }

    public ProfissaoDto ParaDto() => new()
    {
        Id = Id,
        Versao = Versao,
        Nome = Nome.Trim(),
        Descricao = TextoTela.Nulo(Descricao)?.Trim(),
        OcupacaoCboId = int.TryParse(Cbo.Chave, NumberStyles.None, CultureInfo.InvariantCulture, out var codigo) ? codigo : null,
        Ativo = Ativo,
        QuantidadePessoas = QuantidadePessoas
    };
}

/// <summary>
/// Cadastro de profissões: tabela (profissão, CBO, quantos cadastros a usam, ativa) e ficha para criar, alterar,
/// desativar, reativar e mesclar. Também importa a tabela oficial da CBO (arquivo do Ministério do Trabalho).
/// Nada é excluído: mesclar passa os cadastros para outra profissão e desativa esta.
/// </summary>
public sealed partial class ProfissoesViewModel : CadastroViewModelBase<LinhaProfissao>
{
    private readonly ProfissoesApi _api;
    private readonly SessaoCliente _sessao;
    private readonly IArquivos _arquivos;

    /// <summary>Lista inteira mais recente (a busca só filtra o que aparece; os destinos da mesclagem saem daqui).</summary>
    private IReadOnlyList<ProfissaoDto> _cadastro = [];

    /// <summary>Ocupações da CBO (lidas ao abrir a tela e depois de importar).</summary>
    private IReadOnlyList<ItemSeletor> _cbo = [];

    public ProfissoesViewModel(ProfissoesApi api, SessaoCliente sessao, IArquivos arquivos, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
        _sessao = sessao;
        _arquivos = arquivos;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar), nameof(PodeMesclar))]
    private ProfissaoEdicao? _formulario;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeMesclar))]
    private Opcao<Guid>[] _destinosMesclagem = [];

    [ObservableProperty] private Opcao<Guid>? _destinoMesclagem;

    [ObservableProperty] private string _situacaoCbo = string.Empty;

    public bool PodeDesativar => Formulario is { Nova: false, Ativo: true };
    public bool PodeReativar => Formulario is { Nova: false, Ativo: false };
    public bool PodeMesclar => Formulario is { Nova: false } && DestinosMesclagem.Length > 0;
    public bool PodeImportarCbo => _sessao.Possui(Permissoes.Cadastros.TabelasOficiais);

    protected override string TextoDeBusca(LinhaProfissao item) => item.Nome;

    /// <summary>Falha ao ler a CBO não impede a tela: a profissão só fica sem sugestões de ocupação.</summary>
    protected override async Task AntesDeListarAsync()
    {
        try { await CarregarCboAsync(); }
        catch (Exception ex) when (ex is not SessaoExpiradaException)
        {
            _cbo = [];
            SituacaoCbo = $"Não foi possível ler a tabela CBO: {ex.GetBaseException().Message}";
        }
    }

    private async Task CarregarCboAsync()
    {
        _cbo = (await _api.ListarCboAsync()).Select(ProfissaoEdicao.ItemCbo).ToList();
        var situacao = await _api.ObterSituacaoCboAsync();
        SituacaoCbo = situacao.Quantidade == 0
            ? "Tabela CBO ainda não importada. Baixe o arquivo \"CBO2002 - Ocupacao.csv\" no site da CBO (Ministério do Trabalho) e use \"Importar CBO\"."
            : $"Tabela CBO: {situacao.Quantidade.ToString("N0", TextoTela.Brasil)} ocupações" +
              (situacao.AtualizadaEm is { } em ? $", atualizada em {em.ToLocalTime().ToString("dd/MM/yyyy", TextoTela.Brasil)}." : ".");
    }

    protected override async Task<IReadOnlyList<LinhaProfissao>> ListarAsync()
    {
        _cadastro = await _api.ListarAsync(incluirInativas: true);
        return _cadastro
            .OrderBy(p => !p.Ativo).ThenBy(p => p.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new LinhaProfissao(p)).ToList();
    }

    protected override async Task AbrirAsync(LinhaProfissao item) => ExibirFicha(await ObterAsync(item.Id));

    protected override Task NovoItemAsync()
    {
        ExibirFicha(ProfissaoEdicao.Criar(_cbo));
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override object? FichaObservada => Formulario;
    protected override bool FichaNova => Formulario?.Nova ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Nova: false } formulario) return;
        ExibirFicha(await ObterAsync(formulario.Id));
    }

    private async Task<ProfissaoEdicao> ObterAsync(Guid id) =>
        ProfissaoEdicao.De(await _api.ObterAsync(id) ?? throw new ValidacaoException(["Esta profissão não existe mais."]), _cbo);

    /// <summary>Mostra a ficha e monta a lista de destinos da mesclagem (as outras profissões ativas).</summary>
    private void ExibirFicha(ProfissaoEdicao ficha)
    {
        Formulario = ficha;
        DestinoMesclagem = null;
        DestinosMesclagem = ficha.Nova
            ? []
            : _cadastro
                .Where(p => p.Ativo && p.Id != ficha.Id)
                .OrderBy(p => p.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Select(p => new Opcao<Guid>(p.Id, p.Nome))
                .ToArray();
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        ProfissaoDto? salva = null;
        if (!await ExecutarAsync(async () => salva = await _api.SalvarAsync(formulario.ParaDto())))
            return;

        await AtualizarListaAposGravarAsync();
        ExibirFicha(ProfissaoEdicao.De(salva!, _cbo));
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Nova ? "Profissão criada. Ela já pode ser escolhida nos cadastros de pessoas." : "Alterações salvas.",
            TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private Task DesativarAsync() => AlterarSituacaoAsync(desativar: true);

    [RelayCommand]
    private Task ReativarAsync() => AlterarSituacaoAsync(desativar: false);

    private async Task AlterarSituacaoAsync(bool desativar)
    {
        if (Formulario is not { Nova: false } formulario) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes de " + (desativar ? "desativar" : "reativar") + " a profissão.", TipoMensagem.Aviso);
            return;
        }

        if (desativar && !await ConfirmarAsync(
                "Desativar profissão",
                $"\"{formulario.Nome}\" deixará de aparecer para novas escolhas. Os cadastros que já a têm continuam com ela, e ela pode ser reativada.",
                "Desativar", "Cancelar"))
            return;

        ProfissaoDto? gravada = null;
        if (!await ExecutarAsync(async () => gravada = desativar
                ? await _api.DesativarAsync(formulario.Id, formulario.Versao)
                : await _api.ReativarAsync(formulario.Id, formulario.Versao)))
            return;

        await AtualizarListaAposGravarAsync();
        ExibirFicha(ProfissaoEdicao.De(gravada!, _cbo));
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Profissão desativada." : "Profissão reativada.", TipoMensagem.Sucesso);
    }

    /// <summary>
    /// Passa todos os cadastros desta profissão para a escolhida e desativa esta (para juntar duplicadas como
    /// "Adv." e "Advogado"). Cada cadastro alterado fica com a mudança no próprio histórico.
    /// </summary>
    [RelayCommand]
    private async Task MesclarAsync()
    {
        if (Formulario is not { Nova: false } formulario) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes de mesclar a profissão.", TipoMensagem.Aviso);
            return;
        }
        if (DestinoMesclagem is not { } destino)
        {
            Mostrar("Escolha a profissão que vai receber os cadastros.", TipoMensagem.Aviso);
            return;
        }

        if (!await ConfirmarAsync(
                "Mesclar profissões",
                $"Os cadastros com \"{formulario.Nome}\" ({formulario.QuantidadePessoas.ToString("N0", TextoTela.Brasil)}) passarão para \"{destino.Texto}\", " +
                $"e \"{formulario.Nome}\" será desativada. A mudança fica no histórico de cada cadastro. Continuar?",
                "Mesclar", "Cancelar"))
            return;

        ResultadoMesclarProfissao? resultado = null;
        if (!await ExecutarAsync(async () => resultado = await _api.MesclarAsync(formulario.Id, destino.Valor, formulario.Versao)))
            return;

        await AtualizarListaAposGravarAsync();
        ExibirFicha(ProfissaoEdicao.De(resultado!.Origem, _cbo));
        MarcarFichaSemAlteracoes();
        Mostrar($"{resultado.CadastrosAlterados.ToString("N0", TextoTela.Brasil)} cadastro(s) passaram para \"{resultado.Destino.Nome}\". " +
                $"\"{resultado.Origem.Nome}\" foi desativada.", TipoMensagem.Sucesso);
    }

    /// <summary>Importa o arquivo oficial da CBO: inclui, atualiza e desativa ocupações (nunca apaga).</summary>
    [RelayCommand]
    private async Task ImportarCboAsync()
    {
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações da profissão aberta antes de importar a CBO.", TipoMensagem.Aviso);
            return;
        }

        var arquivo = await _arquivos.EscolherAsync("Arquivo \"CBO2002 - Ocupacao.csv\" (site da CBO, Ministério do Trabalho)");
        if (arquivo is null) return;

        ResultadoImportacaoCbo? resultado = null;
        if (!await ExecutarAsync(async () =>
            {
                resultado = await _api.ImportarCboAsync(arquivo.Nome, arquivo.Conteudo);
                await CarregarCboAsync();
            }))
            return;

        Formulario?.Cbo.DefinirItens(_cbo); // a ficha aberta passa a sugerir a tabela nova
        var r = resultado!;
        Mostrar($"CBO importada: {r.Lidas.ToString("N0", TextoTela.Brasil)} ocupações lidas, {r.Incluidas} incluídas, " +
                $"{r.Alteradas} atualizadas, {r.Desativadas} desativadas" +
                (r.LinhasIgnoradas > 0 ? $" ({r.LinhasIgnoradas} linhas do arquivo ignoradas)." : "."),
            TipoMensagem.Sucesso);
    }
}
