using Lone.Application.Comercial;
using Lone.Application.Metas;
using Lone.Application.Pessoas;
using Lone.Application.Seguranca;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comercial;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// Fase 2a-2 na aplicação: o alcance que vale para o usuário (E1), o escopo resolvido uma vez por requisição, a recusa de
/// cadastro fora do alcance e as regras do cadastro novo com alcance restrito (E2 e E4).
/// </summary>
public class EscopoPessoasTests
{
    private static readonly Guid Carla = Guid.NewGuid();
    private static readonly DateOnly Hoje = new(2026, 9, 28);

    private sealed class AlcanceFixo(AlcanceComercial alcance, Guid? pessoa) : IAlcanceDoUsuario
    {
        public AlcanceComercial Alcance { get; } = alcance;
        public Guid? PessoaId { get; } = pessoa;
    }

    private sealed class Empresa : IEmpresaAtual
    {
        public Guid? EmpresaId => null;
        public Guid? EstabelecimentoId => null;
    }

    private sealed class Equipes : IEquipeRepositorio
    {
        public int Consultas { get; private set; }
        public Task<List<Equipe>> ListarAsync(CancellationToken ct) { Consultas++; return Task.FromResult(new List<Equipe>()); }
        public Task<Equipe?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult<Equipe?>(null);
        public Task SalvarAsync(Equipe equipe, bool novo, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Coberturas : ICoberturaRepositorio
    {
        public int Consultas { get; private set; }
        public Task<CoberturaComercial?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult<CoberturaComercial?>(null);
        public Task<List<CoberturaComercial>> DoTitularAsync(Guid titularId, CancellationToken ct) => Task.FromResult(new List<CoberturaComercial>());
        public Task<List<CoberturaComercial>> ListarAsync(DateOnly desde, bool incluirEncerradas, CancellationToken ct)
        {
            Consultas++;
            return Task.FromResult(new List<CoberturaComercial>());
        }
        public Task SalvarAsync(CoberturaComercial item, bool novo, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>Os cadastros que existem (<see cref="Existem"/>) e, entre eles, os que estão fora do escopo.</summary>
    private sealed class NoEscopo : IPessoasNoEscopo
    {
        public HashSet<Guid> Existem { get; } = new();
        public HashSet<Guid> Fora { get; } = new();
        public int Consultas { get; private set; }
        public Task<HashSet<Guid>> ClientesDiretosAsync(IReadOnlyCollection<Guid> ids, EscopoResolvido escopo, CancellationToken ct) =>
            Task.FromResult(ids.Where(i => Existem.Contains(i) && !Fora.Contains(i)).ToHashSet());

        public Task<SituacaoNoEscopo> SituacaoAsync(Guid pessoaId, EscopoResolvido escopo, CancellationToken ct)
        {
            Consultas++;
            return Task.FromResult(!Existem.Contains(pessoaId) ? SituacaoNoEscopo.NaoExiste
                : Fora.Contains(pessoaId) ? SituacaoNoEscopo.ForaDoEscopo
                : SituacaoNoEscopo.NoEscopo);
        }
    }

    private static (EscopoPessoas Servico, Equipes Equipes, Coberturas Coberturas, NoEscopo NoEscopo) Montar(AlcanceComercial alcance, Guid? pessoa)
    {
        var equipes = new Equipes();
        var coberturas = new Coberturas();
        var noEscopo = new NoEscopo();
        var relogio = new FakeTimeProvider(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
        return (new EscopoPessoas(new AlcanceFixo(alcance, pessoa), new Empresa(), equipes, coberturas, noEscopo, relogio), equipes, coberturas, noEscopo);
    }

    [Fact]
    public async Task Alcance_tudo_nao_consulta_equipes_coberturas_nem_o_cadastro()
    {
        var (servico, equipes, coberturas, noEscopo) = Montar(AlcanceComercial.Tudo, null);
        noEscopo.Fora.Add(Guid.NewGuid());

        Assert.True((await servico.ObterAsync()).Tudo);
        await servico.ExigirAsync(noEscopo.Fora.Single());
        Assert.Equal(0, equipes.Consultas + coberturas.Consultas + noEscopo.Consultas);
    }

    [Fact]
    public async Task Escopo_restrito_e_resolvido_uma_vez_por_requisicao()
    {
        var (servico, equipes, coberturas, _) = Montar(AlcanceComercial.MinhaCarteira, Carla);
        var primeiro = await servico.ObterAsync();
        var segundo = await servico.ObterAsync();

        Assert.Same(primeiro, segundo);
        Assert.Equal(1, equipes.Consultas);
        Assert.Equal(1, coberturas.Consultas);
        Assert.Contains(primeiro.Fontes, f => f.VendedorId == Carla && f.Livre);
    }

    [Fact]
    public async Task Sem_pessoa_ligada_nao_consulta_nada_e_fica_vazio()
    {
        var (servico, equipes, coberturas, _) = Montar(AlcanceComercial.MinhaEquipe, null);
        var escopo = await servico.ObterAsync();
        Assert.True(escopo.SemPessoaLigada);
        Assert.True(escopo.Vazio);
        Assert.Equal(0, equipes.Consultas + coberturas.Consultas);
    }

    [Fact]
    public async Task Cadastro_fora_do_escopo_e_recusado_como_se_nao_existisse()
    {
        var (servico, _, _, noEscopo) = Montar(AlcanceComercial.MinhaCarteira, Carla);
        var fora = Guid.NewGuid();
        var dentro = Guid.NewGuid();
        noEscopo.Existem.UnionWith([fora, dentro]);
        noEscopo.Fora.Add(fora);

        var erro = await Assert.ThrowsAsync<ForaDoEscopoException>(() => servico.ExigirAsync(fora));
        Assert.Equal(ForaDoEscopoException.Mensagem, erro.Message);
        await servico.ExigirAsync(dentro);

        // O id que não existe recebe a mesma recusa (não revela quais existem), menos na gravação de um cadastro novo.
        var inexistente = Guid.NewGuid();
        await Assert.ThrowsAsync<ForaDoEscopoException>(() => servico.ExigirAsync(inexistente));
        await servico.ExigirAsync(inexistente, podeSerNovo: true);
        await Assert.ThrowsAsync<ForaDoEscopoException>(() => servico.ExigirAsync(fora, podeSerNovo: true));
    }

    [Fact]
    public void Alcance_efetivo_e_o_maior_dos_perfis_da_empresa_e_administrador_ve_tudo()
    {
        var empresa = Guid.NewGuid();
        var perfis = new List<PerfilAtribuido>
        {
            new(null, false, new HashSet<string> { Permissoes.Pessoas.Visualizar }, AlcanceComercial.MinhaCarteira),
            new(empresa, false, new HashSet<string>(), AlcanceComercial.MinhaEquipe),
            new(Guid.NewGuid(), false, new HashSet<string>(), AlcanceComercial.Tudo) // outra empresa: não vale aqui
        };
        Assert.Equal(AlcanceComercial.MinhaEquipe, RegrasDeAcesso.Efetivo(perfis, empresa).Alcance);
        Assert.Equal(AlcanceComercial.MinhaCarteira, RegrasDeAcesso.Efetivo(perfis, null).Alcance);

        var administrador = new List<PerfilAtribuido> { new(null, true, new HashSet<string>(), AlcanceComercial.Nenhum) };
        Assert.Equal(AlcanceComercial.Tudo, RegrasDeAcesso.Efetivo(administrador, null).Alcance);
    }

    [Fact]
    public void Cadastro_novo_com_alcance_restrito_so_de_cliente_e_com_pessoa_ligada()
    {
        var cliente = new Pessoa { Papeis = [new PessoaPapel { Papel = TipoPapel.Cliente, Ativo = true }] };
        var fornecedor = new Pessoa { Papeis = [new PessoaPapel { Papel = TipoPapel.Fornecedor, Ativo = true }] };
        EscopoResolvido Escopo(AlcanceComercial a, Guid? p) => RegrasEscopo.Resolver(a, p, null, [], [], Hoje);

        Assert.Empty(PessoaAppService.ErrosDoCadastroRestrito(cliente, Escopo(AlcanceComercial.MinhaCarteira, Carla)));
        Assert.Equal(RegrasEscopo.SoClientes,
            Assert.Single(PessoaAppService.ErrosDoCadastroRestrito(fornecedor, Escopo(AlcanceComercial.MinhaCarteira, Carla))));
        Assert.Contains("não está ligado a uma pessoa",
            Assert.Single(PessoaAppService.ErrosDoCadastroRestrito(cliente, Escopo(AlcanceComercial.MinhaEquipe, null))));
        Assert.Contains("não dá acesso",
            Assert.Single(PessoaAppService.ErrosDoCadastroRestrito(cliente, Escopo(AlcanceComercial.Nenhum, Carla))));
    }
}
