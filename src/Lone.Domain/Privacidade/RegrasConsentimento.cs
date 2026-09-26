using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Domain.Privacidade;

/// <summary>Situação do consentimento numa finalidade/canal, com o registro que a decidiu (nulo = não informado).</summary>
public sealed record EstadoConsentimento(SituacaoConsentimento Situacao, PessoaConsentimento? Registro);

/// <summary>
/// Consentimento em períodos: conceder cria um período novo; revogar encerra o período em vigor. Nada é apagado nem
/// reaproveitado. Só finalidades com base legal Consentimento, ativas e que não sejam "somente histórico" recebem
/// concessão; "Registro anterior" não se concede nem se revoga. Consentimento nunca mexe no canal (Aceita comunicações)
/// nem na classificação (Uso para marketing), e vice-versa.
/// </summary>
public static class RegrasConsentimento
{
    /// <summary>
    /// Situação para a finalidade no canal informado. Canal específico prevalece sobre o geral (canal vazio): havendo
    /// registro para o canal, só ele decide; senão, decide o geral. Sem canal, só o geral conta. Período em vigor →
    /// Concedido; só períodos encerrados → Revogado; nenhum registro → Não informado. Base legal que não é consentimento
    /// → Não aplicável.
    /// </summary>
    public static EstadoConsentimento Situacao(IEnumerable<PessoaConsentimento> registros, FinalidadeTratamento finalidade,
                                               CanalComunicacao? canal)
    {
        if (finalidade.BaseLegal != BaseLegal.Consentimento)
            return new(SituacaoConsentimento.NaoAplicavel, null);

        var daFinalidade = registros.Where(r => r.FinalidadeId == finalidade.Id).ToList();
        var especificos = canal is null ? new List<PessoaConsentimento>() : daFinalidade.Where(r => r.Canal == canal).ToList();
        var decisivos = especificos.Count > 0 ? especificos : daFinalidade.Where(r => r.Canal is null).ToList();

        if (decisivos.Count == 0) return new(SituacaoConsentimento.NaoInformado, null);
        var emVigor = decisivos.Where(r => r.Concedido).OrderByDescending(r => r.ConcedidoEm).FirstOrDefault();
        if (emVigor is not null) return new(SituacaoConsentimento.Concedido, emVigor);
        var ultimo = decisivos.OrderByDescending(r => r.RevogadoEm ?? r.ConcedidoEm ?? DateTime.MinValue).First();
        return new(SituacaoConsentimento.Revogado, ultimo);
    }

    /// <summary>
    /// Novo período de consentimento. Recusa finalidade inexistente, desativada, somente histórico ou com base legal
    /// que não é consentimento; recusa se já houver período em vigor para a mesma finalidade e canal; motivo obrigatório.
    /// </summary>
    public static PessoaConsentimento Conceder(Guid id, Guid pessoaId, FinalidadeTratamento? finalidade, CanalComunicacao? canal,
                                               IEnumerable<PessoaConsentimento> existentes, DateTime agoraUtc, string usuario,
                                               string? motivo, string? versaoTermo, string? origem)
    {
        var erros = new List<string>();
        if (finalidade is null) erros.Add("Finalidade de tratamento inexistente.");
        else if (finalidade.SomenteHistorico) erros.Add($"\"{finalidade.Nome}\" guarda só histórico: não recebe consentimento.");
        else if (!finalidade.Ativo) erros.Add($"A finalidade \"{finalidade.Nome}\" está desativada.");
        else if (finalidade.BaseLegal != BaseLegal.Consentimento)
            erros.Add($"A base legal da finalidade \"{finalidade.Nome}\" não é consentimento.");
        if (canal is { } c && !Enum.IsDefined(c)) erros.Add("Canal de comunicação inválido.");

        var motivoLimpo = Texto(motivo);
        if (motivoLimpo is null) erros.Add("Informe o motivo da concessão (ex.: \"Autorizou no balcão\").");
        Limite(motivoLimpo, PessoaConsentimento.TamanhoMaximoMotivo, "O motivo", erros);
        Limite(Texto(versaoTermo), PessoaConsentimento.TamanhoMaximoTexto, "A versão do termo", erros);
        Limite(Texto(origem), PessoaConsentimento.TamanhoMaximoTexto, "A origem", erros);

        if (finalidade is not null && existentes.Any(r => r.FinalidadeId == finalidade.Id && r.Canal == canal && r.Concedido))
            erros.Add($"Já existe consentimento em vigor para {finalidade.Nome}{(canal is { } k ? " por " + NomeCanal(k) : " (qualquer canal)")}.");
        if (erros.Count > 0) throw new ValidacaoException(erros);

        return new PessoaConsentimento
        {
            Id = id,
            PessoaId = pessoaId,
            FinalidadeId = finalidade!.Id,
            Canal = canal,
            Concedido = true,
            ConcedidoEm = agoraUtc,
            ConcedidoPor = Cortar(usuario),
            Motivo = motivoLimpo,
            VersaoTermo = Texto(versaoTermo),
            Origem = Texto(origem)
        };
    }

    /// <summary>
    /// Encerra o período em vigor (o registro continua, com quem/quando/por quê). Recusa registro já revogado e
    /// registro de finalidade "somente histórico".
    /// </summary>
    public static void Revogar(PessoaConsentimento registro, FinalidadeTratamento? finalidade, DateTime agoraUtc, string usuario, string? motivo)
    {
        var erros = new List<string>();
        if (finalidade is null || finalidade.SomenteHistorico)
            erros.Add("Este registro é histórico anterior às finalidades: não pode ser revogado como um consentimento atual.");
        else if (!registro.Concedido)
            erros.Add("Este consentimento já foi revogado.");
        var motivoLimpo = Texto(motivo);
        if (motivoLimpo is null) erros.Add("Informe o motivo da revogação (ex.: \"Pediu por e-mail\").");
        Limite(motivoLimpo, PessoaConsentimento.TamanhoMaximoMotivo, "O motivo", erros);
        if (erros.Count > 0) throw new ValidacaoException(erros);

        registro.Concedido = false;
        registro.RevogadoEm = agoraUtc;
        registro.RevogadoPor = Cortar(usuario);
        registro.MotivoRevogacao = motivoLimpo;
    }

    public static string NomeCanal(CanalComunicacao canal) => canal switch
    {
        CanalComunicacao.Email => "e-mail",
        CanalComunicacao.WhatsApp => "WhatsApp",
        CanalComunicacao.Sms => "SMS",
        CanalComunicacao.Telefone => "ligação",
        CanalComunicacao.Correspondencia => "correspondência",
        _ => canal.ToString()
    };

    private static string? Texto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Cortar(string s) =>
        s.Length > PessoaConsentimento.TamanhoMaximoUsuario ? s[..PessoaConsentimento.TamanhoMaximoUsuario] : s;

    private static void Limite(string? s, int maximo, string oQue, List<string> erros)
    {
        if (s is { } t && t.Length > maximo) erros.Add($"{oQue} pode ter no máximo {maximo} caracteres.");
    }
}
