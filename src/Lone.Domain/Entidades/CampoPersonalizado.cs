using System.ComponentModel;
using Lone.Domain.Auditoria;
using Lone.Domain.CamposPersonalizados;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Definição de um campo criado pelo administrador (ex.: "Quantidade de filhos", inteiro). Nunca é apagado:
/// é desativado, para não perder os valores já gravados nem o histórico. O tipo não muda depois de haver valores.
/// </summary>
[DisplayName("Campo personalizado")]
public class CampoPersonalizado : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 60;
    public const int TamanhoMaximoDica = 150;

    [DisplayName("Cadastro")]
    public EntidadePersonalizavel Entidade { get; set; } = EntidadePersonalizavel.Pessoa;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Tipo")]
    public TipoCampoPersonalizado Tipo { get; set; } = TipoCampoPersonalizado.Texto;

    [DisplayName("Obrigatório")]
    public bool Obrigatorio { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    /// <summary>Posição na aba "Informações adicionais" (menor primeiro).</summary>
    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    /// <summary>Texto de ajuda mostrado dentro do campo.</summary>
    [DisplayName("Dica")]
    public string? Dica { get; set; }

    /// <summary>Casas decimais (tipo Decimal). Moeda usa sempre 2.</summary>
    [DisplayName("Casas decimais")]
    public byte? CasasDecimais { get; set; }

    [DisplayName("Valor mínimo")]
    public decimal? Minimo { get; set; }

    [DisplayName("Valor máximo")]
    public decimal? Maximo { get; set; }

    /// <summary>Opções do tipo Lista. Opções usadas não são apagadas: são desativadas.</summary>
    public List<CampoPersonalizadoOpcao> Opcoes { get; set; } = new();

    public ITipoCampo Definicao => TiposCampo.Obter(Tipo);

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Campo personalizado '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Campo personalizado '{Nome}' reativado.");
    }
}

/// <summary>Uma opção de um campo do tipo lista (ex.: "Casado").</summary>
[DisplayName("Opção de campo")]
public class CampoPersonalizadoOpcao : EntidadeBase, IParteDeAgregado
{
    public const int TamanhoMaximoTexto = 80;

    public Guid CampoId { get; set; }

    [DisplayName("Opção")]
    public string Texto { get; set; } = string.Empty;

    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    [DisplayName("Ativa")]
    public bool Ativa { get; set; } = true;

    string IParteDeAgregado.RaizEntidade => nameof(CampoPersonalizado);
    Guid IParteDeAgregado.RaizId => CampoId;
}
