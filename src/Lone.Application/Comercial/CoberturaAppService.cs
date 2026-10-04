using Lone.Application.Empresas;
using Lone.Application.Metas;
using Lone.Application.Seguranca;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Comercial;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Application.Comercial;

public interface ICoberturaAppService
{
    Task<List<CoberturaDto>> ListarAsync(bool incluirEncerradas, CancellationToken ct = default);
    Task<CoberturaDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<CoberturaOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default);
    Task<CoberturaDto> SalvarAsync(CoberturaDto dto, CancellationToken ct = default);
    Task<CoberturaDto> CancelarAsync(Guid id, CancelarCoberturaRequisicao requisicao, CancellationToken ct = default);
    Task<CarteiraVencendoDto> CarteiraVencendoAsync(int? dias, CancellationToken ct = default);

    /// <summary>Coberturas vigentes e agendadas, em texto, para o aviso na carteira da ficha (sem exigir a permissão do módulo).</summary>
    Task<List<CoberturaAvisoDto>> AvisosAsync(CancellationToken ct = default);
}

/// <summary>
/// Ausências e coberturas da carteira (Motor Comercial, Fase 1c): permissão → normalização → regras (RegrasCobertura) →
/// referências (quem cobre pode ocupar o papel; equipe e tipo ativos) → gravação, com eventos no histórico.
/// </summary>
public sealed class CoberturaAppService : ICoberturaAppService
{
    private readonly ICoberturaRepositorio _repositorio;
    private readonly ICoberturaConsultas _consultas;
    private readonly ITipoAusenciaRepositorio _tipos;
    private readonly ITipoCarteiraRepositorio _papeis;
    private readonly IParametrosComerciaisRepositorio _parametros;
    private readonly IComercialConsultas _comercial;
    private readonly IEquipeRepositorio _equipes;
    private readonly IEmpresaConsultas _empresas;
    private readonly IAutorizacao _autorizacao;
    private readonly TimeProvider _relogio;
    private readonly IEscopoPessoas _escopo;

    public CoberturaAppService(ICoberturaRepositorio repositorio, ICoberturaConsultas consultas, ITipoAusenciaRepositorio tipos,
                               ITipoCarteiraRepositorio papeis, IParametrosComerciaisRepositorio parametros, IComercialConsultas comercial,
                               IEquipeRepositorio equipes, IEmpresaConsultas empresas, IAutorizacao autorizacao, TimeProvider relogio,
                               IEscopoPessoas escopo)
    {
        _escopo = escopo;
        _repositorio = repositorio;
        _consultas = consultas;
        _tipos = tipos;
        _papeis = papeis;
        _parametros = parametros;
        _comercial = comercial;
        _equipes = equipes;
        _empresas = empresas;
        _autorizacao = autorizacao;
        _relogio = relogio;
    }

    private DateOnly Hoje => DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime);

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    private void ExigirVer()
    {
        if (!_autorizacao.Possui(Permissoes.Comercial.Coberturas)) _autorizacao.Exigir(Permissoes.Comercial.Visualizar);
    }

    public async Task<List<CoberturaDto>> ListarAsync(bool incluirEncerradas, CancellationToken ct = default)
    {
        ExigirVer();
        var escopo = await _escopo.ObterAsync(ct);
        var lista = (await _repositorio.ListarAsync(Hoje, incluirEncerradas, ct)).Where(c => Visivel(escopo, c)).ToList();
        return await ParaDtosAsync(lista, ct);
    }

    public async Task<CoberturaDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        ExigirVer();
        var escopo = await _escopo.ObterAsync(ct);
        return await _repositorio.ObterAsync(id, ct) is { } item && Visivel(escopo, item) ? (await ParaDtosAsync([item], ct))[0] : null;
    }

    /// <summary>
    /// A cobertura aparece para o usuário (Fase 2a-3; alcance de hoje, E10): o titular ou quem cobre está no alcance, ou a
    /// cobertura foi dada a uma equipe de alguém do alcance. Fora disso, é como se não existisse.
    /// </summary>
    private static bool Visivel(EscopoResolvido escopo, CoberturaComercial c) =>
        escopo.AlcancaPessoa(c.TitularId) ||
        (c.SubstitutoId is { } s && escopo.AlcancaPessoa(s)) ||
        (c.EquipeSubstitutaId is { } e && escopo.EquipesDasPessoas.Contains(e));

    /// <summary>
    /// E11 (liderança temporal): cadastrar, alterar ou cancelar só a ausência de quem o usuário gerenciava no início dela (ele
    /// mesmo, ou membro de uma equipe que ele liderava naquele dia). Fora disso, responde como inexistente.
    /// </summary>
    /// <param name="gravada">
    /// Verdadeiro para a cobertura já gravada (fora do alcance = inexistente). Falso para o que o usuário está escolhendo
    /// agora (o titular é um colaborador das opções): aí a recusa explica a regra.
    /// </param>
    private async Task ExigirGerenciaAsync(Guid titularId, DateOnly inicio, bool gravada, CancellationToken ct)
    {
        if (titularId == Guid.Empty || inicio == default || await _escopo.GerenciaEmAsync(titularId, inicio, ct)) return;
        if (gravada) throw new ForaDoEscopoException();
        throw new ValidacaoException(["Você só pode lançar a ausência de quem estava na sua equipe (ou de você mesmo) no início dela."]);
    }

    public async Task<CoberturaOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default)
    {
        ExigirVer();
        var todos = await _papeis.ListarAsync(ct);
        var classificacoes = todos.Where(p => p.Ativo).SelectMany(p => p.ClassificacoesAceitas).Distinct().ToList();
        return new CoberturaOpcoesDto
        {
            Pessoas = await _comercial.ListarAtendentesAsync(classificacoes, ct),
            TiposAusencia = [.. (await _tipos.ListarAsync(ct)).OrderBy(t => t.Ordem).ThenBy(t => t.Nome).Select(t => TipoAusenciaAppService.ParaDto(t, 0))],
            Papeis = [.. todos.OrderBy(p => p.Ordem).ThenBy(p => p.Nome).Select(p => TipoCarteiraAppService.ParaDto(p, 0))],
            Equipes = [.. (await _equipes.ListarAsync(ct)).Where(e => e.Ativo).OrderBy(e => e.Nome).Select(e => new PessoaOpcaoDto(e.Id, e.Nome))],
            Empresas = await _empresas.ListarEmpresasAsync(ct),
            Parametros = ParametrosComerciaisAppService.ParaDto(await _parametros.ObterAsync(ct))
        };
    }

    public async Task<CoberturaDto> SalvarAsync(CoberturaDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Comercial.Coberturas);
        var anterior = dto.Id == Guid.Empty ? null : await _repositorio.ObterAsync(dto.Id, ct);
        var dados = new CoberturaComercial
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            TitularId = dto.TitularId,
            TipoAusenciaId = dto.TipoAusenciaId,
            InicioEm = dto.InicioEm,
            FimEm = dto.FimEm,
            SubstitutoId = dto.SubstitutoId,
            EquipeSubstitutaId = dto.EquipeSubstitutaId,
            TipoCarteiraId = dto.TipoCarteiraId,
            EmpresaId = dto.EmpresaId,
            RegraCredito = dto.RegraCredito,
            PercentualSubstituto = dto.PercentualSubstituto,
            PermiteAcesso = dto.PermiteAcesso,
            Observacao = dto.Observacao,
            // Cancelar tem ação própria: a gravação mantém a situação gravada.
            Cancelada = anterior?.Cancelada ?? false,
            MotivoCancelamento = anterior?.MotivoCancelamento,
            CriadoEm = anterior?.CriadoEm ?? default
        };
        if (anterior is not null) await ExigirGerenciaAsync(anterior.TitularId, anterior.InicioEm, gravada: true, ct);
        await ExigirGerenciaAsync(dados.TitularId, dados.InicioEm, gravada: false, ct);
        await ValidarAsync(dados, anterior, ct);

        var nomes = await NomesAsync(dados, ct);
        dados.RegistrarEvento(anterior is null
            ? $"Cobertura cadastrada: {nomes.Titular}, {RegrasCobertura.Descrever(dados, nomes.Tipo, nomes.QuemCobre)}."
            : anterior.FimEm != dados.FimEm
                ? $"Cobertura de {nomes.Titular}: fim mudou de {Data(anterior.FimEm)} para {Data(dados.FimEm)}."
                : $"Cobertura de {nomes.Titular} alterada.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleAsync(dados.Id, ct);
    }

    public async Task<CoberturaDto> CancelarAsync(Guid id, CancelarCoberturaRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Comercial.Coberturas);
        var anterior = await _repositorio.ObterAsync(id, ct);
        // Com alcance restrito, "não existe" e "fora do alcance" respondem igual (não revela quais existem).
        if (anterior is null)
        {
            if ((await _escopo.ObterAsync(ct)).Tudo) throw new ValidacaoException(["Esta cobertura não existe mais."]);
            throw new ForaDoEscopoException();
        }
        await ExigirGerenciaAsync(anterior.TitularId, anterior.InicioEm, gravada: true, ct);
        if (anterior.Cancelada) throw new ValidacaoException(["Esta cobertura já foi cancelada."]);
        if (string.IsNullOrWhiteSpace(requisicao.Motivo)) throw new ValidacaoException(["Informe o motivo do cancelamento."]);
        var dados = await _repositorio.ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
        dados.Versao = requisicao.Versao ?? dados.Versao;
        dados.Cancelada = true;
        dados.MotivoCancelamento = requisicao.Motivo;
        RegrasCobertura.Normalizar(dados);
        var erros = RegrasCobertura.Validar(dados, anterior, [], Hoje);
        if (erros.Count > 0) throw new ValidacaoException(erros);

        var nomes = await NomesAsync(dados, ct);
        dados.RegistrarEvento($"Cobertura de {nomes.Titular} ({Data(anterior.InicioEm)} a {Data(anterior.FimEm)}) cancelada: {dados.MotivoCancelamento}");
        await _repositorio.SalvarAsync(dados, novo: false, ct);
        return await ReleAsync(id, ct);
    }

    public async Task<CarteiraVencendoDto> CarteiraVencendoAsync(int? dias, CancellationToken ct = default)
    {
        ExigirVer();
        var parametros = await _parametros.ObterAsync(ct);
        var prazo = Math.Clamp(dias ?? parametros.DiasAvisoFimVinculo, 0, ParametrosComerciais.MaximoDiasAviso);
        var ate = Hoje.AddDays(prazo);
        return new CarteiraVencendoDto
        {
            Dias = prazo,
            DoAviso = dias is null,
            De = Hoje,
            Ate = ate,
            Vinculos = await _consultas.CarteiraVencendoAsync(Hoje, ate, await _escopo.ObterAsync(ct), ct)
        };
    }

    public async Task<List<CoberturaAvisoDto>> AvisosAsync(CancellationToken ct = default)
    {
        // Só as ausências de quem atende clientes no alcance (Fase 2a-3): o aviso aparece na carteira da ficha.
        var escopo = await _escopo.ObterAsync(ct);
        var titulares = escopo.Fontes.Select(f => f.VendedorId).ToHashSet();
        var lista = (await _repositorio.ListarAsync(Hoje, incluirEncerradas: false, ct))
            .Where(c => escopo.Tudo || titulares.Contains(c.TitularId)).ToList();
        if (lista.Count == 0) return [];
        var dtos = await ParaDtosAsync(lista, ct, contarClientes: false);
        return [.. lista.Zip(dtos, (c, d) => new CoberturaAvisoDto(c.TitularId, c.TipoCarteiraId, c.EmpresaId, c.InicioEm, c.FimEm,
            $"{d.Titular}: {RegrasCobertura.Descrever(c, d.TipoAusencia ?? "Ausência", d.QuemCobre ?? "?")}"))];
    }

    /// <summary>Relê depois de gravar (quem gravou já passou pela conferência de liderança; não depende do alcance de hoje).</summary>
    private async Task<CoberturaDto> ReleAsync(Guid id, CancellationToken ct) =>
        await _repositorio.ObterAsync(id, ct) is { } item ? (await ParaDtosAsync([item], ct))[0] : throw new ConflitoDeEdicaoException();

    // ---- Regras com referências ----

    private async Task ValidarAsync(CoberturaComercial dados, CoberturaComercial? anterior, CancellationToken ct)
    {
        RegrasCobertura.Normalizar(dados);
        var erros = RegrasCobertura.Validar(dados, anterior,
            dados.TitularId == Guid.Empty ? [] : await _repositorio.DoTitularAsync(dados.TitularId, ct), Hoje,
            (await _parametros.ObterAsync(ct)).DiasRetroativosMaximo);

        // Referências só quando mudam (a cobertura antiga continua valendo com o que tinha).
        if (dados.TipoAusenciaId != Guid.Empty && dados.TipoAusenciaId != anterior?.TipoAusenciaId &&
            await _tipos.ObterAsync(dados.TipoAusenciaId, ct) is not { Ativo: true })
            erros.Add("O tipo de ausência escolhido não existe mais ou está desativado.");

        var papeis = (await _papeis.ListarAsync(ct)).ToDictionary(p => p.Id);
        if (dados.TipoCarteiraId is { } papelId && papelId != anterior?.TipoCarteiraId && papeis.GetValueOrDefault(papelId) is not { Ativo: true })
            erros.Add("O papel comercial escolhido não existe mais ou está desativado.");

        var pessoas = await _comercial.PessoasElegiveisAsync(
            new[] { dados.TitularId, dados.SubstitutoId ?? Guid.Empty }.Where(id => id != Guid.Empty).Distinct().ToList(), ct);
        if (dados.TitularId != Guid.Empty && dados.TitularId != anterior?.TitularId && !pessoas.ContainsKey(dados.TitularId))
            erros.Add("Quem vai se ausentar precisa ser uma pessoa ativa.");

        if (dados.SubstitutoId is { } substituto && (substituto != anterior?.SubstitutoId || dados.TipoCarteiraId != anterior?.TipoCarteiraId))
        {
            // Quem cobre precisa poder ocupar o papel da cobertura (ou algum papel ativo, quando vale para todos).
            var aceitas = (dados.TipoCarteiraId is { } p && papeis.TryGetValue(p, out var papel)
                    ? papel.ClassificacoesAceitas
                    : papeis.Values.Where(x => x.Ativo).SelectMany(x => x.ClassificacoesAceitas))
                .ToHashSet();
            if (!pessoas.TryGetValue(substituto, out var pessoa))
                erros.Add("Quem cobre precisa ser uma pessoa ativa.");
            else if (!pessoa.Classificacoes.Any(aceitas.Contains))
                erros.Add($"{pessoa.Nome} não pode cobrir: não tem a classificação aceita pelo papel (Configurações do Comercial › Papéis comerciais › Quem pode ser).");
        }

        if (dados.EquipeSubstitutaId is { } equipe && equipe != anterior?.EquipeSubstitutaId &&
            await _equipes.ObterAsync(equipe, ct) is not { Ativo: true })
            erros.Add("A equipe escolhida não existe mais ou está desativada.");

        if (erros.Count > 0) throw new ValidacaoException(erros.Distinct().ToList());
    }

    private async Task<(string Titular, string Tipo, string QuemCobre)> NomesAsync(CoberturaComercial c, CancellationToken ct)
    {
        var dto = (await ParaDtosAsync([c], ct, contarClientes: false))[0];
        return (dto.Titular ?? "?", dto.TipoAusencia ?? "Ausência", dto.QuemCobre ?? "?");
    }

    private async Task<List<CoberturaDto>> ParaDtosAsync(IReadOnlyList<CoberturaComercial> lista, CancellationToken ct, bool contarClientes = true)
    {
        if (lista.Count == 0) return [];
        var pessoas = await _comercial.NomesAsync(
            lista.SelectMany(c => new[] { c.TitularId, c.SubstitutoId ?? Guid.Empty }).Where(id => id != Guid.Empty).Distinct().ToList(), ct);
        var equipes = await _consultas.NomesEquipesAsync(lista.Select(c => c.EquipeSubstitutaId).OfType<Guid>().Distinct().ToList(), ct);
        var tipos = (await _tipos.ListarAsync(ct)).ToDictionary(t => t.Id, t => t.Nome);
        var clientes = contarClientes ? await _consultas.ContarClientesAsync(lista, ct) : new Dictionary<Guid, int>();
        var hoje = Hoje;
        return [.. lista.Select(c => new CoberturaDto
        {
            Id = c.Id,
            Versao = c.Versao,
            TitularId = c.TitularId,
            TipoAusenciaId = c.TipoAusenciaId,
            InicioEm = c.InicioEm,
            FimEm = c.FimEm,
            SubstitutoId = c.SubstitutoId,
            EquipeSubstitutaId = c.EquipeSubstitutaId,
            TipoCarteiraId = c.TipoCarteiraId,
            EmpresaId = c.EmpresaId,
            RegraCredito = c.RegraCredito,
            PercentualSubstituto = c.PercentualSubstituto,
            PermiteAcesso = c.PermiteAcesso,
            Observacao = c.Observacao,
            Cancelada = c.Cancelada,
            MotivoCancelamento = c.MotivoCancelamento,
            Situacao = c.Situacao(hoje),
            Titular = pessoas.GetValueOrDefault(c.TitularId),
            QuemCobre = c.SubstitutoId is { } s ? pessoas.GetValueOrDefault(s)
                : c.EquipeSubstitutaId is { } e ? "equipe " + equipes.GetValueOrDefault(e, "?") : null,
            TipoAusencia = tipos.GetValueOrDefault(c.TipoAusenciaId),
            ClientesAfetados = clientes.GetValueOrDefault(c.Id)
        })];
    }
}
