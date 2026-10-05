using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Auditoria;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>
/// P0 na ficha (revisão 2.2): pessoa de contato gravada fica inativa ao remover (a nova sai sem deixar rastro); sócios da
/// Receita casados com os gravados (D7) em vez de trocados; ex-sócios no quadro; frases novas do histórico.
/// </summary>
public class P0ContatosSociosClienteTests
{
    private static readonly Guid IdMaria = Guid.NewGuid();

    private static PessoaFormulario Gravada(IEnumerable<ContatoDto>? contatos = null, IEnumerable<SocioDto>? socios = null) =>
        PessoaFormulario.De(new PessoaDto
        {
            Id = Guid.NewGuid(), Natureza = NaturezaPessoa.Juridica, Nome = "EMPRESA A LTDA",
            Estabelecimentos = [new EstabelecimentoDto { Id = Guid.NewGuid(), Principal = true, Cnpj = "11222333000181" }],
            Contatos = contatos?.ToList() ?? [],
            Socios = socios?.ToList() ?? []
        });

    // ---- Pessoas de contato ----

    [Fact]
    public void Contato_gravado_removido_fica_inativo_no_mesmo_registro_e_deixa_de_ser_principal()
    {
        var f = Gravada([new ContatoDto { Id = IdMaria, Nome = "Maria", Principal = true }]);
        var maria = f.Contatos.Single();

        maria.RemoverCommand.Execute(null);

        Assert.Contains(maria, f.Contatos);
        Assert.False(maria.Ativo);
        Assert.True(f.TemContatosInativos);
        var dto = Assert.Single(f.ParaDto().Contatos);
        Assert.Equal((IdMaria, false, false), (dto.Id, dto.Ativo, dto.Principal));
    }

    [Fact]
    public void Contato_novo_removido_antes_de_salvar_some_e_nao_vai_para_a_gravacao()
    {
        var f = Gravada();
        var novo = new ContatoFormulario { Nome = "Temporário" };
        f.AdicionarContato(novo);

        novo.RemoverCommand.Execute(null);

        Assert.Empty(f.Contatos);
        Assert.Empty(f.ParaDto().Contatos); // nada vai ao banco: nem inclusão, nem inativação
        Assert.False(f.TemContatosInativos);
    }

    [Fact]
    public void Reativar_contato_volta_o_mesmo_registro()
    {
        var f = Gravada([new ContatoDto { Id = IdMaria, Nome = "Maria", Ativo = false }]);
        var maria = f.Contatos.Single();
        Assert.False(maria.Visivel);           // inativo só aparece em "Mostrar inativos"

        f.MostrarContatosInativos = true;
        Assert.True(maria.Visivel);
        maria.ReativarCommand.Execute(null);

        var dto = Assert.Single(f.ParaDto().Contatos);
        Assert.Equal((IdMaria, true), (dto.Id, dto.Ativo));
        Assert.False(f.TemContatosInativos);
    }

    // ---- Sócios (D7) ----

    private static SocioDto Socio(Guid id, string nome, string? documento = null, bool ativo = true, DateOnly? saiu = null) =>
        new() { Id = id, Nome = nome, Qualificacao = "Sócio-Administrador", Documento = documento, Ativo = ativo, SaiuEm = saiu };

    private static SocioDto DaReceita(string nome, string? documento = null) =>
        new() { Nome = nome, Qualificacao = "Sócio-Administrador", Documento = documento };

    [Fact]
    public void Socio_que_continua_fica_com_o_mesmo_Id_o_novo_entra_e_quem_saiu_fica_inativo_com_a_data()
    {
        var joao = Guid.NewGuid();
        var pedro = Guid.NewGuid();
        var f = Gravada(socios: [Socio(joao, "João Da Silva", "***111111**"), Socio(pedro, "Pedro Alves", "***222222**")]);

        f.CasarSocios([DaReceita("João da Silva", "***111111**"), DaReceita("Ana Lima", "***333333**")]);

        var porNome = f.Socios.ToDictionary(s => s.Nome);
        Assert.Equal(joao, porNome["João da Silva"].Id);
        Assert.True(porNome["João da Silva"].Ativo);
        Assert.Equal(Guid.Empty, porNome["Ana Lima"].Id);                  // novo: Id gerado na gravação
        Assert.Equal(pedro, porNome["Pedro Alves"].Id);
        Assert.False(porNome["Pedro Alves"].Ativo);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), porNome["Pedro Alves"].SaiuEm);
        Assert.Equal(3, f.Socios.Count);                                    // ninguém some da ficha
        Assert.Empty(f.AvisoSocios);
    }

    [Fact]
    public void Ex_socio_que_volta_e_reativado_no_mesmo_registro_sem_a_data_de_saida_antiga()
    {
        var joao = Guid.NewGuid();
        var f = Gravada(socios: [Socio(joao, "João", "***111111**", ativo: false, saiu: new DateOnly(2026, 1, 10))]);

        f.CasarSocios([DaReceita("João", "***111111**")]);

        var s = Assert.Single(f.Socios);
        Assert.Equal((joao, true, (DateOnly?)null), (s.Id, s.Ativo, s.SaiuEm));
    }

    [Fact]
    public void Ambiguidade_nao_casa_inclui_como_novo_preserva_os_gravados_e_avisa()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var f = Gravada(socios: [Socio(a, "Maria Souza"), Socio(b, "MARIA SOUZA")]);

        f.CasarSocios([DaReceita("Maria Souza")]);

        Assert.Equal(3, f.Socios.Count);
        Assert.All(f.Socios.Where(s => s.Id == a || s.Id == b), s => Assert.True(s.Ativo)); // ficam como estão
        Assert.Single(f.Socios, s => s.Id == Guid.Empty);
        Assert.Contains("1 sócio(s) da Receita não puderam ser identificados com segurança", f.AvisoSocios);
    }

    [Fact]
    public void Quadro_vazio_na_consulta_nao_tira_ninguem()
    {
        var joao = Guid.NewGuid();
        var f = Gravada(socios: [Socio(joao, "João")]);

        f.AplicarCnpj(f.Principal, new DadosCnpj { Cnpj = "11222333000181", RazaoSocial = "EMPRESA A LTDA", Fonte = "BrasilAPI" });

        var s = Assert.Single(f.Socios);
        Assert.True(s.Ativo);
    }

    [Fact]
    public void Consulta_em_cadastro_gravado_casa_os_socios_em_vez_de_trocar()
    {
        var joao = Guid.NewGuid();
        var f = Gravada(socios: [Socio(joao, "João", "***111111**")]);

        f.AplicarCnpj(f.Principal, new DadosCnpj
        {
            Cnpj = "11222333000181", RazaoSocial = "EMPRESA A LTDA", Fonte = "BrasilAPI", Socios = [DaReceita("João", "***111111**")]
        });

        Assert.Equal(joao, Assert.Single(f.Socios).Id);
    }

    [Fact]
    public void Quadro_mostra_os_ativos_e_os_ex_socios_ficam_em_ver_ex_socios()
    {
        var f = Gravada(socios: [Socio(Guid.NewGuid(), "João"), Socio(Guid.NewGuid(), "Pedro", ativo: false, saiu: new DateOnly(2026, 2, 1))]);
        var q = f.QuadroSocios;

        Assert.Equal(["João"], q.Visiveis.Select(s => s.Nome).ToArray());
        Assert.True(q.TemExSocios);
        Assert.Equal("Ver ex-sócios (1) ▾", q.TextoExSocios);
        Assert.Empty(q.ExSocios);

        q.AlternarExSociosCommand.Execute(null);

        Assert.Equal(["Pedro"], q.ExSocios.Select(s => s.Nome).ToArray());
        Assert.Equal("Ocultar ex-sócios ▴", q.TextoExSocios);
    }

    // ---- Histórico (D6 e D3-a) ----

    private static string Frase(AcaoAuditoria acao, string entidade, string? campo = null, string? antes = null, string? depois = null,
                                string? descricao = null) =>
        HistoricoItem.De(new RegistroHistorico
        {
            DataHora = DateTime.UtcNow, Usuario = "teste", Acao = acao, Entidade = entidade, Campo = campo,
            ValorAnterior = antes, ValorNovo = depois, Descricao = descricao
        }).Descricao;

    [Fact]
    public void Historico_mostra_inativacao_reativacao_e_a_foto_e_o_registro_antigo_continua()
    {
        Assert.Equal("Pessoa de contato: inativado — Maria Souza (Compradora)",
            Frase(AcaoAuditoria.Inativacao, "Contato", "Ativo", "Sim", "Não", "Maria Souza (Compradora)"));
        Assert.Equal("Telefone/e-mail: reativado", Frase(AcaoAuditoria.Reativacao, "MeioContato", "Ativo", "Não", "Sim"));
        Assert.Equal("Pessoa de contato: incluído", Frase(AcaoAuditoria.Inclusao, "Contato"));
        Assert.Equal("Pessoa de contato: incluído · Nome = \"Maria\"", Frase(AcaoAuditoria.Inclusao, "Contato", "Nome", depois: "Maria"));
        Assert.Equal("Sócio: removido · Nome era \"João\"", Frase(AcaoAuditoria.Exclusao, "PessoaSocio", "Nome", antes: "João"));
        Assert.Equal("Cadastro criado", Frase(AcaoAuditoria.Inclusao, "Pessoa"));
        // Registro gravado antes do P0: a inativação era uma alteração comum e continua aparecendo assim.
        Assert.Equal("Telefone/e-mail · Ativo: \"Sim\" → \"Não\"", Frase(AcaoAuditoria.Alteracao, "MeioContato", "Ativo", "Sim", "Não"));
    }
}
