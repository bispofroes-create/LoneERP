namespace Lone.Contracts.Comum;

/// <summary>
/// Endereços da API, num lugar só: a API mapeia estes caminhos e o aplicativo chama os mesmos.
/// Tudo sob /api/v1 — uma versão nova da API ganha outro prefixo sem quebrar aplicativos antigos.
/// </summary>
public static class Rotas
{
    public const string Base = "api/v1";

    /// <summary>Verificação de saúde (a API está no ar e o banco responde). Sem login.</summary>
    public const string Saude = "saude";

    public static class Autenticacao
    {
        public const string Grupo = Base + "/autenticacao";
        public const string Situacao = Grupo + "/situacao";
        public const string PrimeiroAcesso = Grupo + "/primeiro-acesso";
        public const string Entrar = Grupo + "/entrar";
        public const string Renovar = Grupo + "/renovar";
        public const string Empresa = Grupo + "/empresa";
        public const string Senha = Grupo + "/senha";
        public const string Sair = Grupo + "/sair";
    }

    public static class Pessoas
    {
        public const string Grupo = Base + "/pessoas";
        public static string PorId(Guid id) => $"{Grupo}/{id}";
        public static string Historico(Guid id) => $"{Grupo}/{id}/historico";
        public static string Desativar(Guid id) => $"{Grupo}/{id}/desativar";
        public static string Reativar(Guid id) => $"{Grupo}/{id}/reativar";
        public const string QuantidadeClientesAtivos = Grupo + "/indicadores/clientes-ativos";

        /// <summary>Pessoas físicas ativas por faixa etária (?papel=Cliente opcional).</summary>
        public const string FaixasEtarias = Grupo + "/indicadores/faixas-etarias";
    }

    /// <summary>Cadastro de profissões e tabela oficial da CBO. Nada é excluído: desativa ou mescla.</summary>
    public static class Profissoes
    {
        public const string Grupo = Base + "/profissoes";
        public static string PorId(Guid id) => $"{Grupo}/{id}";
        public static string Desativar(Guid id) => $"{Grupo}/{id}/desativar";
        public static string Reativar(Guid id) => $"{Grupo}/{id}/reativar";
        public static string Mesclar(Guid origemId) => $"{Grupo}/{origemId}/mesclar";
        public static string Listar(bool incluirInativas) => incluirInativas ? Grupo + "?incluirInativas=true" : Grupo;

        /// <summary>Todas as ocupações ativas da CBO (cerca de 2.600; o aplicativo filtra no aparelho).</summary>
        public const string Cbo = Grupo + "/cbo";
        public const string CboSituacao = Cbo + "/situacao";
        public const string CboImportar = Cbo + "/importar";
    }

    /// <summary>Cadastro de etiquetas (reutilizáveis nas pessoas). Nada é excluído: desativa ou mescla.</summary>
    public static class Etiquetas
    {
        public const string Grupo = Base + "/etiquetas";
        public static string PorId(Guid id) => $"{Grupo}/{id}";
        public static string Desativar(Guid id) => $"{Grupo}/{id}/desativar";
        public static string Reativar(Guid id) => $"{Grupo}/{id}/reativar";
        public static string Mesclar(Guid origemId) => $"{Grupo}/{origemId}/mesclar";
        public static string Listar(bool incluirInativas) => incluirInativas ? Grupo + "?incluirInativas=true" : Grupo;
    }

    /// <summary>Tabela de municípios do IBGE (referência para naturalidade e endereços).</summary>
    public static class Municipios
    {
        public const string Grupo = Base + "/municipios";

        /// <summary>Todos os municípios de uma UF (o aplicativo guarda e filtra enquanto o usuário digita).</summary>
        public static string DaUf(string uf) => $"{Grupo}?uf={Uri.EscapeDataString(uf)}";

        public const string Situacao = Grupo + "/situacao";

        /// <summary>Relê a lista oficial do IBGE e concilia os textos antigos.</summary>
        public const string Atualizar = Grupo + "/atualizar";
    }

    /// <summary>Campos personalizados criados pelo administrador.</summary>
    public static class CamposPersonalizados
    {
        public const string Grupo = Base + "/campos-personalizados";
        public static string PorId(Guid id) => $"{Grupo}/{id}";
        public static string Desativar(Guid id) => $"{Grupo}/{id}/desativar";
        public static string Reativar(Guid id) => $"{Grupo}/{id}/reativar";

        /// <summary>Nova ordem (lista de Ids).</summary>
        public const string Ordem = Grupo + "/ordem";

        /// <summary>?entidade=Pessoa&amp;incluirInativos=true</summary>
        public static string Listar(Lone.Domain.Enums.EntidadePersonalizavel entidade, bool incluirInativos) =>
            $"{Grupo}?entidade={entidade}" + (incluirInativos ? "&incluirInativos=true" : string.Empty);
    }

    public static class Empresas
    {
        public const string Grupo = Base + "/empresas";
        /// <summary>Empresas do grupo (para atribuir perfis por empresa).</summary>
        public const string DoGrupo = Grupo;
    }

    public static class Usuarios
    {
        public const string Grupo = Base + "/usuarios";
        public static string PorId(Guid id) => $"{Grupo}/{id}";
        public static string Desbloquear(Guid id) => $"{Grupo}/{id}/desbloquear";

        /// <summary>Perfis que podem ser atribuídos (para quem gerencia usuários, sem precisar gerenciar perfis).</summary>
        public const string PerfisDisponiveis = Grupo + "/perfis-disponiveis";
    }

    public static class Perfis
    {
        public const string Grupo = Base + "/perfis";
        public static string PorId(Guid id) => $"{Grupo}/{id}";
        public const string Permissoes = Grupo + "/permissoes";
    }

    public static class Consultas
    {
        public const string Grupo = Base + "/consultas";
        public static string Cep(string cep) => $"{Grupo}/cep/{Uri.EscapeDataString(cep)}";
        public static string Cnpj(string cnpj) => $"{Grupo}/cnpj/{Uri.EscapeDataString(cnpj)}";
    }
}
