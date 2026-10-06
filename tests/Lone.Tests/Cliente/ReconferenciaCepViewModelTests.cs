using System.Net;
using System.Text.Json;
using Lone.Cliente.Api;
using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Comum;
using Lone.Contracts.Integracoes;
using Lone.Cliente.Mensagens;
using Lone.Domain.Enderecos.ConferenciaCep;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Cliente;

/// <summary>
/// F6, a tela de reconferência: seleção com contagem, blocos pequenos para a API, confirmação para lote grande, contadores e
/// grupos por situação. Candidatos só aparecem para conferir na ficha; nada é recomendado nem aplicado.
/// </summary>
public class ReconferenciaCepViewModelTests
{
    private static async Task<(AmbienteCliente, ReconferenciaCepViewModel)> TelaAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var vm = new ReconferenciaCepViewModel(new ConsultasApi(ambiente.Api), ambiente.Dialogos)
        {
            Mensagens = new ServicoMensagens(new FakeTimeProvider())
        };
        return (ambiente, vm);
    }

    private static SelecaoReconferenciaCepDto Selecao(int quantos, int confirmarAcimaDe = 50) => new()
    {
        Total = quantos, Enderecos = Enumerable.Range(0, quantos).Select(_ => Guid.NewGuid()).ToList(), ConfirmarAcimaDe = confirmarAcimaDe,
        ItensPorChamada = 10, MaximoAdiamentos = 2
    };

    private static ItemReconferenciaCepDto Item(Guid id, ResultadoItemReconferencia r, params string[] candidatos) => new()
    {
        EnderecoId = id, PessoaId = Guid.NewGuid(), PessoaCodigo = 7, PessoaNome = "Cliente", Endereco = "R. Barao, 150 · Curvelo/MG",
        Cep = "35790999", Resultado = r, Detalhe = r.ToString(),
        Componentes = r == ResultadoItemReconferencia.Divergente
            ? [new ComponenteCepDto { Componente = ComponenteCep.Logradouro, Situacao = SituacaoComponenteCep.Divergente, Motivo = "Logradouro diferente." }]
            : [],
        Candidatos = candidatos.Select(c => new CandidatoCepDto { Cep = c }).ToList()
    };

    private static ResumoReconferenciaCepDto Resumo(IEnumerable<Guid> ids, Func<int, ResultadoItemReconferencia> resultado) =>
        new() { Itens = ids.Select((id, i) => Item(id, resultado(i))).ToList() };

    private static List<int> TamanhosDosBlocos(AmbienteCliente a) =>
        a.Servidor.Recebidas.Where(r => r.Caminho.EndsWith("/cep/reconferencia/processar", StringComparison.Ordinal))
            .Select(r => JsonSerializer.Deserialize<ProcessarReconferenciaCepRequisicao>(r.Corpo, OpcoesJson.Padrao)!.Enderecos.Count)
            .ToList();

    [Fact]
    public void Texto_da_selecao_mostra_total_truncamento_e_sem_cep()
    {
        Assert.Equal("Nenhum endereço a reconferir com estes filtros.", ReconferenciaCepViewModel.TextoDaSelecao(new SelecaoReconferenciaCepDto()));
        Assert.Equal("240 endereço(s) a reconferir. 12 sem CEP válido ficaram de fora.",
            ReconferenciaCepViewModel.TextoDaSelecao(new SelecaoReconferenciaCepDto { Total = 240, SemCepValido = 12 }));
        Assert.Equal("1500 endereço(s) a reconferir; nesta rodada, os 2 primeiros (rode de novo para os demais).",
            ReconferenciaCepViewModel.TextoDaSelecao(new SelecaoReconferenciaCepDto { Total = 1500, Enderecos = [Guid.NewGuid(), Guid.NewGuid()], Truncada = true }));
    }

    [Fact]
    public async Task Filtro_vai_como_marcado_com_uf_normalizada()
    {
        var (_, vm) = await TelaAsync();
        vm.IncluirConferidosAntigos = false;
        vm.Uf = " mg ";

        var f = vm.Filtro();

        Assert.Equal((true, true, true, false, "MG"), (f.NaoConferidos, f.Divergentes, f.NaoEncontrados, f.ConferidosAntigos, f.Uf));
    }

    [Fact]
    public async Task Processa_em_blocos_pequenos_conta_e_agrupa_por_situacao()
    {
        var (a, vm) = await TelaAsync();
        var s = Selecao(23);
        a.Servidor.Responder(HttpStatusCode.OK, s);
        var resultados = new[]
        {
            ResultadoItemReconferencia.Conferido, ResultadoItemReconferencia.Divergente, ResultadoItemReconferencia.NaoEncontrado,
            ResultadoItemReconferencia.Indisponivel, ResultadoItemReconferencia.AlteradoDuranteAReconferencia, ResultadoItemReconferencia.Conferido
        };
        for (var i = 0; i < 23; i += 10)
        {
            var inicio = i;
            a.Servidor.Responder(HttpStatusCode.OK, Resumo(s.Enderecos.Skip(i).Take(10), j => resultados[(inicio + j) % resultados.Length]));
        }

        await vm.SelecionarCommand.ExecuteAsync(null);
        await vm.IniciarCommand.ExecuteAsync(null);

        Assert.Empty(a.Dialogos.Perguntas);                 // 23 ≤ 50: sem confirmação
        Assert.Equal([10, 10, 3], TamanhosDosBlocos(a));
        Assert.Equal((23, 23), (vm.Total, vm.Processados));
        Assert.Equal((7, 4, 4, 4, 4, 0), (vm.Conferidos, vm.Divergentes, vm.NaoEncontrados, vm.Indisponiveis, vm.Alterados, vm.NaoProcessados));
        Assert.Equal(1.0, vm.Progresso);
        Assert.False(vm.Executando);
        Assert.Equal(
            ["CEP não corresponde ao endereço (4)", "CEP não encontrado (4)", "Não foi possível consultar agora (4)",
             "Alterados durante a reconferência (4)", "Conferidos (7)"],
            vm.Grupos.Select(g => g.Titulo));
        Assert.Contains("Logradouro diferente.", vm.Grupos[0].Itens[0].Detalhe);
    }

    [Fact]
    public async Task Lote_grande_pede_confirmacao_e_recusar_nao_processa_nada()
    {
        var (a, vm) = await TelaAsync();
        a.Servidor.Responder(HttpStatusCode.OK, Selecao(60));
        a.Dialogos.RespostaConfirmacao = false;

        await vm.SelecionarCommand.ExecuteAsync(null);
        await vm.IniciarCommand.ExecuteAsync(null);

        Assert.Contains("Serão reconferidos 60 endereços", Assert.Single(a.Dialogos.Perguntas));
        Assert.Contains("Nenhum endereço é alterado", a.Dialogos.Perguntas[0]);
        Assert.Empty(TamanhosDosBlocos(a));
        Assert.Equal(0, vm.Processados);
    }

    [Fact]
    public async Task Falha_de_comunicacao_para_no_bloco_e_o_restante_fica_nao_processado()
    {
        var (a, vm) = await TelaAsync();
        var s = Selecao(15);
        a.Servidor.Responder(HttpStatusCode.OK, s)
            .Responder(HttpStatusCode.OK, Resumo(s.Enderecos.Take(10), _ => ResultadoItemReconferencia.Conferido))
            .ForaDoAr();

        await vm.SelecionarCommand.ExecuteAsync(null);
        await vm.IniciarCommand.ExecuteAsync(null);

        Assert.Equal((10, 5, 0), (vm.Conferidos, vm.NaoProcessados, vm.Indisponiveis)); // não executado não vira falha postal
        Assert.Equal(TipoMensagem.Erro, vm.TipoMensagem);
        Assert.False(vm.Executando);
    }

    [Fact]
    public async Task Adiados_voltam_em_outro_bloco_ate_o_maximo_e_depois_ficam_nao_processados()
    {
        var (a, vm) = await TelaAsync();
        var s = Selecao(3);
        var (x, y, z) = (s.Enderecos[0], s.Enderecos[1], s.Enderecos[2]);
        a.Servidor.Responder(HttpStatusCode.OK, s)
            .Responder(HttpStatusCode.OK, new ResumoReconferenciaCepDto
            {
                Itens = [Item(x, ResultadoItemReconferencia.Conferido), Item(y, ResultadoItemReconferencia.Adiado), Item(z, ResultadoItemReconferencia.Adiado)]
            })
            .Responder(HttpStatusCode.OK, new ResumoReconferenciaCepDto
            {
                Itens = [Item(y, ResultadoItemReconferencia.Conferido), Item(z, ResultadoItemReconferencia.Adiado)]
            })
            .Responder(HttpStatusCode.OK, new ResumoReconferenciaCepDto { Itens = [Item(z, ResultadoItemReconferencia.Adiado)] });

        await vm.SelecionarCommand.ExecuteAsync(null);
        await vm.IniciarCommand.ExecuteAsync(null);

        Assert.Equal([3, 2, 1], TamanhosDosBlocos(a)); // reenviados só os adiados; o 3º adiamento passa do máximo (2)
        Assert.Equal((3, 2, 1), (vm.Processados, vm.Conferidos, vm.NaoProcessados));
        Assert.Contains("Rode de novo mais tarde", Assert.Single(vm.Grupos, g => g.Titulo.StartsWith("Não processados")).Itens[0].Detalhe);
    }

    [Fact]
    public async Task Ao_terminar_registra_uma_execucao_com_filtro_e_quantidades_sem_lista_de_enderecos()
    {
        var (a, vm) = await TelaAsync();
        vm.IncluirConferidosAntigos = false;
        vm.Uf = "mg";
        var s = Selecao(3);
        a.Servidor.Responder(HttpStatusCode.OK, s)
            .Responder(HttpStatusCode.OK, Resumo(s.Enderecos, i => i == 0 ? ResultadoItemReconferencia.Divergente : ResultadoItemReconferencia.Conferido))
            .Responder(HttpStatusCode.NoContent);

        await vm.SelecionarCommand.ExecuteAsync(null);
        await vm.IniciarCommand.ExecuteAsync(null);

        var chamada = a.Servidor.Recebidas.Last();
        Assert.EndsWith("/cep/reconferencia/concluir", chamada.Caminho);
        var c = JsonSerializer.Deserialize<ConcluirReconferenciaCepRequisicao>(chamada.Corpo, OpcoesJson.Padrao)!;
        Assert.NotEqual(Guid.Empty, c.ExecucaoId);
        Assert.Equal((false, 3, 2, 1, 0), (c.Cancelada, c.Total, c.Conferidos, c.Divergentes, c.NaoProcessados));
        Assert.Equal(("MG", false), (c.Filtro.Uf, c.Filtro.ConferidosAntigos));
        foreach (var id in s.Enderecos) Assert.DoesNotContain(id.ToString(), chamada.Corpo); // nenhum endereço vai no evento
    }

    [Fact]
    public async Task Candidatos_aparecem_so_para_conferir_na_ficha_sem_recomendacao()
    {
        var linha = LinhaReconferenciaCep.De(Item(Guid.NewGuid(), ResultadoItemReconferencia.NaoEncontrado, "35790001", "35790002"));

        Assert.Equal("7 · Cliente — R. Barao, 150 · Curvelo/MG · CEP 35790-999", linha.Titulo);
        Assert.Contains("CEPs compatíveis (para conferir na ficha): 35790-001, 35790-002.", linha.Detalhe);
        Assert.DoesNotContain("recomend", linha.Detalhe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aplicad", linha.Detalhe, StringComparison.OrdinalIgnoreCase);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Selecao_vazia_avisa_e_nao_chama_o_processamento()
    {
        var (a, vm) = await TelaAsync();
        a.Servidor.Responder(HttpStatusCode.OK, new SelecaoReconferenciaCepDto { ConfirmarAcimaDe = 50, ItensPorChamada = 10 });

        await vm.IniciarCommand.ExecuteAsync(null);

        Assert.Empty(TamanhosDosBlocos(a));
        Assert.Equal("Nenhum endereço a reconferir com estes filtros.", vm.Mensagem);
    }

    [Fact]
    public async Task Limpar_historico_pede_confirmacao_e_recusar_nao_chama_a_api()
    {
        var (a, vm) = await TelaAsync();
        a.Dialogos.RespostaConfirmacao = false;

        await vm.LimparHistoricoCommand.ExecuteAsync(null);

        Assert.Contains("mais de 90 dias", Assert.Single(a.Dialogos.Perguntas));
        Assert.Empty(a.Servidor.Recebidas);
    }

    [Fact]
    public async Task Limpar_historico_confirmado_chama_a_rota_de_manutencao()
    {
        var (a, vm) = await TelaAsync();
        a.Servidor.Responder(HttpStatusCode.OK, new LimpezaHistoricoCepDto { Removidos = 3, RetencaoDias = 90 });

        await vm.LimparHistoricoCommand.ExecuteAsync(null);

        Assert.EndsWith("/cep/historico/limpar", Assert.Single(a.Servidor.Recebidas).Caminho);
    }
}
