using System.Net;
using System.Text.Json;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Comum;
using Lone.Contracts.Menu;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>Faixa de indicadores acima da lista de pessoas (Etapa 3) e a regra única de "pendência cadastral".</summary>
public class IndicadoresListaTests
{
    private static CondicaoFiltro Sim(string campo) => new() { Campo = campo, Operador = OperadorFiltro.Sim };

    /// <summary>Catálogo com os campos dos indicadores e os números (como a API manda ao abrir a tela).</summary>
    private static CatalogoFiltrosPessoasDto Catalogo() => new()
    {
        Campos =
        [
            new() { Id = CamposFiltroPessoas.Bloqueado, Grupo = "Situação", Nome = "Com bloqueio ativo", Tipo = TipoCampoFiltro.SimNao,
                    Operadores = [OperadorFiltro.Sim, OperadorFiltro.Nao] },
            new() { Id = CamposFiltroPessoas.ComPendenciaCadastral, Grupo = "Cadastro", Nome = "Com pendência cadastral",
                    Tipo = TipoCampoFiltro.SimNao, Operadores = [OperadorFiltro.Sim, OperadorFiltro.Nao] }
        ],
        Indicadores =
        [
            new() { Id = IndicadoresPessoas.ComBloqueio, Nome = "Com bloqueio", Alerta = true, Total = 12, Condicao = Sim(CamposFiltroPessoas.Bloqueado) },
            new() { Id = IndicadoresPessoas.DocumentosVencidos, Nome = "Documentos vencidos", Alerta = true, Total = 0,
                    Condicao = Sim(CamposFiltroPessoas.DocumentosVencidos) },
            new() { Id = IndicadoresPessoas.ComPendencia, Nome = "Com pendência cadastral", Total = 1248,
                    Condicao = Sim(CamposFiltroPessoas.ComPendenciaCadastral) }
        ]
    };

    private static async Task<(PessoasViewModel Tela, AmbienteCliente Ambiente)> AbrirAsync()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao()); // administrador
        return await PessoasViewModelTests.AbrirTelaComAsync(ambiente, Catalogo());
    }

    private static async Task EsperarRequisicoesAsync(AmbienteCliente ambiente, int quantas)
    {
        for (var i = 0; i < 300 && ambiente.Servidor.Recebidas.Count < quantas; i++) await Task.Delay(10);
    }

    private static PaginaListaPessoas Pagina() => new()
    {
        Itens = [new() { Id = Guid.NewGuid(), Codigo = 1, Nome = "Ana", Natureza = NaturezaPessoa.Fisica }], Total = 1
    };

    [Fact]
    public async Task Faixa_vem_com_o_catalogo_sem_ida_a_mais_ao_servidor()
    {
        var (tela, ambiente) = await AbrirAsync();

        Assert.True(tela.Indicadores.Visivel);
        Assert.Equal(new[] { "Com bloqueio", "Documentos vencidos", "Com pendência cadastral" }, tela.Indicadores.Itens.Select(i => i.Nome).ToArray());
        Assert.Equal("1.248", tela.Indicadores.Itens[2].TextoTotal);
        Assert.True(tela.Indicadores.Itens[0].EhAlerta);
        Assert.True(tela.Indicadores.Itens[1].Zerado); // zero: apagado, mas à vista
        Assert.False(tela.Indicadores.Itens[1].EhAlerta);
        Assert.True(tela.Indicadores.Itens[2].EhAtencao);
        Assert.DoesNotContain(ambiente.Servidor.Recebidas, r => r.Caminho == "/" + Rotas.Pessoas.Indicadores);
    }

    [Fact]
    public async Task Tocar_no_indicador_poe_a_condicao_no_painel_e_tocar_de_novo_tira()
    {
        var (tela, ambiente) = await AbrirAsync();
        var bloqueio = tela.Indicadores.Itens[0];
        var antes = ambiente.Servidor.Recebidas.Count;

        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina());
        bloqueio.AlternarCommand.Execute(null);
        Assert.True(bloqueio.Marcado);
        Assert.Contains(tela.Filtros.Condicoes(), c => c.Campo == CamposFiltroPessoas.Bloqueado && c.Operador == OperadorFiltro.Sim);
        Assert.Single(tela.Filtros.Chips);
        await EsperarRequisicoesAsync(ambiente, antes + 1); // a lista é relida com a condição
        Assert.Contains(CamposFiltroPessoas.Bloqueado, ambiente.Servidor.Recebidas.Last().Corpo);

        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina());
        bloqueio.AlternarCommand.Execute(null);
        Assert.False(bloqueio.Marcado);
        Assert.Empty(tela.Filtros.Condicoes());
        await EsperarRequisicoesAsync(ambiente, antes + 2);
    }

    [Fact]
    public async Task Indicador_sem_o_campo_no_catalogo_avisa_e_nao_filtra()
    {
        var (tela, _) = await AbrirAsync();
        var vencidos = tela.Indicadores.Itens[1]; // o catálogo do teste não tem "documentos vencidos"

        vencidos.AlternarCommand.Execute(null);

        Assert.False(vencidos.Marcado);
        Assert.Empty(tela.Filtros.Condicoes());
        Assert.Contains("Documentos vencidos", tela.Mensagem);
    }

    [Fact]
    public async Task Indicadores_sao_recontados_no_maximo_uma_vez_por_intervalo()
    {
        var (tela, ambiente) = await AbrirAsync();
        tela.IntervaloIndicadores = TimeSpan.Zero;

        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina());
        ambiente.Servidor.Responder(HttpStatusCode.OK, new List<IndicadorPessoasDto>
        {
            new() { Id = IndicadoresPessoas.ComBloqueio, Nome = "Com bloqueio", Alerta = true, Total = 13, Condicao = Sim(CamposFiltroPessoas.Bloqueado) }
        });
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Fisicas));
        await tela.AtualizandoIndicadores;

        Assert.Equal("/" + Rotas.Pessoas.Indicadores, ambiente.Servidor.Recebidas.Last().Caminho);
        Assert.Equal("13", Assert.Single(tela.Indicadores.Itens).TextoTotal);

        // Com o intervalo normal, trocar de aba de novo não reconta.
        tela.IntervaloIndicadores = TimeSpan.FromMinutes(1);
        ambiente.Servidor.Responder(HttpStatusCode.OK, Pagina());
        await tela.EscolherFiltroRapidoCommand.ExecuteAsync(tela.FiltrosRapidos.Single(f => f.Chave == FiltroRapido.Juridicas));
        Assert.Equal("/" + Rotas.Pessoas.Pagina, ambiente.Servidor.Recebidas.Last().Caminho);
    }

    [Fact]
    public async Task Tirar_indicador_e_esconder_a_faixa_ficam_na_preferencia()
    {
        var (tela, ambiente) = await AbrirAsync();
        tela.EsperaParaSalvarColunas = TimeSpan.Zero;

        ambiente.Dialogos.RespostaEscolha = "Tirar da faixa: Documentos vencidos";
        ambiente.Servidor.Responder(HttpStatusCode.NoContent);
        await tela.MenuIndicadoresCommand.ExecuteAsync(null);
        await tela.SalvandoColunas;
        Assert.DoesNotContain(tela.Indicadores.Itens, i => i.Id == IndicadoresPessoas.DocumentosVencidos);
        Assert.Equal(new[] { IndicadoresPessoas.DocumentosVencidos }, Layout(ambiente).IndicadoresOcultos!.ToArray());

        ambiente.Dialogos.RespostaEscolha = "Esconder a faixa de indicadores";
        ambiente.Servidor.Responder(HttpStatusCode.NoContent);
        await tela.MenuIndicadoresCommand.ExecuteAsync(null);
        await tela.SalvandoColunas;
        Assert.False(tela.Indicadores.Visivel);
        Assert.True(Layout(ambiente).SemIndicadores);
    }

    private static LayoutListaPessoas Layout(AmbienteCliente ambiente)
    {
        var corpo = ambiente.Servidor.Recebidas.Last(r => r.Caminho == "/" + Rotas.Menu.Tela(ColunasPessoas.TelaLista)).Corpo;
        var preferencia = JsonSerializer.Deserialize<PreferenciaTelaDto>(corpo, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return JsonSerializer.Deserialize<LayoutListaPessoas>(preferencia.Conteudo)!;
    }

    /// <summary>
    /// Regra única: cada pendência que o servidor conta em "Com pendência cadastral" aparece no resumo da pessoa. Uma
    /// pendência nova no enum sem exemplo aqui faz o teste falhar (é preciso mostrá-la no resumo também).
    /// </summary>
    [Fact]
    public void Cada_pendencia_cadastral_contada_aparece_no_resumo_da_pessoa()
    {
        var endereco = Guid.NewGuid();
        var exemplos = new Dictionary<PendenciaCadastral, (PessoaDto Pessoa, string Texto)>
        {
            [PendenciaCadastral.SemCpfCnpj] = (new() { Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica }, "CPF não informado"),
            [PendenciaCadastral.SemEndereco] = (new() { Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica }, "Nenhum endereço"),
            [PendenciaCadastral.MunicipioACorrigir] = (new()
            {
                Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica,
                Enderecos = [new() { Id = endereco, Logradouro = "Rua Direita" }],
                PendenciasMunicipio = [new() { RegistroId = endereco, TextoOriginal = "Curvelo", UfOriginal = "MG" }]
            }, "Endereço com município a corrigir"),
            [PendenciaCadastral.ContribuinteSemIE] = (new()
            {
                Id = Guid.NewGuid(), Nome = "Froés Ltda", Natureza = NaturezaPessoa.Juridica,
                Estabelecimentos = [new() { Id = Guid.NewGuid(), Principal = true, Cnpj = "11222333000181", IndicadorIE = IndicadorIE.Contribuinte }]
            }, "Contribuinte do ICMS sem inscrição estadual")
        };
        Assert.Equal(Enum.GetValues<PendenciaCadastral>().Order(), exemplos.Keys.Order());

        foreach (var (pendencia, (pessoa, texto)) in exemplos)
        {
            var resumo = new ResumoPessoa();
            resumo.Atualizar(PessoaFormulario.De(pessoa), _ => { });
            var cadastro = resumo.Blocos.Single(b => b.Titulo == "Cadastro").Itens;
            Assert.True(cadastro.Any(i => i.Texto == texto), $"{pendencia}: o resumo não mostra \"{texto}\".");
        }
    }
}
