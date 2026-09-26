using Lone.Application.Seguranca;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Privacidade;
using Lone.Domain.Validacao;

namespace Lone.Application.Privacidade;

/// <summary>Leitura e gravação da privacidade da pessoa (fora do Salvar da ficha). Nada é apagado.</summary>
public interface IPrivacidadeRepositorio
{
    /// <summary>Pessoa com telefones/e-mails e todos os períodos de consentimento (sem rastreamento); nulo = não existe.</summary>
    Task<Pessoa?> ObterPessoaAsync(Guid pessoaId, CancellationToken ct);

    /// <summary>Todas as finalidades (ativas e desativadas), na ordem do cadastro.</summary>
    Task<List<FinalidadeTratamento>> ListarFinalidadesAsync(CancellationToken ct);

    /// <summary>Inclui um período novo (a auditoria registra quem, quando e o motivo da operação).</summary>
    Task IncluirConsentimentoAsync(PessoaConsentimento consentimento, CancellationToken ct);

    /// <summary>Grava o encerramento do período (Concedido, RevogadoEm, RevogadoPor, MotivoRevogacao).</summary>
    Task RevogarConsentimentoAsync(PessoaConsentimento revogado, CancellationToken ct);
}

public interface IPrivacidadeAppService
{
    Task<PrivacidadeDto> ObterAsync(Guid pessoaId, CancellationToken ct = default);
    Task<PrivacidadeDto> ConcederAsync(Guid pessoaId, ConcederConsentimentoRequisicao requisicao, CancellationToken ct = default);
    Task<PrivacidadeDto> RevogarAsync(Guid pessoaId, Guid consentimentoId, RevogarConsentimentoRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>
/// Privacidade (LGPD), com a permissão <see cref="Permissoes.Pessoas.Privacidade"/>: consultar, conceder, revogar e ver
/// o histórico. Conceder e revogar são ações próprias com motivo (vai também para a coluna Motivo da auditoria), data do
/// servidor e usuário; nada é apagado. As decisões por canal vêm da regra central do domínio (RegrasComunicacao);
/// este serviço só as apresenta. Nenhuma ação aqui mexe em "Aceita comunicações" ou "Uso para marketing".
/// </summary>
public sealed class PrivacidadeAppService : IPrivacidadeAppService
{
    private readonly IPrivacidadeRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;
    private readonly IUsuarioAtual _usuario;
    private readonly IMotivoDaOperacao _motivo;
    private readonly TimeProvider _relogio;

    public PrivacidadeAppService(IPrivacidadeRepositorio repositorio, IAutorizacao autorizacao, IUsuarioAtual usuario,
                                 IMotivoDaOperacao motivo, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
        _usuario = usuario;
        _motivo = motivo;
        _relogio = relogio;
    }

    public async Task<PrivacidadeDto> ObterAsync(Guid pessoaId, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Privacidade);
        var pessoa = await _repositorio.ObterPessoaAsync(pessoaId, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        return Montar(pessoa, await _repositorio.ListarFinalidadesAsync(ct));
    }

    public async Task<PrivacidadeDto> ConcederAsync(Guid pessoaId, ConcederConsentimentoRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Privacidade);
        var pessoa = await PessoaEditavelAsync(pessoaId, ct);
        var finalidades = await _repositorio.ListarFinalidadesAsync(ct);

        var novo = RegrasConsentimento.Conceder(IdSequencial.Novo(), pessoaId, finalidades.FirstOrDefault(f => f.Id == requisicao.FinalidadeId),
            requisicao.Canal, pessoa.Consentimentos, _relogio.GetUtcNow().UtcDateTime, _usuario.Nome,
            requisicao.Motivo, requisicao.VersaoTermo, requisicao.Origem);
        _motivo.Motivo = novo.Motivo;
        await _repositorio.IncluirConsentimentoAsync(novo, ct);
        return await ObterAsync(pessoaId, ct);
    }

    public async Task<PrivacidadeDto> RevogarAsync(Guid pessoaId, Guid consentimentoId, RevogarConsentimentoRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Privacidade);
        var pessoa = await PessoaEditavelAsync(pessoaId, ct);
        var registro = pessoa.Consentimentos.FirstOrDefault(c => c.Id == consentimentoId)
                       ?? throw new ValidacaoException(["Este consentimento não existe mais."]);
        var finalidade = (await _repositorio.ListarFinalidadesAsync(ct)).FirstOrDefault(f => f.Id == registro.FinalidadeId);

        RegrasConsentimento.Revogar(registro, finalidade, _relogio.GetUtcNow().UtcDateTime, _usuario.Nome, requisicao.Motivo);
        _motivo.Motivo = registro.MotivoRevogacao;
        await _repositorio.RevogarConsentimentoAsync(registro, ct);
        return await ObterAsync(pessoaId, ct);
    }

    private async Task<Pessoa> PessoaEditavelAsync(Guid pessoaId, CancellationToken ct)
    {
        var pessoa = await _repositorio.ObterPessoaAsync(pessoaId, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        if (pessoa.Situacao == SituacaoPessoa.Arquivado) throw new ValidacaoException(["Cadastro arquivado é somente leitura."]);
        return pessoa;
    }

    /// <summary>A tela só mostra: situações e decisões calculadas pelas regras do domínio.</summary>
    public static PrivacidadeDto Montar(Pessoa pessoa, IReadOnlyList<FinalidadeTratamento> finalidades)
    {
        var porId = finalidades.ToDictionary(f => f.Id);
        var usaveis = finalidades.Where(f => f.Ativo && !f.SomenteHistorico).OrderBy(f => f.Ordem).ThenBy(f => f.Nome).ToList();

        var dto = new PrivacidadeDto();
        foreach (var f in usaveis.Where(u => u.BaseLegal == BaseLegal.Consentimento))
        {
            var estado = RegrasConsentimento.Situacao(pessoa.Consentimentos, f, canal: null);
            dto.Finalidades.Add(new FinalidadeConsentimentoDto
            {
                FinalidadeId = f.Id, Nome = f.Nome, Descricao = f.Descricao, BaseLegal = f.BaseLegal, Situacao = estado.Situacao,
                Desde = Utc(estado.Situacao == SituacaoConsentimento.Revogado ? estado.Registro?.RevogadoEm : estado.Registro?.ConcedidoEm)
            });
        }

        dto.Consentimentos = pessoa.Consentimentos
            .OrderByDescending(c => c.Concedido)
            .ThenByDescending(c => c.RevogadoEm ?? c.ConcedidoEm ?? DateTime.MinValue)
            .Select(c => new ConsentimentoDto
            {
                Id = c.Id,
                FinalidadeId = c.FinalidadeId,
                Finalidade = porId.TryGetValue(c.FinalidadeId, out var f) ? f.Nome : "(finalidade desconhecida)",
                SomenteHistorico = f?.SomenteHistorico ?? false,
                Canal = c.Canal,
                EmVigor = c.Concedido,
                ConcedidoEm = Utc(c.ConcedidoEm),
                ConcedidoPor = c.ConcedidoPor,
                Motivo = c.Motivo,
                VersaoTermo = c.VersaoTermo,
                Origem = c.Origem,
                RevogadoEm = Utc(c.RevogadoEm),
                RevogadoPor = c.RevogadoPor,
                MotivoRevogacao = c.MotivoRevogacao
            }).ToList();

        foreach (var meio in pessoa.MeiosContato.Where(m => m.Ativo).OrderBy(m => m.Tipo == TipoContato.Email).ThenByDescending(m => m.Principal))
        {
            var canal = RegrasComunicacao.CanalPadrao(meio);
            var item = new CanalPrivacidadeDto
            {
                MeioContatoId = meio.Id,
                Tipo = meio.Tipo,
                Valor = meio.Valor,
                AceitaComunicacoes = meio.PermiteComunicacao,
                UsoParaMarketing = meio.Tipo == TipoContato.Email ? meio.Finalidades.HasFlag(FinalidadeEmail.Marketing) : null
            };
            if (canal is { } c)
                foreach (var f in usaveis)
                {
                    var decisao = RegrasComunicacao.PodeComunicar(pessoa, meio.Id, c, f);
                    item.Decisoes.Add(new DecisaoCanalDto
                    {
                        FinalidadeId = f.Id, Finalidade = f.Nome, Canal = c, Resultado = decisao.Resultado, Motivo = decisao.Motivo
                    });
                }
            dto.Canais.Add(item);
        }
        return dto;
    }

    private static DateTime? Utc(DateTime? d) => d is { } v ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : null;
}
