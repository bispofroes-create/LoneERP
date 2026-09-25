using Lone.Contracts.CamposPersonalizados;
using Lone.Domain.Enums;

namespace Lone.Contracts.Pessoas;

/// <summary>
/// Cadastro completo de uma pessoa, como trafega entre o aplicativo e a API.
/// Os Ids de pessoa e filhos podem ser gerados no aparelho (IdSequencial.Novo()); Guid.Empty = a API gera.
/// Versao é a que o usuário abriu: se outro usuário gravar antes, a API responde conflito (409).
/// </summary>
public sealed class PessoaDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }

    /// <summary>Código de exibição (000123), dado pela API na inclusão. Zero = ainda não gravada.</summary>
    public int Codigo { get; set; }

    public NaturezaPessoa Natureza { get; set; } = NaturezaPessoa.Fisica;
    public SituacaoPessoa Situacao { get; set; } = SituacaoPessoa.Ativo;

    /// <summary>Somente leitura: motivo e data da última desativação/reativação (mudam só pelas ações próprias).</summary>
    public string? SituacaoMotivo { get; set; }
    public DateTime? SituacaoAlteradaEm { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string? NomeSocial { get; set; }
    public string? NomeExibicao { get; set; }
    public string? Apelido { get; set; }

    /// <summary>CPF (PF) ou identificação do estrangeiro. Na PJ a API deriva a raiz do CNPJ principal.</summary>
    public string? DocumentoPrincipal { get; set; }

    public DateOnly? DataNascimento { get; set; }
    public Guid? GrupoEconomicoId { get; set; }
    public Guid? MescladaEmId { get; set; }
    public string? Observacoes { get; set; }

    // ---- Dados pessoais (PF) ----
    public SexoRegistro Sexo { get; set; }
    public IdentidadeGenero IdentidadeGenero { get; set; }

    /// <summary>Dado sensível. Vem vazio para quem não tem a permissão de ver dados sensíveis (ver CorRacaOculta).</summary>
    public CorRaca CorRaca { get; set; }

    /// <summary>Verdadeiro quando a API escondeu a cor/raça por falta de permissão (a gravação mantém a atual).</summary>
    public bool CorRacaOculta { get; set; }

    public EstadoCivil EstadoCivil { get; set; }
    public Escolaridade Escolaridade { get; set; }
    public string? Nacionalidade { get; set; }

    /// <summary>Município de nascimento (código IBGE). Nome e UF abaixo são só para exibir (a API preenche).</summary>
    public int? NaturalidadeMunicipioId { get; set; }
    public string? NaturalidadeNome { get; set; }
    public string? NaturalidadeUf { get; set; }
    public string? NomeMae { get; set; }
    public string? NomePai { get; set; }
    /// <summary>Profissão do cadastro de profissões (nula = não informada).</summary>
    public Guid? ProfissaoId { get; set; }

    // ---- Dados da empresa (PJ) ----
    public DateOnly? DataAbertura { get; set; }
    public string? Porte { get; set; }
    public decimal? CapitalSocial { get; set; }
    public List<SocioDto> Socios { get; set; } = new();

    // ---- Relacionamento ----
    public string? OrigemCadastro { get; set; }
    public DateOnly? PrimeiroContatoEm { get; set; }
    public List<ConsentimentoDto> Consentimentos { get; set; } = new();
    /// <summary>Etiquetas marcadas (Ids do cadastro de etiquetas).</summary>
    public List<Guid> EtiquetaIds { get; set; } = new();

    public List<EstabelecimentoDto> Estabelecimentos { get; set; } = new();
    public List<EnderecoDto> Enderecos { get; set; } = new();
    public List<MeioContatoDto> MeiosContato { get; set; } = new();
    public List<ContatoDto> Contatos { get; set; } = new();
    public List<DocumentoDto> Documentos { get; set; } = new();
    public List<PapelDto> Papeis { get; set; } = new();
    public List<ContaClienteDto> ContasCliente { get; set; } = new();
    public List<ContaFornecedorDto> ContasFornecedor { get; set; } = new();

    /// <summary>Informações adicionais (campos personalizados). Campos sem valor não vêm na lista.</summary>
    public List<ValorPersonalizadoDto> ValoresPersonalizados { get; set; } = new();

    /// <summary>Somente leitura: textos de município antigos ainda sem município escolhido (para corrigir).</summary>
    public List<PendenciaMunicipioDto> PendenciasMunicipio { get; set; } = new();

    /// <summary>Somente leitura: bloqueios têm operações próprias (etapa 7) e são ignorados ao salvar.</summary>
    public List<BloqueioDto> Bloqueios { get; set; } = new();

    public bool TemPapel(TipoPapel papel) => Papeis.Any(p => p.Papel == papel && p.Ativo);
}

public sealed class EstabelecimentoDto
{
    public Guid Id { get; set; }
    public string? Cnpj { get; set; }
    public bool Principal { get; set; }
    public string? NomeFantasia { get; set; }
    public bool Ativo { get; set; } = true;
    public string? SituacaoReceita { get; set; }
    public DateTime? ConsultadoReceitaEm { get; set; }
    public IndicadorIE IndicadorIE { get; set; } = IndicadorIE.NaoInformado;
    public string? InscricaoEstadual { get; set; }
    public string? InscricaoMunicipal { get; set; }
    public string? InscricaoSuframa { get; set; }
    public RegimeTributario RegimeTributario { get; set; } = RegimeTributario.NaoInformado;
    public string? CnaePrincipal { get; set; }
    public string? NaturezaJuridica { get; set; }

    /// <summary>Códigos dos CNAEs secundários separados por vírgula.</summary>
    public string? CnaesSecundarios { get; set; }

    /// <summary>Id de um endereço desta mesma pessoa. Nulo = endereço fiscal/principal da pessoa.</summary>
    public Guid? EnderecoFiscalId { get; set; }
}

public sealed class EnderecoDto
{
    public Guid Id { get; set; }
    public string? Descricao { get; set; }

    /// <summary>Classificação do cadastro de tipos de endereço (Sede, Depósito...); nula = sem tipo.</summary>
    public Guid? TipoEnderecoId { get; set; }
    public string? Observacoes { get; set; }

    /// <summary>Falso = removido na ficha (fica gravado, fora da lista principal).</summary>
    public bool Ativo { get; set; } = true;
    public FinalidadeEndereco Finalidades { get; set; } = FinalidadeEndereco.Comercial;
    public int Ordem { get; set; }
    public string? Cep { get; set; }
    public string Logradouro { get; set; } = string.Empty;
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }

    /// <summary>Município (código IBGE), obrigatório no Brasil. Cidade, UF e código são cópias feitas pela API.</summary>
    public int? MunicipioId { get; set; }

    /// <summary>No Brasil, nome do município (a API sobrescreve com o do IBGE); no exterior, a cidade digitada.</summary>
    public string Cidade { get; set; } = string.Empty;
    public string? Uf { get; set; }
    public string? CodigoMunicipioIbge { get; set; }
    public string CodigoPais { get; set; } = "1058";
    public string Pais { get; set; } = "Brasil";
}

public sealed class MeioContatoDto
{
    public Guid Id { get; set; }
    public TipoContato Tipo { get; set; } = TipoContato.Celular;
    public string Valor { get; set; } = string.Empty;

    /// <summary>Classificação do cadastro de tipos (Comercial, Residencial...); nula = sem classificação.</summary>
    public Guid? TipoMeioContatoId { get; set; }
    public string? Ramal { get; set; }
    public bool WhatsApp { get; set; }
    public bool Sms { get; set; }
    public FinalidadeEmail Finalidades { get; set; }

    /// <summary>Observação.</summary>
    public string? Descricao { get; set; }
    public bool Principal { get; set; }
    public bool PermiteComunicacao { get; set; } = true;

    /// <summary>Falso = removido na ficha (fica gravado, fora da lista principal).</summary>
    public bool Ativo { get; set; } = true;
}

public sealed class ContatoDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Cargo { get; set; }
    public string? Departamento { get; set; }
    public string? Telefone { get; set; }
    public string? Celular { get; set; }
    public bool CelularWhatsApp { get; set; }
    public string? Email { get; set; }
    public string? Observacoes { get; set; }
    public bool Principal { get; set; }
    public Guid? PessoaVinculadaId { get; set; }
}

public sealed class DocumentoDto
{
    public Guid Id { get; set; }
    public TipoDocumento Tipo { get; set; } = TipoDocumento.Rg;
    public string Numero { get; set; } = string.Empty;
    public string? OrgaoEmissor { get; set; }
    public string? Uf { get; set; }
    public DateOnly? EmitidoEm { get; set; }
    public DateOnly? ValidoAte { get; set; }
    public string? Observacoes { get; set; }
}

public sealed class PapelDto
{
    public Guid Id { get; set; }

    /// <summary>O papel do cadastro de papéis.</summary>
    public Guid PapelId { get; set; }

    /// <summary>Somente leitura: o papel de sistema (enum) do cadastro; nulo nos papéis criados pelo usuário.</summary>
    public TipoPapel? Papel { get; set; }
    public bool Ativo { get; set; } = true;
    public DateOnly InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public string? Observacoes { get; set; }
}

/// <summary>Conta de cliente numa empresa do grupo (EmpresaId nulo = conta padrão).</summary>
public sealed class ContaClienteDto
{
    public Guid Id { get; set; }
    public Guid? EmpresaId { get; set; }
    public decimal? LimiteCredito { get; set; }
    public int? DiasMaximoAtraso { get; set; }
    public decimal? DescontoMaximo { get; set; }
    public string? CondicaoPagamento { get; set; }
    public bool ExigeAprovacaoAcimaLimite { get; set; } = true;
    public Guid? VendedorPadraoId { get; set; }
    public string? Observacoes { get; set; }
}

public sealed class ContaFornecedorDto
{
    public Guid Id { get; set; }
    public Guid? EmpresaId { get; set; }
    public string? CondicaoPagamento { get; set; }
    public int? PrazoMedioDias { get; set; }
    public int? LeadTimeDias { get; set; }
    public Guid? TransportadoraPadraoId { get; set; }
    public byte? Avaliacao { get; set; }
    public string? Observacoes { get; set; }
}

public sealed class BloqueioDto
{
    public Guid Id { get; set; }
    public Guid? EmpresaId { get; set; }
    public EscopoBloqueio Escopo { get; set; }
    public OrigemBloqueio Origem { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTime InicioEm { get; set; }
    public string InicioPor { get; set; } = string.Empty;
    public DateTime? FimEm { get; set; }
    public string? FimPor { get; set; }
    public string? MotivoLiberacao { get; set; }

    public bool Ativo => FimEm is null;
}

/// <summary>Sócio ou administrador que consta na Receita Federal.</summary>
public sealed class SocioDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Qualificacao { get; set; }
    public string? Documento { get; set; }
    public DateOnly? EntradaEm { get; set; }
}

/// <summary>Autorização para comunicações por um canal (LGPD). As datas são gravadas pela API.</summary>
public sealed class ConsentimentoDto
{
    public Guid Id { get; set; }
    public CanalComunicacao Canal { get; set; }
    public bool Concedido { get; set; }
    public DateTime? ConcedidoEm { get; set; }
    public DateTime? RevogadoEm { get; set; }
    public string? Origem { get; set; }
}

/// <summary>Texto de município gravado antes da tabela do IBGE que a conciliação não conseguiu ligar.</summary>
public sealed class PendenciaMunicipioDto
{
    /// <summary>Naturalidade (RegistroId = Id da pessoa) ou Endereço (RegistroId = Id do endereço).</summary>
    public bool DaNaturalidade { get; set; }
    public Guid RegistroId { get; set; }
    public string TextoOriginal { get; set; } = string.Empty;
    public string? UfOriginal { get; set; }
    public string? Observacao { get; set; }

    public string Texto => (UfOriginal is null ? TextoOriginal : $"{TextoOriginal} ({UfOriginal})") +
                           (Observacao is null ? string.Empty : $" — {Observacao}");
}

/// <summary>Quantas pessoas físicas há em cada faixa de idade.</summary>
public sealed record QuantidadePorFaixaEtaria(string Faixa, int Quantidade);
