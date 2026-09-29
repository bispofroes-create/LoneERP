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
        public const string Bloquear = "PESSOAS.BLOQUEAR";
        public const string Desbloquear = "PESSOAS.DESBLOQUEAR";
        public const string RegistrarInteracao = "PESSOAS.INTERACOES";
        public const string Exportar = "PESSOAS.EXPORTAR";

        /// <summary>Grupo empresarial da pessoa jurídica e vínculos societários (sócio, administrador).</summary>
        public const string EstruturaEmpresarial = "PESSOAS.ESTRUTURA_EMPRESARIAL";

        /// <summary>Privacidade (LGPD): consultar, conceder e revogar consentimentos e ver o histórico.</summary>
        public const string Privacidade = "PESSOAS.PRIVACIDADE";
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
        public const string Comercial = "CADASTROS.COMERCIAL";
        public const string Parametros = "CADASTROS.PARAMETROS";
        public const string GruposEmpresariais = "CADASTROS.GRUPOS_EMPRESARIAIS";
    }

    public static class Metas
    {
        public const string Visualizar = "METAS.VISUALIZAR";
        public const string Gerenciar = "METAS.GERENCIAR";
        public const string LancarRealizado = "METAS.LANCAR_REALIZADO";
        public const string Fechar = "METAS.FECHAR";
    }

    /// <summary>Módulo Comercial (Motor Comercial, Fase 1c).</summary>
    public static class Comercial
    {
        public const string Visualizar = "COMERCIAL.VISUALIZAR";
        public const string Coberturas = "COMERCIAL.COBERTURAS";
        public const string Transferir = "COMERCIAL.TRANSFERIR";
    }

    /// <summary>
    /// Territórios (Motor Comercial, Fase 2b). Configurar = tipos, mapas, árvore sem uso e responsáveis. Planejar e aplicar
    /// operações territoriais entram com o motor (2b-1b).
    /// </summary>
    public static class Territorios
    {
        public const string Visualizar = "TERRITORIOS.VISUALIZAR";
        public const string Configurar = "TERRITORIOS.CONFIGURAR";
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
        new(Pessoas.Bloquear, "Pessoas", "Bloquear pessoas (comercial, financeiro, cadastral, faturamento)"),
        new(Pessoas.Desbloquear, "Pessoas", "Liberar bloqueios de pessoas"),
        new(Pessoas.RegistrarInteracao, "Pessoas", "Registrar interações (ligações, visitas, e-mails) com as pessoas"),
        new(Pessoas.Colaborador, "Pessoas", "Ver e alterar os dados de colaborador (vínculos, matrícula, admissão, lotação)"),
        new(Pessoas.Exportar, "Pessoas", "Exportar o resultado da consulta avançada (CSV); cada exportação fica na auditoria"),
        new(Pessoas.EstruturaEmpresarial, "Pessoas", "Alterar o grupo empresarial de uma empresa e os vínculos societários (sócio, administrador)"),
        new(Pessoas.Privacidade, "Pessoas", "Privacidade (LGPD): consultar, conceder e revogar consentimentos e ver o histórico"),
        new(Cadastros.CamposPersonalizados, "Cadastros", "Criar, alterar, ordenar e desativar campos personalizados"),
        new(Cadastros.TabelasOficiais, "Cadastros", "Atualizar tabelas oficiais (municípios do IBGE, ocupações da CBO)"),
        new(Cadastros.Etiquetas, "Cadastros", "Criar, alterar, mesclar e desativar etiquetas"),
        new(Cadastros.Profissoes, "Cadastros", "Criar, alterar, mesclar e desativar profissões"),
        new(Cadastros.Papeis, "Cadastros", "Criar, alterar, ordenar e desativar papéis (cliente, fornecedor...)"),
        new(Cadastros.Tipos, "Cadastros", "Criar, alterar e desativar tipos de telefone/e-mail, de endereço e de documento"),
        new(Cadastros.EstruturaOrganizacional, "Cadastros", "Criar, alterar e desativar cargos, departamentos, setores e centros de custo"),
        new(Cadastros.Parametros, "Cadastros", "Alterar parâmetros do cadastro (regras de inatividade do relacionamento)"),
        new(Cadastros.GruposEmpresariais, "Cadastros", "Criar, alterar e desativar grupos empresariais"),
        new(Cadastros.Comercial, "Cadastros", "Criar, alterar e desativar perfis comerciais, condições de pagamento e tipos de carteira"),
        new(Comercial.Visualizar, "Comercial", "Ver ausências, coberturas e a carteira vencendo"),
        new(Comercial.Coberturas, "Comercial", "Cadastrar, encerrar e cancelar ausências e coberturas"),
        new(Comercial.Transferir, "Comercial", "Transferir a carteira de clientes de uma pessoa para outra e ver as transferências"),
        new(Territorios.Visualizar, "Territórios", "Ver mapas territoriais, a árvore de territórios, os responsáveis e o histórico"),
        new(Territorios.Configurar, "Territórios", "Criar e alterar tipos de território, mapas, a árvore (sem uso operacional) e os responsáveis"),
        new(Metas.Visualizar, "Metas", "Ver metas, participantes e resultados"),
        new(Metas.Gerenciar, "Metas", "Criar e alterar metas, indicadores e equipes; publicar e iniciar a apuração"),
        new(Metas.LancarRealizado, "Metas", "Lançar e importar o realizado informado das metas"),
        new(Metas.Fechar, "Metas", "Fechar (aprovar) e reabrir a apuração das metas"),
        new(Seguranca.GerenciarUsuarios, "Segurança", "Cadastrar usuários, redefinir senhas e desbloquear acessos"),
        new(Seguranca.GerenciarPerfis, "Segurança", "Criar perfis e escolher suas permissões"),
        new(Seguranca.VisualizarAuditoria, "Segurança", "Consultar a auditoria completa do sistema")
    ];

    public static DefinicaoPermissao? Obter(string codigo) => Todas.FirstOrDefault(p => p.Codigo == codigo);
}

public sealed record DefinicaoPermissao(string Codigo, string Modulo, string Descricao);
