using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using C = Lone.Domain.Pessoas.CamposFichaPessoa;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Bloco G (P1-1): violações CONHECIDAS dos índices únicos de documento da pessoa, traduzidas no mesmo erro que a
/// conferência do PessoaAppService daria (com o campo da ficha). Acontecem quando dois usuários gravam o mesmo CPF, raiz
/// de CNPJ ou CNPJ ao mesmo tempo: a conferência passa para os dois e o banco barra o segundo. Só estes índices, pelo
/// NOME; qualquer outra falha do banco continua como erro inesperado (500, com o detalhe técnico só no log), nunca
/// disfarçada de validação. Nenhum texto do SQL vai para o usuário.
/// </summary>
public static class ConflitosDocumentoPessoa
{
    /// <summary>CPF (PF) e raiz do CNPJ (PJ) únicos: índice (Natureza, DocumentoPrincipal) de Pessoas.</summary>
    public const string IndiceDocumentoPrincipal = "IX_Pessoas_Natureza_DocumentoPrincipal";

    /// <summary>Cada CNPJ uma vez só no sistema: índice de Estabelecimentos.</summary>
    public const string IndiceCnpj = "IX_Estabelecimentos_Cnpj";

    public const string CpfJaCadastrado = "Este CPF já está cadastrado.";
    public const string RaizJaCadastrada =
        "Esta empresa (mesma raiz de CNPJ) já está cadastrada. Para uma filial, abra esse cadastro e adicione o CNPJ como estabelecimento.";
    public const string CnpjJaCadastrado = "Este CNPJ já está cadastrado em outra pessoa.";

    /// <summary>P1-8B: número de documento único (tipos que bloqueiam): índice filtrado da chave de unicidade.</summary>
    public const string IndiceChaveUnicidade = "IX_PessoaDocumentos_ChaveUnicidade";

    public const string NumeroDocumentoJaCadastrado =
        "Este número de documento já está cadastrado em outra pessoa e o tipo não permite repetir (gravado agora por outro usuário).";

    /// <summary>O erro de validação equivalente, ou nulo se a falha não é uma das conhecidas.</summary>
    public static ErroValidacao? Erro(Exception erro, Pessoa pessoa) => erro switch
    {
        DbUpdateException { InnerException: SqlException sql } => Erro(sql.Number, sql.Message, pessoa),
        _ => null
    };

    /// <summary>2601/2627 = chave duplicada em índice único (distinguido pelo nome do índice).</summary>
    public static ErroValidacao? Erro(int numero, string texto, Pessoa pessoa)
    {
        if (numero is not (2601 or 2627)) return null;

        if (texto.Contains(IndiceDocumentoPrincipal, StringComparison.Ordinal))
            return new ErroValidacao(pessoa.Natureza == NaturezaPessoa.Juridica ? RaizJaCadastrada : CpfJaCadastrado, C.Documento);

        if (texto.Contains(IndiceCnpj, StringComparison.Ordinal))
        {
            // O CNPJ repetido aparece no texto do SQL ("The duplicate key value is (...)"): o do principal fica na
            // Identificação; o de uma filial, no cartão dela.
            var repetido = pessoa.Estabelecimentos.FirstOrDefault(e => e.Cnpj is { Length: > 0 } cnpj && texto.Contains(cnpj, StringComparison.Ordinal));
            return repetido is null or { Principal: true }
                ? new ErroValidacao(CnpjJaCadastrado, C.Documento)
                : new ErroValidacao(CnpjJaCadastrado, C.Cnpj, repetido.Id);
        }

        if (texto.Contains(IndiceChaveUnicidade, StringComparison.Ordinal))
        {
            // A chave repetida aparece no texto do SQL: leva ao documento dela (sem mostrar a chave ao usuário).
            var documento = pessoa.Documentos.FirstOrDefault(d => d.ChaveUnicidade is { Length: > 0 } chave && texto.Contains(chave, StringComparison.Ordinal));
            return new ErroValidacao(NumeroDocumentoJaCadastrado, C.DocumentoNumero, documento?.Id);
        }

        return null;
    }
}
