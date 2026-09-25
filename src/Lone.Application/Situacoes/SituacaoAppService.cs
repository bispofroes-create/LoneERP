using Lone.Application.Seguranca;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Application.Situacoes;

/// <summary>Bloqueios, interações e parâmetros de relacionamento (gravados à parte da ficha da pessoa).</summary>
public interface ISituacaoRepositorio
{
    /// <summary>Situação cadastral e natureza da pessoa (nulo = não existe).</summary>
    Task<SituacaoPessoa?> SituacaoDaPessoaAsync(Guid pessoaId, CancellationToken ct);

    Task IncluirBloqueioAsync(Bloqueio bloqueio, CancellationToken ct);
    Task<Bloqueio?> ObterBloqueioAsync(Guid bloqueioId, CancellationToken ct);
    Task LiberarBloqueioAsync(Guid bloqueioId, DateTime fimEm, string fimPor, string motivo, CancellationToken ct);

    Task IncluirInteracaoAsync(Interacao interacao, CancellationToken ct);
    Task<List<Interacao>> ListarInteracoesAsync(Guid pessoaId, int limite, CancellationToken ct);
    Task<DateTime?> UltimaInteracaoAsync(Guid pessoaId, CancellationToken ct);

    Task<ParametrosRelacionamento> ObterParametrosAsync(CancellationToken ct);
    Task SalvarParametrosAsync(ParametrosRelacionamento parametros, CancellationToken ct);
}

public interface ISituacaoAppService
{
    Task<BloqueioDto> BloquearAsync(Guid pessoaId, BloquearRequisicao requisicao, CancellationToken ct = default);
    Task<BloqueioDto> LiberarAsync(Guid pessoaId, Guid bloqueioId, LiberarBloqueioRequisicao requisicao, CancellationToken ct = default);
    Task<InteracaoDto> RegistrarInteracaoAsync(Guid pessoaId, RegistrarInteracaoRequisicao requisicao, CancellationToken ct = default);
    Task<RelacionamentoDto> ObterRelacionamentoAsync(Guid pessoaId, CancellationToken ct = default);
    Task<ParametrosRelacionamentoDto> ObterParametrosAsync(CancellationToken ct = default);
    Task<ParametrosRelacionamentoDto> SalvarParametrosAsync(ParametrosRelacionamentoDto dto, CancellationToken ct = default);
}

/// <summary>
/// Situações separadas da cadastral: bloqueios (comercial, financeiro, cadastral, faturamento) com quem/quando/por quê,
/// e relacionamento (interações; a situação "ativo / em risco / inativo" é calculada pelos dias sem interação).
/// Nada é apagado: liberar encerra o bloqueio; interação não se altera.
/// </summary>
public sealed class SituacaoAppService : ISituacaoAppService
{
    public const int TamanhoMaximoMotivo = 250;
    private const int InteracoesNaFicha = 50;

    private readonly ISituacaoRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;
    private readonly IUsuarioAtual _usuario;
    private readonly IMotivoDaOperacao _motivo;
    private readonly TimeProvider _relogio;

    public SituacaoAppService(ISituacaoRepositorio repositorio, IAutorizacao autorizacao, IUsuarioAtual usuario,
                              IMotivoDaOperacao motivo, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
        _usuario = usuario;
        _motivo = motivo;
        _relogio = relogio;
    }

    public async Task<BloqueioDto> BloquearAsync(Guid pessoaId, BloquearRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Bloquear);
        await ExigirPessoaEditavelAsync(pessoaId, ct);
        var motivo = Motivo(requisicao.Motivo, "o motivo do bloqueio");
        if (!Enum.IsDefined(requisicao.Escopo)) throw new ValidacaoException(["Escopo de bloqueio inválido."]);

        var bloqueio = new Bloqueio
        {
            Id = IdSequencial.Novo(),
            PessoaId = pessoaId,
            EmpresaId = requisicao.EmpresaId == Guid.Empty ? null : requisicao.EmpresaId,
            Escopo = requisicao.Escopo,
            Origem = OrigemBloqueio.Manual,
            Motivo = motivo,
            InicioEm = _relogio.GetUtcNow().UtcDateTime,
            InicioPor = Cortar(_usuario.Nome)
        };
        _motivo.Motivo = motivo;
        await _repositorio.IncluirBloqueioAsync(bloqueio, ct);
        return ParaDto(bloqueio);
    }

    public async Task<BloqueioDto> LiberarAsync(Guid pessoaId, Guid bloqueioId, LiberarBloqueioRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Desbloquear);
        var bloqueio = await _repositorio.ObterBloqueioAsync(bloqueioId, ct);
        if (bloqueio is null || bloqueio.PessoaId != pessoaId) throw new ValidacaoException(["Este bloqueio não existe mais."]);
        if (!bloqueio.Ativo) throw new ValidacaoException([$"Este bloqueio já foi liberado por {bloqueio.FimPor}."]);
        var motivo = Motivo(requisicao.Motivo, "o motivo da liberação");

        _motivo.Motivo = motivo;
        await _repositorio.LiberarBloqueioAsync(bloqueioId, _relogio.GetUtcNow().UtcDateTime, Cortar(_usuario.Nome), motivo, ct);
        return ParaDto((await _repositorio.ObterBloqueioAsync(bloqueioId, ct))!);
    }

    public async Task<InteracaoDto> RegistrarInteracaoAsync(Guid pessoaId, RegistrarInteracaoRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.RegistrarInteracao);
        await ExigirPessoaEditavelAsync(pessoaId, ct);
        var descricao = (requisicao.Descricao ?? string.Empty).Trim();
        var agora = _relogio.GetUtcNow().UtcDateTime;
        var quando = requisicao.DataHora is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : agora;

        var erros = new List<string>();
        if (descricao.Length == 0) erros.Add("Descreva a interação (o que foi tratado).");
        if (descricao.Length > Interacao.TamanhoMaximoDescricao) erros.Add($"A descrição pode ter no máximo {Interacao.TamanhoMaximoDescricao} caracteres.");
        if (quando > agora.AddMinutes(5)) erros.Add("A data da interação não pode ser no futuro.");
        if (!Enum.IsDefined(requisicao.Tipo)) erros.Add("Tipo de interação inválido.");
        if (erros.Count > 0) throw new ValidacaoException(erros);

        var interacao = new Interacao
        {
            Id = IdSequencial.Novo(), PessoaId = pessoaId, DataHora = quando, Tipo = requisicao.Tipo, Descricao = descricao, Usuario = Cortar(_usuario.Nome)
        };
        await _repositorio.IncluirInteracaoAsync(interacao, ct);
        return ParaDto(interacao);
    }

    public async Task<RelacionamentoDto> ObterRelacionamentoAsync(Guid pessoaId, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var parametros = await _repositorio.ObterParametrosAsync(ct);
        var ultima = await _repositorio.UltimaInteracaoAsync(pessoaId, ct);
        return new RelacionamentoDto
        {
            UltimaInteracaoEm = ultima is { } u ? DateTime.SpecifyKind(u, DateTimeKind.Utc) : null,
            Situacao = parametros.Situacao(ultima, _relogio.GetUtcNow().UtcDateTime),
            DiasEmRisco = parametros.DiasEmRisco,
            DiasInativo = parametros.DiasInativo,
            Interacoes = (await _repositorio.ListarInteracoesAsync(pessoaId, InteracoesNaFicha, ct)).Select(ParaDto).ToList()
        };
    }

    public async Task<ParametrosRelacionamentoDto> ObterParametrosAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var p = await _repositorio.ObterParametrosAsync(ct);
        return new ParametrosRelacionamentoDto { Versao = p.Versao, DiasEmRisco = p.DiasEmRisco, DiasInativo = p.DiasInativo };
    }

    public async Task<ParametrosRelacionamentoDto> SalvarParametrosAsync(ParametrosRelacionamentoDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.Parametros);
        var atual = await _repositorio.ObterParametrosAsync(ct);
        var novo = new ParametrosRelacionamento
        {
            Id = ParametrosRelacionamento.IdUnico, Versao = dto.Versao ?? atual.Versao, DiasEmRisco = dto.DiasEmRisco, DiasInativo = dto.DiasInativo
        };
        if (novo.Validar() is { Count: > 0 } erros) throw new ValidacaoException(erros);
        if (atual.DiasEmRisco != novo.DiasEmRisco || atual.DiasInativo != novo.DiasInativo)
            novo.RegistrarEvento($"Regras de inatividade: em risco com {novo.DiasEmRisco} dias e inativo com {novo.DiasInativo} dias sem interação.");
        await _repositorio.SalvarParametrosAsync(novo, ct);
        return await ObterParametrosAsync(ct);
    }

    private async Task ExigirPessoaEditavelAsync(Guid pessoaId, CancellationToken ct)
    {
        var situacao = await _repositorio.SituacaoDaPessoaAsync(pessoaId, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        if (situacao == SituacaoPessoa.Arquivado) throw new ValidacaoException(["Cadastro arquivado é somente leitura."]);
    }

    private static string Cortar(string nome) => nome.Length > 100 ? nome[..100] : nome;

    private static string Motivo(string? texto, string oQue)
    {
        var motivo = (texto ?? string.Empty).Trim();
        if (motivo.Length == 0) throw new ValidacaoException([$"Informe {oQue}."]);
        if (motivo.Length > TamanhoMaximoMotivo) throw new ValidacaoException([$"O motivo pode ter no máximo {TamanhoMaximoMotivo} caracteres."]);
        return motivo;
    }

    public static BloqueioDto ParaDto(Bloqueio b) => new()
    {
        Id = b.Id, EmpresaId = b.EmpresaId, Escopo = b.Escopo, Origem = b.Origem, Motivo = b.Motivo,
        InicioEm = DateTime.SpecifyKind(b.InicioEm, DateTimeKind.Utc), InicioPor = b.InicioPor,
        FimEm = b.FimEm is { } f ? DateTime.SpecifyKind(f, DateTimeKind.Utc) : null, FimPor = b.FimPor, MotivoLiberacao = b.MotivoLiberacao
    };

    private static InteracaoDto ParaDto(Interacao i) => new()
    {
        Id = i.Id, DataHora = DateTime.SpecifyKind(i.DataHora, DateTimeKind.Utc), Tipo = i.Tipo, Descricao = i.Descricao, Usuario = i.Usuario
    };
}
