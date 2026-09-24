namespace Lone.Aplicacao.Seguranca;

public enum SituacaoLogin
{
    Sucesso,
    CredenciaisInvalidas,
    Bloqueado,
    Inativo
}

public sealed record ResultadoLogin(SituacaoLogin Situacao, string Mensagem)
{
    public bool Sucesso => Situacao == SituacaoLogin.Sucesso;
}
