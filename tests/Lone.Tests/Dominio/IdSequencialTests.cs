using System.Data.SqlTypes;
using Lone.Domain.Comum;

namespace Lone.Tests.Dominio;

public class IdSequencialTests
{
    [Fact]
    public void Ids_gerados_em_sequencia_ficam_em_ordem_crescente_no_SQL_Server()
    {
        // SqlGuid compara exatamente como o SQL Server ordena uniqueidentifier.
        var ids = Enumerable.Range(0, 5_000).Select(_ => IdSequencial.Novo()).ToList();

        for (var i = 1; i < ids.Count; i++)
            Assert.True(new SqlGuid(ids[i - 1]).CompareTo(new SqlGuid(ids[i])) < 0, $"Fora de ordem na posição {i}.");
    }

    [Fact]
    public void Ids_de_instantes_diferentes_seguem_a_ordem_do_tempo()
    {
        var antes = IdSequencial.Novo(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var depois = IdSequencial.Novo(new DateTimeOffset(2030, 1, 1, 0, 0, 1, TimeSpan.Zero));

        Assert.True(new SqlGuid(antes).CompareTo(new SqlGuid(depois)) < 0);
    }

    [Fact]
    public void Instante_gravado_no_id_pode_ser_lido_de_volta()
    {
        var instante = new DateTimeOffset(2031, 6, 15, 10, 30, 45, 123, TimeSpan.Zero);

        var id = IdSequencial.Novo(instante);

        Assert.Equal(instante, IdSequencial.InstanteDe(id));
    }

    [Fact]
    public void Ids_nao_se_repetem()
    {
        var ids = Enumerable.Range(0, 10_000).Select(_ => IdSequencial.Novo()).ToHashSet();
        Assert.Equal(10_000, ids.Count);
    }
}
