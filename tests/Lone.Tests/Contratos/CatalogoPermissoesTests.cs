using Lone.Contracts.Seguranca;

namespace Lone.Tests.Contratos;

public class CatalogoPermissoesTests
{
    /// <summary>A auditoria guarda o código da permissão como chave do registro (coluna de 40 caracteres).</summary>
    [Fact]
    public void Codigos_cabem_na_chave_da_auditoria()
    {
        Assert.All(Permissoes.Todas, p => Assert.InRange(p.Codigo.Length, 1, 40));
    }

    [Fact]
    public void Codigos_nao_se_repetem()
    {
        Assert.Equal(Permissoes.Todas.Count, Permissoes.Todas.Select(p => p.Codigo).Distinct().Count());
    }
}
