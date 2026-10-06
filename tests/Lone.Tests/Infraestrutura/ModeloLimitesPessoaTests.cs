using Lone.Domain.Entidades;
using Lone.Domain.Pessoas;
using Lone.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Bloco G (P1-1): os limites de tamanho conferidos antes do banco (<see cref="LimitesCadastroPessoa"/>) são exatamente os
/// do modelo do EF, e toda coluna de texto com tamanho das entidades gravadas pela ficha tem limite conferido em algum
/// lugar. Coluna nova sem limite (ou número que mudou de um lado só) falha aqui. Não abre conexão.
/// </summary>
public class ModeloLimitesPessoaTests
{
    private static readonly IModel Modelo = CriarModelo();

    private static IModel CriarModelo()
    {
        using var db = new LoneDbContext(new DbContextOptionsBuilder<LoneDbContext>()
            .UseSqlServer("Server=(sem-conexao);Database=Lone;Trusted_Connection=True").Options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    /// <summary>As entidades que a ficha de Pessoas grava (PUT do cadastro).</summary>
    private static readonly Type[] DaFicha =
    [
        typeof(Pessoa), typeof(Estabelecimento), typeof(PessoaEndereco), typeof(MeioContato), typeof(Contato),
        typeof(PessoaDocumento), typeof(PessoaSocio), typeof(PessoaPapel), typeof(ContaCliente), typeof(ContaFornecedor)
    ];

    /// <summary>Textos com tamanho conferidos por outra regra (formato, derivados ou limites que já existiam), e por quê.</summary>
    private static readonly Dictionary<(Type, string), string> ConferidosEmOutraRegra = new()
    {
        [(typeof(Pessoa), nameof(Pessoa.Nome))] = "PessoaValidador.ValidarIdentificacao (150)",
        [(typeof(Pessoa), nameof(Pessoa.DocumentoPrincipal))] = "CPF/raiz normalizados; estrangeiro limitado a 20 no validador",
        [(typeof(Pessoa), nameof(Pessoa.Nacionalidade))] = "ValidarDadosComplementares",
        [(typeof(Pessoa), nameof(Pessoa.NomeMae))] = "ValidarDadosComplementares",
        [(typeof(Pessoa), nameof(Pessoa.NomePai))] = "ValidarDadosComplementares",
        [(typeof(Pessoa), nameof(Pessoa.Porte))] = "ValidarDadosComplementares",
        [(typeof(Pessoa), nameof(Pessoa.OrigemCadastro))] = "ValidarDadosComplementares",
        [(typeof(Pessoa), nameof(Pessoa.SituacaoMotivo))] = "ValidarDadosComplementares (só pelas ações de situação)",
        [(typeof(Pessoa), Lone.Infrastructure.Persistencia.Configuracoes.PessoaConfiguration.ColunaProfissaoAntiga)] = "coluna antiga, só cópia (não é gravada pela ficha)",
        [(typeof(Estabelecimento), nameof(Estabelecimento.Cnpj))] = "formato (14) no validador",
        [(typeof(Estabelecimento), nameof(Estabelecimento.InscricaoEstadual))] = "ValidarFiscal (14)",
        [(typeof(Estabelecimento), nameof(Estabelecimento.InscricaoSuframa))] = "ValidarFiscal (8 ou 9)",
        [(typeof(Estabelecimento), nameof(Estabelecimento.CnaePrincipal))] = "ValidarFiscal (7)",
        [(typeof(Estabelecimento), nameof(Estabelecimento.CnaesSecundarios))] = "ValidarDadosComplementares (1000)",
        [(typeof(PessoaEndereco), nameof(PessoaEndereco.Cep))] = "CEP (8 dígitos) no Brasil; código postal (8) no exterior",
        [(typeof(PessoaEndereco), nameof(PessoaEndereco.Uf))] = "do IBGE (Brasil) ou EX (exterior)",
        [(typeof(PessoaEndereco), nameof(PessoaEndereco.CodigoMunicipioIbge))] = "copiado do IBGE",
        [(typeof(PessoaEndereco), nameof(PessoaEndereco.Observacoes))] = "ValidarEndereco (250)",
        [(typeof(MeioContato), nameof(MeioContato.Ramal))] = "ValidarMeio (10)",
        [(typeof(MeioContato), Lone.Infrastructure.Persistencia.Configuracoes.MeioContatoConfiguration.ColunaDdd)] = "calculada pelo banco",
        [(typeof(PessoaDocumento), nameof(PessoaDocumento.Uf))] = "UF válida no validador",
        [(typeof(PessoaDocumento), nameof(PessoaDocumento.Observacoes))] = "ValidarDocumento (250)",
        [(typeof(PessoaSocio), nameof(PessoaSocio.Nome))] = "ValidarDadosComplementares",
        [(typeof(PessoaSocio), nameof(PessoaSocio.Qualificacao))] = "ValidarDadosComplementares",
        [(typeof(PessoaSocio), nameof(PessoaSocio.Documento))] = "ValidarDadosComplementares"
    };

    [Fact]
    public void Cada_limite_e_igual_ao_tamanho_da_coluna()
    {
        foreach (var limite in LimitesCadastroPessoa.Todos)
        {
            var propriedade = Modelo.FindEntityType(limite.Entidade)?.FindProperty(limite.Propriedade);
            Assert.True(propriedade is not null, $"{limite.Entidade.Name}.{limite.Propriedade} não existe no modelo");
            Assert.True(propriedade!.GetMaxLength() == limite.Maximo,
                $"{limite.Entidade.Name}.{limite.Propriedade}: limite {limite.Maximo} × coluna {propriedade.GetMaxLength()}");
        }
    }

    [Fact]
    public void Toda_coluna_de_texto_com_tamanho_da_ficha_tem_limite_conferido()
    {
        var comLimite = LimitesCadastroPessoa.Todos.Select(l => (l.Entidade, l.Propriedade)).ToHashSet();
        var semLimite = new List<string>();

        foreach (var tipo in DaFicha)
            foreach (var propriedade in Modelo.FindEntityType(tipo)!.GetProperties())
            {
                if (propriedade.ClrType != typeof(string) || propriedade.GetMaxLength() is null) continue;
                var chave = (tipo, propriedade.Name);
                if (!comLimite.Contains(chave) && !ConferidosEmOutraRegra.ContainsKey(chave))
                    semLimite.Add($"{tipo.Name}.{propriedade.Name} ({propriedade.GetMaxLength()})");
            }

        Assert.True(semLimite.Count == 0, "Sem limite antes do banco: " + string.Join(", ", semLimite));
    }

    [Fact]
    public void Nenhum_limite_repete_o_mesmo_texto()
    {
        var chaves = LimitesCadastroPessoa.Todos.Select(l => (l.Entidade, l.Propriedade)).ToList();
        Assert.Equal(chaves.Count, chaves.Distinct().Count());
        Assert.DoesNotContain(chaves, c => ConferidosEmOutraRegra.ContainsKey(c));
    }
}
