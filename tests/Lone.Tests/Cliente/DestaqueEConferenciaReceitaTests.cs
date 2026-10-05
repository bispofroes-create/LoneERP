using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;

namespace Lone.Tests.Cliente;

/// <summary>
/// Consulta de CNPJ: o que ela preenche fica marcado como vindo da Receita (destaque de alterações); num cadastro gravado,
/// valor diferente de um que já existe não é trocado: vai para a conferência "atual × Receita", e só o marcado é trocado.
/// </summary>
public class DestaqueEConferenciaReceitaTests
{
    private static readonly Guid IdPrincipal = Guid.NewGuid();

    private static DadosCnpj Dados() => new()
    {
        Cnpj = "11222333000181", RazaoSocial = "BANCO NOVO S.A.", NomeFantasia = "NOVO", Cep = "35790000", Logradouro = "Rua A", Numero = "100",
        Bairro = "Centro", Cidade = "Curvelo", Uf = "MG", CodigoMunicipioIbge = "3120904", Porte = "DEMAIS", Fonte = "BrasilAPI"
    };

    private static PessoaFormulario Gravada() => PessoaFormulario.De(new PessoaDto
    {
        Id = Guid.NewGuid(), Natureza = NaturezaPessoa.Juridica, Nome = "BANCO ANTIGO S.A.",
        Estabelecimentos = [new EstabelecimentoDto { Id = IdPrincipal, Principal = true, Cnpj = "11222333000181", NomeFantasia = "" }]
    });

    [Fact]
    public void Igual_ignora_maiusculas_espacos_e_pontuacao_de_numero()
    {
        Assert.True(AplicacaoReceita.Igual("Banco  Novo", "BANCO NOVO"));
        Assert.True(AplicacaoReceita.Igual("35.790-000", "35790000"));
        Assert.True(AplicacaoReceita.Igual("1.000,00", "100000"));
        Assert.False(AplicacaoReceita.Igual("Rua A", "Rua B"));
    }

    [Fact]
    public void Cadastro_novo_recebe_tudo_e_guarda_a_origem_Receita()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);

        f.AplicarCnpj(f.Principal, Dados());

        Assert.Equal("BANCO NOVO S.A.", f.Nome);
        Assert.False(f.ConferenciaReceita.Visivel);                     // nada a conferir num cadastro novo
        Assert.True(f.OrigemReceita.ContainsKey(new ChaveCampo(CamposFichaPessoa.Nome)));
        Assert.True(f.OrigemReceita.ContainsKey(new ChaveCampo(CamposFichaPessoa.Cep, f.Enderecos.Single().Id)));
    }

    [Fact]
    public void Gravado_com_valor_diferente_vai_para_a_conferencia_e_so_o_marcado_e_trocado()
    {
        var f = Gravada();
        var aplicados = new List<ChaveCampo>();
        f.ConferenciaReceita.AoAplicar = aplicados.AddRange;

        f.AplicarCnpj(f.Principal, Dados());

        Assert.Equal("BANCO ANTIGO S.A.", f.Nome);                      // não trocou sozinho
        Assert.Equal("NOVO", f.Principal.NomeFantasia);                 // vazio: preenchido direto
        Assert.True(f.ConferenciaReceita.Visivel);
        var item = Assert.Single(f.ConferenciaReceita.Itens);
        Assert.Equal(("Razão social", "BANCO ANTIGO S.A.", "BANCO NOVO S.A."), (item.Rotulo, item.Atual, item.DaReceita));

        item.Aceitar = true;
        f.ConferenciaReceita.AplicarMarcadosCommand.Execute(null);

        Assert.Equal("BANCO NOVO S.A.", f.Nome);
        Assert.False(f.ConferenciaReceita.Visivel);
        Assert.Equal(new ChaveCampo(CamposFichaPessoa.Nome), Assert.Single(aplicados));
    }

    [Fact]
    public void Manter_os_atuais_nao_troca_nada()
    {
        var f = Gravada();
        f.AplicarCnpj(f.Principal, Dados());

        f.ConferenciaReceita.ManterAtuaisCommand.Execute(null);

        Assert.Equal("BANCO ANTIGO S.A.", f.Nome);
        Assert.False(f.ConferenciaReceita.Visivel);
    }

    [Fact]
    public void Comparar_diz_o_campo_e_o_valor_de_antes()
    {
        var f = Gravada();
        var gravado = f.ParaDto();
        f.Principal.NomeFantasia = "AG CURVELO";
        f.Nome = "BANCO ANTIGO S.A."; // igual: não conta

        var alteracoes = AlteracoesDaFicha.Comparar(gravado, f.ParaDto());

        var (chave, antes, agora) = Assert.Single(alteracoes);
        Assert.Equal(new ChaveCampo(CamposFichaPessoa.NomeFantasia), chave); // o do principal fica na Identificação (sem item)
        Assert.Equal(("", "AG CURVELO"), (antes, agora));
    }

    [Fact]
    public void Destaque_so_aparece_ligado_e_diz_a_origem_e_o_antes()
    {
        var destaques = new DestaquesFicha();
        var chave = new ChaveCampo(CamposFichaPessoa.Nome);
        var mudou = 0;
        destaques.Mudou += () => mudou++;

        destaques.Definir([new DestaqueCampo(chave, "Receita", "")]);
        Assert.Null(destaques.De(CamposFichaPessoa.Nome, null));        // desligado: nada na tela
        Assert.Equal("Destacar alterações (1)", destaques.TextoBotao);

        destaques.AlternarCommand.Execute(null);

        Assert.Equal("● Veio da Receita · antes: (vazio)", destaques.De(CamposFichaPessoa.Nome, null)!.Texto);
        Assert.Equal("Ocultar destaque (1)", destaques.TextoBotao);
        Assert.Equal(2, mudou);
        Assert.Equal("● Alterado · antes: Rua A", new DestaqueCampo(chave, null, "Rua A").Texto);
    }

    // ---- A1: telefone da Receita com zero de operadora não duplica o que já existe ----

    [Theory]
    [InlineData("01170844621")]      // como a Receita manda: zero antes do DDD
    [InlineData("(11) 7084-4621")]
    [InlineData("+55 11 7084-4621")]
    [InlineData("5511 7084-4621")]
    public void Telefone_da_Receita_igual_ao_gravado_nao_e_acrescentado_de_novo(string daReceita)
    {
        var f = PessoaFormulario.De(new PessoaDto
        {
            Id = Guid.NewGuid(), Natureza = NaturezaPessoa.Juridica, Nome = "BANCO ANTIGO S.A.",
            Estabelecimentos = [new EstabelecimentoDto { Id = IdPrincipal, Principal = true, Cnpj = "11222333000181" }],
            MeiosContato = [new MeioContatoDto { Id = Guid.NewGuid(), Tipo = TipoContato.Telefone, Valor = "1170844621" }] // gravado: só dígitos
        });
        var dados = Dados();
        dados.Telefone = daReceita;

        f.AplicarCnpj(f.Principal, dados);

        Assert.Single(f.MeiosContato, m => m.Ativo && m.Valor.Length > 0);
    }

    [Fact]
    public void Telefone_da_Receita_diferente_e_acrescentado_com_o_valor_recebido()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        var dados = Dados();
        dados.Telefone = "01133334444";

        f.AplicarCnpj(f.Principal, dados);

        var meio = Assert.Single(f.MeiosContato, m => m.Valor.Length > 0);
        Assert.Equal("01133334444", meio.Valor); // o valor recebido não é alterado; a gravação normaliza como sempre
    }

    [Fact]
    public void Chave_de_telefone_ignora_55_e_zero_de_operadora()
    {
        Assert.Equal(PessoaFormulario.ChaveTelefone("(11) 7084-4621"), PessoaFormulario.ChaveTelefone("01170844621"));
        Assert.Equal(PessoaFormulario.ChaveTelefone("(11) 7084-4621"), PessoaFormulario.ChaveTelefone("+55 (11) 7084-4621"));
        Assert.NotEqual(PessoaFormulario.ChaveTelefone("(11) 7084-4621"), PessoaFormulario.ChaveTelefone("(31) 7084-4621"));
        Assert.Equal("0800123456", PessoaFormulario.ChaveTelefone("0800 123456")); // inválido: fica só nos dígitos
    }

    // ---- A2: a aba diz quantos campos destacados tem ----

    [Fact]
    public async Task Aba_mostra_quantos_campos_destacados_tem_so_com_o_destaque_ligado()
    {
        var ambiente = new AmbienteCliente();
        await ambiente.Sessao.DefinirAsync(AmbienteCliente.NovaSessao());
        var (tela, _) = await PessoasViewModelTests.AbrirTelaComAsync(ambiente);
        var avisos = new List<string?>();
        tela.PropertyChanged += (_, e) => avisos.Add(e.PropertyName);
        var endereco = Guid.NewGuid();

        tela.Destaques.Definir(
        [
            new DestaqueCampo(new ChaveCampo(CamposFichaPessoa.Nome), "Receita", ""),
            new DestaqueCampo(new ChaveCampo(CamposFichaPessoa.Cep, endereco), "Receita", ""),
            new DestaqueCampo(new ChaveCampo(CamposFichaPessoa.Logradouro, endereco), null, "Rua A")
        ]);
        Assert.Empty(tela.DestaquesPorAba); // desligado: nenhuma marca nas abas

        tela.Destaques.Ligado = true;

        Assert.Equal(1, tela.DestaquesPorAba[SecaoPessoa.Geral]);
        Assert.Equal(2, tela.DestaquesPorAba[SecaoPessoa.Enderecos]);
        Assert.Contains(nameof(PessoasViewModel.DestaquesPorAba), avisos);
    }
}
