using Lone.Application.GruposEmpresariais;
using Lone.Application.Relacionamentos;
using Lone.Application.Seguranca;
using Lone.Contracts.GruposEmpresariais;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Relacionamentos;
using Lone.Domain.Validacao;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// Relacionamentos pelo serviço: o sentido (origem/destino) gravado a partir de qualquer um dos lados, a leitura nos
/// dois sentidos, a permissão dos vínculos societários e o histórico preservado (encerrar e desativar nunca apagam).
/// </summary>
public class PessoaRelacionamentoAppServiceTests
{
    private static readonly TipoRelacionamento SocioDe = new() { Id = TiposRelacionamentoSistema.SocioDe, Nome = "Sócio de", NomeInverso = "Tem como sócio" };
    private static readonly TipoRelacionamento AdministradorDe = new() { Id = TiposRelacionamentoSistema.AdministradorDe, Nome = "Administrador de", NomeInverso = "Tem como administrador" };
    private static readonly TipoRelacionamento ContatoDe = new() { Id = TiposRelacionamentoSistema.ContatoDe, Nome = "Contato de", NomeInverso = "Tem como contato" };

    private readonly RelacionamentosEmMemoria _repositorio = new();
    private readonly AutorizacaoFixa _autorizacao = new();
    private readonly Lone.Tests.Apoio.EscopoFixo _escopo = new();
    private readonly PessoaRelacionamentoAppService _servico;

    private PessoaRelacionamentoAppService ServicoCom(Lone.Tests.Apoio.EscopoFixo escopo) =>
        new(_repositorio, new GruposVazios(), _autorizacao, new MotivoEmMemoria(),
            new FakeTimeProvider(new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero)), escopo, escopo);

    private PessoaRelacionamento Vinculo(Guid origem, TipoRelacionamento tipo, Guid destino) => new()
    {
        Id = Guid.NewGuid(), PessoaId = origem, PessoaDestinoId = destino, TipoRelacionamentoId = tipo.Id, Ativo = true
    };

    [Fact]
    public async Task Com_alcance_restrito_o_contato_de_um_cliente_mostra_so_as_relacoes_com_clientes_do_alcance()
    {
        // E9 (Fase 2a-3): João é contato da ABC (cliente do alcance) e sócio da XYZ (fora). O alcance vem pela relação com a
        // ABC e não abre a XYZ.
        var comAbc = Vinculo(_joao.Id, ContatoDe, _abc.Id);
        var comXyz = Vinculo(_joao.Id, SocioDe, _xyz.Id);
        _repositorio.Vinculos.AddRange([comAbc, comXyz]);
        var escopo = new Lone.Tests.Apoio.EscopoFixo { Restrito = true };
        escopo.NoEscopo.Add(_abc.Id);
        var servico = ServicoCom(escopo);

        var naFichaDoJoao = Assert.Single(await servico.ListarAsync(_joao.Id));
        Assert.Equal("ABC Comércio", naFichaDoJoao.OutraPessoaNome);

        // Na ficha do cliente direto, tudo o que é dele aparece (E3).
        Assert.Single(await servico.ListarAsync(_abc.Id));

        // A relação oculta não pode ser encerrada nem desativada pela ficha do João: é como se não existisse.
        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => servico.EncerrarAsync(_joao.Id, comXyz.Id, new EncerrarRelacionamentoRequisicao()));
        Assert.Contains(erro.Erros, e => e.Contains("não existe mais"));
    }

    [Fact]
    public async Task Com_alcance_restrito_nao_liga_alguem_de_fora_a_um_cliente_do_alcance()
    {
        var escopo = new Lone.Tests.Apoio.EscopoFixo { Restrito = true };
        escopo.NoEscopo.Add(_abc.Id);
        var servico = ServicoCom(escopo);

        await Assert.ThrowsAsync<ForaDoEscopoException>(() => servico.IncluirAsync(_abc.Id,
            new IncluirRelacionamentoRequisicao { TipoRelacionamentoId = ContatoDe.Id, Inverso = true, OutraPessoaId = _xyz.Id }));
        Assert.Empty(_repositorio.Vinculos);

        escopo.NoEscopo.Add(_joao.Id); // João já está no alcance: pode
        await servico.IncluirAsync(_abc.Id, new IncluirRelacionamentoRequisicao { TipoRelacionamentoId = ContatoDe.Id, Inverso = true, OutraPessoaId = _joao.Id });
        Assert.Single(_repositorio.Vinculos);
    }

    private readonly PessoaNoRelacionamento _joao = new(Guid.NewGuid(), NaturezaPessoa.Fisica, SituacaoPessoa.Ativo, "João da Silva");
    private readonly PessoaNoRelacionamento _abc = new(Guid.NewGuid(), NaturezaPessoa.Juridica, SituacaoPessoa.Ativo, "ABC Comércio");
    private readonly PessoaNoRelacionamento _xyz = new(Guid.NewGuid(), NaturezaPessoa.Juridica, SituacaoPessoa.Ativo, "XYZ Transportes");

    public PessoaRelacionamentoAppServiceTests()
    {
        _repositorio.Tipos.AddRange([SocioDe, AdministradorDe, ContatoDe]);
        foreach (var p in new[] { _joao, _abc, _xyz }) _repositorio.Pessoas[p.Id] = p;
        _servico = new PessoaRelacionamentoAppService(_repositorio, new GruposVazios(), _autorizacao, new MotivoEmMemoria(),
            new FakeTimeProvider(new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero)), _escopo, _escopo);
    }

    [Fact]
    public async Task Pela_ficha_da_empresa_tem_como_socio_grava_joao_socio_de_abc_e_os_dois_lados_enxergam()
    {
        var dto = await _servico.IncluirAsync(_abc.Id, new IncluirRelacionamentoRequisicao
        {
            TipoRelacionamentoId = SocioDe.Id, Inverso = true, OutraPessoaId = _joao.Id, InicioEm = new DateOnly(2020, 1, 1)
        });

        var gravado = Assert.Single(_repositorio.Vinculos);
        Assert.Equal(_joao.Id, gravado.PessoaId);        // origem: a pessoa física
        Assert.Equal(_abc.Id, gravado.PessoaDestinoId);  // destino: a empresa
        Assert.Equal("Tem como sócio", dto.Tipo);
        Assert.Equal("João da Silva", dto.OutraPessoaNome);

        var naFichaDoJoao = Assert.Single(await _servico.ListarAsync(_joao.Id));
        Assert.Equal("Sócio de", naFichaDoJoao.Tipo);
        Assert.Equal("ABC Comércio", naFichaDoJoao.OutraPessoaNome);
        Assert.True(naFichaDoJoao.Vigente);
    }

    [Fact]
    public async Task Joao_socio_de_duas_empresas_e_administrador_de_uma_sem_duplicar_a_pessoa()
    {
        await _servico.IncluirAsync(_joao.Id, new IncluirRelacionamentoRequisicao { TipoRelacionamentoId = SocioDe.Id, OutraPessoaId = _abc.Id });
        await _servico.IncluirAsync(_joao.Id, new IncluirRelacionamentoRequisicao { TipoRelacionamentoId = SocioDe.Id, OutraPessoaId = _xyz.Id });
        await _servico.IncluirAsync(_joao.Id, new IncluirRelacionamentoRequisicao { TipoRelacionamentoId = AdministradorDe.Id, OutraPessoaId = _abc.Id });

        Assert.Equal(3, (await _servico.ListarAsync(_joao.Id)).Count);
        Assert.All(_repositorio.Vinculos, v => Assert.Equal(_joao.Id, v.PessoaId));

        var duplicado = await Assert.ThrowsAsync<ValidacaoException>(() => _servico.IncluirAsync(_joao.Id,
            new IncluirRelacionamentoRequisicao { TipoRelacionamentoId = SocioDe.Id, OutraPessoaId = _abc.Id }));
        Assert.Contains(duplicado.Erros, e => e.Contains("já está registrado"));
    }

    [Fact]
    public async Task Vinculo_societario_exige_a_permissao_de_estrutura_empresarial_e_contato_nao()
    {
        _autorizacao.Negadas.Add(Permissoes.Pessoas.EstruturaEmpresarial);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => _servico.IncluirAsync(_joao.Id,
            new IncluirRelacionamentoRequisicao { TipoRelacionamentoId = SocioDe.Id, OutraPessoaId = _abc.Id }));
        Assert.Empty(_repositorio.Vinculos);

        await _servico.IncluirAsync(_joao.Id, new IncluirRelacionamentoRequisicao { TipoRelacionamentoId = ContatoDe.Id, OutraPessoaId = _abc.Id });
        Assert.Single(_repositorio.Vinculos);
    }

    [Fact]
    public async Task Encerrar_e_desativar_nunca_apagam_e_podem_ser_feitos_pelos_dois_lados()
    {
        var dto = await _servico.IncluirAsync(_joao.Id, new IncluirRelacionamentoRequisicao { TipoRelacionamentoId = SocioDe.Id, OutraPessoaId = _abc.Id });

        var encerrado = await _servico.EncerrarAsync(_abc.Id, dto.Id, new EncerrarRelacionamentoRequisicao { FimEm = new DateOnly(2026, 9, 25) });
        Assert.Equal(new DateOnly(2026, 9, 25), encerrado.FimEm);
        Assert.False(encerrado.Vigente);

        var desativado = await _servico.DesativarAsync(_joao.Id, dto.Id, new DesativarRelacionamentoRequisicao { Motivo = "lançado por engano" });
        Assert.False(desativado.Ativo);
        Assert.Single(_repositorio.Vinculos); // continua gravado (histórico)

        await Assert.ThrowsAsync<ValidacaoException>(() => _servico.EncerrarAsync(_xyz.Id, dto.Id, new EncerrarRelacionamentoRequisicao()));
    }

    [Fact]
    public async Task Socio_de_pessoa_fisica_e_recusado()
    {
        var maria = new PessoaNoRelacionamento(Guid.NewGuid(), NaturezaPessoa.Fisica, SituacaoPessoa.Ativo, "Maria");
        _repositorio.Pessoas[maria.Id] = maria;

        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => _servico.IncluirAsync(_joao.Id,
            new IncluirRelacionamentoRequisicao { TipoRelacionamentoId = SocioDe.Id, OutraPessoaId = maria.Id }));
        Assert.Contains(erro.Erros, e => e.Contains("não pode ser pessoa física"));
    }

    // ---- Apoio ----

    private sealed class AutorizacaoFixa : IAutorizacao
    {
        public HashSet<string> Negadas { get; } = new();
        public bool Possui(string permissao) => !Negadas.Contains(permissao);
        public void Exigir(string permissao)
        {
            if (!Possui(permissao)) throw new AcessoNegadoException(permissao);
        }
    }

    private sealed class MotivoEmMemoria : IMotivoDaOperacao
    {
        public string? Motivo { get; set; }
    }

    private sealed class GruposVazios : IGrupoEmpresarialRepositorio
    {
        public Task<List<GrupoEmpresarial>> ListarAsync(CancellationToken ct) => Task.FromResult(new List<GrupoEmpresarial>());
        public Task<GrupoEmpresarial?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult<GrupoEmpresarial?>(null);
        public Task<Dictionary<Guid, int>> ContarEmpresasAsync(Guid? somenteId, CancellationToken ct) => Task.FromResult(new Dictionary<Guid, int>());
        public Task<List<EmpresaDoGrupoEmpresarialDto>> ListarEmpresasAsync(Guid grupoId, CancellationToken ct) => Task.FromResult(new List<EmpresaDoGrupoEmpresarialDto>());
        public Task SalvarAsync(GrupoEmpresarial item, bool novo, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class RelacionamentosEmMemoria : IPessoaRelacionamentoRepositorio
    {
        public List<PessoaRelacionamento> Vinculos { get; } = new();
        public List<TipoRelacionamento> Tipos { get; } = new();
        public Dictionary<Guid, PessoaNoRelacionamento> Pessoas { get; } = new();

        private static PessoaRelacionamento Copia(PessoaRelacionamento r) => new()
        {
            Id = r.Id, PessoaId = r.PessoaId, PessoaDestinoId = r.PessoaDestinoId, TipoRelacionamentoId = r.TipoRelacionamentoId,
            InicioEm = r.InicioEm, FimEm = r.FimEm, Observacoes = r.Observacoes, Ativo = r.Ativo
        };

        public Task<List<PessoaRelacionamento>> ListarDaPessoaAsync(Guid pessoaId, CancellationToken ct) =>
            Task.FromResult(Vinculos.Where(v => v.PessoaId == pessoaId || v.PessoaDestinoId == pessoaId).Select(Copia).ToList());

        public Task<List<PessoaRelacionamento>> ListarDaOrigemAsync(Guid origemId, CancellationToken ct) =>
            Task.FromResult(Vinculos.Where(v => v.PessoaId == origemId).Select(Copia).ToList());

        public Task<PessoaRelacionamento?> ObterAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Vinculos.Where(v => v.Id == id).Select(Copia).FirstOrDefault());

        public Task<int> ContarSocietariosComoEmpresaAsync(Guid pessoaId, CancellationToken ct) =>
            Task.FromResult(Vinculos.Count(v => v.PessoaDestinoId == pessoaId && v.Ativo && v.FimEm == null &&
                                                TiposRelacionamentoSistema.Societarios.Contains(v.TipoRelacionamentoId)));

        public Task<List<TipoRelacionamento>> ListarTiposAsync(CancellationToken ct) => Task.FromResult(Tipos.ToList());

        public Task<Dictionary<Guid, PessoaNoRelacionamento>> PessoasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult(ids.Distinct().Where(Pessoas.ContainsKey).ToDictionary(id => id, id => Pessoas[id]));

        public Task IncluirAsync(PessoaRelacionamento relacionamento, CancellationToken ct)
        {
            Vinculos.Add(Copia(relacionamento));
            return Task.CompletedTask;
        }

        public Task AlterarAsync(Guid id, DateOnly? fimEm, bool ativo, CancellationToken ct)
        {
            var v = Vinculos.Single(x => x.Id == id);
            v.FimEm = fimEm;
            v.Ativo = ativo;
            return Task.CompletedTask;
        }
    }
}
