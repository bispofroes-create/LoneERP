using System.Globalization;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Relacionamentos;

/// <summary>Os dados de uma pessoa que as regras de relacionamento precisam (sem carregar o cadastro inteiro).</summary>
public sealed record PessoaNoRelacionamento(Guid Id, NaturezaPessoa Natureza, SituacaoPessoa Situacao, string Nome);

/// <summary>
/// Regras dos relacionamentos entre pessoas (sócio de, administrador de, contato de...). Não acessa banco.
/// Não limita a quantidade: uma pessoa física pode ser sócia ou administradora de várias empresas independentes, e isso
/// nunca as torna matriz/filial nem do mesmo grupo empresarial (grupo é sempre explícito).
/// </summary>
public static class RegrasRelacionamento
{
    public static bool EhSocietario(Guid tipoId) => TiposRelacionamentoSistema.Societarios.Contains(tipoId);

    public static string? Texto(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    /// <summary>Confere um vínculo novo. <paramref name="existentes"/> = os vínculos já gravados da mesma origem.</summary>
    public static List<string> ValidarNovo(PessoaRelacionamento novo, TipoRelacionamento? tipo, PessoaNoRelacionamento? origem,
                                           PessoaNoRelacionamento? destino, IEnumerable<PessoaRelacionamento> existentes)
    {
        var erros = new List<string>();
        if (tipo is null) erros.Add("Escolha o tipo de relacionamento.");
        if (origem is null || destino is null) erros.Add("A pessoa relacionada não existe mais. Busque de novo.");
        if (novo.PessoaId == novo.PessoaDestinoId) erros.Add("Uma pessoa não pode ter relacionamento com ela mesma.");
        if (origem?.Situacao == SituacaoPessoa.Arquivado || destino?.Situacao == SituacaoPessoa.Arquivado)
            erros.Add("Cadastro arquivado é somente leitura: não recebe relacionamentos novos.");
        if (tipo is not null && destino is not null && EhSocietario(tipo.Id) && destino.Natureza == NaturezaPessoa.Fisica)
            erros.Add($"\"{tipo.Nome}\" liga a pessoa a uma empresa: o outro lado não pode ser pessoa física.");
        if (novo.InicioEm is { } inicio && novo.FimEm is { } fim && fim < inicio)
            erros.Add("O fim do relacionamento é anterior ao início.");
        if (novo.Observacoes is { Length: > PessoaRelacionamento.TamanhoMaximoObservacoes })
            erros.Add($"As observações podem ter no máximo {PessoaRelacionamento.TamanhoMaximoObservacoes} caracteres.");
        if (existentes.Any(e => e.Id != novo.Id && e.Ativo && e.FimEm is null && novo.FimEm is null &&
                                e.PessoaDestinoId == novo.PessoaDestinoId && e.TipoRelacionamentoId == novo.TipoRelacionamentoId))
            erros.Add("Este relacionamento já está registrado e em aberto. Para registrar outro período, encerre o atual antes.");
        return erros;
    }

    /// <summary>Encerrar: preenche o fim (o vínculo continua gravado, com o período).</summary>
    public static List<string> ValidarEncerramento(PessoaRelacionamento r, DateOnly fim)
    {
        var erros = new List<string>();
        if (!r.Ativo) erros.Add("Este relacionamento foi desativado (lançado por engano) e não pode ser encerrado.");
        else if (r.FimEm is not null) erros.Add($"Este relacionamento já foi encerrado em {Data(r.FimEm.Value)}.");
        if (r.InicioEm is { } inicio && fim < inicio) erros.Add($"O fim não pode ser anterior ao início ({Data(inicio)}).");
        return erros;
    }

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
