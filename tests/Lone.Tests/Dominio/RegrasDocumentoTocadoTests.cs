using Lone.Application.Pessoas;
using Lone.Domain.Documentos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

/// <summary>
/// P1-8B: regras do tipo de documento só para documento ativo NOVO, ALTERADO ou REATIVADO (D6) — o legado intocado nunca
/// trava a ficha — e as mensagens de número repetido em outra pessoa (sem número inteiro, sem identificar quem está fora
/// do alcance).
/// </summary>
public class RegrasDocumentoTocadoTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 6);

    private static readonly TipoDocumentoCadastro Livre = new() { Id = Guid.NewGuid(), Nome = "Licença" };

    private static readonly TipoDocumentoCadastro Exigente = new()
    {
        Id = Guid.NewGuid(), Nome = "Registro", UsoOrgaoEmissor = UsoCampoDocumento.Obrigatorio, UsoUf = UsoCampoDocumento.Obrigatorio,
        UsoEmissao = UsoCampoDocumento.Obrigatorio, FormatoNumero = FormatoNumeroDocumento.SomenteDigitos, TamanhoMinimoNumero = 4, TamanhoMaximoNumero = 6
    };

    private static readonly Dictionary<Guid, TipoDocumentoCadastro> Cadastro = new() { [Livre.Id] = Livre, [Exigente.Id] = Exigente };

    private static PessoaDocumento Doc(TipoDocumentoCadastro tipo, string numero, bool ativo = true, Guid? id = null) =>
        new() { Id = id ?? Guid.NewGuid(), TipoDocumentoId = tipo.Id, Numero = numero, Ativo = ativo };

    private static PessoaDocumento Copia(PessoaDocumento d) => new()
    {
        Id = d.Id, TipoDocumentoId = d.TipoDocumentoId, Numero = d.Numero, Ativo = d.Ativo, OrgaoEmissor = d.OrgaoEmissor, Uf = d.Uf,
        EmitidoEm = d.EmitidoEm, ValidoAte = d.ValidoAte, Observacoes = d.Observacoes
    };

    [Fact]
    public void Tocado_e_novo_reativado_ou_com_dado_do_documento_alterado_e_nao_so_observacao()
    {
        var gravado = Doc(Livre, "123");
        Assert.True(RegrasDocumento.Tocado(gravado, null));
        Assert.False(RegrasDocumento.Tocado(Copia(gravado), gravado));

        var observacao = Copia(gravado);
        observacao.Observacoes = "nova";
        Assert.False(RegrasDocumento.Tocado(observacao, gravado));

        var numero = Copia(gravado);
        numero.Numero = "124";
        Assert.True(RegrasDocumento.Tocado(numero, gravado));

        var inativo = Doc(Livre, "9", ativo: false);
        var reativado = Copia(inativo);
        reativado.Ativo = true;
        Assert.True(RegrasDocumento.Tocado(reativado, inativo));
    }

    [Fact]
    public void Mesmo_numero_comparavel_na_pessoa_e_erro_no_documento_tocado()
    {
        var a = Doc(Livre, "12.345-6");
        var b = Doc(Livre, "123456");
        var erros = RegrasDocumento.ValidarTocados([a, b], [], Cadastro, Hoje);

        Assert.Equal(2, erros.Count(e => e.Campo == CamposFichaPessoa.DocumentoNumero));
        Assert.Contains(erros, e => e.Item == b.Id);
    }

    [Fact]
    public void Inativo_outro_tipo_e_sem_numero_comparavel_nao_contam()
    {
        var a = Doc(Livre, "777");
        var inativo = Doc(Livre, "777", ativo: false);
        var outroTipo = new PessoaDocumento { Id = Guid.NewGuid(), TipoDocumentoId = Exigente.Id, Numero = "777", Ativo = false };
        var simbolos1 = Doc(Livre, "##");
        var simbolos2 = Doc(Livre, "--");

        Assert.Empty(RegrasDocumento.ValidarTocados([a, inativo, outroTipo, simbolos1, simbolos2], [], Cadastro, Hoje));
    }

    [Fact]
    public void Legado_intocado_nao_trava_e_o_mesmo_documento_mexido_passa_pelas_regras()
    {
        var legado1 = Doc(Exigente, "AB");           // fora do formato, sem órgão, UF e emissão
        var legado2 = Doc(Livre, "55");
        var legado3 = Doc(Livre, "5-5");             // repetido com o legado2
        var gravados = new[] { Copia(legado1), Copia(legado2), Copia(legado3) };

        Assert.Empty(RegrasDocumento.ValidarTocados([legado1, legado2, legado3], gravados, Cadastro, Hoje));

        legado1.Numero = "AC";
        var erros = RegrasDocumento.ValidarTocados([legado1, legado2, legado3], gravados, Cadastro, Hoje);
        Assert.Equal(
            new[] { CamposFichaPessoa.DocumentoOrgaoEmissor, CamposFichaPessoa.DocumentoUf, CamposFichaPessoa.DocumentoEmitidoEm, CamposFichaPessoa.DocumentoNumero },
            erros.Select(e => e.Campo));
        Assert.All(erros, e => Assert.Equal(legado1.Id, e.Item));
    }

    [Theory]
    [InlineData(UsoCampoDocumento.Oculto, false)]
    [InlineData(UsoCampoDocumento.Opcional, false)]
    [InlineData(UsoCampoDocumento.Obrigatorio, true)]
    public void Orgao_UF_e_emissao_seguem_o_uso_do_tipo(UsoCampoDocumento uso, bool exige)
    {
        var tipo = new TipoDocumentoCadastro { Id = Guid.NewGuid(), Nome = "T", UsoOrgaoEmissor = uso, UsoUf = uso, UsoEmissao = uso };
        var erros = RegrasDocumento.ValidarTocados([Doc(tipo, "1")], [], new Dictionary<Guid, TipoDocumentoCadastro> { [tipo.Id] = tipo }, Hoje);
        Assert.Equal(exige ? 3 : 0, erros.Count);
    }

    [Fact]
    public void Emissao_no_futuro_e_recusada_e_hoje_e_aceita()
    {
        var futuro = Doc(Livre, "1");
        futuro.EmitidoEm = Hoje.AddDays(1);
        var hoje = Doc(Livre, "2");
        hoje.EmitidoEm = Hoje;

        var erro = Assert.Single(RegrasDocumento.ValidarTocados([futuro, hoje], [], Cadastro, Hoje));
        Assert.Equal((CamposFichaPessoa.DocumentoEmitidoEm, (Guid?)futuro.Id), (erro.Campo, erro.Item));
    }

    [Fact]
    public void Formato_e_tamanho_do_tipo()
    {
        var ok = Doc(Exigente, "12.34");
        ok.OrgaoEmissor = "SSP"; ok.Uf = "SP"; ok.EmitidoEm = Hoje;
        Assert.Empty(RegrasDocumento.ValidarTocados([ok], [], Cadastro, Hoje));

        ok.Numero = "1234567";
        Assert.Contains("no máximo 6", Assert.Single(RegrasDocumento.ValidarTocados([ok], [], Cadastro, Hoje)).Mensagem);
        ok.Numero = "12X4";
        Assert.Contains("só números", Assert.Single(RegrasDocumento.ValidarTocados([ok], [], Cadastro, Hoje)).Mensagem);
    }

    [Fact]
    public void Mensagem_de_repetido_em_outra_pessoa_nao_mostra_o_numero_nem_quem_esta_fora_do_alcance()
    {
        var documento = new PessoaDocumento { Id = Guid.NewGuid(), Numero = "12.345.678-9", NumeroNormalizado = "123456789" };
        var avisa = new TipoDocumentoCadastro { Nome = "RG", Unicidade = UnicidadeDocumento.Aviso };
        var bloqueia = new TipoDocumentoCadastro { Nome = "CNH", Unicidade = UnicidadeDocumento.PorTipo };
        var visivel = new PessoaIdentificacao(Guid.NewGuid(), 42, "Maria Visível");

        var (_, aviso) = PessoaAppService.MensagemDeDocumentoRepetido(avisa, documento, [visivel], foraDoAlcance: true);
        Assert.StartsWith("Possível duplicidade", aviso);
        Assert.Contains("terminado em 789", aviso);
        Assert.Contains("000042 - Maria Visível", aviso);
        Assert.Contains("fora do seu alcance", aviso);
        Assert.DoesNotContain("123456789", aviso);
        Assert.DoesNotContain("12.345", aviso);

        var (_, soFora) = PessoaAppService.MensagemDeDocumentoRepetido(avisa, documento, [], foraDoAlcance: true);
        Assert.DoesNotContain("Maria", soFora);
        Assert.Contains("cadastro fora do seu alcance", soFora);

        var (erro, semAviso) = PessoaAppService.MensagemDeDocumentoRepetido(bloqueia, documento, [], foraDoAlcance: true);
        Assert.Null(semAviso);
        Assert.Equal((CamposFichaPessoa.DocumentoNumero, (Guid?)documento.Id), (erro!.Campo, erro.Item));
        Assert.DoesNotContain("123456789", erro.Mensagem);

        Assert.Equal(((ErroValidacao?)null, (string?)null), PessoaAppService.MensagemDeDocumentoRepetido(avisa, documento, [], foraDoAlcance: false));
    }
}
