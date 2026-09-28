using System.Net;
using System.Text.Json;
using Lone.Cliente.Api;
using Lone.Cliente.Navegacao;
using Lone.Cliente.ViewModels.Comercial;
using Lone.Contracts.Comercial;
using Lone.Contracts.Comum;
using Lone.Contracts.Empresas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>
/// Telas da transferência de carteira e da carteira em uma data (Motor Comercial, Fase 1d): o assistente em 3 passos, a
/// prévia por cliente (marcar, trocar o destino), o pedido que leva exatamente o que a prévia mostrou, o resultado e o
/// "Abrir ficha" que leva para Pessoas.
/// </summary>
public class TransferenciasTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 28);
    private static readonly Guid Joao = Guid.NewGuid();
    private static readonly Guid Maria = Guid.NewGuid();
    private static readonly Guid Pedro = Guid.NewGuid();
    private static readonly Guid Vendedor = Guid.NewGuid();
    private static readonly Guid ClienteA = Guid.NewGuid();
    private static readonly Guid ClienteB = Guid.NewGuid();
    private static readonly Guid ClienteC = Guid.NewGuid();

    private static TransferenciaOpcoesDto Opcoes() => new()
    {
        Pessoas =
        [
            new AtendenteOpcaoDto(Joao, "João", []),
            new AtendenteOpcaoDto(Maria, "Maria", []),
            new AtendenteOpcaoDto(Pedro, "Pedro", [])
        ],
        ClientesHoje = new Dictionary<Guid, int> { [Joao] = 3, [Maria] = 1 },
        Papeis = [new TipoCarteiraDto { Id = Vendedor, Nome = "Vendedor", Ativo = true }],
        Empresas = [new EmpresaResumo(Guid.NewGuid(), "Matriz", true)],
        DiasRetroativosMaximo = 30
    };

    private static ItemTransferenciaDto Item(Guid cliente, string nome, Guid destino, ResultadoItemTransferencia r, string? motivo = null) => new()
    {
        ClienteId = cliente, Cliente = nome, Papel = "Vendedor", DestinoId = destino, Destino = destino == Maria ? "Maria" : "Pedro",
        InicioOrigem = new DateOnly(2025, 1, 1), Resultado = r, Motivo = motivo
    };

    private static PreviaTransferenciaDto Previa() => new()
    {
        Transferir = 2,
        NaoProcessar = 1,
        PorDestino = [new DestinoTransferenciaDto(Maria, "Maria", 1), new DestinoTransferenciaDto(Pedro, "Pedro", 1)],
        Avisos = ["João tem ausência cadastrada de 10/10/2026 a 20/10/2026."],
        Itens =
        [
            Item(ClienteA, "A", Pedro, ResultadoItemTransferencia.Transferido),
            Item(ClienteB, "B", Maria, ResultadoItemTransferencia.Transferido),
            Item(ClienteC, "C", Maria, ResultadoItemTransferencia.NaoProcessado, "Vendedor começa em 05/10/2026: fica como está.")
        ]
    };

    private static AssistenteTransferencia Preenchido()
    {
        var a = new AssistenteTransferencia(Opcoes(), Hoje);
        a.Origem = a.Origens.First(o => o.Valor == Joao);
        a.Papel = a.Papeis.First(o => o.Valor == Vendedor);
        a.Passo = 2;
        a.NovoDestino = a.DestinosDisponiveis.First(o => o.Valor == Maria);
        Assert.Empty(a.Destinos);                                              // só escolher não inclui
        a.IncluirDestinoCommand.Execute(null);
        a.NovoDestino = a.DestinosDisponiveis.First(o => o.Valor == Pedro);
        a.IncluirDestinoCommand.Execute(null);
        a.EfeitoEm = "01/10/2026";
        a.Motivo = "Desligamento";
        return a;
    }

    [Fact]
    public void Origem_mostra_a_carteira_de_hoje_e_nao_pode_ser_destino()
    {
        var a = new AssistenteTransferencia(Opcoes(), Hoje);
        Assert.Contains(a.ValidarPasso(), e => e.Contains("de quem a carteira sai"));
        Assert.Equal("João (3 clientes)", a.Origens.First(o => o.Valor == Joao).Texto);

        a.Origem = a.Origens.First(o => o.Valor == Joao);
        Assert.Contains("Atende hoje 3 clientes", a.ClientesDaOrigem);
        Assert.DoesNotContain(a.DestinosDisponiveis, o => o.Valor == Joao);
        Assert.Empty(a.ValidarPasso());
    }

    [Fact]
    public void Destinos_entram_pela_lista_saem_pelo_x_e_a_origem_escolhida_depois_sai_dos_destinos()
    {
        var a = Preenchido();
        Assert.Equal(new[] { "Maria", "Pedro" }, a.Destinos.Select(d => d.Nome).ToArray());
        Assert.Null(a.NovoDestino.Valor);                                      // a lista volta para "—"
        Assert.False(a.PodeIncluirDestino);
        Assert.DoesNotContain(a.DestinosDisponiveis, o => o.Valor == Maria);   // já escolhida
        Assert.True(a.VariosDestinos);
        Assert.Empty(a.ValidarPasso());

        a.TirarDestinoCommand.Execute(a.Destinos[0]);
        Assert.Equal("Pedro", Assert.Single(a.Destinos).Nome);

        a.Origem = a.Origens.First(o => o.Valor == Pedro);                   // quem sai não pode receber
        Assert.Empty(a.Destinos);
        Assert.Contains(a.ValidarPasso(), e => e.Contains("para quem a carteira vai"));
    }

    [Fact]
    public void Passo_2_exige_data_e_motivo()
    {
        var a = Preenchido();
        a.EfeitoEm = "31/02/2026";
        a.Motivo = " ";
        var erros = a.ValidarPasso();
        Assert.Contains(erros, e => e.Contains("Data de efeito"));
        Assert.Contains(erros, e => e.Contains("motivo"));
    }

    [Fact]
    public void Previa_agrupa_por_cliente_marca_quem_passa_e_o_pedido_leva_o_que_a_previa_mostrou()
    {
        var a = Preenchido();
        a.DefinirPrevia(Previa());

        Assert.True(a.NoPasso3);
        Assert.Equal(3, a.Linhas.Count);
        Assert.Equal("Transferir 2 clientes", a.TextoTransferir);
        Assert.True(a.TemAvisos);
        Assert.Contains("Maria: 1 cliente", a.PorDestino);
        var c = a.Linhas.Single(l => l.ClienteId == ClienteC);
        Assert.False(c.PodeMarcar);
        Assert.Equal("Não será processado", c.Situacao);
        Assert.Contains("fica como está", c.Motivo);

        // Desmarca B e troca o destino de A (Pedro → Maria).
        a.Linhas.Single(l => l.ClienteId == ClienteB).Marcado = false;
        var linhaA = a.Linhas.Single(l => l.ClienteId == ClienteA);
        Assert.True(linhaA.PodeTrocarDestino);
        linhaA.Destino = linhaA.Destinos.First(d => d.Valor == Maria);
        Assert.Equal("Transferir 1 cliente", a.TextoTransferir);

        var pedido = a.ParaRequisicao();
        Assert.Equal(Joao, pedido.OrigemId);
        Assert.Equal(Vendedor, pedido.TipoCarteiraId);
        Assert.Equal(new DateOnly(2026, 10, 1), pedido.EfeitoEm);
        Assert.Equal(new[] { Maria, Pedro }, pedido.Destinos.ToArray());
        Assert.Equal(new[] { ClienteA }, pedido.Clientes!.ToArray());
        Assert.Equal(Maria, pedido.DestinoPorCliente![ClienteA]);
        Assert.Equal("Desligamento", pedido.Motivo);
    }

    [Fact]
    public async Task Tela_faz_o_caminho_todo_e_mostra_o_resultado()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var abertura = new AberturaDePessoa(ambiente.Sessao);
        var tela = new TransferenciasViewModel(new ComercialApi(ambiente.Api), abertura, ambiente.Dialogos);
        string? rotaAberta = null;
        tela.AbrirTela = rota =>
        {
            rotaAberta = rota;
            return Task.CompletedTask;
        };

        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<TransferenciaDto>());
        await tela.CarregarCommand.ExecuteAsync(null);

        ambiente.Servidor.Responder(HttpStatusCode.OK, Opcoes());
        await tela.NovoCommand.ExecuteAsync(null);
        var a = tela.Assistente!;
        a.Origem = a.Origens.First(o => o.Valor == Joao);
        await tela.AvancarCommand.ExecuteAsync(null);
        Assert.True(a.NoPasso2);

        a.NovoDestino = a.DestinosDisponiveis.First(o => o.Valor == Maria);
        a.IncluirDestinoCommand.Execute(null);
        a.Motivo = "Desligamento";
        var previa = Previa();
        previa.Itens = [.. previa.Itens.Select(i => { i.DestinoId = Maria; return i; })];
        ambiente.Servidor.Responder(HttpStatusCode.OK, previa);
        await tela.AvancarCommand.ExecuteAsync(null);
        Assert.True(a.NoPasso3);
        Assert.EndsWith(Rotas.Comercial.TransferenciasPrevia, ambiente.Servidor.Recebidas[^1].Caminho);

        var feita = new TransferenciaDto
        {
            Id = Guid.NewGuid(), Numero = "TR-2026-0001", Origem = "João", EfeitoEm = Hoje, Motivo = "Desligamento", Usuario = "Admin",
            Transferidos = 2, NaoProcessados = 1, Concluida = true, Itens = previa.Itens
        };
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, feita)
            .Responder(HttpStatusCode.OK, new List<TransferenciaDto> { feita });
        await tela.TransferirCommand.ExecuteAsync(null);

        Assert.Contains(ambiente.Dialogos.Perguntas, p => p.StartsWith("Transferir 2 clientes de João para Maria"));
        var enviado = JsonSerializer.Deserialize<TransferenciaRequisicao>(ambiente.Servidor.Recebidas[^2].Corpo, OpcoesJson.Padrao)!;
        Assert.Equal(2, enviado.Clientes!.Count);
        Assert.Null(tela.Assistente);
        Assert.Equal("Transferência TR-2026-0001", tela.Resultado!.Titulo);
        Assert.Equal(3, tela.Resultado.Linhas.Count);
        Assert.False(tela.TemAlteracoes);
        Assert.Single(tela.Itens);

        // "Abrir ficha": pede a pessoa e vai para Pessoas.
        await tela.AbrirClienteCommand.ExecuteAsync(tela.Resultado.Linhas[0]);
        Assert.Equal(AberturaDePessoa.RotaPessoas, rotaAberta);
        Assert.Equal(tela.Resultado.Linhas[0].ClienteId, abertura.Retirar());
        Assert.Null(abertura.Retirar()); // um pedido de cada vez
    }

    [Fact]
    public void Carteira_em_uma_data_mostra_papel_credito_origem_e_ausencia()
    {
        var v = new VinculoEmDataDto
        {
            ClienteId = ClienteA, Cliente = "ABC", Papel = "Vendedor", Pessoa = "Maria", InicioEm = new DateOnly(2026, 10, 1),
            Origem = OrigemVinculoCarteira.Transferencia, Transferencia = "TR-2026-0001", TipoCredito = TipoCreditoComercial.Receita,
            Credito = 70, Cobertura = "Férias de 03/10/2026 a 11/10/2026 · atendimento por Pedro (crédito do titular)"
        };

        var porCliente = new LinhaVinculoEmData(v, porCliente: true);
        Assert.Equal("Vendedor: Maria", porCliente.Titulo);
        Assert.Equal("desde 01/10/2026 · transferência TR-2026-0001", porCliente.Detalhe);
        Assert.Equal("crédito de receita: 70%", porCliente.Credito);
        Assert.StartsWith("Ausente: Férias", porCliente.Cobertura);

        var porPessoa = new LinhaVinculoEmData(v, porCliente: false);
        Assert.Equal("ABC", porPessoa.Titulo);
        Assert.StartsWith("Vendedor · desde", porPessoa.Detalhe);
        Assert.False(porPessoa.TemCredito); // o crédito do dia só vem na consulta por cliente
    }
}
