using Lone.Contracts.Auditoria;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Cliente.Api;

/// <summary>Chamadas do cadastro de pessoas. A API confere as permissões de cada operação.</summary>
public sealed class PessoasApi
{
    private readonly ClienteApi _api;

    public PessoasApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<int> ContarClientesAtivosAsync(CancellationToken ct = default) =>
        _api.GetAsync<int>(Rotas.Pessoas.QuantidadeClientesAtivos, ct);

    /// <summary>Busca no servidor (nome, código, CPF/CNPJ, telefone ou e-mail), com filtro de papel.</summary>
    public Task<List<PessoaResumo>> ListarAsync(FiltroPessoas filtro, CancellationToken ct = default) =>
        _api.GetAsync<List<PessoaResumo>>(Rotas.Pessoas.Grupo + Consulta(filtro), ct);

    public Task<List<QuantidadePorFaixaEtaria>> ListarFaixasEtariasAsync(TipoPapel? papel, CancellationToken ct = default) =>
        _api.GetAsync<List<QuantidadePorFaixaEtaria>>(Rotas.Pessoas.FaixasEtarias + (papel is { } p ? "?papel=" + p : string.Empty), ct);

    public Task<PessoaDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<PessoaDto>(Rotas.Pessoas.PorId(id), ct);

    /// <summary>Inclui ou altera (o Id da pessoa nova é gerado no aparelho).</summary>
    public Task<ResultadoSalvarPessoa> SalvarAsync(PessoaDto pessoa, CancellationToken ct = default) =>
        _api.PutAsync<ResultadoSalvarPessoa>(Rotas.Pessoas.PorId(pessoa.Id), pessoa, ct);

    public Task<PessoaDto> DesativarAsync(Guid id, byte[]? versao, string? motivo, CancellationToken ct = default) =>
        _api.PostAsync<PessoaDto>(Rotas.Pessoas.Desativar(id), new AlterarSituacaoRequisicao { Versao = versao, Motivo = motivo }, ct: ct);

    public Task<PessoaDto> ReativarAsync(Guid id, byte[]? versao, string? motivo, CancellationToken ct = default) =>
        _api.PostAsync<PessoaDto>(Rotas.Pessoas.Reativar(id), new AlterarSituacaoRequisicao { Versao = versao, Motivo = motivo }, ct: ct);

    /// <summary>Consolida o endereço <paramref name="origemId"/> em <paramref name="destinoId"/> (o servidor decide o resultado).</summary>
    public Task<PessoaDto> ConsolidarEnderecosAsync(Guid id, byte[]? versao, Guid origemId, Guid destinoId, CancellationToken ct = default) =>
        _api.PostAsync<PessoaDto>(Rotas.Pessoas.ConsolidarEnderecos(id),
            new Lone.Contracts.Enderecos.ConsolidarEnderecosRequisicao { Versao = versao, OrigemId = origemId, DestinoId = destinoId }, ct: ct);

    /// <summary>Uma página do histórico; <paramref name="antes"/> = Id do último registro já mostrado (nulo = do começo).</summary>
    public Task<List<RegistroHistorico>> ListarHistoricoAsync(Guid id, long? antes = null, int limite = PaginaHistorico, CancellationToken ct = default) =>
        _api.GetAsync<List<RegistroHistorico>>(Rotas.Pessoas.Historico(id, antes, limite), ct);

    public const int PaginaHistorico = 100;

    public Task<BloqueioDto> BloquearAsync(Guid id, BloquearRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<BloqueioDto>(Rotas.Pessoas.Bloquear(id), requisicao, ct: ct);

    public Task<BloqueioDto> LiberarBloqueioAsync(Guid id, Guid bloqueioId, string motivo, CancellationToken ct = default) =>
        _api.PostAsync<BloqueioDto>(Rotas.Pessoas.LiberarBloqueio(id, bloqueioId), new LiberarBloqueioRequisicao { Motivo = motivo }, ct: ct);

    public Task<InteracaoDto> RegistrarInteracaoAsync(Guid id, RegistrarInteracaoRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<InteracaoDto>(Rotas.Pessoas.Interacoes(id), requisicao, ct: ct);

    // ---- Estrutura empresarial: relacionamentos entre pessoas (gravados na hora, fora do "Salvar" da ficha) ----

    /// <summary>Tipos de relacionamento e grupos empresariais (lidos quando a aba precisa).</summary>
    public Task<EstruturaEmpresarialOpcoesDto> ListarOpcoesEstruturaAsync(CancellationToken ct = default) =>
        _api.GetAsync<EstruturaEmpresarialOpcoesDto>(Rotas.Pessoas.OpcoesEstrutura, ct);

    /// <summary>Os vínculos da pessoa nos dois sentidos (ex.: "Sócio de ABC" e "Tem como sócio João").</summary>
    /// <summary>Privacidade (LGPD): leitura da aba e as ações próprias de conceder/revogar consentimento.</summary>
    public Task<PrivacidadeDto> ObterPrivacidadeAsync(Guid id, CancellationToken ct = default) =>
        _api.GetAsync<PrivacidadeDto>(Rotas.Pessoas.Privacidade(id), ct);

    public Task<PrivacidadeDto> ConcederConsentimentoAsync(Guid id, ConcederConsentimentoRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<PrivacidadeDto>(Rotas.Pessoas.Consentimentos(id), requisicao, ct: ct);

    public Task<PrivacidadeDto> RevogarConsentimentoAsync(Guid id, Guid consentimentoId, string motivo, CancellationToken ct = default) =>
        _api.PostAsync<PrivacidadeDto>(Rotas.Pessoas.RevogarConsentimento(id, consentimentoId), new RevogarConsentimentoRequisicao { Motivo = motivo }, ct: ct);

    public Task<List<PessoaRelacionamentoDto>> ListarRelacionamentosAsync(Guid id, CancellationToken ct = default) =>
        _api.GetAsync<List<PessoaRelacionamentoDto>>(Rotas.Pessoas.Relacionamentos(id), ct);

    public Task<PessoaRelacionamentoDto> IncluirRelacionamentoAsync(Guid id, IncluirRelacionamentoRequisicao requisicao, CancellationToken ct = default) =>
        _api.PostAsync<PessoaRelacionamentoDto>(Rotas.Pessoas.Relacionamentos(id), requisicao, ct: ct);

    public Task<PessoaRelacionamentoDto> EncerrarRelacionamentoAsync(Guid id, Guid relacionamentoId, EncerrarRelacionamentoRequisicao requisicao,
                                                                     CancellationToken ct = default) =>
        _api.PostAsync<PessoaRelacionamentoDto>(Rotas.Pessoas.EncerrarRelacionamento(id, relacionamentoId), requisicao, ct: ct);

    public Task<PessoaRelacionamentoDto> DesativarRelacionamentoAsync(Guid id, Guid relacionamentoId, string? motivo, CancellationToken ct = default) =>
        _api.PostAsync<PessoaRelacionamentoDto>(Rotas.Pessoas.DesativarRelacionamento(id, relacionamentoId),
            new DesativarRelacionamentoRequisicao { Motivo = motivo }, ct: ct);

    /// <summary>Monta "?texto=...&amp;papel=Cliente&amp;incluirInativos=true" só com o que foi informado.</summary>
    internal static string Consulta(FiltroPessoas filtro)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(filtro.Texto)) partes.Add("texto=" + Uri.EscapeDataString(filtro.Texto.Trim()));
        if (filtro.PapelId is { } papel) partes.Add("papelId=" + papel);
        if (filtro.EtiquetaId is { } etiqueta) partes.Add("etiquetaId=" + etiqueta);
        if (filtro.IncluirInativos) partes.Add("incluirInativos=true");
        if (filtro.MunicipioACorrigir) partes.Add("municipioACorrigir=true");
        if (filtro.Limite != FiltroPessoas.LimiteMaximo) partes.Add("limite=" + filtro.Limite);
        return partes.Count == 0 ? string.Empty : "?" + string.Join("&", partes);
    }
}
