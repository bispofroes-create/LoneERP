namespace Lone.Contracts.Comercial;

/// <summary>Condição de pagamento (cadastro comercial).</summary>
public sealed class CondicaoPagamentoDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    /// <summary>Dias das parcelas separados por "/" (ex.: "0/30/60").</summary>
    public string Parcelas { get; set; } = "0";
    public decimal? AcrescimoPercentual { get; set; }

    /// <summary>Somente leitura: prazo médio em dias.</summary>
    public decimal PrazoMedio { get; set; }

    /// <summary>Somente leitura: onde está em uso (só para quem gerencia).</summary>
    public int QuantidadeUsos { get; set; }
}

/// <summary>Perfil comercial (cadastro comercial).</summary>
public sealed class PerfilComercialDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public decimal? LimiteCredito { get; set; }
    public decimal? DescontoMaximo { get; set; }
    public int? DiasMaximoAtraso { get; set; }
    public Guid? CondicaoPagamentoId { get; set; }
    public bool? ExigeAprovacaoAcimaLimite { get; set; }

    /// <summary>Somente leitura: onde está em uso (só para quem gerencia).</summary>
    public int QuantidadeUsos { get; set; }
}

/// <summary>Tipo de carteira (cadastro comercial).</summary>
public sealed class TipoCarteiraDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    /// <summary>O vendedor vigente deste papel vira o vendedor padrão da conta (só um papel; exige um por vez).</summary>
    public bool ResponsavelDaConta { get; set; }
    public int Ordem { get; set; }

    /// <summary>Vínculos simultâneos por cliente e empresa; nulo = sem limite; 1 = um por vez (substituição).</summary>
    public int? LimitePorVez { get; set; }
    public Lone.Domain.Enums.TipoCreditoComercial TipoCredito { get; set; }
    public decimal? PercentualPadrao { get; set; }
    public bool ContaParaMetas { get; set; }

    /// <summary>Quem pode ocupar o papel: Ids das classificações de pessoa (papéis do cadastro) aceitas.</summary>
    public List<Guid> Classificacoes { get; set; } = new();

    /// <summary>Somente leitura: onde está em uso (só para quem gerencia).</summary>
    public int QuantidadeUsos { get; set; }
}

/// <summary>Exceção comercial do cliente (vazio = não sobrescreve aquele campo).</summary>
public sealed class ExcecaoComercialDto
{
    public Guid Id { get; set; }
    public Guid? EmpresaId { get; set; }
    public DateOnly InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public decimal? LimiteCredito { get; set; }
    public decimal? DescontoMaximo { get; set; }
    public int? DiasMaximoAtraso { get; set; }
    public Guid? CondicaoPagamentoId { get; set; }
    public bool? ExigeAprovacaoAcimaLimite { get; set; }
    public string? Motivo { get; set; }
}

/// <summary>Vínculo da carteira de clientes (D5).</summary>
public sealed class CarteiraDto
{
    public Guid Id { get; set; }
    public Guid? EmpresaId { get; set; }
    public Guid TipoCarteiraId { get; set; }
    public Guid VendedorId { get; set; }

    /// <summary>Somente leitura: nome do vendedor.</summary>
    public string? Vendedor { get; set; }
    public DateOnly InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public bool Exclusivo { get; set; }

    /// <summary>Crédito da venda (%) deste vínculo; nulo = o padrão do papel.</summary>
    public decimal? PercentualCredito { get; set; }

    /// <summary>Somente leitura: como o vínculo foi criado (o servidor define).</summary>
    public Lone.Domain.Enums.OrigemVinculoCarteira Origem { get; set; }
    public string? Observacao { get; set; }
    public bool Ativo { get; set; } = true;
}

/// <summary>Opções da aba "Cliente" numa chamada só (lida quando a aba abre).</summary>
public sealed class ComercialOpcoesDto
{
    public List<PerfilComercialDto> Perfis { get; set; } = new();
    public List<CondicaoPagamentoDto> Condicoes { get; set; } = new();
    public List<TipoCarteiraDto> TiposCarteira { get; set; } = new();

    /// <summary>
    /// Pessoas ativas que podem ocupar algum papel comercial (com as classificações aceitas por algum deles), cada uma com
    /// as suas classificações ativas: a ficha mostra, para cada papel, só quem pode ocupá-lo.
    /// </summary>
    public List<AtendenteOpcaoDto> Atendentes { get; set; } = new();

    /// <summary>Classificações de pessoa (papéis do cadastro), com as desativadas: nomes e "Quem pode ser".</summary>
    public List<ClassificacaoOpcaoDto> Classificacoes { get; set; } = new();

    /// <summary>Com quantos dias de antecedência o fim de um vínculo da carteira é destacado na ficha.</summary>
    public int DiasAvisoFimVinculo { get; set; } = 30;

    /// <summary>Coberturas de ausência vigentes ou agendadas (aviso nos vínculos da carteira do titular).</summary>
    public List<CoberturaAvisoDto> Coberturas { get; set; } = new();
    public List<Lone.Contracts.Empresas.EmpresaResumo> Empresas { get; set; } = new();
}

/// <summary>Pessoa que pode ser escolhida na carteira, com as classificações ativas dela (entre as aceitas por algum papel).</summary>
public sealed record AtendenteOpcaoDto(Guid Id, string Nome, List<Guid> Classificacoes);

/// <summary>Classificação de pessoa (papel do cadastro: Vendedor, Funcionário...).</summary>
public sealed record ClassificacaoOpcaoDto(Guid Id, string Nome, bool Ativo);
