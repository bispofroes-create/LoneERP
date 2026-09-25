using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Tests.Dominio;

public class RelacionamentoTests
{
    private static readonly DateTime Agora = new(2026, 9, 25, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, SituacaoRelacionamento.SemInteracao)]
    [InlineData(10, SituacaoRelacionamento.Ativo)]
    [InlineData(90, SituacaoRelacionamento.EmRisco)]
    [InlineData(180, SituacaoRelacionamento.Inativo)]
    public void Situacao_pelos_dias_sem_interacao(int? dias, SituacaoRelacionamento esperada)
    {
        var parametros = new ParametrosRelacionamento();
        DateTime? ultima = dias is { } d ? Agora.AddDays(-d) : null;
        Assert.Equal(esperada, parametros.Situacao(ultima, Agora));
    }

    [Fact]
    public void Inativo_precisa_ser_maior_que_em_risco()
    {
        Assert.NotEmpty(new ParametrosRelacionamento { DiasEmRisco = 90, DiasInativo = 60 }.Validar());
        Assert.Empty(new ParametrosRelacionamento { DiasEmRisco = 30, DiasInativo = 60 }.Validar());
    }
}
