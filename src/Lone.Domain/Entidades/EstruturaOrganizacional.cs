using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>
/// Cargo (ex.: "Vendedor interno"). Pode apontar para a ocupação da CBO (eSocial e relatórios). Nunca é excluído:
/// desativado, some das escolhas novas mas continua nas lotações que já o têm. Nome único sem diferenciar
/// maiúsculas nem acentos.
/// </summary>
[DisplayName("Cargo")]
public class Cargo : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 80;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Código CBO (tabela oficial), opcional.</summary>
    [DisplayName("Ocupação (CBO)")]
    public int? OcupacaoCboId { get; set; }

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Cargo '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Cargo '{Nome}' reativado.");
    }
}

/// <summary>Departamento (ex.: "Comercial"). Tem setores. Nunca é excluído: desativado.</summary>
[DisplayName("Departamento")]
public class Departamento : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 80;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Departamento '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Departamento '{Nome}' reativado.");
    }
}

/// <summary>Setor de um departamento (ex.: Comercial → "Televendas"). Nome único dentro do departamento. Nunca é excluído.</summary>
[DisplayName("Setor")]
public class Setor : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 80;

    /// <summary>Departamento a que o setor pertence (não muda depois de haver lotações no setor).</summary>
    [DisplayName("Departamento")]
    public Guid DepartamentoId { get; set; }

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Setor '{Nome}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Setor '{Nome}' reativado.");
    }
}

/// <summary>
/// Centro de custo, em árvore (ex.: "1 Administrativo" → "1.01 RH"). Só os analíticos (sem filhos) recebem
/// colaboradores e lançamentos; os sintéticos agrupam. Código único. Nunca é excluído: desativado.
/// </summary>
[DisplayName("Centro de custo")]
public class CentroCusto : AgregadoRaiz
{
    public const int TamanhoMaximoCodigo = 20;
    public const int TamanhoMaximoNome = 80;

    /// <summary>Código estruturado (ex.: "1.01"); único.</summary>
    [DisplayName("Código")]
    public string Codigo { get; set; } = string.Empty;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Centro de custo pai (nulo = raiz).</summary>
    [DisplayName("Centro de custo pai")]
    public Guid? PaiId { get; set; }

    /// <summary>Analítico = recebe colaboradores/lançamentos; sintético = só agrupa (pode ter filhos).</summary>
    [DisplayName("Analítico")]
    public bool Analitico { get; set; } = true;

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    public string Descricao => $"{Codigo} {Nome}";

    public void Desativar()
    {
        if (!Ativo) return;
        Ativo = false;
        RegistrarEvento($"Centro de custo '{Descricao}' desativado.");
    }

    public void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        RegistrarEvento($"Centro de custo '{Descricao}' reativado.");
    }
}
