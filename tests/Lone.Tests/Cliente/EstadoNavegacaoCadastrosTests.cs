using System.Net;
using System.Text.Json;
using Lone.Cliente.Api;
using Lone.Cliente.Navegacao;
using Lone.Cliente.ViewModels;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Comum;
using Lone.Contracts.Etiquetas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>
/// Navegação Fase 3 — preservação de contexto nas telas reais: Pessoas (piloto completo: aba, filtros, ordenação, página,
/// pesquisa, ficha, aba da ficha, prévia, rolagens) e os demais cadastros (estado genérico: pesquisa e ficha). O que o
/// usuário descartou nunca volta; o que deixou de valer é ignorado sem erro; restaurar duas vezes dá no mesmo.
/// </summary>
public class EstadoNavegacaoCadastrosTests
{
    private static readonly string CaminhoLista = "/" + Rotas.Pessoas.Pagina;

    private static CatalogoFiltrosPessoasDto Catalogo() => new()
    {
        Campos =
        [
            new() { Id = CamposFiltroPessoas.Uf, Grupo = "Endereços", Nome = "UF", Tipo = TipoCampoFiltro.Lista,
                    Operadores = [OperadorFiltro.UmDestes, OperadorFiltro.NenhumDestes],
                    Opcoes = [new("MG", "MG"), new("SP", "SP")] }
        ]
    };

    private static CondicaoFiltro Uf(params string[] valores) =>
        new() { Campo = CamposFiltroPessoas.Uf, Operador = OperadorFiltro.UmDestes, Valores = valores.ToList() };

    private static PessoaResumo Resumo(Guid id, string nome) =>
        new() { Id = id, Codigo = 7, Nome = nome, Natureza = NaturezaPessoa.Fisica, Situacao = SituacaoPessoa.Ativo };

    private static PessoaDto Ficha(Guid id, string nome, string apelido = "") =>
        new() { Id = id, Codigo = 7, Nome = nome, Apelido = apelido, Natureza = NaturezaPessoa.Fisica };

    private static PaginaListaPessoas Pagina(int total, int pagina, params PessoaResumo[] itens) =>
        new() { Total = total, Pagina = pagina, TamanhoPagina = PaginaListaPessoas.TamanhoPadrao, Itens = itens.ToList() };

    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente)> PessoasAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao()); // administrador
        return await PessoasViewModelTests.AbrirTelaComAsync(ambiente, Catalogo());
    }

    private static int Leituras(AmbienteCliente ambiente, string caminho, int desde = 0) =>
        ambiente.Servidor.Recebidas.Skip(desde).Count(r => r.Caminho == caminho);

    private static string Json(object valor) => JsonSerializer.Serialize(valor);

    // ---- Pessoas: conjunto completo ----

    [Fact]
    public async Task Pessoas_volta_com_todo_o_contexto_numa_leitura_so()
    {
        var (tela, ambiente) = await PessoasAsync();
        var bruno = Guid.NewGuid();
        var estado = new EstadoPessoas
        {
            Busca = "João",
            AbaRapida = FiltroRapido.Fisicas,
            Condicoes = [Uf("MG")],
            OrdenacaoColuna = ColunasPessoas.Nome,
            OrdenacaoDirecao = DirecaoOrdenacao.Decrescente,
            Pagina = 3,
            Registro = new ReferenciaRegistro(PessoasViewModel.TipoPessoa, bruno, "Bruno"),
            Secao = SecaoPessoa.Contatos,
            Rolagens = new Dictionary<string, PosicaoRolagem> { ["lista"] = new(0, 480), ["ficha"] = new(0, 120) }
        };
        var pedidasRolagem = new List<string>();
        tela.Rolagem.RestauracaoPedida += (_, chave) => pedidasRolagem.Add(chave);
        var antes = ambiente.Servidor.Recebidas.Count;
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, Pagina(150, 3, Resumo(bruno, "Bruno")))  // a lista, uma vez, com tudo aplicado
            .Responder(HttpStatusCode.OK, Ficha(bruno, "Bruno"));                    // a ficha

        await tela.RestaurarEstadoAsync(estado);

        Assert.Equal(1, Leituras(ambiente, CaminhoLista, antes)); // uma releitura só, no fim do contexto da lista
        var lista = ambiente.Servidor.Recebidas.Skip(antes).First(r => r.Caminho == CaminhoLista);
        Assert.Contains("MG", lista.Corpo);
        Assert.Equal("João", tela.Busca);
        Assert.True(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Fisicas).Selecionado);
        Assert.Equal(Json(new[] { Uf("MG") }), Json(tela.Filtros.Condicoes()));
        Assert.Equal(ColunasPessoas.Nome, tela.Grade.Ordenacao!.Coluna);
        Assert.Equal(DirecaoOrdenacao.Decrescente, tela.Grade.Ordenacao.Direcao);
        Assert.Equal(3, tela.PaginaAtual);
        Assert.True(tela.Editando);
        Assert.Equal(bruno, tela.Formulario!.Id);
        Assert.Equal(SecaoPessoa.Contatos, tela.SecaoSelecionada!.Secao);
        Assert.Equal(new[] { "lista", "ficha" }, pedidasRolagem.ToArray()); // por último, com lista e ficha prontas
        Assert.Equal(480, tela.Rolagem.Pendente("lista")!.Y);
        Assert.Equal(120, tela.Rolagem.Pendente("ficha")!.Y);
        Assert.False(tela.VisaoAlterada);
        Assert.False(tela.TemAlteracoes);

        // O que a tela entrega de novo ao sair é o mesmo contexto.
        var capturado = Assert.IsType<EstadoPessoas>(tela.CapturarEstado());
        Assert.Equal("João", capturado.Busca);
        Assert.Equal(FiltroRapido.Fisicas, capturado.AbaRapida);
        Assert.Equal(Json(new[] { Uf("MG") }), Json(capturado.Condicoes));
        Assert.Equal(3, capturado.Pagina);
        Assert.Equal(SecaoPessoa.Contatos, capturado.Secao);
        Assert.Equal(bruno, capturado.Registro!.Id);
        Assert.Equal(ColunasPessoas.Nome, capturado.OrdenacaoColuna);
    }

    [Fact]
    public async Task Pessoas_restaurada_duas_vezes_nao_rele_nem_reabre_nada()
    {
        var (tela, ambiente) = await PessoasAsync();
        var bruno = Guid.NewGuid();
        var estado = new EstadoPessoas
        {
            Busca = "João", AbaRapida = FiltroRapido.Fisicas, Condicoes = [Uf("MG")], Pagina = 2,
            Registro = new ReferenciaRegistro(PessoasViewModel.TipoPessoa, bruno, "Bruno"), Secao = SecaoPessoa.Contatos
        };
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, Pagina(120, 2, Resumo(bruno, "Bruno")))
            .Responder(HttpStatusCode.OK, Ficha(bruno, "Bruno"));
        await tela.RestaurarEstadoAsync(estado);
        var depoisDaPrimeira = ambiente.Servidor.Recebidas.Count;
        var linhaNaPrevia = tela.Previa.Linha;

        await tela.RestaurarEstadoAsync(estado); // o mesmo estado outra vez

        Assert.Equal(depoisDaPrimeira, ambiente.Servidor.Recebidas.Count); // nenhuma chamada a mais
        Assert.Equal(bruno, tela.Formulario!.Id);
        Assert.Equal(SecaoPessoa.Contatos, tela.SecaoSelecionada!.Secao);
        Assert.Equal(2, tela.PaginaAtual);
        Assert.Same(linhaNaPrevia, tela.Previa.Linha); // seleção intacta
    }

    [Fact]
    public async Task Pessoas_ignora_so_o_que_deixou_de_valer_e_restaura_o_resto()
    {
        var (tela, ambiente) = await PessoasAsync();
        var estado = new EstadoPessoas
        {
            Busca = "Maria",
            AbaRapida = "aba.que.nao.existe.mais",                                 // → "Todos"
            Condicoes = [Uf("MG"), new CondicaoFiltro { Campo = "campo.removido", Operador = OperadorFiltro.Igual, Valores = ["x"] }],
            OrdenacaoColuna = "coluna.que.nao.ordena.mais",                        // → ordem padrão
            Pagina = 9,                                                             // → última página válida
            Secao = (SecaoPessoa)999                                                // (sem ficha: nada a fazer)
        };
        var antes = ambiente.Servidor.Recebidas.Count;
        ambiente.Servidor
            .Responder(HttpStatusCode.OK, Pagina(120, 9))                           // a página 9 não existe mais…
            .Responder(HttpStatusCode.OK, Pagina(120, 3, Resumo(Guid.NewGuid(), "Maria"))); // …fica na última (3)

        await tela.RestaurarEstadoAsync(estado);

        Assert.Equal("Maria", tela.Busca);
        Assert.True(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Todos).Selecionado);
        Assert.Equal(Json(new[] { Uf("MG") }), Json(tela.Filtros.Condicoes())); // só o filtro inválido ficou de fora
        Assert.Null(tela.Grade.Ordenacao);
        Assert.Equal(3, tela.PaginaAtual);
        Assert.Equal(2, Leituras(ambiente, CaminhoLista, antes));
        Assert.True(string.IsNullOrEmpty(tela.Mensagem) || tela.TipoMensagem != TipoMensagem.Erro);

        // Idempotente mesmo com o estado velho: não pede a página 9 de novo nem relê por causa do filtro removido.
        var n = ambiente.Servidor.Recebidas.Count;
        await tela.RestaurarEstadoAsync(estado);
        Assert.Equal(n, ambiente.Servidor.Recebidas.Count);
        Assert.Equal(3, tela.PaginaAtual);
    }

    [Fact]
    public async Task Pessoas_com_aba_da_ficha_que_nao_existe_mais_fica_na_padrao()
    {
        var (tela, ambiente) = await PessoasAsync();
        var bruno = Guid.NewGuid();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Ficha(bruno, "Bruno"));

        await tela.RestaurarEstadoAsync(new EstadoPessoas
        {
            Registro = new ReferenciaRegistro(PessoasViewModel.TipoPessoa, bruno, "Bruno"), Secao = (SecaoPessoa)999
        });

        Assert.True(tela.Editando);
        Assert.Equal(SecaoPessoa.Geral, tela.SecaoSelecionada!.Secao);
    }

    [Fact]
    public async Task Registro_excluido_nao_abre_e_nao_vira_erro_de_navegacao()
    {
        var (tela, ambiente) = await PessoasAsync();
        var excluido = Guid.NewGuid();
        ambiente.Servidor.Problema(HttpStatusCode.NotFound, ErrosApi.NaoEncontrado, "Não encontrado.");

        await tela.RestaurarEstadoAsync(new EstadoPessoas
        {
            Busca = "", Registro = new ReferenciaRegistro(PessoasViewModel.TipoPessoa, excluido, "Bruno")
        });

        Assert.False(tela.Editando);
        Assert.Null(tela.RegistroAberto);
        Assert.True(string.IsNullOrEmpty(tela.Mensagem)); // o mundo mudou: não é erro
        Assert.Single(tela.Itens); // a lista continua lá

        // Restaurar de novo não tenta ler o excluído outra vez.
        var n = ambiente.Servidor.Recebidas.Count;
        await tela.RestaurarEstadoAsync(new EstadoPessoas { Registro = new ReferenciaRegistro(PessoasViewModel.TipoPessoa, excluido, "Bruno") });
        Assert.Equal(n, ambiente.Servidor.Recebidas.Count);
    }

    [Fact]
    public async Task Registro_sem_permissao_nao_abre_e_nao_vira_erro_de_navegacao()
    {
        var (tela, ambiente) = await PessoasAsync();
        ambiente.Servidor.Problema(HttpStatusCode.Forbidden, ErrosApi.AcessoNegado, "Sem permissão para este cadastro.");

        await tela.RestaurarEstadoAsync(new EstadoPessoas
        {
            Registro = new ReferenciaRegistro(PessoasViewModel.TipoPessoa, Guid.NewGuid(), "Sigiloso")
        });

        Assert.False(tela.Editando);
        Assert.True(string.IsNullOrEmpty(tela.Mensagem));
    }

    [Fact]
    public async Task Pessoas_restaura_a_previa_da_pessoa_que_ainda_esta_na_pagina()
    {
        var (tela, ambiente) = await PessoasAsync();
        tela.Previa.EsperaParaLer = TimeSpan.Zero;
        tela.DefinirLarguraDaLista(1600); // janela larga: a prévia cabe ao lado
        var ana = tela.Itens[0];
        ambiente.Servidor.Responder(HttpStatusCode.OK, Ficha(ana.Id, ana.Nome)); // o resumo da prévia

        await tela.RestaurarEstadoAsync(new EstadoPessoas { PreviaId = ana.Id });
        await tela.Previa.Lendo;

        Assert.True(tela.Previa.Aberta);
        Assert.Equal(ana.Id, tela.Previa.Linha!.Pessoa.Id);
        Assert.False(tela.Editando); // prévia não é ficha
        Assert.Equal(ana.Id, Assert.IsType<EstadoPessoas>(tela.CapturarEstado()).PreviaId);

        // Pessoa que saiu da página: sem prévia (nada é lido).
        var (outra, ambiente2) = await PessoasAsync();
        var n = ambiente2.Servidor.Recebidas.Count;
        await outra.RestaurarEstadoAsync(new EstadoPessoas { PreviaId = Guid.NewGuid() });
        Assert.False(outra.Previa.Aberta);
        Assert.Equal(n, ambiente2.Servidor.Recebidas.Count);
    }

    [Fact]
    public async Task Alteracao_descartada_ao_sair_nunca_volta()
    {
        var (tela, ambiente) = await PessoasAsync();
        var bruno = Guid.NewGuid();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Ficha(bruno, "Bruno", apelido: "Teste"));
        Assert.True(await tela.IrParaRegistroAsync(new ReferenciaRegistro(PessoasViewModel.TipoPessoa, bruno, "Bruno")));
        tela.Formulario!.Nome = "Bruno XYZ"; // o nome aparece no título da ficha
        tela.Formulario.Apelido = "Teste XYZ";
        Assert.True(tela.TemAlteracoes);
        var leiturasAntes = ambiente.Servidor.Recebidas.Count;

        // Ordem real ao sair (menu ou ←): 1) o motor captura o estado; 2) o Shell pergunta; 3) "Descartar alterações".
        var estado = tela.CapturarEstado();
        ambiente.Dialogos.RespostaConfirmacao = true;
        ambiente.Servidor.Responder(HttpStatusCode.OK, Ficha(bruno, "Bruno", apelido: "Teste")); // a ficha relida
        Assert.True(await tela.PodeSairAsync());

        // Nada foi gravado: a única chamada depois da edição foi a releitura da ficha.
        var depois = ambiente.Servidor.Recebidas.Skip(leiturasAntes).ToList();
        Assert.All(depois, r => Assert.Equal(HttpMethod.Get, r.Metodo));
        Assert.Equal("Bruno", tela.Formulario!.Nome);
        Assert.Equal("Teste", tela.Formulario.Apelido);
        Assert.False(tela.TemAlteracoes);

        // O estado guardado (capturado ainda com a edição na tela) só sabe qual ficha era: nada do que foi digitado.
        Assert.DoesNotContain("XYZ", JsonSerializer.Serialize(estado, estado.GetType()));
        Assert.Equal(bruno, estado.Registro!.Id);
        Assert.Equal(string.Empty, estado.Registro.Titulo);

        // Volta para Pessoas (tela recriada): a ficha reabre com o que está gravado.
        var (nova, ambiente2) = await PessoasAsync();
        ambiente2.Servidor.Responder(HttpStatusCode.OK, Ficha(bruno, "Bruno", apelido: "Teste"));
        await nova.RestaurarEstadoAsync(estado);
        Assert.Equal("Bruno", nova.Formulario!.Nome);
        Assert.Equal("Teste", nova.Formulario.Apelido);
        Assert.Equal("Bruno", nova.RegistroAberto!.Titulo); // o título vem da ficha gravada, não do estado
        Assert.False(nova.TemAlteracoes);
    }

    [Fact]
    public async Task Continuar_editando_nao_sai_nem_descarta()
    {
        var (tela, ambiente) = await PessoasAsync();
        var bruno = Guid.NewGuid();
        ambiente.Servidor.Responder(HttpStatusCode.OK, Ficha(bruno, "Bruno", apelido: "Teste"));
        await tela.IrParaRegistroAsync(new ReferenciaRegistro(PessoasViewModel.TipoPessoa, bruno, "Bruno"));
        tela.Formulario!.Apelido = "Teste XYZ";
        ambiente.Dialogos.RespostaConfirmacao = false; // "Continuar editando"

        Assert.False(await tela.PodeSairAsync());

        Assert.Equal("Teste XYZ", tela.Formulario!.Apelido); // o que o usuário digitou continua na tela dele
        Assert.True(tela.TemAlteracoes);
    }

    // ---- Demais cadastros: estado genérico (pesquisa + ficha) ----

    private static readonly EtiquetaDto Vip = new() { Id = Guid.NewGuid(), Nome = "VIP", Ativo = true };
    private static readonly EtiquetaDto Atacado = new() { Id = Guid.NewGuid(), Nome = "Atacado", Ativo = true };

    private static async Task<(EtiquetasViewModel Tela, AmbienteCliente Ambiente)> EtiquetasAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<EtiquetaDto> { Vip, Atacado });
        var tela = new EtiquetasViewModel(new EtiquetasApi(ambiente.Api), ambiente.Dialogos);
        await tela.CarregarCommand.ExecuteAsync(null);
        return (tela, ambiente);
    }

    [Fact]
    public async Task Cadastro_generico_guarda_pesquisa_e_ficha_e_volta_com_elas()
    {
        var (tela, ambiente) = await EtiquetasAsync();
        tela.Busca = "vi";
        ambiente.Servidor.Responder(HttpStatusCode.OK, Vip);
        Assert.True(await tela.IrParaRegistroAsync(new ReferenciaRegistro("etiquetas", Vip.Id, "VIP")));

        var estado = tela.CapturarEstado();
        Assert.IsType<EstadoTela>(estado); // só o genérico: nada específico de Pessoas
        Assert.Equal("vi", estado.Busca);
        Assert.Equal(Vip.Id, estado.Registro!.Id);

        var (nova, ambiente2) = await EtiquetasAsync(); // tela recriada
        var antes = ambiente2.Servidor.Recebidas.Count;
        ambiente2.Servidor.Responder(HttpStatusCode.OK, Vip);
        await nova.RestaurarEstadoAsync(estado);

        Assert.Equal("vi", nova.Busca);
        Assert.Equal(new[] { "VIP" }, nova.Itens.Select(i => i.Nome).ToArray()); // pesquisa local: já filtrou, sem reler
        Assert.Equal(Vip.Id, nova.Formulario!.Id);
        Assert.Equal(1, ambiente2.Servidor.Recebidas.Count - antes); // só a ficha

        var n = ambiente2.Servidor.Recebidas.Count;
        await nova.RestaurarEstadoAsync(estado); // idempotente
        Assert.Equal(n, ambiente2.Servidor.Recebidas.Count);
        Assert.Equal(Vip.Id, nova.Formulario!.Id);
    }

    [Fact]
    public async Task Cadastro_generico_com_registro_que_sumiu_da_lista_fica_so_na_lista()
    {
        var (tela, ambiente) = await EtiquetasAsync();
        var antes = ambiente.Servidor.Recebidas.Count;

        await tela.RestaurarEstadoAsync(new EstadoTela { Busca = "", Registro = new ReferenciaRegistro("etiquetas", Guid.NewGuid(), "Removida") });

        Assert.False(tela.Editando);
        Assert.True(string.IsNullOrEmpty(tela.Mensagem));
        Assert.Equal(antes, ambiente.Servidor.Recebidas.Count);
        Assert.Equal(2, tela.Itens.Count);
    }

    [Fact]
    public async Task Restauracao_cancelada_antes_de_a_tela_carregar_nao_aplica_nada_depois()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var tela = new EtiquetasViewModel(new EtiquetasApi(ambiente.Api), ambiente.Dialogos); // recém-criada: ainda carregando
        var pedidas = new List<string>();
        tela.Rolagem.RestauracaoPedida += (_, chave) => pedidas.Add(chave);
        using var cancelamento = new CancellationTokenSource();
        var restaurando = tela.RestaurarEstadoAsync(new EstadoTela
        {
            Busca = "vi",
            Registro = new ReferenciaRegistro("etiquetas", Vip.Id, "VIP"),
            Rolagens = new Dictionary<string, PosicaoRolagem> { ["lista"] = new(0, 300) }
        }, cancelamento.Token);
        Assert.False(restaurando.IsCompleted); // esperando a primeira carga

        cancelamento.Cancel(); // o usuário foi para outra tela
        await restaurando;      // termina na hora (não fica esperando a carga)

        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<EtiquetaDto> { Vip, Atacado });
        await tela.CarregarCommand.ExecuteAsync(null); // a carga da tela abandonada chega depois
        await Task.Delay(50);

        Assert.Equal(string.Empty, tela.Busca);  // nada do estado foi aplicado depois
        Assert.False(tela.Editando);            // a ficha não abriu
        Assert.Empty(pedidas);                  // nenhuma rolagem pedida
        Assert.Single(ambiente.Servidor.Recebidas); // só a carga da própria tela: nenhuma leitura da restauração
    }

    [Fact]
    public async Task Restauracao_ja_cancelada_nao_comeca()
    {
        var (tela, ambiente) = await EtiquetasAsync();
        var antes = ambiente.Servidor.Recebidas.Count;

        await tela.RestaurarEstadoAsync(new EstadoTela { Busca = "vi", Registro = new ReferenciaRegistro("etiquetas", Vip.Id, "VIP") },
            new CancellationToken(canceled: true));

        Assert.Equal(string.Empty, tela.Busca);
        Assert.False(tela.Editando);
        Assert.Equal(antes, ambiente.Servidor.Recebidas.Count);
    }

    [Fact]
    public async Task Ficha_nova_nao_gravada_nao_entra_no_estado()
    {
        var (tela, _) = await EtiquetasAsync();
        await tela.NovoCommand.ExecuteAsync(null);

        Assert.Null(tela.CapturarEstado().Registro); // registro que não existe no servidor não é "reaberto"
    }
}
