using System.ComponentModel;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Vínculo de trabalho da pessoa com uma empresa do grupo (matrícula, admissão, desligamento). Uma pessoa pode ter
/// vários: em empresas diferentes ao mesmo tempo, ou na mesma empresa em épocas diferentes (recontratação = vínculo
/// novo). Nunca é apagado: o desligamento encerra.
/// </summary>
[DisplayName("Vínculo de colaborador")]
public class VinculoColaborador : EntidadePessoaFilha
{
    public const int TamanhoMaximoMatricula = 20;
    public const int TamanhoMaximoTexto = 250;

    /// <summary>Empresa do grupo (pessoa com o papel "Empresa do grupo").</summary>
    [DisplayName("Empresa")]
    public Guid EmpresaId { get; set; }

    [DisplayName("Matrícula")]
    public string? Matricula { get; set; }

    [DisplayName("Tipo de vínculo")]
    public TipoVinculo Tipo { get; set; } = TipoVinculo.Clt;

    [DisplayName("Admissão")]
    public DateOnly AdmissaoEm { get; set; }

    [DisplayName("Desligamento")]
    public DateOnly? DesligamentoEm { get; set; }

    [DisplayName("Motivo do desligamento")]
    public string? MotivoDesligamento { get; set; }

    /// <summary>Jornada contratual em horas por semana (ex.: 44).</summary>
    [DisplayName("Jornada semanal (h)")]
    public decimal? JornadaSemanal { get; set; }

    [DisplayName("Observações")]
    public string? Observacoes { get; set; }

    public bool Ativo(DateOnly hoje) => AdmissaoEm <= hoje && (DesligamentoEm is null || DesligamentoEm >= hoje);
}

/// <summary>
/// Lotação do colaborador num período (início/fim): cargo, departamento, setor, centro de custo e gestor juntos.
/// Cada mudança encerra o período aberto e começa outro, então "quem era o gestor em março?" tem resposta direta
/// (é o que as metas e as comissões usam). Nunca é apagada.
/// </summary>
[DisplayName("Lotação")]
public class LotacaoColaborador : EntidadePessoaFilha
{
    [DisplayName("Vínculo")]
    public Guid VinculoId { get; set; }

    [DisplayName("Início")]
    public DateOnly InicioEm { get; set; }

    /// <summary>Nulo = período em aberto (a lotação atual).</summary>
    [DisplayName("Fim")]
    public DateOnly? FimEm { get; set; }

    [DisplayName("Cargo")]
    public Guid? CargoId { get; set; }

    [DisplayName("Departamento")]
    public Guid? DepartamentoId { get; set; }

    [DisplayName("Setor")]
    public Guid? SetorId { get; set; }

    [DisplayName("Centro de custo")]
    public Guid? CentroCustoId { get; set; }

    /// <summary>Gestor imediato (outra pessoa, normalmente também colaboradora).</summary>
    [DisplayName("Gestor")]
    public Guid? GestorId { get; set; }

    public bool Vigente(DateOnly data) => InicioEm <= data && (FimEm is null || FimEm >= data);
}
