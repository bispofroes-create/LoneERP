using System.Net;
using Lone.Cliente.Api;
using Lone.Cliente.Grade;
using Lone.Cliente.Navegacao;
using Lone.Cliente.ViewModels.Territorios;
using Lone.Contracts.Territorios;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>Divergências territoriais no padrão de tela de consulta (04/10/2026): contador, atalhos, lista vazia e exportação.</summary>
public class DivergenciasTerritoriaisTests
{
    private static readonly Guid Mapa = Guid.NewGuid();
    private static readonly Guid Centro = Guid.NewGuid();
    private static readonly Guid Norte = Guid.NewGuid();

    private static ItemOperacaoTerritorialDto Item(string pessoa, EfeitoNoCliente efeito, string? atual, string? certo, long codigo) => new()
    {
        PessoaId = Guid.NewGuid(), Codigo = codigo, Pessoa = pessoa, Efeito = efeito, EfeitoNome = efeito.ToString(),
        ResultadoNome = "Atribuído", TerritorioAtual = atual, TerritorioProposto = certo, Motivo = "CEP"
    };

    private static DivergenciasTerritoriaisDto Divergencias(params ItemOperacaoTerritorialDto[] itens) => new()
    {
        MapaId = Mapa, Data = new DateOnly(2026, 10, 4),
        Entram = itens.Count(i => i.Efeito == EfeitoNoCliente.Entra), Saem = itens.Count(i => i.Efeito == EfeitoNoCliente.Sai),
        Mudam = itens.Count(i => i.Efeito == EfeitoNoCliente.Muda), Inconsistencias = itens.Count(i => i.Efeito == EfeitoNoCliente.Bloqueado),
        Itens = new PaginaItensOperacaoTerritorialDto { Total = itens.Length, Itens = [.. itens] }
    };

    private static async Task<(DivergenciasTerritoriaisViewModel Tela, AmbienteCliente Ambiente)> AbrirAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        ambiente.Servidor.Responder(HttpStatusCode.OK, new OperacoesTerritoriaisOpcoesDto
        {
            PodePlanejar = true, Mapas = [new MapaTerritorialDto { Id = Mapa, Nome = "Vendas", Ativo = true }]
        });
        var tela = new DivergenciasTerritoriaisViewModel(new TerritoriosApi(ambiente.Api), new AberturaDeOperacaoTerritorial(),
            ambiente.Dialogos, ambiente.Arquivos);
        await tela.CarregarCommand.ExecuteAsync(null);
        return (tela, ambiente);
    }

    private static void ResponderConferencia(AmbienteCliente ambiente, DivergenciasTerritoriaisDto d)
    {
        ambiente.Servidor.Responder(HttpStatusCode.OK, new ArvoreTerritorialDto());
        ambiente.Servidor.Responder(HttpStatusCode.OK, d);
    }

    [Fact]
    public void Titulo_conta_e_selo_tem_a_cor_do_efeito()
    {
        Assert.Equal("Divergências", DivergenciasTerritoriais.TituloLista(null, 0));
        Assert.Equal("Divergências em 04/10/2026 (1.240)", DivergenciasTerritoriais.TituloLista(new DateOnly(2026, 10, 4), 1240));
        Assert.Equal("Erro", DivergenciasTerritoriais.Tom(EfeitoNoCliente.Bloqueado));
        Assert.Equal("Sucesso", DivergenciasTerritoriais.Tom(EfeitoNoCliente.Entra));
        Assert.Equal("Sem território", DivergenciasTerritoriais.Territorio(null));
    }

    [Fact]
    public async Task Antes_de_conferir_a_lista_vazia_diz_o_que_fazer_e_nao_ha_atalhos_nem_exportar()
    {
        var (tela, _) = await AbrirAsync();

        Assert.True(tela.ListaVazia);
        Assert.Equal(DivergenciasTerritoriais.TituloAntesDeConferir, tela.TituloVazio);
        Assert.False(tela.MostrarAtalhos);
        Assert.False(tela.MostrarExportar);
        Assert.False(tela.PodeCriarOperacao);
        Assert.Equal("Divergências", tela.TituloLista);
    }

    [Fact]
    public async Task Conferir_mostra_contador_atalhos_com_contagem_e_a_acao_principal()
    {
        var (tela, ambiente) = await AbrirAsync();
        ResponderConferencia(ambiente, Divergencias(
            Item("Maria", EfeitoNoCliente.Muda, "Centro", "Norte", 123),
            Item("Bruno", EfeitoNoCliente.Entra, null, "Norte", 7),
            Item("Ana", EfeitoNoCliente.Muda, "Norte", "Centro", 9)));

        await tela.ConferirCommand.ExecuteAsync(null);

        Assert.Equal("Divergências em 04/10/2026 (3)", tela.TituloLista);
        Assert.True(tela.MostrarAtalhos);
        Assert.Equal(3, tela.Atalhos[0].Quantidade);
        Assert.Equal(2, tela.Atalhos.Single(a => a.Efeito == EfeitoNoCliente.Muda).Quantidade);
        Assert.False(tela.Atalhos.Single(a => a.Efeito == EfeitoNoCliente.Sai).Visivel); // 0: some
        Assert.True(tela.PodeCriarOperacao);
        Assert.True(tela.MostrarExportar);
        Assert.False(tela.TemMais);
        Assert.Equal(3, tela.ConteudoLista.Linhas.Count);
    }

    [Fact]
    public async Task Atalho_pede_so_aquele_efeito_a_API_e_tocar_de_novo_volta_para_todas()
    {
        var (tela, ambiente) = await AbrirAsync();
        var todas = Divergencias(Item("Maria", EfeitoNoCliente.Muda, "Centro", "Norte", 1), Item("Bruno", EfeitoNoCliente.Entra, null, "Norte", 2));
        ResponderConferencia(ambiente, todas);
        await tela.ConferirCommand.ExecuteAsync(null);

        var muda = tela.Atalhos.Single(a => a.Efeito == EfeitoNoCliente.Muda);
        var soMuda = Divergencias(Item("Maria", EfeitoNoCliente.Muda, "Centro", "Norte", 1));
        soMuda.Entram = 1; // a API conta o mapa todo
        soMuda.Itens.Total = 1;
        ResponderConferencia(ambiente, soMuda);
        await tela.SelecionarEfeitoCommand.ExecuteAsync(muda);

        Assert.Contains("\"efeito\":\"Muda\"", ambiente.Servidor.Recebidas[^1].Corpo, StringComparison.OrdinalIgnoreCase);
        Assert.True(muda.Selecionado);
        Assert.Equal(2, tela.Atalhos[0].Quantidade); // "Todas" continua com o total sem atalho
        Assert.Equal("Divergências em 04/10/2026 (1)", tela.TituloLista);

        ResponderConferencia(ambiente, todas);
        await tela.SelecionarEfeitoCommand.ExecuteAsync(muda);
        Assert.True(tela.Atalhos[0].Selecionado);
        Assert.DoesNotContain("\"efeito\":\"Muda\"", ambiente.Servidor.Recebidas[^1].Corpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sem_divergencia_a_lista_vazia_e_a_boa_noticia_sem_botao()
    {
        var (tela, ambiente) = await AbrirAsync();
        ResponderConferencia(ambiente, Divergencias());

        await tela.ConferirCommand.ExecuteAsync(null);

        Assert.True(tela.ListaVazia);
        Assert.Equal("Nenhuma divergência no mapa Vendas", tela.TituloVazio);
        Assert.False(tela.MostrarAcaoVazio);
        Assert.False(tela.MostrarAtalhos);
        Assert.False(tela.PodeCriarOperacao);
    }

    [Fact]
    public async Task Tocar_na_linha_mostra_o_por_que_e_tocar_de_novo_fecha()
    {
        var (tela, ambiente) = await AbrirAsync();
        ResponderConferencia(ambiente, Divergencias(Item("Maria", EfeitoNoCliente.Muda, "Centro", "Norte", 1)));
        await tela.ConferirCommand.ExecuteAsync(null);

        var linha = tela.ConteudoLista.Linhas[0];
        tela.AbrirLinhaCommand.Execute(linha);
        Assert.True(tela.TemSelecionado);
        Assert.Equal("000001 · Maria", tela.Selecionado!.Titulo); // código com 6 dígitos, como em Pessoas
        Assert.True(((LinhaCadastro)tela.ConteudoLista.Linhas[0]).Selecionada);

        tela.AbrirLinhaCommand.Execute(tela.ConteudoLista.Linhas[0]);
        Assert.False(tela.TemSelecionado);
    }

    [Fact]
    public async Task Inconsistencia_vira_aviso_e_trocar_de_mapa_limpa_o_conferido()
    {
        var (tela, ambiente) = await AbrirAsync();
        ResponderConferencia(ambiente, Divergencias(Item("Maria", EfeitoNoCliente.Bloqueado, "Centro", "Norte", 1)));
        await tela.ConferirCommand.ExecuteAsync(null);

        Assert.Contains("1 cliente com inconsistência", tela.Mensagem);

        tela.Mapa = null;
        Assert.True(tela.ListaVazia);
        Assert.False(tela.MostrarAtalhos);
        Assert.Equal("Divergências", tela.TituloLista);
    }

    [Fact]
    public async Task Exportar_csv_tem_as_colunas_e_respeita_a_ordem_da_lista()
    {
        var (tela, ambiente) = await AbrirAsync();
        ResponderConferencia(ambiente, Divergencias(
            Item("Maria; filial", EfeitoNoCliente.Muda, "Centro", "Norte", 2),
            Item("Ana", EfeitoNoCliente.Entra, null, "Norte", 1)));
        await tela.ConferirCommand.ExecuteAsync(null);
        tela.GradeDaLista.OrdenarColunaCommand.Execute(tela.GradeDaLista.ColunaFixa); // por nome

        await tela.ExportarCsvCommand.ExecuteAsync(null);

        var csv = System.Text.Encoding.UTF8.GetString(ambiente.Arquivos.Abertos.Single().Conteudo).TrimStart('﻿');
        var linhas = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Código;Cliente;Efeito;Território atual;Território certo;Resultado;Motivo;Por quê", linhas[0]);
        Assert.StartsWith("000001;Ana;Entra;Sem território;Norte;", linhas[1]);
        Assert.StartsWith("000002;\"Maria; filial\";Muda;Centro;Norte;", linhas[2]);
    }
}
