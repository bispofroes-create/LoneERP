using Lone.Cliente.ViewModels.Comum;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;
using Lone.Domain.Fiscal;

namespace Lone.Tests.Cliente;

/// <summary>Identificação: natureza jurídica da tabela, documento completo (aviso de duplicidade e consulta automática).</summary>
public class IdentificacaoDocumentoTests
{
    private static PessoaFormulario NovaJuridica()
    {
        var f = PessoaFormulario.NovaPessoa();
        f.Natureza = Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        return f;
    }

    [Fact]
    public void Natureza_juridica_escolhida_na_lista_grava_o_codigo_e_o_gravado_aparece_descrito()
    {
        var f = NovaJuridica();
        var lista = f.Principal.NaturezaJuridicaLista;

        lista.Texto = "anonima fechada";
        var item = Assert.Single(lista.Sugestoes);
        lista.EscolherCommand.Execute(item);
        Assert.Equal("2054", f.Principal.NaturezaJuridica);
        Assert.Equal("2054", f.ParaDto().Estabelecimentos[0].NaturezaJuridica);

        f.Principal.NaturezaJuridica = "2062"; // ex.: veio da consulta do CNPJ
        Assert.Equal("206-2 · Sociedade Empresária Limitada", lista.Texto);
        Assert.True(lista.Escolhido);

        lista.Texto = string.Empty; // apagou
        Assert.Equal(string.Empty, f.Principal.NaturezaJuridica);
    }

    [Fact]
    public void Natureza_juridica_digitada_sem_escolher_nao_grava_e_codigo_fora_da_tabela_continua()
    {
        var f = NovaJuridica();
        f.Principal.NaturezaJuridicaLista.Texto = "xyz";
        Assert.Contains(f.ValidarLocalmente(), e => e.StartsWith("Natureza jurídica:"));

        var antigo = NovaJuridica();
        antigo.Principal.NaturezaJuridica = "9997";
        Assert.Equal("9997", antigo.Principal.NaturezaJuridicaLista.Texto);
        Assert.Equal("9997", antigo.ParaDto().Estabelecimentos[0].NaturezaJuridica);
    }

    [Fact]
    public void Documento_completo_e_valido_avisa_a_tela_uma_vez()
    {
        var f = PessoaFormulario.NovaPessoa();
        var chamadas = 0;
        f.AoCompletarDocumento = () => { chamadas++; return Task.CompletedTask; };

        f.Documento = "529.982.247-2"; // incompleto
        Assert.Equal(0, chamadas);
        f.Documento = "529.982.247-25";
        Assert.Equal(1, chamadas);
        Assert.Equal("52998224725", f.DocumentoCompleto);
        f.Documento = "52998224725"; // mesmo CPF sem máscara
        Assert.Equal(1, chamadas);

        var pj = NovaJuridica();
        var consultas = 0;
        pj.AoCompletarDocumento = () => { consultas++; return Task.CompletedTask; };
        pj.Principal.Cnpj = "11.222.333/0001-81";
        Assert.Equal(1, consultas);
    }

    [Fact]
    public void Abrir_cadastro_gravado_nao_confere_o_documento_de_novo()
    {
        var f = PessoaFormulario.De(new PessoaDto
        {
            Id = Guid.NewGuid(), Natureza = NaturezaPessoa.Fisica, Nome = "Ana", DocumentoPrincipal = "52998224725"
        });
        var chamadas = 0;
        f.AoCompletarDocumento = () => { chamadas++; return Task.CompletedTask; };
        f.Nome = "Ana Maria";
        Assert.Equal(0, chamadas);
    }

    [Fact]
    public void Documento_em_uso_mostra_o_aviso_e_some_ao_trocar_o_documento()
    {
        var f = NovaJuridica();
        f.Principal.Cnpj = "11.222.333/0001-81";
        var outra = Guid.NewGuid();
        f.DefinirDocumentoEmUso(new DocumentoEmUsoResposta { EmUso = true, Id = outra, Codigo = 12, Nome = "ABC Ltda" });

        Assert.True(f.TemAvisoDocumentoEmUso);
        Assert.StartsWith("Esta empresa (mesma raiz de CNPJ) já está cadastrada: 000012 - ABC Ltda.", f.AvisoDocumentoEmUso);
        Assert.Equal(outra, f.DocumentoEmUsoId);

        f.Principal.Cnpj = "11.444.777/0001-61";
        Assert.False(f.TemAvisoDocumentoEmUso);
        Assert.Null(f.DocumentoEmUsoId);
    }

    [Fact]
    public void Tabela_de_natureza_juridica_tem_codigo_de_quatro_digitos_e_descricao()
    {
        Assert.Contains(NaturezasJuridicas.Todas, n => n.Codigo == "2054" && n.Texto == "205-4 · Sociedade Anônima Fechada");
        Assert.All(NaturezasJuridicas.Todas, n => Assert.Equal(4, n.Codigo.Length));
    }
}
