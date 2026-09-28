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
        /// <summary>Lista paginada com total (tela de Pessoas): ?pagina=1&amp;tamanho=50 mais os filtros da lista.</summary>
        public const string Pagina = Grupo + "/pagina";
        /// <summary>CPF ou CNPJ (raiz) já cadastrado em outra pessoa. POST: o documento vai no corpo, não na URL.</summary>
        public const string DocumentoEmUso = Grupo + "/documento-em-uso";
        public static string Historico(Guid id) => $"{Grupo}/{id}/historico";
        public static string Bloquear(Guid id) => $"{Grupo}/{id}/bloqueios";
        public static string LiberarBloqueio(Guid id, Guid bloqueioId) => $"{Grupo}/{id}/bloqueios/{bloqueioId}/liberar";
        public static string Interacoes(Guid id) => $"{Grupo}/{id}/interacoes";
        public const string ParametrosRelacionamento = Grupo + "/parametros-relacionamento";

        /// <summary>Relacionamentos entre pessoas (sócio de, administrador de...): operações próprias, fora da ficha.</summary>
        public static string Relacionamentos(Guid id) => $"{Grupo}/{id}/relacionamentos";
        public static string EncerrarRelacionamento(Guid id, Guid relacionamentoId) => $"{Grupo}/{id}/relacionamentos/{relacionamentoId}/encerrar";
        public static string DesativarRelacionamento(Guid id, Guid relacionamentoId) => $"{Grupo}/{id}/relacionamentos/{relacionamentoId}/desativar";

        /// <summary>Privacidade (LGPD): leitura da aba e as ações próprias de conceder/revogar consentimento.</summary>
        public static string Privacidade(Guid id) => $"{Grupo}/{id}/privacidade";
        public static string Consentimentos(Guid id) => $"{Grupo}/{id}/consentimentos";
        public static string RevogarConsentimento(Guid id, Guid consentimentoId) => $"{Grupo}/{id}/consentimentos/{consentimentoId}/revogar";

        /// <summary>Tipos de relacionamento e grupos empresariais para a ficha (lidos quando a aba abre).</summary>
        public const string OpcoesEstrutura = Grupo + "/estrutura/opcoes";

        /// <summary>Próxima página do histórico: registros anteriores ao Id informado.</summary>
        public static string Historico(Guid id, long? antes, int limite) =>
            $"{Historico(id)}?limite={limite}" + (antes is { } a ? $"&antes={a}" : string.Empty);
        public static string Desativar(Guid id) => $"{Grupo}/{id}/desativar";
        public static string Reativar(Guid id) => $"{Grupo}/{id}/reativar";
        /// <summary>Consolidar um endereço duplicado em outro da mesma pessoa (o servidor decide o resultado).</summary>
        public static string ConsolidarEnderecos(Guid id) => $"{Grupo}/{id}/enderecos/consolidar";
        public const string QuantidadeClientesAtivos = Grupo + "/indicadores/clientes-ativos";

        /// <summary>Pessoas físicas ativas por faixa etária (?papel=Cliente opcional).</summary>
        public const string FaixasEtarias = Grupo + "/indicadores/faixas-etarias";

        /// <summary>Consulta avançada (critérios tipados no corpo, paginação por chave), exportação e filtros salvos.</summary>
        public const string Consulta = Grupo + "/consulta";
        public const string Exportar = Consulta + "/exportar";
        public const string OpcoesConsulta = Consulta + "/opcoes";
        /// <summary>Catálogo de campos do filtro (grupos, tipos, operadores, opções), já filtrado pelas permissões.</summary>
        public const string CatalogoFiltros = Consulta + "/catalogo";
        public const string FiltrosSalvos = Consulta + "/filtros";
        public static string FiltroSalvo(Guid id) => $"{FiltrosSalvos}/{id}";
        public static string DesativarFiltro(Guid id) => $"{FiltrosSalvos}/{id}/desativar";
        /// <summary>Quantas pessoas cada visão salva traz (abas de visão da lista). POST com os Ids.</summary>
        public const string ContagemFiltros = FiltrosSalvos + "/contagens";
        /// <summary>Faixa de indicadores da lista (base toda): com bloqueio, documentos vencidos/vencendo, com pendência.</summary>
        public const string Indicadores = Consulta + "/indicadores";
    }

    /// <summary>Grupos empresariais (conjuntos de pessoas jurídicas independentes). Nada é excluído: desativa.</summary>
    public static class GruposEmpresariais
    {
        public const string Grupo = Base + "/grupos-empresariais";
        public static string Listar(bool incluirInativos) => incluirInativos ? Grupo + "?incluirInativos=true" : Grupo;
        public static string PorId(Guid id) => $"{Grupo}/{id}";
        public static string Empresas(Guid id) => $"{Grupo}/{id}/empresas";
        public static string Desativar(Guid id) => $"{Grupo}/{id}/desativar";
        public static string Reativar(Guid id) => $"{Grupo}/{id}/reativar";
    }

    /// <summary>Arquivos anexados aos documentos das pessoas. Nada é excluído: remover desativa.</summary>
    public static class Anexos
    {
        public const string Grupo = Base + "/anexos";
        public static string Enviar(Guid pessoaId, Guid documentoId) => $"{Pessoas.Grupo}/{pessoaId}/documentos/{documentoId}/anexos";
        public static string Conteudo(Guid id) => $"{Grupo}/{id}/conteudo";
        public static string Desativar(Guid id) => $"{Grupo}/{id}/desativar";
        public static string Reativar(Guid id) => $"{Grupo}/{id}/reativar";
    }

    /// <summary>Estrutura organizacional (cargo, departamento, setor, centro de custo). Nada é excluído: desativa.</summary>
    public static class Estrutura
    {
        public const string Cargos = Base + "/cargos";
        public const string Departamentos = Base + "/departamentos";
        public const string Setores = Base + "/setores";
        public const string CentrosCusto = Base + "/centros-custo";
        public static string PorId(string grupo, Guid id) => $"{grupo}/{id}";
        public static string Desativar(string grupo, Guid id) => $"{grupo}/{id}/desativar";
        public static string Reativar(string grupo, Guid id) => $"{grupo}/{id}/reativar";
        public static string Listar(string grupo, bool incluirInativos) => incluirInativos ? grupo + "?incluirInativos=true" : grupo;
    }

    /// <summary>Cadastros comerciais (perfis, condições de pagamento, tipos de carteira). Nada é excluído: desativa.</summary>
    public static class Comercial
    {
        public const string Perfis = Base + "/perfis-comerciais";
        public const string Condicoes = Base + "/condicoes-pagamento";
        public const string TiposCarteira = Base + "/tipos-carteira";

        /// <summary>Perfis, condições, tipos de carteira, vendedores e empresas para a aba "Cliente".</summary>
        public const string Opcoes = Base + "/comercial/opcoes";

        // Motor Comercial, Fase 1c
        public const string TiposAusencia = Base + "/tipos-ausencia";
        public const string Parametros = Base + "/comercial/parametros";
        public const string Coberturas = Base + "/comercial/coberturas";
        public const string CoberturasOpcoes = Coberturas + "/opcoes";
        public static string CoberturaPorId(Guid id) => $"{Coberturas}/{id}";
        public static string CancelarCobertura(Guid id) => $"{Coberturas}/{id}/cancelar";
        public static string ListarCoberturas(bool incluirEncerradas) => $"{Coberturas}?incluirEncerradas={(incluirEncerradas ? "true" : "false")}";
        public const string CarteiraVencendo = Base + "/comercial/carteira-vencendo";

        // Motor Comercial, Fase 1d
        public const string Transferencias = Base + "/comercial/transferencias";
        public const string TransferenciasPrevia = Transferencias + "/previa";
        public static string TransferenciaPorId(Guid id) => $"{Transferencias}/{id}";
    }

    /// <summary>Tabela CNAE (IBGE): busca para a ficha e atualização pelo administrador.</summary>
    public static class Cnaes
    {
        public const string Grupo = Base + "/cnaes";
        public const string Situacao = Grupo + "/situacao";
        public const string Atualizar = Grupo + "/atualizar";
        public static string Buscar(string texto) => $"{Grupo}?texto={Uri.EscapeDataString(texto)}";
    }

    /// <summary>Motor de metas: equipes, indicadores, metas, realizado e apuração.</summary>
    public static class Metas
    {
        public const string Grupo = Base + "/metas";
        public const string Equipes = Base + "/equipes";
        public const string Indicadores = Base + "/indicadores";
        public const string Opcoes = Grupo + "/opcoes";
        public static string PorId(Guid id) => $"{Grupo}/{id}";
        public static string Situacao(Guid id) => $"{Grupo}/{id}/situacao";
        public static string Realizado(Guid id) => $"{Grupo}/{id}/realizado";
        public static string Importar(Guid id) => $"{Grupo}/{id}/realizado/importar";
        public static string Apuracao(Guid id) => $"{Grupo}/{id}/apuracao";
        public static string Desativar(Guid id) => $"{Grupo}/{id}/desativar";
    }

    public static class Colaboradores
    {
        public const string Grupo = Base + "/colaboradores";

        /// <summary>Empresas, gestores e cadastros da estrutura para a aba "Colaborador".</summary>
        public const string Opcoes = Grupo + "/opcoes";
    }

    /// <summary>Tipos de documento. Nada é excluído: desativa.</summary>
    public static class TiposDocumento
    {
        public const string Grupo = Base + "/tipos-documento";
        public static string PorId(Guid id) => $"{Grupo}/{id}";
        public static string Desativar(Guid id) => $"{Grupo}/{id}/desativar";
        public static string Reativar(Guid id) => $"{Grupo}/{id}/reativar";
        public static string Listar(bool incluirInativos) => incluirInativos ? Grupo + "?incluirInativos=true" : Grupo;
    }

    /// <summary>Finalidades de endereço (leitura para a ficha) e rotina que aponta endereços duplicados.</summary>
    public static class FinalidadesEndereco
    {
        public const string Grupo = Base + "/finalidades-endereco";
        public const string Duplicados = Base + "/pessoas/enderecos-duplicados";
        public static string ListarDuplicados(Guid? apos, int limite) =>
            $"{Duplicados}?limite={limite}" + (apos is { } a ? $"&apos={a}" : string.Empty);
    }

    /// <summary>Finalidades de tratamento (LGPD). Nada é excluído: desativa.</summary>
    public static class FinalidadesTratamento
    {
        public const string Grupo = Base + "/finalidades-tratamento";
        public static string PorId(Guid id) => $"{Grupo}/{id}";
        public static string Desativar(Guid id) => $"{Grupo}/{id}/desativar";
        public static string Reativar(Guid id) => $"{Grupo}/{id}/reativar";
        public static string Listar(bool incluirInativos) => incluirInativos ? Grupo + "?incluirInativos=true" : Grupo;
    }

    /// <summary>Tipos (classificações) de endereço. Nada é excluído: desativa.</summary>
    public static class TiposEndereco
    {
        public const string Grupo = Base + "/tipos-endereco";
        public static string PorId(Guid id) => $"{Grupo}/{id}";
        public static string Desativar(Guid id) => $"{Grupo}/{id}/desativar";
        public static string Reativar(Guid id) => $"{Grupo}/{id}/reativar";
        public static string Listar(bool incluirInativos) => incluirInativos ? Grupo + "?incluirInativos=true" : Grupo;
    }

    /// <summary>Tipos (classificações) de telefone e e-mail. Nada é excluído: desativa.</summary>
    public static class TiposMeioContato
    {
        public const string Grupo = Base + "/tipos-meio-contato";
        public static string PorId(Guid id) => $"{Grupo}/{id}";
        public static string Desativar(Guid id) => $"{Grupo}/{id}/desativar";
        public static string Reativar(Guid id) => $"{Grupo}/{id}/reativar";
        public static string Listar(bool incluirInativos) => incluirInativos ? Grupo + "?incluirInativos=true" : Grupo;
    }

    /// <summary>Cadastro de papéis. Nada é excluído: desativa (os de sistema com regra não podem ser desativados).</summary>
    public static class Papeis
    {
        public const string Grupo = Base + "/papeis";
        public static string PorId(Guid id) => $"{Grupo}/{id}";
        public static string Desativar(Guid id) => $"{Grupo}/{id}/desativar";
        public static string Reativar(Guid id) => $"{Grupo}/{id}/reativar";
        public static string Listar(bool incluirInativos) => incluirInativos ? Grupo + "?incluirInativos=true" : Grupo;
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

        public static string OrdemDe(Lone.Domain.Enums.EntidadePersonalizavel entidade) => $"{Ordem}?entidade={entidade}";

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

    /// <summary>Menu do usuário logado: favoritos e recentes (dados dele mesmo, sem permissão própria).</summary>
    public static class Menu
    {
        public const string Grupo = Base + "/menu";
        public const string Preferencias = Grupo + "/preferencias";
        public const string Favoritos = Grupo + "/favoritos";
        public const string Acessos = Grupo + "/acessos";

        /// <summary>Preferência de uma tela (ex.: colunas da lista de pessoas).</summary>
        public static string Tela(string tela) => $"{Grupo}/telas/{Uri.EscapeDataString(tela)}";
    }

    public static class Consultas
    {
        public const string Grupo = Base + "/consultas";
        public static string Cep(string cep) => $"{Grupo}/cep/{Uri.EscapeDataString(cep)}";
        public static string Cnpj(string cnpj) => $"{Grupo}/cnpj/{Uri.EscapeDataString(cnpj)}";
    }
}
