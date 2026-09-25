using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comercial;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Comercial;

public interface IPerfilComercialAppService
{
    Task<List<PerfilComercialDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<PerfilComercialDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<PerfilComercialDto> SalvarAsync(PerfilComercialDto dto, CancellationToken ct = default);
    Task<PerfilComercialDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<PerfilComercialDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Perfil comercial: permissão → normalização → regras → gravação (com eventos no histórico).</summary>
public sealed class PerfilComercialAppService : IPerfilComercialAppService
{
    private readonly IPerfilComercialRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;
    private readonly ICondicaoPagamentoRepositorio _condicoes;

    public PerfilComercialAppService(IPerfilComercialRepositorio repositorio, IAutorizacao autorizacao, ICondicaoPagamentoRepositorio condicoes)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
        _condicoes = condicoes;
    }

    public async Task<List<PerfilComercialDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.Comercial);
        if (!gerencia) _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var todos = await _repositorio.ListarAsync(ct);
        var usos = gerencia ? await _repositorio.ContarUsosAsync(null, ct) : new Dictionary<Guid, int>();
        return todos.Where(x => incluirInativos || x.Ativo).OrderBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase).Select(x => ParaDto(x, usos.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<PerfilComercialDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        return await ReleAsync(id, ct);
    }

    public async Task<PerfilComercialDto> SalvarAsync(PerfilComercialDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        var todos = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : todos.FirstOrDefault(x => x.Id == dto.Id);
        var dados = new PerfilComercial
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = Texto(dto.Nome),
            LimiteCredito = dto.LimiteCredito,
            DescontoMaximo = dto.DescontoMaximo,
            DiasMaximoAtraso = dto.DiasMaximoAtraso,
            CondicaoPagamentoId = dto.CondicaoPagamentoId == Guid.Empty ? null : dto.CondicaoPagamentoId,
            ExigeAprovacaoAcimaLimite = dto.ExigeAprovacaoAcimaLimite,
            Ativo = anterior?.Ativo ?? true
        };

        var erros = RegrasComercial.ValidarValores("Perfil", dados.LimiteCredito, dados.DescontoMaximo, dados.DiasMaximoAtraso);
        if (dados.Nome.Length == 0) erros.Add("Informe o nome do perfil.");
        else if (dados.Nome.Length > PerfilComercial.TamanhoMaximoNome) erros.Add($"O nome pode ter no máximo {PerfilComercial.TamanhoMaximoNome} caracteres.");
        if (dados.CondicaoPagamentoId is { } condicaoId &&
            (await _condicoes.ObterAsync(condicaoId, ct)) is not { } condicao)
            erros.Add("A condição de pagamento escolhida não existe mais.");
        if (dados.Nome.Length > 0 && todos.Any(t => t.Id != dados.Id && TextoBusca.Normalizar(t.Nome) == TextoBusca.Normalizar(dados.Nome)))
            erros.Add($"Já existe \"{dados.Nome}\" (ativo ou desativado; maiúsculas e acentos não contam).");
        if (erros.Count > 0) throw new ValidacaoException(erros);

        if (anterior is null) dados.RegistrarEvento($"Perfil comercial '{dados.Nome}' criado.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Perfil comercial '{anterior.Nome}' renomeado para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public Task<PerfilComercialDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Desativar(), ct);

    public Task<PerfilComercialDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Reativar(), ct);

    private async Task<PerfilComercialDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<PerfilComercial> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        var item = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        item.Versao = requisicao.Versao ?? item.Versao;
        acao(item);
        await _repositorio.SalvarAsync(item, novo: false, ct);
        return await ReleAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    private async Task<PerfilComercialDto?> ReleAsync(Guid id, CancellationToken ct) =>
        await _repositorio.ObterAsync(id, ct) is { } item ? ParaDto(item, (await _repositorio.ContarUsosAsync(id, ct)).GetValueOrDefault(id)) : null;

    private static string Texto(string? s) =>
        string.IsNullOrWhiteSpace(s) ? string.Empty : string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static PerfilComercialDto ParaDto(PerfilComercial x, int usos) => new()
    {
        Id = x.Id,
        Versao = x.Versao,
        Nome = x.Nome,
        LimiteCredito = x.LimiteCredito,
        DescontoMaximo = x.DescontoMaximo,
        DiasMaximoAtraso = x.DiasMaximoAtraso,
        CondicaoPagamentoId = x.CondicaoPagamentoId,
        ExigeAprovacaoAcimaLimite = x.ExigeAprovacaoAcimaLimite,
        Ativo = x.Ativo,
        QuantidadeUsos = usos
    };
}
