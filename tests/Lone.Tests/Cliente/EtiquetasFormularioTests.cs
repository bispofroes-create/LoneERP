using Lone.Cliente.Api;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Etiquetas;
using Lone.Contracts.Pessoas;

namespace Lone.Tests.Cliente;

public class EtiquetasFormularioTests
{
    private static readonly EtiquetaDto Vip = new() { Id = Guid.NewGuid(), Nome = "VIP", Ativo = true };
    private static readonly EtiquetaDto Atacado = new() { Id = Guid.NewGuid(), Nome = "Atacado", Ativo = true };
    private static readonly EtiquetaDto Promocao = new() { Id = Guid.NewGuid(), Nome = "Promoção", Ativo = true };
    private static readonly EtiquetaDto Antiga = new() { Id = Guid.NewGuid(), Nome = "Antiga", Ativo = false };
    private static readonly EtiquetaDto Esquecida = new() { Id = Guid.NewGuid(), Nome = "Esquecida", Ativo = false };

    [Fact]
    public void Mostra_as_ativas_e_as_desativadas_que_a_pessoa_ja_tem_marcadas_primeiro()
    {
        var f = EtiquetasFormulario.Criar([Vip, Atacado, Antiga, Esquecida], [Vip.Id, Antiga.Id]);

        Assert.Equal(new[] { "Antiga (desativada)", "VIP", "Atacado" }, f.Visiveis.Select(e => e.Texto));
        Assert.Equal(new[] { Antiga.Id, Vip.Id }, f.Marcadas);
    }

    [Fact]
    public void Etiqueta_marcada_fora_do_cadastro_lido_volta_intacta()
    {
        var desconhecida = Guid.NewGuid();
        var f = EtiquetasFormulario.Criar([Vip], [desconhecida]);

        Assert.Equal(new[] { desconhecida }, f.Marcadas);
    }

    [Fact]
    public void Busca_ignora_acentos_e_nao_muda_o_que_esta_marcado()
    {
        var f = EtiquetasFormulario.Criar([Vip, Promocao], [Vip.Id]);

        f.Busca = "promocao";

        Assert.Equal("Promoção", Assert.Single(f.Visiveis).Nome);
        Assert.Equal(new[] { Vip.Id }, f.Marcadas);
    }

    [Fact]
    public void Etiqueta_criada_pelo_atalho_entra_marcada_e_limpa_a_busca()
    {
        var f = EtiquetasFormulario.Criar([Vip], []);
        f.Busca = "xyz";
        var nova = new EtiquetaDto { Id = Guid.NewGuid(), Nome = "Nova", Ativo = true };

        f.Incluir(nova, marcar: true);

        Assert.Equal(string.Empty, f.Busca);
        Assert.Equal(new[] { nova.Id }, f.Marcadas);
        Assert.Equal(2, f.Visiveis.Count);
    }

    [Fact]
    public void Filtro_por_etiqueta_vai_pelo_id()
    {
        var id = Guid.NewGuid();

        Assert.Equal($"?etiquetaId={id}", PessoasApi.Consulta(new FiltroPessoas { EtiquetaId = id }));
    }

    [Fact]
    public void Ficha_da_etiqueta_envia_nome_limpo_e_descricao_vazia_como_nula()
    {
        var ficha = EtiquetaEdicao.De(new EtiquetaDto { Id = Guid.NewGuid(), Nome = "VIP", Ativo = true, QuantidadePessoas = 3 });
        ficha.Nome = "  Cliente VIP ";
        ficha.Descricao = "  ";

        var dto = ficha.ParaDto();

        Assert.Equal("Cliente VIP", dto.Nome);
        Assert.Null(dto.Descricao);
        Assert.Equal("3 cadastros com esta etiqueta.", ficha.UsoTexto);
        Assert.Empty(EtiquetaEdicao.Criar().ParaDto().Nome);
        Assert.NotEmpty(EtiquetaEdicao.Criar().ValidarLocalmente());
    }
}
