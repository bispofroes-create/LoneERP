using Lone.Contracts.Documentos;
using Lone.Domain.Documentos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

/// <summary>
/// P1-8: as regras do tipo de documento como dados. A semente dos cinco de sistema reproduz exatamente as regras fixas de
/// antes (TiposDocumentoSistema.AplicaA / TemOrgaoEmissor / TemUf) e o tipo do usuário nasce com o comportamento de sempre.
/// </summary>
public class MetadadosTipoDocumentoTests
{
    private static TipoDocumentoCadastro DeSistema(TipoDocumento tipo) =>
        TiposDocumentoSistema.ComSemente(new TipoDocumentoCadastro
        {
            Id = TiposDocumentoSistema.Id(tipo), Nome = tipo.ToString(), TipoSistema = tipo
        }, tipo);

    [Fact]
    public void Semente_dos_cinco_tipos_de_sistema_reproduz_as_regras_fixas_de_antes()
    {
        foreach (var tipo in Enum.GetValues<TipoDocumento>())
        {
            var t = DeSistema(tipo);
            foreach (var natureza in Enum.GetValues<NaturezaPessoa>())
                Assert.True(TiposDocumentoSistema.AplicaA(tipo, natureza) == t.AplicaA(natureza), $"{tipo} × {natureza}");
            Assert.Equal(TiposDocumentoSistema.TemOrgaoEmissor(tipo), t.UsoOrgaoEmissor != UsoCampoDocumento.Oculto);
            Assert.Equal(TiposDocumentoSistema.TemUf(tipo), t.UsoUf != UsoCampoDocumento.Oculto);
            Assert.NotEqual(UsoCampoDocumento.Obrigatorio, t.UsoOrgaoEmissor); // nada passou a ser obrigatório
            Assert.NotEqual(UsoCampoDocumento.Obrigatorio, t.UsoUf);
            Assert.Equal(UsoCampoDocumento.Opcional, t.UsoEmissao);
            Assert.Equal(FormatoNumeroDocumento.Livre, t.FormatoNumero);    // formato de sempre
            Assert.False(RegrasDocumento.Bloqueia(t.Unicidade));            // a migração não liga bloqueio (D4)
            Assert.Empty(RegrasDocumento.Validar(t));
        }
    }

    [Fact]
    public void Semente_explicita_por_tipo()
    {
        Assert.Equal(UnicidadeDocumento.Aviso, DeSistema(TipoDocumento.Rg).Unicidade);
        Assert.Equal(UnicidadeDocumento.Aviso, DeSistema(TipoDocumento.Cnh).Unicidade); // D4: CNH começa em Aviso
        Assert.Equal(UnicidadeDocumento.Aviso, DeSistema(TipoDocumento.Passaporte).Unicidade);
        Assert.Equal(UnicidadeDocumento.Aviso, DeSistema(TipoDocumento.DocumentoEstrangeiro).Unicidade);
        Assert.Equal(UnicidadeDocumento.Nenhuma, DeSistema(TipoDocumento.Outro).Unicidade);
        Assert.Equal((true, false, true), (DeSistema(TipoDocumento.Passaporte).AplicaPessoaFisica,
            DeSistema(TipoDocumento.Passaporte).AplicaPessoaJuridica, DeSistema(TipoDocumento.Passaporte).AplicaEstrangeiro));
    }

    [Fact]
    public void Tipo_criado_pelo_usuario_nasce_com_o_comportamento_de_sempre()
    {
        var t = new TipoDocumentoCadastro { Nome = "Alvará" };
        foreach (var natureza in Enum.GetValues<NaturezaPessoa>())
            Assert.Equal(TiposDocumentoSistema.AplicaA(null, natureza), t.AplicaA(natureza));
        Assert.Equal(TiposDocumentoSistema.TemOrgaoEmissor(null), t.UsoOrgaoEmissor != UsoCampoDocumento.Oculto);
        Assert.Equal(TiposDocumentoSistema.TemUf(null), t.UsoUf != UsoCampoDocumento.Oculto);
        Assert.Equal(UsoCampoDocumento.Opcional, t.UsoEmissao);
        Assert.Equal(FormatoNumeroDocumento.Livre, t.FormatoNumero);
        Assert.Equal(UnicidadeDocumento.Nenhuma, t.Unicidade);
        Assert.Null(t.TamanhoMinimoNumero);
        Assert.Null(t.TamanhoMaximoNumero);
    }

    [Fact]
    public void Padroes_do_contrato_sao_os_mesmos_do_tipo_novo()
    {
        var dto = new TipoDocumentoDto();
        var t = new TipoDocumentoCadastro();
        Assert.Equal(
            (t.AplicaPessoaFisica, t.AplicaPessoaJuridica, t.AplicaEstrangeiro, t.UsoOrgaoEmissor, t.UsoUf, t.UsoEmissao, t.FormatoNumero, t.Unicidade),
            (dto.AplicaPessoaFisica, dto.AplicaPessoaJuridica, dto.AplicaEstrangeiro, dto.UsoOrgaoEmissor, dto.UsoUf, dto.UsoEmissao, dto.FormatoNumero, dto.Unicidade));
    }

    [Fact]
    public void Configuracao_invalida_do_tipo_e_recusada()
    {
        static List<string> Erros(Action<TipoDocumentoCadastro> ajuste)
        {
            var t = new TipoDocumentoCadastro { Nome = "Tipo" };
            ajuste(t);
            return RegrasDocumento.Validar(t);
        }

        Assert.Empty(Erros(_ => { }));
        Assert.Single(Erros(t => { t.AplicaPessoaFisica = false; t.AplicaPessoaJuridica = false; t.AplicaEstrangeiro = false; }));
        Assert.Single(Erros(t => t.TamanhoMinimoNumero = 0));
        Assert.Single(Erros(t => t.TamanhoMaximoNumero = NumeroDocumento.TamanhoMaximo + 1));
        Assert.Single(Erros(t => { t.TamanhoMinimoNumero = 9; t.TamanhoMaximoNumero = 8; }));
        Assert.Empty(Erros(t => { t.TamanhoMinimoNumero = 9; t.TamanhoMaximoNumero = 9; }));
        Assert.Single(Erros(t => t.Unicidade = UnicidadeDocumento.PorTipoEUf));                     // UF precisa ser obrigatória
        Assert.Empty(Erros(t => { t.Unicidade = UnicidadeDocumento.PorTipoEUf; t.UsoUf = UsoCampoDocumento.Obrigatorio; }));
        Assert.Single(Erros(t => t.FormatoNumero = (FormatoNumeroDocumento)99));
        Assert.Single(Erros(t => t.Unicidade = (UnicidadeDocumento)4));                             // "por país" não existe
        Assert.Single(Erros(t => t.UsoUf = (UsoCampoDocumento)7));
    }

    [Fact]
    public void A_quem_se_aplica_vem_do_cadastro_e_so_vale_para_documento_novo_ou_troca_de_tipo()
    {
        var rg = DeSistema(TipoDocumento.Rg);
        var soEmpresa = new TipoDocumentoCadastro { Id = Guid.NewGuid(), Nome = "Alvará", AplicaPessoaFisica = false, AplicaEstrangeiro = false };
        var cadastro = new Dictionary<Guid, TipoDocumentoCadastro> { [rg.Id] = rg, [soEmpresa.Id] = soEmpresa };
        var rgNovo = new PessoaDocumento { Id = Guid.NewGuid(), TipoDocumentoId = rg.Id, Numero = "1" };
        var alvaraNovo = new PessoaDocumento { Id = Guid.NewGuid(), TipoDocumentoId = soEmpresa.Id, Numero = "2" };

        Assert.Contains("O tipo de documento \"Rg\" não se aplica a pessoa jurídica.",
            RegrasDocumento.Aplicar([rgNovo], new Dictionary<Guid, Guid>(), cadastro, NaturezaPessoa.Juridica));
        Assert.Contains("O tipo de documento \"Alvará\" não se aplica a pessoa física.",
            RegrasDocumento.Aplicar([alvaraNovo], new Dictionary<Guid, Guid>(), cadastro, NaturezaPessoa.Fisica));
        Assert.Empty(RegrasDocumento.Aplicar([alvaraNovo], new Dictionary<Guid, Guid>(), cadastro, NaturezaPessoa.Juridica));
        // Gravado antes com o mesmo tipo: continua como está.
        Assert.Empty(RegrasDocumento.Aplicar([rgNovo], new Dictionary<Guid, Guid> { [rgNovo.Id] = rg.Id }, cadastro, NaturezaPessoa.Juridica));
    }

    [Fact]
    public void Gravacao_calcula_o_numero_comparavel_e_a_chave_so_quando_o_tipo_bloqueia()
    {
        var aviso = DeSistema(TipoDocumento.Rg);
        var bloqueia = new TipoDocumentoCadastro { Id = Guid.NewGuid(), Nome = "Registro", Unicidade = UnicidadeDocumento.PorTipo };
        var cadastro = new Dictionary<Guid, TipoDocumentoCadastro> { [aviso.Id] = aviso, [bloqueia.Id] = bloqueia };
        var d1 = new PessoaDocumento { Id = Guid.NewGuid(), TipoDocumentoId = aviso.Id, Numero = "12.345.678-9" };
        var d2 = new PessoaDocumento { Id = Guid.NewGuid(), TipoDocumentoId = bloqueia.Id, Numero = "ab-1", Ativo = false };

        RegrasDocumento.Aplicar([d1, d2], new Dictionary<Guid, Guid>(), cadastro);

        Assert.Equal(("12.345.678-9", "123456789", (string?)null), (d1.Numero, d1.NumeroNormalizado, d1.ChaveUnicidade));
        Assert.Equal($"{bloqueia.Id:N}|AB1", d2.ChaveUnicidade); // inativo tem chave, mas o índice só olha os ativos
    }

    [Fact]
    public void Normalizador_da_ficha_preenche_o_numero_comparavel_sem_mudar_o_exibido()
    {
        var p = new Pessoa { Nome = "Ana" };
        p.Documentos.Add(new PessoaDocumento { Id = Guid.NewGuid(), Numero = "  mg-12.345.678 " });

        PessoaNormalizador.Normalizar(p);

        Assert.Equal(("MG-12.345.678", "MG12345678"), (p.Documentos[0].Numero, p.Documentos[0].NumeroNormalizado));
    }
}
