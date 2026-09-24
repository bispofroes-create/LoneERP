using Lone.Core.Enums;

namespace Lone.Aplicacao.Pessoas;

public class FiltroPessoas
{
    /// <summary>Nome, código, CPF/CNPJ, telefone ou e-mail.</summary>
    public string? Texto { get; set; }

    /// <summary>Só pessoas com este papel ativo. Nulo = todas.</summary>
    public TipoPapel? Papel { get; set; }

    public bool IncluirInativos { get; set; }
    public int Limite { get; set; } = 500;
}
