using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Fiscal;
using Lone.Domain.GruposEmpresariais;
using Lone.Domain.Pessoas;
using Lone.Domain.Relacionamentos;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

/// <summary>
/// Pessoa global com papéis e relacionamentos; pessoa jurídica com os seus estabelecimentos (matriz e filiais da MESMA
/// raiz); empresas independentes nunca viram filial; grupo empresarial opcional e só de pessoa jurídica.
/// </summary>
public class EstruturaEmpresarialTests
{
    private static readonly TipoRelacionamento SocioDe = new() { Id = TiposRelacionamentoSistema.SocioDe, Nome = "Sócio de", NomeInverso = "Tem como sócio" };
    private static readonly TipoRelacionamento AdministradorDe = new() { Id = TiposRelacionamentoSistema.AdministradorDe, Nome = "Administrador de", NomeInverso = "Tem como administrador" };
    private static readonly TipoRelacionamento ContatoDe = new() { Id = TiposRelacionamentoSistema.ContatoDe, Nome = "Contato de", NomeInverso = "Tem como contato" };

    private static Pessoa Empresa(string nome, params string[] cnpjs)
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Natureza = NaturezaPessoa.Juridica, Nome = nome };
        for (var i = 0; i < cnpjs.Length; i++)
            p.Estabelecimentos.Add(new Estabelecimento { Id = Guid.NewGuid(), PessoaId = p.Id, Cnpj = cnpjs[i], Principal = i == 0 });
        PessoaNormalizador.Normalizar(p, new DateOnly(2026, 9, 26));
        return p;
    }

    private static Pessoa PessoaFisica(string nome)
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Natureza = NaturezaPessoa.Fisica, Nome = nome };
        p.Estabelecimentos.Add(new Estabelecimento { Id = Guid.NewGuid(), PessoaId = p.Id, Principal = true });
        PessoaNormalizador.Normalizar(p, new DateOnly(2026, 9, 26));
        return p;
    }

    private static PessoaNoRelacionamento Dados(Pessoa p) => new(p.Id, p.Natureza, p.Situacao, p.Nome);

    private static PessoaRelacionamento Vinculo(Pessoa origem, TipoRelacionamento tipo, Pessoa destino) => new()
    {
        Id = Guid.NewGuid(), PessoaId = origem.Id, PessoaDestinoId = destino.Id, TipoRelacionamentoId = tipo.Id
    };

    // ---- 1, 2, 8 e 11: pessoa física relacionada a várias empresas, sem duplicar a pessoa ----

    [Fact]
    public void Pessoa_fisica_pode_ser_socia_e_administradora_de_varias_empresas_independentes()
    {
        var joao = PessoaFisica("João da Silva");
        var abc = Empresa("ABC Comércio Ltda", "11222333000181");
        var xyz = Empresa("XYZ Transportes Ltda", "11444777000161");
        var gravados = new List<PessoaRelacionamento>();

        foreach (var (tipo, empresa) in new[] { (SocioDe, abc), (SocioDe, xyz), (AdministradorDe, abc) })
        {
            var novo = Vinculo(joao, tipo, empresa);
            Assert.Empty(RegrasRelacionamento.ValidarNovo(novo, tipo, Dados(joao), Dados(empresa), gravados));
            gravados.Add(novo);
        }

        // Uma pessoa só (a mesma origem em todos os vínculos): nada limita a uma empresa por pessoa.
        Assert.Equal(3, gravados.Count);
        Assert.All(gravados, v => Assert.Equal(joao.Id, v.PessoaId));
        Assert.Equal(2, gravados.Select(v => v.PessoaDestinoId).Distinct().Count());
    }

    [Fact]
    public void O_mesmo_vinculo_em_aberto_nao_se_repete_mas_um_novo_periodo_depois_de_encerrar_pode()
    {
        var joao = PessoaFisica("João");
        var abc = Empresa("ABC", "11222333000181");
        var gravado = Vinculo(joao, SocioDe, abc);

        Assert.Contains(RegrasRelacionamento.ValidarNovo(Vinculo(joao, SocioDe, abc), SocioDe, Dados(joao), Dados(abc), [gravado]),
            e => e.Contains("já está registrado"));

        gravado.FimEm = new DateOnly(2025, 12, 31);
        Assert.Empty(RegrasRelacionamento.ValidarNovo(Vinculo(joao, SocioDe, abc), SocioDe, Dados(joao), Dados(abc), [gravado]));

        var desativado = Vinculo(joao, SocioDe, abc);
        desativado.Ativo = false; // lançado por engano: não conta
        Assert.Empty(RegrasRelacionamento.ValidarNovo(Vinculo(joao, SocioDe, abc), SocioDe, Dados(joao), Dados(abc), [desativado]));
    }

    [Fact]
    public void Socio_e_administrador_ligam_a_uma_empresa_e_ninguem_se_relaciona_consigo()
    {
        var joao = PessoaFisica("João");
        var maria = PessoaFisica("Maria");

        Assert.Contains(RegrasRelacionamento.ValidarNovo(Vinculo(joao, SocioDe, maria), SocioDe, Dados(joao), Dados(maria), []),
            e => e.Contains("não pode ser pessoa física"));
        Assert.Empty(RegrasRelacionamento.ValidarNovo(Vinculo(maria, ContatoDe, joao), ContatoDe, Dados(maria), Dados(joao), []));
        Assert.Contains(RegrasRelacionamento.ValidarNovo(Vinculo(joao, ContatoDe, joao), ContatoDe, Dados(joao), Dados(joao), []),
            e => e.Contains("ela mesma"));
    }

    [Fact]
    public void Encerrar_preenche_o_fim_uma_vez_so_e_o_vinculo_fica_gravado()
    {
        var v = new PessoaRelacionamento { InicioEm = new DateOnly(2024, 1, 1) };
        Assert.True(v.Vigente(new DateOnly(2026, 9, 26)));
        Assert.Contains(RegrasRelacionamento.ValidarEncerramento(v, new DateOnly(2023, 1, 1)), e => e.Contains("anterior ao início"));
        Assert.Empty(RegrasRelacionamento.ValidarEncerramento(v, new DateOnly(2026, 9, 26)));

        v.FimEm = new DateOnly(2026, 9, 26);
        Assert.False(v.Vigente(new DateOnly(2026, 9, 27)));
        Assert.Contains(RegrasRelacionamento.ValidarEncerramento(v, new DateOnly(2026, 9, 30)), e => e.Contains("já foi encerrado"));
    }

    // ---- 2, 3 e 4: estabelecimentos da mesma pessoa jurídica; outra raiz é outra pessoa ----

    [Fact]
    public void Pessoa_juridica_tem_matriz_e_varias_filiais_da_mesma_raiz()
    {
        var abc = Empresa("ABC Comércio Ltda", "11222333000181", "11222333000262", "11222333000343");

        Assert.Empty(PessoaValidador.Validar(abc));
        Assert.Equal("11222333", abc.DocumentoPrincipal);
        Assert.True(abc.EstabelecimentoPrincipal()!.EhMatriz());
        Assert.Equal(2, abc.Estabelecimentos.Count(e => !e.Principal));
    }

    [Fact]
    public void Empresa_de_outra_raiz_nunca_e_filial_mesmo_com_os_mesmos_socios()
    {
        var abc = Empresa("ABC Comércio Ltda", "11222333000181", "11444777000161");

        Assert.Contains(PessoaValidador.Validar(abc), e => e.Contains("raiz diferente"));

        // Duas pessoas jurídicas independentes: cada uma com a sua raiz e os seus estabelecimentos.
        var abcSozinha = Empresa("ABC Comércio Ltda", "11222333000181");
        var xyz = Empresa("XYZ Transportes Ltda", "11444777000161", "11444777000242");
        Assert.Empty(PessoaValidador.Validar(abcSozinha));
        Assert.Empty(PessoaValidador.Validar(xyz));
        Assert.NotEqual(abcSozinha.DocumentoPrincipal, xyz.DocumentoPrincipal);
    }

    // ---- 9: estabelecimento gravado não é apagado ----

    [Fact]
    public void Filial_gravada_que_falta_na_gravacao_e_recusada_e_desativar_vira_historico()
    {
        var anterior = Empresa("ABC", "11222333000181", "11222333000262");
        var dados = Empresa("ABC", "11222333000181");
        dados.Id = anterior.Id;
        dados.Estabelecimentos[0].Id = anterior.Estabelecimentos[0].Id;

        Assert.Contains(RegrasEstabelecimento.ValidarCompleto(dados, anterior), e => e.Contains("não veio na gravação"));

        // A ficha manda a filial desativada: aceita, e a mudança vira frase no histórico.
        var filial = anterior.Estabelecimentos[1];
        dados.Estabelecimentos.Add(new Estabelecimento { Id = filial.Id, PessoaId = dados.Id, Cnpj = filial.Cnpj, Ativo = false });
        Assert.Empty(RegrasEstabelecimento.ValidarCompleto(dados, anterior));
        Assert.Contains(RegrasEstabelecimento.Mudancas(anterior, dados), f => f.Contains("11.222.333/0002-62 desativado"));
        Assert.Empty(PessoaValidador.Validar(dados));
    }

    [Fact]
    public void Pessoa_juridica_gravada_nao_vira_pessoa_fisica_e_a_recusa_lista_o_que_seria_perdido()
    {
        var anterior = Empresa("ABC", "11222333000181", "11222333000262");
        anterior.GrupoEmpresarialId = Guid.NewGuid();
        anterior.Estabelecimentos[0].NomeFantasia = "ABC Comércio";
        var dados = PessoaFisica("ABC");
        dados.Id = anterior.Id;
        dados.Estabelecimentos[0].Id = anterior.Estabelecimentos[0].Id;
        dados.GrupoEmpresarialId = anterior.GrupoEmpresarialId; // a ficha manda como está: nada é limpo em silêncio

        var erro = RegrasNaturezaPessoa.ValidarTroca(anterior, dados, vinculosSocietariosComoEmpresa: 2);

        Assert.NotNull(erro);
        Assert.Contains("não pode virar pessoa física", erro);
        Assert.Contains("11.222.333/0001-81", erro);
        Assert.Contains("11.222.333/0002-62", erro);
        Assert.Contains("grupo empresarial", erro);
        Assert.Contains("2 vínculo(s) de sócio/administrador", erro);
        Assert.Contains("nome fantasia", erro);
        Assert.Contains(PessoaValidador.Validar(dados), e => e.Contains("grupo empresarial")); // e o banco tem a CHECK
        Assert.Empty(RegrasEstabelecimento.ValidarCompleto(dados, anterior)); // uma recusa só, a da natureza
    }

    [Fact]
    public void Outras_trocas_de_natureza_e_cadastro_novo_nao_sao_barrados_por_esta_regra()
    {
        var pf = PessoaFisica("Ana");
        var agoraPj = Empresa("Ana ME", "11222333000181");
        agoraPj.Id = pf.Id;

        Assert.Null(RegrasNaturezaPessoa.ValidarTroca(pf, agoraPj, 0)); // física → jurídica
        Assert.Null(RegrasNaturezaPessoa.ValidarTroca(null, agoraPj, 0)); // cadastro novo
        Assert.Null(RegrasNaturezaPessoa.ValidarTroca(agoraPj, agoraPj, 0)); // continua jurídica
    }

    [Fact]
    public void Filial_inativa_continua_existindo_so_as_regras_operacionais_deixam_de_valer()
    {
        var abc = Empresa("ABC", "11222333000181", "11222333000262");
        var filial = abc.Estabelecimentos[1];
        var enderecoAntigo = new PessoaEndereco { Id = Guid.NewGuid(), PessoaId = abc.Id, Logradouro = "Rua Velha", Ativo = false };
        abc.Enderecos.Add(enderecoAntigo);
        filial.Ativo = false;
        filial.IndicadorIE = IndicadorIE.Contribuinte; // sem IE: operacional, não vale para quem não opera
        filial.EnderecoFiscalId = enderecoAntigo.Id;   // endereço da época, hoje inativo: fica como estava

        Assert.Empty(PessoaValidador.Validar(abc));

        filial.InscricaoSuframa = "12";                // formato continua valendo para todos
        Assert.Contains(PessoaValidador.Validar(abc), e => e.Contains("SUFRAMA"));
        filial.InscricaoSuframa = null;
        filial.EnderecoFiscalId = Guid.NewGuid();      // referência fora do cadastro continua recusada
        Assert.Contains(PessoaValidador.Validar(abc), e => e.Contains("não está entre os endereços"));

        filial.EnderecoFiscalId = enderecoAntigo.Id;
        filial.Ativo = true;                           // reativada: volta a valer tudo
        Assert.Contains(PessoaValidador.Validar(abc), e => e.Contains("contribuinte do ICMS precisa ter inscrição estadual"));
        Assert.Contains(PessoaValidador.Validar(abc), e => e.Contains("endereço fiscal foi removido"));
    }

    [Fact]
    public void Troca_do_principal_e_inclusao_de_filial_viram_frases()
    {
        var anterior = Empresa("ABC", "11222333000181", "11222333000262");
        var dados = Empresa("ABC", "11222333000262", "11222333000181", "11222333000343");
        dados.Id = anterior.Id;
        dados.Estabelecimentos[0].Id = anterior.Estabelecimentos[1].Id;
        dados.Estabelecimentos[1].Id = anterior.Estabelecimentos[0].Id;

        var frases = RegrasEstabelecimento.Mudancas(anterior, dados).ToList();
        Assert.Contains(frases, f => f.Contains("11.222.333/0003-43 incluído"));
        Assert.Contains(frases, f => f.Contains("principal alterado de 11.222.333/0001-81 para 11.222.333/0002-62"));
    }

    // ---- 5, 6 e 10: grupo empresarial opcional, só de pessoa jurídica ----

    [Fact]
    public void Duas_empresas_independentes_podem_estar_no_mesmo_grupo_e_empresa_sem_grupo_e_valida()
    {
        var grupo = new GrupoEmpresarial { Id = Guid.NewGuid(), Nome = "Grupo João" };
        var abc = Empresa("ABC Comércio Ltda", "11222333000181", "11222333000262");
        var xyz = Empresa("XYZ Transportes Ltda", "11444777000161");
        var semGrupo = Empresa("Outra Ltda", "11444777000242");
        abc.GrupoEmpresarialId = grupo.Id;
        xyz.GrupoEmpresarialId = grupo.Id;

        Assert.Empty(PessoaValidador.Validar(abc));
        Assert.Empty(PessoaValidador.Validar(xyz));
        Assert.Null(RegrasGrupoEmpresarial.ValidarEscolhido(abc.GrupoEmpresarialId, null, grupo));

        Assert.Null(semGrupo.GrupoEmpresarialId); // cadastrar uma empresa não exige grupo
        Assert.Empty(PessoaValidador.Validar(semGrupo));
        Assert.Null(RegrasGrupoEmpresarial.ValidarEscolhido(null, null, null));

        // Continuam duas pessoas, cada uma com os seus estabelecimentos (o grupo não vira matriz/filial).
        Assert.NotEqual(abc.Id, xyz.Id);
        Assert.Equal(2, abc.Estabelecimentos.Count);
        Assert.Single(xyz.Estabelecimentos);
    }

    [Fact]
    public void Pessoa_fisica_nao_entra_direto_em_grupo_empresarial()
    {
        var joao = PessoaFisica("João");
        joao.GrupoEmpresarialId = Guid.NewGuid();

        Assert.Contains(PessoaValidador.Validar(joao), e => e.Contains("Só pessoa jurídica pode fazer parte de um grupo empresarial"));
    }

    [Fact]
    public void Grupo_desativado_fica_so_em_quem_ja_tinha_e_a_entrada_e_saida_viram_frase()
    {
        var grupo = new GrupoEmpresarial { Id = Guid.NewGuid(), Nome = "Grupo João", Ativo = false };

        Assert.NotNull(RegrasGrupoEmpresarial.ValidarEscolhido(grupo.Id, null, grupo));
        Assert.Null(RegrasGrupoEmpresarial.ValidarEscolhido(grupo.Id, grupo.Id, grupo));
        Assert.NotNull(RegrasGrupoEmpresarial.ValidarEscolhido(Guid.NewGuid(), null, null));

        string Nome(Guid _) => grupo.Nome;
        Assert.Equal("Entrou no grupo empresarial 'Grupo João'.", RegrasGrupoEmpresarial.Mudanca(null, grupo.Id, Nome));
        Assert.Equal("Saiu do grupo empresarial 'Grupo João'.", RegrasGrupoEmpresarial.Mudanca(grupo.Id, null, Nome));
        Assert.Null(RegrasGrupoEmpresarial.Mudanca(grupo.Id, grupo.Id, Nome));
    }

    [Fact]
    public void Nome_do_grupo_e_normalizado_e_obrigatorio()
    {
        var grupo = new GrupoEmpresarial { Nome = "  Grupo   João " };
        RegrasGrupoEmpresarial.Normalizar(grupo);
        Assert.Equal("Grupo João", grupo.Nome);
        Assert.Empty(RegrasGrupoEmpresarial.Validar(grupo));
        Assert.NotEmpty(RegrasGrupoEmpresarial.Validar(new GrupoEmpresarial()));
    }

    // ---- 7 e 8: cliente e fornecedor na mesma pessoa; contextos por empresa ----

    [Fact]
    public void A_mesma_pessoa_e_cliente_e_fornecedor_e_pode_ter_contas_por_empresa()
    {
        var joao = PessoaFisica("João da Silva");
        var empresaA = Guid.NewGuid();
        var empresaB = Guid.NewGuid();
        joao.Papeis.Add(new PessoaPapel { Id = Guid.NewGuid(), PessoaId = joao.Id, Papel = TipoPapel.Cliente, Ativo = true });
        joao.Papeis.Add(new PessoaPapel { Id = Guid.NewGuid(), PessoaId = joao.Id, Papel = TipoPapel.Fornecedor, Ativo = true });
        joao.ContasCliente.Add(new ContaCliente { Id = Guid.NewGuid(), PessoaId = joao.Id, EmpresaId = empresaA, LimiteCredito = 1000m });
        joao.ContasFornecedor.Add(new ContaFornecedor { Id = Guid.NewGuid(), PessoaId = joao.Id, EmpresaId = empresaB, Avaliacao = 4 });

        Assert.True(joao.TemPapel(TipoPapel.Cliente));
        Assert.True(joao.TemPapel(TipoPapel.Fornecedor));
        Assert.Empty(PessoaValidador.Validar(joao));

        // Uma pessoa só: a relação comercial muda por empresa (cliente da A, fornecedor da B), o cadastro não.
        Assert.Equal(empresaA, Assert.Single(joao.ContasCliente).EmpresaId);
        Assert.Equal(empresaB, Assert.Single(joao.ContasFornecedor).EmpresaId);
    }

    // ---- Nome que identifica a pessoa (cabeçalho e lista) ----

    [Theory]
    [InlineData(NaturezaPessoa.Juridica, "ABC Comércio e Participações Ltda", null, null, "ABC Comércio", "ABC Comércio")]
    [InlineData(NaturezaPessoa.Juridica, "ABC Comércio e Participações Ltda", "ABC Matriz", null, "ABC Comércio", "ABC Matriz")]
    [InlineData(NaturezaPessoa.Juridica, "ABC Comércio e Participações Ltda", null, null, "  ", "ABC Comércio e Participações Ltda")]
    [InlineData(NaturezaPessoa.Fisica, "João da Silva Santos", null, "Joana", "ignorado", "Joana")]
    [InlineData(NaturezaPessoa.Fisica, "João da Silva Santos", "João Silva", "Joana", null, "João Silva")]
    [InlineData(NaturezaPessoa.Fisica, "João da Silva Santos", null, null, null, "João da Silva Santos")]
    [InlineData(NaturezaPessoa.Estrangeiro, "John Smith", null, null, "ignorado", "John Smith")]
    public void Nome_para_exibir_segue_a_precedencia(NaturezaPessoa natureza, string nome, string? exibicao, string? social, string? fantasia,
                                                    string esperado)
    {
        Assert.Equal(esperado, NomePessoa.ParaExibir(natureza, nome, exibicao, social, fantasia));
    }

    [Fact]
    public void Nome_da_pessoa_juridica_usa_o_nome_fantasia_do_estabelecimento_principal()
    {
        var abc = Empresa("ABC Comércio e Participações Ltda", "11222333000181", "11222333000262");
        abc.Estabelecimentos[0].NomeFantasia = "ABC Comércio";
        abc.Estabelecimentos[1].NomeFantasia = "ABC Filial";

        Assert.Equal("ABC Comércio", abc.NomeParaExibir());
        Assert.Equal("ABC Comércio e Participações Ltda", abc.Nome); // nada gravado muda
    }
}
