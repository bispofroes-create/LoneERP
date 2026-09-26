using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Privacidade;
using Lone.Domain.Validacao;

namespace Lone.Application.Privacidade;

/// <summary>Persistência das finalidades de tratamento. Nada é apagado: finalidades são desativadas.</summary>
public interface IFinalidadeTratamentoRepositorio
{
    /// <summary>Na ordem do cadastro, sem rastreamento.</summary>
    Task<List<FinalidadeTratamento>> ListarAsync(bool incluirInativas, CancellationToken ct);

    Task<FinalidadeTratamento?> ObterAsync(Guid id, CancellationToken ct);

    /// <summary>Nome ou código já usado por outra finalidade (nome sem diferenciar maiúsculas nem acentos).</summary>
    Task<bool> NomeEmUsoAsync(string nome, Guid ignorarId, CancellationToken ct);
    Task<bool> CodigoEmUsoAsync(string codigo, Guid ignorarId, CancellationToken ct);

    /// <summary>Quantos períodos de consentimento usam cada finalidade (ou só a informada).</summary>
    Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct);

    Task<int> ProximaOrdemAsync(CancellationToken ct);

    Task SalvarAsync(FinalidadeTratamento finalidade, bool nova, CancellationToken ct);
}

public interface IFinalidadeTratamentoAppService
{
    Task<List<FinalidadeTratamentoDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default);
    Task<FinalidadeTratamentoDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<FinalidadeTratamentoDto> SalvarAsync(FinalidadeTratamentoDto dto, CancellationToken ct = default);
    Task<FinalidadeTratamentoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<FinalidadeTratamentoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>
/// Cadastro de finalidades de tratamento (Configurações › Pessoas), sob a permissão dos cadastros auxiliares de pessoas
/// (<see cref="Permissoes.Cadastros.Tipos"/>). Leitura também para quem tem a permissão de privacidade.
/// </summary>
public sealed class FinalidadeTratamentoAppService : IFinalidadeTratamentoAppService
{
    private readonly IFinalidadeTratamentoRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public FinalidadeTratamentoAppService(IFinalidadeTratamentoRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<List<FinalidadeTratamentoDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default)
    {
        var gerencia = _autorizacao.Possui(Permissoes.Cadastros.Tipos);
        if (!gerencia) _autorizacao.Exigir(Permissoes.Pessoas.Privacidade);

        var finalidades = await _repositorio.ListarAsync(incluirInativas, ct);
        var usos = gerencia ? await _repositorio.ContarUsosAsync(null, ct) : new Dictionary<Guid, int>();
        return finalidades.Select(f => ParaDto(f, usos.GetValueOrDefault(f.Id))).ToList();
    }

    public async Task<FinalidadeTratamentoDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Tipos);
        var finalidade = await _repositorio.ObterAsync(id, ct);
        return finalidade is null ? null : await ParaDtoComUsoAsync(finalidade, ct);
    }

    public async Task<FinalidadeTratamentoDto> SalvarAsync(FinalidadeTratamentoDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Tipos);

        var anterior = dto.Id == Guid.Empty ? null : await _repositorio.ObterAsync(dto.Id, ct);
        // O que o usuário não escolhe vem do gravado (de sistema, somente histórico, situação).
        var dados = new FinalidadeTratamento
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Codigo = anterior?.Codigo ?? dto.Codigo,
            Nome = dto.Nome,
            Descricao = dto.Descricao,
            BaseLegal = dto.BaseLegal,
            ClassificacaoExigida = dto.ClassificacaoExigida,
            Ordem = dto.Ordem,
            DoSistema = anterior?.DoSistema ?? false,
            SomenteHistorico = anterior?.SomenteHistorico ?? false,
            Ativo = anterior?.Ativo ?? true
        };
        if (anterior is null && dados.Ordem <= 0) dados.Ordem = await _repositorio.ProximaOrdemAsync(ct);

        RegrasFinalidadeTratamento.Normalizar(dados);
        var erros = RegrasFinalidadeTratamento.ValidarAlteracao(anterior, dados);
        if (dados.Nome.Length > 0 && await _repositorio.NomeEmUsoAsync(dados.Nome, dados.Id, ct))
            erros.Add($"Já existe a finalidade \"{dados.Nome}\" (ativa ou desativada; maiúsculas e acentos não contam).");
        if (anterior is null && dados.Codigo.Length > 0 && await _repositorio.CodigoEmUsoAsync(dados.Codigo, dados.Id, ct))
            erros.Add($"Já existe uma finalidade com o código {dados.Codigo}.");
        if (erros.Count > 0) throw new ValidacaoException(erros);

        if (anterior is null)
            dados.RegistrarEvento($"Finalidade de tratamento '{dados.Nome}' criada.");
        else if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Finalidade de tratamento '{anterior.Nome}' renomeada para '{dados.Nome}'.");

        await _repositorio.SalvarAsync(dados, anterior is null, ct);
        return await ReleAsync(dados.Id, ct);
    }

    public Task<FinalidadeTratamentoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, desativar: true, ct);

    public Task<FinalidadeTratamentoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, desativar: false, ct);

    private async Task<FinalidadeTratamentoDto> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, bool desativar, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Tipos);
        var finalidade = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Esta finalidade não existe mais."]);
        if (desativar && finalidade.DoSistema)
            throw new ValidacaoException([$"A finalidade {finalidade.Nome} é de sistema e não pode ser desativada."]);
        finalidade.Versao = requisicao.Versao ?? finalidade.Versao;
        if (desativar) finalidade.Desativar(); else finalidade.Reativar();
        await _repositorio.SalvarAsync(finalidade, nova: false, ct);
        return await ReleAsync(id, ct);
    }

    private async Task<FinalidadeTratamentoDto> ReleAsync(Guid id, CancellationToken ct)
    {
        var gravada = await _repositorio.ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
        return await ParaDtoComUsoAsync(gravada, ct);
    }

    private async Task<FinalidadeTratamentoDto> ParaDtoComUsoAsync(FinalidadeTratamento f, CancellationToken ct) =>
        ParaDto(f, (await _repositorio.ContarUsosAsync(f.Id, ct)).GetValueOrDefault(f.Id));

    public static FinalidadeTratamentoDto ParaDto(FinalidadeTratamento f, int usos) => new()
    {
        Id = f.Id,
        Versao = f.Versao,
        Codigo = f.Codigo,
        Nome = f.Nome,
        Descricao = f.Descricao,
        BaseLegal = f.BaseLegal,
        ClassificacaoExigida = f.ClassificacaoExigida,
        SomenteHistorico = f.SomenteHistorico,
        Ordem = f.Ordem,
        DoSistema = f.DoSistema,
        Ativo = f.Ativo,
        QuantidadeUsos = usos
    };
}
