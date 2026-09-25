using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

public class PessoaFormularioTests
{
    private static void TornarJuridica(PessoaFormulario f) =>
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);

    [Fact]
    public void Pessoa_nova_nasce_cliente_com_um_endereco_sem_finalidade_e_um_estabelecimento()
    {
        var f = PessoaFormulario.NovaPessoa();

        Assert.True(f.Nova);
        Assert.True(f.PapelCliente.Ativo);
        Assert.True(Assert.Single(f.Enderecos).SemFinalidades); // finalidade e principal: o usuário escolhe
        Assert.True(Assert.Single(f.Estabelecimentos).EhPrincipal);
    }

    [Fact]
    public void Pessoa_fisica_envia_so_o_estabelecimento_principal_e_o_CPF()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Nome = "Ana";
        f.Documento = "529.982.247-25";
        f.DataNascimento = "15/03/1990";
        f.AdicionarEstabelecimento(); // sobra de quando era PJ

        var dto = f.ParaDto();

        Assert.Equal(NaturezaPessoa.Fisica, dto.Natureza);
        Assert.Equal("529.982.247-25", dto.DocumentoPrincipal);
        Assert.Equal(new DateOnly(1990, 3, 15), dto.DataNascimento);
        Assert.True(Assert.Single(dto.Estabelecimentos).Principal);
        Assert.Single(dto.ContasCliente); // papel cliente ligado: conta padrão vai junto
    }

    [Fact]
    public void Pessoa_juridica_envia_as_filiais_com_o_endereco_fiscal_proprio()
    {
        var f = PessoaFormulario.NovaPessoa();
        TornarJuridica(f);
        var endereco = new EnderecoFormulario { Descricao = "Filial" };
        f.AdicionarEndereco(endereco);
        var filial = f.AdicionarEstabelecimento();
        filial.EnderecoFiscal = endereco;

        var dto = f.ParaDto();

        Assert.Null(dto.DocumentoPrincipal);
        Assert.Equal(2, dto.Estabelecimentos.Count);
        Assert.Equal(endereco.Id, dto.Estabelecimentos[1].EnderecoFiscalId);
        Assert.True(filial.FilialDaPJ);
    }

    [Fact]
    public void Remover_o_endereco_da_filial_volta_ela_ao_endereco_principal()
    {
        var f = PessoaFormulario.NovaPessoa();
        TornarJuridica(f);
        var endereco = new EnderecoFormulario();
        f.AdicionarEndereco(endereco);
        var filial = f.AdicionarEstabelecimento();
        filial.EnderecoFiscal = endereco;

        endereco.RemoverCommand.Execute(null);

        Assert.Null(filial.EnderecoFiscal);
        Assert.DoesNotContain(endereco, f.Enderecos);
    }

    [Fact]
    public async Task Tornar_principal_de_uma_finalidade_pergunta_e_desmarca_o_anterior()
    {
        var f = PessoaFormulario.NovaPessoa(finalidades: Finalidades.Cadastro);
        f.Enderecos[0].Logradouro = "Rua A";
        var segundo = new EnderecoFormulario { Logradouro = "Rua B" };
        f.AdicionarEndereco(segundo);
        var entregaA = f.Enderecos[0].AdicionarFinalidade(Finalidades.Entrega)!;
        var entregaB = segundo.AdicionarFinalidade(Finalidades.Entrega)!;
        await f.AlternarPrincipalAsync(f.Enderecos[0], entregaA);
        string? pergunta = null;
        f.Confirmar = (_, mensagem, _, _) => { pergunta = mensagem; return Task.FromResult(true); };

        await f.AlternarPrincipalAsync(segundo, entregaB);

        Assert.Contains("Já existe um endereço principal para Entrega", pergunta);
        Assert.False(entregaA.Principal);
        Assert.True(entregaB.Principal);
    }

    [Fact]
    public void Tornar_filial_principal_move_ela_para_o_topo()
    {
        var f = PessoaFormulario.NovaPessoa();
        TornarJuridica(f);
        var filial = f.AdicionarEstabelecimento();

        filial.TornarPrincipalCommand.Execute(null);

        Assert.Same(filial, f.Principal);
        Assert.True(filial.EhPrincipal);
        Assert.False(f.Estabelecimentos[1].EhPrincipal);
    }

    [Fact]
    public void Papel_que_nunca_existiu_e_continua_desligado_nao_vai_para_a_API()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.PapelCliente.Ativo = false;

        var dto = f.ParaDto();

        Assert.Empty(dto.Papeis);
        Assert.Empty(dto.ContasCliente);
    }

    [Fact]
    public void Datas_e_numeros_invalidos_sao_apontados_antes_de_enviar()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.DataNascimento = "31/02/1990";
        f.ContaCliente.LimiteCredito = "mil";
        var documento = new DocumentoFormulario { Numero = "123", ValidoAte = "amanhã" };
        f.AdicionarDocumento(documento);

        var erros = f.ValidarLocalmente();

        Assert.Contains("Data de nascimento inválida (use dd/mm/aaaa).", erros);
        Assert.Contains("Cliente: limite de crédito inválido.", erros);
        Assert.Contains(erros, e => e.EndsWith(": validade inválida (use dd/mm/aaaa)."));
        // O endereço principal da pessoa nova ainda não tem município: no Brasil isso também bloqueia (intencional).
        Assert.Contains("Endereço 1: escolha a UF e o município na lista.", erros);
        Assert.Equal(4, erros.Count);
    }

    [Fact]
    public void Cadastro_aberto_volta_com_as_contas_de_outras_empresas_intactas()
    {
        var empresa = Guid.NewGuid();
        var dto = new PessoaDto
        {
            Id = Guid.NewGuid(), Codigo = 12, Nome = "Loja", Natureza = NaturezaPessoa.Fisica,
            Papeis = [new PapelDto { Id = Guid.NewGuid(), Papel = TipoPapel.Cliente, Ativo = true, InicioEm = new DateOnly(2025, 1, 1) }],
            ContasCliente =
            [
                new ContaClienteDto { Id = Guid.NewGuid(), LimiteCredito = 1500.5m },
                new ContaClienteDto { Id = Guid.NewGuid(), EmpresaId = empresa, LimiteCredito = 99m }
            ]
        };

        var f = PessoaFormulario.De(dto);
        var volta = f.ParaDto();

        Assert.Equal("Código 000012", f.CodigoTexto);
        Assert.Equal("1.500,5", f.ContaCliente.LimiteCredito);
        Assert.Equal(new decimal?[] { 1500.5m, 99m }, volta.ContasCliente.Select(c => c.LimiteCredito));
        Assert.Equal(new DateOnly(2025, 1, 1), Assert.Single(volta.Papeis).InicioEm);
    }

    [Fact]
    public void Consulta_de_CNPJ_no_principal_preenche_razao_social_endereco_e_telefone()
    {
        var f = PessoaFormulario.NovaPessoa();
        TornarJuridica(f);
        var dados = new DadosCnpj
        {
            Cnpj = "11222333000181", RazaoSocial = "Minha Empresa Ltda", NomeFantasia = "Minha Empresa",
            SituacaoCadastral = "ATIVA", Cep = "01310100", Logradouro = "Av. Paulista", Numero = "1000",
            Cidade = "São Paulo", Uf = "SP", Telefone = "1133334444", Fonte = "BrasilAPI"
        };

        f.AplicarCnpj(f.Principal, dados);

        Assert.Equal("Minha Empresa Ltda", f.Nome);
        Assert.Equal("11.222.333/0001-81", f.Principal.Cnpj);
        Assert.Equal("Av. Paulista", f.Enderecos[0].Logradouro);
        Assert.Equal("01310-100", f.Enderecos[0].Cep);
        Assert.Equal(TipoContato.Telefone, Assert.Single(f.MeiosContato).Tipo.Valor);

        f.AplicarCnpj(f.Principal, dados); // de novo: não duplica o telefone
        Assert.Single(f.MeiosContato);
    }
}
