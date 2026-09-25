using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

public class SituacoesFormularioTests
{
    [Fact]
    public void Bloqueios_ativos_primeiro_e_liberacao_atualiza_o_item()
    {
        var s = new SituacoesFormulario();
        var liberado = new BloqueioDto { Id = Guid.NewGuid(), Escopo = EscopoBloqueio.Financeiro, Motivo = "x", InicioEm = DateTime.UtcNow.AddDays(-9), FimEm = DateTime.UtcNow.AddDays(-1), FimPor = "ana", MotivoLiberacao = "pago" };
        var ativo = new BloqueioDto { Id = Guid.NewGuid(), Escopo = EscopoBloqueio.Faturamento, Motivo = "sem IE", InicioEm = DateTime.UtcNow.AddDays(-2), InicioPor = "ana" };

        s.Carregar([liberado, ativo], new RelacionamentoDto { Situacao = SituacaoRelacionamento.SemInteracao });

        Assert.True(s.TemBloqueioAtivo);
        Assert.Equal(ativo.Id, s.Bloqueios[0].Id);
        Assert.Equal("Faturamento (notas)", s.Bloqueios[0].Titulo);
        Assert.Equal("Nenhuma interação registrada.", s.TextoRelacionamento);

        ativo.FimEm = DateTime.UtcNow;
        s.Liberado(s.Bloqueios[0], ativo);
        Assert.False(s.TemBloqueioAtivo);
    }

    [Fact]
    public void Interacao_registrada_entra_no_topo_e_limpa_o_campo()
    {
        var s = new SituacoesFormulario { NovaDescricao = "Ligou pedindo tabela" };
        s.IncluirInteracao(new InteracaoDto { Id = Guid.NewGuid(), DataHora = DateTime.UtcNow, Tipo = TipoInteracao.Ligacao, Descricao = "Ligou pedindo tabela", Usuario = "ana" });

        Assert.Equal("Ligou pedindo tabela", s.Interacoes[0].Descricao);
        Assert.StartsWith("Ligação · ", s.Interacoes[0].Detalhe);
        Assert.Equal(string.Empty, s.NovaDescricao);
        Assert.StartsWith("Relacionamento ativo", s.TextoRelacionamento);
    }
}
