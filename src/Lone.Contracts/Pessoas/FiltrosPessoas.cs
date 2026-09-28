namespace Lone.Contracts.Pessoas;

/// <summary>Como o valor de um campo de filtro é informado (decide o editor na tela e a validação).</summary>
public enum TipoCampoFiltro
{
    Texto,
    Lista,
    Data,
    Numero,
    SimNao
}

/// <summary>
/// Operadores do filtro. Cada campo do catálogo diz quais aceita. Datas e números: "Entre", "APartirDe" e "Ate" incluem
/// os limites. Nunca vira SQL por texto: o servidor traduz cada operador numa condição com parâmetros.
/// Os números são gravados nos filtros salvos: não mudar os valores existentes.
/// </summary>
public enum OperadorFiltro
{
    // Texto
    Contem = 1,
    ComecaCom = 2,
    Igual = 3,
    Vazio = 4,
    NaoVazio = 5,

    // Lista
    UmDestes = 10,
    TodosDestes = 11,
    NenhumDestes = 12,

    // Data e número
    Entre = 20,
    APartirDe = 21,
    Ate = 22,
    Maior = 23,
    Menor = 24,

    // Sim/não
    Sim = 30,
    Nao = 31,

    // Dias relativos a hoje
    EmAteDias = 40,
    HaMaisDeDias = 41
}

/// <summary>Para qual natureza o campo existe (a tela marca "só PF"/"só PJ").</summary>
public enum ValeParaNatureza
{
    Todas,
    Fisica,
    Juridica
}

/// <summary>Uma condição do filtro: campo do catálogo + operador + valores (texto; datas em aaaa-mm-dd, números com ponto).</summary>
public sealed class CondicaoFiltro
{
    public string Campo { get; set; } = string.Empty;
    public OperadorFiltro Operador { get; set; }
    public List<string> Valores { get; set; } = new();
}

public sealed record OpcaoFiltroDto(string Valor, string Texto);

/// <summary>Um campo do catálogo, já filtrado pelas permissões de quem pediu.</summary>
public sealed class CampoFiltroDto
{
    /// <summary>Identificador estável (ex.: "enderecos.uf"; campo personalizado: "campo:{Id}"). Vai para os filtros salvos.</summary>
    public string Id { get; set; } = string.Empty;
    public string Grupo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public TipoCampoFiltro Tipo { get; set; }
    public List<OperadorFiltro> Operadores { get; set; } = new();
    public ValeParaNatureza ValePara { get; set; }

    /// <summary>Lista fechada de escolhas (vazia quando não se aplica ou quando vem de <see cref="OpcoesSobDemanda"/>).</summary>
    public List<OpcaoFiltroDto> Opcoes { get; set; } = new();

    /// <summary>Lista grande demais para vir junto (ex.: "municipios"): a tela usa a busca própria dela.</summary>
    public string? OpcoesSobDemanda { get; set; }

    public string? Dica { get; set; }

    /// <summary>Busca em texto livre: pode ficar lenta em base grande (a tela avisa).</summary>
    public bool TextoLivre { get; set; }

    /// <summary>Texto que vale só pelos dígitos (telefone, CEP, IE, CNAE): a tela tira o resto antes de mandar.</summary>
    public bool SomenteDigitos { get; set; }

    /// <summary>Tamanho aceito do texto (depois de tirar o que não é dígito, se for o caso). Nulo = sem limite próprio.</summary>
    public int? TamanhoMinimo { get; set; }
    public int? TamanhoMaximo { get; set; }
}

/// <summary>Catálogo de campos do filtro de pessoas (grupos na ordem da ficha).</summary>
public sealed class CatalogoFiltrosPessoasDto
{
    public List<CampoFiltroDto> Campos { get; set; } = new();

    /// <summary>Colunas que o usuário pode mostrar na lista (já filtradas pelas permissões), na ordem do seletor.</summary>
    public List<ColunaListaDto> Colunas { get; set; } = new();

    /// <summary>Colunas e ordenação que o usuário deixou na lista da última vez (nulo = padrão).</summary>
    public LayoutListaPessoas? Layout { get; set; }
}

/// <summary>Identificadores estáveis dos campos do filtro de pessoas (gravados nos filtros salvos: não renomear).</summary>
public static class CamposFiltroPessoas
{
    public const string Natureza = "identificacao.natureza";
    public const string Papeis = "identificacao.papeis";
    public const string Etiquetas = "identificacao.etiquetas";
    public const string Uf = "enderecos.uf";
    public const string Municipio = "enderecos.municipio";
    public const string MunicipioACorrigir = "enderecos.municipioACorrigir";
    public const string DocumentosVencidos = "documentos.vencidos";
    public const string DocumentosVencendo = "documentos.vencendo";
    public const string ProdutorRural = "fiscal.produtorRural";
    public const string Regime = "fiscal.regime";
    public const string Cnae = "fiscal.cnae";
    public const string CnaePrincipal = "fiscal.cnaePrincipal";
    public const string Vendedor = "cliente.vendedor";
    public const string SemCarteira = "cliente.semCarteira";
    public const string Relacionamento = "interacoes.relacionamento";
    public const string SemInteracao = "interacoes.semInteracao";
    public const string Situacao = "situacao.situacao";
    public const string Bloqueado = "situacao.bloqueado";
    public const string CadastradoEm = "cadastro.cadastradoEm";

    // ---- Lista com colunas: campos que também filtram pela linha de filtro das colunas ----
    public const string Nome = "identificacao.nome";
    public const string Codigo = "identificacao.codigo";
    public const string Documento = "identificacao.documento";
    public const string Cidade = "enderecos.cidade";

    // ---- Fase 3: campos da 1ª versão ----
    public const string NomeFantasia = "identificacao.nomeFantasia";
    public const string DataNascimento = "identificacao.dataNascimento";
    public const string Aniversario = "identificacao.aniversario";
    public const string Idade = "identificacao.idade";
    public const string DataAbertura = "identificacao.dataAbertura";
    public const string Porte = "identificacao.porte";
    public const string NaturezaJuridica = "identificacao.naturezaJuridica";
    public const string GrupoEmpresarial = "identificacao.grupoEmpresarial";
    public const string Sexo = "pessoais.sexo";
    public const string EstadoCivil = "pessoais.estadoCivil";
    public const string Profissao = "pessoais.profissao";
    public const string TipoContato = "contatos.tipo";
    public const string Telefone = "contatos.telefone";
    public const string Ddd = "contatos.ddd";
    public const string Email = "contatos.email";
    public const string WhatsApp = "contatos.whatsapp";
    public const string FinalidadeContato = "contatos.finalidade";
    public const string AceitaComunicacoes = "contatos.aceitaComunicacoes";
    public const string SemContato = "contatos.semContato";
    public const string CargoContato = "pessoasContato.cargo";
    public const string FinalidadeEndereco = "enderecos.finalidade";
    public const string Bairro = "enderecos.bairro";
    public const string Cep = "enderecos.cep";
    public const string SemEndereco = "enderecos.semEndereco";
    public const string TipoDocumento = "documentos.tipo";
    public const string DocumentoValidoAte = "documentos.validoAte";
    public const string IndicadorIE = "fiscal.indicadorIE";
    public const string InscricaoEstadual = "fiscal.inscricaoEstadual";
    public const string SituacaoReceita = "fiscal.situacaoReceita";
    public const string LimiteCredito = "cliente.limiteCredito";
    public const string PerfilComercial = "cliente.perfilComercial";
    public const string CondicaoCliente = "cliente.condicaoPagamento";
    public const string CondicaoFornecedor = "fornecedor.condicaoPagamento";
    public const string AvaliacaoFornecedor = "fornecedor.avaliacao";
    public const string EmpresaVinculo = "colaborador.empresa";
    public const string TipoVinculo = "colaborador.tipoVinculo";
    public const string Admissao = "colaborador.admissao";
    public const string ColaboradorAtivo = "colaborador.ativo";
    public const string Cargo = "colaborador.cargo";
    public const string Departamento = "colaborador.departamento";
    public const string Setor = "colaborador.setor";
    public const string TipoRelacionamento = "relacionamentos.tipo";
    public const string PessoaRelacionada = "relacionamentos.pessoa";
    public const string Origem = "interacoes.origem";
    public const string ConsentimentoEmVigor = "privacidade.emVigor";
    public const string ConsentimentoRevogado = "privacidade.revogado";
    public const string CanalConsentimento = "privacidade.canal";
    public const string EscopoBloqueio = "situacao.escopoBloqueio";
    public const string AlteradoEm = "cadastro.alteradoEm";

    /// <summary>Campo personalizado pesquisável: "campo:{Id}".</summary>
    public const string PrefixoCampoPersonalizado = "campo:";

    public static string CampoPersonalizado(Guid id) => PrefixoCampoPersonalizado + id.ToString("D");

    public static Guid? IdCampoPersonalizado(string campo) =>
        campo.StartsWith(PrefixoCampoPersonalizado, StringComparison.Ordinal) &&
        Guid.TryParse(campo[PrefixoCampoPersonalizado.Length..], out var id) ? id : null;
}
