namespace Lone.Aplicacao.Pessoas;

/// <summary>O mínimo para citar uma pessoa numa mensagem (ex.: aviso de duplicidade).</summary>
public sealed record PessoaIdentificacao(int Id, int Codigo, string Nome)
{
    public override string ToString() => $"{Codigo:000000} - {Nome}";
}
