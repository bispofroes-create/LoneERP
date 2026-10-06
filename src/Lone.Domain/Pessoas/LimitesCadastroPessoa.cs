using Lone.Domain.Entidades;
using C = Lone.Domain.Pessoas.CamposFichaPessoa;

namespace Lone.Domain.Pessoas;

/// <summary>Tamanho máximo de um texto da ficha: o mesmo da coluna do banco (um teste confere com o modelo do EF).</summary>
/// <param name="Entidade">A entidade do cadastro (Pessoa ou um item de lista dela).</param>
/// <param name="Propriedade">A propriedade texto.</param>
/// <param name="Nome">Como o campo aparece na mensagem ("o logradouro", "as observações").</param>
/// <param name="Campo">Id do campo da ficha (<see cref="CamposFichaPessoa"/>); nulo = erro geral, sem levar a um campo.</param>
public sealed record LimiteTexto(Type Entidade, string Propriedade, int Maximo, string Nome, string? Campo);

/// <summary>
/// Bloco G (P1-1): os limites de tamanho dos textos gravados pela ficha de Pessoas, conferidos ANTES do banco (o excesso
/// vira erro no campo, nunca uma falha do SaveChanges). A fonte de verdade é o modelo do banco: estes números são cópia
/// dele, e o teste ModeloLimitesPessoaTests falha se um mudar sem o outro ou se aparecer coluna nova sem limite aqui.
/// Os textos que já tinham limite ou formato conferidos em outra regra ficam fora desta lista (ver o teste).
/// </summary>
public static class LimitesCadastroPessoa
{
    public static IReadOnlyList<LimiteTexto> Todos { get; } =
    [
        // Identificação
        new(typeof(Pessoa), nameof(Pessoa.NomeSocial), 150, "o nome social", C.NomeSocial),
        new(typeof(Pessoa), nameof(Pessoa.NomeExibicao), 80, "o nome de exibição", C.NomeExibicao),
        new(typeof(Pessoa), nameof(Pessoa.Apelido), 60, "o apelido", C.Apelido),
        new(typeof(Pessoa), nameof(Pessoa.Observacoes), 2000, "as observações", null),

        // Fiscal e estabelecimentos
        new(typeof(Estabelecimento), nameof(Estabelecimento.NomeFantasia), 150, "o nome fantasia", C.NomeFantasia),
        new(typeof(Estabelecimento), nameof(Estabelecimento.NaturezaJuridica), 10, "a natureza jurídica", C.NaturezaJuridica),
        new(typeof(Estabelecimento), nameof(Estabelecimento.InscricaoMunicipal), 20, "a inscrição municipal", C.InscricaoMunicipal),
        new(typeof(Estabelecimento), nameof(Estabelecimento.SituacaoReceita), 40, "a situação na Receita", null),

        // Endereços
        new(typeof(PessoaEndereco), nameof(PessoaEndereco.Descricao), 60, "a descrição", C.DescricaoEndereco),
        new(typeof(PessoaEndereco), nameof(PessoaEndereco.Logradouro), 150, "o logradouro", C.Logradouro),
        new(typeof(PessoaEndereco), nameof(PessoaEndereco.Numero), 10, "o número", C.Numero),
        new(typeof(PessoaEndereco), nameof(PessoaEndereco.Complemento), 60, "o complemento", C.Complemento),
        new(typeof(PessoaEndereco), nameof(PessoaEndereco.Bairro), 80, "o bairro", C.Bairro),
        new(typeof(PessoaEndereco), nameof(PessoaEndereco.Cidade), 80, "a cidade", C.Cidade),
        new(typeof(PessoaEndereco), nameof(PessoaEndereco.Pais), 60, "o país", C.Pais),
        new(typeof(PessoaEndereco), nameof(PessoaEndereco.CodigoPais), 4, "o código do país", C.Pais),

        // Telefones e e-mails
        new(typeof(MeioContato), nameof(MeioContato.Valor), 150, "o número ou e-mail", C.MeioContatoValor),
        new(typeof(MeioContato), nameof(MeioContato.Descricao), 80, "a observação", C.MeioContatoDescricao),

        // Pessoas de contato
        new(typeof(Contato), nameof(Contato.Nome), 100, "o nome", C.ContatoNome),
        new(typeof(Contato), nameof(Contato.Cargo), 60, "o cargo", C.ContatoCargo),
        new(typeof(Contato), nameof(Contato.Departamento), 60, "o departamento", C.ContatoDepartamento),
        new(typeof(Contato), nameof(Contato.Telefone), 20, "o telefone", C.ContatoTelefone),
        new(typeof(Contato), nameof(Contato.Celular), 20, "o celular", C.ContatoCelular),
        new(typeof(Contato), nameof(Contato.Email), 150, "o e-mail", C.ContatoEmail),
        new(typeof(Contato), nameof(Contato.Observacoes), 500, "as observações", C.ContatoObservacoes),

        // Documentos
        new(typeof(PessoaDocumento), nameof(PessoaDocumento.Numero), 30, "o número", C.DocumentoNumero),
        new(typeof(PessoaDocumento), nameof(PessoaDocumento.OrgaoEmissor), 20, "o órgão emissor", C.DocumentoOrgaoEmissor),

        // Papéis e contas
        new(typeof(PessoaPapel), nameof(PessoaPapel.Observacoes), 500, "as observações", null),
        new(typeof(ContaCliente), nameof(ContaCliente.CondicaoPagamento), 60, "a condição de pagamento (texto)", null),
        new(typeof(ContaCliente), nameof(ContaCliente.Observacoes), 1000, "as observações", null),
        new(typeof(ContaFornecedor), nameof(ContaFornecedor.CondicaoPagamento), 60, "a condição de pagamento (texto)", null),
        new(typeof(ContaFornecedor), nameof(ContaFornecedor.Observacoes), 1000, "as observações", null)
    ];

    /// <summary>Os limites de uma entidade.</summary>
    public static IEnumerable<LimiteTexto> De(Type entidade) => Todos.Where(l => l.Entidade == entidade);

    /// <summary>O texto da propriedade (nulo se não for texto).</summary>
    public static string? Valor(LimiteTexto limite, object item) =>
        limite.Entidade.GetProperty(limite.Propriedade)?.GetValue(item) as string;
}
