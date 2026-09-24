using System.Globalization;
using System.Net;
using System.Text.Json;
using Lone.Application.Integracoes;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Validacao;

namespace Lone.Infrastructure.Integracoes.Cnpj;

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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
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

            DataAbertura = LeitorJson.Data(r, "data_inicio_atividade"),
            Porte = TituloOuNulo(LeitorJson.Texto(r, "descricao_porte") ?? LeitorJson.Texto(r, "porte")),
            CapitalSocial = LeitorJson.Decimal(r, "capital_social"),
            OpcaoSimples = LeitorJson.Logico(r, "opcao_pelo_simples"),
            OpcaoMei = LeitorJson.Logico(r, "opcao_pelo_mei"),
            CnaePrincipal = Cnae(LeitorJson.Texto(r, "cnae_fiscal")),
            NaturezaJuridica = LeitorJson.Texto(r, "codigo_natureza_juridica"),
            CnaesSecundarios = CnaesSecundarios(r),
            Socios = Socios(r),
            Fonte = "BrasilAPI"
        };
    }

    /// <summary>A Receita manda "0" quando não há CNAE secundário; códigos vêm como número (sem zero à esquerda).</summary>
    private static List<string> CnaesSecundarios(JsonElement r) =>
        !r.TryGetProperty("cnaes_secundarios", out var lista) || lista.ValueKind != JsonValueKind.Array
            ? []
            : lista.EnumerateArray().Select(c => Cnae(LeitorJson.Texto(c, "codigo"))).OfType<string>().Distinct().ToList();

    private static string? Cnae(string? codigo)
    {
        var digitos = Documento.SomenteDigitos(codigo);
        return digitos.Length is 0 || digitos.All(c => c == '0') ? null : digitos.PadLeft(7, '0');
    }

    /// <summary>Quadro de sócios e administradores (o CPF vem em parte, como a Receita divulga).</summary>
    private static List<SocioDto> Socios(JsonElement r) =>
        !r.TryGetProperty("qsa", out var lista) || lista.ValueKind != JsonValueKind.Array
            ? []
            : lista.EnumerateArray()
                .Select(s => new SocioDto
                {
                    Nome = TituloOuNulo(LeitorJson.Texto(s, "nome_socio")) ?? string.Empty,
                    Qualificacao = LeitorJson.Texto(s, "qualificacao_socio"),
                    Documento = LeitorJson.Texto(s, "cnpj_cpf_do_socio"),
                    EntradaEm = LeitorJson.Data(s, "data_entrada_sociedade")
                })
                .Where(s => s.Nome.Length > 0)
                .ToList();

    /// <summary>A Receita devolve tudo em maiúsculas; deixa "RUA DAS FLORES" como "Rua Das Flores".</summary>
    private static string? TituloOuNulo(string? s) => s is null ? null : Texto.ToTitleCase(s.ToLowerInvariant());
}
