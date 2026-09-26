using Lone.Application.Pessoas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enderecos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// Pedido no formato antigo (só os bits de PessoaEnderecos.Finalidades, sem Usos): o legado nunca destrói informação do
/// modelo novo (PessoaEnderecoFinalidades é a fonte de verdade).
/// </summary>
public class CompatibilidadeFinalidadesLegadoTests
{
    private static readonly Guid Entrega = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Entrega);
    private static readonly Guid Fiscal = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Fiscal);
    private static readonly Guid Cobranca = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Cobranca);
    private static readonly Guid Personalizada = Guid.NewGuid(); // criada no cadastro novo (sem bit legado)

    private static (PessoaDto Dto, Pessoa Gravada, Guid EnderecoId) Cenario(FinalidadeEndereco bits)
    {
        var enderecoId = Guid.NewGuid();
        var pessoaId = Guid.NewGuid();
        var gravada = new Pessoa
        {
            Id = pessoaId,
            Enderecos = [new PessoaEndereco { Id = enderecoId, PessoaId = pessoaId, Logradouro = "Rua A", Cidade = "X" }],
            FinalidadesEnderecos =
            [
                new PessoaEnderecoFinalidade { Id = Guid.NewGuid(), PessoaId = pessoaId, PessoaEnderecoId = enderecoId, FinalidadeId = Fiscal, Principal = true },
                new PessoaEnderecoFinalidade { Id = Guid.NewGuid(), PessoaId = pessoaId, PessoaEnderecoId = enderecoId, FinalidadeId = Cobranca, Ativo = false },
                new PessoaEnderecoFinalidade { Id = Guid.NewGuid(), PessoaId = pessoaId, PessoaEnderecoId = enderecoId, FinalidadeId = Personalizada, Principal = true }
            ]
        };
        var dto = new PessoaDto
        {
            Id = pessoaId, Nome = "Empresa", Natureza = NaturezaPessoa.Juridica,
            Enderecos = [new EnderecoDto { Id = enderecoId, Logradouro = "Rua A", Cidade = "X", Finalidades = bits }] // sem Usos
        };
        return (dto, gravada, enderecoId);
    }

    private static Pessoa Traduzir(PessoaDto dto, Pessoa gravada)
    {
        var dados = PessoaMapeamento.ParaEntidade(dto);
        CompatibilidadeFinalidadesLegado.Aplicar(dados, dto.Enderecos, gravada);
        return dados;
    }

    [Fact]
    public void Pedido_antigo_nao_apaga_o_principal_do_modelo_novo()
    {
        var (dto, gravada, _) = Cenario(FinalidadeEndereco.Principal | FinalidadeEndereco.Fiscal);

        var dados = Traduzir(dto, gravada);

        var fiscal = dados.FinalidadesEnderecos.Single(u => u.FinalidadeId == Fiscal);
        Assert.True(fiscal.Ativo);
        Assert.True(fiscal.Principal);                                            // preservado
        Assert.True(dados.FinalidadesEnderecos.Single(u => u.FinalidadeId == Personalizada).Principal); // sem bit: intacta
        Assert.False(dados.FinalidadesEnderecos.Single(u => u.FinalidadeId == Cobranca).Ativo);        // bit desligado: segue retirada
        Assert.Empty(RegrasFinalidadeEndereco.ValidarCompleto(dados, gravada));   // nada gravado ficou de fora
    }

    [Fact]
    public void Bit_ligado_sem_relacao_cria_finalidade_sem_principal_e_bit_desligado_e_retirada()
    {
        var (dto, gravada, _) = Cenario(FinalidadeEndereco.Entrega); // tirou Fiscal no aplicativo antigo, pôs Entrega

        var dados = Traduzir(dto, gravada);

        var entrega = dados.FinalidadesEnderecos.Single(u => u.FinalidadeId == Entrega);
        Assert.True(entrega.Ativo);
        Assert.False(entrega.Principal);                                          // os bits não dizem de qual seria
        var fiscal = dados.FinalidadesEnderecos.Single(u => u.FinalidadeId == Fiscal);
        Assert.False(fiscal.Ativo);                                               // retirada pelo usuário (histórico)
        Assert.False(fiscal.Principal);                                           // relação inativa não é principal
    }

    [Fact]
    public void Bit_de_finalidade_retirada_reativa_a_mesma_linha_sem_principal()
    {
        var (dto, gravada, _) = Cenario(FinalidadeEndereco.Fiscal | FinalidadeEndereco.Cobranca);

        var dados = Traduzir(dto, gravada);

        var cobranca = dados.FinalidadesEnderecos.Single(u => u.FinalidadeId == Cobranca);
        Assert.Equal(gravada.FinalidadesEnderecos.Single(u => u.FinalidadeId == Cobranca).Id, cobranca.Id);
        Assert.True(cobranca.Ativo);
        Assert.False(cobranca.Principal);
    }

    [Fact]
    public void Formato_novo_ignora_os_bits_a_relacao_e_a_fonte_de_verdade()
    {
        var (dto, gravada, enderecoId) = Cenario(FinalidadeEndereco.Principal | FinalidadeEndereco.Entrega | FinalidadeEndereco.Cobranca);
        dto.Enderecos[0].Usos = gravada.FinalidadesEnderecos
            .Select(u => new FinalidadeDoEnderecoDto { Id = u.Id, FinalidadeId = u.FinalidadeId, Principal = u.Principal, Ativo = u.Ativo })
            .ToList();

        var dados = Traduzir(dto, gravada);

        Assert.Equal(3, dados.FinalidadesEnderecos.Count);                        // exatamente os Usos; os bits não criam nada
        Assert.DoesNotContain(dados.FinalidadesEnderecos, u => u.FinalidadeId == Entrega);
        Assert.All(dados.FinalidadesEnderecos, u => Assert.Equal(enderecoId, u.PessoaEnderecoId));
    }

    [Fact]
    public void Endereco_novo_no_formato_antigo_ganha_as_finalidades_sem_principal()
    {
        var dto = new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica,
            Enderecos = [new EnderecoDto { Id = Guid.NewGuid(), Logradouro = "Rua B", Cidade = "Y", Finalidades = FinalidadeEndereco.Principal | FinalidadeEndereco.Entrega | FinalidadeEndereco.Fiscal }]
        };

        var dados = PessoaMapeamento.ParaEntidade(dto);
        CompatibilidadeFinalidadesLegado.Aplicar(dados, dto.Enderecos, anterior: null);

        Assert.Equal(new[] { Fiscal, Entrega }.OrderBy(x => x), dados.FinalidadesEnderecos.Select(u => u.FinalidadeId).OrderBy(x => x));
        Assert.All(dados.FinalidadesEnderecos, u => Assert.False(u.Principal)); // o bit "Principal" antigo não vira principal
    }
}
