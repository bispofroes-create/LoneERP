using Lone.Cliente.Mensagens;
using Lone.Cliente.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Cliente;

/// <summary>
/// Fase 1 da arquitetura global de mensagens: o sucesso sai da barra da tela e vai para o serviço global (toast), com
/// duração, repetição, fila, prioridade e ação decididas no serviço; erro, aviso e informação continuam como estavam.
/// </summary>
public class MensagensTests
{
    private static (ServicoMensagens Servico, FakeTimeProvider Relogio) Novo()
    {
        var relogio = new FakeTimeProvider();
        return (new ServicoMensagens(relogio), relogio);
    }

    /// <summary>Uma tela qualquer: só usa o Mostrar de sempre.</summary>
    private sealed class Tela : ViewModelBase
    {
        public void Avisar(string texto, TipoMensagem tipo) => Mostrar(texto, tipo);
        public void AvisarComAcao(string texto, TipoMensagem tipo, AcaoMensagem acao) => Mostrar(texto, tipo, acao);
    }

    // ---- 27.1 Sucesso: chega ao serviço, sem depender da barra nem da rolagem ----

    [Fact]
    public void Sucesso_pelo_Mostrar_de_sempre_vira_toast_e_nao_ocupa_a_barra_nem_rola_a_tela()
    {
        var (servico, _) = Novo();
        var tela = new Tela { Mensagens = servico };
        var rolagens = 0;
        tela.MensagemMostrada += (_, _) => rolagens++;

        tela.Avisar("Alterações salvas", TipoMensagem.Sucesso);

        var toast = Assert.Single(servico.Visiveis);
        Assert.Equal("Alterações salvas", toast.Texto);
        Assert.Equal(TipoMensagem.Sucesso, toast.Tipo);
        Assert.Equal(ApresentacaoMensagem.Toast, toast.Apresentacao);
        Assert.Equal(PrioridadeMensagem.Normal, toast.Prioridade);
        Assert.False(tela.TemMensagem); // a barra no topo da tela não recebe nada
        Assert.Equal(0, rolagens);      // nenhuma tela é levada ao topo para mostrar sucesso
    }

    [Fact]
    public void Sucesso_tira_da_barra_o_erro_anterior_como_antes()
    {
        var (servico, _) = Novo();
        var tela = new Tela { Mensagens = servico };
        tela.Avisar("Informe o nome.", TipoMensagem.Erro);

        tela.Avisar("Alterações salvas", TipoMensagem.Sucesso);

        Assert.False(tela.TemMensagem);
        Assert.Single(servico.Visiveis);
    }

    [Theory]
    [InlineData(TipoMensagem.Erro)]
    [InlineData(TipoMensagem.Aviso)]
    [InlineData(TipoMensagem.Informacao)]
    public void Erro_aviso_e_informacao_continuam_na_barra_e_nao_viram_toast(TipoMensagem tipo)
    {
        var (servico, _) = Novo();
        var tela = new Tela { Mensagens = servico };
        var rolagens = 0;
        tela.MensagemMostrada += (_, _) => rolagens++;

        tela.Avisar("Salve ou descarte as alterações antes de desativar.", tipo);

        Assert.Empty(servico.Visiveis);
        Assert.Equal("Salve ou descarte as alterações antes de desativar.", tela.Mensagem);
        Assert.Equal(tipo, tela.TipoMensagem);
        Assert.Equal(1, rolagens); // o paliativo de Operações territoriais continua valendo para a barra
        Assert.Null(ServicoMensagens.Classificar(tipo));
        Assert.Throws<ArgumentException>(() => servico.Publicar("x", tipo));
    }

    [Fact]
    public void Acao_so_existe_no_toast()
    {
        var tela = new Tela { Mensagens = Novo().Servico };
        Assert.Throws<InvalidOperationException>(() =>
            tela.AvisarComAcao("Algo", TipoMensagem.Erro, new AcaoMensagem("Abrir", () => Task.CompletedTask)));
    }

    // ---- Texto: confirmação com o nome do registro ----

    [Fact]
    public void Confirmacao_leva_o_nome_do_registro_abreviado_se_for_longo()
    {
        Assert.Equal("Pessoa salva: Bruno", TextosMensagem.ComNome("Pessoa salva", "  Bruno "));
        Assert.Equal("Pessoa salva", TextosMensagem.ComNome("Pessoa salva", null));
        Assert.Equal("Pessoa salva", TextosMensagem.ComNome("Pessoa salva", "   "));

        var longo = "Comércio Atacadista de Materiais de Construção Curvelo Ltda";
        var texto = TextosMensagem.ComNome("Pessoa criada", longo);
        Assert.StartsWith("Pessoa criada: Comércio Atacadista", texto);
        Assert.EndsWith("…", texto);
        Assert.Equal(TextosMensagem.TamanhoMaximoNome, texto.Length - "Pessoa criada: ".Length);

        var exato = new string('a', TextosMensagem.TamanhoMaximoNome);
        Assert.Equal("Pessoa salva: " + exato, TextosMensagem.ComNome("Pessoa salva", exato)); // no limite: inteiro
    }

    // ---- Duração: calculada pelo serviço ----

    [Fact]
    public void Duracao_cresce_com_o_texto_entre_o_minimo_e_o_teto_e_com_acao_da_tempo_de_decidir()
    {
        Assert.Equal(TimeSpan.FromSeconds(4), ServicoMensagens.DuracaoPara("Pessoa salva", temAcao: false));
        Assert.Equal(TimeSpan.FromSeconds(9), ServicoMensagens.DuracaoPara(new string('a', 100), temAcao: false));
        Assert.Equal(TimeSpan.FromSeconds(12), ServicoMensagens.DuracaoPara(new string('a', 400), temAcao: false));
        Assert.Equal(TimeSpan.FromSeconds(8), ServicoMensagens.DuracaoPara("Pessoa salva", temAcao: true));
        Assert.Equal(TimeSpan.FromSeconds(9), ServicoMensagens.DuracaoPara(new string('a', 100), temAcao: true));
    }

    [Fact]
    public void Toast_some_sozinho_quando_acaba_o_tempo()
    {
        var (servico, relogio) = Novo();
        var mudou = 0;
        servico.Mudou += (_, _) => mudou++;
        var toast = servico.Publicar("Pessoa salva");
        Assert.Equal(TimeSpan.FromSeconds(4), toast.Duracao);

        relogio.Advance(TimeSpan.FromSeconds(3.9));
        Assert.Single(servico.Visiveis);

        relogio.Advance(TimeSpan.FromSeconds(0.1));
        Assert.Empty(servico.Visiveis);
        Assert.Equal(2, mudou); // entrou e saiu
    }

    [Fact]
    public void Ponteiro_ou_foco_sobre_o_toast_seguram_o_tempo()
    {
        var (servico, relogio) = Novo();
        var toast = servico.Publicar("Pessoa salva");
        relogio.Advance(TimeSpan.FromSeconds(3));

        servico.Pausar(toast.Id);
        relogio.Advance(TimeSpan.FromMinutes(1));
        Assert.Single(servico.Visiveis);

        servico.Retomar(toast.Id); // faltava 1 s: volta com o mínimo de 2 s
        relogio.Advance(TimeSpan.FromSeconds(1.9));
        Assert.Single(servico.Visiveis);
        relogio.Advance(TimeSpan.FromSeconds(0.1));
        Assert.Empty(servico.Visiveis);
    }

    // ---- 27.3 Repetidas ----

    [Fact]
    public void Mesma_mensagem_repetida_nao_empilha_soma_e_recomeca_o_tempo()
    {
        var (servico, relogio) = Novo();
        var anuncios = 0;
        servico.Publicada += (_, _) => anuncios++;

        var primeira = servico.Publicar("Alterações salvas");
        relogio.Advance(TimeSpan.FromSeconds(3));
        var segunda = servico.Publicar("Alterações salvas");
        servico.Publicar("Alterações salvas");

        Assert.Same(primeira, segunda);
        Assert.Equal(3, Assert.Single(servico.Visiveis).Contagem);
        Assert.Equal(3, anuncios); // o leitor de tela ouve de novo: a ação aconteceu de novo

        relogio.Advance(TimeSpan.FromSeconds(3)); // 6 s desde a primeira, 3 s desde a última
        Assert.Single(servico.Visiveis);
        relogio.Advance(TimeSpan.FromSeconds(1));
        Assert.Empty(servico.Visiveis);
    }

    [Fact]
    public void Mesmo_texto_de_outro_contexto_ou_depois_de_sumir_e_outra_mensagem()
    {
        var (servico, relogio) = Novo();
        servico.Publicar("Alterações salvas", contexto: "pessoa:1");
        servico.Publicar("Alterações salvas", contexto: "pessoa:2");
        Assert.Equal(2, servico.Visiveis.Count);

        relogio.Advance(TimeSpan.FromSeconds(4));
        servico.Publicar("Alterações salvas", contexto: "pessoa:1");
        Assert.Equal(1, Assert.Single(servico.Visiveis).Contagem);
    }

    // ---- 27.4 Várias mensagens: no máximo 3 na tela ----

    [Fact]
    public void Mais_de_tres_esperam_a_vez_na_ordem_de_chegada()
    {
        var (servico, _) = Novo();
        var a = servico.Publicar("A");
        servico.Publicar("B");
        servico.Publicar("C");
        servico.Publicar("D");
        servico.Publicar("E");

        Assert.Equal(new[] { "A", "B", "C" }, servico.Visiveis.Select(m => m.Texto));
        Assert.Equal(new[] { "D", "E" }, servico.EmEspera.Select(m => m.Texto));

        Assert.True(servico.Dispensar(a.Id));
        Assert.Equal(new[] { "B", "C", "D" }, servico.Visiveis.Select(m => m.Texto));
        Assert.False(servico.Dispensar(a.Id));
    }

    [Fact]
    public void Repetida_que_esta_esperando_tambem_soma()
    {
        var (servico, _) = Novo();
        foreach (var t in new[] { "A", "B", "C", "D", "D" }) servico.Publicar(t);

        var d = Assert.Single(servico.EmEspera);
        Assert.Equal(2, d.Contagem);
    }

    [Fact]
    public void Prioridade_maior_passa_a_frente_e_toma_o_lugar_da_menor_visivel()
    {
        var (servico, _) = Novo();
        servico.Publicar("A", prioridade: PrioridadeMensagem.Baixa);
        servico.Publicar("B");
        servico.Publicar("C");

        // Tela cheia com uma Baixa: a Normal que chega toma o lugar dela (que volta a esperar).
        servico.Publicar("D");
        Assert.Equal(new[] { "B", "C", "D" }, servico.Visiveis.Select(m => m.Texto));
        Assert.Equal(new[] { "A" }, servico.EmEspera.Select(m => m.Texto));

        // Alta: toma o lugar da Normal mais antiga na tela; ela volta a esperar à frente das de prioridade menor.
        servico.Publicar("Urgente", prioridade: PrioridadeMensagem.Alta);
        Assert.Equal(new[] { "C", "D", "Urgente" }, servico.Visiveis.Select(m => m.Texto));
        Assert.Equal(new[] { "B", "A" }, servico.EmEspera.Select(m => m.Texto));

        // Normal com a tela só de Normal/Alta: espera, depois das Normais e à frente das Baixas.
        servico.Publicar("Normal 2");
        Assert.Equal(new[] { "C", "D", "Urgente" }, servico.Visiveis.Select(m => m.Texto));
        Assert.Equal(new[] { "B", "Normal 2", "A" }, servico.EmEspera.Select(m => m.Texto));

        // Outra Alta: sai a Normal mais antiga na tela (C), que volta à frente das Normais que esperam.
        servico.Publicar("Alta 2", prioridade: PrioridadeMensagem.Alta);
        Assert.Equal(new[] { "D", "Urgente", "Alta 2" }, servico.Visiveis.Select(m => m.Texto));
        Assert.Equal(new[] { "C", "B", "Normal 2", "A" }, servico.EmEspera.Select(m => m.Texto));

        // Vaga aberta: entra a primeira da fila.
        Assert.True(servico.Dispensar(servico.Visiveis[0].Id));
        Assert.Equal(new[] { "Urgente", "Alta 2", "C" }, servico.Visiveis.Select(m => m.Texto));
    }

    [Fact]
    public void Fila_de_espera_tem_limite_e_perde_a_menor_prioridade_mais_antiga()
    {
        var (servico, _) = Novo();
        for (var i = 0; i < ServicoMensagens.MaximoVisiveis; i++) servico.Publicar($"Visível {i}");
        servico.Publicar("Baixa", prioridade: PrioridadeMensagem.Baixa);
        for (var i = 0; i < ServicoMensagens.MaximoEmEspera; i++) servico.Publicar($"Espera {i}");

        Assert.Equal(ServicoMensagens.MaximoEmEspera, servico.EmEspera.Count);
        Assert.DoesNotContain(servico.EmEspera, m => m.Texto == "Baixa");
    }

    // ---- 27.5 Ação ----

    [Fact]
    public async Task Acao_executa_uma_vez_e_tira_o_toast_da_tela()
    {
        var (servico, _) = Novo();
        var abertas = 0;
        var tela = new Tela { Mensagens = servico };
        tela.AvisarComAcao("Relacionamento registrado", TipoMensagem.Sucesso,
            new AcaoMensagem("Abrir", () => { abertas++; return Task.CompletedTask; }, "Abrir a ficha de Ana"));
        var toast = Assert.Single(servico.Visiveis);
        Assert.Equal("Abrir", toast.Acao!.Texto);
        Assert.Equal(TimeSpan.FromSeconds(8), toast.Duracao);

        Assert.True(await servico.ExecutarAcaoAsync(toast.Id));
        Assert.False(await servico.ExecutarAcaoAsync(toast.Id)); // já saiu da tela

        Assert.Equal(1, abertas);
        Assert.Empty(servico.Visiveis);
    }

    [Fact]
    public async Task Acao_que_falha_nao_derruba_nada()
    {
        var (servico, _) = Novo();
        var toast = servico.Publicar("Relacionamento registrado",
            acao: new AcaoMensagem("Abrir", () => throw new InvalidOperationException("tela fechada")));

        Assert.False(await servico.ExecutarAcaoAsync(toast.Id));
        Assert.Empty(servico.Visiveis);
    }

    [Fact]
    public async Task Toast_sem_acao_nao_executa_nada()
    {
        var (servico, _) = Novo();
        var toast = servico.Publicar("Pessoa salva");
        Assert.False(await servico.ExecutarAcaoAsync(toast.Id));
        Assert.Single(servico.Visiveis);
    }

    // ---- 27.2 Navegação: o toast não pertence à tela que o publicou ----

    [Fact]
    public void Toast_continua_depois_que_a_tela_que_publicou_sai_e_outra_entra()
    {
        var (servico, relogio) = Novo();
        var origem = new Tela { Mensagens = servico };
        origem.Avisar("Operação criada", TipoMensagem.Sucesso);
        // ...navegou: a tela de origem saiu.

        var destino = new Tela { Mensagens = servico }; // a tela nova lê o mesmo serviço (a camada dela desenha o que há)
        relogio.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal("Operação criada", Assert.Single(destino.Mensagens.Visiveis).Texto);
        Assert.False(destino.TemMensagem);
    }

    [Fact]
    public void Troca_de_contexto_limpa_tudo()
    {
        var (servico, _) = Novo();
        var mudou = 0;
        foreach (var t in new[] { "A", "B", "C", "D" }) servico.Publicar(t);
        servico.Mudou += (_, _) => mudou++;

        servico.Limpar();
        servico.Limpar(); // já vazio: não avisa de novo

        Assert.Empty(servico.Visiveis);
        Assert.Empty(servico.EmEspera);
        Assert.Equal(1, mudou);
    }

    [Fact]
    public void Telas_usam_o_servico_unico_do_aplicativo_por_padrao()
    {
        Assert.Same(ServicoMensagens.Padrao, new Tela().Mensagens);
    }

    // ---- 27.6 / 27.7 Posição: computador e celular ----

    [Fact]
    public void Computador_canto_inferior_direito_com_400_de_largura_acima_da_barra_de_acoes()
    {
        var p = PosicaoToast.Calcular(1366, celular: false);
        Assert.Equal(400, p.Largura);
        Assert.True(p.AlinhadaADireita);
        Assert.Equal(24, p.MargemDireita);
        Assert.Equal(80, p.MargemInferior);
    }

    [Fact]
    public void Janela_estreita_usa_a_largura_disponivel_sem_passar_da_borda()
    {
        // Área estreita (menos que 400 + 2×24): margens de 16, para o texto não quebrar palavras no meio.
        var p = PosicaoToast.Calcular(360, celular: false); // largura mínima da janela do Lone
        Assert.Equal(328, p.Largura);
        Assert.Equal(16, p.MargemDireita);
        Assert.True(p.AlinhadaADireita);
        Assert.Equal(400, PosicaoToast.Calcular(448, celular: false).Largura); // limite: volta às margens de 24
        Assert.Equal(24, PosicaoToast.Calcular(448, celular: false).MargemDireita);
        Assert.Equal(400, PosicaoToast.Calcular(440, celular: false).Largura);  // 440 − 2×16 = 408 → teto 400
        Assert.Equal(16, PosicaoToast.Calcular(440, celular: false).MargemDireita);
        Assert.Equal(0, PosicaoToast.Calcular(20, celular: false).Largura);
    }

    [Fact]
    public void Celular_embaixo_na_largura_toda_com_margem()
    {
        var p = PosicaoToast.Calcular(412, celular: true);
        Assert.Null(p.Largura); // ocupa a largura disponível
        Assert.False(p.AlinhadaADireita);
        Assert.Equal(16, p.MargemEsquerda);
        Assert.Equal(16, p.MargemDireita);
        Assert.Equal(80, p.MargemInferior);
    }
}
