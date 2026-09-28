using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comercial;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Application.Comercial;

public interface ITipoCarteiraAppService
{
    Task<List<TipoCarteiraDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default);
    Task<TipoCarteiraDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<TipoCarteiraDto> SalvarAsync(TipoCarteiraDto dto, CancellationToken ct = default);
    Task<TipoCarteiraDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<TipoCarteiraDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>Papel comercial (tipo de carteira): permissão → normalização → regras → gravação (com eventos no histórico).</summary>
public sealed class TipoCarteiraAppService : ITipoCarteiraAppService
{
    private readonly ITipoCarteiraRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;
    private readonly TimeProvider _relogio;

    public TipoCarteiraAppService(ITipoCarteiraRepositorio repositorio, IAutorizacao autorizacao, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
        _relogio = relogio;
    }

    public async Task<List<TipoCarteiraDto>> ListarAsync(bool incluirInativos, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.Comercial);
        if (!gerencia) _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var todos = await _repositorio.ListarAsync(ct);
        var usos = gerencia ? await _repositorio.ContarUsosAsync(null, ct) : new Dictionary<Guid, int>();
        return todos.Where(x => incluirInativos || x.Ativo).OrderBy(x => x.Ordem).ThenBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase).Select(x => ParaDto(x, usos.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<TipoCarteiraDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        return await ReleAsync(id, ct);
    }

    public async Task<TipoCarteiraDto> SalvarAsync(TipoCarteiraDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        var todos = await _repositorio.ListarAsync(ct);
        var anterior = dto.Id == Guid.Empty ? null : todos.FirstOrDefault(x => x.Id == dto.Id);
        var dados = new TipoCarteira
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = Texto(dto.Nome),
            ResponsavelDaConta = dto.ResponsavelDaConta,
            Ordem = dto.Ordem,
            LimitePorVez = dto.LimitePorVez,
            TipoCredito = Enum.IsDefined(dto.TipoCredito) ? dto.TipoCredito : TipoCreditoComercial.Nenhum,
            PercentualPadrao = dto.PercentualPadrao,
            ContaParaMetas = dto.ContaParaMetas,
            Ativo = anterior?.Ativo ?? true
        };

        var erros = RegrasComercial.ValidarPapel(dados, todos);
        // Baixar o limite só quando nenhum cliente fica acima dele (de hoje em diante; o histórico não é revalidado).
        if (erros.Count == 0 && anterior is not null && dados.LimitePorVez is { } limite && (anterior.LimitePorVez is null || anterior.LimitePorVez > limite))
        {
            var hoje = DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime);
            var acima = RegrasComercial.ClientesAcimaDoLimite(await _repositorio.VinculosAtivosAsync(dados.Id, hoje, ct), limite, hoje);
            if (acima > 0)
                erros.Add($"{acima} cliente(s) têm mais de {limite} vínculo(s) de \"{dados.Nome}\" ao mesmo tempo, de hoje em diante. " +
                          "Encerre os excedentes na carteira desses clientes (ou transfira) antes de baixar o limite.");
        }
        if (erros.Count > 0) throw new ValidacaoException(erros);

        if (anterior is null) dados.RegistrarEvento($"Papel comercial '{dados.Nome}' criado.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Papel comercial '{anterior.Nome}' renomeado para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    public Task<TipoCarteiraDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Desativar(), ct);

    public Task<TipoCarteiraDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, x => x.Reativar(), ct);

    private async Task<TipoCarteiraDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, Action<TipoCarteira> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Comercial);
        var item = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        item.Versao = requisicao.Versao ?? item.Versao;
        acao(item);
        await _repositorio.SalvarAsync(item, novo: false, ct);
        return await ReleAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
    }

    private async Task<TipoCarteiraDto?> ReleAsync(Guid id, CancellationToken ct) =>
        await _repositorio.ObterAsync(id, ct) is { } item ? ParaDto(item, (await _repositorio.ContarUsosAsync(id, ct)).GetValueOrDefault(id)) : null;

    private static string Texto(string? s) =>
        string.IsNullOrWhiteSpace(s) ? string.Empty : string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static TipoCarteiraDto ParaDto(TipoCarteira x, int usos) => new()
    {
        Id = x.Id,
        Versao = x.Versao,
        Nome = x.Nome,
        ResponsavelDaConta = x.ResponsavelDaConta,
        Ordem = x.Ordem,
        LimitePorVez = x.LimitePorVez,
        TipoCredito = x.TipoCredito,
        PercentualPadrao = x.PercentualPadrao,
        ContaParaMetas = x.ContaParaMetas,
        Ativo = x.Ativo,
        QuantidadeUsos = usos
    };
}
