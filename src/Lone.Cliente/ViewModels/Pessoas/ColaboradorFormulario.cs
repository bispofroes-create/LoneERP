using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Colaboradores;
using Lone.Domain.Comum;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Escolhas da aba "Colaborador" (empresas, gestores e a estrutura organizacional), lidas quando a aba abre.
/// Os itens desativados vêm junto só para mostrar o que já está gravado; as listas oferecem os ativos.
/// </summary>
public sealed class OpcoesColaborador
{
    public static readonly Opcao<Guid?> Nenhum = new(null, "—");

    public OpcoesColaborador(ColaboradorOpcoesDto dto)
    {
        Dados = dto;
    }

    public ColaboradorOpcoesDto Dados { get; }

    /// <summary>Lista com "—", os ativos e, se não estiver entre eles, o gravado (marcado como desativado).</summary>
    public static Opcao<Guid?>[] Lista<T>(IEnumerable<T> itens, Func<T, Guid> id, Func<T, string> texto, Func<T, bool> ativo, Guid? gravado) =>
    [
        Nenhum,
        .. itens.Where(i => ativo(i) || id(i) == gravado)
            .Select(i => new Opcao<Guid?>(id(i), ativo(i) ? texto(i) : texto(i) + " (desativado)"))
    ];
}

/// <summary>
/// Uma lotação (período): cargo, departamento, setor, centro de custo e gestor. Lotação gravada nunca é apagada;
/// a mudança de cargo/departamento é feita com "Nova lotação", que encerra a atual.
/// </summary>
public sealed partial class LotacaoFormulario : ItemDeLista
{
    private LotacaoDto _gravada;
    private OpcoesColaborador? _opcoes;
    private bool _montando;

    private LotacaoFormulario(LotacaoDto dados, bool gravada)
    {
        _gravada = dados;
        Gravada = gravada;
        Id = dados.Id;
        _inicioEm = TextoTela.Data(dados.InicioEm == default ? null : dados.InicioEm);
        _fimEm = TextoTela.Data(dados.FimEm);
        _resumoGravado = dados.Gestor is { } g ? "Gestor: " + g : string.Empty;
    }

    public static LotacaoFormulario De(LotacaoDto dados) => new(dados, gravada: true);

    /// <summary>Lotação nova com os mesmos dados de outra (mudança de um item só, ex.: novo cargo).</summary>
    public static LotacaoFormulario Nova(LotacaoFormulario? copiarDe, DateOnly? inicio)
    {
        var dados = copiarDe?.ParaDto() ?? new LotacaoDto();
        dados.Id = IdSequencial.Novo();
        dados.InicioEm = inicio ?? default;
        dados.FimEm = null;
        var nova = new LotacaoFormulario(dados, gravada: false);
        if (copiarDe?._opcoes is { } opcoes) nova.DefinirOpcoes(opcoes);
        return nova;
    }

    public Guid Id { get; }
    public bool Gravada { get; }
    public bool PodeRemover => !Gravada;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Periodo))] private string _inicioEm;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Periodo), nameof(EmAberto))] private string _fimEm;

    /// <summary>"desde 01/03/2026" ou "01/03/2025 a 28/02/2026".</summary>
    public string Periodo => string.IsNullOrWhiteSpace(FimEm) ? $"Desde {InicioEm}" : $"{InicioEm} a {FimEm}";
    public bool EmAberto => string.IsNullOrWhiteSpace(FimEm);

    /// <summary>Antes de ler as opções: só o gestor (os demais nomes chegam com a lista).</summary>
    [ObservableProperty] private string _resumoGravado;

    [ObservableProperty] private Opcao<Guid?>[] _cargos = [OpcoesColaborador.Nenhum];
    [ObservableProperty] private Opcao<Guid?> _cargo = OpcoesColaborador.Nenhum;
    [ObservableProperty] private Opcao<Guid?>[] _departamentos = [OpcoesColaborador.Nenhum];
    [ObservableProperty] private Opcao<Guid?> _departamento = OpcoesColaborador.Nenhum;
    [ObservableProperty] private Opcao<Guid?>[] _setores = [OpcoesColaborador.Nenhum];
    [ObservableProperty] private Opcao<Guid?> _setor = OpcoesColaborador.Nenhum;
    [ObservableProperty] private Opcao<Guid?>[] _centrosCusto = [OpcoesColaborador.Nenhum];
    [ObservableProperty] private Opcao<Guid?> _centroCusto = OpcoesColaborador.Nenhum;
    [ObservableProperty] private Opcao<Guid?>[] _gestores = [OpcoesColaborador.Nenhum];
    [ObservableProperty] private Opcao<Guid?> _gestor = OpcoesColaborador.Nenhum;

    public bool OpcoesCarregadas => _opcoes is not null;

    /// <summary>Monta as listas a partir das opções lidas (mantém o que já estava escolhido).</summary>
    public void DefinirOpcoes(OpcoesColaborador opcoes)
    {
        var atual = ParaDto();
        _opcoes = opcoes;
        _montando = true;
        try
        {
            var d = opcoes.Dados;
            Cargos = OpcoesColaborador.Lista(d.Cargos, c => c.Id, c => c.Nome, c => c.Ativo, _gravada.CargoId);
            Cargo = Escolher(Cargos, atual.CargoId);
            Departamentos = OpcoesColaborador.Lista(d.Departamentos, x => x.Id, x => x.Nome, x => x.Ativo, _gravada.DepartamentoId);
            Departamento = Escolher(Departamentos, atual.DepartamentoId);
            MontarSetores(atual.SetorId);
            // Só os analíticos recebem colaboradores (o gravado aparece mesmo que não seja mais).
            CentrosCusto = OpcoesColaborador.Lista(d.CentrosCusto, c => c.Id, c => $"{c.Codigo} {c.Nome}", c => c.Ativo && c.Analitico, _gravada.CentroCustoId);
            CentroCusto = Escolher(CentrosCusto, atual.CentroCustoId);
            var gestores = d.Gestores.Select(g => (g.Id, g.Nome)).ToList();
            if (_gravada.GestorId is { } gid && gestores.All(g => g.Id != gid))
                gestores.Add((gid, _gravada.Gestor ?? "(gestor gravado)"));
            Gestores = [OpcoesColaborador.Nenhum, .. gestores.Select(g => new Opcao<Guid?>(g.Id, g.Nome))];
            Gestor = Escolher(Gestores, atual.GestorId);
            ResumoGravado = string.Empty;
        }
        finally
        {
            _montando = false;
        }
        OnPropertyChanged(nameof(OpcoesCarregadas));
    }

    private static Opcao<Guid?> Escolher(Opcao<Guid?>[] lista, Guid? id) => lista.FirstOrDefault(o => o.Valor == id) ?? lista[0];

    /// <summary>A lista de setores acompanha o departamento escolhido.</summary>
    partial void OnDepartamentoChanged(Opcao<Guid?> value)
    {
        if (_opcoes is null || _montando) return;
        MontarSetores(Setor?.Valor);
    }

    private void MontarSetores(Guid? setorAtual)
    {
        if (_opcoes is null) return;
        var departamento = Departamento?.Valor;
        Setores = OpcoesColaborador.Lista(_opcoes.Dados.Setores.Where(s => s.DepartamentoId == departamento),
            s => s.Id, s => s.Nome, s => s.Ativo, _gravada.SetorId);
        Setor = Escolher(Setores, setorAtual);
    }

    /// <param name="inicioOpcional">Primeira lotação de um vínculo com admissão: vazio = começa na admissão.</param>
    public IEnumerable<string> Validar(string rotulo, bool inicioOpcional = false)
    {
        if (!TextoTela.TentarData(InicioEm, out var inicio) || (inicio is null && !inicioOpcional))
            yield return $"{rotulo}: informe o início da lotação (dd/mm/aaaa).";
        if (!TextoTela.TentarData(FimEm, out _)) yield return $"{rotulo}: fim da lotação inválido (use dd/mm/aaaa).";
    }

    public LotacaoDto ParaDto()
    {
        TextoTela.TentarData(InicioEm, out var inicio);
        TextoTela.TentarData(FimEm, out var fim);
        // Sem as opções (ainda não lidas ou falha ao ler), as escolhas gravadas voltam intactas.
        var carregadas = _opcoes is not null;
        return new LotacaoDto
        {
            Id = Id,
            InicioEm = inicio ?? default,
            FimEm = fim,
            CargoId = carregadas ? Cargo?.Valor : _gravada.CargoId,
            DepartamentoId = carregadas ? Departamento?.Valor : _gravada.DepartamentoId,
            SetorId = carregadas ? Setor?.Valor : _gravada.SetorId,
            CentroCustoId = carregadas ? CentroCusto?.Valor : _gravada.CentroCustoId,
            GestorId = carregadas ? Gestor?.Valor : _gravada.GestorId
        };
    }
}

/// <summary>
/// Vínculo com uma empresa do grupo: matrícula, tipo, admissão, desligamento e as lotações (a mais recente primeiro).
/// Vínculo gravado não é removido: o desligamento o encerra.
/// </summary>
public sealed partial class VinculoFormulario : ItemDeLista
{
    public static readonly Opcao<TipoVinculo>[] TiposVinculo =
    [
        new(TipoVinculo.Clt, "CLT"),
        new(TipoVinculo.Estagio, "Estágio"),
        new(TipoVinculo.Aprendiz, "Aprendiz"),
        new(TipoVinculo.Temporario, "Temporário"),
        new(TipoVinculo.PrestadorPj, "Prestador (PJ)"),
        new(TipoVinculo.Autonomo, "Autônomo"),
        new(TipoVinculo.Diretor, "Diretor / sócio"),
        new(TipoVinculo.Outro, "Outro")
    ];

    private readonly Guid _empresaGravada;
    private readonly string? _nomeEmpresaGravada;
    private OpcoesColaborador? _opcoes;

    private VinculoFormulario(Guid id, bool gravado, Guid empresa, string? nomeEmpresa)
    {
        Id = id;
        Gravado = gravado;
        _empresaGravada = empresa;
        _nomeEmpresaGravada = nomeEmpresa;
        _empresas = empresa == Guid.Empty ? [SemEmpresa] : [new Opcao<Guid>(empresa, nomeEmpresa ?? "(empresa gravada)")];
        _empresa = _empresas[0];
    }

    public static readonly Opcao<Guid> SemEmpresa = new(Guid.Empty, "—");

    /// <summary>Vínculo novo, com a primeira lotação (começa na admissão).</summary>
    public static VinculoFormulario Novo(OpcoesColaborador? opcoes)
    {
        var v = new VinculoFormulario(IdSequencial.Novo(), gravado: false, Guid.Empty, null);
        v.IncluirLotacao(LotacaoFormulario.Nova(null, null));
        if (opcoes is not null) v.DefinirOpcoes(opcoes);
        return v;
    }

    public static VinculoFormulario De(VinculoDto d)
    {
        var v = new VinculoFormulario(d.Id, gravado: true, d.EmpresaId, d.Empresa)
        {
            Matricula = d.Matricula ?? string.Empty,
            Tipo = Opcao.De(TiposVinculo, d.Tipo),
            AdmissaoEm = TextoTela.Data(d.AdmissaoEm),
            DesligamentoEm = TextoTela.Data(d.DesligamentoEm),
            MotivoDesligamento = d.MotivoDesligamento ?? string.Empty,
            JornadaSemanal = TextoTela.Numero(d.JornadaSemanal),
            Observacoes = d.Observacoes ?? string.Empty
        };
        foreach (var l in d.Lotacoes.OrderByDescending(l => l.InicioEm)) v.IncluirLotacao(LotacaoFormulario.De(l));
        return v;
    }

    public Guid Id { get; }
    public bool Gravado { get; }
    public bool PodeRemover => !Gravado;

    public IReadOnlyList<Opcao<TipoVinculo>> ListaTipos => TiposVinculo;

    [ObservableProperty] private Opcao<Guid>[] _empresas;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private Opcao<Guid> _empresa;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _matricula = string.Empty;
    [ObservableProperty] private Opcao<TipoVinculo> _tipo = TiposVinculo[0];
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _admissaoEm = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo), nameof(Desligado))] private string _desligamentoEm = string.Empty;
    [ObservableProperty] private string _motivoDesligamento = string.Empty;
    [ObservableProperty] private string _jornadaSemanal = string.Empty;
    [ObservableProperty] private string _observacoes = string.Empty;

    public bool Desligado => !string.IsNullOrWhiteSpace(DesligamentoEm);

    /// <summary>"Empresa X · matrícula 123 · desde 01/02/2024" (ou "até ..." quando desligado).</summary>
    public string Titulo => string.Join(" · ", new[]
    {
        Empresa.Valor == Guid.Empty ? "Novo vínculo" : Empresa.Texto,
        string.IsNullOrWhiteSpace(Matricula) ? string.Empty : "matrícula " + Matricula.Trim(),
        string.IsNullOrWhiteSpace(AdmissaoEm) ? string.Empty : Desligado ? $"{AdmissaoEm} a {DesligamentoEm}" : "desde " + AdmissaoEm
    }.Where(s => s.Length > 0));

    public ObservableCollection<LotacaoFormulario> Lotacoes { get; } = new();

    public void DefinirOpcoes(OpcoesColaborador opcoes)
    {
        _opcoes = opcoes;
        var atual = Empresa.Valor;
        var empresas = opcoes.Dados.Empresas.Where(e => e.Ativa || e.Id == _empresaGravada)
            .Select(e => new Opcao<Guid>(e.Id, e.Ativa ? e.Nome : e.Nome + " (inativa)")).ToList();
        if (_empresaGravada != Guid.Empty && empresas.All(e => e.Valor != _empresaGravada))
            empresas.Add(new Opcao<Guid>(_empresaGravada, _nomeEmpresaGravada ?? "(empresa gravada)"));
        Empresas = [SemEmpresa, .. empresas];
        Empresa = Empresas.FirstOrDefault(e => e.Valor == atual) ?? Empresas[0];
        foreach (var l in Lotacoes) l.DefinirOpcoes(opcoes);
    }

    private void IncluirLotacao(LotacaoFormulario lotacao, bool noInicio = false)
    {
        lotacao.AoRemover = () => RemoverLotacao(lotacao);
        if (noInicio) Lotacoes.Insert(0, lotacao);
        else Lotacoes.Add(lotacao);
    }

    /// <summary>A lotação encerrada por "Nova lotação" (volta a ficar aberta se a nova for removida antes de salvar).</summary>
    private readonly Dictionary<LotacaoFormulario, (LotacaoFormulario Anterior, string Fim)> _encerradas = new();

    /// <summary>Mudança de cargo, departamento, gestor...: encerra a atual na véspera e abre outra com os mesmos dados, desde hoje.</summary>
    [RelayCommand]
    private void NovaLotacao()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var atual = Lotacoes.FirstOrDefault(l => l.EmAberto);
        var nova = LotacaoFormulario.Nova(atual ?? Lotacoes.FirstOrDefault(), hoje);
        if (_opcoes is not null && !nova.OpcoesCarregadas) nova.DefinirOpcoes(_opcoes);
        if (atual is not null)
        {
            _encerradas[nova] = (atual, atual.FimEm);
            atual.FimEm = TextoTela.Data(hoje.AddDays(-1));
        }
        IncluirLotacao(nova, noInicio: true);
    }

    private void RemoverLotacao(LotacaoFormulario lotacao)
    {
        if (lotacao.Gravada) return; // período gravado é histórico: não sai
        if (_encerradas.Remove(lotacao, out var encerrada)) encerrada.Anterior.FimEm = encerrada.Fim;
        Lotacoes.Remove(lotacao);
    }

    public IEnumerable<string> Validar(string rotulo)
    {
        if (Empresa.Valor == Guid.Empty) yield return $"{rotulo}: escolha a empresa.";
        if (!TextoTela.TentarData(AdmissaoEm, out var admissao) || admissao is null) yield return $"{rotulo}: informe a admissão (dd/mm/aaaa).";
        if (!TextoTela.TentarData(DesligamentoEm, out _)) yield return $"{rotulo}: desligamento inválido (use dd/mm/aaaa).";
        if (!TextoTela.TentarDecimal(JornadaSemanal, out _)) yield return $"{rotulo}: jornada semanal inválida.";
        var primeira = Lotacoes.Count > 0 ? Lotacoes[^1] : null;
        foreach (var erro in Lotacoes.SelectMany(l => l.Validar(rotulo, inicioOpcional: ReferenceEquals(l, primeira) && admissao is not null)))
            yield return erro;
    }

    public VinculoDto ParaDto()
    {
        TextoTela.TentarData(AdmissaoEm, out var admissao);
        TextoTela.TentarData(DesligamentoEm, out var desligamento);
        TextoTela.TentarDecimal(JornadaSemanal, out var jornada);
        var lotacoes = Lotacoes.Select(l => l.ParaDto()).ToList();

        // A primeira lotação de um vínculo novo começa na admissão; no desligamento, a lotação aberta é encerrada.
        if (lotacoes.Count > 0 && lotacoes[^1].InicioEm == default && admissao is { } a) lotacoes[^1].InicioEm = a;
        if (desligamento is { } fim)
            foreach (var l in lotacoes.Where(l => l.FimEm is null)) l.FimEm = fim;

        return new VinculoDto
        {
            Id = Id,
            EmpresaId = Empresa.Valor,
            Matricula = TextoTela.Nulo(Matricula)?.Trim(),
            Tipo = Tipo.Valor,
            AdmissaoEm = admissao ?? default,
            DesligamentoEm = desligamento,
            MotivoDesligamento = desligamento is null ? null : TextoTela.Nulo(MotivoDesligamento)?.Trim(),
            JornadaSemanal = jornada,
            Observacoes = TextoTela.Nulo(Observacoes)?.Trim(),
            Lotacoes = lotacoes
        };
    }
}
