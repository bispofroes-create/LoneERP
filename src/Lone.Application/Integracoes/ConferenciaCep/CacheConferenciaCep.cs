using System.Collections.Concurrent;
using Lone.Domain.Enderecos.ConferenciaCep;

namespace Lone.Application.Integracoes.ConferenciaCep;

/// <summary>
/// Cache transitório da F2 (DM2), só em memória: some ao reiniciar a API e nunca é fonte de verdade. Guarda só
/// resultados <b>funcionais</b> (encontrado, inexistente, busca feita); falha técnica nunca entra. Não é a tabela
/// <c>CacheCep</c> da F3.
/// </summary>
public sealed class CacheConferenciaCep
{
    /// <summary>Política inicial da F2 (parecer final DM2/DM3, §10).</summary>
    public static readonly TimeSpan ValidadeEncontrado = TimeSpan.FromHours(24);
    public static readonly TimeSpan ValidadeInexistente = TimeSpan.FromHours(1);
    public static readonly TimeSpan ValidadeBusca = TimeSpan.FromHours(1);

    /// <summary>Teto de itens (consultas + buscas). Passou: saem os vencidos e, se precisar, os que vencem primeiro.</summary>
    public const int MaximoItens = 5000;

    private readonly TimeProvider _relogio;
    private readonly ConcurrentDictionary<string, (DateTimeOffset Vence, object Valor)> _itens = new(StringComparer.Ordinal);

    public CacheConferenciaCep(TimeProvider relogio) => _relogio = relogio;

    public int Quantidade => _itens.Count;

    public static string ChaveCep(string cep) => "cep:" + Lone.Domain.Enderecos.DuplicidadeEndereco.Cep(cep);
    public static string ChaveBusca(BuscaEnderecoCep busca) => "busca:" + busca.Chave;

    public ResultadoProvedorCep? ObterConsulta(string cep) => Obter<ResultadoProvedorCep>(ChaveCep(cep));

    public ResultadoBuscaProvedorCep? ObterBusca(BuscaEnderecoCep busca) => Obter<ResultadoBuscaProvedorCep>(ChaveBusca(busca));

    /// <summary>Guarda a consulta por CEP: encontrado 24 h, inexistente 1 h; falha técnica é ignorada.</summary>
    public void GuardarConsulta(string cep, ResultadoProvedorCep resultado)
    {
        var validade = resultado.Situacao switch
        {
            SituacaoProvedorCep.Encontrado => ValidadeEncontrado,
            SituacaoProvedorCep.NaoEncontrado => ValidadeInexistente,
            _ => (TimeSpan?)null
        };
        if (validade is { } v) Guardar(ChaveCep(cep), resultado, v);
    }

    /// <summary>Guarda a busca por endereço (com ou sem candidatos) por 1 h; falha técnica é ignorada.</summary>
    public void GuardarBusca(BuscaEnderecoCep busca, ResultadoBuscaProvedorCep resultado)
    {
        if (!resultado.Situacao.EhFalhaTecnica()) Guardar(ChaveBusca(busca), resultado, ValidadeBusca);
    }

    private T? Obter<T>(string chave) where T : class
    {
        if (!_itens.TryGetValue(chave, out var item)) return null;
        if (item.Vence > _relogio.GetUtcNow()) return item.Valor as T;
        _itens.TryRemove(new KeyValuePair<string, (DateTimeOffset, object)>(chave, item));
        return null;
    }

    private void Guardar(string chave, object valor, TimeSpan validade)
    {
        var agora = _relogio.GetUtcNow();
        _itens[chave] = (agora + validade, valor);
        if (_itens.Count <= MaximoItens) return;

        foreach (var vencido in _itens.Where(i => i.Value.Vence <= agora).ToList())
            _itens.TryRemove(vencido);
        foreach (var antigo in _itens.OrderBy(i => i.Value.Vence).Take(Math.Max(0, _itens.Count - MaximoItens)).ToList())
            _itens.TryRemove(antigo);
    }
}

/// <summary>
/// Sugestões de CEP que a própria API emitiu (caso 4 da conferência), por 1 h, só em memória. Na gravação, a procedência
/// ("aplicado pela sugestão") só é registrada se o par CEP conferido → CEP sugerido, com a fonte, tiver saído daqui: o
/// aplicativo não pode inventar a procedência (DM3). Reiniciou a API: a procedência deixa de ser registrada, o CEP grava
/// normalmente.
/// </summary>
public sealed class SugestoesCepEmitidas
{
    public static readonly TimeSpan Validade = CacheConferenciaCep.ValidadeBusca;

    private readonly TimeProvider _relogio;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _emitidas = new(StringComparer.Ordinal);

    public SugestoesCepEmitidas(TimeProvider relogio) => _relogio = relogio;

    private static string Chave(string cepConferido, string cepSugerido, CepFonte fonte) =>
        $"{Lone.Domain.Enderecos.DuplicidadeEndereco.Cep(cepConferido)}>{Lone.Domain.Enderecos.DuplicidadeEndereco.Cep(cepSugerido)}@{(int)fonte}";

    public void Registrar(string cepConferido, string cepSugerido, CepFonte fonte)
    {
        var agora = _relogio.GetUtcNow();
        _emitidas[Chave(cepConferido, cepSugerido, fonte)] = agora + Validade;
        if (_emitidas.Count > CacheConferenciaCep.MaximoItens)
            foreach (var vencida in _emitidas.Where(e => e.Value <= agora).ToList())
                _emitidas.TryRemove(vencida);
    }

    public bool FoiEmitida(string cepConferido, string cepSugerido, CepFonte fonte) =>
        _emitidas.TryGetValue(Chave(cepConferido, cepSugerido, fonte), out var vence) && vence > _relogio.GetUtcNow();
}
