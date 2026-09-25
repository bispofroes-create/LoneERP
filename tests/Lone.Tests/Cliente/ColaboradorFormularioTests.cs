using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Empresas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

public class ColaboradorFormularioTests
{
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly DepartamentoDto Comercial = new() { Id = Guid.NewGuid(), Nome = "Comercial" };
    private static readonly SetorDto Televendas = new() { Id = Guid.NewGuid(), Nome = "Televendas", DepartamentoId = Comercial.Id };
    private static readonly SetorDto Folha = new() { Id = Guid.NewGuid(), Nome = "Folha", DepartamentoId = Guid.NewGuid() };

    private static ColaboradorOpcoesDto Opcoes() => new()
    {
        Empresas = [new EmpresaResumo(Empresa, "Lone Matriz", true)],
        Departamentos = [Comercial],
        Setores = [Televendas, Folha]
    };

    [Fact]
    public void Vinculo_novo_comeca_a_lotacao_na_admissao()
    {
        var v = VinculoFormulario.Novo(new OpcoesColaborador(Opcoes()));
        v.Empresa = v.Empresas.First(e => e.Valor == Empresa);
        v.AdmissaoEm = "10/01/2024";

        Assert.Empty(v.Validar("Vínculo 1"));
        var dto = v.ParaDto();
        Assert.Equal(new DateOnly(2024, 1, 10), Assert.Single(dto.Lotacoes).InicioEm);
    }

    [Fact]
    public void Nova_lotacao_encerra_a_atual_e_remover_antes_de_salvar_reabre()
    {
        var gravado = new VinculoDto
        {
            Id = Guid.NewGuid(), EmpresaId = Empresa, AdmissaoEm = new DateOnly(2024, 1, 10),
            Lotacoes = [new LotacaoDto { Id = Guid.NewGuid(), InicioEm = new DateOnly(2024, 1, 10), DepartamentoId = Comercial.Id }]
        };
        var v = VinculoFormulario.De(gravado);
        var atual = v.Lotacoes[0];

        v.NovaLotacaoCommand.Execute(null);

        Assert.Equal(2, v.Lotacoes.Count);
        Assert.False(atual.EmAberto);
        Assert.Equal(Comercial.Id, v.Lotacoes[0].ParaDto().DepartamentoId); // copia os dados da atual

        v.Lotacoes[0].RemoverCommand.Execute(null);
        Assert.Single(v.Lotacoes);
        Assert.True(atual.EmAberto);

        atual.RemoverCommand.Execute(null); // lotação gravada é histórico: não sai
        Assert.Single(v.Lotacoes);
    }

    [Fact]
    public void Desligamento_encerra_a_lotacao_aberta_no_envio()
    {
        var v = VinculoFormulario.De(new VinculoDto
        {
            Id = Guid.NewGuid(), EmpresaId = Empresa, AdmissaoEm = new DateOnly(2024, 1, 10),
            Lotacoes = [new LotacaoDto { Id = Guid.NewGuid(), InicioEm = new DateOnly(2024, 1, 10) }]
        });
        v.DesligamentoEm = "31/05/2025";

        Assert.Equal(new DateOnly(2025, 5, 31), v.ParaDto().Lotacoes[0].FimEm);
    }

    [Fact]
    public void Setores_acompanham_o_departamento()
    {
        var v = VinculoFormulario.Novo(new OpcoesColaborador(Opcoes()));
        var l = v.Lotacoes[0];

        l.Departamento = l.Departamentos.First(d => d.Valor == Comercial.Id);

        Assert.Contains(l.Setores, s => s.Valor == Televendas.Id);
        Assert.DoesNotContain(l.Setores, s => s.Valor == Folha.Id);
    }

    [Fact]
    public void Aba_colaborador_aparece_para_funcionario_e_some_sem_permissao()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Fisica);
        Assert.DoesNotContain(SecaoOpcao.Para(f), s => s.Secao == SecaoPessoa.Colaborador);

        f.Papeis.First(p => p.Papel == TipoPapel.Funcionario).Ativo = true;
        Assert.Contains(SecaoOpcao.Para(f), s => s.Secao == SecaoPessoa.Colaborador);

        var oculto = PessoaFormulario.De(new PessoaDto { Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica, ColaboradorOculto = true });
        Assert.DoesNotContain(SecaoOpcao.Para(oculto), s => s.Secao == SecaoPessoa.Colaborador);
    }
}
