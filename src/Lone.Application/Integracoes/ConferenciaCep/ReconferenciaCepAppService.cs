using Lone.Application.Seguranca;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Seguranca;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Enums;

namespace Lone.Application.Integracoes.ConferenciaCep;

/// <summary>Reconferência de CEPs em lote (F6) e manutenção do histórico técnico, pela API.</summary>
public interface IReconferenciaCepAppService
{
    Task<SelecaoReconferenciaCepDto> SelecionarAsync(FiltroReconferenciaCepDto filtro, CancellationToken ct = default);
    Task<ResumoReconferenciaCepDto> ProcessarAsync(ProcessarReconferenciaCepRequisicao requisicao, CancellationToken ct = default);
    Task<LimpezaHistoricoCepDto> LimparHistoricoAsync(CancellationToken ct = default);

    /// <summary>Fim de uma execução: um evento na Auditoria com os metadados da execução (repetir não duplica).</summary>
    Task ConcluirAsync(ConcluirReconferenciaCepRequisicao requisicao, CancellationToken ct = default);
}

/// <summary>
/// Quem pode: editar pessoas + tabelas oficiais + alcance "Tudo". A reconferência percorre a base inteira e mostra nomes;
/// com alcance restrito ela revelaria cadastros fora do alcance, então é recusada (operação de administração).
/// </summary>
public sealed class ReconferenciaCepAppService : IReconferenciaCepAppService
{
    private readonly ServicoReconferenciaCep _servico;
    private readonly IAutorizacao _autorizacao;
    private readonly IAlcanceDoUsuario _alcance;
    private readonly IRegistroExecucoesReconferenciaCep? _execucoes;
    private readonly IUsuarioAtual? _usuario;
    private readonly TimeProvider _relogio;

    public ReconferenciaCepAppService(ServicoReconferenciaCep servico, IAutorizacao autorizacao, IAlcanceDoUsuario alcance,
                                      IRegistroExecucoesReconferenciaCep? execucoes = null, IUsuarioAtual? usuario = null,
                                      TimeProvider? relogio = null)
    {
        _servico = servico;
        _autorizacao = autorizacao;
        _alcance = alcance;
        _execucoes = execucoes;
        _usuario = usuario;
        _relogio = relogio ?? TimeProvider.System;
    }

    public async Task ConcluirAsync(ConcluirReconferenciaCepRequisicao requisicao, CancellationToken ct = default)
    {
        Exigir();
        ArgumentNullException.ThrowIfNull(requisicao);
        if (requisicao.ExecucaoId == Guid.Empty)
            throw new Lone.Domain.Validacao.ValidacaoException(["Execução da reconferência sem identificação."]);
        if (_execucoes is null) return;
        var f = requisicao.Filtro ?? new FiltroReconferenciaCepDto();
        var descricao = EventoReconferenciaCep.Descricao(requisicao.Cancelada, requisicao.Total, requisicao.Conferidos, requisicao.Divergentes,
            requisicao.NaoEncontrados, requisicao.Indisponiveis, requisicao.Alterados, requisicao.NaoProcessados, requisicao.DuracaoSegundos,
            f.NaoConferidos, f.Divergentes, f.NaoEncontrados, f.ConferidosAntigos, f.Uf, f.MunicipioId);
        await _execucoes.RegistrarAsync(new ExecucaoReconferenciaCep(requisicao.ExecucaoId, _usuario?.Nome ?? "sistema",
            _relogio.GetUtcNow().UtcDateTime, descricao), CancellationToken.None);
    }

    private void Exigir()
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Editar);
        _autorizacao.Exigir(Permissoes.Cadastros.TabelasOficiais);
        if (_alcance.Alcance != AlcanceComercial.Tudo)
            throw new AcessoNegadoException(Permissoes.Pessoas.Editar,
                "A reconferência de CEPs percorre todos os cadastros: é só para quem tem alcance a toda a base.");
    }

    public async Task<SelecaoReconferenciaCepDto> SelecionarAsync(FiltroReconferenciaCepDto filtro, CancellationToken ct = default)
    {
        Exigir();
        ArgumentNullException.ThrowIfNull(filtro);
        var s = await _servico.SelecionarAsync(new CriterioReconferenciaCep(filtro.NaoConferidos, filtro.Divergentes, filtro.NaoEncontrados,
            filtro.ConferidosAntigos, string.IsNullOrWhiteSpace(filtro.Uf) ? null : filtro.Uf.Trim().ToUpperInvariant(), filtro.MunicipioId,
            filtro.Limite), ct);
        return new SelecaoReconferenciaCepDto
        {
            Total = s.Total, Enderecos = s.Enderecos.ToList(), SemCepValido = s.SemCepValido, Truncada = s.Truncada,
            ConfirmarAcimaDe = PoliticaReconferenciaCep.ConfirmarAcimaDe, ItensPorChamada = PoliticaReconferenciaCep.ItensPorChamada,
            MaximoAdiamentos = PoliticaReconferenciaCep.MaximoAdiamentos
        };
    }

    public async Task<ResumoReconferenciaCepDto> ProcessarAsync(ProcessarReconferenciaCepRequisicao requisicao, CancellationToken ct = default)
    {
        Exigir();
        ArgumentNullException.ThrowIfNull(requisicao);
        var r = await _servico.ProcessarAsync(requisicao.Enderecos, ct);
        return new ResumoReconferenciaCepDto { Itens = r.Itens.Select(ParaDto).ToList(), DuracaoMs = (int)r.Duracao.TotalMilliseconds };
    }

    public async Task<LimpezaHistoricoCepDto> LimparHistoricoAsync(CancellationToken ct = default)
    {
        Exigir();
        return new LimpezaHistoricoCepDto
        {
            Removidos = await _servico.LimparHistoricoAsync(ct), RetencaoDias = (int)PoliticaCachePostalCep.RetencaoRecomendadaHistorico.TotalDays
        };
    }

    private static ItemReconferenciaCepDto ParaDto(ItemReconferenciaCep i)
    {
        var decisao = i.Decisao is null ? null : ConsultasAppService.ParaDto(i.Decisao);
        return new ItemReconferenciaCepDto
        {
            EnderecoId = i.EnderecoId, PessoaId = i.Endereco?.PessoaId, PessoaCodigo = i.Endereco?.PessoaCodigo, PessoaNome = i.Endereco?.PessoaNome,
            Endereco = i.Endereco?.Resumo, Cep = i.Endereco?.Cep, Resultado = i.Resultado, Detalhe = i.Detalhe,
            Componentes = decisao?.Componentes ?? [], Candidatos = decisao?.Candidatos ?? []
        };
    }
}
