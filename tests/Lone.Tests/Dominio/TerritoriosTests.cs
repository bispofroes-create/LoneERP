using Lone.Domain.Comercial;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Territorios;

namespace Lone.Tests.Dominio;

/// <summary>
/// Estrutura dos territórios (Fase 2b-1a): código estável, árvore sem ciclo e no mesmo mapa, nome único entre irmãos
/// ativos, limite de níveis, "existe desde" coerente com o pai e os filhos, uso operacional na subárvore (T14/T18),
/// encerrar/reativar, mapa com campos travados depois do uso e responsáveis (pessoa ou equipe, função, histórico).
/// </summary>
public class TerritoriosTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 29);
    private static readonly Guid MapaId = Guid.NewGuid();
    private static readonly Guid Geografico = TiposTerritorioIniciais.Todos[0].Id;
    private static readonly IReadOnlySet<Guid> SemUso = new HashSet<Guid>();

    private static readonly MapaTerritorial Mapa = new()
    {
        Id = MapaId, Codigo = "GEOGRAFIA", Nome = "Geografia", FinalidadeEnderecoReferenciaId = Guid.NewGuid(),
        Classificacoes = [new MapaTerritorialClassificacao { Id = Guid.NewGuid(), MapaId = MapaId, PapelId = Guid.NewGuid() }]
    };

    private static readonly Dictionary<Guid, TipoTerritorio> Tipos = TiposTerritorioIniciais.Todos
        .ToDictionary(t => t.Id, t => new TipoTerritorio { Id = t.Id, Codigo = t.Codigo, Nome = t.Nome });

    private static Territorio T(string nome, Territorio? pai = null, DateOnly? inicio = null, string? codigo = null)
    {
        var t = new Territorio
        {
            Id = Guid.NewGuid(), MapaId = MapaId, Codigo = codigo ?? RegrasCadastroTerritorial.NormalizarCodigo(nome), Nome = nome, TipoId = Geografico,
            PaiId = pai?.Id
        };
        t.Posicoes.Add(new TerritorioPosicao { Id = Guid.NewGuid(), MapaId = MapaId, TerritorioId = t.Id, PaiId = t.PaiId, InicioEm = inicio ?? new(2026, 1, 1) });
        return t;
    }

    /// <summary>Cópia editável (o que a tela manda), com as posições ajustadas como o serviço faz.</summary>
    private static Territorio Editar(Territorio gravado, Action<Territorio> mudar, bool usoNaSubarvore = false, DateOnly? inicio = null)
    {
        var dados = new Territorio
        {
            Id = gravado.Id, MapaId = gravado.MapaId, Codigo = gravado.Codigo, Nome = gravado.Nome, TipoId = gravado.TipoId, PaiId = gravado.PaiId,
            Situacao = gravado.Situacao, FimEm = gravado.FimEm
        };
        mudar(dados);
        RegrasArvoreTerritorial.AjustarPosicoes(dados, gravado, inicio ?? RegrasArvoreTerritorial.InicioDe(gravado)!.Value, usoNaSubarvore);
        return dados;
    }

    private static List<string> Validar(Territorio dados, Territorio? anterior, IReadOnlyCollection<Territorio> doMapa, IReadOnlySet<Guid>? comUso = null) =>
        RegrasArvoreTerritorial.Validar(dados, anterior, doMapa, Mapa, Tipos, comUso ?? SemUso, Hoje);

    // ------------------------------------------------------------------ Código e nome

    [Theory]
    [InlineData("mg norte", "MG_NORTE")]
    [InlineData("  São João del-Rei ", "SAO_JOAO_DEL-REI")]
    [InlineData("Curvelo.01", "CURVELO.01")]
    [InlineData("A  &  B", "A_B")]
    public void Codigo_fica_estavel_sem_acento_e_em_maiusculas(string digitado, string esperado) =>
        Assert.Equal(esperado, RegrasCadastroTerritorial.NormalizarCodigo(digitado));

    [Fact]
    public void Codigo_repetido_no_mapa_nao_passa_nem_contra_encerrado()
    {
        var mg = T("MG");
        mg.Situacao = SituacaoTerritorio.Encerrado;
        mg.FimEm = new(2026, 6, 30);
        var novo = T("Minas", codigo: "MG");
        Assert.Contains(Validar(novo, null, [mg]), e => e.Contains("código MG"));
    }

    [Fact]
    public void Nome_so_repete_entre_irmaos_diferentes_ou_encerrados()
    {
        var bh = T("Belo Horizonte");
        var curvelo = T("Curvelo");
        var centroBh = T("Centro", bh);
        var centroCurvelo = T("Centro", curvelo, codigo: "CENTRO_CURVELO"); // outro pai: pode (o código é único no mapa)
        Assert.Empty(Validar(centroCurvelo, null, [bh, curvelo, centroBh]));

        var outroCentroBh = T("centro", bh, codigo: "CENTRO_2"); // mesmo pai, maiúscula não conta
        Assert.Contains(Validar(outroCentroBh, null, [bh, curvelo, centroBh]), e => e.Contains("abaixo do mesmo território"));

        centroBh.Situacao = SituacaoTerritorio.Encerrado;
        centroBh.FimEm = new(2026, 8, 31);
        Assert.Empty(Validar(outroCentroBh, null, [bh, curvelo, centroBh])); // o encerrado não conta
    }

    // ------------------------------------------------------------------ Árvore

    [Fact]
    public void Mover_para_baixo_de_si_mesmo_ou_de_um_descendente_forma_ciclo_e_nao_passa()
    {
        var a = T("A");
        var b = T("B", a);
        var c = T("C", b);
        var todos = new[] { a, b, c };

        Assert.Contains(Validar(Editar(a, d => d.PaiId = a.Id), a, todos), e => e.Contains("abaixo dele mesmo"));
        Assert.Contains(Validar(Editar(a, d => d.PaiId = b.Id), a, todos), e => e.Contains("formaria um ciclo")); // direto
        Assert.Contains(Validar(Editar(a, d => d.PaiId = c.Id), a, todos), e => e.Contains("formaria um ciclo")); // indireto A→B→C→A
    }

    [Fact]
    public void Pai_precisa_existir_no_mesmo_mapa_e_estar_ativo()
    {
        var deOutroMapa = T("SP");
        deOutroMapa.MapaId = Guid.NewGuid();
        var novo = T("Campinas");
        novo.PaiId = deOutroMapa.Id;
        Assert.Contains(Validar(novo, null, [T("MG")]), e => e.Contains("não existe neste mapa"));

        var encerrado = T("Norte");
        encerrado.Situacao = SituacaoTerritorio.Encerrado;
        encerrado.FimEm = new(2026, 8, 31);
        Assert.Contains(Validar(T("Montes Claros", encerrado), null, [encerrado]), e => e.Contains("está encerrado"));
    }

    [Fact]
    public void Arvore_nao_passa_de_doze_niveis()
    {
        var niveis = new List<Territorio> { T("N1") };
        for (var i = 2; i <= RegrasArvoreTerritorial.ProfundidadeMaxima; i++) niveis.Add(T($"N{i}", niveis[^1]));
        Assert.Empty(Validar(T("Ultimo ok", niveis[^2]), null, niveis)); // nível 12
        Assert.Contains(Validar(T("Demais", niveis[^1]), null, niveis), e => e.Contains("12 níveis")); // nível 13

        // Mover um galho de 3 níveis para o nível 11 também passaria do limite.
        var galho = T("Galho");
        var g2 = T("G2", galho);
        var g3 = T("G3", g2);
        var todos = niveis.Concat([galho, g2, g3]).ToList();
        Assert.Contains(Validar(Editar(galho, d => d.PaiId = niveis[9].Id), galho, todos), e => e.Contains("12 níveis"));
    }

    [Fact]
    public void Existe_desde_respeita_o_pai_os_filhos_e_nao_e_futuro()
    {
        var mg = T("MG", inicio: new(2026, 3, 1));
        var norte = T("Norte", mg, new(2026, 3, 1));
        var todos = new[] { mg, norte };

        Assert.Contains(Validar(T("Sul", mg, new(2026, 2, 1)), null, todos), e => e.Contains("anterior ao território acima"));
        Assert.Contains(Validar(T("Leste", mg, Hoje.AddDays(1)), null, todos), e => e.Contains("data futura"));
        Assert.Contains(Validar(Editar(mg, _ => { }, inicio: new(2026, 4, 1)), mg, todos), e => e.Contains("não pode ser posterior"));
    }

    [Fact]
    public void Sem_uso_mover_corrige_a_posicao_aberta_sem_criar_historico()
    {
        var mg = T("MG");
        var sp = T("SP");
        var curvelo = T("Curvelo", mg);
        var movido = Editar(curvelo, d => d.PaiId = sp.Id);

        Assert.Empty(Validar(movido, curvelo, [mg, sp, curvelo]));
        var posicao = Assert.Single(movido.Posicoes);
        Assert.Equal(sp.Id, posicao.PaiId);
        Assert.Equal(curvelo.Posicoes[0].Id, posicao.Id); // a mesma linha, corrigida
    }

    [Fact]
    public void Com_uso_no_territorio_ou_abaixo_dele_mover_so_por_operacao()
    {
        var mg = T("MG");
        var sp = T("SP");
        var regiao = T("Norte de MG", mg);
        var curvelo = T("Curvelo", regiao);
        var todos = new[] { mg, sp, regiao, curvelo };

        // O nó vazio "Norte de MG" não tem uso, mas Curvelo (abaixo) tem: mover o nó muda o desempate de Curvelo.
        var usoAbaixo = new HashSet<Guid> { curvelo.Id };
        var movido = Editar(regiao, d => d.PaiId = sp.Id, usoNaSubarvore: true);
        Assert.Contains(Validar(movido, regiao, todos, usoAbaixo), e => e.Contains("só por uma operação territorial"));
        Assert.True(RegrasArvoreTerritorial.UsoNaSubarvore(todos, regiao.Id, usoAbaixo));
        Assert.False(RegrasArvoreTerritorial.UsoNaSubarvore(todos, sp.Id, usoAbaixo));

        // Com uso, o código e o início também não mudam; renomear continua livre.
        var usoProprio = new HashSet<Guid> { curvelo.Id };
        Assert.Contains(Validar(Editar(curvelo, d => d.Codigo = "CVL", usoNaSubarvore: true), curvelo, todos, usoProprio),
            e => e.Contains("código não muda"));
        Assert.Empty(Validar(Editar(curvelo, d => d.Nome = "Curvelo (cidade)", usoNaSubarvore: true), curvelo, todos, usoProprio));
    }

    [Fact]
    public void Territorio_nao_muda_de_mapa_e_encerrado_nao_e_alterado()
    {
        var mg = T("MG");
        Assert.Contains(Validar(Editar(mg, d => d.MapaId = Guid.NewGuid()), mg, [mg]), e => e.Contains("não muda de mapa"));

        mg.Situacao = SituacaoTerritorio.Encerrado;
        mg.FimEm = Hoje;
        Assert.Contains(Validar(Editar(mg, d => d.Nome = "Minas"), mg, [mg]), e => e.Contains("reative-o antes"));
    }

    [Fact]
    public void Mapa_desativado_nao_aceita_mudanca_na_arvore()
    {
        var desativado = new MapaTerritorial { Id = MapaId, Nome = "Geografia", Ativo = false };
        var erros = RegrasArvoreTerritorial.Validar(T("MG"), null, [], desativado, Tipos, SemUso, Hoje);
        Assert.Contains(erros, e => e.Contains("está desativado"));
    }

    // ------------------------------------------------------------------ Encerrar e reativar

    [Fact]
    public void Encerrar_exige_nenhum_ativo_abaixo_e_nenhum_uso()
    {
        var mg = T("MG");
        var norte = T("Norte", mg);
        Assert.Contains(RegrasArvoreTerritorial.ValidarEncerramento(mg, [mg, norte], Mapa, SemUso), e => e.Contains("1 território(s) ativo(s)"));
        Assert.Contains(RegrasArvoreTerritorial.ValidarEncerramento(norte, [mg, norte], Mapa, new HashSet<Guid> { norte.Id }),
            e => e.Contains("só por uma operação territorial"));
        Assert.Empty(RegrasArvoreTerritorial.ValidarEncerramento(norte, [mg, norte], Mapa, SemUso));
    }

    [Fact]
    public void Encerrar_fecha_posicao_e_responsaveis_hoje_e_anula_os_que_nem_comecaram()
    {
        var norte = T("Norte");
        var vigente = new TerritorioResponsavel { Id = Guid.NewGuid(), TerritorioId = norte.Id, PessoaId = Guid.NewGuid(), InicioEm = new(2026, 1, 1) };
        var futuro = new TerritorioResponsavel { Id = Guid.NewGuid(), TerritorioId = norte.Id, PessoaId = Guid.NewGuid(), InicioEm = Hoje.AddDays(10) };
        var jaTerminado = new TerritorioResponsavel { Id = Guid.NewGuid(), TerritorioId = norte.Id, PessoaId = Guid.NewGuid(), InicioEm = new(2026, 1, 1), FimEm = new(2026, 5, 31) };
        norte.Responsaveis.AddRange([vigente, futuro, jaTerminado]);

        RegrasArvoreTerritorial.Encerrar(norte, Hoje);

        Assert.Equal(SituacaoTerritorio.Encerrado, norte.Situacao);
        Assert.Equal(Hoje, norte.FimEm);
        Assert.Equal(Hoje, norte.Posicoes.Single().FimEm);
        Assert.Equal(Hoje, vigente.FimEm);
        Assert.False(futuro.Ativo); // anulado, nunca apagado
        Assert.Equal(new DateOnly(2026, 5, 31), jaTerminado.FimEm); // o passado não muda
        Assert.Contains(norte.EventosPendentes, e => e.Contains("encerrado"));
    }

    [Fact]
    public void Reativar_exige_pai_ativo_e_nome_livre_e_reabre_a_posicao()
    {
        var mg = T("MG");
        var norte = T("Norte", mg);
        RegrasArvoreTerritorial.Encerrar(norte, Hoje);
        var outroNorte = T("NORTE", mg, codigo: "NORTE_2");

        Assert.Contains(RegrasArvoreTerritorial.ValidarReativacao(norte, [mg, norte, outroNorte], Mapa, SemUso), e => e.Contains("renomeie"));
        Assert.Empty(RegrasArvoreTerritorial.ValidarReativacao(norte, [mg, norte], Mapa, SemUso));

        RegrasArvoreTerritorial.Encerrar(mg, Hoje);
        Assert.Contains(RegrasArvoreTerritorial.ValidarReativacao(norte, [mg, norte], Mapa, SemUso), e => e.Contains("reative-o primeiro"));

        RegrasArvoreTerritorial.Reativar(mg);
        Assert.True(mg.Ativo);
        Assert.Null(mg.FimEm);
        Assert.Null(mg.Posicoes.Single().FimEm);
    }

    // ------------------------------------------------------------------ Mapa

    private static MapaTerritorial NovoMapa(Action<MapaTerritorial>? mudar = null)
    {
        var m = new MapaTerritorial
        {
            Id = Guid.NewGuid(), Codigo = "SEGMENTOS", Nome = "Segmentos", Exclusivo = true, FinalidadeEnderecoReferenciaId = Guid.NewGuid()
        };
        m.Classificacoes = RegrasMapaTerritorial.SincronizarClassificacoes(m.Id, [], [Guid.NewGuid()]);
        mudar?.Invoke(m);
        return m;
    }

    [Fact]
    public void Mapa_exige_universo_e_endereco_de_referencia_e_nao_repete_nome()
    {
        var existente = NovoMapa();
        var semNada = NovoMapa(m =>
        {
            m.Classificacoes = [];
            m.FinalidadeEnderecoReferenciaId = Guid.Empty;
            m.Codigo = "OUTRO";
            m.Nome = "segmentos";
        });
        var erros = RegrasMapaTerritorial.Validar(semNada, [existente], null, emUso: false);
        Assert.Contains(erros, e => e.Contains("universo"));
        Assert.Contains(erros, e => e.Contains("endereço de referência"));
        Assert.Contains(erros, e => e.Contains("Já existe o mapa \"segmentos\""));
    }

    [Fact]
    public void Com_uso_empresa_exclusividade_endereco_e_universo_travam_e_o_codigo_nunca_muda()
    {
        var gravado = NovoMapa();
        var alterado = NovoMapa(m =>
        {
            m.Id = gravado.Id;
            m.EmpresaId = Guid.NewGuid();
            m.Exclusivo = false;
            m.Classificacoes = RegrasMapaTerritorial.SincronizarClassificacoes(gravado.Id, gravado.Classificacoes, [Guid.NewGuid()]);
        });

        var semUso = RegrasMapaTerritorial.Validar(alterado, [gravado], gravado, emUso: false);
        Assert.DoesNotContain(semUso, e => e.Contains("depois do uso"));

        var comUso = RegrasMapaTerritorial.Validar(alterado, [gravado], gravado, emUso: true);
        Assert.Contains(comUso, e => e.Contains("empresa do mapa"));
        Assert.Contains(comUso, e => e.Contains("exclusividade"));
        Assert.Contains(comUso, e => e.Contains("endereço de referência"));
        Assert.Contains(comUso, e => e.Contains("universo"));

        var outroCodigo = NovoMapa(m => { m.Id = gravado.Id; m.Codigo = "SEG"; m.Classificacoes = gravado.Classificacoes; m.FinalidadeEnderecoReferenciaId = gravado.FinalidadeEnderecoReferenciaId; });
        Assert.Contains(RegrasMapaTerritorial.Validar(outroCodigo, [gravado], gravado, emUso: false), e => e.Contains("código do mapa não muda"));
    }

    [Fact]
    public void Desmarcar_classificacao_do_universo_desativa_sem_apagar()
    {
        var mapaId = Guid.NewGuid();
        var cliente = Guid.NewGuid();
        var prospect = Guid.NewGuid();
        var gravadas = RegrasMapaTerritorial.SincronizarClassificacoes(mapaId, [], [cliente]);
        var depois = RegrasMapaTerritorial.SincronizarClassificacoes(mapaId, gravadas, [prospect]);

        Assert.Equal(2, depois.Count);
        Assert.False(depois.Single(c => c.PapelId == cliente).Ativo);
        Assert.Equal(gravadas[0].Id, depois.Single(c => c.PapelId == cliente).Id);
        Assert.True(depois.Single(c => c.PapelId == prospect).Ativo);
    }

    // ------------------------------------------------------------------ Responsáveis

    private static readonly Guid Vendedor = Guid.NewGuid();
    private static readonly Guid Supervisor = Guid.NewGuid();
    private static readonly Guid ClassificacaoVendedor = Guid.NewGuid();
    private static readonly Guid ClassificacaoFuncionario = Guid.NewGuid();
    private static readonly Guid Joao = Guid.NewGuid();
    private static readonly Guid Maria = Guid.NewGuid();
    private static readonly Guid EquipeSul = Guid.NewGuid();
    private static readonly Guid EquipeNorte = Guid.NewGuid();

    private static readonly Dictionary<Guid, TipoCarteira> Funcoes = new()
    {
        [Vendedor] = new TipoCarteira
        {
            Id = Vendedor, Nome = "Vendedor",
            Classificacoes = [new TipoCarteiraClassificacao { Id = Guid.NewGuid(), TipoCarteiraId = Vendedor, PapelId = ClassificacaoVendedor }]
        },
        [Supervisor] = new TipoCarteira
        {
            Id = Supervisor, Nome = "Supervisor",
            Classificacoes = [new TipoCarteiraClassificacao { Id = Guid.NewGuid(), TipoCarteiraId = Supervisor, PapelId = ClassificacaoFuncionario }]
        }
    };

    private static readonly Dictionary<Guid, PessoaElegivel> Pessoas = new()
    {
        [Joao] = new PessoaElegivel("João", new HashSet<Guid> { ClassificacaoVendedor }),
        [Maria] = new PessoaElegivel("Maria", new HashSet<Guid> { ClassificacaoVendedor, ClassificacaoFuncionario })
    };
    private static readonly Dictionary<Guid, Equipe> Equipes = new()
    {
        [EquipeSul] = new Equipe { Id = EquipeSul, Nome = "Equipe Sul" },
        [EquipeNorte] = new Equipe { Id = EquipeNorte, Nome = "Equipe Norte" }
    };
    private static readonly Dictionary<Guid, string> Nomes = new() { [Joao] = "João", [Maria] = "Maria", [EquipeSul] = "Equipe Sul", [EquipeNorte] = "Equipe Norte" };

    private static TerritorioResponsavel R(Guid? pessoa, Guid? equipe, Guid funcao, DateOnly inicio, DateOnly? fim = null) =>
        new() { Id = Guid.NewGuid(), PessoaId = pessoa, EquipeId = equipe, TipoCarteiraId = funcao, InicioEm = inicio, FimEm = fim };

    private static List<string> ValidarResponsaveis(Territorio dados, Territorio? anterior = null) =>
        RegrasResponsavelTerritorio.Validar(dados, anterior, new DateOnly(2026, 1, 1), Hoje, Funcoes, Pessoas, Equipes, Nomes);

    [Fact]
    public void Responsavel_e_pessoa_ou_equipe_e_a_pessoa_precisa_poder_ocupar_a_funcao()
    {
        var t = T("MG");
        t.Responsaveis.Add(R(Joao, null, Vendedor, new(2026, 2, 1)));
        t.Responsaveis.Add(R(null, EquipeSul, Supervisor, new(2026, 2, 1)));
        Assert.Empty(ValidarResponsaveis(t));

        var ambos = T("SP");
        ambos.Responsaveis.Add(R(Joao, EquipeSul, Vendedor, new(2026, 2, 1)));
        Assert.Contains(ValidarResponsaveis(ambos), e => e.Contains("uma pessoa ou uma equipe"));

        var semClassificacao = T("RJ");
        semClassificacao.Responsaveis.Add(R(Joao, null, Supervisor, new(2026, 2, 1)));
        Assert.Contains(ValidarResponsaveis(semClassificacao), e => e.Contains("João não pode ser \"Supervisor\""));
    }

    [Fact]
    public void Mesma_pessoa_na_mesma_funcao_nao_sobrepoe_e_nao_comeca_antes_do_territorio()
    {
        var t = T("MG");
        t.Responsaveis.Add(R(Joao, null, Vendedor, new(2026, 2, 1), new(2026, 6, 30)));
        t.Responsaveis.Add(R(Joao, null, Vendedor, new(2026, 6, 30)));
        Assert.Contains(ValidarResponsaveis(t), e => e.Contains("duas vezes na mesma função"));

        var antes = T("SP");
        antes.Responsaveis.Add(R(Joao, null, Vendedor, new(2025, 12, 1)));
        Assert.Contains(ValidarResponsaveis(antes), e => e.Contains("anterior à existência do território"));
    }

    /// <summary>
    /// A invariante de sobreposição na aplicação (a mesma do gatilho do banco): mesmo território + função + pessoa, ou mesma
    /// equipe; início e fim contam como dias de vigência; fim vazio = em aberto; anulados não contam.
    /// </summary>
    [Theory]
    [InlineData("período válido (termina num dia, o outro começa no seguinte)", false)]
    [InlineData("período sobreposto (encosta no último dia)", true)]
    [InlineData("dois períodos em aberto", true)]
    [InlineData("histórico com três períodos em sequência", false)]
    [InlineData("anulado cruzando o período não conta", false)]
    [InlineData("mesma pessoa em funções diferentes", false)]
    [InlineData("pessoas diferentes na mesma função", false)]
    [InlineData("equipes diferentes na mesma função", false)]
    [InlineData("pessoa e equipe na mesma função", false)]
    public void Sobreposicao_de_responsaveis_so_na_mesma_funcao_e_pessoa_ou_equipe(string caso, bool conflito)
    {
        var t = T("MG");
        DateOnly D(int mes, int dia) => new(2026, mes, dia);
        TerritorioResponsavel[] responsaveis = caso switch
        {
            "período válido (termina num dia, o outro começa no seguinte)" => [R(Joao, null, Vendedor, D(2, 1), D(3, 31)), R(Joao, null, Vendedor, D(4, 1))],
            "período sobreposto (encosta no último dia)" => [R(Joao, null, Vendedor, D(2, 1), D(3, 31)), R(Joao, null, Vendedor, D(3, 31), D(6, 30))],
            "dois períodos em aberto" => [R(Joao, null, Vendedor, D(2, 1)), R(Joao, null, Vendedor, D(7, 1))],
            "histórico com três períodos em sequência" =>
                [R(Joao, null, Vendedor, D(2, 1), D(3, 31)), R(Joao, null, Vendedor, D(4, 1), D(6, 30)), R(Joao, null, Vendedor, D(7, 1))],
            "anulado cruzando o período não conta" => [R(Joao, null, Vendedor, D(2, 1)), Anulado(R(Joao, null, Vendedor, D(3, 1)))],
            "mesma pessoa em funções diferentes" => [R(Maria, null, Vendedor, D(2, 1)), R(Maria, null, Supervisor, D(2, 1))],
            "pessoas diferentes na mesma função" => [R(Joao, null, Vendedor, D(2, 1)), R(Maria, null, Vendedor, D(2, 1))],
            "equipes diferentes na mesma função" => [R(null, EquipeSul, Vendedor, D(2, 1)), R(null, EquipeNorte, Vendedor, D(2, 1))],
            "pessoa e equipe na mesma função" => [R(Joao, null, Vendedor, D(2, 1)), R(null, EquipeSul, Vendedor, D(2, 1))],
            _ => throw new ArgumentOutOfRangeException(nameof(caso))
        };
        t.Responsaveis.AddRange(responsaveis);

        var erros = ValidarResponsaveis(t);
        if (conflito) Assert.Contains(erros, e => e.Contains("duas vezes na mesma função"));
        else Assert.Empty(erros);
    }

    [Fact]
    public void Territorios_diferentes_nao_conflitam_entre_si()
    {
        // A regra é por território: a mesma pessoa, na mesma função e no mesmo período, em MG e em SP.
        var mg = T("MG");
        var sp = T("SP");
        mg.Responsaveis.Add(R(Joao, null, Vendedor, new(2026, 2, 1)));
        sp.Responsaveis.Add(R(Joao, null, Vendedor, new(2026, 2, 1)));
        Assert.Empty(ValidarResponsaveis(mg));
        Assert.Empty(ValidarResponsaveis(sp));
    }

    private static TerritorioResponsavel Anulado(TerritorioResponsavel r)
    {
        r.Ativo = false;
        return r;
    }

    [Fact]
    public void Quem_ja_comecou_so_muda_o_fim_e_quem_nao_comecou_pode_ser_anulado()
    {
        var gravado = T("MG");
        var comecou = R(Joao, null, Vendedor, new(2026, 2, 1));
        var futuro = R(null, EquipeSul, Supervisor, Hoje.AddDays(5));
        gravado.Responsaveis.AddRange([comecou, futuro]);

        var trocaPessoa = T("MG");
        trocaPessoa.Id = gravado.Id;
        trocaPessoa.Responsaveis.Add(new TerritorioResponsavel { Id = comecou.Id, EquipeId = EquipeSul, TipoCarteiraId = Vendedor, InicioEm = comecou.InicioEm });
        Assert.Contains(ValidarResponsaveis(trocaPessoa, gravado), e => e.Contains("histórico não é reescrito"));

        var encerraEAnula = T("MG");
        encerraEAnula.Id = gravado.Id;
        encerraEAnula.Responsaveis.Add(new TerritorioResponsavel
            { Id = comecou.Id, PessoaId = Joao, TipoCarteiraId = Vendedor, InicioEm = comecou.InicioEm, FimEm = Hoje });
        encerraEAnula.Responsaveis.Add(new TerritorioResponsavel
            { Id = futuro.Id, EquipeId = EquipeSul, TipoCarteiraId = Supervisor, InicioEm = futuro.InicioEm, Ativo = false });
        Assert.Empty(ValidarResponsaveis(encerraEAnula, gravado));

        var anulaQuemComecou = T("MG");
        anulaQuemComecou.Id = gravado.Id;
        anulaQuemComecou.Responsaveis.Add(new TerritorioResponsavel
            { Id = comecou.Id, PessoaId = Joao, TipoCarteiraId = Vendedor, InicioEm = comecou.InicioEm, Ativo = false });
        Assert.Contains(ValidarResponsaveis(anulaQuemComecou, gravado), e => e.Contains("não é removido"));
    }
}
