using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Auditoria;
using Lone.Contracts.Comum;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

public class HistoricoTests
{
    [Fact]
    public void Proxima_pagina_do_historico_leva_o_ultimo_id()
    {
        var id = Guid.Parse("0f7a4c1e-0000-0000-0000-0197a1b2c3d4");
        Assert.Equal($"api/v1/pessoas/{id}/historico?limite=100", Rotas.Pessoas.Historico(id, null, 100));
        Assert.Equal($"api/v1/pessoas/{id}/historico?limite=100&antes=987", Rotas.Pessoas.Historico(id, 987, 100));
    }

    [Fact]
    public void Filtro_do_historico_vai_na_url_so_com_o_que_foi_escolhido()
    {
        var id = Guid.Parse("0f7a4c1e-0000-0000-0000-0197a1b2c3d4");
        Assert.Equal($"api/v1/pessoas/{id}/historico?limite=100", Rotas.Pessoas.Historico(id, null, 100, new FiltroHistorico()));
        var filtro = new FiltroHistorico
        {
            Entidades = ["PessoaEndereco"],
            DeUtc = new DateTime(2026, 9, 1, 3, 0, 0, DateTimeKind.Utc),
            AteUtc = new DateTime(2026, 10, 1, 3, 0, 0, DateTimeKind.Utc),
            Usuario = "Rafael Froés"
        };
        Assert.Equal($"api/v1/pessoas/{id}/historico?limite=500&antes=9&entidade=PessoaEndereco&de=2026-09-01T03%3A00%3A00Z" +
                     "&ate=2026-10-01T03%3A00%3A00Z&usuario=Rafael%20Fro%C3%A9s", Rotas.Pessoas.Historico(id, 9, 500, filtro));
        Assert.Equal($"api/v1/pessoas/{id}/historico/opcoes", Rotas.Pessoas.HistoricoOpcoes(id));
    }

    [Fact]
    public void Periodo_local_vira_instantes_utc_com_o_ultimo_dia_inteiro()
    {
        var filtro = PessoasViewModel.FiltroDoPeriodo("Pessoa", "maria", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
        Assert.Equal(["Pessoa"], filtro.Entidades);
        Assert.Equal("maria", filtro.Usuario);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), filtro.DeUtc);
        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), filtro.AteUtc); // fim exclusivo

        var vazio = PessoasViewModel.FiltroDoPeriodo("", "", null, null);
        Assert.True(vazio.Vazio);
    }

    [Fact]
    public void Impressao_do_historico_escapa_o_texto_e_mostra_filtro_e_motivo()
    {
        var itens = new List<HistoricoItem>
        {
            new("03/10/2026 10:10 · Rafael", "Telefone · Número: \"<1>\" → \"2\"", "Motivo: teste & cia"),
            new("02/10/2026 09:00 · Maria", "Cadastro criado")
        };
        var html = HistoricoImpressao.Html("BRADESCO <AG>", "Tipo: Telefone", itens, cortado: true, new DateTime(2026, 10, 3, 12, 0, 0));

        Assert.Contains("Histórico · BRADESCO &lt;AG&gt;", html);
        Assert.Contains("2 registros · Tipo: Telefone · gerado em 03/10/2026 12:00", html);
        Assert.Contains("&lt;1&gt;", html);
        Assert.Contains("<em>Motivo: teste &amp; cia</em>", html);
        Assert.Contains("Mostrando só os registros mais recentes", html);
        Assert.Contains("window.print()", html);
        Assert.Contains("Sem alterações registradas.", HistoricoImpressao.Html("X", "", [], false, DateTime.Now));
    }

    [Fact]
    public void Motivo_aparece_na_linha_do_historico()
    {
        var comMotivo = HistoricoItem.De(new RegistroHistorico
        {
            Id = 1, DataHora = DateTime.UtcNow, Usuario = "maria", Acao = AcaoAuditoria.Evento, Entidade = "Pessoa",
            Descricao = "Cadastro desativado.", Motivo = "pedido do cliente"
        });
        var semMotivo = HistoricoItem.De(new RegistroHistorico
        {
            Id = 2, DataHora = DateTime.UtcNow, Usuario = "maria", Acao = AcaoAuditoria.Evento, Entidade = "Pessoa", Descricao = "x"
        });

        Assert.Equal("Motivo: pedido do cliente", comMotivo.Motivo);
        Assert.True(comMotivo.TemMotivo);
        Assert.False(semMotivo.TemMotivo);
    }
}
