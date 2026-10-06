using System.Net;
using System.Text.Json;
using Lone.Application.Integracoes;
using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Contracts.Integracoes;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Validacao;
using Microsoft.Extensions.Logging;

namespace Lone.Infrastructure.Integracoes.Cep;

/// <summary>
/// ViaCEP (gratuito; devolve o código IBGE e a faixa de numeração no "complemento"). É a única integração com o ViaCEP:
/// fonte do motor (<see cref="IProvedorCep"/>, consulta por CEP e busca por endereço) e, pelo mesmo cliente HTTP e o
/// mesmo pipeline de resiliência, a consulta antiga <see cref="ICepConsulta"/> (rota GET consultas/cep/{cep}). Só
/// traduz a resposta; não decide nada sobre o CEP.
/// </summary>
public sealed class ViaCepProvedor : IProvedorCep, ICepConsulta
{
    private const string Servico = "consulta de CEP";

    /// <summary>Máximo de CEPs que o ViaCEP devolve numa busca por endereço; chegou nele, a lista pode estar cortada.</summary>
    public const int LimiteBusca = 50;
    private readonly HttpClient _http;
    private readonly ILogger<ViaCepProvedor> _log;

    public ViaCepProvedor(HttpClient http, ILogger<ViaCepProvedor> log)
    {
        _http = http;
        _log = log;
    }

    public CepFonte Fonte => CepFonte.ViaCep;
    public bool BuscaPorEndereco => true;

    public async Task<ResultadoProvedorCep> ConsultarPorCepAsync(string cep, CancellationToken ct = default)
    {
        var numero = Documento.SomenteDigitos(cep);
        if (numero.Length != 8) return ResultadoProvedorCep.NaoEncontrado(Fonte);

        var r = await ChamadaCep.GetAsync(_http, $"{numero}/json/", ct);
        if (r.Tecnica) return ChamadaCep.FalhaConsulta(_log, Fonte, r, numero);
        if (r.Status == HttpStatusCode.BadRequest) return ResultadoProvedorCep.NaoEncontrado(Fonte); // CEP malformado
        if (r.Json is null) return ChamadaCep.InvalidaConsulta(_log, Fonte, numero, $"HTTP {(int)r.Status}");

        using var json = r.Json;
        var raiz = json.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object) return ChamadaCep.InvalidaConsulta(_log, Fonte, numero, "não é objeto");

        // CEP inexistente: o ViaCEP responde 200 com { "erro": true } (ou "true"). É resultado, não falha.
        if (LeitorJson.Texto(raiz, "erro") == "true") return ResultadoProvedorCep.NaoEncontrado(Fonte);

        var registro = Ler(raiz);
        if (registro is null || registro.Cep != numero)
            return ChamadaCep.InvalidaConsulta(_log, Fonte, numero, "sem o CEP pedido");
        return ResultadoProvedorCep.Encontrado(Fonte, registro);
    }

    /// <summary>
    /// GET {UF}/{cidade}/{logradouro}/json/ (até 50 CEPs). Lista vazia = nenhum CEP para o endereço (resultado
    /// funcional). 400 aqui é falha de contrato (os dados já chegam conferidos), não "não encontrado".
    /// </summary>
    public async Task<ResultadoBuscaProvedorCep> BuscarPorEnderecoAsync(BuscaEnderecoCep busca, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(busca);
        var rota = string.Join('/', Uri.EscapeDataString(busca.Uf), Uri.EscapeDataString(busca.Cidade),
            Uri.EscapeDataString(busca.Logradouro), "json/");
        var r = await ChamadaCep.GetAsync(_http, rota, ct);
        if (r.Tecnica)
        {
            _log.LogWarning("Busca de CEP por endereço na fonte {Fonte} falhou: {Falha}", Fonte, r.Falha);
            return r.Invalida ? ResultadoBuscaProvedorCep.Invalida(Fonte, r.Falha!) : ResultadoBuscaProvedorCep.Indisponivel(Fonte, r.Falha!);
        }
        if (r.Json is null) return InvalidaBusca($"HTTP {(int)r.Status}");

        using var json = r.Json;
        var raiz = json.RootElement;
        if (raiz.ValueKind == JsonValueKind.Object && LeitorJson.Texto(raiz, "erro") == "true")
            return ResultadoBuscaProvedorCep.Com(Fonte, []);
        if (raiz.ValueKind != JsonValueKind.Array) return InvalidaBusca("não é lista");

        var registros = new List<RegistroCep>();
        foreach (var item in raiz.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || Ler(item) is not { } registro) return InvalidaBusca("item sem CEP");
            registros.Add(registro);
        }
        return ResultadoBuscaProvedorCep.Com(Fonte, registros, limiteAtingido: registros.Count >= LimiteBusca);
    }

    private ResultadoBuscaProvedorCep InvalidaBusca(string motivo)
    {
        _log.LogWarning("Busca de CEP por endereço na fonte {Fonte}: resposta fora do contrato ({Motivo})", Fonte, motivo);
        return ResultadoBuscaProvedorCep.Invalida(Fonte, motivo);
    }

    /// <summary>Um CEP do JSON do ViaCEP; nulo sem CEP de 8 dígitos. Nada é inventado: campo ausente fica nulo.</summary>
    internal static RegistroCep? Ler(JsonElement e)
    {
        var cep = Documento.SomenteDigitos(LeitorJson.Texto(e, "cep"));
        if (cep.Length != 8) return null;
        return new RegistroCep(cep, LeitorJson.Texto(e, "logradouro"), LeitorJson.Texto(e, "complemento"), LeitorJson.Texto(e, "bairro"),
            LeitorJson.Texto(e, "localidade"), LeitorJson.Texto(e, "uf"), LeitorJson.Texto(e, "ibge"),
            LeitorJson.Texto(e, "unidade")); // nome do prédio/grande usuário: só explicação na tela
    }

    /// <summary>
    /// Contrato antigo da rota GET consultas/cep/{cep}, sem mudança: nulo = CEP não existe; falha técnica =
    /// <see cref="ServicoExternoException"/> (HTTP 502). Agora com o mesmo pipeline de resiliência do motor.
    /// </summary>
    async Task<DadosCep?> ICepConsulta.ConsultarAsync(string cep, CancellationToken ct)
    {
        var numero = Documento.SomenteDigitos(cep);
        if (numero.Length != 8) return null;

        var r = await ConsultarPorCepAsync(numero, ct);
        return r.Situacao switch
        {
            SituacaoProvedorCep.Encontrado => new DadosCep
            {
                Cep = r.Registro!.Cep,
                Logradouro = r.Registro.Logradouro,
                Complemento = r.Registro.Complemento,
                Bairro = r.Registro.Bairro,
                Cidade = r.Registro.Cidade,
                Uf = r.Registro.Uf,
                CodigoMunicipioIbge = r.Registro.CodigoMunicipioIbge
            },
            SituacaoProvedorCep.NaoEncontrado => null,
            SituacaoProvedorCep.RespostaInvalida => throw new ServicoExternoException(
                $"O serviço de {Servico} respondeu de forma inesperada. Tente de novo mais tarde."),
            _ => throw new ServicoExternoException($"Não foi possível conectar ao serviço de {Servico}. Tente de novo em instantes.")
        };
    }
}
