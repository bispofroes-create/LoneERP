using Lone.Domain.Auditoria;
using Lone.Domain.Entidades;
using Lone.Domain.Etiquetas;

namespace Lone.Tests.Dominio;

public class EtiquetasTests
{
    private static Etiqueta Nova(string nome, bool ativa = true) => new() { Id = Guid.NewGuid(), Nome = nome, Ativo = ativa };

    [Fact]
    public void Nome_e_descricao_sao_limpos()
    {
        var etiqueta = new Etiqueta { Nome = "  Cliente   VIP ", Descricao = "   " };

        RegrasEtiqueta.Normalizar(etiqueta);

        Assert.Equal("Cliente VIP", etiqueta.Nome);
        Assert.Null(etiqueta.Descricao);
    }

    [Fact]
    public void Nome_e_obrigatorio_e_tem_limite()
    {
        Assert.Contains("Informe o nome da etiqueta.", RegrasEtiqueta.Validar(new Etiqueta()));
        Assert.Single(RegrasEtiqueta.Validar(new Etiqueta { Nome = new string('x', Etiqueta.TamanhoMaximoNome + 1) }));
        Assert.Empty(RegrasEtiqueta.Validar(new Etiqueta { Nome = "VIP" }));
    }

    [Fact]
    public void Desativar_e_reativar_registram_o_evento_uma_vez()
    {
        var etiqueta = Nova("VIP");

        etiqueta.Desativar();
        etiqueta.Desativar(); // já desativada: nada muda

        Assert.False(etiqueta.Ativo);
        Assert.Equal(new[] { "Etiqueta 'VIP' desativada." }, etiqueta.RetirarEventos());

        etiqueta.Reativar();
        Assert.True(etiqueta.Ativo);
        Assert.Equal(new[] { "Etiqueta 'VIP' reativada." }, etiqueta.RetirarEventos());
    }

    [Fact]
    public void Etiqueta_desativada_so_continua_em_quem_ja_tinha()
    {
        var ativa = Nova("VIP");
        var desativada = Nova("Antiga", ativa: false);
        var cadastro = new Dictionary<Guid, Etiqueta> { [ativa.Id] = ativa, [desativada.Id] = desativada };
        var marcadas = new[] { Marcada(ativa.Id), Marcada(desativada.Id) };

        var semTer = RegrasEtiqueta.ValidarMarcadas(marcadas, new HashSet<Guid>(), cadastro);
        var jaTinha = RegrasEtiqueta.ValidarMarcadas(marcadas, new HashSet<Guid> { desativada.Id }, cadastro);

        Assert.Contains(semTer, e => e.Contains("\"Antiga\" está desativada"));
        Assert.Empty(jaTinha);
    }

    [Fact]
    public void Etiqueta_que_nao_existe_no_cadastro_e_recusada()
    {
        var erros = RegrasEtiqueta.ValidarMarcadas([Marcada(Guid.NewGuid())], new HashSet<Guid>(), new Dictionary<Guid, Etiqueta>());

        Assert.Contains(erros, e => e.Contains("não existe mais"));
    }

    [Fact]
    public void Na_auditoria_a_etiqueta_da_pessoa_e_um_valor_so()
    {
        var id = Guid.NewGuid();
        IValorAuditavel valor = new PessoaEtiqueta { EtiquetaId = id };

        Assert.Equal(nameof(PessoaEtiqueta.EtiquetaId), valor.CampoAuditado);
        Assert.Equal(id.ToString(), valor.DescreverValor(_ => id));
        Assert.Null(valor.DescreverValor(_ => Guid.Empty));
    }

    private static PessoaEtiqueta Marcada(Guid etiquetaId) => new() { Id = Guid.NewGuid(), EtiquetaId = etiquetaId };
}
