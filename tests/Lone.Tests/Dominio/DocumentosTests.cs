using Lone.Domain.Documentos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

public class DocumentosTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 25);

    private static readonly TipoDocumentoCadastro Cnh = new()
    {
        Id = TiposDocumentoSistema.Id(TipoDocumento.Cnh), Nome = "CNH", TipoSistema = TipoDocumento.Cnh, ExigeValidade = true
    };

    private static readonly TipoDocumentoCadastro Alvara = new() { Id = Guid.NewGuid(), Nome = "Alvará", DiasAvisoVencimento = 60 };

    private static Dictionary<Guid, TipoDocumentoCadastro> Cadastro(params TipoDocumentoCadastro[] tipos) => tipos.ToDictionary(t => t.Id);

    [Theory]
    [InlineData(null, SituacaoValidade.SemValidade)]
    [InlineData(-1, SituacaoValidade.Vencido)]
    [InlineData(0, SituacaoValidade.VenceEmBreve)]
    [InlineData(30, SituacaoValidade.VenceEmBreve)]
    [InlineData(31, SituacaoValidade.Valido)]
    public void Situacao_pela_validade_e_antecedencia(int? diasAteVencer, SituacaoValidade esperada)
    {
        DateOnly? validade = diasAteVencer is { } d ? Hoje.AddDays(d) : null;
        Assert.Equal(esperada, RegrasDocumento.Situacao(validade, 30, Hoje));
    }

    [Fact]
    public void Texto_da_situacao()
    {
        Assert.Equal("Vencido há 3 dias", RegrasDocumento.TextoSituacao(Hoje.AddDays(-3), 30, Hoje));
        Assert.Equal("Vencido ontem", RegrasDocumento.TextoSituacao(Hoje.AddDays(-1), 30, Hoje));
        Assert.Equal("Vence hoje", RegrasDocumento.TextoSituacao(Hoje, 30, Hoje));
        Assert.Equal("Vence em 12 dias", RegrasDocumento.TextoSituacao(Hoje.AddDays(12), 30, Hoje));
        Assert.Equal(string.Empty, RegrasDocumento.TextoSituacao(Hoje.AddDays(90), 30, Hoje));
    }

    [Fact]
    public void Chamada_antiga_so_com_o_enum_e_ligada_ao_tipo_de_sistema()
    {
        var p = new Pessoa { Nome = "Ana" };
        p.Documentos.Add(new PessoaDocumento { Id = Guid.NewGuid(), Tipo = TipoDocumento.Passaporte, Numero = "X1" });

        RegrasDocumento.CompletarIds(p);

        Assert.Equal(TiposDocumentoSistema.Id(TipoDocumento.Passaporte), p.Documentos[0].TipoDocumentoId);
    }

    [Fact]
    public void Enum_e_copiado_do_tipo_escolhido_e_validade_obrigatoria_so_em_ativo()
    {
        var cnhSemValidade = new PessoaDocumento { Id = Guid.NewGuid(), TipoDocumentoId = Cnh.Id, Tipo = TipoDocumento.Rg, Numero = "1" };
        var alvara = new PessoaDocumento { Id = Guid.NewGuid(), TipoDocumentoId = Alvara.Id, Tipo = TipoDocumento.Cnh, Numero = "2" };
        var cnhAntiga = new PessoaDocumento { Id = Guid.NewGuid(), TipoDocumentoId = Cnh.Id, Numero = "3", Ativo = false };

        var erros = RegrasDocumento.Aplicar([cnhSemValidade, alvara, cnhAntiga], new Dictionary<Guid, Guid>(), Cadastro(Cnh, Alvara));

        Assert.Equal(TipoDocumento.Cnh, cnhSemValidade.Tipo);
        Assert.Equal(TipoDocumento.Outro, alvara.Tipo); // o aplicativo não decide o enum
        Assert.Equal(new[] { "Documento 1 (CNH): informe a validade." }, erros);
    }

    [Fact]
    public void Tipo_desativado_so_vale_se_ja_era_o_do_documento()
    {
        var antigo = new TipoDocumentoCadastro { Id = Guid.NewGuid(), Nome = "Carteira antiga", Ativo = false };
        var d = new PessoaDocumento { Id = Guid.NewGuid(), TipoDocumentoId = antigo.Id, Numero = "1" };

        Assert.Single(RegrasDocumento.Aplicar([d], new Dictionary<Guid, Guid>(), Cadastro(antigo)));
        Assert.Empty(RegrasDocumento.Aplicar([d], new Dictionary<Guid, Guid> { [d.Id] = antigo.Id }, Cadastro(antigo)));
        Assert.Single(RegrasDocumento.Aplicar([d], new Dictionary<Guid, Guid>(), Cadastro()));
    }

    [Fact]
    public void Documento_inativo_antigo_nao_impede_gravar()
    {
        var p = new Pessoa { Nome = "Ana" };
        p.Documentos.Add(new PessoaDocumento { Id = Guid.NewGuid(), Numero = string.Empty, Uf = "XX", Ativo = false });

        Assert.DoesNotContain(PessoaValidador.Validar(p), e => e.StartsWith("Documento 1", StringComparison.Ordinal));
    }

    [Fact]
    public void Tipo_valida_nome_e_dias_de_aviso()
    {
        var tipo = new TipoDocumentoCadastro { Nome = "  Certificado   digital ", DiasAvisoVencimento = -1 };
        RegrasDocumento.Normalizar(tipo);

        Assert.Equal("Certificado digital", tipo.Nome);
        Assert.Single(RegrasDocumento.Validar(tipo));
    }

    [Fact]
    public void Ids_dos_tipos_de_sistema_sao_unicos_e_cobrem_o_enum()
    {
        Assert.Equal(TiposDocumentoSistema.Todos.Count, TiposDocumentoSistema.Todos.Select(t => t.Id).Distinct().Count());
        foreach (var tipo in Enum.GetValues<TipoDocumento>())
            Assert.Contains(TiposDocumentoSistema.Todos, t => t.Tipo == tipo);
    }
}
