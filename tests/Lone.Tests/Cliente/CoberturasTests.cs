using Lone.Cliente.ViewModels.Comercial;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Comercial;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>Ficha de cobertura e aviso de ausência na carteira (Motor Comercial, Fase 1c).</summary>
public class CoberturasTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 28);
    private static readonly Guid ClassVendedor = Guid.NewGuid();
    private static readonly Guid ClassFuncionario = Guid.NewGuid();
    private static readonly Guid Joao = Guid.NewGuid();
    private static readonly Guid Maria = Guid.NewGuid();
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly TipoCarteiraDto Vendedor = new()
    {
        Id = Guid.NewGuid(), Nome = "Vendedor", ResponsavelDaConta = true, LimitePorVez = 1, Ordem = 1, Classificacoes = [ClassVendedor]
    };
    private static readonly TipoCarteiraDto Supervisor = new() { Id = Guid.NewGuid(), Nome = "Supervisor", Ordem = 2, Classificacoes = [ClassFuncionario] };
    private static readonly TipoAusenciaDto Ferias = new() { Id = Guid.NewGuid(), Nome = "Férias", Ordem = 1 };

    private static CoberturaOpcoesDto Opcoes() => new()
    {
        Pessoas =
        [
            new AtendenteOpcaoDto(Joao, "João", [ClassVendedor]),
            new AtendenteOpcaoDto(Maria, "Maria", [ClassVendedor]),
            new AtendenteOpcaoDto(Ana, "Ana", [ClassFuncionario])
        ],
        TiposAusencia = [Ferias],
        Papeis = [Vendedor, Supervisor],
        Equipes = [new PessoaOpcaoDto(Guid.NewGuid(), "Equipe Sul")],
        Parametros = new ParametrosComerciaisDto { CreditoNaAusencia = RegraCreditoAusencia.Substituto }
    };

    [Fact]
    public void Nova_cobertura_vem_com_o_credito_dos_parametros_e_quem_cobre_segue_o_papel()
    {
        var opcoes = Opcoes();
        var f = CoberturaEdicao.Criar(Hoje, opcoes.Parametros);
        f.DefinirOpcoes(opcoes);

        Assert.Equal(RegraCreditoAusencia.Substituto, f.Credito.Valor);
        Assert.Equal(Ferias.Id, f.Tipo.Valor); // o primeiro tipo ativo
        Assert.Equal(["—", "João", "Maria", "Ana"], f.Substitutos.Select(o => o.Texto)); // todos os papéis

        f.Papel = f.Papeis.First(p => p.Valor == Supervisor.Id);
        Assert.Equal(["—", "Ana"], f.Substitutos.Select(o => o.Texto));

        Assert.Contains(f.ValidarLocalmente(), e => e.Contains("quem vai se ausentar"));
        f.Titular = f.Pessoas.First(p => p.Valor == Joao);
        f.Substituto = f.Substitutos.First(p => p.Valor == Ana);
        f.FimEm = "15/10/2026";
        Assert.Empty(f.ValidarLocalmente());

        var dto = f.ParaDto();
        Assert.Equal(Joao, dto.TitularId);
        Assert.Equal(Ana, dto.SubstitutoId);
        Assert.Null(dto.EquipeSubstitutaId);
        Assert.Equal(Supervisor.Id, dto.TipoCarteiraId);
        Assert.Equal(new DateOnly(2026, 10, 15), dto.FimEm);

        f.PorEquipe = CoberturaEdicao.Modos[1];
        Assert.False(f.CobrePessoa);
        Assert.Null(f.ParaDto().SubstitutoId); // cobre por equipe: a pessoa não vai
    }

    [Fact]
    public void Cobertura_que_ja_comecou_trava_e_so_encerra_a_agendada_pode_cancelar()
    {
        var comecou = CoberturaEdicao.De(new CoberturaDto
        {
            Id = Guid.NewGuid(), TitularId = Joao, SubstitutoId = Maria, TipoAusenciaId = Ferias.Id,
            InicioEm = Hoje.AddDays(-2), FimEm = Hoje.AddDays(5), Situacao = SituacaoCobertura.Vigente, Titular = "João"
        }, Hoje);
        Assert.True(comecou.Travado);
        Assert.True(comecou.PodeEncerrarHoje);
        Assert.False(comecou.PodeCancelar);

        var agendada = CoberturaEdicao.De(new CoberturaDto
        {
            Id = Guid.NewGuid(), TitularId = Joao, SubstitutoId = Maria, TipoAusenciaId = Ferias.Id,
            InicioEm = Hoje.AddDays(3), FimEm = Hoje.AddDays(5), Situacao = SituacaoCobertura.Agendada, Titular = "João"
        }, Hoje);
        Assert.False(agendada.Travado);
        Assert.True(agendada.PodeCancelar);
        Assert.False(agendada.PodeEncerrarHoje);
    }

    [Fact]
    public void Carteira_avisa_a_ausencia_de_quem_atende_no_escopo_do_vinculo()
    {
        var opcoes = new ComercialOpcoesDto
        {
            TiposCarteira = [Vendedor, Supervisor],
            Atendentes = [new AtendenteOpcaoDto(Joao, "João", [ClassVendedor])],
            Coberturas =
            [
                new CoberturaAvisoDto(Joao, null, null, Hoje.AddDays(3), Hoje.AddDays(17), "João: Férias de 01/10/2026 a 15/10/2026 · atendimento por Maria (crédito do titular)"),
                new CoberturaAvisoDto(Joao, Supervisor.Id, null, Hoje, Hoje.AddDays(2), "João: só como supervisor")
            ]
        };
        var vinculo = CarteiraFormulario.De(new CarteiraDto
        {
            Id = Guid.NewGuid(), TipoCarteiraId = Vendedor.Id, VendedorId = Joao, InicioEm = new DateOnly(2026, 1, 1)
        }, Hoje);
        vinculo.DefinirOpcoes(new OpcoesComercial(opcoes));

        Assert.True(vinculo.TemCobertura);
        Assert.StartsWith("João: Férias de 01/10/2026", vinculo.AvisoCobertura);
        Assert.DoesNotContain("supervisor", vinculo.AvisoCobertura); // outro papel

        vinculo.Ativo = false;
        Assert.False(vinculo.TemCobertura);
    }
}
