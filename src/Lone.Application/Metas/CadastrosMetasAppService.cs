using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Metas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Metas;
using Lone.Domain.Validacao;

namespace Lone.Application.Metas;

public interface IEquipeAppService
{
    Task<List<EquipeDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default);
    Task<EquipeDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<EquipeDto> SalvarAsync(EquipeDto dto, CancellationToken ct = default);
    Task<EquipeDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<EquipeDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Equipes: nome único, líder, departamento, membros com vigência (sem sobreposição da mesma pessoa).</summary>
public sealed class EquipeAppService : IEquipeAppService
{
    private readonly IEquipeRepositorio _repositorio;
    private readonly IMetaConsultas _consultas;
    private readonly IAutorizacao _autorizacao;
    private readonly TimeProvider _relogio;

    public EquipeAppService(IEquipeRepositorio repositorio, IMetaConsultas consultas, IAutorizacao autorizacao, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _consultas = consultas;
        _autorizacao = autorizacao;
        _relogio = relogio;
    }

    private DateOnly Hoje => DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime);

    public async Task<List<EquipeDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.Visualizar);
        var equipes = (await _repositorio.ListarAsync(ct)).Where(e => incluirInativas || e.Ativo)
            .OrderBy(e => e.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();
        return await ParaDtosAsync(equipes, ct);
    }

    public async Task<EquipeDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.Visualizar);
        return await _repositorio.ObterAsync(id, ct) is { } e ? (await ParaDtosAsync([e], ct))[0] : null;
    }

    public async Task<EquipeDto> SalvarAsync(EquipeDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.Gerenciar);
        var todas = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : await _repositorio.ObterAsync(dto.Id, ct);
        var id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id;
        var dados = new Equipe
        {
            Id = id,
            Versao = dto.Versao,
            Nome = RegrasMeta.Texto(dto.Nome) ?? string.Empty,
            DepartamentoId = dto.DepartamentoId == Guid.Empty ? null : dto.DepartamentoId,
            EquipePaiId = dto.EquipePaiId == Guid.Empty ? null : dto.EquipePaiId,
            Ativo = anterior?.Ativo ?? true,
            Membros = dto.Membros.Select(m => new MembroEquipe
            {
                Id = m.Id == Guid.Empty ? IdSequencial.Novo() : m.Id, EquipeId = id, PessoaId = m.PessoaId, InicioEm = m.InicioEm,
                FimEm = m.FimEm, Papel = m.Papel
            }).ToList()
        };
        // Membros gravados nunca somem: o que não veio continua como estava.
        foreach (var gravado in anterior?.Membros ?? [])
            if (dados.Membros.All(m => m.Id != gravado.Id)) dados.Membros.Add(gravado);

        var erros = RegrasEquipe.Validar(dados, todas, anterior, Hoje);
        if (erros.Count > 0) throw new ValidacaoException(erros.Distinct().ToList());

        // O líder da lista e das metas é a cópia do líder vigente hoje (a verdade, com histórico, está nos membros).
        dados.LiderId = RegrasEquipe.LiderEm(dados, Hoje);

        if (anterior is null) dados.RegistrarEvento($"Equipe '{dados.Nome}' criada.");
        else if (anterior.Nome != dados.Nome) dados.RegistrarEvento($"Equipe '{anterior.Nome}' renomeada para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public Task<EquipeDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarAsync(id, requisicao, e => e.Desativar(), ct);

    public Task<EquipeDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarAsync(id, requisicao, e => e.Reativar(), ct);

    private async Task<EquipeDto> AlterarAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<Equipe> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Metas.Gerenciar);
        var equipe = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Esta equipe não existe mais."]);
        equipe.Versao = requisicao.Versao ?? equipe.Versao;
        acao(equipe);
        await _repositorio.SalvarAsync(equipe, novo: false, ct);
        return await ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    private async Task<List<EquipeDto>> ParaDtosAsync(IReadOnlyList<Equipe> equipes, CancellationToken ct)
    {
        var hoje = Hoje;
        var nomes = await _consultas.NomesPessoasAsync(
            equipes.SelectMany(e => e.Membros.Select(m => m.PessoaId)).Distinct().ToList(), ct);
        var nomesEquipes = equipes.Any(e => e.EquipePaiId is not null)
            ? (await _repositorio.ListarAsync(ct)).ToDictionary(e => e.Id, e => e.Nome)
            : new Dictionary<Guid, string>();
        return equipes.Select(e =>
        {
            // Líder de hoje pelos membros (a cópia gravada pode estar velha se a liderança venceu sem a equipe ser salva).
            var lider = RegrasEquipe.LiderEm(e, hoje);
            return new EquipeDto
            {
                Id = e.Id, Versao = e.Versao, Nome = e.Nome, DepartamentoId = e.DepartamentoId, LiderId = lider,
                Lider = lider is { } l ? nomes.GetValueOrDefault(l) : null, Ativo = e.Ativo,
                EquipePaiId = e.EquipePaiId, EquipePai = e.EquipePaiId is { } pai ? nomesEquipes.GetValueOrDefault(pai) : null,
                Membros = e.Membros.OrderByDescending(m => m.FimEm is null).ThenByDescending(m => m.Papel).ThenBy(m => nomes.GetValueOrDefault(m.PessoaId))
                    .Select(m => new MembroEquipeDto
                    {
                        Id = m.Id, PessoaId = m.PessoaId, Pessoa = nomes.GetValueOrDefault(m.PessoaId), InicioEm = m.InicioEm, FimEm = m.FimEm,
                        Papel = m.Papel
                    })
                    .ToList()
            };
        }).ToList();
    }
}

public interface IIndicadorAppService
{
    Task<List<IndicadorDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<IndicadorDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<IndicadorDto> SalvarAsync(IndicadorDto dto, CancellationToken ct = default);
    Task<IndicadorDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<IndicadorDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Indicadores: os de sistema (fontes do cadastro) nascem com a base; os informados o usuário cria.</summary>
public sealed class IndicadorAppService : IIndicadorAppService
{
    private readonly IIndicadorRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public IndicadorAppService(IIndicadorRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<List<IndicadorDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.Visualizar);
        return (await _repositorio.ListarAsync(ct)).Where(i => incluirInativos || i.Ativo)
            .OrderBy(i => i.Nome, StringComparer.CurrentCultureIgnoreCase).Select(ParaDto).ToList();
    }

    public async Task<IndicadorDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.Visualizar);
        return await _repositorio.ObterAsync(id, ct) is { } i ? ParaDto(i) : null;
    }

    public async Task<IndicadorDto> SalvarAsync(IndicadorDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Metas.Gerenciar);
        var todos = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : todos.FirstOrDefault(i => i.Id == dto.Id);
        var sistema = anterior is not null && IndicadoresSistema.Todos.Any(s => s.Id == anterior.Id);
        var dados = new Indicador
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            // Código e fonte não mudam depois de criados (integrações e metas antigas dependem deles).
            Codigo = anterior?.Codigo ?? Codigo(dto.Codigo),
            Nome = RegrasMeta.Texto(dto.Nome) ?? string.Empty,
            Fonte = anterior?.Fonte ?? FonteIndicador.Informado,
            Unidade = dto.Unidade,
            Sentido = sistema ? anterior!.Sentido : dto.Sentido,
            Ativo = anterior?.Ativo ?? true
        };

        var erros = new List<string>();
        if (dados.Codigo.Length == 0) erros.Add("Informe o código (ex.: FATURAMENTO).");
        else if (dados.Codigo.Length > Indicador.TamanhoMaximoCodigo) erros.Add($"O código pode ter no máximo {Indicador.TamanhoMaximoCodigo} caracteres.");
        if (dados.Nome.Length == 0) erros.Add("Informe o nome do indicador.");
        else if (dados.Nome.Length > Indicador.TamanhoMaximoNome) erros.Add($"O nome pode ter no máximo {Indicador.TamanhoMaximoNome} caracteres.");
        if (todos.Any(t => t.Id != dados.Id && t.Codigo == dados.Codigo)) erros.Add($"Já existe o indicador de código {dados.Codigo}.");
        if (todos.Any(t => t.Id != dados.Id && TextoBusca.Normalizar(t.Nome) == TextoBusca.Normalizar(dados.Nome))) erros.Add($"Já existe o indicador \"{dados.Nome}\".");
        if (erros.Count > 0) throw new ValidacaoException(erros);

        if (anterior is null) dados.RegistrarEvento($"Indicador '{dados.Nome}' criado.");
        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ObterAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public Task<IndicadorDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarAsync(id, requisicao, i => i.Desativar(), ct);

    public Task<IndicadorDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarAsync(id, requisicao, i => i.Reativar(), ct);

    private async Task<IndicadorDto> AlterarAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<Indicador> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Metas.Gerenciar);
        var indicador = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este indicador não existe mais."]);
        indicador.Versao = requisicao.Versao ?? indicador.Versao;
        acao(indicador);
        await _repositorio.SalvarAsync(indicador, novo: false, ct);
        return await ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    private static string Codigo(string? texto) =>
        new((texto ?? string.Empty).Trim().ToUpperInvariant().Replace(' ', '_').Where(c => char.IsAsciiLetterOrDigit(c) || c == '_').ToArray());

    public static IndicadorDto ParaDto(Indicador i) => new()
    {
        Id = i.Id, Versao = i.Versao, Codigo = i.Codigo, Nome = i.Nome, Fonte = i.Fonte, Unidade = i.Unidade, Sentido = i.Sentido,
        Ativo = i.Ativo, DoSistema = IndicadoresSistema.Todos.Any(s => s.Id == i.Id)
    };
}
