using Lone.Application.Seguranca;

namespace Lone.Tests.Aplicacao;

public class PoliticaSenhaTests
{
    [Theory]
    [InlineData("curta1")]
    [InlineData("somenteletras")]
    [InlineData("12345678")]
    public void Senhas_fracas_sao_recusadas(string senha) =>
        Assert.NotEmpty(PoliticaSenha.ValidarSenha(senha, "maria"));

    [Fact]
    public void Senha_igual_ao_login_e_recusada() =>
        Assert.Contains(PoliticaSenha.ValidarSenha("maria2026", "MARIA2026"), e => e.Contains("login"));

    [Fact]
    public void Senha_com_letras_e_numeros_e_aceita() =>
        Assert.Empty(PoliticaSenha.ValidarSenha("Lone2026erp", "maria"));

    [Theory]
    [InlineData("  Maria.Silva ", "maria.silva")]
    [InlineData("JOAO_2", "joao_2")]
    public void Login_e_normalizado(string entrada, string esperado) =>
        Assert.Equal(esperado, PoliticaSenha.NormalizarLogin(entrada));

    [Theory]
    [InlineData("ab", false)]
    [InlineData("joão", false)]
    [InlineData("joao silva", false)]
    [InlineData("joao.silva-2", true)]
    public void Formato_do_login(string login, bool valido) =>
        Assert.Equal(valido, PoliticaSenha.LoginValido(login));

    [Fact]
    public void Hash_de_senha_confere_e_nao_guarda_a_senha()
    {
        var hasher = new HasherSenhaPbkdf2();
        var hash = hasher.Gerar("Lone2026erp");

        Assert.DoesNotContain("Lone2026erp", hash);
        Assert.True(hasher.Verificar("Lone2026erp", hash));
        Assert.False(hasher.Verificar("Lone2026erX", hash));
    }

    [Fact]
    public void Token_de_renovacao_e_unico_e_o_hash_e_deterministico()
    {
        var (token1, hash1) = GeradorTokenRenovacao.Novo();
        var (token2, _) = GeradorTokenRenovacao.Novo();

        Assert.NotEqual(token1, token2);
        Assert.Equal(hash1, GeradorTokenRenovacao.Hash(token1));
        Assert.Equal(44, hash1.Length); // SHA-256 em Base64 (tamanho da coluna no banco)
    }
}
