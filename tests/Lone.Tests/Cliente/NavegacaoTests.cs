using System.Net;
using Lone.Cliente.Mensagens;
using Lone.Cliente.Navegacao;
using Lone.Contracts.Pessoas;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Cliente;

/// <summary>
/// Motor global de navegação (Fase 1): histórico real ("de onde eu vim"), Voltar, registros dentro da tela, links,
/// duplo clique, permissões, ciclo de vida e alterações não salvas. Plataforma e telas falsas: nada de MAUI.
/// </summary>
public class NavegacaoTests
{
    private static readonly ReferenciaRegistro Joao = new("pessoa", Guid.NewGuid(), "João da Silva");
    private static readonly ReferenciaRegistro Maria = new("pessoa", Guid.NewGuid(), "Maria Souza");

    /// <summary>Tela que abre e fecha registros como a base dos cadastros faz (e informa o motor).</summary>
    private sealed class TelaFalsa(GerenciadorNavegacao motor) : ITelaNavegavel
    {
        public ReferenciaRegistro? RegistroAberto { get; set; }

        /// <summary>Simula "alterações não salvas" em que o usuário escolhe continuar editando.</summary>
        public bool UsuarioFica { get; set; }

        /// <summary>Simula um registro que não abre mais (excluído, sem acesso): a tela fica na lista.</summary>
        public bool NaoAbre { get; set; }

        public int Pedidos { get; private set; }

        public Task<bool> IrParaRegistroAsync(ReferenciaRegistro? registro)
        {
            Pedidos++;
            if (UsuarioFica) return Task.FromResult(false);
            if (NaoAbre && registro is not null) return Task.FromResult(false);
            RegistroAberto = registro is { Titulo.Length: 0 } r ? r with { Titulo = "Carregado" } : registro;
            motor.InformarRegistro(this);
            return Task.FromResult(true);
        }

        public void Abrir(ReferenciaRegistro registro, OrigemNavegacao origem = OrigemNavegacao.Lista)
        {
            RegistroAberto = registro;
            motor.InformarRegistro(this, origem);
        }

        public void Fechar()
        {
            RegistroAberto = null;
            motor.InformarRegistro(this);
        }
    }

    /// <summary>Shell falso: rotas, a tela de cada rota, e a "guarda" de alterações não salvas do Shell.</summary>
    private sealed class PlataformaFalsa(GerenciadorNavegacao motor) : IPlataformaNavegacao
    {
        public Dictionary<string, TelaFalsa> Telas { get; } = new();
        public string? RotaAtual { get; set; }
        public ITelaNavegavel? TelaAtual => RotaAtual is { } r && Telas.TryGetValue(r, out var t) ? t : null;
        public bool ShellCancela { get; set; }
        public TaskCompletionSource? Segurar { get; set; }
        public List<string> Pedidos { get; } = new();
        public Action? DuranteANavegacao { get; set; }

        public async Task IrParaRotaAsync(string rota)
        {
            Pedidos.Add(rota);
            DuranteANavegacao?.Invoke();
            if (Segurar is { } s) await s.Task;
            if (ShellCancela) return;
            RotaAtual = rota;
            motor.AoChegarNaTela(); // o Shell avisa (o motor ignora durante a navegação dele)
        }
    }

    private static (GerenciadorNavegacao Motor, PlataformaFalsa Shell, TelaFalsa Pessoas, TelaFalsa Etiquetas) Novo()
    {
        // O relógio anda 1 s a cada leitura: voltas seguidas no teste são intencionais (não um duplo clique).
        var motor = new GerenciadorNavegacao(new FakeTimeProvider { AutoAdvanceAmount = TimeSpan.FromSeconds(1) });
        var shell = new PlataformaFalsa(motor) { RotaAtual = "inicio" };
        var pessoas = new TelaFalsa(motor);
        var etiquetas = new TelaFalsa(motor);
        shell.Telas["inicio"] = new TelaFalsa(motor);
        shell.Telas["pessoas"] = pessoas;
        shell.Telas["etiquetas"] = etiquetas;
        motor.TituloDaRota = r => r switch { "pessoas" => "Cadastro de pessoas", "etiquetas" => "Etiquetas", "inicio" => "Início", _ => null };
        motor.Conectar(shell);
        motor.AoChegarNaTela(); // primeira tela
        return (motor, shell, pessoas, etiquetas);
    }

    private static string[] Caminho(GerenciadorNavegacao motor) =>
        motor.Historico.Select(e => e.Local.Registro is { } r ? $"{e.Local.Rota}:{r.Titulo}" : e.Local.Rota).ToArray();

    // ---- Histórico e Voltar ----

    [Fact]
    public void Sem_historico_nao_ha_para_onde_voltar()
    {
        var (motor, _, _, _) = Novo();
        Assert.False(motor.PodeVoltar);
        Assert.Equal("Nada para voltar", motor.DescricaoVoltar);
        Assert.Equal(new[] { "inicio" }, Caminho(motor));
    }

    [Fact]
    public async Task Voltar_segue_o_historico_real_tela_ficha_e_tela_anterior()
    {
        var (motor, shell, pessoas, _) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        pessoas.Abrir(Joao);
        Assert.Equal(new[] { "inicio", "pessoas", "pessoas:João da Silva" }, Caminho(motor));
        Assert.Equal("Voltar para Cadastro de pessoas", motor.DescricaoVoltar);

        Assert.True(await motor.VoltarAsync()); // ficha → lista (mesma tela)
        Assert.Null(pessoas.RegistroAberto);
        Assert.Equal(new[] { "inicio", "pessoas" }, Caminho(motor));

        Assert.True(await motor.VoltarAsync()); // lista → tela anterior
        Assert.Equal("inicio", shell.RotaAtual);
        Assert.Equal(new[] { "inicio" }, Caminho(motor));
        Assert.False(await motor.VoltarAsync());
    }

    [Fact]
    public async Task Link_empilha_e_o_Voltar_retorna_a_origem_nao_ao_pai_logico()
    {
        var (motor, _, pessoas, _) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        pessoas.Abrir(Joao);

        Assert.True(await motor.AbrirAsync(new LocalNavegacao("pessoas", Maria), OrigemNavegacao.Link)); // "Abrir" do toast
        Assert.Equal(Maria, pessoas.RegistroAberto);
        Assert.Equal("Voltar para João da Silva", motor.DescricaoVoltar);

        Assert.True(await motor.VoltarAsync());
        Assert.True(pessoas.RegistroAberto!.MesmoQue(Joao)); // de volta a quem estava, não à lista
    }

    [Fact]
    public async Task Menu_leva_a_tela_como_ela_estava_e_o_Voltar_reencontra_a_ficha()
    {
        var (motor, shell, pessoas, _) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        pessoas.Abrir(Joao);
        await motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu);
        Assert.Equal("Voltar para João da Silva", motor.DescricaoVoltar);

        Assert.True(await motor.VoltarAsync());
        Assert.Equal("pessoas", shell.RotaAtual);
        Assert.True(pessoas.RegistroAberto!.MesmoQue(Joao));
        Assert.Equal(0, pessoas.Pedidos); // a ficha já estava aberta lá: nada foi relido
    }

    // ---- Registros dentro da tela ----

    [Fact]
    public async Task Trocar_de_registro_pela_lista_nao_vira_historico()
    {
        var (motor, _, pessoas, _) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        pessoas.Abrir(Joao);
        pessoas.Abrir(Maria); // outro da mesma lista
        Assert.Equal(new[] { "inicio", "pessoas", "pessoas:Maria Souza" }, Caminho(motor));
    }

    [Fact]
    public async Task Fechar_a_ficha_por_qualquer_caminho_e_voltar()
    {
        var (motor, _, pessoas, _) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        pessoas.Abrir(Joao);
        pessoas.Fechar(); // botão Fechar, voltar do celular...
        Assert.Equal(new[] { "inicio", "pessoas" }, Caminho(motor));
    }

    [Fact]
    public async Task Registro_novo_gravado_continua_sendo_o_mesmo_passo()
    {
        var (motor, _, pessoas, _) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        pessoas.Abrir(new ReferenciaRegistro("pessoa", null, "Novo cadastro", Novo: true));
        pessoas.Abrir(Joao); // gravou: ganhou Id e nome
        Assert.Equal(new[] { "inicio", "pessoas", "pessoas:João da Silva" }, Caminho(motor));
    }

    [Fact]
    public async Task Link_de_outra_tela_que_chega_e_depois_abre_a_ficha_e_um_passo_so()
    {
        var (motor, _, pessoas, _) = Novo();
        pessoas.RegistroAberto = Maria; // ficou aberta de antes, em segundo plano
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Link); // "Abrir ficha" da carteira
        pessoas.Abrir(Joao, OrigemNavegacao.Link);                   // a tela abre o pedido ao aparecer

        Assert.Equal(new[] { "inicio", "pessoas:João da Silva" }, Caminho(motor));
        Assert.True(await motor.VoltarAsync());
        Assert.Equal("inicio", motor.Atual!.Local.Rota);
    }

    [Fact]
    public async Task Mesmo_lugar_de_novo_nao_duplica_e_atualiza_o_nome()
    {
        var (motor, _, pessoas, _) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        pessoas.Abrir(Joao);
        pessoas.Abrir(Joao with { Titulo = "João da Silva Jr." }); // gravou com outro nome

        Assert.Equal(new[] { "inicio", "pessoas", "pessoas:João da Silva Jr." }, Caminho(motor));
    }

    // ---- Duplo clique e concorrência ----

    [Fact]
    public async Task Duplo_clique_no_Voltar_volta_uma_vez_so()
    {
        var (motor, shell, _, _) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        await motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu);

        shell.Segurar = new TaskCompletionSource();
        var primeiro = motor.VoltarAsync();
        Assert.True(motor.Navegando);
        Assert.False(await motor.VoltarAsync()); // o segundo clique é ignorado
        Assert.False(await motor.IrParaTelaAsync("inicio", OrigemNavegacao.Menu));
        shell.Segurar.SetResult();

        Assert.True(await primeiro);
        Assert.Equal(new[] { "inicio", "pessoas" }, Caminho(motor));
        Assert.Equal(new[] { "pessoas", "etiquetas", "pessoas" }, shell.Pedidos); // um Voltar só, nenhum "inicio"
    }

    [Fact]
    public async Task Duplo_clique_rapido_depois_de_uma_volta_ja_concluida_tambem_conta_como_um()
    {
        var relogio = new FakeTimeProvider();
        var motor = new GerenciadorNavegacao(relogio);
        var shell = new PlataformaFalsa(motor) { RotaAtual = "inicio" };
        foreach (var r in new[] { "inicio", "pessoas", "etiquetas" }) shell.Telas[r] = new TelaFalsa(motor);
        motor.Conectar(shell);
        motor.AoChegarNaTela();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        await motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu);

        Assert.True(await motor.VoltarAsync());
        relogio.Advance(TimeSpan.FromMilliseconds(150)); // segundo clique do duplo clique
        Assert.False(await motor.VoltarAsync());
        Assert.Equal("pessoas", shell.RotaAtual);

        relogio.Advance(GerenciadorNavegacao.IntervaloMinimoVoltar); // clique intencional, depois
        Assert.True(await motor.VoltarAsync());
        Assert.Equal("inicio", shell.RotaAtual);
    }

    [Fact]
    public async Task Registro_que_nao_abre_mais_ao_voltar_nao_duplica_a_entrada_da_lista()
    {
        var (motor, shell, pessoas, _) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        pessoas.Abrir(Joao);
        await motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu);

        pessoas.RegistroAberto = null; // a tela de Pessoas foi recriada ao sair do módulo
        pessoas.NaoAbre = true;        // e o João não abre mais (ex.: excluído)
        Assert.False(await motor.VoltarAsync());

        Assert.Equal("pessoas", shell.RotaAtual);
        Assert.Equal(new[] { "inicio", "pessoas" }, Caminho(motor)); // sem "pessoas, pessoas"
        Assert.Equal("Voltar para Início", motor.DescricaoVoltar);
    }

    [Fact]
    public async Task Shell_trocado_no_meio_da_navegacao_nao_mexe_em_nada()
    {
        var (motor, shell, _, _) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        shell.Segurar = new TaskCompletionSource();
        var ida = motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu);

        motor.Desconectar(shell); // saiu do sistema / trocou de empresa enquanto navegava
        shell.Segurar.SetResult();

        Assert.False(await ida);
        Assert.Empty(motor.Historico);
        Assert.False(motor.PodeVoltar);
    }

    [Fact]
    public async Task Tela_em_segundo_plano_nao_mexe_no_historico()
    {
        var (motor, _, pessoas, etiquetas) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        etiquetas.Abrir(new ReferenciaRegistro("etiquetas", Guid.NewGuid(), "VIP")); // terminou de carregar depois

        Assert.Equal(new[] { "inicio", "pessoas" }, Caminho(motor));
    }

    // ---- Alterações não salvas ----

    [Fact]
    public async Task Voltar_com_alteracoes_nao_salvas_respeita_quem_escolhe_continuar_editando()
    {
        var (motor, shell, pessoas, _) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        pessoas.Abrir(Joao);

        pessoas.UsuarioFica = true; // mesma tela: a ficha pergunta
        Assert.False(await motor.VoltarAsync());
        Assert.Equal(new[] { "inicio", "pessoas", "pessoas:João da Silva" }, Caminho(motor));

        await motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu);
        shell.ShellCancela = true; // outra tela: quem pergunta é o Shell
        pessoas.UsuarioFica = false;
        var antes = Caminho(motor);
        Assert.False(await motor.VoltarAsync());
        Assert.Equal(antes, Caminho(motor));
    }

    // ---- Permissões ----

    [Fact]
    public async Task Sem_permissao_nao_abre_e_o_Voltar_pula_a_tela()
    {
        var (motor, shell, _, _) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        await motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu);

        motor.RotaPermitida = r => r != "pessoas"; // perfil mudou
        Assert.False(await motor.AbrirAsync(new LocalNavegacao("pessoas", Joao), OrigemNavegacao.Link));
        Assert.Equal("Voltar para Início", motor.DescricaoVoltar);

        Assert.True(await motor.VoltarAsync());
        Assert.Equal("inicio", shell.RotaAtual);
        Assert.DoesNotContain("pessoas", shell.Pedidos.Skip(2));
    }

    // ---- Endereço interno (deep link), recentes, limite ----

    [Fact]
    public void Endereco_interno_leva_tela_tipo_e_id_mas_nunca_o_nome()
    {
        var local = new LocalNavegacao("pessoas", Joao);
        Assert.Equal($"lone://pessoas/pessoa/{Joao.Id:D}", local.Endereco);
        Assert.DoesNotContain("João", local.Endereco);

        Assert.True(LocalNavegacao.TentarLer(local.Endereco, out var lido));
        Assert.True(lido.MesmoQue(local));
        Assert.True(LocalNavegacao.TentarLer("lone://pessoas", out var tela));
        Assert.Null(tela.Registro);
        Assert.False(LocalNavegacao.TentarLer("https://pessoas/pessoa/1", out _));
        Assert.False(LocalNavegacao.TentarLer("lone://pessoas/pessoa/nao-e-guid", out _));
        Assert.False(LocalNavegacao.TentarLer("lone://pes soas", out _));
    }

    [Fact]
    public async Task Endereco_interno_abre_pelo_motor()
    {
        var (motor, shell, pessoas, _) = Novo();
        Assert.True(await motor.AbrirEnderecoAsync($"lone://pessoas/pessoa/{Joao.Id:D}"));
        Assert.Equal("pessoas", shell.RotaAtual);
        Assert.True(pessoas.RegistroAberto!.MesmoQue(Joao));
        Assert.Equal(OrigemNavegacao.Endereco, motor.Atual!.Origem);
    }

    [Fact]
    public async Task Recentes_da_sessao_sem_repetir_e_com_limite()
    {
        var (motor, _, pessoas, _) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        for (var i = 0; i < GerenciadorNavegacao.MaximoRecentes + 3; i++)
            pessoas.Abrir(new ReferenciaRegistro("pessoa", Guid.NewGuid(), $"P{i}"));
        pessoas.Abrir(Joao);
        pessoas.Abrir(Maria);
        pessoas.Abrir(Joao);

        Assert.Equal(GerenciadorNavegacao.MaximoRecentes, motor.RegistrosRecentes.Count);
        Assert.Equal(new[] { "João da Silva", "Maria Souza" }, motor.RegistrosRecentes.Take(2).Select(r => r.Titulo));
    }

    [Fact]
    public void Historico_tem_limite()
    {
        var historico = new HistoricoNavegacao();
        for (var i = 0; i < HistoricoNavegacao.Maximo + 10; i++)
            historico.Registrar(new EntradaNavegacao(new LocalNavegacao($"tela{i}"), OrigemNavegacao.Menu, $"Tela {i}", DateTimeOffset.MinValue));
        Assert.Equal(HistoricoNavegacao.Maximo, historico.Entradas.Count);
        Assert.Equal("tela59", historico.Atual!.Local.Rota);
    }

    // ---- Integração real: tela de Pessoas (base dos cadastros) ----

    private sealed class ShellDePessoas(Lone.Cliente.ViewModels.Pessoas.PessoasViewModel tela) : IPlataformaNavegacao
    {
        public string? RotaAtual => "pessoas";
        public ITelaNavegavel? TelaAtual => tela;
        public Task IrParaRotaAsync(string rota) => Task.CompletedTask;
    }

    [Fact]
    public async Task Pessoas_informa_a_ficha_e_o_Voltar_fecha_perguntando_se_houver_alteracoes()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var (tela, _) = await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
        tela.Mensagens = new ServicoMensagens(new FakeTimeProvider());
        var motor = new GerenciadorNavegacao(new FakeTimeProvider { AutoAdvanceAmount = TimeSpan.FromSeconds(1) });
        tela.Navegacao = motor;
        motor.Conectar(new ShellDePessoas(tela));
        motor.AoChegarNaTela(OrigemNavegacao.Menu);

        await tela.NovoCommand.ExecuteAsync(null);
        Assert.True(motor.Atual!.Local.Registro!.Novo);
        var f = tela.Formulario!;
        f.Nome = "Carlos";
        f.Enderecos[0].Municipio.Definir(3550308, "São Paulo", "SP");
        var gravada = f.ParaDto();
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, new ResultadoSalvarPessoa { Pessoa = gravada, Avisos = [] })
            .Responder(HttpStatusCode.OK, new PaginaListaPessoas());
        await tela.SalvarCommand.ExecuteAsync(null);

        var registro = motor.Atual!.Local.Registro!;
        Assert.Equal("pessoa", registro.Tipo);
        Assert.Equal(f.Id, registro.Id);
        Assert.Equal("Carlos", registro.Titulo);
        Assert.Equal(2, motor.Historico.Count); // lista + Carlos (o "Novo" virou Carlos)
        Assert.Equal($"lone://pessoas/pessoa/{f.Id:D}", motor.Atual.Local.Endereco);

        // Alterou e tentou voltar: pergunta; quem escolhe continuar editando fica.
        tela.Formulario!.Nome = "Carlos Souza";
        ambiente.Dialogos.RespostaConfirmacao = false;
        Assert.False(await motor.VoltarAsync());
        Assert.Single(ambiente.Dialogos.Perguntas);
        Assert.True(tela.Editando);

        // Aceitou descartar: volta para a lista.
        ambiente.Dialogos.RespostaConfirmacao = true;
        Assert.True(await motor.VoltarAsync());
        Assert.False(tela.Editando);
        Assert.Single(motor.Historico);
    }
}
