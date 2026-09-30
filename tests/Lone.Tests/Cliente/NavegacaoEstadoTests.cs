using Lone.Cliente.Navegacao;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Cliente;

/// <summary>
/// Navegação Fase 3 — preservação de contexto no motor: o Shell recria a tela ao trocar de módulo e o motor devolve o
/// estado (pelo menu: o último da tela; pelo ←: o retrato daquela entrada do histórico). Telas e Shell falsos: nada de MAUI.
/// </summary>
public class NavegacaoEstadoTests
{
    private static readonly ReferenciaRegistro Bruno = new("pessoa", Guid.NewGuid(), "Bruno");
    private static readonly ReferenciaRegistro Maria = new("pessoa", Guid.NewGuid(), "Maria Souza");

    /// <summary>Tela com estado de navegação (pesquisa, ficha, rolagem), como a base dos cadastros.</summary>
    private sealed class TelaComEstado(GerenciadorNavegacao motor, HashSet<Guid> existentes) : IEstadoNavegavel
    {
        public string Busca { get; set; } = string.Empty;
        public ReferenciaRegistro? RegistroAberto { get; private set; }
        public MemoriaRolagem Rolagem { get; } = new();
        public List<EstadoTela> Restauracoes { get; } = new();
        public int Leituras { get; private set; }
        public bool FalhaAoCapturar { get; set; }

        public Task<bool> IrParaRegistroAsync(ReferenciaRegistro? registro)
        {
            if (registro is null)
            {
                RegistroAberto = null;
                motor.InformarRegistro(this);
                return Task.FromResult(true);
            }
            Leituras++; // "lê a ficha no servidor"
            if (registro.Id is not { } id || !existentes.Contains(id)) return Task.FromResult(false); // excluído / sem acesso
            RegistroAberto = registro;
            motor.InformarRegistro(this); // durante a restauração o motor ignora (nenhum passo novo)
            return Task.FromResult(true);
        }

        public void Abrir(ReferenciaRegistro registro)
        {
            RegistroAberto = registro;
            motor.InformarRegistro(this);
        }

        public EstadoTela CapturarEstado() => FalhaAoCapturar
            ? throw new InvalidOperationException("tela quebrada")
            : new EstadoTela { Busca = Busca, Registro = RegistroAberto, Rolagens = Rolagem.Capturar() };

        /// <summary>Segura a restauração (a "primeira carga" demorando) até o teste liberar.</summary>
        public TaskCompletionSource? Segurar { get; set; }

        /// <summary>Tela que não respeita o cancelamento (leitura que não dá para cancelar): aplica tudo quando chega.</summary>
        public bool IgnoraCancelamento { get; set; }

        public async Task RestaurarEstadoAsync(EstadoTela estado, CancellationToken cancelamento = default)
        {
            Restauracoes.Add(estado);
            if (Segurar is { } segurar)
            {
                if (IgnoraCancelamento) await segurar.Task;
                else await segurar.Task.WaitAsync(cancelamento);
            }
            Busca = estado.Busca;
            if (estado.Registro is { } r && RegistroAberto?.MesmoQue(r) != true) await IrParaRegistroAsync(r);
            Rolagem.Restaurar(estado.Rolagens);
        }
    }

    /// <summary>Shell falso que, como o real, cria uma tela nova ao trocar de módulo (ou mantém a mesma, se pedido).</summary>
    private sealed class ShellQueRecria(GerenciadorNavegacao motor, HashSet<Guid> existentes) : IPlataformaNavegacao
    {
        private readonly Dictionary<string, TelaComEstado> _vivas = new();
        public bool Recriar { get; set; } = true;

        /// <summary>As telas criadas a partir de agora seguram a restauração até este sinal.</summary>
        public TaskCompletionSource? SegurarRestauracoes { get; set; }
        public bool IgnorarCancelamento { get; set; }
        public string? RotaAtual { get; private set; }
        public TelaComEstado? Tela { get; private set; }
        public ITelaNavegavel? TelaAtual => Tela;
        public List<TelaComEstado> Criadas { get; } = new();

        public Task IrParaRotaAsync(string rota)
        {
            if (Recriar || !_vivas.TryGetValue(rota, out var tela))
            {
                tela = _vivas[rota] = new TelaComEstado(motor, existentes)
                {
                    Segurar = SegurarRestauracoes, IgnoraCancelamento = IgnorarCancelamento
                };
                Criadas.Add(tela);
            }
            RotaAtual = rota;
            Tela = tela;
            motor.AoChegarNaTela();
            return Task.CompletedTask;
        }
    }

    private static (GerenciadorNavegacao Motor, ShellQueRecria Shell, HashSet<Guid> Existentes) Novo()
    {
        var motor = new GerenciadorNavegacao(new FakeTimeProvider { AutoAdvanceAmount = TimeSpan.FromSeconds(1) });
        var existentes = new HashSet<Guid> { Bruno.Id!.Value, Maria.Id!.Value };
        var shell = new ShellQueRecria(motor, existentes);
        motor.TituloDaRota = r => r switch { "pessoas" => "Cadastro de pessoas", "etiquetas" => "Etiquetas", "inicio" => "Início", _ => null };
        motor.Conectar(shell);
        shell.IrParaRotaAsync("inicio").GetAwaiter().GetResult(); // primeira tela
        return (motor, shell, existentes);
    }

    private static string[] Caminho(GerenciadorNavegacao motor) =>
        motor.Historico.Select(e => e.Local.Registro is { } r ? $"{e.Local.Rota}:{r.Titulo}" : e.Local.Rota).ToArray();

    /// <summary>Pessoas: pesquisa "João", Bruno aberto, lista rolada; depois vai para Etiquetas pelo menu.</summary>
    private static async Task<TelaComEstado> TrabalharEmPessoasESairAsync(GerenciadorNavegacao motor, ShellQueRecria shell)
    {
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        var pessoas = shell.Tela!;
        pessoas.Busca = "João";
        pessoas.Abrir(Bruno);
        pessoas.Rolagem.Registrar("lista", 0, 480);
        await motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu);
        return pessoas;
    }

    // ---- Menu e ← ----

    [Fact]
    public async Task Menu_devolve_a_tela_recriada_como_o_usuario_deixou()
    {
        var (motor, shell, _) = Novo();
        var antiga = await TrabalharEmPessoasESairAsync(motor, shell);

        Assert.True(await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu));

        var nova = shell.Tela!;
        Assert.NotSame(antiga, nova); // o Shell recriou
        Assert.Equal("João", nova.Busca);
        Assert.True(nova.RegistroAberto!.MesmoQue(Bruno));
        Assert.Equal(480, nova.Rolagem.Pendente("lista")!.Y);
        Assert.Single(nova.Restauracoes);
    }

    [Fact]
    public async Task Voltar_devolve_a_tela_recriada_como_estava_naquele_ponto()
    {
        var (motor, shell, _) = Novo();
        await TrabalharEmPessoasESairAsync(motor, shell);

        Assert.True(await motor.VoltarAsync());

        var nova = shell.Tela!;
        Assert.Equal("pessoas", shell.RotaAtual);
        Assert.Equal("João", nova.Busca);
        Assert.True(nova.RegistroAberto!.MesmoQue(Bruno));
        Assert.Equal(1, nova.Leituras); // a ficha foi lida uma vez só (restauração + ← não abrem duas vezes)
    }

    /// <summary>
    /// Pessoas(João, Bruno) → Etiquetas → link para Maria (tela nova) → pesquisa "Zé" → Etiquetas. A tela ficou em "Zé",
    /// mas a entrada anterior do histórico (Bruno) continua com o retrato dela ("João").
    /// </summary>
    private static async Task<(GerenciadorNavegacao Motor, ShellQueRecria Shell)> DoisPontosNaMesmaTelaAsync()
    {
        var (motor, shell, _) = Novo();
        await TrabalharEmPessoasESairAsync(motor, shell);
        Assert.True(await motor.AbrirAsync(new LocalNavegacao("pessoas", Maria), OrigemNavegacao.Link));
        Assert.Equal("João", shell.Tela!.Busca); // chegou com o último contexto da tela, no registro pedido
        Assert.True(shell.Tela.RegistroAberto!.MesmoQue(Maria));
        shell.Tela.Busca = "Zé";
        await motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu);
        return (motor, shell);
    }

    [Fact]
    public async Task Estado_atual_da_tela_e_diferente_do_retrato_da_entrada_do_historico()
    {
        var (motor, _) = await DoisPontosNaMesmaTelaAsync();

        Assert.Equal("Zé", motor.EstadoDaTela("pessoas")!.Busca); // estado atual da tela
        var entradaBruno = motor.Historico.Single(e => e.Local.Registro?.MesmoQue(Bruno) == true);
        var retrato = Assert.IsAssignableFrom<EstadoTela>(entradaBruno.Estado);
        Assert.Equal("João", retrato.Busca); // a navegação posterior não sobrescreveu o retrato da entrada anterior
        Assert.True(retrato.Registro!.MesmoQue(Bruno));
    }

    [Fact]
    public async Task Voltar_usa_o_retrato_da_entrada_e_nao_o_ultimo_estado_da_tela()
    {
        var (motor, shell) = await DoisPontosNaMesmaTelaAsync();

        Assert.True(await motor.VoltarAsync());

        Assert.Equal("João", shell.Tela!.Busca);
        Assert.True(shell.Tela.RegistroAberto!.MesmoQue(Bruno));
    }

    [Fact]
    public async Task Menu_usa_o_ultimo_estado_conhecido_da_tela()
    {
        var (motor, shell) = await DoisPontosNaMesmaTelaAsync();

        Assert.True(await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu));

        Assert.Equal("Zé", shell.Tela!.Busca);
        Assert.True(shell.Tela.RegistroAberto!.MesmoQue(Maria));
    }

    [Fact]
    public async Task Estado_e_guardado_por_tela()
    {
        var (motor, shell, _) = Novo();
        await TrabalharEmPessoasESairAsync(motor, shell);
        shell.Tela!.Busca = "vip"; // etiquetas
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        Assert.Equal("João", shell.Tela!.Busca);

        await motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu);

        Assert.Equal("vip", shell.Tela!.Busca); // cada tela com o seu
        Assert.Equal("João", motor.EstadoDaTela("pessoas")!.Busca);
    }

    // ---- Idempotência, histórico e instância ----

    [Fact]
    public async Task Restauracao_nao_cria_passos_no_historico()
    {
        var (motor, shell, _) = Novo();
        await TrabalharEmPessoasESairAsync(motor, shell);
        Assert.Equal(new[] { "inicio", "pessoas", "pessoas:Bruno", "etiquetas" }, Caminho(motor));

        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);

        // A ficha reaberta pela restauração não vira passo novo: chegar ao lugar anterior é voltar a ele.
        Assert.Equal(new[] { "inicio", "pessoas", "pessoas:Bruno" }, Caminho(motor));
        Assert.Equal(1, shell.Tela!.Leituras);
    }

    [Fact]
    public async Task Mesma_instancia_nao_e_restaurada()
    {
        var (motor, shell, _) = Novo();
        shell.Recriar = false; // plataforma que mantém a tela viva
        var antiga = await TrabalharEmPessoasESairAsync(motor, shell);

        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);

        Assert.Same(antiga, shell.Tela);
        Assert.Empty(antiga.Restauracoes); // já estava como estava: nada é reaplicado
        Assert.Equal(0, antiga.Leituras); // aberta pelo usuário, nunca relida pelo motor
    }

    [Fact]
    public async Task Restaurar_o_mesmo_estado_duas_vezes_da_o_mesmo_resultado()
    {
        var (motor, shell, _) = Novo();
        await TrabalharEmPessoasESairAsync(motor, shell);
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        var tela = shell.Tela!;
        var historico = Caminho(motor);

        await tela.RestaurarEstadoAsync(tela.Restauracoes[0]);

        Assert.Equal("João", tela.Busca);
        Assert.True(tela.RegistroAberto!.MesmoQue(Bruno));
        Assert.Equal(1, tela.Leituras); // a ficha já aberta não é lida de novo
        Assert.Equal(480, tela.Rolagem.Capturar()["lista"].Y);
        Assert.Equal(historico, Caminho(motor));
    }

    // ---- Estado que deixou de valer ----

    [Fact]
    public async Task Registro_excluido_ao_voltar_pelo_menu_abre_so_a_tela_sem_erro()
    {
        var (motor, shell, existentes) = Novo();
        await TrabalharEmPessoasESairAsync(motor, shell);
        existentes.Remove(Bruno.Id!.Value); // excluído (ou o usuário perdeu acesso) depois que ele saiu

        Assert.True(await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu));

        Assert.Equal("pessoas", shell.RotaAtual);
        Assert.Equal("João", shell.Tela!.Busca); // o que ainda vale é restaurado
        Assert.Null(shell.Tela.RegistroAberto);
        Assert.Equal(1, shell.Tela.Leituras); // tentou uma vez; o motor não insiste
    }

    [Fact]
    public async Task Registro_excluido_ao_voltar_pelo_Voltar_fica_na_lista_sem_duplicar_o_historico()
    {
        var (motor, shell, existentes) = Novo();
        await TrabalharEmPessoasESairAsync(motor, shell);
        existentes.Remove(Bruno.Id!.Value);

        await motor.VoltarAsync();

        Assert.Equal("pessoas", shell.RotaAtual);
        Assert.Null(shell.Tela!.RegistroAberto);
        Assert.Equal(1, shell.Tela.Leituras);
        Assert.Equal(new[] { "inicio", "pessoas" }, Caminho(motor));
    }

    [Fact]
    public async Task Tela_que_falha_ao_se_descrever_nao_impede_a_navegacao_e_volta_no_padrao()
    {
        var (motor, shell, _) = Novo();
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        shell.Tela!.Busca = "João";
        shell.Tela.FalhaAoCapturar = true;

        Assert.True(await motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu));
        Assert.Null(motor.EstadoDaTela("pessoas"));

        Assert.True(await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu));
        Assert.Equal(string.Empty, shell.Tela!.Busca); // estado seguro/padrão
        Assert.Empty(shell.Tela.Restauracoes);
    }

    [Fact]
    public async Task Tela_sem_permissao_nao_e_restaurada_nem_aberta()
    {
        var (motor, shell, _) = Novo();
        await TrabalharEmPessoasESairAsync(motor, shell);
        motor.RotaPermitida = r => r != "pessoas"; // perdeu o acesso à tela

        Assert.False(await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu));

        Assert.Equal("etiquetas", shell.RotaAtual);
        Assert.Equal(3, shell.Criadas.Count); // início, pessoas, etiquetas: nenhuma tela nova foi criada
    }

    // ---- Ciclo de vida (LGPD: só em memória, some com o contexto) ----

    [Fact]
    public async Task Shell_novo_esquece_todo_o_estado_de_navegacao()
    {
        var (motor, shell, existentes) = Novo();
        await TrabalharEmPessoasESairAsync(motor, shell);
        Assert.NotNull(motor.EstadoDaTela("pessoas"));

        var outro = new ShellQueRecria(motor, existentes); // saiu e entrou (ou trocou de empresa)
        motor.Conectar(outro);
        await outro.IrParaRotaAsync("inicio");
        await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);

        Assert.Null(motor.EstadoDaTela("etiquetas"));
        Assert.Equal(string.Empty, outro.Tela!.Busca); // a pesquisa digitada antes não volta
        Assert.Empty(outro.Tela.Restauracoes);
        Assert.DoesNotContain(motor.Historico, e => e.Estado is EstadoTela { Busca: "João" });
    }

    [Fact]
    public async Task Desconectar_tambem_esquece_o_estado()
    {
        var (motor, shell, _) = Novo();
        await TrabalharEmPessoasESairAsync(motor, shell);

        motor.Desconectar(shell);

        Assert.Null(motor.EstadoDaTela("pessoas"));
        Assert.Empty(motor.Historico);
    }

    // ---- Interromper a restauração (a tela nova demora a voltar; o usuário muda de ideia) ----

    /// <summary>Etiquetas → menu Pessoas com a restauração "demorando" (segura até o teste liberar).</summary>
    private static async Task<(GerenciadorNavegacao Motor, ShellQueRecria Shell, Task<bool> Ida, TelaComEstado Restaurando,
        TaskCompletionSource Sinal)> PessoasRestaurandoAsync(bool ignoraCancelamento = false)
    {
        var (motor, shell, _) = Novo();
        await TrabalharEmPessoasESairAsync(motor, shell);
        var sinal = new TaskCompletionSource();
        shell.SegurarRestauracoes = sinal;
        shell.IgnorarCancelamento = ignoraCancelamento;
        var ida = motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        shell.SegurarRestauracoes = null; // só a tela de Pessoas demora
        shell.IgnorarCancelamento = false;
        Assert.False(ida.IsCompleted); // restaurando
        Assert.Equal("pessoas", shell.RotaAtual);
        return (motor, shell, ida, shell.Tela!, sinal);
    }

    [Fact]
    public async Task Menu_durante_a_restauracao_interrompe_e_abre_a_outra_tela_na_hora()
    {
        var (motor, shell, ida, restaurando, _) = await PessoasRestaurandoAsync();
        Assert.False(motor.Navegando); // restaurando não trava o menu nem o ←

        Assert.True(await motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu));

        Assert.False(await ida); // a ida a Pessoas terminou interrompida
        Assert.Equal("etiquetas", shell.RotaAtual);
        Assert.False(motor.Navegando);
        // Histórico: só navegações reais; voltar para onde estava desfaz a passagem por Pessoas (regra da Fase 1).
        Assert.Equal(new[] { "inicio", "pessoas", "pessoas:Bruno", "etiquetas" }, Caminho(motor));
        // A tela interrompida parou onde estava; o estado guardado dela continua o que ia ser restaurado (não a metade).
        Assert.Equal(string.Empty, restaurando.Busca);
        Assert.Null(restaurando.RegistroAberto);
        Assert.Equal("João", motor.EstadoDaTela("pessoas")!.Busca);
        Assert.True(motor.EstadoDaTela("pessoas")!.Registro!.MesmoQue(Bruno));
        var retratoBruno = motor.Historico.Single(e => e.Local.Registro?.MesmoQue(Bruno) == true).Estado as EstadoTela;
        Assert.Equal("João", retratoBruno!.Busca); // o retrato da entrada anterior continua intacto
    }

    [Fact]
    public async Task Voltar_durante_a_restauracao_interrompe_e_volta_para_onde_o_usuario_estava()
    {
        var (motor, shell, ida, _, _) = await PessoasRestaurandoAsync();
        Assert.True(motor.PodeVoltar);
        Assert.Equal("Voltar para Etiquetas", motor.DescricaoVoltar); // já prevê a chegada a Pessoas

        Assert.True(await motor.VoltarAsync());

        Assert.False(await ida);
        Assert.Equal("etiquetas", shell.RotaAtual);
        Assert.Equal(new[] { "inicio", "pessoas", "pessoas:Bruno", "etiquetas" }, Caminho(motor));
        Assert.False(motor.Navegando);
    }

    [Fact]
    public async Task Voltar_durante_a_restauracao_de_um_Voltar_segue_para_o_passo_anterior()
    {
        var (motor, shell, _) = Novo();
        await TrabalharEmPessoasESairAsync(motor, shell);
        shell.SegurarRestauracoes = new TaskCompletionSource();
        var volta = motor.VoltarAsync(); // ← para a ficha do Bruno, demorando
        shell.SegurarRestauracoes = null;
        Assert.False(volta.IsCompleted);

        Assert.True(await motor.VoltarAsync()); // mudou de ideia: mais um ←

        Assert.False(await volta);
        Assert.Equal("inicio", shell.RotaAtual);
        Assert.Equal(new[] { "inicio" }, Caminho(motor)); // sem passos duplicados nem a ficha que não chegou a abrir
    }

    [Fact]
    public async Task Restauracao_interrompida_nao_tem_efeito_tardio()
    {
        // Tela que não consegue cancelar o que já começou: quando a "leitura" chega, ela aplica tudo nela mesma.
        var (motor, shell, ida, restaurando, sinal) = await PessoasRestaurandoAsync(ignoraCancelamento: true);
        await motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu);
        var caminho = Caminho(motor);
        var atual = shell.Tela!;
        var buscaAtual = atual.Busca;
        var estadoGuardado = motor.EstadoDaTela("pessoas");

        sinal.SetResult(); // a leitura antiga termina agora
        Assert.False(await ida);

        Assert.True(restaurando.RegistroAberto!.MesmoQue(Bruno)); // (só na instância abandonada, fora da tela)
        Assert.Equal(caminho, Caminho(motor));                    // nenhuma entrada no histórico
        Assert.Equal("etiquetas", shell.RotaAtual);               // a tela atual não mudou
        Assert.Same(atual, shell.Tela);
        Assert.Equal(buscaAtual, atual.Busca);                    // nem o contexto dela
        Assert.Null(atual.RegistroAberto);
        Assert.Same(estadoGuardado, motor.EstadoDaTela("pessoas")); // nem o estado guardado de Pessoas
        Assert.False(motor.Navegando);
    }

    [Fact]
    public async Task Depois_de_interromper_voltar_a_tela_faz_uma_restauracao_limpa()
    {
        var (motor, shell, ida, _, _) = await PessoasRestaurandoAsync();
        await motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu);
        await ida;

        Assert.True(await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu));

        var nova = shell.Tela!;
        Assert.Single(nova.Restauracoes);
        Assert.Equal("João", nova.Busca);
        Assert.True(nova.RegistroAberto!.MesmoQue(Bruno));
        Assert.Equal(480, nova.Rolagem.Pendente("lista")!.Y);
        Assert.Equal(1, nova.Leituras);
        Assert.Equal(new[] { "inicio", "pessoas", "pessoas:Bruno" }, Caminho(motor));
    }

    [Fact]
    public async Task Pedidos_seguidos_durante_restauracoes_terminam_na_ultima_navegacao_sem_duplicar()
    {
        var (motor, shell, _) = Novo();
        await TrabalharEmPessoasESairAsync(motor, shell);
        shell.SegurarRestauracoes = new TaskCompletionSource(); // Pessoas e Etiquetas demoram a voltar
        var pessoas = motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu);
        var etiquetas = motor.IrParaTelaAsync("etiquetas", OrigemNavegacao.Menu); // interrompe Pessoas; Etiquetas restaura
        shell.SegurarRestauracoes = null;
        Assert.False(etiquetas.IsCompleted);

        Assert.True(await motor.IrParaTelaAsync("inicio", OrigemNavegacao.Menu)); // interrompe Etiquetas

        Assert.False(await pessoas);
        Assert.False(await etiquetas);
        Assert.Equal("inicio", shell.RotaAtual);
        Assert.Equal(new[] { "inicio", "pessoas", "pessoas:Bruno", "etiquetas", "inicio" }, Caminho(motor));
        Assert.False(motor.Navegando);
        Assert.Equal("João", motor.EstadoDaTela("pessoas")!.Busca);
    }

    [Fact]
    public async Task Menu_para_a_propria_tela_que_esta_restaurando_nao_interrompe()
    {
        var (motor, shell, ida, restaurando, sinal) = await PessoasRestaurandoAsync();

        Assert.False(await motor.IrParaTelaAsync("pessoas", OrigemNavegacao.Menu)); // já está chegando lá

        sinal.SetResult();
        Assert.True(await ida);
        Assert.Same(restaurando, shell.Tela);
        Assert.Equal("João", restaurando.Busca);
        Assert.True(restaurando.RegistroAberto!.MesmoQue(Bruno));
        Assert.Equal(new[] { "inicio", "pessoas", "pessoas:Bruno" }, Caminho(motor));
    }

    [Fact]
    public async Task Shell_novo_durante_a_restauracao_cancela_sem_registrar_nada()
    {
        var (motor, _, ida, restaurando, _) = await PessoasRestaurandoAsync();
        var outro = new ShellQueRecria(motor, new HashSet<Guid>());

        motor.Conectar(outro); // saiu / trocou de empresa no meio

        Assert.False(await ida);
        Assert.Empty(motor.Historico);
        Assert.Null(motor.EstadoDaTela("pessoas"));
        Assert.Equal(string.Empty, restaurando.Busca);
        Assert.False(motor.Navegando);
    }

    // ---- Rolagem ----

    [Fact]
    public void Rolagem_guarda_a_posicao_de_cada_chave_marcada()
    {
        var memoria = new MemoriaRolagem();
        memoria.Registrar("lista", 0, 100);
        memoria.Registrar("lista", 0, 480);
        memoria.Registrar("ficha", 0, 60);

        var retrato = memoria.Capturar();
        memoria.Registrar("lista", 0, 0); // o retrato é uma cópia

        Assert.Equal(480, retrato["lista"].Y);
        Assert.Equal(60, retrato["ficha"].Y);
        Assert.Equal(2, retrato.Count);
    }

    [Fact]
    public void Rolagem_restaurada_fica_pendente_ate_a_tela_conseguir_aplicar()
    {
        var memoria = new MemoriaRolagem();
        var pedidos = new List<string>();
        memoria.RestauracaoPedida += (_, chave) => pedidos.Add(chave);

        memoria.Restaurar(new Dictionary<string, PosicaoRolagem> { ["lista"] = new(0, 480) });

        Assert.Equal(new[] { "lista" }, pedidos.ToArray());
        Assert.Equal(480, memoria.Pendente("lista")!.Y);
        memoria.Registrar("lista", 0, 0); // a lista recarregando rola para o topo: não apaga a posição a restaurar
        Assert.Equal(480, memoria.Capturar()["lista"].Y);

        memoria.Concluir("lista"); // chegou lá
        Assert.Null(memoria.Pendente("lista"));
        memoria.Registrar("lista", 0, 520); // daqui em diante vale o que o usuário fizer
        Assert.Equal(520, memoria.Capturar()["lista"].Y);
    }

    [Fact]
    public void Rolagem_restaurada_duas_vezes_termina_na_mesma_posicao()
    {
        var memoria = new MemoriaRolagem();
        var posicoes = new Dictionary<string, PosicaoRolagem> { ["lista"] = new(0, 480), ["ficha"] = new(0, 90) };

        memoria.Restaurar(posicoes);
        memoria.Restaurar(posicoes);

        Assert.Equal(480, memoria.Pendente("lista")!.Y);
        Assert.Equal(90, memoria.Pendente("ficha")!.Y);
        Assert.Equal(2, memoria.Capturar().Count);
    }

    [Fact]
    public void Rolagem_sem_nada_guardado_nao_pede_nada()
    {
        var memoria = new MemoriaRolagem();
        var pedidos = 0;
        memoria.RestauracaoPedida += (_, _) => pedidos++;

        memoria.Restaurar(new Dictionary<string, PosicaoRolagem>());

        Assert.Equal(0, pedidos);
        Assert.Null(memoria.Pendente("lista"));
    }
}
