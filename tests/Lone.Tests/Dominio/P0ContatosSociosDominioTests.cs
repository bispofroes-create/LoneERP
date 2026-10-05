using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

/// <summary>
/// P0 da auditoria do cadastro (revisão 2.2): casamento de sócios da Receita (D7), contato inativo fora da escolha do
/// principal e da validação, e a data de saída do sócio como estado atual.
/// </summary>
public class P0ContatosSociosDominioTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 5);
    private static readonly DateTime Agora = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private static DadosSocio S(string? documento, string? nome, string? qualificacao = "Sócio-Administrador") =>
        new(documento, nome, qualificacao);

    // ---- Casamento (D7) ----

    [Fact]
    public void Mesmo_documento_casa_mesmo_com_o_nome_escrito_diferente()
    {
        var r = CasamentoSocios.Casar([S("***123456**", "JOÃO DA SILVA")], [S("***123456**", "João Da Silva")]);

        Assert.Equal(0, r.GravadoDoRecebido[0]);
        Assert.Empty(r.Ambiguos);
    }

    [Fact]
    public void Documentos_diferentes_nunca_casam_mesmo_com_o_nome_igual()
    {
        var r = CasamentoSocios.Casar([S("***111111**", "João da Silva")], [S("***222222**", "João da Silva")]);

        Assert.Null(r.GravadoDoRecebido[0]);
        Assert.Empty(r.Ambiguos);
        Assert.Equal([0], r.GravadosQueSairam(1));
    }

    [Fact]
    public void Sem_documento_casa_pelo_nome_normalizado_e_pela_qualificacao()
    {
        var r = CasamentoSocios.Casar([S(null, "JOÃO  DA SILVA", "sócio")], [S(null, "Joao da Silva", "Sócio")]);
        Assert.Equal(0, r.GravadoDoRecebido[0]);

        var outraQualificacao = CasamentoSocios.Casar([S(null, "João da Silva", "Sócio")], [S(null, "João da Silva", "Administrador")]);
        Assert.Null(outraQualificacao.GravadoDoRecebido[0]);
    }

    [Fact]
    public void Dois_gravados_compativeis_com_um_recebido_nao_casa_e_preserva_os_dois()
    {
        var r = CasamentoSocios.Casar([S(null, "Maria Souza"), S(null, "MARIA SOUZA")], [S(null, "Maria Souza")]);

        Assert.Null(r.GravadoDoRecebido[0]);
        Assert.Equal([0], r.Ambiguos.Order());
        Assert.Equal([0, 1], r.GravadosPreservados.Order());
        Assert.Empty(r.GravadosQueSairam(2)); // preservados: não saem
    }

    [Fact]
    public void Um_gravado_disputado_por_dois_recebidos_nao_casa_com_nenhum()
    {
        var r = CasamentoSocios.Casar([S(null, "Maria Souza")], [S(null, "Maria Souza"), S(null, "maria souza")]);

        Assert.Null(r.GravadoDoRecebido[0]);
        Assert.Null(r.GravadoDoRecebido[1]);
        Assert.Equal([0, 1], r.Ambiguos.Order());
        Assert.Equal([0], r.GravadosPreservados.Order());
    }

    [Fact]
    public void Quem_nao_veio_sai_e_quem_e_novo_nao_casa()
    {
        var r = CasamentoSocios.Casar([S("1", "A"), S("2", "B")], [S("1", "A"), S("3", "C")]);

        Assert.Equal(0, r.GravadoDoRecebido[0]);
        Assert.Null(r.GravadoDoRecebido[1]);
        Assert.Equal([1], r.GravadosQueSairam(2));
    }

    // ---- Contato inativo (P0, §2.1) ----

    private static Pessoa Empresa() => new() { Id = Guid.NewGuid(), Nome = "Empresa Teste Ltda", Natureza = NaturezaPessoa.Juridica };

    [Fact]
    public void Contato_inativo_nunca_fica_principal_e_o_principal_e_escolhido_entre_os_ativos()
    {
        var p = Empresa();
        var inativo = new Contato { Id = Guid.NewGuid(), Nome = "Antigo", Principal = true, Ativo = false };
        var ativo = new Contato { Id = Guid.NewGuid(), Nome = "Atual", Principal = true };
        p.Contatos.AddRange([inativo, ativo]);

        PessoaNormalizador.Normalizar(p, Hoje, Agora);

        Assert.False(inativo.Principal);
        Assert.True(ativo.Principal);
    }

    [Fact]
    public void Contato_inativo_nao_e_validado()
    {
        var p = Empresa();
        p.Contatos.Add(new Contato { Id = Guid.NewGuid(), Nome = "", Telefone = "123", Ativo = false });
        PessoaNormalizador.Normalizar(p, Hoje, Agora);

        Assert.DoesNotContain(PessoaValidador.Validar(p), e => e.Contains("Pessoa de contato", StringComparison.Ordinal));

        p.Contatos[0].Ativo = true;
        Assert.Contains(PessoaValidador.Validar(p), e => e.Contains("Pessoa de contato 1", StringComparison.Ordinal));
    }

    // ---- Sócio: data de saída é estado atual (revisão 2.2) ----

    [Fact]
    public void Socio_ativo_nao_guarda_data_de_saida()
    {
        var p = Empresa();
        var voltou = new PessoaSocio { Id = Guid.NewGuid(), Nome = "João", Ativo = true, SaiuEm = new DateOnly(2026, 1, 10) };
        var saiu = new PessoaSocio { Id = Guid.NewGuid(), Nome = "Maria", Ativo = false, SaiuEm = new DateOnly(2026, 2, 1) };
        p.Socios.AddRange([voltou, saiu]);

        PessoaNormalizador.Normalizar(p, Hoje, Agora);

        Assert.Null(voltou.SaiuEm);
        Assert.Equal(new DateOnly(2026, 2, 1), saiu.SaiuEm);
    }

    [Fact]
    public void Resumo_do_contato_e_do_socio_para_o_historico()
    {
        Assert.Equal("Maria Souza (Compradora)", new Contato { Nome = "Maria Souza", Cargo = "Compradora" }.ResumoAuditoria);
        Assert.Equal("Maria Souza", new Contato { Nome = "Maria Souza" }.ResumoAuditoria);
        Assert.Equal("João (Sócio-Administrador)", new PessoaSocio { Nome = "João", Qualificacao = "Sócio-Administrador" }.ResumoAuditoria);
    }
}
