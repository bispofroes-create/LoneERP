using Lone.Domain.Comercial;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Tests.Dominio;

/// <summary>
/// Escopo de acesso da Fase 2a-2 (F5 e E1 a E7): quem cada alcance enxerga, pela carteira, pela hierarquia das equipes e
/// pelas coberturas de ausência. A expressão testada aqui é a mesma que vai para o banco.
/// </summary>
public class EscopoTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 28);
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid OutraEmpresa = Guid.NewGuid();
    private static readonly Guid Vendedor = Guid.NewGuid();
    private static readonly Guid Representante = Guid.NewGuid();

    private static readonly Guid Ana = Guid.NewGuid();    // gerente (lidera Regional Sul)
    private static readonly Guid Bruno = Guid.NewGuid();  // líder de Televendas Sul (abaixo da Regional)
    private static readonly Guid Carla = Guid.NewGuid();  // membro de Televendas Sul
    private static readonly Guid Diego = Guid.NewGuid();  // outra equipe
    private static readonly Guid Elisa = Guid.NewGuid();  // saiu de Televendas Sul

    private static MembroEquipe Membro(Guid pessoa, DateOnly inicio, DateOnly? fim = null, PapelNaEquipe papel = PapelNaEquipe.Membro) =>
        new() { Id = Guid.NewGuid(), PessoaId = pessoa, InicioEm = inicio, FimEm = fim, Papel = papel };

    private static Equipe Equipe(string nome, Guid? pai, params MembroEquipe[] membros) =>
        new() { Id = Guid.NewGuid(), Nome = nome, EquipePaiId = pai, Membros = membros.ToList() };

    private static CarteiraCliente Vinculo(Guid vendedor, DateOnly? inicio = null, DateOnly? fim = null, Guid? empresa = null,
                                           Guid? tipo = null, bool ativo = true) =>
        new()
        {
            Id = Guid.NewGuid(), PessoaId = Guid.NewGuid(), VendedorId = vendedor, TipoCarteiraId = tipo ?? Vendedor,
            InicioEm = inicio ?? new(2026, 1, 1), FimEm = fim, EmpresaId = empresa, Ativo = ativo
        };

    private static CoberturaComercial Cobertura(Guid titular, Guid? substituto = null, Guid? equipe = null, bool acesso = true,
                                                Guid? tipo = null, Guid? empresa = null, DateOnly? inicio = null, DateOnly? fim = null,
                                                bool cancelada = false) =>
        new()
        {
            Id = Guid.NewGuid(), TitularId = titular, SubstitutoId = substituto, EquipeSubstitutaId = equipe, PermiteAcesso = acesso,
            TipoCarteiraId = tipo, EmpresaId = empresa, InicioEm = inicio ?? new(2026, 9, 20), FimEm = fim ?? new(2026, 10, 10),
            Cancelada = cancelada
        };

    /// <summary>Regional Sul (líder Ana) › Televendas Sul (líder Bruno; Carla; Elisa saiu) ; Norte (Diego) à parte.</summary>
    private static (List<Equipe> Todas, Equipe Regional, Equipe Televendas, Equipe Norte) Estrutura()
    {
        var regional = Equipe("Regional Sul", null, Membro(Ana, new(2026, 1, 1), papel: PapelNaEquipe.Lider));
        var televendas = Equipe("Televendas Sul", regional.Id,
            Membro(Bruno, new(2026, 1, 1), papel: PapelNaEquipe.Lider),
            Membro(Carla, new(2026, 1, 1)),
            Membro(Elisa, new(2026, 1, 1), new(2026, 8, 31)));
        var norte = Equipe("Norte", null, Membro(Diego, new(2026, 1, 1), papel: PapelNaEquipe.Lider));
        return ([regional, televendas, norte], regional, televendas, norte);
    }

    private static EscopoResolvido Resolver(AlcanceComercial alcance, Guid? eu, IReadOnlyCollection<Equipe>? equipes = null,
                                            IReadOnlyCollection<CoberturaComercial>? coberturas = null, Guid? empresa = null) =>
        RegrasEscopo.Resolver(alcance, eu, empresa ?? Empresa, equipes ?? Estrutura().Todas, coberturas ?? [], Hoje);

    private static bool Ve(EscopoResolvido e, params CarteiraCliente[] carteira) => RegrasEscopo.Alcanca(e, carteira);

    [Fact]
    public void Vale_o_maior_alcance_entre_os_perfis_e_sem_perfil_nenhum()
    {
        Assert.Equal(AlcanceComercial.MinhaEquipe, RegrasEscopo.Maior([AlcanceComercial.MinhaCarteira, AlcanceComercial.MinhaEquipe]));
        Assert.Equal(AlcanceComercial.Tudo, RegrasEscopo.Maior([AlcanceComercial.Nenhum, AlcanceComercial.Tudo]));
        Assert.Equal(AlcanceComercial.Nenhum, RegrasEscopo.Maior([]));
    }

    [Fact]
    public void Tudo_ve_qualquer_um_e_nenhum_nao_ve_ninguem()
    {
        var tudo = Resolver(AlcanceComercial.Tudo, null);
        Assert.True(tudo.Tudo);
        Assert.True(Ve(tudo)); // sem carteira nenhuma (fornecedor): também vê

        var nenhum = Resolver(AlcanceComercial.Nenhum, Ana);
        Assert.True(nenhum.Vazio);
        Assert.False(Ve(nenhum, Vinculo(Ana)));
    }

    [Fact]
    public void Sem_pessoa_ligada_o_alcance_restrito_nao_ve_nada()
    {
        var e = Resolver(AlcanceComercial.MinhaCarteira, null);
        Assert.True(e.SemPessoaLigada);
        Assert.True(e.Vazio);
        Assert.False(Ve(e, Vinculo(Ana)));
    }

    [Fact]
    public void Minha_carteira_ve_so_os_clientes_da_propria_carteira_e_nao_os_sem_carteira()
    {
        var e = Resolver(AlcanceComercial.MinhaCarteira, Carla);
        Assert.True(Ve(e, Vinculo(Carla)));
        Assert.False(Ve(e, Vinculo(Bruno)));
        Assert.False(Ve(e)); // F5: fornecedor, funcionário, cliente sem carteira
    }

    [Fact]
    public void Vinculo_encerrado_desativado_e_futuro()
    {
        var e = Resolver(AlcanceComercial.MinhaCarteira, Carla);
        Assert.False(Ve(e, Vinculo(Carla, fim: new(2026, 9, 27))));        // terminou ontem
        Assert.True(Ve(e, Vinculo(Carla, fim: Hoje)));                      // termina hoje
        Assert.False(Ve(e, Vinculo(Carla, ativo: false)));                  // lançado por engano
        Assert.True(Ve(e, Vinculo(Carla, inicio: new(2026, 10, 5))));       // E7: transferência para a semana que vem
    }

    [Fact]
    public void So_contam_os_vinculos_da_empresa_ativa_ou_sem_empresa()
    {
        var e = Resolver(AlcanceComercial.MinhaCarteira, Carla);
        Assert.True(Ve(e, Vinculo(Carla, empresa: null)));
        Assert.True(Ve(e, Vinculo(Carla, empresa: Empresa)));
        Assert.False(Ve(e, Vinculo(Carla, empresa: OutraEmpresa)));        // E6
    }

    [Fact]
    public void Minha_equipe_ve_a_propria_equipe_e_as_de_baixo_mas_nao_as_de_cima_nem_as_do_lado()
    {
        var ana = Resolver(AlcanceComercial.MinhaEquipe, Ana);
        Assert.True(Ve(ana, Vinculo(Ana)));
        Assert.True(Ve(ana, Vinculo(Bruno)));  // Televendas Sul está abaixo da Regional
        Assert.True(Ve(ana, Vinculo(Carla)));
        Assert.False(Ve(ana, Vinculo(Diego))); // Norte é outra árvore

        var bruno = Resolver(AlcanceComercial.MinhaEquipe, Bruno);
        Assert.True(Ve(bruno, Vinculo(Carla)));
        Assert.False(Ve(bruno, Vinculo(Ana))); // a de cima não
    }

    [Fact]
    public void Quem_saiu_da_equipe_nao_entra_e_membro_sem_lideranca_ve_so_a_propria_carteira()
    {
        var bruno = Resolver(AlcanceComercial.MinhaEquipe, Bruno);
        Assert.False(Ve(bruno, Vinculo(Elisa)));

        var carla = Resolver(AlcanceComercial.MinhaEquipe, Carla); // não lidera nada
        Assert.True(Ve(carla, Vinculo(Carla)));
        Assert.False(Ve(carla, Vinculo(Bruno)));
    }

    [Fact]
    public void Lider_que_ja_saiu_da_lideranca_perde_a_equipe()
    {
        var (todas, _, televendas, _) = Estrutura();
        var lideranca = televendas.Membros.Single(m => m.PessoaId == Bruno);
        lideranca.FimEm = new(2026, 9, 1);
        var bruno = Resolver(AlcanceComercial.MinhaEquipe, Bruno, todas);
        Assert.False(Ve(bruno, Vinculo(Carla)));
    }

    [Fact]
    public void Equipe_desativada_nao_abre_a_carteira_dos_membros()
    {
        var (todas, _, televendas, _) = Estrutura();
        televendas.Ativo = false;
        var ana = Resolver(AlcanceComercial.MinhaEquipe, Ana, todas);
        Assert.False(Ve(ana, Vinculo(Carla)));
    }

    [Fact]
    public void Hierarquia_com_ciclo_gravado_nao_trava()
    {
        var (todas, regional, televendas, _) = Estrutura();
        regional.EquipePaiId = televendas.Id; // dado circular vindo de fora
        var ana = Resolver(AlcanceComercial.MinhaEquipe, Ana, todas);
        Assert.True(Ve(ana, Vinculo(Carla)));
    }

    [Fact]
    public void Cobertura_com_acesso_abre_a_carteira_do_titular_so_enquanto_vale()
    {
        var vigente = Resolver(AlcanceComercial.MinhaCarteira, Carla, coberturas: [Cobertura(Diego, substituto: Carla)]);
        Assert.True(Ve(vigente, Vinculo(Diego)));

        var semAcesso = Resolver(AlcanceComercial.MinhaCarteira, Carla, coberturas: [Cobertura(Diego, substituto: Carla, acesso: false)]);
        Assert.False(Ve(semAcesso, Vinculo(Diego)));

        var acabou = Resolver(AlcanceComercial.MinhaCarteira, Carla,
            coberturas: [Cobertura(Diego, substituto: Carla, inicio: new(2026, 9, 1), fim: new(2026, 9, 27))]);
        Assert.False(Ve(acabou, Vinculo(Diego)));

        var cancelada = Resolver(AlcanceComercial.MinhaCarteira, Carla, coberturas: [Cobertura(Diego, substituto: Carla, cancelada: true)]);
        Assert.False(Ve(cancelada, Vinculo(Diego)));
    }

    [Fact]
    public void Cobertura_pela_equipe_vale_para_os_membros_e_para_quem_lidera_quem_cobre()
    {
        var (todas, _, televendas, _) = Estrutura();
        var coberturas = new[] { Cobertura(Diego, equipe: televendas.Id) };
        Assert.True(Ve(Resolver(AlcanceComercial.MinhaCarteira, Carla, todas, coberturas), Vinculo(Diego)));
        Assert.True(Ve(Resolver(AlcanceComercial.MinhaEquipe, Ana, todas, coberturas), Vinculo(Diego)));
        Assert.False(Ve(Resolver(AlcanceComercial.MinhaCarteira, Elisa, todas, coberturas), Vinculo(Diego))); // já saiu
    }

    [Fact]
    public void Cobertura_so_de_um_papel_ou_de_uma_empresa_nao_abre_o_resto()
    {
        var porPapel = Resolver(AlcanceComercial.MinhaCarteira, Carla, coberturas: [Cobertura(Diego, substituto: Carla, tipo: Vendedor)]);
        Assert.True(Ve(porPapel, Vinculo(Diego, tipo: Vendedor)));
        Assert.False(Ve(porPapel, Vinculo(Diego, tipo: Representante)));

        var porEmpresa = Resolver(AlcanceComercial.MinhaCarteira, Carla, coberturas: [Cobertura(Diego, substituto: Carla, empresa: Empresa)]);
        Assert.True(Ve(porEmpresa, Vinculo(Diego, empresa: Empresa)));
        Assert.True(Ve(porEmpresa, Vinculo(Diego, empresa: null)));
        Assert.False(Ve(porEmpresa, Vinculo(Diego, empresa: OutraEmpresa)));
    }

    [Fact]
    public void Cadastro_novo_de_cliente_recebe_quem_cadastrou_como_responsavel()
    {
        var classificacaoVendedor = Guid.NewGuid();
        var responsavel = new TipoCarteira
        {
            Id = Vendedor, Nome = "Vendedor", ResponsavelDaConta = true, Ativo = true,
            Classificacoes = [new TipoCarteiraClassificacao { TipoCarteiraId = Vendedor, PapelId = classificacaoVendedor }]
        };
        var apoio = new TipoCarteira { Id = Representante, Nome = "Representante", Ativo = true };
        var tipos = new Dictionary<Guid, TipoCarteira> { [Vendedor] = responsavel, [Representante] = apoio };
        var cliente = new Pessoa { Id = Guid.NewGuid(), Papeis = [new PessoaPapel { Papel = TipoPapel.Cliente, Ativo = true }] };
        var daCarla = new HashSet<Guid> { classificacaoVendedor };

        var v = RegrasEscopo.ResponsavelDoCadastro(cliente, Carla, daCarla, tipos, Hoje);
        Assert.NotNull(v);
        Assert.Equal(Carla, v!.VendedorId);
        Assert.Equal(Vendedor, v.TipoCarteiraId);
        Assert.Equal(Hoje, v.InicioEm);
        Assert.Null(v.FimEm);
        Assert.Null(v.EmpresaId);
        Assert.Equal(OrigemVinculoCarteira.Cadastro, v.Origem);

        // Quem não pode ser responsável da conta ("Quem pode ser") não entra: a gravação recusaria um vínculo que ele nem viu.
        Assert.Null(RegrasEscopo.ResponsavelDoCadastro(cliente, Carla, new HashSet<Guid> { Guid.NewGuid() }, tipos, Hoje));

        cliente.Carteira = [Vinculo(Bruno, tipo: Vendedor)];
        Assert.Null(RegrasEscopo.ResponsavelDoCadastro(cliente, Carla, daCarla, tipos, Hoje)); // já veio com responsável

        var fornecedor = new Pessoa { Id = Guid.NewGuid(), Papeis = [new PessoaPapel { Papel = TipoPapel.Fornecedor, Ativo = true }] };
        Assert.Null(RegrasEscopo.ResponsavelDoCadastro(fornecedor, Carla, daCarla, tipos, Hoje));
        Assert.False(RegrasEscopo.EhCliente(fornecedor));
    }
}
