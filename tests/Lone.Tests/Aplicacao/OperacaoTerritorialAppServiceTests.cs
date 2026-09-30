using Lone.Application.Consultas;
using Lone.Application.Seguranca;
using Lone.Application.Territorios;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Contracts.Territorios;
using Lone.Domain.Enums;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// Regras da camada de aplicação das operações territoriais que não precisam de banco (plano, seção Q, "Aplicação"): a
/// lista fechada de campos da regra (DN-07), o campo financeiro só com a permissão, e PLANEJAR/APLICAR com alcance Tudo
/// (DN-06). O resto (criar, simular, aplicar, cancelar, desfazer) é provado com os serviços de verdade no SQL Server
/// (BancoOperacoesTerritoriaisTests).
/// </summary>
public class OperacaoTerritorialAppServiceTests
{
    private static GruposRegraTerritorioDto Com(params CondicaoFiltro[] condicoes) =>
        new() { Inclusao = [new GrupoCondicoesTerritorioDto { Condicoes = [.. condicoes] }] };

    private static CondicaoFiltro Condicao(string campo, OperadorFiltro operador, params string[] valores) =>
        new() { Campo = campo, Operador = operador, Valores = [.. valores] };

    [Fact]
    public void Campo_fora_da_lista_fechada_e_recusado_na_regra()
    {
        // Relativo a hoje (aniversário), da carteira (vendedor), dado pessoal (sexo) e campo personalizado: fora (DN-07).
        foreach (var c in new[]
                 {
                     Condicao(CamposFiltroPessoas.Aniversario, OperadorFiltro.UmDestes, "5"),
                     Condicao(CamposFiltroPessoas.Vendedor, OperadorFiltro.UmDestes, Guid.NewGuid().ToString()),
                     Condicao(CamposFiltroPessoas.Sexo, OperadorFiltro.UmDestes, "Feminino"),
                     Condicao(CamposFiltroPessoas.CampoPersonalizado(Guid.NewGuid()), OperadorFiltro.NaoVazio)
                 })
            Assert.Contains(TextoRegraTerritorio.Validar(Com(c), _ => true), e => e.Contains("não pode ser usado em regra de território", StringComparison.Ordinal));
    }

    [Fact]
    public void Campos_da_lista_fechada_passam_e_a_regra_precisa_de_inclusao()
    {
        Assert.Empty(TextoRegraTerritorio.Validar(Com(Condicao(CamposFiltroPessoas.Uf, OperadorFiltro.UmDestes, "MG"),
                                                      Condicao(CamposFiltroPessoas.Etiquetas, OperadorFiltro.UmDestes, Guid.NewGuid().ToString())), _ => true));
        Assert.NotEmpty(TextoRegraTerritorio.Validar(new GruposRegraTerritorioDto(), _ => true));
        Assert.NotEmpty(TextoRegraTerritorio.Validar(Com(), _ => true)); // grupo sem condição
        Assert.Equal(17, CatalogoFiltrosPessoas.CamposRegraTerritorio.Count);
    }

    [Fact]
    public void Condicao_de_pagamento_do_cliente_na_regra_exige_ver_o_financeiro()
    {
        var regra = Com(Condicao(CamposFiltroPessoas.CondicaoCliente, OperadorFiltro.UmDestes, Guid.NewGuid().ToString()));
        Assert.Contains(TextoRegraTerritorio.Validar(regra, p => p != Permissoes.Pessoas.VisualizarFinanceiro),
                        e => e.Contains("permissão", StringComparison.Ordinal));
        Assert.Empty(TextoRegraTerritorio.Validar(regra, _ => true));
    }

    [Fact]
    public void Texto_congelado_usa_os_nomes_do_dia()
    {
        var etiqueta = Guid.NewGuid().ToString();
        var texto = TextoRegraTerritorio.Texto(Com(Condicao(CamposFiltroPessoas.Etiquetas, OperadorFiltro.UmDestes, etiqueta)),
            new Dictionary<string, IReadOnlyDictionary<string, string>> { [CamposFiltroPessoas.Etiquetas] = new Dictionary<string, string> { [etiqueta] = "VIP" } });
        Assert.Equal("Entra quem atende a: [Etiquetas: VIP].", texto);
    }

    [Fact]
    public async Task Planejar_e_aplicar_exigem_a_permissao_e_o_alcance_Tudo()
    {
        var acesso = new AcessoFixo { Alcance = AlcanceComercial.Tudo };
        acesso.Negadas.Add(Permissoes.Territorios.Planejar);
        acesso.Negadas.Add(Permissoes.Territorios.Aplicar);
        var servico = Servico(acesso);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => servico.CriarAsync(new CriarOperacaoTerritorialRequisicao()));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => servico.AplicarAsync(Guid.NewGuid(), new AplicarOperacaoTerritorialRequisicao()));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => servico.DesfazerAsync(Guid.NewGuid(), new MotivoOperacaoTerritorialRequisicao()));

        // Com a permissão, mas alcance de equipe: recusado também (a operação mexe no mapa inteiro).
        var equipe = new AcessoFixo { Alcance = AlcanceComercial.MinhaEquipe };
        var comEquipe = Servico(equipe);
        var erro = await Assert.ThrowsAsync<AcessoNegadoException>(() => comEquipe.CriarAsync(new CriarOperacaoTerritorialRequisicao()));
        Assert.Equal(OperacaoTerritorialAppService.MensagemAlcance, erro.Message);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => comEquipe.SimularAsync(Guid.NewGuid(), new VersaoOperacaoTerritorialRequisicao()));
    }

    /// <summary>Só o que a conferência de permissão usa: o resto nunca é chamado antes dela.</summary>
    private static OperacaoTerritorialAppService Servico(AcessoFixo acesso) =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, acesso, new UsuarioFixo(), acesso, null!, null!, null!, TimeProvider.System);

    private sealed class AcessoFixo : IAutorizacao, IAlcanceDoUsuario
    {
        public HashSet<string> Negadas { get; } = new();
        public AlcanceComercial Alcance { get; set; }
        public Guid? PessoaId => null;
        public bool Possui(string permissao) => !Negadas.Contains(permissao);
        public void Exigir(string permissao)
        {
            if (!Possui(permissao)) throw new AcessoNegadoException(permissao);
        }
    }

    private sealed class UsuarioFixo : IUsuarioAtual
    {
        public Guid? Id => null;
        public string Nome => "Teste";
    }
}
