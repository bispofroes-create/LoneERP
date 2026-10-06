using Lone.Contracts.Pessoas;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Entidades;

namespace Lone.Application.Pessoas;

/// <summary>
/// F3, no Salvar da pessoa: as três colunas de conferência do CEP de cada endereço (<see cref="EstadoConferenciaCep"/>).
/// Nenhuma consulta externa (salvar não depende da internet). O que o aplicativo manda nas colunas é ignorado; o pedido
/// "conferido na ficha" só vale se a conferência saiu desta API para exatamente os dados gravados.
/// </summary>
public static class EstadoConferenciaCepNoSalvar
{
    /// <param name="novos">Os endereços que serão gravados (já normalizados, com o município copiado do IBGE).</param>
    /// <param name="gravados">Os endereços como estão no banco (nulo = pessoa nova).</param>
    /// <param name="enviados">O que a ficha mandou (só o pedido <see cref="EnderecoDto.ConferenciaCepNaFicha"/> é lido).</param>
    /// <param name="conferenciaFeita">A conferência que esta API fez para uma assinatura de dados (nula se não fez).</param>
    public static void Aplicar(IEnumerable<PessoaEndereco> novos, IEnumerable<PessoaEndereco>? gravados, IEnumerable<EnderecoDto> enviados,
                               Func<string?, ConferenciaCepRealizada?> conferenciaFeita)
    {
        var anteriores = gravados?.ToDictionary(e => e.Id) ?? new Dictionary<Guid, PessoaEndereco>();
        var pedidos = enviados.Where(e => e.ConferenciaCepNaFicha && e.Id != Guid.Empty).Select(e => e.Id).ToHashSet();
        foreach (var e in novos)
        {
            var conferencia = pedidos.Contains(e.Id) ? conferenciaFeita(EstadoConferenciaCep.Assinatura(e)) : null;
            EstadoConferenciaCep.Aplicar(e, anteriores.GetValueOrDefault(e.Id), conferencia);
        }
    }
}
