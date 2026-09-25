using System.ComponentModel;
using Lone.Domain.Auditoria;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Identidade central do ERP (Business Partner): existe uma vez só, e os papéis dizem o que ela é
/// para a empresa. Pessoa jurídica = a empresa (raiz do CNPJ); cada CNPJ completo é um Estabelecimento.
/// Toda pessoa tem ao menos um estabelecimento (na PF ele fica oculto e guarda os dados fiscais).
/// </summary>
[DisplayName("Cadastro")]
public class Pessoa : AgregadoRaiz
{
    /// <summary>Código de exibição (000123), gerado pelo banco. Não é a chave.</summary>
    [DisplayName("Código")]
    public int Codigo { get; set; }

    [DisplayName("Natureza")]
    public NaturezaPessoa Natureza { get; set; } = NaturezaPessoa.Fisica;

    [DisplayName("Situação")]
    public SituacaoPessoa Situacao { get; set; } = SituacaoPessoa.Ativo;

    /// <summary>Motivo informado na última desativação (ou reativação).</summary>
    [DisplayName("Motivo da situação")]
    public string? SituacaoMotivo { get; set; }

    /// <summary>Quando a situação mudou pela última vez (UTC).</summary>
    [DisplayName("Situação alterada em")]
    public DateTime? SituacaoAlteradaEm { get; set; }

    /// <summary>Nome civil completo (PF), razão social (PJ) ou nome (estrangeiro). É o que vai nos documentos.</summary>
    [DisplayName("Nome / razão social")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Nome social (PF), quando a pessoa informar.</summary>
    [DisplayName("Nome social")]
    public string? NomeSocial { get; set; }

    /// <summary>Nome curto usado nas telas.</summary>
    [DisplayName("Nome de exibição")]
    public string? NomeExibicao { get; set; }

    [DisplayName("Apelido")]
    public string? Apelido { get; set; }

    /// <summary>CPF (PF), raiz do CNPJ com 8 posições (PJ) ou identificação do estrangeiro. Único por natureza.</summary>
    [DisplayName("Documento"), DadoSensivel]
    public string? DocumentoPrincipal { get; set; }

    [DisplayName("Data de nascimento"), DadoSensivel]
    public DateOnly? DataNascimento { get; set; }

    // ---- Dados pessoais (pessoa física) ----

    [DisplayName("Sexo")]
    public SexoRegistro Sexo { get; set; }

    /// <summary>O histórico registra que mudou, sem o valor.</summary>
    [DisplayName("Identidade de gênero"), NaoAuditarValor]
    public IdentidadeGenero IdentidadeGenero { get; set; }

    /// <summary>
    /// Dado sensível (LGPD, art. 11): só para funcionários (eSocial) e só visível com permissão. O histórico
    /// registra que mudou, nunca o valor (mascarar o texto não bastaria: o final da palavra já identifica a cor).
    /// </summary>
    [DisplayName("Cor/raça"), NaoAuditarValor]
    public CorRaca CorRaca { get; set; }

    [DisplayName("Estado civil")]
    public EstadoCivil EstadoCivil { get; set; }

    [DisplayName("Escolaridade")]
    public Escolaridade Escolaridade { get; set; }

    [DisplayName("Nacionalidade")]
    public string? Nacionalidade { get; set; }

    /// <summary>Município de nascimento (código IBGE, na tabela Municipios). A UF vem do município.</summary>
    [DisplayName("Naturalidade")]
    public int? NaturalidadeMunicipioId { get; set; }

    [DisplayName("Nome da mãe")]
    public string? NomeMae { get; set; }

    [DisplayName("Nome do pai")]
    public string? NomePai { get; set; }

    /// <summary>Profissão do cadastro de profissões. O texto livre de antes fica no banco só como cópia (coluna Profissao).</summary>
    [DisplayName("Profissão")]
    public Guid? ProfissaoId { get; set; }

    // ---- Dados da empresa (pessoa jurídica, vindos da Receita) ----

    [DisplayName("Data de abertura")]
    public DateOnly? DataAbertura { get; set; }

    /// <summary>Porte na Receita (ex.: "Micro empresa", "Empresa de pequeno porte", "Demais").</summary>
    [DisplayName("Porte")]
    public string? Porte { get; set; }

    [DisplayName("Capital social")]
    public decimal? CapitalSocial { get; set; }

    // ---- Relacionamento comercial ----

    /// <summary>Como a pessoa chegou até a empresa (ex.: "Indicação", "Site").</summary>
    [DisplayName("Origem do cadastro")]
    public string? OrigemCadastro { get; set; }

    [DisplayName("Primeiro contato")]
    public DateOnly? PrimeiroContatoEm { get; set; }

    [DisplayName("Grupo econômico")]
    public Guid? GrupoEconomicoId { get; set; }

    /// <summary>Quando um cadastro duplicado é arquivado, aponta para o que ficou valendo.</summary>
    [DisplayName("Mesclado em")]
    public Guid? MescladaEmId { get; set; }

    [DisplayName("Observações")]
    public string? Observacoes { get; set; }

    public List<Estabelecimento> Estabelecimentos { get; set; } = new();
    public List<PessoaDocumento> Documentos { get; set; } = new();
    public List<PessoaEndereco> Enderecos { get; set; } = new();
    public List<MeioContato> MeiosContato { get; set; } = new();
    public List<Contato> Contatos { get; set; } = new();
    public List<PessoaPapel> Papeis { get; set; } = new();
    public List<ContaCliente> ContasCliente { get; set; } = new();
    public List<ContaFornecedor> ContasFornecedor { get; set; } = new();

    public List<PessoaSocio> Socios { get; set; } = new();
    public List<PessoaConsentimento> Consentimentos { get; set; } = new();
    public List<PessoaEtiqueta> Etiquetas { get; set; } = new();

    /// <summary>Informações adicionais: valores dos campos personalizados criados pelo administrador.</summary>
    public List<PessoaValorPersonalizado> ValoresPersonalizados { get; set; } = new();

    /// <summary>Valores dos campos personalizados dos documentos (cada um com o Id do documento).</summary>
    public List<DocumentoValorPersonalizado> ValoresDocumentos { get; set; } = new();

    /// <summary>Situação fiscal de cada estabelecimento por período (mantida pela API). Nunca apagada.</summary>
    public List<HistoricoFiscal> HistoricoFiscal { get; set; } = new();

    /// <summary>CNAEs dos estabelecimentos em tabela (cópia dos campos de texto, mantida pela API).</summary>
    public List<EstabelecimentoCnae> Cnaes { get; set; } = new();

    /// <summary>Exceções comerciais do cliente, com vigência. Nunca apagadas.</summary>
    public List<ExcecaoComercial> ExcecoesComerciais { get; set; } = new();

    /// <summary>Carteira de clientes (D5): quem atende este cliente, com vigência. Nunca apagada.</summary>
    public List<CarteiraCliente> Carteira { get; set; } = new();

    /// <summary>Vínculos de trabalho com as empresas do grupo (dados do colaborador). Nunca apagados.</summary>
    public List<VinculoColaborador> Vinculos { get; set; } = new();

    /// <summary>Lotações (cargo, departamento, setor, centro de custo, gestor) com vigência, de cada vínculo. Nunca apagadas.</summary>
    public List<LotacaoColaborador> Lotacoes { get; set; } = new();

    /// <summary>Gravados só pelas operações de bloquear/liberar (não pelo formulário de cadastro).</summary>
    public List<Bloqueio> Bloqueios { get; set; } = new();

    /// <summary>Vínculos em que esta pessoa é a origem (ex.: "Sócio de" outra pessoa).</summary>
    public List<PessoaRelacionamento> Relacionamentos { get; set; } = new();

    public string NomeParaExibir() =>
        !string.IsNullOrWhiteSpace(NomeExibicao) ? NomeExibicao
        : !string.IsNullOrWhiteSpace(NomeSocial) ? NomeSocial
        : Nome;

    public const int TamanhoMaximoMotivo = 200;

    /// <summary>Ativo ou em análise: aparece nas operações do dia a dia.</summary>
    public bool EmUso => Situacao is SituacaoPessoa.Ativo or SituacaoPessoa.EmAnalise;

    /// <summary>
    /// Tira o cadastro de uso sem apagar nada: some das buscas normais (continua no filtro "Inativos"), mantém
    /// o histórico e pode ser reativado. Registra o evento para a auditoria.
    /// </summary>
    public void Desativar(string? motivo, DateTime agoraUtc)
    {
        if (Situacao == SituacaoPessoa.Arquivado)
            throw new Validacao.ValidacaoException(["Cadastro arquivado é somente leitura."]);
        if (Situacao == SituacaoPessoa.Inativo)
            throw new Validacao.ValidacaoException(["Este cadastro já está inativo."]);

        Situacao = SituacaoPessoa.Inativo;
        SituacaoMotivo = MotivoValido(motivo);
        SituacaoAlteradaEm = agoraUtc;
        RegistrarEvento($"{Descricao()} foi desativado." + (SituacaoMotivo is null ? string.Empty : $" Motivo: {SituacaoMotivo}"));
    }

    /// <summary>Volta a usar um cadastro inativo.</summary>
    public void Reativar(string? motivo, DateTime agoraUtc)
    {
        if (Situacao != SituacaoPessoa.Inativo)
            throw new Validacao.ValidacaoException(["Só um cadastro inativo pode ser reativado."]);

        Situacao = SituacaoPessoa.Ativo;
        SituacaoMotivo = MotivoValido(motivo);
        SituacaoAlteradaEm = agoraUtc;
        RegistrarEvento($"{Descricao()} foi reativado." + (SituacaoMotivo is null ? string.Empty : $" Motivo: {SituacaoMotivo}"));
    }

    /// <summary>"Cliente João da Silva", "Fornecedor Acme Ltda", ou "Cadastro Fulano" sem papel ativo.</summary>
    public string Descricao()
    {
        var papel = Papeis.Where(p => p.Ativo).OrderBy(p => p.Papel).Select(p => (TipoPapel?)p.Papel).FirstOrDefault();
        var tipo = papel switch
        {
            TipoPapel.Cliente => "Cliente",
            TipoPapel.Fornecedor => "Fornecedor",
            TipoPapel.Funcionario => "Funcionário",
            TipoPapel.Vendedor => "Vendedor",
            TipoPapel.Transportadora => "Transportadora",
            _ => "Cadastro"
        };
        return $"{tipo} {Nome}";
    }

    private static string? MotivoValido(string? motivo)
    {
        var texto = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        if (texto is { Length: > TamanhoMaximoMotivo })
            throw new Validacao.ValidacaoException([$"O motivo pode ter no máximo {TamanhoMaximoMotivo} caracteres."]);
        return texto;
    }

    public bool TemPapel(TipoPapel papel) => Papeis.Any(p => p.Papel == papel && p.Ativo);

    public Estabelecimento? EstabelecimentoPrincipal() =>
        Estabelecimentos.FirstOrDefault(e => e.Principal) ?? Estabelecimentos.FirstOrDefault();
}
