using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.Sessao;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Consulta avançada de pessoas (tela própria): critérios tipados, "Carregar mais" (paginação por chave), filtros
/// salvos por usuário (com opção de compartilhar) e exportação CSV com permissão própria (registrada na auditoria).
/// </summary>
public sealed partial class ConsultaPessoasViewModel : ViewModelBase
{
    private static readonly Opcao<Guid?> Todos = new(null, "Todos");
    private static readonly Guid SemCarteiraId = new("00000000-0000-0000-0000-000000000001");

    public static readonly Opcao<bool?>[] SimNaoTodos = [new(null, "Tanto faz"), new(true, "Sim"), new(false, "Não")];

    public static readonly Opcao<NaturezaPessoa?>[] Naturezas =
        [new(null, "Todas"), new(NaturezaPessoa.Fisica, "Pessoa física"), new(NaturezaPessoa.Juridica, "Pessoa jurídica"), new(NaturezaPessoa.Estrangeiro, "Estrangeiro")];

    public static readonly Opcao<SituacaoPessoa[]>[] Situacoes =
    [
        new([], "Ativos e em análise"), new([SituacaoPessoa.Ativo], "Só ativos"), new([SituacaoPessoa.EmAnalise], "Em análise"),
        new([SituacaoPessoa.Inativo], "Inativos"),
        new([SituacaoPessoa.Ativo, SituacaoPessoa.EmAnalise, SituacaoPessoa.Inativo, SituacaoPessoa.Arquivado], "Todas")
    ];

    public static readonly Opcao<RegimeTributario?>[] Regimes =
    [
        new(null, "Todos"), new(RegimeTributario.SimplesNacional, "Simples Nacional"), new(RegimeTributario.Mei, "MEI"),
        new(RegimeTributario.RegimeNormal, "Regime normal"), new(RegimeTributario.NaoInformado, "Não informado")
    ];

    public static readonly Opcao<SituacaoRelacionamento?>[] Relacionamentos =
    [
        new(null, "Todos"), new(SituacaoRelacionamento.Ativo, "Ativo"), new(SituacaoRelacionamento.EmRisco, "Em risco"),
        new(SituacaoRelacionamento.Inativo, "Inativo"), new(SituacaoRelacionamento.SemInteracao, "Sem interação")
    ];

    private readonly ConsultaPessoasApi _api;
    private readonly SessaoCliente _sessao;
    private readonly IArquivos _arquivos;
    private readonly IDialogos _dialogos;
    private bool _carregado;
    private CriteriosPessoas? _criteriosDaLista;
    private string? _proximoNome;
    private Guid? _proximoId;

    public ConsultaPessoasViewModel(ConsultaPessoasApi api, SessaoCliente sessao, IArquivos arquivos, IDialogos dialogos)
    {
        _api = api;
        _sessao = sessao;
        _arquivos = arquivos;
        _dialogos = dialogos;
    }

    public bool PodeExportar => _sessao.Possui(Permissoes.Pessoas.Exportar);

    // ---- Critérios ----
    [ObservableProperty] private string _texto = string.Empty;
    [ObservableProperty] private Opcao<NaturezaPessoa?> _natureza = Naturezas[0];
    [ObservableProperty] private Opcao<SituacaoPessoa[]> _situacao = Situacoes[0];
    [ObservableProperty] private Opcao<Guid?>[] _papeis = [Todos];
    [ObservableProperty] private Opcao<Guid?> _papel = Todos;
    [ObservableProperty] private Opcao<Guid?>[] _etiquetas = [Todos];
    [ObservableProperty] private Opcao<Guid?> _etiqueta = Todos;
    [ObservableProperty] private string _uf = string.Empty;
    [ObservableProperty] private string _cnae = string.Empty;
    [ObservableProperty] private bool _somenteCnaePrincipal;
    [ObservableProperty] private Opcao<bool?> _produtorRural = SimNaoTodos[0];
    [ObservableProperty] private Opcao<RegimeTributario?> _regime = Regimes[0];
    [ObservableProperty] private Opcao<Guid?>[] _vendedores = [Todos];
    [ObservableProperty] private Opcao<Guid?> _vendedor = Todos;
    [ObservableProperty] private Opcao<SituacaoRelacionamento?> _relacionamento = Relacionamentos[0];
    [ObservableProperty] private string _semInteracaoDias = string.Empty;
    [ObservableProperty] private Opcao<bool?> _bloqueado = SimNaoTodos[0];
    [ObservableProperty] private bool _documentosVencidos;
    [ObservableProperty] private string _documentosVencendoDias = string.Empty;
    [ObservableProperty] private Opcao<Guid?>[] _campos = [Todos];
    [ObservableProperty] private Opcao<Guid?> _campo = Todos;
    [ObservableProperty] private string _campoValor = string.Empty;
    [ObservableProperty] private string _cadastradoDe = string.Empty;
    [ObservableProperty] private string _cadastradoAte = string.Empty;

    public IReadOnlyList<Opcao<NaturezaPessoa?>> ListaNaturezas => Naturezas;
    public IReadOnlyList<Opcao<SituacaoPessoa[]>> ListaSituacoes => Situacoes;
    public IReadOnlyList<Opcao<bool?>> ListaSimNao => SimNaoTodos;
    public IReadOnlyList<Opcao<RegimeTributario?>> ListaRegimes => Regimes;
    public IReadOnlyList<Opcao<SituacaoRelacionamento?>> ListaRelacionamentos => Relacionamentos;

    // ---- Filtros salvos ----
    public ObservableCollection<FiltroSalvoDto> Filtros { get; } = new();
    [ObservableProperty][NotifyPropertyChangedFor(nameof(PodeRemoverFiltro))] private FiltroSalvoDto? _filtroEscolhido;
    [ObservableProperty] private bool _compartilhar;
    public bool PodeRemoverFiltro => FiltroEscolhido is { Proprio: true };

    partial void OnFiltroEscolhidoChanged(FiltroSalvoDto? value)
    {
        if (value is null) return;
        Aplicar(value.Criterios);
        Compartilhar = value.Compartilhado;
    }

    // ---- Resultado ----
    public ObservableCollection<PessoaResumo> Resultado { get; } = new();
    [ObservableProperty] private string _totalTexto = string.Empty;
    [ObservableProperty] private bool _temMais;

    [RelayCommand]
    private async Task CarregarAsync()
    {
        if (_carregado) return;
        OpcoesConsultaPessoasDto? opcoes = null;
        if (!await ExecutarAsync(async () => opcoes = await _api.OpcoesAsync())) return;
        _carregado = true;
        Papeis = [Todos, .. opcoes!.Papeis.Select(o => new Opcao<Guid?>(o.Id, o.Nome))];
        Etiquetas = [Todos, .. opcoes.Etiquetas.Select(o => new Opcao<Guid?>(o.Id, o.Nome))];
        Vendedores = [Todos, new(SemCarteiraId, "(sem ninguém na carteira)"), .. opcoes.Vendedores.Select(o => new Opcao<Guid?>(o.Id, o.Nome))];
        Campos = [Todos, .. opcoes.CamposPesquisaveis.Select(o => new Opcao<Guid?>(o.Id, o.Nome))];
        Papel = Papeis[0]; Etiqueta = Etiquetas[0]; Vendedor = Vendedores[0]; Campo = Campos[0];
        Filtros.Clear();
        foreach (var f in opcoes.Filtros) Filtros.Add(f);
    }

    /// <summary>Critérios da tela. Nulo = algo digitado não é um número/data válido (a mensagem já foi mostrada).</summary>
    private CriteriosPessoas? Criterios()
    {
        var erros = new List<string>();
        if (!TextoTela.TentarInteiro(SemInteracaoDias, out var semInteracao)) erros.Add("\"Sem interação há\": informe os dias.");
        if (!TextoTela.TentarInteiro(DocumentosVencendoDias, out var vencendo)) erros.Add("\"Vencendo em\": informe os dias.");
        if (!TextoTela.TentarData(CadastradoDe, out var de)) erros.Add("\"Cadastrado de\": use dd/mm/aaaa.");
        if (!TextoTela.TentarData(CadastradoAte, out var ate)) erros.Add("\"Cadastrado até\": use dd/mm/aaaa.");
        if (erros.Count > 0)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return null;
        }
        return new CriteriosPessoas
        {
            Texto = TextoTela.Nulo(Texto)?.Trim(),
            Naturezas = Natureza.Valor is { } n ? new List<NaturezaPessoa> { n } : new List<NaturezaPessoa>(),
            Situacoes = [.. Situacao.Valor],
            PapeisIds = Papel.Valor is { } p ? new List<Guid> { p } : new List<Guid>(),
            EtiquetasIds = Etiqueta.Valor is { } e ? new List<Guid> { e } : new List<Guid>(),
            Uf = TextoTela.Nulo(Uf)?.Trim(),
            Cnae = TextoTela.Nulo(Cnae)?.Trim(),
            SomenteCnaePrincipal = SomenteCnaePrincipal,
            ProdutorRural = ProdutorRural.Valor,
            Regime = Regime.Valor,
            VendedorId = Vendedor.Valor is { } v && v != SemCarteiraId ? v : null,
            SemCarteira = Vendedor.Valor == SemCarteiraId,
            Relacionamento = Relacionamento.Valor,
            SemInteracaoDias = semInteracao,
            Bloqueado = Bloqueado.Valor,
            DocumentosVencidos = DocumentosVencidos,
            DocumentosVencendoDias = vencendo,
            CampoId = Campo.Valor,
            CampoValor = TextoTela.Nulo(CampoValor)?.Trim(),
            CadastradoDe = de,
            CadastradoAte = ate
        };
    }

    /// <summary>Põe na tela os critérios de um filtro salvo (o que a tela não mostra — várias escolhas — fica com a primeira).</summary>
    private void Aplicar(CriteriosPessoas c)
    {
        Opcao<Guid?> Escolher(Opcao<Guid?>[] lista, Guid? id) => lista.FirstOrDefault(o => o.Valor == id) ?? lista[0];
        Texto = c.Texto ?? string.Empty;
        Natureza = Naturezas.FirstOrDefault(o => c.Naturezas.Count > 0 && o.Valor == c.Naturezas[0]) ?? Naturezas[0];
        Situacao = Situacoes.FirstOrDefault(o => o.Valor.OrderBy(x => x).SequenceEqual(c.Situacoes.OrderBy(x => x))) ?? Situacoes[0];
        Papel = Escolher(Papeis, c.PapeisIds.Count > 0 ? c.PapeisIds[0] : null);
        Etiqueta = Escolher(Etiquetas, c.EtiquetasIds.Count > 0 ? c.EtiquetasIds[0] : null);
        Uf = c.Uf ?? string.Empty;
        Cnae = c.Cnae ?? string.Empty;
        SomenteCnaePrincipal = c.SomenteCnaePrincipal;
        ProdutorRural = SimNaoTodos.First(o => o.Valor == c.ProdutorRural);
        Regime = Regimes.First(o => o.Valor == c.Regime);
        Vendedor = c.SemCarteira ? Escolher(Vendedores, SemCarteiraId) : Escolher(Vendedores, c.VendedorId);
        Relacionamento = Relacionamentos.First(o => o.Valor == c.Relacionamento);
        SemInteracaoDias = TextoTela.Inteiro(c.SemInteracaoDias);
        Bloqueado = SimNaoTodos.First(o => o.Valor == c.Bloqueado);
        DocumentosVencidos = c.DocumentosVencidos;
        DocumentosVencendoDias = TextoTela.Inteiro(c.DocumentosVencendoDias);
        Campo = Escolher(Campos, c.CampoId);
        CampoValor = c.CampoValor ?? string.Empty;
        CadastradoDe = TextoTela.Data(c.CadastradoDe);
        CadastradoAte = TextoTela.Data(c.CadastradoAte);
    }

    [RelayCommand]
    private void Limpar()
    {
        FiltroEscolhido = null;
        Aplicar(new CriteriosPessoas());
        Resultado.Clear();
        TotalTexto = string.Empty;
        TemMais = false;
        LimparMensagem();
    }

    [RelayCommand]
    private async Task PesquisarAsync()
    {
        if (Criterios() is not { } criterios) return;
        PaginaPessoas? pagina = null;
        if (!await ExecutarAsync(async () => pagina = await _api.ConsultarAsync(new ConsultaPessoasRequisicao
            {
                Criterios = criterios, Limite = ConsultaPessoasRequisicao.LimitePadrao, ContarTotal = true
            })))
            return;
        _criteriosDaLista = criterios;
        Resultado.Clear();
        Receber(pagina!);
        TotalTexto = pagina!.Total is { } t ? $"{t.ToString("N0", TextoTela.Brasil)} pessoa(s) encontrada(s)." : string.Empty;
    }

    [RelayCommand]
    private async Task CarregarMaisAsync()
    {
        if (_criteriosDaLista is null || !TemMais) return;
        PaginaPessoas? pagina = null;
        if (!await ExecutarAsync(async () => pagina = await _api.ConsultarAsync(new ConsultaPessoasRequisicao
            {
                Criterios = _criteriosDaLista, AposNome = _proximoNome, AposId = _proximoId, Limite = ConsultaPessoasRequisicao.LimitePadrao
            })))
            return;
        Receber(pagina!);
    }

    private void Receber(PaginaPessoas pagina)
    {
        foreach (var p in pagina.Itens) Resultado.Add(p);
        _proximoNome = pagina.ProximoNome;
        _proximoId = pagina.ProximoId;
        TemMais = pagina.ProximoId is not null;
    }

    [RelayCommand]
    private async Task ExportarAsync()
    {
        if (!PodeExportar || Criterios() is not { } criterios) return;
        if (!await _dialogos.ConfirmarAsync("Exportar", "Exportar o resultado em CSV? A exportação fica registrada na auditoria (quem, quando e os critérios).",
                "Exportar", "Cancelar"))
            return;
        ArquivoExportado? arquivo = null;
        if (!await ExecutarAsync(async () =>
            {
                arquivo = await _api.ExportarAsync(criterios);
                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(arquivo.Conteudo)).ToArray();
                await _arquivos.AbrirAsync(arquivo.NomeArquivo, bytes);
            }))
            return;
        Mostrar($"{arquivo!.Linhas.ToString("N0", TextoTela.Brasil)} linha(s) exportada(s) em {arquivo.NomeArquivo}.", TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private async Task SalvarFiltroAsync()
    {
        if (Criterios() is not { } criterios) return;
        var proprio = FiltroEscolhido is { Proprio: true } ? FiltroEscolhido : null;
        var nome = await _dialogos.PerguntarAsync("Salvar filtro",
            proprio is null ? "Nome do filtro (ex.: Clientes SP sem contato há 90 dias):" : "Nome do filtro (mesmo nome = atualiza este):",
            "Salvar", "Cancelar", proprio?.Nome, 80);
        if (string.IsNullOrWhiteSpace(nome)) return;
        var atualizar = proprio is not null && TextoBusca.Normalizar(nome) == TextoBusca.Normalizar(proprio.Nome);
        FiltroSalvoDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarFiltroAsync(new FiltroSalvoDto
            {
                Id = atualizar ? proprio!.Id : IdSequencial.Novo(),
                Versao = atualizar ? proprio!.Versao : null,
                Nome = nome.Trim(),
                Compartilhado = Compartilhar,
                Criterios = criterios
            })))
            return;
        var existente = Filtros.FirstOrDefault(f => f.Id == salvo!.Id);
        if (existente is not null) Filtros[Filtros.IndexOf(existente)] = salvo!;
        else Filtros.Insert(0, salvo!);
        FiltroEscolhido = salvo;
        Mostrar("Filtro salvo.", TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private async Task RemoverFiltroAsync()
    {
        if (FiltroEscolhido is not { Proprio: true } filtro) return;
        if (!await _dialogos.ConfirmarAsync("Remover filtro", $"Remover o filtro \"{filtro.Nome}\"?", "Remover", "Cancelar")) return;
        if (!await ExecutarAsync(() => _api.DesativarFiltroAsync(filtro.Id))) return;
        Filtros.Remove(filtro);
        FiltroEscolhido = null;
        Mostrar("Filtro removido.", TipoMensagem.Sucesso);
    }
}
