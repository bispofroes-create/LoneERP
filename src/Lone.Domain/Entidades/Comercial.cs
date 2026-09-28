using System.ComponentModel;
using Lone.Domain.Auditoria;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Condição de pagamento (ex.: "30/60/90"): parcelas em dias a partir da data da venda e acréscimo (ou desconto,
/// negativo) sobre o total. Nunca é excluída: desativada, some das escolhas novas mas continua onde já está.
/// </summary>
[DisplayName("Condição de pagamento")]
public class CondicaoPagamento : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 60;
    public const int MaximoParcelas = 48;
    public const int MaximoDias = 999;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Dias de cada parcela, em ordem, separados por "/" (ex.: "0/30/60"; "0" = à vista).</summary>
    [DisplayName("Parcelas (dias)")]
    public string Parcelas { get; set; } = "0";

    /// <summary>Acréscimo (positivo) ou desconto (negativo) em % sobre o total.</summary>
    [DisplayName("Acréscimo/desconto (%)")]
    public decimal? AcrescimoPercentual { get; set; }

    [DisplayName("Ativa")]
    public bool Ativo { get; set; } = true;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Condição de pagamento '{Nome}' desativada.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Condição de pagamento '{Nome}' reativada.");
    }
}

/// <summary>
/// Perfil comercial (ex.: "Varejo", "Atacado"): os padrões de venda de um grupo de clientes. Campo vazio = o perfil
/// não define (vale o da conta). Nunca é excluído: desativado.
/// </summary>
[DisplayName("Perfil comercial")]
public class PerfilComercial : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 60;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Limite de crédito")]
    public decimal? LimiteCredito { get; set; }

    [DisplayName("Desconto máximo (%)")]
    public decimal? DescontoMaximo { get; set; }

    [DisplayName("Dias máximos de atraso")]
    public int? DiasMaximoAtraso { get; set; }

    [DisplayName("Condição de pagamento")]
    public Guid? CondicaoPagamentoId { get; set; }

    [DisplayName("Exige aprovação acima do limite")]
    public bool? ExigeAprovacaoAcimaLimite { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Perfil comercial '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Perfil comercial '{Nome}' reativado.");
    }
}

/// <summary>
/// Papel comercial: a função de quem atende o cliente na carteira (Vendedor, Representante, Televendas, Supervisor...).
/// Cada papel tem a sua política (Motor Comercial, Fase 1): quantos vínculos ao mesmo tempo, como entra no crédito da
/// venda, o percentual padrão e se conta para as metas. O papel "responsável da conta" (só um) define o vendedor padrão
/// copiado para a conta do cliente. Nunca é excluído: desativado. Na tabela continua "TiposCarteira".
/// </summary>
[DisplayName("Papel comercial")]
public class TipoCarteira : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 40;

    /// <summary>Limite de vínculos simultâneos aceito no cadastro (acima disso é "sem limite").</summary>
    public const int MaximoPorVez = 99;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// O vendedor vigente deste papel é o "vendedor padrão" da conta do cliente (só um papel; exige no máximo 1 por vez).
    /// </summary>
    [DisplayName("Responsável da conta")]
    public bool ResponsavelDaConta { get; set; }

    /// <summary>
    /// Quantos vínculos ativos deste papel o cliente pode ter ao mesmo tempo (por empresa). Nulo = sem limite. 1 = um por
    /// vez: incluir outro pede a substituição (encerra o anterior na véspera). "Exclusivo" no vínculo continua valendo.
    /// </summary>
    [DisplayName("Quantos ao mesmo tempo")]
    public int? LimitePorVez { get; set; }

    [DisplayName("Crédito da venda")]
    public TipoCreditoComercial TipoCredito { get; set; }

    /// <summary>Percentual de crédito sugerido para os vínculos deste papel (o vínculo pode ter o seu).</summary>
    [DisplayName("Percentual padrão (%)")]
    public decimal? PercentualPadrao { get; set; }

    /// <summary>Os clientes dos vínculos deste papel contam no realizado das metas do vendedor.</summary>
    [DisplayName("Conta para metas")]
    public bool ContaParaMetas { get; set; }

    /// <summary>Um vínculo por vez (a regra de substituição vale para ele).</summary>
    public bool UmPorVez => LimitePorVez == 1;

    /// <summary>
    /// Quem pode ocupar este papel: as classificações de pessoa (papéis do cadastro: Vendedor, Representante,
    /// Funcionário...) aceitas. Nunca são apagadas: desmarcar desativa. Ao menos uma ativa.
    /// </summary>
    public List<TipoCarteiraClassificacao> Classificacoes { get; set; } = new();

    /// <summary>As classificações aceitas hoje (as ativas).</summary>
    public IEnumerable<Guid> ClassificacoesAceitas => Classificacoes.Where(c => c.Ativo).Select(c => c.PapelId);

    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Papel comercial '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Papel comercial '{Nome}' reativado.");
    }
}

/// <summary>
/// Classificação de pessoa (papel do cadastro, ex.: Funcionário) aceita num papel comercial (ex.: Supervisor): só quem tem
/// uma das classificações aceitas, ativa, pode ser escolhido para o papel na carteira do cliente. Nunca é apagada:
/// desmarcar desativa (o histórico fica).
/// </summary>
[DisplayName("Quem pode ser")]
public class TipoCarteiraClassificacao : EntidadeBase, IParteDeAgregado
{
    public Guid TipoCarteiraId { get; set; }

    [DisplayName("Classificação")]
    public Guid PapelId { get; set; }

    [DisplayName("Ativa")]
    public bool Ativo { get; set; } = true;

    string IParteDeAgregado.RaizEntidade => nameof(TipoCarteira);
    Guid IParteDeAgregado.RaizId => TipoCarteiraId;
}

/// <summary>
/// Exceção comercial de um cliente, com vigência: sobrescreve só os campos informados do perfil/conta
/// (ex.: desconto máximo de 15% durante a campanha de março). Nunca é apagada: encerra pelo fim.
/// </summary>
[DisplayName("Exceção comercial")]
public class ExcecaoComercial : EntidadePessoaFilha
{
    /// <summary>Empresa do grupo; nulo = todas (como a conta padrão).</summary>
    [DisplayName("Empresa")]
    public Guid? EmpresaId { get; set; }

    [DisplayName("Início")]
    public DateOnly InicioEm { get; set; }

    [DisplayName("Fim")]
    public DateOnly? FimEm { get; set; }

    [DisplayName("Limite de crédito")]
    public decimal? LimiteCredito { get; set; }

    [DisplayName("Desconto máximo (%)")]
    public decimal? DescontoMaximo { get; set; }

    [DisplayName("Dias máximos de atraso")]
    public int? DiasMaximoAtraso { get; set; }

    [DisplayName("Condição de pagamento")]
    public Guid? CondicaoPagamentoId { get; set; }

    [DisplayName("Exige aprovação acima do limite")]
    public bool? ExigeAprovacaoAcimaLimite { get; set; }

    [DisplayName("Motivo")]
    public string? Motivo { get; set; }

    public bool Vigente(DateOnly data) => InicioEm <= data && (FimEm is null || FimEm >= data);
}

/// <summary>
/// Vínculo da carteira de clientes (D5): quem atende o cliente (vendedor, representante...), com vigência.
/// Exclusivo = nenhum outro do mesmo tipo no mesmo período. Nunca é apagado: encerra pelo fim ou é desativado.
/// </summary>
[DisplayName("Carteira de clientes")]
public class CarteiraCliente : EntidadePessoaFilha
{
    /// <summary>Empresa do grupo; nulo = todas.</summary>
    [DisplayName("Empresa")]
    public Guid? EmpresaId { get; set; }

    [DisplayName("Tipo")]
    public Guid TipoCarteiraId { get; set; }

    /// <summary>Pessoa que atende o cliente (com o papel Vendedor ou Representante).</summary>
    [DisplayName("Vendedor")]
    public Guid VendedorId { get; set; }

    [DisplayName("Início")]
    public DateOnly InicioEm { get; set; }

    [DisplayName("Fim")]
    public DateOnly? FimEm { get; set; }

    [DisplayName("Exclusivo")]
    public bool Exclusivo { get; set; }

    /// <summary>
    /// Percentual do crédito da venda para este vínculo (papel com crédito de receita ou sobreposição). Nulo = o percentual
    /// padrão do papel. Com um só vínculo de receita vigente, ele fica com 100%.
    /// </summary>
    [DisplayName("Crédito (%)")]
    public decimal? PercentualCredito { get; set; }

    /// <summary>Como o vínculo foi criado (ficha, substituição, transferência...). Definido pelo servidor.</summary>
    [DisplayName("Origem")]
    public OrigemVinculoCarteira Origem { get; set; }

    /// <summary>A transferência de carteira que criou este vínculo (Origem = Transferência). Definido pelo servidor.</summary>
    [DisplayName("Transferência")]
    public Guid? TransferenciaId { get; set; }

    [DisplayName("Observação")]
    public string? Observacao { get; set; }

    /// <summary>Falso = lançado por engano (fica no histórico, não vale para nada).</summary>
    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public bool Vigente(DateOnly data) => Ativo && InicioEm <= data && (FimEm is null || FimEm >= data);
}
