using System.Security.Claims;
using Lone.Application.Seguranca;
using Lone.Contracts.Empresas;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Lone.Infrastructure.Seguranca;

/// <summary>
/// Emite o token de acesso (JWT assinado com HMAC-SHA256). O token diz quem é o usuário e a empresa ativa;
/// as permissões não vão no token — a API as calcula a cada requisição, então mudanças valem na hora.
/// </summary>
public sealed class EmissorTokenJwt : IEmissorToken
{
    private readonly OpcoesJwt _opcoes;
    private readonly TimeProvider _relogio;
    private readonly SigningCredentials _credenciais;
    private readonly JsonWebTokenHandler _manipulador = new();

    public EmissorTokenJwt(IOptions<OpcoesJwt> opcoes, TimeProvider relogio)
    {
        _opcoes = opcoes.Value;
        _opcoes.Validar();
        _relogio = relogio;
        _credenciais = new SigningCredentials(_opcoes.ChaveAssinatura(), SecurityAlgorithms.HmacSha256);
    }

    public TimeSpan ValidadeRenovacao => TimeSpan.FromDays(_opcoes.DiasRenovacao);

    public TokenAcesso Emitir(Guid usuarioId, string nome, string login, EmpresaAtiva? empresa)
    {
        var agora = _relogio.GetUtcNow();
        var expira = agora.AddMinutes(_opcoes.MinutosAcesso);

        var claims = new List<Claim>
        {
            new(ClaimsLone.Usuario, usuarioId.ToString()),
            new(ClaimsLone.Nome, nome),
            new(ClaimsLone.Login, login),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (empresa is not null)
        {
            claims.Add(new Claim(ClaimsLone.Empresa, empresa.EmpresaId.ToString()));
            claims.Add(new Claim(ClaimsLone.Estabelecimento, empresa.EstabelecimentoId.ToString()));
        }

        var token = _manipulador.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _opcoes.Emissor,
            Audience = _opcoes.Publico,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expira.UtcDateTime,
            SigningCredentials = _credenciais
        });

        return new TokenAcesso(token, expira);
    }
}
