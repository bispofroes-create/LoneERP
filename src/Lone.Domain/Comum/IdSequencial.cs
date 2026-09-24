using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Lone.Domain.Comum;

/// <summary>
/// Gera as chaves (Guid) dos registros do Lone, no servidor ou no aparelho (inclusive offline).
/// O SQL Server ordena uniqueidentifier começando pelos 6 últimos bytes; por isso o instante da geração
/// vai nesses bytes (em ordem crescente) e os registros novos entram no fim do índice, sem fragmentá-lo.
/// Guid.NewGuid() (aleatório) e Guid.CreateVersion7() (instante nos primeiros bytes) fragmentariam o índice.
/// </summary>
public static class IdSequencial
{
    private static readonly Lock Trava = new();
    private static long _ultimoInstante;
    private static ushort _contador;

    /// <summary>Novo Guid: 8 bytes aleatórios, 2 de contador e 6 com o instante atual em milissegundos.</summary>
    public static Guid Novo()
    {
        var milissegundos = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ushort contador;

        lock (Trava)
        {
            // Mesmo milissegundo (ou relógio que voltou): o contador mantém a ordem dentro do processo.
            if (milissegundos <= _ultimoInstante)
            {
                milissegundos = _ultimoInstante;
                if (++_contador == 0)
                    milissegundos = ++_ultimoInstante;
            }
            else
            {
                _ultimoInstante = milissegundos;
                _contador = 0;
            }
            contador = _contador;
        }

        return Montar(milissegundos, contador);
    }

    /// <summary>Guid de um instante informado, sem afetar a sequência do processo (usado em testes e importações).</summary>
    public static Guid Novo(DateTimeOffset instante) => Montar(instante.ToUnixTimeMilliseconds(), 0);

    private static Guid Montar(long milissegundos, ushort contador)
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes[..8]);

        // Bytes 8-9: segundo critério de ordenação do SQL Server.
        BinaryPrimitives.WriteUInt16BigEndian(bytes.Slice(8, 2), contador);

        // Bytes 10-15: primeiro critério de ordenação do SQL Server (48 bits de milissegundos, big-endian).
        Span<byte> instanteBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(instanteBytes, milissegundos);
        instanteBytes[2..].CopyTo(bytes[10..]);

        return new Guid(bytes);
    }

    /// <summary>Instante gravado no Guid (útil para diagnóstico). Só vale para Guids gerados por esta classe.</summary>
    public static DateTimeOffset InstanteDe(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);
        Span<byte> instanteBytes = stackalloc byte[8];
        bytes[10..].CopyTo(instanteBytes[2..]);
        return DateTimeOffset.FromUnixTimeMilliseconds(BinaryPrimitives.ReadInt64BigEndian(instanteBytes));
    }
}
