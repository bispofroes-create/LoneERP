using Lone.Application.GruposEmpresariais;
using Lone.Application.Seguranca;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Relacionamentos;
using Lone.Domain.Validacao;

namespace Lone.Application.Relacionamentos;

public interface IPessoaRelacionamentoAppService
{
    Task<List<PessoaRelacionamentoDto>> ListarAsync(Guid pessoaId, CancellationToken ct = default);
    Task<PessoaRelacionamentoDto> IncluirAsync(Guid pessoaId, IncluirRelacionamentoRequisicao requisicao, CancellationToken ct = default);
    Task<PessoaRelacionamentoDto> EncerrarAsync(Guid pessoaId, Guid relacionamentoId, EncerrarRelacionamentoRequisicao requisicao, CancellationToken ct = default);
    Task<PessoaRelacionamentoDto> DesativarAsync(Guid pessoaId, Guid relacionamentoId, DesativarRelacionamentoRequisicao requisicao, CancellationToken ct = default);
    Task<EstruturaEmpresarialOpcoesDto> OpcoesAsync(CancellationToken ct = default);
}

/// <summary>
/// Relacionamentos entre pessoas (sócio de, administrador de, contato de...). Ações próprias, gravadas na hora e fora do
/// "Salvar" da ficha (como bloqueios e interações): não mudam a versão da ficha aberta. O vínculo pertence à pessoa de
/// origem (auditoria no histórico dela); a ficha de qualquer um dos lados mostra e pode incluir, encerrar ou desativar.
/// Vínculos societários (sócio, administrador) exigem também a permissão de estrutura empresarial.
/// </summary>
public sealed class PessoaRelacionamentoAppService : IPessoaRelacionamentoAppService
{
    public const int TamanhoMaximoMotivo = 250;

    private readonly IPessoaRelacionamentoRepositorio _repositorio;
    private readonly IGrupoEmpresarialRepositorio _grupos;
    private readonly IAutorizacao _autorizacao;
    private readonly IMotivoDaOperacao _motivo;
    private readonly TimeProvider _relogio;

    public PessoaRelacionamentoAppService(IPessoaRelacionamentoRepositorio repositorio, IGrupoEmpresarialRepositorio grupos,
                                          IAutorizacao autorizacao, IMotivoDaOperacao motivo, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _grupos = grupos;
        _autorizacao = autorizacao;
        _motivo = motivo;
        _relogio = relogio;
    }

    private DateOnly Hoje => DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime);

    public async Task<List<PessoaRelacionamentoDto>> ListarAsync(Guid pessoaId, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var vinculos = await _repositorio.ListarDaPessoaAsync(pessoaId, ct);
        return await ParaDtosAsync(pessoaId, vinculos, ct);
    }

    public async Task<PessoaRelacionamentoDto> IncluirAsync(Guid pessoaId, IncluirRelacionamentoRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Editar);
        if (RegrasRelacionamento.EhSocietario(requisicao.TipoRelacionamentoId))
            _autorizacao.Exigir(Permissoes.Pessoas.EstruturaEmpresarial);

        // Na ficha do destino ("Tem como sócio João"), o vínculo é gravado no sentido de sempre: João → Sócio de → ABC.
        var origemId = requisicao.Inverso ? requisicao.OutraPessoaId : pessoaId;
        var destinoId = requisicao.Inverso ? pessoaId : requisicao.OutraPessoaId;
        var novo = new PessoaRelacionamento
        {
            Id = IdSequencial.Novo(),
            PessoaId = origemId,
            PessoaDestinoId = destinoId,
            TipoRelacionamentoId = requisicao.TipoRelacionamentoId,
            InicioEm = requisicao.InicioEm,
            FimEm = requisicao.FimEm,
            Observacoes = RegrasRelacionamento.Texto(requisicao.Observacoes),
            Ativo = true
        };

        var tipo = (await _repositorio.ListarTiposAsync(ct)).FirstOrDefault(t => t.Id == novo.TipoRelacionamentoId);
        var pessoas = await _repositorio.PessoasAsync([origemId, destinoId], ct);
        var erros = RegrasRelacionamento.ValidarNovo(novo, tipo, pessoas.GetValueOrDefault(origemId), pessoas.GetValueOrDefault(destinoId),
            await _repositorio.ListarDaOrigemAsync(origemId, ct));
        if (erros.Count > 0) throw new ValidacaoException(erros);

        await _repositorio.IncluirAsync(novo, ct);
        return await ReleAsync(pessoaId, novo.Id, ct);
    }

    public async Task<PessoaRelacionamentoDto> EncerrarAsync(Guid pessoaId, Guid relacionamentoId, EncerrarRelacionamentoRequisicao requisicao,
                                                              CancellationToken ct = default)
    {
        var vinculo = await ObterDaPessoaAsync(pessoaId, relacionamentoId, ct);
        var fim = requisicao.FimEm ?? Hoje;
        var erros = RegrasRelacionamento.ValidarEncerramento(vinculo, fim);
        if (erros.Count > 0) throw new ValidacaoException(erros);

        _motivo.Motivo = Motivo(requisicao.Motivo);
        await _repositorio.AlterarAsync(vinculo.Id, fim, ativo: true, ct);
        return await ReleAsync(pessoaId, vinculo.Id, ct);
    }

    public async Task<PessoaRelacionamentoDto> DesativarAsync(Guid pessoaId, Guid relacionamentoId, DesativarRelacionamentoRequisicao requisicao,
                                                               CancellationToken ct = default)
    {
        var vinculo = await ObterDaPessoaAsync(pessoaId, relacionamentoId, ct);
        if (!vinculo.Ativo) throw new ValidacaoException(["Este relacionamento já está desativado."]);

        _motivo.Motivo = Motivo(requisicao.Motivo);
        await _repositorio.AlterarAsync(vinculo.Id, vinculo.FimEm, ativo: false, ct);
        return await ReleAsync(pessoaId, vinculo.Id, ct);
    }

    public async Task<EstruturaEmpresarialOpcoesDto> OpcoesAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var empresas = await _grupos.ContarEmpresasAsync(null, ct);
        return new EstruturaEmpresarialOpcoesDto
        {
            TiposRelacionamento = (await _repositorio.ListarTiposAsync(ct))
                .OrderBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Select(t => new TipoRelacionamentoDto
                {
                    Id = t.Id, Nome = t.Nome, NomeInverso = t.NomeInverso, Societario = RegrasRelacionamento.EhSocietario(t.Id)
                }).ToList(),
            GruposEmpresariais = (await _grupos.ListarAsync(ct))
                .OrderBy(g => g.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Select(g => GrupoEmpresarialAppService.ParaDto(g, empresas.GetValueOrDefault(g.Id)))
                .ToList()
        };
    }

    /// <summary>Encerrar e desativar: permissão de editar (e a de estrutura, se societário); só um vínculo desta pessoa.</summary>
    private async Task<PessoaRelacionamento> ObterDaPessoaAsync(Guid pessoaId, Guid relacionamentoId, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Editar);
        var vinculo = await _repositorio.ObterAsync(relacionamentoId, ct);
        if (vinculo is null || (vinculo.PessoaId != pessoaId && vinculo.PessoaDestinoId != pessoaId))
            throw new ValidacaoException(["Este relacionamento não existe mais."]);
        if (RegrasRelacionamento.EhSocietario(vinculo.TipoRelacionamentoId))
            _autorizacao.Exigir(Permissoes.Pessoas.EstruturaEmpresarial);
        return vinculo;
    }

    private async Task<PessoaRelacionamentoDto> ReleAsync(Guid pessoaId, Guid relacionamentoId, CancellationToken ct)
    {
        var vinculo = await _repositorio.ObterAsync(relacionamentoId, ct) ?? throw new ConflitoDeEdicaoException();
        return (await ParaDtosAsync(pessoaId, [vinculo], ct))[0];
    }

    /// <summary>Vistos da ficha aberta: o outro lado, o texto do tipo no sentido certo e se vale hoje.</summary>
    private async Task<List<PessoaRelacionamentoDto>> ParaDtosAsync(Guid pessoaId, IReadOnlyList<PessoaRelacionamento> vinculos, CancellationToken ct)
    {
        if (vinculos.Count == 0) return [];
        var tipos = (await _repositorio.ListarTiposAsync(ct)).ToDictionary(t => t.Id);
        var outros = vinculos.Select(v => v.PessoaId == pessoaId ? v.PessoaDestinoId : v.PessoaId).Distinct().ToList();
        var pessoas = await _repositorio.PessoasAsync(outros, ct);
        var hoje = Hoje;

        return vinculos
            .Select(v =>
            {
                var inverso = v.PessoaId != pessoaId;
                var outra = inverso ? v.PessoaId : v.PessoaDestinoId;
                var tipo = tipos.GetValueOrDefault(v.TipoRelacionamentoId);
                var pessoa = pessoas.GetValueOrDefault(outra);
                return new PessoaRelacionamentoDto
                {
                    Id = v.Id,
                    TipoRelacionamentoId = v.TipoRelacionamentoId,
                    Tipo = tipo is null ? "(tipo)" : inverso ? tipo.NomeInverso : tipo.Nome,
                    Inverso = inverso,
                    OutraPessoaId = outra,
                    OutraPessoaNome = pessoa?.Nome ?? "(cadastro)",
                    OutraPessoaNatureza = pessoa?.Natureza ?? NaturezaPessoa.Fisica,
                    InicioEm = v.InicioEm,
                    FimEm = v.FimEm,
                    Observacoes = v.Observacoes,
                    Ativo = v.Ativo,
                    Vigente = v.Vigente(hoje),
                    Societario = RegrasRelacionamento.EhSocietario(v.TipoRelacionamentoId)
                };
            })
            .OrderByDescending(d => d.Vigente).ThenByDescending(d => d.Ativo).ThenBy(d => d.Tipo).ThenBy(d => d.OutraPessoaNome)
            .ToList();
    }

    private static string? Motivo(string? texto)
    {
        var motivo = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        if (motivo is { Length: > TamanhoMaximoMotivo })
            throw new ValidacaoException([$"O motivo pode ter no máximo {TamanhoMaximoMotivo} caracteres."]);
        return motivo;
    }
}
