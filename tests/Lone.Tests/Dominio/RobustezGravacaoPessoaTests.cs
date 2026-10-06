using Lone.Domain.Contatos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

/// <summary>
/// Bloco G (robustez da gravação da ficha de Pessoas), regras puras: limites de tamanho antes do banco (P1-1), endereço
/// inativo legado (P1-14), telefone/e-mail repetido na mesma pessoa (P1-12) e a troca de pessoa física (P1-2).
/// </summary>
public class RobustezGravacaoPessoaTests
{
    /// <summary>Pessoa física válida (com o estabelecimento único dos dados fiscais, como o normalizador deixa).</summary>
    private static Pessoa Ana()
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica };
        p.Estabelecimentos.Add(new Estabelecimento { Id = Guid.NewGuid(), PessoaId = p.Id, Principal = true });
        return p;
    }

    private static PessoaEndereco Endereco(string logradouro, bool ativo = true) => new()
    {
        Id = Guid.NewGuid(), Logradouro = logradouro, Ativo = ativo, MunicipioId = 3550308, Cep = "01310100", Numero = "100", Bairro = "Centro"
    };

    // ---- G2: limites de tamanho ----

    [Fact]
    public void Logradouro_maior_que_a_coluna_vira_erro_no_campo_do_endereco()
    {
        var p = Ana();
        var endereco = Endereco(new string('R', 151));
        p.Enderecos.Add(endereco);

        var erro = Assert.Single(PessoaValidador.ValidarComCampos(p), e => e.Mensagem.Contains("logradouro pode ter"));

        Assert.Equal("Endereço 1: o logradouro pode ter no máximo 150 caracteres.", erro.Mensagem);
        Assert.Equal(CamposFichaPessoa.Logradouro, erro.Campo);
        Assert.Equal(endereco.Id, erro.Item);
    }

    [Fact]
    public void No_limite_exato_nao_ha_erro()
    {
        var p = Ana();
        p.NomeSocial = new string('a', 150);
        p.Apelido = new string('b', 60);
        p.Enderecos.Add(Endereco(new string('R', 150)));

        Assert.DoesNotContain(PessoaValidador.ValidarComCampos(p), e => e.Mensagem.Contains("no máximo"));
    }

    [Fact]
    public void Textos_da_identificacao_contato_e_documento_tem_limite_com_campo()
    {
        var p = Ana();
        p.NomeSocial = new string('a', 151);
        p.Observacoes = new string('o', 2001);
        var contato = new Contato { Id = Guid.NewGuid(), Nome = "Maria", Cargo = new string('c', 61), Ativo = true };
        p.Contatos.Add(contato);
        var documento = new PessoaDocumento { Id = Guid.NewGuid(), Numero = "123", OrgaoEmissor = new string('s', 21), Ativo = true };
        p.Documentos.Add(documento);

        var erros = PessoaValidador.ValidarComCampos(p);

        Assert.Contains(erros, e => e.Mensagem == "O nome social pode ter no máximo 150 caracteres." && e.Campo == CamposFichaPessoa.NomeSocial && e.Item is null);
        Assert.Contains(erros, e => e.Mensagem == "As observações podem ter no máximo 2000 caracteres." && e.Campo is null);
        Assert.Contains(erros, e => e.Mensagem == "Pessoa de contato 1: o cargo pode ter no máximo 60 caracteres." &&
                                    e.Campo == CamposFichaPessoa.ContatoCargo && e.Item == contato.Id);
        Assert.Contains(erros, e => e.Mensagem == "Documento 1: o órgão emissor pode ter no máximo 20 caracteres." &&
                                    e.Campo == CamposFichaPessoa.DocumentoOrgaoEmissor && e.Item == documento.Id);
    }

    [Fact]
    public void Nome_fantasia_do_principal_aponta_a_identificacao_e_da_filial_o_cartao()
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "ABC", Natureza = NaturezaPessoa.Juridica };
        var principal = new Estabelecimento { Id = Guid.NewGuid(), Principal = true, Cnpj = "11222333000181", NomeFantasia = new string('f', 151) };
        var filial = new Estabelecimento { Id = Guid.NewGuid(), Principal = false, Cnpj = "11222333000262", NomeFantasia = new string('g', 151), InscricaoMunicipal = new string('9', 21) };
        p.Estabelecimentos.AddRange([principal, filial]);

        var erros = PessoaValidador.ValidarComCampos(p);

        Assert.Contains(erros, e => e.Campo == CamposFichaPessoa.NomeFantasia && e.Item is null && e.Mensagem.StartsWith("Estabelecimento 1:"));
        Assert.Contains(erros, e => e.Campo == CamposFichaPessoa.NomeFantasia && e.Item == filial.Id);
        Assert.Contains(erros, e => e.Campo == CamposFichaPessoa.InscricaoMunicipal && e.Item == filial.Id &&
                                    e.Mensagem == "Estabelecimento 2: a inscrição municipal pode ter no máximo 20 caracteres.");
    }

    [Fact]
    public void Codigo_postal_no_exterior_maior_que_a_coluna_vira_erro()
    {
        var p = Ana();
        var exterior = new PessoaEndereco
        {
            Id = Guid.NewGuid(), Logradouro = "Main St", Ativo = true, Cidade = "Lisboa", Pais = "Portugal", CodigoPais = "6076", Cep = "123456789"
        };
        p.Enderecos.Add(exterior);

        var erro = Assert.Single(PessoaValidador.ValidarComCampos(p), e => e.Campo == CamposFichaPessoa.Cep);
        Assert.Equal(exterior.Id, erro.Item);
    }

    // ---- G6: endereço inativo ----

    [Fact]
    public void Endereco_inativo_legado_sem_logradouro_nao_bloqueia()
    {
        var p = Ana();
        p.Enderecos.Add(new PessoaEndereco { Id = Guid.NewGuid(), Logradouro = "", Ativo = false });

        Assert.Empty(PessoaValidador.ValidarComCampos(p));
    }

    [Fact]
    public void Endereco_ativo_sem_logradouro_continua_invalido()
    {
        var p = Ana();
        var ativo = Endereco("");
        p.Enderecos.Add(ativo);

        var erro = Assert.Single(PessoaValidador.ValidarComCampos(p), e => e.Campo == CamposFichaPessoa.Logradouro);
        Assert.Equal("Endereço 1: informe o logradouro.", erro.Mensagem);
        Assert.Equal(ativo.Id, erro.Item);
    }

    [Fact]
    public void Endereco_inativo_reativado_volta_a_exigir_os_dados()
    {
        var p = Ana();
        var legado = new PessoaEndereco { Id = Guid.NewGuid(), Logradouro = "", Ativo = false };
        p.Enderecos.Add(legado);
        Assert.Empty(PessoaValidador.ValidarComCampos(p));

        legado.Ativo = true; // reativado na ficha

        var erros = PessoaValidador.ValidarComCampos(p);
        Assert.Contains(erros, e => e.Campo == CamposFichaPessoa.Logradouro && e.Item == legado.Id);
        Assert.Contains(erros, e => e.Campo == CamposFichaPessoa.Municipio && e.Item == legado.Id);
    }

    [Fact]
    public void Limite_de_tamanho_vale_tambem_para_o_endereco_inativo()
    {
        var p = Ana();
        var inativo = new PessoaEndereco { Id = Guid.NewGuid(), Logradouro = "", Complemento = new string('c', 61), Ativo = false };
        p.Enderecos.Add(inativo);

        var erro = Assert.Single(PessoaValidador.ValidarComCampos(p));
        Assert.Equal((CamposFichaPessoa.Complemento, (Guid?)inativo.Id), (erro.Campo, erro.Item));
    }

    // ---- G7: telefone/e-mail repetido ----

    private static MeioContato Meio(TipoContato tipo, string valor, bool ativo = true) =>
        new() { Id = Guid.NewGuid(), Tipo = tipo, Valor = valor, Ativo = ativo };

    /// <summary>Normaliza como o PessoaAppService faz antes de validar.</summary>
    private static List<MeioContato> Normalizados(params MeioContato[] meios)
    {
        var p = Ana();
        p.MeiosContato.AddRange(meios);
        PessoaNormalizador.Normalizar(p);
        return p.MeiosContato;
    }

    [Fact]
    public void Telefone_com_formatacao_diferente_e_o_mesmo_numero()
    {
        var meios = Normalizados(Meio(TipoContato.Celular, "(11) 98765-4321"), Meio(TipoContato.Celular, "+55 11 98765 4321"));

        var erro = Assert.Single(RegrasMeioContato.ValidarRepetidos(meios, []));
        Assert.Equal("Telefone/e-mail 2: este número já está na lista (Telefone/e-mail 1).", erro.Mensagem);
        Assert.Equal((CamposFichaPessoa.MeioContatoValor, (Guid?)meios[1].Id), (erro.Campo, erro.Item));
    }

    [Fact]
    public void Email_com_maiusculas_e_o_mesmo_email()
    {
        var meios = Normalizados(Meio(TipoContato.Email, "Ana@Empresa.com.br"), Meio(TipoContato.Email, "ana@empresa.com.br"));

        Assert.Contains("este e-mail", Assert.Single(RegrasMeioContato.ValidarRepetidos(meios, [])).Mensagem);
    }

    [Fact]
    public void Telefone_fixo_e_celular_com_o_mesmo_numero_sao_repetidos()
    {
        var meios = Normalizados(Meio(TipoContato.Telefone, "1133334444"), Meio(TipoContato.Celular, "(11) 3333-4444"));

        Assert.Single(RegrasMeioContato.ValidarRepetidos(meios, []));
    }

    [Fact]
    public void Inativo_contatos_distintos_e_tipo_outro_nao_contam()
    {
        var meios = Normalizados(
            Meio(TipoContato.Celular, "11987654321"),
            Meio(TipoContato.Celular, "11987654321", ativo: false), // removido: histórico
            Meio(TipoContato.Celular, "11912345678"),
            Meio(TipoContato.Outro, "site"),
            Meio(TipoContato.Outro, "site"));

        Assert.Empty(RegrasMeioContato.ValidarRepetidos(meios, []));
    }

    [Fact]
    public void Repetido_que_ja_estava_gravado_nao_bloqueia_mas_um_novo_igual_sim()
    {
        var a = Meio(TipoContato.Celular, "11987654321");
        var b = Meio(TipoContato.Celular, "11987654321");
        var gravados = new[] { Clonar(a), Clonar(b) };

        Assert.Empty(RegrasMeioContato.ValidarRepetidos([a, b], gravados)); // cadastro antigo: fica como está

        var novo = Meio(TipoContato.Celular, "11987654321");
        var erro = Assert.Single(RegrasMeioContato.ValidarRepetidos([a, b, novo], gravados), e => e.Item == novo.Id);
        Assert.StartsWith("Telefone/e-mail 3:", erro.Mensagem);
    }

    [Fact]
    public void Erro_vai_para_o_que_mudou_mesmo_que_ele_venha_primeiro()
    {
        var gravado = Meio(TipoContato.Celular, "11987654321");
        var alterado = Meio(TipoContato.Celular, "11912345678");
        var anteriores = new[] { Clonar(gravado), Clonar(alterado) };
        alterado.Valor = "11987654321"; // o primeiro da lista passa a repetir o segundo

        var erro = Assert.Single(RegrasMeioContato.ValidarRepetidos([alterado, gravado], anteriores));
        Assert.Equal(alterado.Id, erro.Item);
        Assert.StartsWith("Telefone/e-mail 1:", erro.Mensagem);
    }

    [Fact]
    public void Reativar_um_repetido_e_erro()
    {
        var ativo = Meio(TipoContato.Email, "ana@empresa.com.br");
        var inativo = Meio(TipoContato.Email, "ana@empresa.com.br", ativo: false);
        var anteriores = new[] { Clonar(ativo), Clonar(inativo) };
        inativo.Ativo = true;

        var erro = Assert.Single(RegrasMeioContato.ValidarRepetidos([ativo, inativo], anteriores));
        Assert.Equal(inativo.Id, erro.Item);
    }

    private static MeioContato Clonar(MeioContato m) => new() { Id = m.Id, Tipo = m.Tipo, Valor = m.Valor, Ativo = m.Ativo };

    // ---- G4: troca de pessoa física ----

    private static Pessoa FisicaGravada() => new()
    {
        Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica, DocumentoPrincipal = "52998224725",
        NomeSocial = "Aninha", DataNascimento = new DateOnly(1990, 1, 2), Sexo = SexoRegistro.Feminino
    };

    [Fact]
    public void Perdas_listam_so_o_que_esta_preenchido()
    {
        var perdas = RegrasNaturezaPessoa.PerdasAoSairDaPessoaFisica(DadosSoDaPessoaFisica.De(FisicaGravada()), NaturezaPessoa.Juridica);

        Assert.Equal(new[] { CamposFichaPessoa.Documento, CamposFichaPessoa.NomeSocial, CamposFichaPessoa.DataNascimento, CamposFichaPessoa.Sexo },
            perdas.Select(p => p.Campo));
        Assert.Equal("o CPF; o nome social; a data de nascimento; o sexo", RegrasNaturezaPessoa.Lista(perdas));
        Assert.Empty(RegrasNaturezaPessoa.PerdasAoSairDaPessoaFisica(DadosSoDaPessoaFisica.De(FisicaGravada()), NaturezaPessoa.Fisica));
    }

    [Fact]
    public void Inscricao_estadual_so_se_perde_no_estrangeiro()
    {
        var p = FisicaGravada();
        p.Estabelecimentos.Add(new Estabelecimento { Id = Guid.NewGuid(), Principal = true, InscricaoEstadual = "110042490114" });
        var dados = DadosSoDaPessoaFisica.De(p);

        Assert.DoesNotContain(RegrasNaturezaPessoa.PerdasAoSairDaPessoaFisica(dados, NaturezaPessoa.Juridica), x => x.Campo == CamposFichaPessoa.InscricaoEstadual);
        Assert.Contains(RegrasNaturezaPessoa.PerdasAoSairDaPessoaFisica(dados, NaturezaPessoa.Estrangeiro), x => x.Campo == CamposFichaPessoa.InscricaoEstadual);
    }

    [Fact]
    public void Sem_dados_a_perder_a_troca_passa_sem_confirmacao()
    {
        var vazia = new Pessoa { Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica };

        Assert.Null(RegrasNaturezaPessoa.ValidarSaidaDaPessoaFisica(vazia, NaturezaPessoa.Juridica, null, null, null));
    }

    [Fact]
    public void Com_dados_e_sem_confirmacao_a_troca_e_recusada_com_a_lista()
    {
        var erro = RegrasNaturezaPessoa.ValidarSaidaDaPessoaFisica(FisicaGravada(), NaturezaPessoa.Juridica, null, null, null);

        Assert.Equal("Ao trocar de pessoa física para pessoa jurídica, seriam apagados: o CPF; o nome social; a data de nascimento; " +
                     "o sexo. Nada foi alterado. Para continuar, escolha de novo o tipo de pessoa na ficha e confirme a troca.", erro);
    }

    [Fact]
    public void Confirmacao_vale_so_para_a_troca_e_os_campos_confirmados()
    {
        var p = FisicaGravada();
        string[] todos = [CamposFichaPessoa.Documento, CamposFichaPessoa.NomeSocial, CamposFichaPessoa.DataNascimento, CamposFichaPessoa.Sexo];

        Assert.Null(RegrasNaturezaPessoa.ValidarSaidaDaPessoaFisica(p, NaturezaPessoa.Juridica, NaturezaPessoa.Fisica, NaturezaPessoa.Juridica, todos));
        // Outra troca (para estrangeiro), outro "de" ou faltando um campo: não vale.
        Assert.NotNull(RegrasNaturezaPessoa.ValidarSaidaDaPessoaFisica(p, NaturezaPessoa.Estrangeiro, NaturezaPessoa.Fisica, NaturezaPessoa.Juridica, todos));
        Assert.NotNull(RegrasNaturezaPessoa.ValidarSaidaDaPessoaFisica(p, NaturezaPessoa.Juridica, NaturezaPessoa.Juridica, NaturezaPessoa.Juridica, todos));
        Assert.NotNull(RegrasNaturezaPessoa.ValidarSaidaDaPessoaFisica(p, NaturezaPessoa.Juridica, NaturezaPessoa.Fisica, NaturezaPessoa.Juridica, todos[..3]));
    }

    [Fact]
    public void Cadastro_novo_e_quem_nao_era_pessoa_fisica_nao_passam_por_esta_regra()
    {
        Assert.Null(RegrasNaturezaPessoa.ValidarSaidaDaPessoaFisica(null, NaturezaPessoa.Juridica, null, null, null));
        var estrangeiro = FisicaGravada();
        estrangeiro.Natureza = NaturezaPessoa.Estrangeiro;
        Assert.Null(RegrasNaturezaPessoa.ValidarSaidaDaPessoaFisica(estrangeiro, NaturezaPessoa.Juridica, null, null, null));
    }

    [Fact]
    public void O_normalizador_apaga_exatamente_o_que_a_regra_lista()
    {
        // Prova que a lista de perdas não esquece nada: tudo o que ela diz que existe some depois de normalizar como PJ.
        var p = FisicaGravada();
        p.Apelido = "Nina";
        p.NomeMae = "Maria";
        p.NomePai = "José";
        p.EstadoCivil = EstadoCivil.Casado;
        p.Escolaridade = Escolaridade.SuperiorCompleto;
        p.IdentidadeGenero = IdentidadeGenero.Mulher;
        p.ProfissaoId = Guid.NewGuid();
        p.NaturalidadeMunicipioId = 3550308;
        var antes = DadosSoDaPessoaFisica.De(p);
        Assert.Equal(12, RegrasNaturezaPessoa.PerdasAoSairDaPessoaFisica(antes, NaturezaPessoa.Juridica).Count);

        p.Natureza = NaturezaPessoa.Juridica;
        p.DocumentoPrincipal = null; // a ficha não manda CPF na PJ (vira a raiz do CNPJ)
        PessoaNormalizador.Normalizar(p);

        Assert.Empty(RegrasNaturezaPessoa.PerdasAoSairDaPessoaFisica(DadosSoDaPessoaFisica.De(p), NaturezaPessoa.Juridica));
    }
}
