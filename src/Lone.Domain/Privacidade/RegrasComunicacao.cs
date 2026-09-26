using Lone.Domain.Contatos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Privacidade;

/// <summary>Decisão da regra central: o resultado e o motivo em linguagem do usuário.</summary>
public sealed record DecisaoComunicacao(ResultadoComunicacao Resultado, string Motivo)
{
    public bool Permitido => Resultado == ResultadoComunicacao.Permitido;
}

/// <summary>
/// Regra central de privacidade: pode-se usar este telefone/e-mail, por este canal, para esta finalidade?
/// Única fonte da decisão (telas e serviços só mostram o resultado). Conceitos independentes:
/// consentimento AUTORIZA a finalidade, "Aceita comunicações" RESTRINGE o canal, "Uso para marketing" CLASSIFICA o canal.
/// Nenhum substitui o outro e nenhum é alterado aqui.
///
/// Ordem (determinística; o primeiro bloqueio encontrado é o resultado):
/// 1. Pessoa existe e está em uso (inativa ou arquivada → BloqueadoPorPessoaInativa; os bloqueios comerciais,
///    de faturamento e financeiros NÃO entram aqui).
/// 2. Canal existe, é da pessoa e combina com o meio (e-mail só por e-mail; WhatsApp/SMS só em telefone marcado;
///    correspondência não é telefone/e-mail) → BloqueadoPeloCanal; canal inativo → BloqueadoPorCanalInativo.
/// 3. Finalidade existe, está ativa e não é "somente histórico" (Registro anterior) → BloqueadoPorFinalidade.
/// 4. Base legal com regra no Lone → senão SemBaseLegalAplicavel.
/// 5. Base legal Consentimento: consentimento em vigor para pessoa + finalidade + canal (canal específico prevalece
///    sobre o geral) → senão BloqueadoPorConsentimento.
/// 6. "Aceita comunicações" do canal → senão BloqueadoPeloCanal (vale para qualquer base legal).
/// 7. Classificação exigida pela finalidade (Marketing: e-mail marcado "Uso para marketing") → senão BloqueadoPorFinalidade.
/// 8. Permitido.
/// Obs.: o consentimento (5) é avaliado antes de "Aceita comunicações" (6) para atender à matriz aprovada
/// (sem consentimento o motivo é o consentimento, mesmo com o canal também restrito).
/// </summary>
public static class RegrasComunicacao
{
    public static DecisaoComunicacao PodeComunicar(Pessoa? pessoa, Guid meioContatoId, CanalComunicacao canal, FinalidadeTratamento? finalidade)
    {
        // 1. Pessoa
        if (pessoa is null)
            return new(ResultadoComunicacao.BloqueadoPorPessoaInativa, "Cadastro não encontrado.");
        if (!pessoa.EmUso)
            return new(ResultadoComunicacao.BloqueadoPorPessoaInativa, "O cadastro está inativo: não se comunica com cadastros inativos.");

        // 2. Canal
        var meio = pessoa.MeiosContato.FirstOrDefault(m => m.Id == meioContatoId);
        if (meio is null)
            return new(ResultadoComunicacao.BloqueadoPeloCanal, "Este telefone/e-mail não pertence ao cadastro.");
        if (!Enum.IsDefined(canal))
            return new(ResultadoComunicacao.BloqueadoPeloCanal, "Canal de comunicação inválido.");
        if (!Combina(meio, canal))
            return new(ResultadoComunicacao.BloqueadoPeloCanal, $"Este contato não serve para {RegrasConsentimento.NomeCanal(canal)}.");
        if (!meio.Ativo)
            return new(ResultadoComunicacao.BloqueadoPorCanalInativo, "Este telefone/e-mail está inativo.");

        // 3. Finalidade
        if (finalidade is null)
            return new(ResultadoComunicacao.BloqueadoPorFinalidade, "Finalidade de tratamento inexistente.");
        if (finalidade.SomenteHistorico)
            return new(ResultadoComunicacao.BloqueadoPorFinalidade, $"\"{finalidade.Nome}\" é somente histórico e não autoriza comunicação.");
        if (!finalidade.Ativo)
            return new(ResultadoComunicacao.BloqueadoPorFinalidade, $"A finalidade \"{finalidade.Nome}\" está desativada.");

        // 4. Base legal
        if (!RegrasFinalidadeTratamento.BasesLegaisComRegra.Contains(finalidade.BaseLegal))
            return new(ResultadoComunicacao.SemBaseLegalAplicavel, $"A finalidade \"{finalidade.Nome}\" não tem base legal com regra no Lone.");

        // 5. Consentimento (quando a base legal é consentimento)
        if (finalidade.BaseLegal == BaseLegal.Consentimento)
        {
            var estado = RegrasConsentimento.Situacao(pessoa.Consentimentos, finalidade, canal);
            if (estado.Situacao != SituacaoConsentimento.Concedido)
                return new(ResultadoComunicacao.BloqueadoPorConsentimento, estado.Situacao == SituacaoConsentimento.Revogado
                    ? $"O consentimento para {finalidade.Nome} foi revogado."
                    : $"Não há consentimento para {finalidade.Nome}.");
        }

        // 6. Restrição do canal
        if (!meio.PermiteComunicacao)
            return new(ResultadoComunicacao.BloqueadoPeloCanal, "Este telefone/e-mail não aceita comunicações.");

        // 7. Classificação exigida
        if (!TemClassificacao(meio, finalidade.ClassificacaoExigida))
            return new(ResultadoComunicacao.BloqueadoPorFinalidade, finalidade.ClassificacaoExigida == ClassificacaoCanal.Marketing
                ? "Este contato não está marcado \"Uso para marketing\" (só e-mails têm essa marca)."
                : "Este contato não tem a classificação exigida pela finalidade.");

        return new(ResultadoComunicacao.Permitido, "Pode ser usado.");
    }

    /// <summary>Canal natural do meio: e-mail → e-mail; telefone → ligação; "outro" não tem canal.</summary>
    public static CanalComunicacao? CanalPadrao(MeioContato meio) =>
        meio.Tipo == TipoContato.Email ? CanalComunicacao.Email
        : RegrasMeioContato.EhTelefone(meio.Tipo) ? CanalComunicacao.Telefone
        : null;

    /// <summary>O meio suporta o canal? (WhatsApp/SMS pelas marcas do telefone; correspondência usa endereço.)</summary>
    public static bool Combina(MeioContato meio, CanalComunicacao canal) => canal switch
    {
        CanalComunicacao.Email => meio.Tipo == TipoContato.Email,
        CanalComunicacao.Telefone => RegrasMeioContato.EhTelefone(meio.Tipo),
        CanalComunicacao.WhatsApp => RegrasMeioContato.EhTelefone(meio.Tipo) && (meio.WhatsApp || meio.Tipo == TipoContato.WhatsApp),
        CanalComunicacao.Sms => RegrasMeioContato.EhTelefone(meio.Tipo) && meio.Sms,
        _ => false
    };

    /// <summary>Classificação é do canal; hoje só Marketing, e só no e-mail. Nunca vem do consentimento.</summary>
    public static bool TemClassificacao(MeioContato meio, ClassificacaoCanal exigida) => exigida switch
    {
        ClassificacaoCanal.Nenhuma => true,
        ClassificacaoCanal.Marketing => meio.Tipo == TipoContato.Email && meio.Finalidades.HasFlag(FinalidadeEmail.Marketing),
        _ => false
    };
}
