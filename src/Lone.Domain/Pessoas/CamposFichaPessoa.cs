namespace Lone.Domain.Pessoas;

/// <summary>
/// Ids estáveis dos campos da ficha de Pessoas, para um erro (e, depois, uma pendência) dizer em que campo está e a tela
/// levar até ele (Lone Contextual, Fase 1). Formato <c>área.campo</c>, o mesmo dos campos de filtro
/// (<c>Lone.Contracts.Pessoas.CamposFiltroPessoas</c>): quando o campo também é filtrável, o id é <b>o mesmo</b> (um teste
/// confere). Esta classe só identifica campos da ficha; quem pode ser filtrado continua sendo decidido lá.
/// A área diz a aba da ficha (a tabela fica em <c>SecaoPessoa</c>, no app). Nunca usar o rótulo da tela como id.
/// </summary>
public static class CamposFichaPessoa
{
    // ---- Identificação ----
    public const string Natureza = "identificacao.natureza";
    public const string Nome = "identificacao.nome";
    public const string Documento = "identificacao.documento";
    public const string DataNascimento = "identificacao.dataNascimento";
    public const string DataAbertura = "identificacao.dataAbertura";
    public const string CapitalSocial = "identificacao.capitalSocial";
    public const string Porte = "identificacao.porte";
    public const string GrupoEmpresarial = "identificacao.grupoEmpresarial";
    public const string Etiquetas = "identificacao.etiquetas";

    /// <summary>Natureza jurídica (o id é o do filtro): a do principal fica na Identificação; a de cada filial, no cartão dela.</summary>
    public const string NaturezaJuridica = "identificacao.naturezaJuridica";

    // ---- Dados pessoais ----
    public const string Nacionalidade = "pessoais.nacionalidade";
    public const string NomeMae = "pessoais.nomeMae";
    public const string NomePai = "pessoais.nomePai";
    public const string Naturalidade = "pessoais.naturalidade";
    public const string Profissao = "pessoais.profissao";

    // ---- Fiscal e estabelecimentos (item = Id do estabelecimento) ----
    public const string Cnpj = "fiscal.cnpj";
    public const string IndicadorIE = "fiscal.indicadorIE";
    public const string InscricaoEstadual = "fiscal.inscricaoEstadual";
    public const string Suframa = "fiscal.suframa";
    public const string Cnae = "fiscal.cnae";
    public const string EnderecoFiscal = "fiscal.enderecoFiscal";

    // ---- Endereços (item = Id do endereço) ----
    public const string Logradouro = "enderecos.logradouro";
    public const string Cep = "enderecos.cep";
    public const string Municipio = "enderecos.municipio";
    public const string Cidade = "enderecos.cidade";
    public const string Pais = "enderecos.pais";
    public const string ObservacoesEndereco = "enderecos.observacoes";

    // ---- Telefones e e-mails (item = Id do meio de contato) ----
    public const string MeioContatoValor = "contatos.valor";
    public const string Ramal = "contatos.ramal";

    // ---- Pessoas de contato (item = Id da pessoa de contato; ficam na aba Contatos) ----
    public const string ContatoNome = "pessoasContato.nome";
    public const string ContatoTelefone = "pessoasContato.telefone";
    public const string ContatoCelular = "pessoasContato.celular";
    public const string ContatoEmail = "pessoasContato.email";

    // ---- Documentos (item = Id do documento) ----
    public const string DocumentoTipo = "documentos.tipo";
    public const string DocumentoNumero = "documentos.numero";
    public const string DocumentoUf = "documentos.uf";
    public const string DocumentoEmitidoEm = "documentos.emitidoEm";
    public const string DocumentoValidoAte = "documentos.validoAte";
    public const string DocumentoObservacoes = "documentos.observacoes";

    // ---- Comercial ----
    public const string LimiteCredito = "cliente.limiteCredito";
    public const string DescontoMaximo = "cliente.descontoMaximo";
    public const string DiasMaximoAtraso = "cliente.diasMaximoAtraso";
    public const string PrazosFornecedor = "fornecedor.prazos";
    public const string AvaliacaoFornecedor = "fornecedor.avaliacao";

    // ---- Interações ----
    public const string PrimeiroContato = "interacoes.primeiroContato";

    // ---- Informações adicionais (item = Id do campo personalizado) ----
    public const string CampoPersonalizado = "adicionais.valor";

    /// <summary>A área do id ("enderecos" em "enderecos.municipio").</summary>
    public static string Area(string campo)
    {
        var ponto = campo.IndexOf('.');
        return ponto < 0 ? campo : campo[..ponto];
    }
}
