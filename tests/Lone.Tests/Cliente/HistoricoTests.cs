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
