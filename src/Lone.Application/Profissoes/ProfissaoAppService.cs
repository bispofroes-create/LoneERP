using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Profissoes;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Profissoes;
using Lone.Domain.Validacao;

namespace Lone.Application.Profissoes;

public interface IProfissaoAppService
{
    /// <summary>
    /// Quem vê o cadastro de pessoas lê as profissões (para escolher); só quem gerencia profissões recebe a
    /// quantidade de cadastros de cada uma.
    /// </summary>
    Task<List<ProfissaoDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default);
    Task<ProfissaoDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<ProfissaoDto> SalvarAsync(ProfissaoDto dto, CancellationToken ct = default);
    Task<ProfissaoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<ProfissaoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<ResultadoMesclarProfissao> MesclarAsync(Guid origemId, MesclarProfissaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Cadastro de profissões: permissão → normalização → regras → gravação (com eventos no histórico).</summary>
public sealed class ProfissaoAppService : IProfissaoAppService
{
    private readonly IProfissaoRepositorio _repositorio;
    private readonly IOcupacaoCboRepositorio _cbo;
    private readonly IAutorizacao _autorizacao;

    public ProfissaoAppService(IProfissaoRepositorio repositorio, IOcupacaoCboRepositorio cbo, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _cbo = cbo;
        _autorizacao = autorizacao;
    }

    public async Task<List<ProfissaoDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.Profissoes);
        if (!gerencia) _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);

        var profissoes = await _repositorio.ListarAsync(incluirInativas, ct);
        var uso = gerencia ? await _repositorio.ContarPessoasAsync(null, ct) : new Dictionary<Guid, int>();
        var ocupacoes = await OcupacoesAsync(profissoes, ct);
        return profissoes.Select(p => ParaDto(p, uso.GetValueOrDefault(p.Id), ocupacoes)).ToList();
    }

    public async Task<ProfissaoDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Profissoes);
        var profissao = await _repositorio.ObterAsync(id, ct);
        return profissao is null ? null : await ParaDtoCompletoAsync(profissao, ct);
    }

    public async Task<ProfissaoDto> SalvarAsync(ProfissaoDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Profissoes);

        var anterior = dto.Id == Guid.Empty ? null : await _repositorio.ObterAsync(dto.Id, ct);
        var dados = new Profissao
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = dto.Nome,
            Descricao = dto.Descricao,
            OcupacaoCboId = dto.OcupacaoCboId,
            Ativo = anterior?.Ativo ?? true // ativar/desativar têm ações próprias
        };

        RegrasProfissao.Normalizar(dados);
        var ocupacao = dados.OcupacaoCboId is { } codigo
            ? (await _cbo.ObterAsync([codigo], ct)).GetValueOrDefault(codigo)
            : null;
        var erros = RegrasProfissao.Validar(dados, ocupacao);
        if (dados.Nome.Length > 0 && await _repositorio.NomeEmUsoAsync(dados.Nome, dados.Id, ct))
            erros.Add($"Já existe a profissão \"{dados.Nome}\" (ativa ou desativada; maiúsculas e acentos não contam). Use a existente ou reative-a.");
        if (erros.Count > 0)
            throw new ValidacaoException(erros);

        if (anterior is null)
            dados.RegistrarEvento($"Profissão '{dados.Nome}' criada.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Profissão '{anterior.Nome}' renomeada para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleAsync(dados.Id, ct);
    }

    public Task<ProfissaoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, p => p.Desativar(), ct);

    public Task<ProfissaoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, p => p.Reativar(), ct);

    public async Task<ResultadoMesclarProfissao> MesclarAsync(Guid origemId, MesclarProfissaoRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Profissoes);

        if (origemId == requisicao.DestinoId)
            throw new ValidacaoException(["Escolha outra profissão para receber os cadastros."]);
        var origem = await _repositorio.ObterAsync(origemId, ct) ?? throw new ValidacaoException(["Esta profissão não existe mais."]);
        var destino = await _repositorio.ObterAsync(requisicao.DestinoId, ct)
                      ?? throw new ValidacaoException(["A profissão escolhida para receber os cadastros não existe mais."]);
        if (!destino.Ativo)
            throw new ValidacaoException([$"A profissão \"{destino.Nome}\" está desativada: reative-a antes de mesclar nela."]);

        origem.Versao = requisicao.Versao ?? origem.Versao; // a versão que o usuário via: se mudou, dá conflito
        origem.Desativar();
        origem.RegistrarEvento($"Profissão '{origem.Nome}' mesclada em '{destino.Nome}': os cadastros passaram para '{destino.Nome}'.");
        destino.RegistrarEvento($"Profissão '{destino.Nome}' recebeu os cadastros da profissão '{origem.Nome}' (mesclagem).");

        var cadastros = await _repositorio.MesclarAsync(origem, destino, ct);
        return new ResultadoMesclarProfissao
        {
            Origem = await ReleAsync(origem.Id, ct),
            Destino = await ReleAsync(destino.Id, ct),
            CadastrosAlterados = cadastros
        };
    }

    private async Task<ProfissaoDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<Profissao> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Profissoes);
        var profissao = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Esta profissão não existe mais."]);
        profissao.Versao = requisicao.Versao ?? profissao.Versao;
        acao(profissao);
        await _repositorio.SalvarAsync(profissao, nova: false, ct);
        return await ReleAsync(id, ct);
    }

    private async Task<ProfissaoDto> ReleAsync(Guid id, CancellationToken ct)
    {
        var gravada = await _repositorio.ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
        return await ParaDtoCompletoAsync(gravada, ct);
    }

    private async Task<ProfissaoDto> ParaDtoCompletoAsync(Profissao profissao, CancellationToken ct) =>
        ParaDto(profissao,
            (await _repositorio.ContarPessoasAsync(profissao.Id, ct)).GetValueOrDefault(profissao.Id),
            await OcupacoesAsync([profissao], ct));

    private async Task<Dictionary<int, OcupacaoCbo>> OcupacoesAsync(IReadOnlyCollection<Profissao> profissoes, CancellationToken ct)
    {
        var codigos = profissoes.Select(p => p.OcupacaoCboId).OfType<int>().Distinct().ToList();
        return codigos.Count == 0 ? new() : await _cbo.ObterAsync(codigos, ct);
    }

    private static ProfissaoDto ParaDto(Profissao p, int quantidadePessoas, IReadOnlyDictionary<int, OcupacaoCbo> ocupacoes) => new()
    {
        Id = p.Id,
        Versao = p.Versao,
        Nome = p.Nome,
        Descricao = p.Descricao,
        OcupacaoCboId = p.OcupacaoCboId,
        OcupacaoCboTexto = p.OcupacaoCboId is { } codigo
            ? $"{OcupacaoCbo.Formatar(codigo)} · {(ocupacoes.TryGetValue(codigo, out var o) ? o.Titulo : "(fora da tabela)")}"
            : null,
        Ativo = p.Ativo,
        QuantidadePessoas = quantidadePessoas
    };
}
