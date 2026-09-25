using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

public class DadosComplementaresTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 24);
    private static readonly DateTime Agora = new(2026, 9, 24, 15, 0, 0, DateTimeKind.Utc);

    private static Pessoa PessoaFisica(params TipoPapel[] papeis) => new()
    {
        Id = Guid.NewGuid(),
        Nome = "Ana",
        Natureza = NaturezaPessoa.Fisica,
        Papeis = papeis.Select(p => new PessoaPapel { Id = Guid.NewGuid(), Papel = p, Ativo = true }).ToList()
    };

    [Fact]
    public void Cor_raca_so_fica_gravada_para_funcionario()
    {
        var cliente = PessoaFisica(TipoPapel.Cliente);
        cliente.CorRaca = CorRaca.Parda;
        var funcionario = PessoaFisica(TipoPapel.Funcionario);
        funcionario.CorRaca = CorRaca.Parda;

        PessoaNormalizador.Normalizar(cliente, Hoje, Agora);
        PessoaNormalizador.Normalizar(funcionario, Hoje, Agora);

        Assert.Equal(CorRaca.NaoInformado, cliente.CorRaca);
        Assert.Equal(CorRaca.Parda, funcionario.CorRaca);
    }

    [Fact]
    public void Dados_civis_somem_na_pessoa_juridica_e_dados_da_empresa_na_fisica()
    {
        var empresa = PessoaFisica();
        empresa.Natureza = NaturezaPessoa.Juridica;
        empresa.Sexo = SexoRegistro.Feminino;
        empresa.NomeMae = "Maria";
        var pessoa = PessoaFisica();
        pessoa.CapitalSocial = 1000m;
        pessoa.Socios.Add(new PessoaSocio { Id = Guid.NewGuid(), Nome = "Sócio" });

        PessoaNormalizador.Normalizar(empresa, Hoje, Agora);
        PessoaNormalizador.Normalizar(pessoa, Hoje, Agora);

        Assert.Equal(SexoRegistro.NaoInformado, empresa.Sexo);
        Assert.Null(empresa.NomeMae);
        Assert.Null(pessoa.CapitalSocial);
        Assert.Empty(pessoa.Socios);
    }

    [Fact]
    public void Consentimento_recebe_as_datas_do_servidor_ao_autorizar_e_ao_revogar()
    {
        var p = PessoaFisica();
        var email = new PessoaConsentimento { Id = Guid.NewGuid(), Canal = CanalComunicacao.Email, Concedido = true };
        var whatsapp = new PessoaConsentimento
        {
            Id = Guid.NewGuid(), Canal = CanalComunicacao.WhatsApp, Concedido = false, ConcedidoEm = Agora.AddDays(-30)
        };
        p.Consentimentos.AddRange([email, whatsapp, new PessoaConsentimento { Id = Guid.NewGuid(), Canal = CanalComunicacao.Email }]);

        PessoaNormalizador.Normalizar(p, Hoje, Agora);

        Assert.Equal(2, p.Consentimentos.Count); // um por canal
        Assert.Equal(Agora, email.ConcedidoEm);
        Assert.Null(email.RevogadoEm);
        Assert.Equal(Agora, whatsapp.RevogadoEm);
    }

    [Fact]
    public void Etiquetas_sem_referencia_vazia_e_sem_repetir()
    {
        var p = PessoaFisica();
        var vip = Guid.NewGuid();
        var atacado = Guid.NewGuid();
        foreach (var id in new[] { vip, vip, Guid.Empty, atacado })
            p.Etiquetas.Add(new PessoaEtiqueta { Id = Guid.NewGuid(), EtiquetaId = id });

        PessoaNormalizador.Normalizar(p, Hoje, Agora);

        Assert.Equal(new[] { vip, atacado }, p.Etiquetas.Select(e => e.EtiquetaId));
    }

    [Fact]
    public void CNAEs_secundarios_ficam_so_com_codigos_validos_sem_repetir()
    {
        var p = PessoaFisica();
        p.Natureza = NaturezaPessoa.Juridica;
        p.Estabelecimentos.Add(new Estabelecimento { Id = Guid.NewGuid(), Principal = true, CnaesSecundarios = "4711-3/02; 4711302, 123, 5611201" });

        PessoaNormalizador.Normalizar(p, Hoje, Agora);

        Assert.Equal("4711302,5611201", p.Estabelecimentos[0].CnaesSecundarios);
    }

    [Fact]
    public void Validador_confere_tamanho_do_motivo_e_datas_futuras()
    {
        var p = PessoaFisica();
        p.SituacaoMotivo = new string('x', 201);
        p.PrimeiroContatoEm = DateOnly.FromDateTime(DateTime.Today).AddDays(1);

        var erros = PessoaValidador.Validar(p);

        Assert.Contains("O motivo da situação pode ter no máximo 200 caracteres.", erros);
        Assert.Contains("A data do primeiro contato não pode ser no futuro.", erros);
    }
}
