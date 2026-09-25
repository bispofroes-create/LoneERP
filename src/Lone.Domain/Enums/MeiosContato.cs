namespace Lone.Domain.Enums;

/// <summary>Para que serve um e-mail (pode ter vários). Usado por rotinas futuras: cobrança, envio de NF-e, marketing.</summary>
[Flags]
public enum FinalidadeEmail : short
{
    Nenhuma = 0,
    Financeiro = 1,
    Cobranca = 2,
    NFe = 4,
    Marketing = 8
}

/// <summary>Grupo de um tipo de telefone/e-mail do cadastro de tipos (define em quais meios ele é oferecido).</summary>
public enum CategoriaMeioContato : byte
{
    Telefone = 0,
    Email = 1
}
