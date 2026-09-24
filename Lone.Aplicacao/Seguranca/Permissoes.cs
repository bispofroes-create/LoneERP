namespace Lone.Aplicacao.Seguranca;

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
    }

    public static class Seguranca
    {
        public const string GerenciarUsuarios = "SEGURANCA.USUARIOS";
        public const string GerenciarPerfis = "SEGURANCA.PERFIS";
        public const string VisualizarAuditoria = "SEGURANCA.AUDITORIA";
    }

    /// <summary>Todas as permissões com descrição, para a tela de perfis (etapa 2).</summary>
    public static IReadOnlyList<DefinicaoPermissao> Todas { get; } =
    [
        new(Pessoas.Visualizar, "Pessoas", "Ver o cadastro de pessoas"),
        new(Pessoas.Criar, "Pessoas", "Cadastrar novas pessoas"),
        new(Pessoas.Editar, "Pessoas", "Alterar cadastros existentes"),
        new(Pessoas.Inativar, "Pessoas", "Inativar e arquivar pessoas"),
        new(Pessoas.AlterarCredito, "Pessoas", "Alterar limite de crédito e bloqueios de cliente"),
        new(Pessoas.VisualizarFinanceiro, "Pessoas", "Ver dados financeiros da pessoa"),
        new(Pessoas.VisualizarDadosSensiveis, "Pessoas", "Ver CPF, data de nascimento e outros dados pessoais completos"),
        new(Pessoas.GerenciarEmpresasDoGrupo, "Pessoas", "Marcar ou desmarcar uma pessoa como empresa do grupo"),
        new(Seguranca.GerenciarUsuarios, "Segurança", "Cadastrar usuários, redefinir senhas e desbloquear acessos"),
        new(Seguranca.GerenciarPerfis, "Segurança", "Criar perfis e escolher suas permissões"),
        new(Seguranca.VisualizarAuditoria, "Segurança", "Consultar a auditoria completa do sistema")
    ];

    public static DefinicaoPermissao? Obter(string codigo) => Todas.FirstOrDefault(p => p.Codigo == codigo);
}

public sealed record DefinicaoPermissao(string Codigo, string Modulo, string Descricao);
