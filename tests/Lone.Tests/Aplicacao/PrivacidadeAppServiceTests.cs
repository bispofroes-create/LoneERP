using Lone.Application.Privacidade;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Privacidade;

namespace Lone.Tests.Aplicacao;

/// <summary>A privacidade apresentada à tela vem das regras do domínio (o serviço não decide nada por conta própria).</summary>
public class PrivacidadeAppServiceTests
{
    private static List<FinalidadeTratamento> Finalidades() => FinalidadesTratamentoIniciais.Todas.Select(i => new FinalidadeTratamento
    {
        Id = i.Id, Codigo = i.Codigo, Nome = i.Nome, BaseLegal = i.BaseLegal, ClassificacaoExigida = i.ClassificacaoExigida,
        SomenteHistorico = i.SomenteHistorico, Ordem = i.Ordem, DoSistema = true, Ativo = true
    }).ToList();

    [Fact]
    public void Montar_mostra_situacao_por_finalidade_historico_e_decisao_por_canal()
    {
        var finalidades = Finalidades();
        var marketing = finalidades.Single(f => f.Codigo == FinalidadesTratamentoIniciais.Marketing);
        var anterior = finalidades.Single(f => f.Codigo == FinalidadesTratamentoIniciais.RegistroAnterior);
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Ana" };
        var email = new MeioContato { Id = Guid.NewGuid(), PessoaId = p.Id, Tipo = TipoContato.Email, Valor = "ana@exemplo.com.br", Finalidades = FinalidadeEmail.Marketing };
        var inativo = new MeioContato { Id = Guid.NewGuid(), PessoaId = p.Id, Tipo = TipoContato.Celular, Valor = "31987654321", Ativo = false };
        p.MeiosContato.AddRange([email, inativo]);
        p.Consentimentos.Add(new PessoaConsentimento { Id = Guid.NewGuid(), PessoaId = p.Id, FinalidadeId = anterior.Id, Canal = CanalComunicacao.Email, Concedido = true });

        var semConsentimento = PrivacidadeAppService.Montar(p, finalidades);

        var linha = Assert.Single(semConsentimento.Finalidades); // "Registro anterior" não aparece como finalidade normal
        Assert.Equal(marketing.Id, linha.FinalidadeId);
        Assert.Equal(SituacaoConsentimento.NaoInformado, linha.Situacao); // o registro anterior não conta
        Assert.True(Assert.Single(semConsentimento.Consentimentos).SomenteHistorico);
        var canal = Assert.Single(semConsentimento.Canais); // só os ativos
        Assert.True(canal.UsoParaMarketing);
        Assert.Equal(ResultadoComunicacao.BloqueadoPorConsentimento, Assert.Single(canal.Decisoes).Resultado);

        p.Consentimentos.Add(RegrasConsentimento.Conceder(Guid.NewGuid(), p.Id, marketing, null, p.Consentimentos,
            new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc), "Maria", "Balcão", null, null));
        var comConsentimento = PrivacidadeAppService.Montar(p, finalidades);

        Assert.Equal(SituacaoConsentimento.Concedido, Assert.Single(comConsentimento.Finalidades).Situacao);
        Assert.Equal(ResultadoComunicacao.Permitido, Assert.Single(Assert.Single(comConsentimento.Canais).Decisoes).Resultado);
        Assert.True(comConsentimento.Consentimentos[0].EmVigor && !comConsentimento.Consentimentos[0].SomenteHistorico); // em vigor primeiro
    }
}
