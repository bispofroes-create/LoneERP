using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Privacidade;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

/// <summary>
/// Fase 3 — Privacidade. Consentimento AUTORIZA a finalidade, "Aceita comunicações" RESTRINGE o canal,
/// "Uso para marketing" CLASSIFICA o canal. Nenhum substitui nem altera o outro.
/// </summary>
public class PrivacidadeTests
{
    private static readonly DateTime Agora = new(2026, 9, 26, 15, 0, 0, DateTimeKind.Utc);

    private static FinalidadeTratamento Marketing() => Inicial(FinalidadesTratamentoIniciais.Marketing);
    private static FinalidadeTratamento RegistroAnterior() => Inicial(FinalidadesTratamentoIniciais.RegistroAnterior);

    private static FinalidadeTratamento Inicial(string codigo)
    {
        var i = FinalidadesTratamentoIniciais.Todas.Single(t => t.Codigo == codigo);
        return new FinalidadeTratamento
        {
            Id = i.Id, Codigo = i.Codigo, Nome = i.Nome, BaseLegal = i.BaseLegal, ClassificacaoExigida = i.ClassificacaoExigida,
            SomenteHistorico = i.SomenteHistorico, Ordem = i.Ordem, DoSistema = true, Ativo = true
        };
    }

    /// <summary>Pessoa com um e-mail e um celular (com WhatsApp).</summary>
    private static (Pessoa Pessoa, MeioContato Email, MeioContato Celular) Cadastro(bool aceita = true, bool marketing = true)
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Ana", Situacao = SituacaoPessoa.Ativo };
        var email = new MeioContato
        {
            Id = Guid.NewGuid(), PessoaId = p.Id, Tipo = TipoContato.Email, Valor = "ana@exemplo.com.br",
            PermiteComunicacao = aceita, Finalidades = marketing ? FinalidadeEmail.Marketing : FinalidadeEmail.Nenhuma
        };
        var celular = new MeioContato
        {
            Id = Guid.NewGuid(), PessoaId = p.Id, Tipo = TipoContato.Celular, Valor = "31987654321", WhatsApp = true, PermiteComunicacao = aceita
        };
        p.MeiosContato.AddRange([email, celular]);
        return (p, email, celular);
    }

    private static PessoaConsentimento Conceder(Pessoa p, FinalidadeTratamento f, CanalComunicacao? canal = null, DateTime? quando = null)
    {
        var c = RegrasConsentimento.Conceder(Guid.NewGuid(), p.Id, f, canal, p.Consentimentos, quando ?? Agora, "Maria", "Autorizou no balcão", "v1.0", "Balcão");
        p.Consentimentos.Add(c);
        return c;
    }

    private static ResultadoComunicacao Decidir(Pessoa p, MeioContato meio, FinalidadeTratamento f, CanalComunicacao canal = CanalComunicacao.Email) =>
        RegrasComunicacao.PodeComunicar(p, meio.Id, canal, f).Resultado;

    // ------------------------------------------------------------------ Matriz mínima (seção 33)

    [Theory]
    [InlineData(false, false, false, true, false, ResultadoComunicacao.BloqueadoPorConsentimento)]
    [InlineData(false, true, true, true, false, ResultadoComunicacao.BloqueadoPorConsentimento)]
    [InlineData(true, false, true, true, false, ResultadoComunicacao.BloqueadoPeloCanal)]
    [InlineData(true, true, false, true, false, ResultadoComunicacao.BloqueadoPorFinalidade)]
    [InlineData(true, true, true, true, false, ResultadoComunicacao.Permitido)]
    [InlineData(true, true, true, false, false, ResultadoComunicacao.BloqueadoPorPessoaInativa)]
    [InlineData(true, true, true, true, true, ResultadoComunicacao.BloqueadoPorCanalInativo)]
    public void Matriz_minima(bool consentimento, bool aceitaComunicacoes, bool usoMarketing, bool pessoaAtiva, bool canalInativo,
                              ResultadoComunicacao esperado)
    {
        var (p, email, _) = Cadastro(aceitaComunicacoes, usoMarketing);
        if (!pessoaAtiva) p.Situacao = SituacaoPessoa.Inativo;
        if (canalInativo) email.Ativo = false;
        var marketing = Marketing();
        if (consentimento) Conceder(p, marketing);

        Assert.Equal(esperado, Decidir(p, email, marketing));
    }

    // ------------------------------------------------------------------ Todos os resultados da regra central

    [Fact]
    public void Pessoa_arquivada_tambem_bloqueia_e_os_bloqueios_comerciais_nao_entram()
    {
        var (p, email, _) = Cadastro();
        var marketing = Marketing();
        Conceder(p, marketing);
        p.Bloqueios.Add(new Bloqueio { Id = Guid.NewGuid(), PessoaId = p.Id, Escopo = EscopoBloqueio.Comercial, Motivo = "Atraso", InicioEm = Agora, InicioPor = "Maria" });
        Assert.Equal(ResultadoComunicacao.Permitido, Decidir(p, email, marketing)); // bloqueio comercial não é regra de comunicação

        p.Situacao = SituacaoPessoa.Arquivado;
        Assert.Equal(ResultadoComunicacao.BloqueadoPorPessoaInativa, Decidir(p, email, marketing));
        Assert.Equal(ResultadoComunicacao.BloqueadoPorPessoaInativa, RegrasComunicacao.PodeComunicar(null, email.Id, CanalComunicacao.Email, marketing).Resultado);
    }

    [Fact]
    public void Pessoa_em_analise_nao_e_inativa()
    {
        var (p, email, _) = Cadastro();
        var marketing = Marketing();
        Conceder(p, marketing);
        p.Situacao = SituacaoPessoa.EmAnalise;
        Assert.Equal(ResultadoComunicacao.Permitido, Decidir(p, email, marketing));
    }

    [Fact]
    public void Canal_que_nao_e_da_pessoa_ou_nao_combina_com_o_meio_e_bloqueado_pelo_canal()
    {
        var (p, email, celular) = Cadastro();
        var marketing = Marketing();
        Conceder(p, marketing);

        Assert.Equal(ResultadoComunicacao.BloqueadoPeloCanal, RegrasComunicacao.PodeComunicar(p, Guid.NewGuid(), CanalComunicacao.Email, marketing).Resultado);
        Assert.Equal(ResultadoComunicacao.BloqueadoPeloCanal, Decidir(p, email, marketing, CanalComunicacao.WhatsApp)); // e-mail não é WhatsApp
        Assert.Equal(ResultadoComunicacao.BloqueadoPeloCanal, Decidir(p, celular, marketing, CanalComunicacao.Sms)); // celular sem SMS marcado
        Assert.Equal(ResultadoComunicacao.BloqueadoPeloCanal, Decidir(p, email, marketing, CanalComunicacao.Correspondencia));
    }

    [Fact]
    public void Finalidade_inexistente_desativada_ou_registro_anterior_bloqueia_pela_finalidade()
    {
        var (p, email, _) = Cadastro();
        var marketing = Marketing();
        Conceder(p, marketing);

        Assert.Equal(ResultadoComunicacao.BloqueadoPorFinalidade, RegrasComunicacao.PodeComunicar(p, email.Id, CanalComunicacao.Email, null).Resultado);
        Assert.Equal(ResultadoComunicacao.BloqueadoPorFinalidade, Decidir(p, email, RegistroAnterior()));
        marketing.Ativo = false;
        Assert.Equal(ResultadoComunicacao.BloqueadoPorFinalidade, Decidir(p, email, marketing));
    }

    [Theory]
    [InlineData(BaseLegal.NaoDefinida)]
    [InlineData(BaseLegal.ExecucaoContrato)]
    [InlineData(BaseLegal.ObrigacaoLegal)]
    [InlineData(BaseLegal.LegitimoInteresse)]
    [InlineData(BaseLegal.ExercicioRegularDireitos)]
    public void Base_legal_sem_regra_no_Lone_responde_sem_base_legal_aplicavel(BaseLegal baseLegal)
    {
        var (p, email, _) = Cadastro();
        var futura = new FinalidadeTratamento { Id = Guid.NewGuid(), Codigo = "FUTURA", Nome = "Futura", BaseLegal = baseLegal, Ativo = true };
        Assert.Equal(ResultadoComunicacao.SemBaseLegalAplicavel, Decidir(p, email, futura));
    }

    [Fact]
    public void Decisao_traz_o_motivo()
    {
        var (p, email, _) = Cadastro(aceita: false);
        var marketing = Marketing();
        Conceder(p, marketing);
        var decisao = RegrasComunicacao.PodeComunicar(p, email.Id, CanalComunicacao.Email, marketing);
        Assert.False(decisao.Permitido);
        Assert.Contains("não aceita comunicações", decisao.Motivo);
    }

    // ------------------------------------------------------------------ Marketing (classificação ≠ autorização)

    [Fact]
    public void Marketing_classifica_mas_nao_concede_consentimento()
    {
        var (p, email, _) = Cadastro(marketing: true);
        var marketing = Marketing();

        Assert.Equal(ResultadoComunicacao.BloqueadoPorConsentimento, Decidir(p, email, marketing));
        Assert.Empty(p.Consentimentos); // a marca não cria consentimento
        Assert.Equal(SituacaoConsentimento.NaoInformado, RegrasConsentimento.Situacao(p.Consentimentos, marketing, CanalComunicacao.Email).Situacao);
    }

    [Fact]
    public void Consentimento_nao_ativa_marketing_nem_aceita_comunicacoes()
    {
        var (p, email, _) = Cadastro(aceita: false, marketing: false);
        var marketing = Marketing();
        Conceder(p, marketing);
        var decisao = RegrasComunicacao.PodeComunicar(p, email.Id, CanalComunicacao.Email, marketing);

        Assert.Equal(ResultadoComunicacao.BloqueadoPeloCanal, decisao.Resultado);
        Assert.False(email.PermiteComunicacao); // nada foi mudado pela regra nem pelo consentimento
        Assert.Equal(FinalidadeEmail.Nenhuma, email.Finalidades);
    }

    [Fact]
    public void Marketing_so_existe_no_email_telefone_nao_e_classificado()
    {
        var (p, _, celular) = Cadastro();
        var marketing = Marketing();
        Conceder(p, marketing);
        Assert.Equal(ResultadoComunicacao.BloqueadoPorFinalidade, Decidir(p, celular, marketing, CanalComunicacao.WhatsApp));
    }

    [Fact]
    public void Aceita_comunicacoes_restringe_qualquer_base_legal_mas_nao_autoriza()
    {
        var (p, email, _) = Cadastro(aceita: true);
        Assert.Equal(ResultadoComunicacao.BloqueadoPorConsentimento, Decidir(p, email, Marketing())); // aceitar não autoriza

        email.PermiteComunicacao = false; // alteração da preferência do canal
        var marketing = Marketing();
        Conceder(p, marketing);
        Assert.Equal(ResultadoComunicacao.BloqueadoPeloCanal, Decidir(p, email, marketing));
        email.PermiteComunicacao = true;
        Assert.Equal(ResultadoComunicacao.Permitido, Decidir(p, email, marketing));
    }

    // ------------------------------------------------------------------ Consentimento: períodos, estados, geral × específico

    [Fact]
    public void Conceder_revogar_e_conceder_de_novo_sao_dois_periodos()
    {
        var (p, _, _) = Cadastro();
        var marketing = Marketing();
        var primeiro = Conceder(p, marketing, quando: new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));
        RegrasConsentimento.Revogar(primeiro, marketing, new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc), "Maria", "Pediu por e-mail");
        Assert.Equal(SituacaoConsentimento.Revogado, RegrasConsentimento.Situacao(p.Consentimentos, marketing, null).Situacao);

        var segundo = Conceder(p, marketing, quando: new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, p.Consentimentos.Count);
        Assert.NotEqual(primeiro.Id, segundo.Id);
        Assert.False(primeiro.Concedido); // o histórico fica como estava
        Assert.Equal(new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc), primeiro.RevogadoEm);
        Assert.Equal("Pediu por e-mail", primeiro.MotivoRevogacao);
        Assert.Equal("Maria", primeiro.RevogadoPor);
        Assert.True(segundo.Concedido);
        Assert.Null(segundo.RevogadoEm);
        var estado = RegrasConsentimento.Situacao(p.Consentimentos, marketing, null);
        Assert.Equal(SituacaoConsentimento.Concedido, estado.Situacao);
        Assert.Same(segundo, estado.Registro);
    }

    [Fact]
    public void Conceder_registra_quem_quando_motivo_versao_e_origem()
    {
        var (p, _, _) = Cadastro();
        var c = Conceder(p, Marketing(), CanalComunicacao.Email);
        Assert.Equal(Agora, c.ConcedidoEm);
        Assert.Equal("Maria", c.ConcedidoPor);
        Assert.Equal("Autorizou no balcão", c.Motivo);
        Assert.Equal("v1.0", c.VersaoTermo);
        Assert.Equal("Balcão", c.Origem);
        Assert.Equal(CanalComunicacao.Email, c.Canal);
    }

    [Fact]
    public void Nao_informado_e_diferente_de_revogado()
    {
        var (p, _, _) = Cadastro();
        var marketing = Marketing();
        Assert.Equal(SituacaoConsentimento.NaoInformado, RegrasConsentimento.Situacao(p.Consentimentos, marketing, null).Situacao);
        var c = Conceder(p, marketing);
        RegrasConsentimento.Revogar(c, marketing, Agora.AddDays(1), "Maria", "Pediu");
        Assert.Equal(SituacaoConsentimento.Revogado, RegrasConsentimento.Situacao(p.Consentimentos, marketing, null).Situacao);
    }

    [Fact]
    public void Nao_aplicavel_quando_a_base_legal_nao_e_consentimento()
    {
        var futura = new FinalidadeTratamento { Id = Guid.NewGuid(), Nome = "Cobrança", BaseLegal = BaseLegal.ExecucaoContrato, Ativo = true };
        Assert.Equal(SituacaoConsentimento.NaoAplicavel, RegrasConsentimento.Situacao([], futura, null).Situacao);
    }

    [Fact]
    public void Finalidades_diferentes_nao_se_misturam()
    {
        var (p, email, _) = Cadastro();
        var marketing = Marketing();
        var pesquisa = new FinalidadeTratamento { Id = Guid.NewGuid(), Codigo = "PESQUISA", Nome = "Pesquisa", BaseLegal = BaseLegal.Consentimento, Ativo = true };
        Conceder(p, pesquisa);

        Assert.Equal(ResultadoComunicacao.Permitido, Decidir(p, email, pesquisa));
        Assert.Equal(ResultadoComunicacao.BloqueadoPorConsentimento, Decidir(p, email, marketing));
    }

    [Fact]
    public void Consentimento_geral_vale_para_qualquer_canal_compativel()
    {
        var (p, email, _) = Cadastro();
        var marketing = Marketing();
        Conceder(p, marketing, canal: null);
        Assert.Equal(SituacaoConsentimento.Concedido, RegrasConsentimento.Situacao(p.Consentimentos, marketing, CanalComunicacao.Email).Situacao);
        Assert.Equal(SituacaoConsentimento.Concedido, RegrasConsentimento.Situacao(p.Consentimentos, marketing, CanalComunicacao.WhatsApp).Situacao);
        Assert.Equal(ResultadoComunicacao.Permitido, Decidir(p, email, marketing));
    }

    [Fact]
    public void Consentimento_especifico_vale_so_para_o_seu_canal()
    {
        var (p, email, _) = Cadastro();
        var marketing = Marketing();
        Conceder(p, marketing, CanalComunicacao.WhatsApp);
        Assert.Equal(SituacaoConsentimento.Concedido, RegrasConsentimento.Situacao(p.Consentimentos, marketing, CanalComunicacao.WhatsApp).Situacao);
        Assert.Equal(SituacaoConsentimento.NaoInformado, RegrasConsentimento.Situacao(p.Consentimentos, marketing, CanalComunicacao.Email).Situacao);
        Assert.Equal(ResultadoComunicacao.BloqueadoPorConsentimento, Decidir(p, email, marketing));
    }

    [Fact]
    public void Especifico_revogado_prevalece_sobre_o_geral_concedido()
    {
        var (p, email, _) = Cadastro();
        var marketing = Marketing();
        Conceder(p, marketing, canal: null);
        var porEmail = Conceder(p, marketing, CanalComunicacao.Email);
        RegrasConsentimento.Revogar(porEmail, marketing, Agora.AddDays(1), "Maria", "Não quer e-mail");

        Assert.Equal(SituacaoConsentimento.Revogado, RegrasConsentimento.Situacao(p.Consentimentos, marketing, CanalComunicacao.Email).Situacao);
        Assert.Equal(SituacaoConsentimento.Concedido, RegrasConsentimento.Situacao(p.Consentimentos, marketing, CanalComunicacao.WhatsApp).Situacao);
        Assert.Equal(ResultadoComunicacao.BloqueadoPorConsentimento, Decidir(p, email, marketing));
    }

    [Fact]
    public void Especifico_concedido_prevalece_sobre_o_geral_revogado()
    {
        var (p, email, _) = Cadastro();
        var marketing = Marketing();
        var geral = Conceder(p, marketing, canal: null);
        RegrasConsentimento.Revogar(geral, marketing, Agora.AddDays(1), "Maria", "Revogou");
        Conceder(p, marketing, CanalComunicacao.Email, Agora.AddDays(2));

        Assert.Equal(ResultadoComunicacao.Permitido, Decidir(p, email, marketing));
        Assert.Equal(SituacaoConsentimento.Revogado, RegrasConsentimento.Situacao(p.Consentimentos, marketing, CanalComunicacao.WhatsApp).Situacao);
    }

    [Fact]
    public void Nao_ha_dois_periodos_em_vigor_para_a_mesma_finalidade_e_canal()
    {
        var (p, _, _) = Cadastro();
        var marketing = Marketing();
        Conceder(p, marketing, CanalComunicacao.Email);
        var erro = Assert.Throws<ValidacaoException>(() => Conceder(p, marketing, CanalComunicacao.Email));
        Assert.Contains(erro.Erros, e => e.Contains("em vigor"));
        Conceder(p, marketing, canal: null); // geral e específico podem coexistir (o específico prevalece no seu canal)
        Assert.Equal(2, p.Consentimentos.Count);
    }

    [Fact]
    public void Conceder_recusa_finalidade_inexistente_desativada_ou_registro_anterior_e_exige_motivo()
    {
        var (p, _, _) = Cadastro();
        Assert.Throws<ValidacaoException>(() => RegrasConsentimento.Conceder(Guid.NewGuid(), p.Id, null, null, [], Agora, "Maria", "ok", null, null));
        Assert.Throws<ValidacaoException>(() => RegrasConsentimento.Conceder(Guid.NewGuid(), p.Id, RegistroAnterior(), null, [], Agora, "Maria", "ok", null, null));
        var desativada = Marketing();
        desativada.Ativo = false;
        Assert.Throws<ValidacaoException>(() => RegrasConsentimento.Conceder(Guid.NewGuid(), p.Id, desativada, null, [], Agora, "Maria", "ok", null, null));
        var semMotivo = Assert.Throws<ValidacaoException>(() => RegrasConsentimento.Conceder(Guid.NewGuid(), p.Id, Marketing(), null, [], Agora, "Maria", "  ", null, null));
        Assert.Contains(semMotivo.Erros, e => e.Contains("motivo"));
    }

    [Fact]
    public void Registro_anterior_nao_se_revoga_e_nao_autoriza()
    {
        var (p, email, _) = Cadastro();
        var anterior = RegistroAnterior();
        var legado = new PessoaConsentimento
        {
            Id = Guid.NewGuid(), PessoaId = p.Id, FinalidadeId = anterior.Id, Canal = CanalComunicacao.Email, Concedido = true, ConcedidoEm = Agora.AddYears(-1)
        };
        p.Consentimentos.Add(legado);

        Assert.Throws<ValidacaoException>(() => RegrasConsentimento.Revogar(legado, anterior, Agora, "Maria", "teste"));
        Assert.True(legado.Concedido); // continua como estava
        Assert.Equal(ResultadoComunicacao.BloqueadoPorConsentimento, Decidir(p, email, Marketing())); // não vale para Marketing
        Assert.Equal(ResultadoComunicacao.BloqueadoPorFinalidade, Decidir(p, email, anterior));
    }

    [Fact]
    public void Revogar_exige_motivo_e_nao_revoga_duas_vezes()
    {
        var (p, _, _) = Cadastro();
        var marketing = Marketing();
        var c = Conceder(p, marketing);
        Assert.Throws<ValidacaoException>(() => RegrasConsentimento.Revogar(c, marketing, Agora, "Maria", ""));
        Assert.True(c.Concedido);
        RegrasConsentimento.Revogar(c, marketing, Agora, "Maria", "Pediu");
        Assert.Throws<ValidacaoException>(() => RegrasConsentimento.Revogar(c, marketing, Agora, "Maria", "De novo"));
    }

    // ------------------------------------------------------------------ Cadastro de finalidades

    [Fact]
    public void Finalidade_de_sistema_nao_muda_codigo_base_classificacao_nem_e_desativada()
    {
        var anterior = Marketing();
        var nova = Marketing();
        nova.Codigo = "OUTRO";
        nova.Ativo = false;
        nova.ClassificacaoExigida = ClassificacaoCanal.Nenhuma;
        var erros = RegrasFinalidadeTratamento.ValidarAlteracao(anterior, nova);
        Assert.Contains(erros, e => e.Contains("código"));
        Assert.Contains(erros, e => e.Contains("desativada"));
        Assert.Contains(erros, e => e.Contains("classificação"));
    }

    [Fact]
    public void Finalidade_nova_so_com_base_legal_que_tem_regra_e_nunca_somente_historico()
    {
        var nova = new FinalidadeTratamento { Codigo = "PESQUISA", Nome = "Pesquisa de satisfação", BaseLegal = BaseLegal.LegitimoInteresse };
        Assert.Contains(RegrasFinalidadeTratamento.ValidarAlteracao(null, nova), e => e.Contains("base legal"));
        nova.BaseLegal = BaseLegal.Consentimento;
        Assert.Empty(RegrasFinalidadeTratamento.ValidarAlteracao(null, nova));
        nova.SomenteHistorico = true;
        Assert.Contains(RegrasFinalidadeTratamento.ValidarAlteracao(null, nova), e => e.Contains("somente histórico"));
        var sistema = new FinalidadeTratamento { Codigo = FinalidadesTratamentoIniciais.Marketing, Nome = "Outro", BaseLegal = BaseLegal.Consentimento };
        Assert.Contains(RegrasFinalidadeTratamento.ValidarAlteracao(null, sistema), e => e.Contains("sistema"));
    }

    [Fact]
    public void Finalidades_iniciais_sao_so_Marketing_e_Registro_anterior()
    {
        Assert.Equal(new[] { FinalidadesTratamentoIniciais.Marketing, FinalidadesTratamentoIniciais.RegistroAnterior },
            FinalidadesTratamentoIniciais.Todas.Select(t => t.Codigo).ToArray());
        var marketing = Marketing();
        Assert.Equal(BaseLegal.Consentimento, marketing.BaseLegal);
        Assert.Equal(ClassificacaoCanal.Marketing, marketing.ClassificacaoExigida);
        Assert.True(RegistroAnterior().SomenteHistorico);
        Assert.False(RegistroAnterior().AceitaConsentimento);
    }
}
