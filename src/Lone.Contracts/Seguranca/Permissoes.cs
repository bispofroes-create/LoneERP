namespace Lone.Contracts.Seguranca;

/// <summary>
/// Catálogo de permissões do ERP. Cada módulo acrescenta a sua classe aninhada.
/// O código (ex.: "PESSOAS.EDITAR") é o que fica gravado nos perfis — nunca mude um código existente.
/// </summary>
public static class Permissoes
{
    public static class Pessoas
    {
        public const string Visualizar = "PESSOAS.VISUALIZAR";
        public const string Criar = "PESSOAS.CRIAR";
        public const string Editar = "PESSOAS.EDITAR";
        public const string Inativar = "PESSOAS.INATIVAR";
        public const string AlterarCredito = "PESSOAS.ALTERAR_CREDITO";
        public const string VisualizarFinanceiro = "PESSOAS.VISUALIZAR_FINANCEIRO";
        public const string VisualizarDadosSensiveis = "PESSOAS.VISUALIZAR_DADOS_SENSIVEIS";
        public const string GerenciarEmpresasDoGrupo = "PESSOAS.EMPRESAS_DO_GRUPO";
        public const string Colaborador = "PESSOAS.COLABORADOR";
    }

    public static class Cadastros
    {
        public const string CamposPersonalizados = "CADASTROS.CAMPOS_PERSONALIZADOS";
        public const string TabelasOficiais = "CADASTROS.TABELAS_OFICIAIS";
        public const string Etiquetas = "CADASTROS.ETIQUETAS";
        public const string Profissoes = "CADASTROS.PROFISSOES";
        public const string Papeis = "CADASTROS.PAPEIS";
        public const string Tipos = "CADASTROS.TIPOS";
        public const string EstruturaOrganizacional = "CADASTROS.ESTRUTURA_ORGANIZACIONAL";
    }

    public static class Seguranca
    {
        public const string GerenciarUsuarios = "SEGURANCA.USUARIOS";
        public const string GerenciarPerfis = "SEGURANCA.PERFIS";
        public const string VisualizarAuditoria = "SEGURANCA.AUDITORIA";
    }

    /// <summary>Todas as permissões com descrição (tela de perfis e verificação dos códigos recebidos).</summary>
    public static IReadOnlyList<DefinicaoPermissao> Todas { get; } =
    [
        new(Pessoas.Visualizar, "Pessoas", "Ver o cadastro de pessoas"),
        new(Pessoas.Criar, "Pessoas", "Cadastrar novas pessoas"),
        new(Pessoas.Editar, "Pessoas", "Alterar cadastros existentes"),
        new(Pessoas.Inativar, "Pessoas", "Desativar e reativar cadastros (e arquivar)"),
        new(Pessoas.AlterarCredito, "Pessoas", "Alterar limite de crédito e bloqueios de cliente"),
        new(Pessoas.VisualizarFinanceiro, "Pessoas", "Ver dados financeiros da pessoa"),
        new(Pessoas.VisualizarDadosSensiveis, "Pessoas", "Ver CPF, data de nascimento e outros dados pessoais completos"),
        new(Pessoas.GerenciarEmpresasDoGrupo, "Pessoas", "Marcar ou desmarcar uma pessoa como empresa do grupo"),
        new(Pessoas.Colaborador, "Pessoas", "Ver e alterar os dados de colaborador (vínculos, matrícula, admissão, lotação)"),
        new(Cadastros.CamposPersonalizados, "Cadastros", "Criar, alterar, ordenar e desativar campos personalizados"),
        new(Cadastros.TabelasOficiais, "Cadastros", "Atualizar tabelas oficiais (municípios do IBGE, ocupações da CBO)"),
        new(Cadastros.Etiquetas, "Cadastros", "Criar, alterar, mesclar e desativar etiquetas"),
        new(Cadastros.Profissoes, "Cadastros", "Criar, alterar, mesclar e desativar profissões"),
        new(Cadastros.Papeis, "Cadastros", "Criar, alterar, ordenar e desativar papéis (cliente, fornecedor...)"),
        new(Cadastros.Tipos, "Cadastros", "Criar, alterar e desativar tipos de telefone/e-mail, de endereço e de documento"),
        new(Cadastros.EstruturaOrganizacional, "Cadastros", "Criar, alterar e desativar cargos, departamentos, setores e centros de custo"),
        new(Seguranca.GerenciarUsuarios, "Segurança", "Cadastrar usuários, redefinir senhas e desbloquear acessos"),
        new(Seguranca.GerenciarPerfis, "Segurança", "Criar perfis e escolher suas permissões"),
        new(Seguranca.VisualizarAuditoria, "Segurança", "Consultar a auditoria completa do sistema")
    ];

    public static DefinicaoPermissao? Obter(string codigo) => Todas.FirstOrDefault(p => p.Codigo == codigo);
}

public sealed record DefinicaoPermissao(string Codigo, string Modulo, string Descricao);
