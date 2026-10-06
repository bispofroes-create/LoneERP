using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Domain.Enderecos.ConferenciaCep;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// Fase 1 (T-2 / L-6): o teto de 5000 itens do cache transitório da F2, como ele funciona hoje (documentado, não alterado).
/// Acima do teto saem primeiro os vencidos e, se ainda precisar, os que vencem primeiro (não os mais antigos). Falha
/// técnica nunca entra. Os testes usam vencimentos distintos: com vencimentos iguais, qual sai não é determinado (ver
/// relatório da Fase 1).
/// </summary>
public class CacheConferenciaCepTests
{
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));

    private static string Cep(int i) => (10000000 + i).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static ResultadoProvedorCep Achou(string cep) =>
        ResultadoProvedorCep.Encontrado(CepFonte.ViaCep, new RegistroCep(cep, "Rua A", null, "Centro", "Curvelo", "MG", "3120904"));

    /// <summary>Guarda <paramref name="quantidade"/> CEPs encontrados, um a cada milissegundo (vencimentos distintos).</summary>
    private void Encher(CacheConferenciaCep cache, int quantidade, int inicio = 0)
    {
        for (var i = inicio; i < inicio + quantidade; i++)
        {
            cache.GuardarConsulta(Cep(i), Achou(Cep(i)));
            _relogio.Advance(TimeSpan.FromMilliseconds(1));
        }
    }

    [Fact]
    public void Ate_o_teto_nada_sai()
    {
        var cache = new CacheConferenciaCep(_relogio);

        Encher(cache, CacheConferenciaCep.MaximoItens);

        Assert.Equal(5000, CacheConferenciaCep.MaximoItens);
        Assert.Equal(CacheConferenciaCep.MaximoItens, cache.Quantidade);
        Assert.NotNull(cache.ObterConsulta(Cep(0)));
        Assert.NotNull(cache.ObterConsulta(Cep(4999)));
    }

    [Fact]
    public void Passou_do_teto_sai_o_que_vence_primeiro_e_o_total_volta_ao_teto()
    {
        var cache = new CacheConferenciaCep(_relogio);
        Encher(cache, CacheConferenciaCep.MaximoItens);

        Encher(cache, 1, inicio: 5000);

        Assert.Equal(CacheConferenciaCep.MaximoItens, cache.Quantidade);
        Assert.Null(cache.ObterConsulta(Cep(0)));                // vencia primeiro
        Assert.NotNull(cache.ObterConsulta(Cep(1)));
        Assert.NotNull(cache.ObterConsulta(Cep(5000)));          // o novo fica
    }

    [Fact]
    public void Vencidos_saem_antes_dos_validos()
    {
        var cache = new CacheConferenciaCep(_relogio);
        cache.GuardarConsulta("99999999", ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep)); // 1 h
        _relogio.Advance(TimeSpan.FromHours(2));                                                 // venceu, ainda guardado

        Encher(cache, CacheConferenciaCep.MaximoItens);          // o último passa do teto

        Assert.Equal(CacheConferenciaCep.MaximoItens, cache.Quantidade);
        Assert.NotNull(cache.ObterConsulta(Cep(0)));             // nenhum válido saiu: bastou tirar o vencido
        Assert.NotNull(cache.ObterConsulta(Cep(4999)));
    }

    [Fact]
    public void Documenta_que_sai_o_que_vence_primeiro_mesmo_que_seja_o_recem_guardado()
    {
        // Comportamento atual (não alterado na Fase 1): o critério é o vencimento, não a idade. Com o cache cheio de CEPs
        // encontrados (24 h), um inexistente novo (1 h) vence antes de todos e é ele que sai. Não há perda de dado: o cache
        // só evita chamadas; a próxima conferência desse CEP consulta a fonte de novo.
        var cache = new CacheConferenciaCep(_relogio);
        Encher(cache, CacheConferenciaCep.MaximoItens);

        cache.GuardarConsulta("99999999", ResultadoProvedorCep.NaoEncontrado(CepFonte.ViaCep));

        Assert.Equal(CacheConferenciaCep.MaximoItens, cache.Quantidade);
        Assert.Null(cache.ObterConsulta("99999999"));
        Assert.NotNull(cache.ObterConsulta(Cep(0)));
    }

    [Fact]
    public void Consultas_e_buscas_dividem_o_mesmo_teto()
    {
        var cache = new CacheConferenciaCep(_relogio);
        Encher(cache, CacheConferenciaCep.MaximoItens - 1);

        cache.GuardarBusca(new BuscaEnderecoCep("MG", "Curvelo", "RUA A"), ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, []));
        _relogio.Advance(TimeSpan.FromMilliseconds(1));
        cache.GuardarBusca(new BuscaEnderecoCep("MG", "Curvelo", "RUA B"), ResultadoBuscaProvedorCep.Com(CepFonte.ViaCep, []));

        Assert.Equal(CacheConferenciaCep.MaximoItens, cache.Quantidade);
        // A busca vence em 1 h, antes dos CEPs encontrados (24 h): sai a primeira busca guardada.
        Assert.Null(cache.ObterBusca(new BuscaEnderecoCep("MG", "Curvelo", "RUA A")));
        Assert.NotNull(cache.ObterBusca(new BuscaEnderecoCep("MG", "Curvelo", "RUA B")));
        Assert.NotNull(cache.ObterConsulta(Cep(0)));
    }

    [Fact]
    public void Falha_tecnica_com_o_cache_cheio_nao_entra_nem_tira_ninguem()
    {
        var cache = new CacheConferenciaCep(_relogio);
        Encher(cache, CacheConferenciaCep.MaximoItens);

        cache.GuardarConsulta("99999999", ResultadoProvedorCep.Indisponivel(CepFonte.ViaCep, "timeout"));
        cache.GuardarBusca(new BuscaEnderecoCep("MG", "Curvelo", "RUA A"), ResultadoBuscaProvedorCep.Indisponivel(CepFonte.ViaCep, "timeout"));

        Assert.Equal(CacheConferenciaCep.MaximoItens, cache.Quantidade);
        Assert.Null(cache.ObterConsulta("99999999"));
        Assert.NotNull(cache.ObterConsulta(Cep(0)));
    }

    [Fact]
    public void Documenta_que_sugestoes_emitidas_validas_nao_saem_acima_do_teto()
    {
        // Comportamento atual (não alterado): acima de 5000, as sugestões emitidas só perdem as vencidas; as válidas
        // ficam (a procedência DM3 não se perde dentro da 1 h). Não há teto rígido nesse registro (ver relatório).
        var sugestoes = new SugestoesCepEmitidas(_relogio);

        for (var i = 0; i <= CacheConferenciaCep.MaximoItens; i++)
            sugestoes.Registrar("35790000", Cep(i), CepFonte.ViaCep);

        Assert.True(sugestoes.FoiEmitida("35790000", Cep(0), CepFonte.ViaCep));
        Assert.True(sugestoes.FoiEmitida("35790000", Cep(CacheConferenciaCep.MaximoItens), CepFonte.ViaCep));

        _relogio.Advance(SugestoesCepEmitidas.Validade);
        Assert.False(sugestoes.FoiEmitida("35790000", Cep(0), CepFonte.ViaCep)); // venceu
    }
}
