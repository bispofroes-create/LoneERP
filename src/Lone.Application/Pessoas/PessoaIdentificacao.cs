namespace Lone.Application.Pessoas;

/// <summary>O mínimo para citar uma pessoa numa mensagem (ex.: aviso de duplicidade).</summary>
public sealed record PessoaIdentificacao(Guid Id, int Codigo, string Nome)
{
    public override string ToString() => $"{Codigo:000000} - {Nome}";
}
