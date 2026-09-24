using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Configuracoes;

/// <summary>Sequências do SQL Server usadas para códigos de exibição.</summary>
internal static class SequenciasConfiguration
{
    public static void Configurar(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>(PessoaConfiguration.SequenciaCodigo).StartsAt(1).IncrementsBy(1);
    }
}
