using Lone.Api.Seguranca;
using Lone.Application.Seguranca;
using Lone.Contracts.Seguranca;
using Microsoft.Extensions.Caching.Memory;

namespace Lone.Tests.Api;

public class SegurancaDaRequisicaoTests
{
    private sealed class AcessosContados : IAcessoService
    {
        public int Chamadas { get; private set; }

        public Task<AcessoDoUsuario?> CarregarAsync(Guid usuarioId, Guid? empresaId, CancellationToken ct = default)
        {
            Chamadas++;
            var acesso = new AcessoEfetivo(false, new HashSet<string> { Permissoes.Pessoas.Visualizar });
            return Task.FromResult<AcessoDoUsuario?>(new AcessoDoUsuario(usuarioId, "Maria", false, acesso));
        }
    }

    [Fact]
    public void Sem_login_nao_ha_permissao_nenhuma()
    {
        var usuario = new UsuarioDaRequisicao();

        Assert.False(usuario.Autenticado);
        Assert.Equal("sistema", usuario.Nome);
        Assert.Throws<AcessoNegadoException>(() => usuario.Exigir(Permissoes.Pessoas.Visualizar));
        Assert.Throws<SessaoInvalidaException>(() => usuario.IdObrigatorio);
    }

    [Fact]
    public async Task Permissoes_carregadas_valem_na_requisicao()
    {
        var usuario = new UsuarioDaRequisicao();
        var acesso = await new AcessosContados().CarregarAsync(Guid.NewGuid(), null);
        usuario.Definir(acesso!, empresaId: null, estabelecimentoId: null);

        Assert.True(usuario.Possui(Permissoes.Pessoas.Visualizar));
        Assert.False(usuario.Possui(Permissoes.Pessoas.Editar));
    }

    [Fact]
    public async Task Cache_evita_consultas_repetidas_e_invalidar_forca_nova_leitura()
    {
        using var memoria = new MemoryCache(new MemoryCacheOptions());
        var cache = new CacheAcesso(memoria);
        var servico = new AcessosContados();
        var usuarioId = Guid.NewGuid();

        await cache.ObterAsync(usuarioId, null, servico, CancellationToken.None);
        await cache.ObterAsync(usuarioId, null, servico, CancellationToken.None);
        Assert.Equal(1, servico.Chamadas);

        cache.Invalidar();
        await cache.ObterAsync(usuarioId, null, servico, CancellationToken.None);
        Assert.Equal(2, servico.Chamadas);
    }
}
