using System.Globalization;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Application.Consultas;

/// <summary>
/// Um campo do catálogo: onde aparece (grupo), como é informado (tipo e operadores), para quem vale (natureza e
/// permissão) e de onde vêm as escolhas. A condição no banco fica na Infraestrutura (FiltrosPessoasSql), pelo mesmo Id.
/// </summary>
public sealed record DefinicaoCampoFiltro(
    string Id,
    string Grupo,
    string Nome,
    TipoCampoFiltro Tipo,
    OperadorFiltro[] Operadores,
    ValeParaNatureza ValePara = ValeParaNatureza.Todas,
    string? Permissao = null,
    string? Dica = null,
    IReadOnlyList<OpcaoFiltroDto>? Opcoes = null,
    string? FonteOpcoes = null,
    Func<string, bool>? ValorValido = null,
    bool TextoLivre = false,
    bool SomenteDigitos = false,
    int? TamanhoMinimo = null,
    int? TamanhoMaximo = null);

/// <summary>
/// Catálogo dos campos do filtro de pessoas (Fase 1: os critérios que a consulta avançada já tinha; Fase 3: 1ª versão). Os grupos seguem a
/// ordem da ficha. Campo novo = uma definição aqui + a condição em FiltrosPessoasSql (um teste confere os dois lados).
/// </summary>
public static class CatalogoFiltrosPessoas
{
    /// <summary>Fontes de opções carregadas do banco (papéis, etiquetas...) ou sob demanda na tela (municípios).</summary>
    public const string FontePapeis = "papeis";
    public const string FonteEtiquetas = "etiquetas";
    public const string FonteVendedores = "vendedores";
    public const string FonteMunicipios = "municipios";

    // Fontes do banco (Fase 3): cadastros e valores já usados (porte, origem, situação na Receita, natureza jurídica).
    public const string FontePortes = "portes";
    public const string FonteNaturezasJuridicas = "naturezasJuridicas";
    public const string FonteGruposEmpresariais = "gruposEmpresariais";
    public const string FonteProfissoes = "profissoes";
    public const string FonteFinalidadesEndereco = "finalidadesEndereco";
    public const string FonteTiposDocumento = "tiposDocumento";
    public const string FonteSituacoesReceita = "situacoesReceita";
    public const string FontePerfisComerciais = "perfisComerciais";
    public const string FonteCondicoesPagamento = "condicoesPagamento";
    public const string FonteEmpresas = "empresas";
    public const string FonteCargos = "cargos";
    public const string FonteDepartamentos = "departamentos";
    public const string FonteSetores = "setores";
    public const string FonteTiposRelacionamento = "tiposRelacionamento";
    public const string FonteOrigens = "origens";
    public const string FonteFinalidadesTratamento = "finalidadesTratamento";

    /// <summary>Fontes que o banco preenche (o serviço pede todas numa ida só).</summary>
    public static readonly string[] FontesDoBanco =
    [
        FontePortes, FonteNaturezasJuridicas, FonteGruposEmpresariais, FonteProfissoes, FonteFinalidadesEndereco,
        FonteTiposDocumento, FonteSituacoesReceita, FontePerfisComerciais, FonteCondicoesPagamento, FonteEmpresas,
        FonteCargos, FonteDepartamentos, FonteSetores, FonteTiposRelacionamento, FonteOrigens, FonteFinalidadesTratamento
    ];

    public const int MaximoCondicoes = 30;
    public const int MaximoValores = 50;
    public const int TamanhoMaximoTexto = 100;
    public const int MaximoDias = 3650;

    private static readonly OperadorFiltro[] Lista = [OperadorFiltro.UmDestes, OperadorFiltro.NenhumDestes];
    private static readonly OperadorFiltro[] SimNao = [OperadorFiltro.Sim, OperadorFiltro.Nao];
    private static readonly OperadorFiltro[] Periodo = [OperadorFiltro.Entre, OperadorFiltro.APartirDe, OperadorFiltro.Ate];
    private static readonly OperadorFiltro[] Faixa = [OperadorFiltro.Entre, OperadorFiltro.APartirDe, OperadorFiltro.Ate];
    private static readonly OperadorFiltro[] Texto = [OperadorFiltro.Contem, OperadorFiltro.ComecaCom, OperadorFiltro.Igual];
    private static readonly OperadorFiltro[] TemTipo = [OperadorFiltro.UmDestes, OperadorFiltro.NenhumDestes];
    private static readonly OperadorFiltro[] Preenchimento = [OperadorFiltro.Igual, OperadorFiltro.Vazio, OperadorFiltro.NaoVazio];
    private const string PermissaoFinanceiro = Lone.Contracts.Seguranca.Permissoes.Pessoas.VisualizarFinanceiro;
    private const string PermissaoColaborador = Lone.Contracts.Seguranca.Permissoes.Pessoas.Colaborador;
    private const string PermissaoPrivacidade = Lone.Contracts.Seguranca.Permissoes.Pessoas.Privacidade;

    public static IReadOnlyList<DefinicaoCampoFiltro> Campos { get; } =
    [
        // ---- Identificação ----
        new(CamposFiltroPessoas.Nome, "Identificação", "Nome", TipoCampoFiltro.Texto, Texto,
            Dica: "Nome ou razão social, e o nome que aparece na lista."),
        new(CamposFiltroPessoas.Codigo, "Identificação", "Código", TipoCampoFiltro.Numero, Faixa),
        new(CamposFiltroPessoas.Documento, "Identificação", "CPF / CNPJ (número ou parte)", TipoCampoFiltro.Texto,
            [OperadorFiltro.Contem, OperadorFiltro.ComecaCom], Dica: "Só os dígitos importam. Inclui o CNPJ de qualquer estabelecimento.",
            SomenteDigitos: true, TamanhoMinimo: 1, TamanhoMaximo: 14),
        new(CamposFiltroPessoas.Natureza, "Identificação", "Natureza", TipoCampoFiltro.Lista, Lista,
            Opcoes: OpcoesDe<NaturezaPessoa>(NomesPessoa.Natureza), ValorValido: EnumValido<NaturezaPessoa>),
        new(CamposFiltroPessoas.Papeis, "Identificação", "Papéis", TipoCampoFiltro.Lista,
            [OperadorFiltro.UmDestes, OperadorFiltro.TodosDestes, OperadorFiltro.NenhumDestes],
            FonteOpcoes: FontePapeis, ValorValido: GuidValido),
        new(CamposFiltroPessoas.Etiquetas, "Identificação", "Etiquetas", TipoCampoFiltro.Lista, Lista,
            FonteOpcoes: FonteEtiquetas, ValorValido: GuidValido),
        new(CamposFiltroPessoas.NomeFantasia, "Identificação", "Nome fantasia", TipoCampoFiltro.Texto, Texto, ValeParaNatureza.Juridica,
            Dica: "Qualquer estabelecimento (matriz ou filial)."),
        new(CamposFiltroPessoas.DataNascimento, "Identificação", "Data de nascimento", TipoCampoFiltro.Data, Periodo, ValeParaNatureza.Fisica),
        new(CamposFiltroPessoas.Aniversario, "Identificação", "Aniversário no mês", TipoCampoFiltro.Lista, [OperadorFiltro.UmDestes],
            ValeParaNatureza.Fisica, Opcoes: Meses(), ValorValido: v => int.TryParse(v, out var m) && m is >= 1 and <= 12),
        new(CamposFiltroPessoas.Idade, "Identificação", "Idade (anos)", TipoCampoFiltro.Numero, Faixa, ValeParaNatureza.Fisica,
            Dica: "Pela data de nascimento."),
        new(CamposFiltroPessoas.DataAbertura, "Identificação", "Data de abertura", TipoCampoFiltro.Data, Periodo, ValeParaNatureza.Juridica),
        new(CamposFiltroPessoas.Porte, "Identificação", "Porte", TipoCampoFiltro.Lista, Lista, ValeParaNatureza.Juridica,
            Dica: "Como veio da Receita.", FonteOpcoes: FontePortes),
        new(CamposFiltroPessoas.NaturezaJuridica, "Identificação", "Natureza jurídica", TipoCampoFiltro.Lista, Lista,
            ValeParaNatureza.Juridica, FonteOpcoes: FonteNaturezasJuridicas,
            ValorValido: v => v.Length is > 0 and <= 10 && v.All(char.IsAsciiDigit)),
        new(CamposFiltroPessoas.GrupoEmpresarial, "Identificação", "Grupo empresarial", TipoCampoFiltro.Lista, Lista,
            ValeParaNatureza.Juridica, FonteOpcoes: FonteGruposEmpresariais, ValorValido: GuidValido),

        // ---- Dados pessoais ----
        new(CamposFiltroPessoas.Sexo, "Dados pessoais", "Sexo", TipoCampoFiltro.Lista, Lista, ValeParaNatureza.Fisica,
            Opcoes: OpcoesDe<SexoRegistro>(NomesPessoa.Sexo), ValorValido: EnumValido<SexoRegistro>),
        new(CamposFiltroPessoas.EstadoCivil, "Dados pessoais", "Estado civil", TipoCampoFiltro.Lista, Lista, ValeParaNatureza.Fisica,
            Opcoes: OpcoesDe<EstadoCivil>(NomesPessoa.EstadoCivil), ValorValido: EnumValido<EstadoCivil>),
        new(CamposFiltroPessoas.Profissao, "Dados pessoais", "Profissão", TipoCampoFiltro.Lista, Lista, ValeParaNatureza.Fisica,
            FonteOpcoes: FonteProfissoes, ValorValido: GuidValido),

        // ---- Telefones e e-mails ----
        new(CamposFiltroPessoas.Telefone, "Telefones e e-mails", "Telefone (número ou parte)", TipoCampoFiltro.Texto, Texto,
            Dica: "Com ou sem DDD, só os dígitos importam (ex.: 99988 ou 38999887766). Inclui os das pessoas de contato.",
            SomenteDigitos: true, TamanhoMinimo: 1, TamanhoMaximo: 15),
        new(CamposFiltroPessoas.Ddd, "Telefones e e-mails", "DDD", TipoCampoFiltro.Texto, [OperadorFiltro.Igual],
            Dica: "Dois dígitos (ex.: 38). Telefones nacionais.", SomenteDigitos: true, TamanhoMinimo: 2, TamanhoMaximo: 2),
        new(CamposFiltroPessoas.Email, "Telefones e e-mails", "E-mail (endereço ou parte)", TipoCampoFiltro.Texto, Texto,
            Dica: "Ex.: \"@gmail.com\" acha todos do domínio. Inclui os das pessoas de contato."),
        new(CamposFiltroPessoas.TipoContato, "Telefones e e-mails", "Tem contato do tipo", TipoCampoFiltro.Lista, TemTipo,
            Opcoes:
            [
                new(nameof(TipoContato.Celular), "Celular"), new(nameof(TipoContato.Telefone), "Telefone fixo"),
                new(nameof(TipoContato.WhatsApp), "WhatsApp"), new(nameof(TipoContato.Email), "E-mail"), new(nameof(TipoContato.Outro), "Outro")
            ],
            ValorValido: EnumValido<TipoContato>),
        new(CamposFiltroPessoas.WhatsApp, "Telefones e e-mails", "Tem WhatsApp", TipoCampoFiltro.SimNao, SimNao),
        new(CamposFiltroPessoas.FinalidadeContato, "Telefones e e-mails", "Tem contato para", TipoCampoFiltro.Lista, TemTipo,
            Dica: "Ex.: \"não é nenhum destes: NF-e\" = sem e-mail de NF-e.",
            Opcoes:
            [
                new(nameof(FinalidadeEmail.NFe), "NF-e"), new(nameof(FinalidadeEmail.Cobranca), "Cobrança"),
                new(nameof(FinalidadeEmail.Financeiro), "Financeiro"), new(nameof(FinalidadeEmail.Marketing), "Marketing")
            ],
            ValorValido: v => EnumValido<FinalidadeEmail>(v) && v != nameof(FinalidadeEmail.Nenhuma)),
        new(CamposFiltroPessoas.AceitaComunicacoes, "Telefones e e-mails", "Aceita comunicações", TipoCampoFiltro.SimNao, SimNao,
            Dica: "Algum telefone ou e-mail ativo que permite comunicação."),
        new(CamposFiltroPessoas.SemContato, "Telefones e e-mails", "Sem telefone nem e-mail", TipoCampoFiltro.SimNao, SimNao),

        // ---- Pessoas de contato ----
        new(CamposFiltroPessoas.CargoContato, "Pessoas de contato", "Cargo do contato", TipoCampoFiltro.Texto, Texto),

        // ---- Endereços ----
        new(CamposFiltroPessoas.Uf, "Endereços", "UF", TipoCampoFiltro.Lista, Lista,
            Dica: "Qualquer endereço ativo.",
            Opcoes: [.. Ufs.Todas.Order(StringComparer.Ordinal).Select(u => new OpcaoFiltroDto(u, u))],
            ValorValido: v => Ufs.Todas.Contains(v) || v == Ufs.Exterior),
        new(CamposFiltroPessoas.Municipio, "Endereços", "Município", TipoCampoFiltro.Lista, Lista,
            Dica: "Qualquer endereço ativo.", FonteOpcoes: FonteMunicipios,
            ValorValido: v => int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0),
        new(CamposFiltroPessoas.MunicipioACorrigir, "Endereços", "Município a corrigir", TipoCampoFiltro.SimNao, SimNao,
            Dica: "Endereço antigo com o município ainda por escolher na tabela do IBGE."),
        new(CamposFiltroPessoas.FinalidadeEndereco, "Endereços", "Tem endereço para", TipoCampoFiltro.Lista, TemTipo,
            Dica: "Entrega, cobrança, fiscal...", FonteOpcoes: FonteFinalidadesEndereco, ValorValido: GuidValido),
        new(CamposFiltroPessoas.Cidade, "Endereços", "Cidade (nome)", TipoCampoFiltro.Texto, Texto,
            Dica: "Qualquer endereço ativo. Para escolher da tabela do IBGE, use \"Município\"."),
        new(CamposFiltroPessoas.Bairro, "Endereços", "Bairro", TipoCampoFiltro.Texto, Texto),
        new(CamposFiltroPessoas.Cep, "Endereços", "CEP", TipoCampoFiltro.Texto, [OperadorFiltro.ComecaCom, OperadorFiltro.Igual],
            Dica: "\"Começa com\" pega uma região (ex.: 35790).", SomenteDigitos: true, TamanhoMinimo: 1, TamanhoMaximo: 8),
        new(CamposFiltroPessoas.SemEndereco, "Endereços", "Sem endereço", TipoCampoFiltro.SimNao, SimNao),

        // ---- Documentos ----
        new(CamposFiltroPessoas.DocumentosVencidos, "Documentos", "Documentos vencidos", TipoCampoFiltro.SimNao, SimNao),
        new(CamposFiltroPessoas.DocumentosVencendo, "Documentos", "Documentos vencendo", TipoCampoFiltro.Numero,
            [OperadorFiltro.EmAteDias], Dica: "Nos próximos N dias (sem contar os já vencidos)."),
        new(CamposFiltroPessoas.DocumentosVencendoPeloAviso, "Documentos", "Documentos vencendo (aviso do tipo)", TipoCampoFiltro.SimNao,
            SimNao, Dica: "Dentro da antecedência de aviso de cada tipo de documento, como no resumo da pessoa (sem os já vencidos)."),
        new(CamposFiltroPessoas.TipoDocumento, "Documentos", "Tem documento do tipo", TipoCampoFiltro.Lista, TemTipo,
            Dica: "Ex.: \"não é nenhum destes: Alvará\" = sem alvará.", FonteOpcoes: FonteTiposDocumento, ValorValido: GuidValido),
        new(CamposFiltroPessoas.DocumentoValidoAte, "Documentos", "Documento válido até", TipoCampoFiltro.Data, Periodo,
            Dica: "Algum documento ativo com a validade no período."),

        // ---- Fiscal ----
        new(CamposFiltroPessoas.ProdutorRural, "Fiscal", "Produtor rural", TipoCampoFiltro.SimNao, SimNao),
        new(CamposFiltroPessoas.Regime, "Fiscal", "Regime tributário", TipoCampoFiltro.Lista, Lista, ValeParaNatureza.Juridica,
            Opcoes:
            [
                new(nameof(RegimeTributario.SimplesNacional), "Simples Nacional"),
                new(nameof(RegimeTributario.Mei), "MEI"),
                new(nameof(RegimeTributario.RegimeNormal), "Regime normal"),
                new(nameof(RegimeTributario.NaoInformado), "Não informado")
            ],
            ValorValido: EnumValido<RegimeTributario>),
        new(CamposFiltroPessoas.Cnae, "Fiscal", "CNAE (principal ou secundário)", TipoCampoFiltro.Texto,
            [OperadorFiltro.ComecaCom], ValeParaNatureza.Juridica, Dica: "Código ou começo dele (ex.: 47 = comércio varejista).",
            ValorValido: CnaeValido, SomenteDigitos: true, TamanhoMinimo: 1, TamanhoMaximo: 7),
        new(CamposFiltroPessoas.CnaePrincipal, "Fiscal", "CNAE principal", TipoCampoFiltro.Texto,
            [OperadorFiltro.ComecaCom], ValeParaNatureza.Juridica, Dica: "Código ou começo dele (ex.: 4711302 ou 47).",
            ValorValido: CnaeValido, SomenteDigitos: true, TamanhoMinimo: 1, TamanhoMaximo: 7),
        new(CamposFiltroPessoas.IndicadorIE, "Fiscal", "Indicador de IE", TipoCampoFiltro.Lista, Lista,
            Opcoes:
            [
                new(nameof(IndicadorIE.Contribuinte), "Contribuinte do ICMS"), new(nameof(IndicadorIE.Isento), "Contribuinte isento"),
                new(nameof(IndicadorIE.NaoContribuinte), "Não contribuinte"), new(nameof(IndicadorIE.NaoInformado), "Não informado")
            ],
            ValorValido: EnumValido<IndicadorIE>),
        new(CamposFiltroPessoas.InscricaoEstadual, "Fiscal", "Inscrição estadual", TipoCampoFiltro.Texto, Preenchimento,
            Dica: "Ex.: \"não preenchido\" com o indicador \"contribuinte\" = contribuintes sem IE.", SomenteDigitos: true,
            TamanhoMinimo: 1, TamanhoMaximo: 14),
        new(CamposFiltroPessoas.SituacaoReceita, "Fiscal", "Situação na Receita", TipoCampoFiltro.Lista, Lista, ValeParaNatureza.Juridica,
            Dica: "Da última consulta de CNPJ.", FonteOpcoes: FonteSituacoesReceita),

        // ---- Comercial – Cliente ----
        new(CamposFiltroPessoas.Vendedor, "Comercial – Cliente", "Vendedor (carteira)", TipoCampoFiltro.Lista,
            [OperadorFiltro.UmDestes], Dica: "Carteira vigente hoje.", FonteOpcoes: FonteVendedores, ValorValido: GuidValido),
        new(CamposFiltroPessoas.SemCarteira, "Comercial – Cliente", "Sem vendedor na carteira", TipoCampoFiltro.SimNao, SimNao),
        new(CamposFiltroPessoas.LimiteCredito, "Comercial – Cliente", "Limite de crédito (R$)", TipoCampoFiltro.Numero, Faixa,
            Permissao: PermissaoFinanceiro),
        new(CamposFiltroPessoas.PerfilComercial, "Comercial – Cliente", "Perfil comercial", TipoCampoFiltro.Lista, Lista,
            FonteOpcoes: FontePerfisComerciais, ValorValido: GuidValido),
        new(CamposFiltroPessoas.CondicaoCliente, "Comercial – Cliente", "Condição de pagamento", TipoCampoFiltro.Lista, Lista,
            FonteOpcoes: FonteCondicoesPagamento, ValorValido: GuidValido),

        // ---- Comercial – Fornecedor ----
        new(CamposFiltroPessoas.CondicaoFornecedor, "Comercial – Fornecedor", "Condição de pagamento", TipoCampoFiltro.Lista, Lista,
            FonteOpcoes: FonteCondicoesPagamento, ValorValido: GuidValido),
        new(CamposFiltroPessoas.AvaliacaoFornecedor, "Comercial – Fornecedor", "Avaliação", TipoCampoFiltro.Numero, Faixa),

        // ---- Colaborador ----
        new(CamposFiltroPessoas.EmpresaVinculo, "Colaborador", "Empresa do vínculo", TipoCampoFiltro.Lista, Lista, ValeParaNatureza.Fisica,
            PermissaoColaborador, FonteOpcoes: FonteEmpresas, ValorValido: GuidValido),
        new(CamposFiltroPessoas.TipoVinculo, "Colaborador", "Tipo de vínculo", TipoCampoFiltro.Lista, Lista, ValeParaNatureza.Fisica,
            PermissaoColaborador,
            Opcoes:
            [
                new(nameof(TipoVinculo.Clt), "CLT"), new(nameof(TipoVinculo.Estagio), "Estágio"), new(nameof(TipoVinculo.Aprendiz), "Aprendiz"),
                new(nameof(TipoVinculo.Temporario), "Temporário"), new(nameof(TipoVinculo.PrestadorPj), "Prestador PJ"),
                new(nameof(TipoVinculo.Autonomo), "Autônomo"), new(nameof(TipoVinculo.Diretor), "Diretor"), new(nameof(TipoVinculo.Outro), "Outro")
            ],
            ValorValido: EnumValido<TipoVinculo>),
        new(CamposFiltroPessoas.Admissao, "Colaborador", "Admissão", TipoCampoFiltro.Data, Periodo, ValeParaNatureza.Fisica, PermissaoColaborador),
        new(CamposFiltroPessoas.ColaboradorAtivo, "Colaborador", "Colaborador ativo", TipoCampoFiltro.SimNao, SimNao, ValeParaNatureza.Fisica,
            PermissaoColaborador, Dica: "Sim: com vínculo sem desligamento. Não: todos os vínculos desligados."),
        new(CamposFiltroPessoas.Cargo, "Colaborador", "Cargo", TipoCampoFiltro.Lista, Lista, ValeParaNatureza.Fisica,
            PermissaoColaborador, Dica: "Lotação vigente.", FonteOpcoes: FonteCargos, ValorValido: GuidValido),
        new(CamposFiltroPessoas.Departamento, "Colaborador", "Departamento", TipoCampoFiltro.Lista, Lista, ValeParaNatureza.Fisica,
            PermissaoColaborador, Dica: "Lotação vigente.", FonteOpcoes: FonteDepartamentos, ValorValido: GuidValido),
        new(CamposFiltroPessoas.Setor, "Colaborador", "Setor", TipoCampoFiltro.Lista, Lista, ValeParaNatureza.Fisica,
            PermissaoColaborador, Dica: "Lotação vigente.", FonteOpcoes: FonteSetores, ValorValido: GuidValido),

        // ---- Relacionamentos ----
        new(CamposFiltroPessoas.TipoRelacionamento, "Relacionamentos", "Tipo de relacionamento", TipoCampoFiltro.Lista, TemTipo,
            Dica: "Sócio de, administrador de, contato de...", FonteOpcoes: FonteTiposRelacionamento, ValorValido: GuidValido),
        new(CamposFiltroPessoas.PessoaRelacionada, "Relacionamentos", "Relacionado a (nome)", TipoCampoFiltro.Texto,
            [OperadorFiltro.Contem, OperadorFiltro.ComecaCom], Dica: "Ex.: todas as empresas de um sócio."),

        // ---- Interações ----
        new(CamposFiltroPessoas.Origem, "Interações", "Como conheceu a empresa", TipoCampoFiltro.Lista, Lista, FonteOpcoes: FonteOrigens),
        new(CamposFiltroPessoas.Relacionamento, "Interações", "Situação do relacionamento", TipoCampoFiltro.Lista, Lista,
            Opcoes:
            [
                new(nameof(SituacaoRelacionamento.Ativo), "Ativo"),
                new(nameof(SituacaoRelacionamento.EmRisco), "Em risco"),
                new(nameof(SituacaoRelacionamento.Inativo), "Inativo"),
                new(nameof(SituacaoRelacionamento.SemInteracao), "Sem interação")
            ],
            ValorValido: EnumValido<SituacaoRelacionamento>),
        new(CamposFiltroPessoas.SemInteracao, "Interações", "Sem interação há", TipoCampoFiltro.Numero,
            [OperadorFiltro.HaMaisDeDias], Dica: "Mais de N dias sem interação (inclui quem nunca teve)."),

        // ---- Privacidade ----
        new(CamposFiltroPessoas.ConsentimentoEmVigor, "Privacidade", "Consentimento em vigor para", TipoCampoFiltro.Lista, TemTipo,
            Permissao: PermissaoPrivacidade, Dica: "Ex.: quem aceitou marketing.", FonteOpcoes: FonteFinalidadesTratamento, ValorValido: GuidValido),
        new(CamposFiltroPessoas.ConsentimentoRevogado, "Privacidade", "Consentimento revogado para", TipoCampoFiltro.Lista,
            [OperadorFiltro.UmDestes], Permissao: PermissaoPrivacidade, FonteOpcoes: FonteFinalidadesTratamento, ValorValido: GuidValido),
        new(CamposFiltroPessoas.CanalConsentimento, "Privacidade", "Canal com consentimento em vigor", TipoCampoFiltro.Lista,
            [OperadorFiltro.UmDestes], Permissao: PermissaoPrivacidade,
            Opcoes: OpcoesDe<CanalComunicacao>(NomesPessoa.Canal), ValorValido: EnumValido<CanalComunicacao>),

        // ---- Situação ----
        new(CamposFiltroPessoas.Situacao, "Situação", "Situação do cadastro", TipoCampoFiltro.Lista, Lista,
            Dica: "Sem este filtro: ativos e em análise.",
            Opcoes: OpcoesDe<SituacaoPessoa>(NomesPessoa.Situacao), ValorValido: EnumValido<SituacaoPessoa>),
        new(CamposFiltroPessoas.Bloqueado, "Situação", "Com bloqueio ativo", TipoCampoFiltro.SimNao, SimNao),
        new(CamposFiltroPessoas.EscopoBloqueio, "Situação", "Bloqueio ativo do tipo", TipoCampoFiltro.Lista, [OperadorFiltro.UmDestes],
            Opcoes:
            [
                new(nameof(EscopoBloqueio.Comercial), "Comercial"), new(nameof(EscopoBloqueio.Financeiro), "Financeiro"),
                new(nameof(EscopoBloqueio.Cadastral), "Cadastral"), new(nameof(EscopoBloqueio.Faturamento), "Faturamento")
            ],
            ValorValido: EnumValido<EscopoBloqueio>),

        // ---- Cadastro ----
        new(CamposFiltroPessoas.CadastradoEm, "Cadastro", "Cadastrado em", TipoCampoFiltro.Data, Periodo),
        new(CamposFiltroPessoas.ComPendenciaCadastral, "Cadastro", "Com pendência cadastral", TipoCampoFiltro.SimNao, SimNao,
            Dica: "Sem CPF/CNPJ, sem endereço, com município a corrigir ou contribuinte do ICMS sem inscrição estadual."),
        new(CamposFiltroPessoas.AlteradoEm, "Cadastro", "Alterado em", TipoCampoFiltro.Data, Periodo)
    ];

    private static readonly Dictionary<string, DefinicaoCampoFiltro> PorId = Campos.ToDictionary(c => c.Id, StringComparer.Ordinal);

    public const string GrupoInformacoesAdicionais = "Informações adicionais";

    /// <summary>Operadores de um campo personalizado pesquisável (o índice guarda o começo do valor).</summary>
    public static readonly OperadorFiltro[] OperadoresCampoPersonalizado = [OperadorFiltro.ComecaCom, OperadorFiltro.NaoVazio];

    /// <summary>A definição do campo (campo personalizado: uma definição genérica); nulo = campo desconhecido.</summary>
    public static DefinicaoCampoFiltro? Obter(string campo) =>
        PorId.TryGetValue(campo, out var definicao) ? definicao
        : CamposFiltroPessoas.IdCampoPersonalizado(campo) is not null
            ? new DefinicaoCampoFiltro(campo, GrupoInformacoesAdicionais, "Campo personalizado", TipoCampoFiltro.Texto,
                OperadoresCampoPersonalizado)
            : null;

    // ---- Opções e conferência de valores ----

    private static OpcaoFiltroDto[] Meses() =>
        [.. Enumerable.Range(1, 12).Select(m => new OpcaoFiltroDto(m.ToString(CultureInfo.InvariantCulture),
            CultureInfo.GetCultureInfo("pt-BR").DateTimeFormat.GetMonthName(m) is var n ? char.ToUpper(n[0]) + n[1..] : m.ToString()))];

    private static OpcaoFiltroDto[] OpcoesDe<T>(Func<T, string> nome) where T : struct, Enum =>
        [.. Enum.GetValues<T>().Select(v => new OpcaoFiltroDto(v.ToString(), nome(v)))];

    public static bool EnumValido<T>(string valor) where T : struct, Enum =>
        Enum.TryParse<T>(valor, ignoreCase: false, out var v) && Enum.IsDefined(v) && !int.TryParse(valor, out _);

    public static bool GuidValido(string valor) => Guid.TryParse(valor, out var id) && id != Guid.Empty;

    private static bool CnaeValido(string valor) => valor.Length is > 0 and <= 7 && valor.All(char.IsAsciiDigit);

    /// <summary>
    /// Limpa e confere as condições (campo conhecido, operador do campo, quantidade e formato dos valores).
    /// Permissões ficam com o serviço (precisa do usuário). Devolve os erros; as condições ficam normalizadas.
    /// </summary>
    public static List<string> Normalizar(List<CondicaoFiltro> condicoes)
    {
        var erros = new List<string>();
        if (condicoes.Count > MaximoCondicoes)
            erros.Add($"Filtros demais: use no máximo {MaximoCondicoes} condições.");

        foreach (var c in condicoes)
        {
            c.Campo = (c.Campo ?? string.Empty).Trim();
            c.Valores = (c.Valores ?? []).Select(v => (v ?? string.Empty).Trim()).Where(v => v.Length > 0).Distinct().ToList();
            var definicao = Obter(c.Campo);
            if (definicao is null)
            {
                erros.Add($"Campo de filtro desconhecido: \"{c.Campo}\".");
                continue;
            }
            if (!definicao.Operadores.Contains(c.Operador))
            {
                erros.Add($"\"{definicao.Nome}\": operador não aceito.");
                continue;
            }
            if (ErroNosValores(definicao, c) is { } erro) erros.Add($"\"{definicao.Nome}\": {erro}");
        }
        return erros.Distinct().ToList();
    }

    private static string? ErroNosValores(DefinicaoCampoFiltro d, CondicaoFiltro c)
    {
        var v = c.Valores;
        switch (c.Operador)
        {
            case OperadorFiltro.Sim or OperadorFiltro.Nao or OperadorFiltro.Vazio or OperadorFiltro.NaoVazio:
                c.Valores = [];
                return null;

            case OperadorFiltro.EmAteDias or OperadorFiltro.HaMaisDeDias:
                return v.Count == 1 && int.TryParse(v[0], NumberStyles.None, CultureInfo.InvariantCulture, out var dias) &&
                       dias is >= 1 and <= MaximoDias
                    ? null
                    : $"informe um número de dias entre 1 e {MaximoDias:N0}.";

            case OperadorFiltro.UmDestes or OperadorFiltro.TodosDestes or OperadorFiltro.NenhumDestes:
                if (v.Count == 0) return "escolha pelo menos uma opção.";
                if (v.Count > MaximoValores) return $"escolha no máximo {MaximoValores} opções.";
                return d.ValorValido is { } valida && !v.All(valida) ? "opção inválida." : null;

            case OperadorFiltro.Entre or OperadorFiltro.APartirDe or OperadorFiltro.Ate or OperadorFiltro.Maior or OperadorFiltro.Menor:
                var quantos = c.Operador == OperadorFiltro.Entre ? 2 : 1;
                if (v.Count != quantos) return c.Operador == OperadorFiltro.Entre ? "informe o início e o fim." : "informe o valor.";
                if (d.Tipo == TipoCampoFiltro.Data)
                {
                    if (!v.All(x => DateOnly.TryParseExact(x, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
                        return "data inválida.";
                    if (quantos == 2 && string.CompareOrdinal(v[1], v[0]) < 0) return "o fim é anterior ao início.";
                    return null;
                }
                if (!v.All(x => decimal.TryParse(x, NumberStyles.Number, CultureInfo.InvariantCulture, out _))) return "número inválido.";
                return quantos == 2 && decimal.Parse(v[1], CultureInfo.InvariantCulture) < decimal.Parse(v[0], CultureInfo.InvariantCulture)
                    ? "o fim é menor que o início."
                    : null;

            default: // Contem, ComecaCom, Igual
                if (v.Count != 1) return "informe o texto.";
                if (v[0].Length > TamanhoMaximoTexto) return $"texto com mais de {TamanhoMaximoTexto} caracteres.";
                if (d.SomenteDigitos)
                    c.Valores = [new string(v[0].Where(char.IsAsciiDigit).ToArray())];
                if (d.SomenteDigitos && c.Valores[0].Length == 0) return "digite números.";
                if (d.TamanhoMinimo is { } minimo && c.Valores[0].Length < minimo)
                    return minimo == d.TamanhoMaximo ? $"informe {minimo} dígitos." : $"informe pelo menos {minimo} caracteres.";
                if (d.TamanhoMaximo is { } maximo && c.Valores[0].Length > maximo)
                    return d.SomenteDigitos ? $"no máximo {maximo} dígitos." : $"no máximo {maximo} caracteres.";
                return d.ValorValido is { } validaTexto && !validaTexto(c.Valores[0]) ? "valor inválido." : null;
        }
    }
}
