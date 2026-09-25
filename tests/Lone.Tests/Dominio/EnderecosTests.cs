using Lone.Domain.Enderecos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

public class EnderecosTests
{
    private static PessoaEndereco Endereco(bool ativo = true, FinalidadeEndereco finalidades = FinalidadeEndereco.Comercial) => new()
    {
        Id = Guid.NewGuid(), Logradouro = "Rua A", Cidade = "Cidade Antiga", Ativo = ativo, Finalidades = finalidades
    };

    [Fact]
    public void Inativo_nunca_e_principal_e_o_principal_nao_passa_sozinho_para_outro_pela_ordem()
    {
        var antigo = Endereco(ativo: false);
        var novo = Endereco();
        var p = new Pessoa { Nome = "Ana" };
        p.Enderecos.AddRange([antigo, novo]);
        var entrega = new PessoaEnderecoFinalidade
        {
            Id = Guid.NewGuid(), PessoaEnderecoId = antigo.Id, FinalidadeId = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Entrega), Principal = true
        };
        p.FinalidadesEnderecos.Add(entrega);

        PessoaNormalizador.Normalizar(p, new DateOnly(2026, 9, 25));

        Assert.False(entrega.Principal);                          // endereço inativo não é principal de nada
        Assert.True(entrega.Ativo);                               // a finalidade continua como histórico
        Assert.Null(RegrasFinalidadeEndereco.EnderecoPrincipal(p, entrega.FinalidadeId)); // ninguém vira principal pela ordem
    }

    [Fact]
    public void Endereco_inativo_antigo_sem_municipio_nao_impede_gravar()
    {
        var p = new Pessoa { Nome = "Ana" };
        p.Enderecos.Add(Endereco(ativo: false));

        Assert.DoesNotContain(PessoaValidador.Validar(p), e => e.StartsWith("Endereço 1", StringComparison.Ordinal));
    }

    [Fact]
    public void Observacoes_tem_limite()
    {
        var p = new Pessoa { Nome = "Ana" };
        var e = Endereco(ativo: false);
        e.Observacoes = new string('x', RegrasEndereco.TamanhoMaximoObservacoes + 1);
        p.Enderecos.Add(e);

        Assert.Contains(PessoaValidador.Validar(p), x => x.Contains("observa", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Tipo_desativado_so_vale_se_ja_era_o_do_endereco()
    {
        var deposito = new TipoEndereco { Id = Guid.NewGuid(), Nome = "Depósito", Ativo = false };
        var cadastro = new Dictionary<Guid, TipoEndereco> { [deposito.Id] = deposito };
        var e = Endereco();
        e.TipoEnderecoId = deposito.Id;

        Assert.Single(RegrasEndereco.ValidarTipos([e], new Dictionary<Guid, Guid?>(), cadastro));
        Assert.Empty(RegrasEndereco.ValidarTipos([e], new Dictionary<Guid, Guid?> { [e.Id] = deposito.Id }, cadastro));

        e.TipoEnderecoId = Guid.NewGuid();
        Assert.Single(RegrasEndereco.ValidarTipos([e], new Dictionary<Guid, Guid?>(), cadastro));
    }

    [Fact]
    public void Nome_do_tipo_e_normalizado_e_obrigatorio()
    {
        var tipo = new TipoEndereco { Nome = "  Centro   de  distribuição " };
        RegrasEndereco.Normalizar(tipo);
        Assert.Equal("Centro de distribuição", tipo.Nome);
        Assert.Empty(RegrasEndereco.Validar(tipo));

        tipo.Nome = "   ";
        RegrasEndereco.Normalizar(tipo);
        Assert.Single(RegrasEndereco.Validar(tipo));
    }
}
