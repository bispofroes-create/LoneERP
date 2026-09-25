using Lone.Domain.Colaboradores;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Tests.Dominio;

public class ColaboradorTests
{
    private static readonly Guid Empresa = Guid.NewGuid();

    private static (Pessoa Pessoa, VinculoColaborador Vinculo) ComVinculo(DateOnly admissao, DateOnly? desligamento = null)
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica };
        var v = new VinculoColaborador { Id = Guid.NewGuid(), PessoaId = p.Id, EmpresaId = Empresa, AdmissaoEm = admissao, DesligamentoEm = desligamento, Matricula = " 12a " };
        p.Vinculos.Add(v);
        return (p, v);
    }

    private static LotacaoColaborador Lotacao(VinculoColaborador v, DateOnly inicio, DateOnly? fim = null) =>
        new() { Id = Guid.NewGuid(), VinculoId = v.Id, InicioEm = inicio, FimEm = fim };

    [Fact]
    public void Periodos_validos_passam_e_matricula_e_normalizada()
    {
        var (p, v) = ComVinculo(new DateOnly(2024, 1, 10));
        p.Lotacoes.Add(Lotacao(v, new DateOnly(2024, 1, 10), new DateOnly(2025, 2, 28)));
        p.Lotacoes.Add(Lotacao(v, new DateOnly(2025, 3, 1)));

        RegrasColaborador.Normalizar(p);

        Assert.Empty(RegrasColaborador.Validar(p));
        Assert.Equal("12A", v.Matricula);
    }

    [Fact]
    public void Sobreposicao_duas_abertas_e_lotacao_antes_da_admissao_sao_recusadas()
    {
        var (p, v) = ComVinculo(new DateOnly(2024, 1, 10));
        p.Lotacoes.Add(Lotacao(v, new DateOnly(2024, 1, 1)));
        p.Lotacoes.Add(Lotacao(v, new DateOnly(2024, 6, 1)));

        var erros = RegrasColaborador.Validar(p);

        Assert.Contains(erros, e => e.Contains("só uma lotação pode ficar em aberto"));
        Assert.Contains(erros, e => e.Contains("antes da admissão"));
        Assert.Contains(erros, e => e.Contains("se sobrepõem"));
    }

    [Fact]
    public void Lotacao_aberta_depois_do_desligamento_e_recusada()
    {
        var (p, v) = ComVinculo(new DateOnly(2024, 1, 10), new DateOnly(2025, 5, 31));
        p.Lotacoes.Add(Lotacao(v, new DateOnly(2024, 1, 10)));

        Assert.Contains(RegrasColaborador.Validar(p), e => e.Contains("desligamento"));
    }

    [Fact]
    public void Setor_de_outro_departamento_sintetico_e_gestor_de_si_mesmo_sao_recusados()
    {
        var (p, v) = ComVinculo(new DateOnly(2024, 1, 10));
        var comercial = new Departamento { Id = Guid.NewGuid(), Nome = "Comercial" };
        var rh = new Departamento { Id = Guid.NewGuid(), Nome = "RH" };
        var televendas = new Setor { Id = Guid.NewGuid(), Nome = "Televendas", DepartamentoId = comercial.Id };
        var grupo = new CentroCusto { Id = Guid.NewGuid(), Codigo = "1", Nome = "Adm", Analitico = false };
        var l = Lotacao(v, new DateOnly(2024, 1, 10));
        l.DepartamentoId = rh.Id;
        l.SetorId = televendas.Id;
        l.CentroCustoId = grupo.Id;
        l.GestorId = p.Id;
        p.Lotacoes.Add(l);

        var estrutura = new EstruturaParaConferir(
            new Dictionary<Guid, Cargo>(),
            new[] { comercial, rh }.ToDictionary(d => d.Id),
            new Dictionary<Guid, Setor> { [televendas.Id] = televendas },
            new Dictionary<Guid, CentroCusto> { [grupo.Id] = grupo },
            new HashSet<Guid> { Empresa },
            new HashSet<Guid>());

        var erros = RegrasColaborador.ValidarReferencias(p, new Dictionary<Guid, LotacaoColaborador>(), new HashSet<Guid>(), estrutura);

        Assert.Equal(3, erros.Count);
        Assert.Contains(erros, e => e.Contains("não é do departamento"));
        Assert.Contains(erros, e => e.Contains("sintético"));
        Assert.Contains(erros, e => e.Contains("gestora de si mesma"));
    }

    [Fact]
    public void Centro_de_custo_nao_pode_ficar_abaixo_de_um_filho()
    {
        var raiz = new CentroCusto { Id = Guid.NewGuid(), Codigo = "1", Nome = "Adm", Analitico = false };
        var filho = new CentroCusto { Id = Guid.NewGuid(), Codigo = "1.01", Nome = "RH", Analitico = false, PaiId = raiz.Id };
        var editado = new CentroCusto { Id = raiz.Id, Codigo = "1", Nome = "Adm", Analitico = false, PaiId = filho.Id };

        var erros = RegrasEstrutura.ValidarCentroCusto(editado, [raiz, filho]);

        Assert.Contains(erros, e => e.Contains("abaixo dele mesmo"));
    }

    [Fact]
    public void Admissao_e_desligamento_viram_frases_do_historico()
    {
        var (p, v) = ComVinculo(new DateOnly(2024, 1, 10));
        var antes = new VinculoColaborador { Id = v.Id, EmpresaId = Empresa, AdmissaoEm = v.AdmissaoEm };
        v.DesligamentoEm = new DateOnly(2025, 5, 31);
        v.MotivoDesligamento = "pedido de demissão";
        var nomes = new Dictionary<Guid, string> { [Empresa] = "Lone Matriz" };

        Assert.Equal(new[] { "Admitido em 10/01/2024 em Lone Matriz." }, RegrasColaborador.Mudancas([], [new VinculoColaborador { Id = Guid.NewGuid(), EmpresaId = Empresa, AdmissaoEm = v.AdmissaoEm }], nomes).ToArray());
        Assert.Equal(new[] { "Desligado em 31/05/2025 de Lone Matriz (pedido de demissão)." }, RegrasColaborador.Mudancas([antes], [v], nomes).ToArray());
    }
}
