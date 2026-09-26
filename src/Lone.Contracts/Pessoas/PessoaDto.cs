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

    /// <summary>Grupo empresarial (só pessoa jurídica; opcional). Mudar exige a permissão de estrutura empresarial.</summary>
    public Guid? GrupoEmpresarialId { get; set; }

    /// <summary>Somente leitura: nome do grupo empresarial (cabeçalho da ficha).</summary>
    public string? GrupoEmpresarialNome { get; set; }
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
    /// <summary>Etiquetas marcadas (Ids do cadastro de etiquetas).</summary>
    public List<Guid> EtiquetaIds { get; set; } = new();

    public List<EstabelecimentoDto> Estabelecimentos { get; set; } = new();
    public List<EnderecoDto> Enderecos { get; set; } = new();
    public List<MeioContatoDto> MeiosContato { get; set; } = new();
    public List<ContatoDto> Contatos { get; set; } = new();
    public List<DocumentoDto> Documentos { get; set; } = new();

    /// <summary>Somente leitura: relacionamento (última interação, situação e as interações mais recentes).</summary>
    public RelacionamentoDto? Relacionamento { get; set; }

    /// <summary>
    /// Somente leitura: a migração deixou finalidades de endereço para definir à mão (a ficha mostra o motivo por
    /// finalidade). A API desliga quando todas as pendências forem resolvidas.
    /// </summary>
    public bool RevisarFinalidadesEndereco { get; set; }

    /// <summary>Dados de colaborador (vínculos e lotações). Vazio e <see cref="ColaboradorOculto"/> = sem permissão para vê-los.</summary>
    public List<Lone.Contracts.Colaboradores.VinculoDto> Vinculos { get; set; } = new();

    /// <summary>Somente leitura: o usuário não tem a permissão de colaborador; a API mantém os dados gravados.</summary>
    public bool ColaboradorOculto { get; set; }

    /// <summary>Só no envio: motivo da alteração (opcional), gravado na auditoria desta gravação. Não é lido de volta.</summary>
    public string? MotivoAlteracao { get; set; }
    public List<PapelDto> Papeis { get; set; } = new();
    public List<ContaClienteDto> ContasCliente { get; set; } = new();

    /// <summary>Exceções comerciais do cliente, com vigência (a mais recente primeiro).</summary>
    public List<Lone.Contracts.Comercial.ExcecaoComercialDto> ExcecoesComerciais { get; set; } = new();

    /// <summary>Carteira de clientes (D5): quem atende o cliente, com vigência.</summary>
    public List<Lone.Contracts.Comercial.CarteiraDto> Carteira { get; set; } = new();
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
    public bool ProdutorRural { get; set; }
    public string? CnaePrincipal { get; set; }

    /// <summary>Somente leitura: "0111-3/01 · Cultivo de arroz" (tabela CNAE do IBGE).</summary>
    public string? CnaePrincipalDescricao { get; set; }

    /// <summary>Somente leitura: situação fiscal por período (mantida pela API; a mais recente primeiro).</summary>
    public List<HistoricoFiscalDto> HistoricoFiscal { get; set; } = new();
    public string? NaturezaJuridica { get; set; }

    /// <summary>Códigos dos CNAEs secundários separados por vírgula.</summary>
    public string? CnaesSecundarios { get; set; }

    /// <summary>Id de um endereço desta mesma pessoa. Nulo = endereço fiscal/principal da pessoa.</summary>
    public Guid? EnderecoFiscalId { get; set; }
}

/// <summary>Período da situação fiscal do estabelecimento (somente leitura).</summary>
public sealed class HistoricoFiscalDto
{
    public DateOnly InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public RegimeTributario RegimeTributario { get; set; }
    public IndicadorIE IndicadorIE { get; set; }
    public string? InscricaoEstadual { get; set; }
    public string? SituacaoReceita { get; set; }
    public bool ProdutorRural { get; set; }
}

public sealed class FinalidadeDoEnderecoDto
{
    public Guid Id { get; set; }
    public Guid FinalidadeId { get; set; }
    public bool Principal { get; set; }
    public bool Ativo { get; set; } = true;
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

    /// <summary>
    /// Cópia em bits das finalidades ativas (compatibilidade; a fonte é <see cref="Usos"/>). Cliente antigo que manda só
    /// os bits (Usos vazio): a API traduz para as relações preservando o principal já gravado — nunca o apaga — e sem
    /// criar principal novo.
    /// </summary>
    public FinalidadeEndereco Finalidades { get; set; } = FinalidadeEndereco.Nenhuma;

    /// <summary>Finalidades do endereço, com o principal de cada uma (explícito). Retirada = Ativo falso (histórico).</summary>
    public List<FinalidadeDoEnderecoDto> Usos { get; set; } = new();

    /// <summary>
    /// Somente leitura: duplicado consolidado neste outro endereço. A ficha não altera (a API mantém o gravado);
    /// consolidar é a operação própria POST pessoas/{id}/enderecos/consolidar.
    /// </summary>
    public Guid? MescladoEmId { get; set; }

    /// <summary>
    /// Motivos de revisão deixados pela migração. O aplicativo só pode desligar (usuário marcou como revisado); a API
    /// ignora qualquer motivo novo.
    /// </summary>
    public MotivoRevisaoEndereco RevisaoMigracao { get; set; } = MotivoRevisaoEndereco.Nenhum;
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

    /// <summary>Tipo do cadastro de tipos de documento. Vazio = chamada antiga: vale o <see cref="Tipo"/>.</summary>
    public Guid TipoDocumentoId { get; set; }

    /// <summary>Cópia do tipo de sistema (a API sobrescreve com a do cadastro).</summary>
    public TipoDocumento Tipo { get; set; } = TipoDocumento.Rg;

    /// <summary>Falso = removido na ficha (fica gravado, fora da lista principal).</summary>
    public bool Ativo { get; set; } = true;
    public string Numero { get; set; } = string.Empty;
    public string? OrgaoEmissor { get; set; }
    public string? Uf { get; set; }
    public DateOnly? EmitidoEm { get; set; }
    public DateOnly? ValidoAte { get; set; }
    public string? Observacoes { get; set; }

    /// <summary>Valores dos campos personalizados do tipo deste documento.</summary>
    public List<Lone.Contracts.CamposPersonalizados.ValorPersonalizadoDto> ValoresPersonalizados { get; set; } = new();

    /// <summary>Somente leitura: arquivos anexados (enviados e removidos à parte, pelas rotas de anexos).</summary>
    public List<Lone.Contracts.Documentos.AnexoDto> Anexos { get; set; } = new();
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
    public Guid? PerfilComercialId { get; set; }
    public Guid? CondicaoPagamentoId { get; set; }
    public string? Observacoes { get; set; }
}

public sealed class ContaFornecedorDto
{
    public Guid Id { get; set; }
    public Guid? EmpresaId { get; set; }
    public string? CondicaoPagamento { get; set; }

    /// <summary>Condição de pagamento do cadastro (fonte principal). O texto acima é o anterior, preservado.</summary>
    public Guid? CondicaoPagamentoId { get; set; }
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
