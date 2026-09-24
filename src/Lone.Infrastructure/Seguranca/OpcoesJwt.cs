using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Lone.Infrastructure.Seguranca;

/// <summary>
/// Configuração dos tokens (seção "Jwt" do appsettings da API). A chave NUNCA vai para o repositório:
/// em desenvolvimento fica nos "segredos do usuário" do Visual Studio; em produção, em variável de ambiente.
/// </summary>
public sealed class OpcoesJwt
{
    public const string Secao = "Jwt";

    /// <summary>Quem emite (a API do Lone).</summary>
    public string Emissor { get; set; } = "Lone.Api";

    /// <summary>Para quem o token vale (os aplicativos do Lone).</summary>
    public string Publico { get; set; } = "Lone.App";

    /// <summary>Chave de assinatura HMAC-SHA256, com pelo menos 32 caracteres (256 bits).</summary>
    public string Chave { get; set; } = string.Empty;

    /// <summary>Validade do token de acesso. Curta: o aplicativo renova sozinho.</summary>
    public int MinutosAcesso { get; set; } = 15;

    /// <summary>Validade do token de renovação (sessão sem uso por mais tempo pede login de novo).</summary>
    public int DiasRenovacao { get; set; } = 14;

    public const int TamanhoMinimoChave = 32;

    public SymmetricSecurityKey ChaveAssinatura() => new(Encoding.UTF8.GetBytes(Chave));

    /// <summary>Mesmas regras usadas pela API para aceitar um token (assinatura, emissor, público, validade).</summary>
    public TokenValidationParameters ParametrosValidacao() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Emissor,
        ValidateAudience = true,
        ValidAudience = Publico,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = ChaveAssinatura(),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimsLone.Nome
    };

    /// <summary>Falha cedo, na inicialização, em vez de emitir tokens fracos.</summary>
    public void Validar()
    {
        if (Chave.Length < TamanhoMinimoChave)
            throw new InvalidOperationException(
                $"A chave JWT (configuração \"{Secao}:Chave\") precisa ter pelo menos {TamanhoMinimoChave} caracteres. " +
                "Em desenvolvimento, defina-a nos segredos do usuário do projeto Lone.Api.");
        if (MinutosAcesso is < 1 or > 120)
            throw new InvalidOperationException($"\"{Secao}:MinutosAcesso\" deve ficar entre 1 e 120.");
        if (DiasRenovacao is < 1 or > 90)
            throw new InvalidOperationException($"\"{Secao}:DiasRenovacao\" deve ficar entre 1 e 90.");
    }
}

/// <summary>Nomes das informações (claims) que o token do Lone carrega.</summary>
public static class ClaimsLone
{
    public const string Usuario = "sub";
    public const string Nome = "name";
    public const string Login = "login";
    public const string Empresa = "empresa";
    public const string Estabelecimento = "estabelecimento";
}
