using Lone.Api.Erros;
using Lone.Application.Integracoes;
using Lone.Application.Seguranca;
using Lone.Contracts.Comum;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Validacao;

namespace Lone.Tests.Api;

public class ProblemasTests
{
    [Fact]
    public void Regra_de_negocio_vira_400_com_a_lista_de_erros()
    {
        var problema = Problemas.De(new ValidacaoException(["Informe o nome.", "CPF inválido."]))!;

        Assert.Equal(400, problema.Status);
        Assert.Equal(ErrosApi.Validacao, problema.Extensions[ErrosApi.CampoCodigo]);
        Assert.Equal(new[] { "Informe o nome.", "CPF inválido." }, (IReadOnlyList<string>)problema.Extensions[ErrosApi.CampoErros]!);
    }

    [Fact]
    public void Falta_de_permissao_vira_403_com_o_codigo_da_permissao()
    {
        var problema = Problemas.De(new AcessoNegadoException(Permissoes.Pessoas.Editar))!;

        Assert.Equal(403, problema.Status);
        Assert.Equal(Permissoes.Pessoas.Editar, problema.Extensions[ErrosApi.CampoPermissao]);
        Assert.Contains("Alterar cadastros", problema.Detail);
    }

    [Fact]
    public void Conflito_de_edicao_vira_409()
    {
        var problema = Problemas.De(new ConflitoDeEdicaoException())!;

        Assert.Equal(409, problema.Status);
        Assert.Equal(ErrosApi.Conflito, problema.Extensions[ErrosApi.CampoCodigo]);
    }

    [Fact]
    public void Sessao_invalida_vira_401()
    {
        var problema = Problemas.De(new SessaoInvalidaException())!;

        Assert.Equal(401, problema.Status);
        Assert.Equal(ErrosApi.NaoAutenticado, problema.Extensions[ErrosApi.CampoCodigo]);
    }

    [Fact]
    public void Servico_externo_fora_do_ar_vira_502() =>
        Assert.Equal(502, Problemas.De(new ServicoExternoException("BrasilAPI fora do ar"))!.Status);

    [Fact]
    public void Erro_inesperado_nao_e_traduzido() =>
        Assert.Null(Problemas.De(new InvalidOperationException("detalhe técnico")));
}
