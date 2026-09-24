using System.Globalization;
using System.Net;
using System.Text.Json;
using Lone.Aplicacao.Integracoes;
using Lone.Core.Validacao;

namespace Lone.Integracoes.Cnpj;

/// <summary>Consulta de CNPJ pela BrasilAPI (gratuita, dados públicos da Receita Federal).</summary>
public class BrasilApiCnpjConsulta : ICnpjConsulta
{
    private const string Servico = "consulta de CNPJ";
    private static readonly TextInfo Texto = new CultureInfo("pt-BR").TextInfo;

    private readonly HttpClient _http;

    public BrasilApiCnpjConsulta(HttpClient http)
    {
        _http = http;
    }

    public async Task<DadosCnpj?> ConsultarAsync(string cnpj, CancellationToken ct = default)
    {
        var numero = Documento.Normalizar(cnpj);

        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.GetAsync($"cnpj/v1/{numero}", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw ErroIntegracao.SemConexao(Servico, ex);
        }

        using (resposta)
        {
            if (resposta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                return null;
            if (!resposta.IsSuccessStatusCode)
                throw ErroIntegracao.RespostaInesperada(Servico, (int)resposta.StatusCode);

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(fluxo, cancellationToken: ct);
            return Converter(json.RootElement, numero);
        }
    }

    private static DadosCnpj Converter(JsonElement r, string numero)
    {
        var tipoLogradouro = LeitorJson.Texto(r, "descricao_tipo_de_logradouro");
        var logradouro = LeitorJson.Texto(r, "logradouro");
        if (tipoLogradouro is not null && logradouro is not null &&
            !logradouro.StartsWith(tipoLogradouro, StringComparison.OrdinalIgnoreCase))
            logradouro = $"{tipoLogradouro} {logradouro}";

        var numeroEndereco = LeitorJson.Texto(r, "numero");
        var cep = Documento.SomenteDigitos(LeitorJson.Texto(r, "cep"));

        return new DadosCnpj
        {
            Cnpj = numero,
            RazaoSocial = LeitorJson.Texto(r, "razao_social") ?? string.Empty,
            NomeFantasia = LeitorJson.Texto(r, "nome_fantasia"),
            SituacaoCadastral = LeitorJson.Texto(r, "descricao_situacao_cadastral"),
            EhMatriz = LeitorJson.Inteiro(r, "identificador_matriz_filial") == 1,

            Cep = cep.Length == 0 ? null : cep.PadLeft(8, '0'), // CEP pode vir como número (sem zero à esquerda)
            Logradouro = TituloOuNulo(logradouro),
            Numero = numeroEndereco,
            Complemento = TituloOuNulo(LeitorJson.Texto(r, "complemento")),
            Bairro = TituloOuNulo(LeitorJson.Texto(r, "bairro")),
            Cidade = TituloOuNulo(LeitorJson.Texto(r, "municipio")),
            Uf = LeitorJson.Texto(r, "uf")?.ToUpperInvariant(),
            CodigoMunicipioIbge = LeitorJson.Texto(r, "codigo_municipio_ibge"),

            Telefone = LeitorJson.Texto(r, "ddd_telefone_1"),
            Email = LeitorJson.Texto(r, "email")?.ToLowerInvariant(),
            Fonte = "BrasilAPI"
        };
    }

    /// <summary>A Receita devolve tudo em maiúsculas; deixa "RUA DAS FLORES" como "Rua Das Flores".</summary>
    private static string? TituloOuNulo(string? s) => s is null ? null : Texto.ToTitleCase(s.ToLowerInvariant());
}
