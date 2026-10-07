using Lone.Application;
using Lone.Application.Pessoas;
using Lone.Application.Seguranca;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Papeis;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;
using Lone.Infrastructure;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Tests.Infraestrutura;

/// <summary>O usuário da requisição nos testes do fluxo real: alcance Tudo e todas as permissões, menos as negadas.</summary>
public sealed class UsuarioDoTeste : IUsuarioAtual, IEmpresaAtual, IAutorizacao, IMotivoDaOperacao, IAlcanceDoUsuario
{
    public HashSet<string> Negadas { get; } = new();
    public string? Motivo { get; set; }
    public Guid? Id => null;
    public string Nome => "Teste";
    public Guid? EmpresaId => null;
    public Guid? EstabelecimentoId => null;
    public AlcanceComercial Alcance => AlcanceComercial.Tudo;
    public Guid? PessoaId => null;

    public bool Possui(string permissao) => !Negadas.Contains(permissao);

    public void Exigir(string permissao)
    {
        if (!Possui(permissao)) throw new AcessoNegadoException(permissao);
    }
}

/// <summary>
/// Bloco G (P0-4): o PessoaAppService de verdade, montado pelo mesmo contêiner da API (AddLoneApplication +
/// AddLoneInfrastructure), sobre um banco temporário criado pelo modelo atual, com as proteções do banco (finalidades e
/// Auditoria) e dois municípios do IBGE. Um por classe de teste; pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public sealed class AmbienteCadastroPessoas : IAsyncLifetime
{
    public const int SaoPaulo = 3550308;
    public const int Campinas = 3509502;

    public BancoDeTeste? Banco { get; private set; }
    private ServiceProvider? _provedor;

    /// <summary>P1-8: pasta temporária dos anexos destes testes (nunca a do sistema); apagada no fim.</summary>
    public string PastaAnexos { get; } = Path.Combine(Path.GetTempPath(), "Lone_Teste_Anexos_" + Guid.NewGuid().ToString("N"));

    public async Task InitializeAsync()
    {
        if (FatoSqlServerAttribute.Conexao is null) return;
        Banco = await BancoDeTeste.CriarAsync(comProtecoes: true);
        await using (var db = Banco.Contexto())
        {
            foreach (var lote in BancoDeTeste.Lotes(SqlMigracaoAuditoria.CriarProtecao))
                await db.Database.ExecuteSqlRawAsync(lote);
            db.Municipios.AddRange(
                new Municipio { Id = SaoPaulo, Nome = "São Paulo", NomeBusca = "SAO PAULO", Uf = "SP", CodigoUf = 35, AtualizadoEm = DateTime.UtcNow },
                new Municipio { Id = Campinas, Nome = "Campinas", NomeBusca = "CAMPINAS", Uf = "SP", CodigoUf = 35, AtualizadoEm = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Lone"] = Banco.Conexao,
            ["Jwt:Chave"] = new string('k', 64),
            ["Anexos:Pasta"] = PastaAnexos
        }).Build();

        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AddLoneApplication();
        servicos.AddLoneInfrastructure(configuracao);
        servicos.AddScoped<UsuarioDoTeste>();
        servicos.AddScoped<IUsuarioAtual>(sp => sp.GetRequiredService<UsuarioDoTeste>());
        servicos.AddScoped<IEmpresaAtual>(sp => sp.GetRequiredService<UsuarioDoTeste>());
        servicos.AddScoped<IAutorizacao>(sp => sp.GetRequiredService<UsuarioDoTeste>());
        servicos.AddScoped<IMotivoDaOperacao>(sp => sp.GetRequiredService<UsuarioDoTeste>());
        servicos.AddScoped<IAlcanceDoUsuario>(sp => sp.GetRequiredService<UsuarioDoTeste>());
        _provedor = servicos.BuildServiceProvider();
    }

    /// <summary>Uma "requisição": escopo novo, com o serviço e o usuário dela.</summary>
    public AsyncServiceScope Requisicao() => _provedor!.CreateAsyncScope();

    public async Task DisposeAsync()
    {
        if (_provedor is not null) await _provedor.DisposeAsync();
        if (Banco is not null) await Banco.DisposeAsync();
        if (Directory.Exists(PastaAnexos)) Directory.Delete(PastaAnexos, recursive: true);
    }
}

/// <summary>CPF e CNPJ válidos e diferentes a cada chamada (o banco é compartilhado pelos testes da classe).</summary>
internal static class DocumentosDeTeste
{
    private static readonly int[] PesosCpf1 = [10, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosCpf2 = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosCnpj1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosCnpj2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    private static int Dv(string digitos, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < pesos.Length; i++) soma += (digitos[i] - '0') * pesos[i];
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static string Digitos(int quantidade)
    {
        string texto;
        do texto = string.Concat(Enumerable.Range(0, quantidade).Select(_ => (char)('0' + Random.Shared.Next(10))));
        while (texto.Distinct().Count() == 1 || texto[0] == '0');
        return texto;
    }

    public static string Cpf()
    {
        var b = Digitos(9);
        var d1 = Dv(b, PesosCpf1);
        return b + d1 + Dv(b + d1, PesosCpf2);
    }

    public static string Raiz() => Digitos(8);

    public static string Cnpj(string raiz, int ordem = 1)
    {
        var b = raiz + ordem.ToString("D4");
        var d1 = Dv(b, PesosCnpj1);
        return b + d1 + Dv(b + d1, PesosCnpj2);
    }
}

/// <summary>
/// Bloco G: o fluxo completo da gravação de pessoa (PessoaAppService.SalvarAsync → regras → repositório → SQL Server),
/// documentando o comportamento que já existia (P0-4) e provando as correções G2–G7 no caminho real.
/// </summary>
public class GravacaoPessoaFluxoTests : IClassFixture<AmbienteCadastroPessoas>
{
    private readonly AmbienteCadastroPessoas _ambiente;

    public GravacaoPessoaFluxoTests(AmbienteCadastroPessoas ambiente) => _ambiente = ambiente;

    private BancoDeTeste Banco => _ambiente.Banco!;

    private async Task<PessoaDto> SalvarAsync(PessoaDto dto, params string[] negadas)
    {
        await using var requisicao = _ambiente.Requisicao();
        requisicao.ServiceProvider.GetRequiredService<UsuarioDoTeste>().Negadas.UnionWith(negadas);
        return (await requisicao.ServiceProvider.GetRequiredService<IPessoaAppService>().SalvarAsync(dto)).Pessoa;
    }

    private async Task<ResultadoSalvarPessoa> SalvarComAvisosAsync(PessoaDto dto)
    {
        await using var requisicao = _ambiente.Requisicao();
        return await requisicao.ServiceProvider.GetRequiredService<IPessoaAppService>().SalvarAsync(dto);
    }

    private async Task<PessoaDto> ObterAsync(Guid id)
    {
        await using var requisicao = _ambiente.Requisicao();
        return (await requisicao.ServiceProvider.GetRequiredService<IPessoaAppService>().ObterAsync(id))!;
    }

    private static PessoaDto Fisica(string? cpf = null, string nome = "Ana Teste") =>
        new() { Natureza = NaturezaPessoa.Fisica, Nome = nome, DocumentoPrincipal = cpf ?? DocumentosDeTeste.Cpf() };

    private static EnderecoDto EnderecoSaoPaulo(string logradouro = "Avenida Paulista") => new()
    {
        Id = Guid.NewGuid(), Logradouro = logradouro, Numero = "1000", Bairro = "Bela Vista", Cep = "01310100",
        MunicipioId = AmbienteCadastroPessoas.SaoPaulo
    };

    private static async Task<ValidacaoException> RecusadaAsync(Func<Task> acao) => await Assert.ThrowsAsync<ValidacaoException>(acao);

    // =====================================================================================================
    // G1 / P0-4 — o comportamento que já existia, agora protegido pelo fluxo real
    // =====================================================================================================

    [FatoSqlServer]
    public async Task Inclui_pessoa_fisica_com_endereco_telefone_e_email_como_o_banco_guarda()
    {
        var cpf = DocumentosDeTeste.Cpf();
        var dto = Fisica(cpf);
        dto.Enderecos.Add(EnderecoSaoPaulo());
        dto.MeiosContato.Add(new MeioContatoDto { Id = Guid.NewGuid(), Tipo = TipoContato.Celular, Valor = "(11) 98765-4321" });
        dto.MeiosContato.Add(new MeioContatoDto { Id = Guid.NewGuid(), Tipo = TipoContato.Email, Valor = "Ana@Teste.com.br" });

        var salva = await SalvarAsync(dto);

        Assert.True(salva.Codigo > 0);
        Assert.NotNull(salva.Versao);
        Assert.Equal(cpf, salva.DocumentoPrincipal);
        var endereco = Assert.Single(salva.Enderecos);
        Assert.Equal(("São Paulo", "SP", "3550308"), (endereco.Cidade, endereco.Uf, endereco.CodigoMunicipioIbge)); // cópias do IBGE
        Assert.Contains(salva.MeiosContato, m => m.Valor == "11987654321");
        Assert.Contains(salva.MeiosContato, m => m.Valor == "ana@teste.com.br");
        var fiscal = Assert.Single(salva.Estabelecimentos); // PF: um conjunto de dados fiscais, criado pela API
        Assert.Equal(IndicadorIE.NaoContribuinte, fiscal.IndicadorIE);

        await using var db = Banco.Contexto();
        Assert.True(await db.Auditoria.AnyAsync(a => a.RaizId == salva.Id && a.Acao == AcaoAuditoria.Inclusao));
    }

    [FatoSqlServer]
    public async Task Inclui_pessoa_juridica_com_matriz_e_filial_e_a_raiz_vira_o_documento()
    {
        var raiz = DocumentosDeTeste.Raiz();
        var dto = new PessoaDto
        {
            Natureza = NaturezaPessoa.Juridica, Nome = "Empresa Teste " + raiz,
            Estabelecimentos =
            [
                new EstabelecimentoDto { Id = Guid.NewGuid(), Principal = true, Cnpj = DocumentosDeTeste.Cnpj(raiz, 1) },
                new EstabelecimentoDto { Id = Guid.NewGuid(), Principal = false, Cnpj = DocumentosDeTeste.Cnpj(raiz, 2) }
            ]
        };

        var salva = await SalvarAsync(dto);

        Assert.Equal(raiz, salva.DocumentoPrincipal);
        Assert.Equal(2, salva.Estabelecimentos.Count);
        Assert.Single(salva.Estabelecimentos, e => e.Principal);
    }

    [FatoSqlServer]
    public async Task Inclui_estrangeiro_com_endereco_no_exterior()
    {
        var dto = new PessoaDto { Natureza = NaturezaPessoa.Estrangeiro, Nome = "John Smith", DocumentoPrincipal = "P1234567" };
        dto.Enderecos.Add(new EnderecoDto
        {
            Id = Guid.NewGuid(), Logradouro = "Rua Augusta", Numero = "10", Cidade = "Lisboa", Pais = "Portugal", CodigoPais = "6076"
        });

        var salva = await SalvarAsync(dto);

        Assert.Equal("P1234567", salva.DocumentoPrincipal);
        var endereco = Assert.Single(salva.Enderecos);
        Assert.Equal(("Lisboa", "Portugal", Ufs.Exterior), (endereco.Cidade, endereco.Pais, endereco.Uf));
        Assert.Null(endereco.MunicipioId);
        Assert.Equal(IndicadorIE.NaoContribuinte, Assert.Single(salva.Estabelecimentos).IndicadorIE); // exterior: sempre não contribuinte
    }

    [FatoSqlServer]
    public async Task Codigo_postal_do_exterior_com_letras_e_gravado_e_reaberto_como_digitado()
    {
        // Bloco A, D-1: o código postal do exterior guarda as letras (antes ficava só com os algarismos: "SW1A 1AA" → "11").
        var dto = new PessoaDto { Natureza = NaturezaPessoa.Estrangeiro, Nome = "John Smith", DocumentoPrincipal = "P7654321" };
        dto.Enderecos.Add(new EnderecoDto
        {
            Id = Guid.NewGuid(), Logradouro = "Downing Street", Numero = "10", Cidade = "Londres", Pais = "Reino Unido", CodigoPais = "6289",
            Cep = "sw1a 1aa"
        });

        var salva = await SalvarAsync(dto);
        var reaberta = await ObterAsync(salva.Id);

        var endereco = Assert.Single(reaberta.Enderecos);
        Assert.Equal(("SW1A 1AA", "6289", Ufs.Exterior), (endereco.Cep, endereco.CodigoPais, endereco.Uf));
        Assert.Null(endereco.MunicipioId);
    }

    [FatoSqlServer]
    public async Task Cpf_ja_cadastrado_e_recusado_no_campo_documento()
    {
        var cpf = DocumentosDeTeste.Cpf();
        await SalvarAsync(Fisica(cpf, "Primeira"));

        var erro = await RecusadaAsync(() => SalvarAsync(Fisica(cpf, "Segunda")));

        var item = Assert.Single(erro.Itens, i => i.Campo == CamposFichaPessoa.Documento);
        Assert.StartsWith("Este CPF já está cadastrado:", item.Mensagem);
    }

    [FatoSqlServer]
    public async Task Raiz_de_cnpj_ja_cadastrada_e_recusada_com_a_orientacao_da_filial()
    {
        var raiz = DocumentosDeTeste.Raiz();
        PessoaDto Empresa(int ordem) => new()
        {
            Natureza = NaturezaPessoa.Juridica, Nome = "Raiz " + raiz + " " + ordem,
            Estabelecimentos = [new EstabelecimentoDto { Id = Guid.NewGuid(), Principal = true, Cnpj = DocumentosDeTeste.Cnpj(raiz, ordem) }]
        };
        await SalvarAsync(Empresa(1));

        var erro = await RecusadaAsync(() => SalvarAsync(Empresa(3)));

        Assert.Contains(erro.Itens, i => i.Campo == CamposFichaPessoa.Documento && i.Mensagem.StartsWith("Esta empresa (mesma raiz de CNPJ) já está cadastrada"));
    }

    [FatoSqlServer]
    public async Task Cpf_invalido_e_municipio_inexistente_sao_recusados()
    {
        var dto = Fisica("12345678900");
        var endereco = EnderecoSaoPaulo();
        endereco.MunicipioId = 9999999;
        dto.Enderecos.Add(endereco);

        var erro = await RecusadaAsync(() => SalvarAsync(dto));

        Assert.Contains(erro.Itens, i => i.Mensagem == "CPF inválido." && i.Campo == CamposFichaPessoa.Documento);
        Assert.Contains(erro.Erros, e => e.Contains("município não encontrado na tabela do IBGE"));
    }

    [FatoSqlServer]
    public async Task Sem_permissao_de_criar_ou_de_editar_a_gravacao_e_negada()
    {
        await Assert.ThrowsAsync<AcessoNegadoException>(() => SalvarAsync(Fisica(), Permissoes.Pessoas.Criar));

        var salva = await SalvarAsync(Fisica());
        salva.Nome = "Outro nome";
        await Assert.ThrowsAsync<AcessoNegadoException>(() => SalvarAsync(salva, Permissoes.Pessoas.Editar));
    }

    [FatoSqlServer]
    public async Task Mudar_o_credito_do_cliente_exige_a_permissao_propria()
    {
        var dto = Fisica();
        dto.Papeis.Add(new PapelDto { Id = Guid.NewGuid(), PapelId = PapeisSistema.Id(TipoPapel.Cliente), Ativo = true });
        dto.ContasCliente.Add(new ContaClienteDto { Id = Guid.NewGuid(), LimiteCredito = 1000m });
        var salva = await SalvarAsync(dto);

        salva.ContasCliente.Single(c => c.EmpresaId is null).LimiteCredito = 2000m;
        await Assert.ThrowsAsync<AcessoNegadoException>(() => SalvarAsync(salva, Permissoes.Pessoas.AlterarCredito));

        var denovo = await ObterAsync(salva.Id);
        Assert.Equal(1000m, denovo.ContasCliente.Single(c => c.EmpresaId is null).LimiteCredito); // nada foi gravado
    }

    [FatoSqlServer]
    public async Task Versao_antiga_da_conflito_de_edicao()
    {
        var aberta = await SalvarAsync(Fisica());
        var outroUsuario = await ObterAsync(aberta.Id);
        outroUsuario.Nome = "Gravado antes";
        await SalvarAsync(outroUsuario);

        aberta.Nome = "Gravado depois, com a versão antiga";
        await Assert.ThrowsAnyAsync<ConflitoDeEdicaoException>(() => SalvarAsync(aberta));
        Assert.Equal("Gravado antes", (await ObterAsync(aberta.Id)).Nome);
    }

    [FatoSqlServer]
    public async Task Alteracao_fica_na_auditoria_campo_a_campo_com_o_motivo()
    {
        var salva = await SalvarAsync(Fisica(nome: "Nome antigo"));
        salva.Nome = "Nome novo";
        salva.MotivoAlteracao = "Correção do cadastro";
        await SalvarAsync(salva);

        await using var db = Banco.Contexto();
        var linha = await db.Auditoria.AsNoTracking()
            .SingleAsync(a => a.RaizId == salva.Id && a.Acao == AcaoAuditoria.Alteracao && a.ValorAnterior == "Nome antigo");
        Assert.Equal(("Nome novo", "Correção do cadastro"), (linha.ValorNovo, linha.Motivo));
    }

    [FatoSqlServer]
    public async Task Endereco_removido_na_ficha_fica_inativo_e_nao_e_apagado()
    {
        var dto = Fisica();
        dto.Enderecos.Add(EnderecoSaoPaulo());
        var salva = await SalvarAsync(dto);

        salva.Enderecos[0].Ativo = false;
        var depois = await SalvarAsync(salva);

        Assert.False(Assert.Single(depois.Enderecos).Ativo);
        await using var db = Banco.Contexto();
        Assert.Equal(1, await db.PessoaEnderecos.CountAsync(e => e.PessoaId == salva.Id));
    }

    [FatoSqlServer]
    public async Task Ficha_que_nao_traz_um_endereco_gravado_e_recusada()
    {
        var dto = Fisica();
        dto.Enderecos.Add(EnderecoSaoPaulo());
        var salva = await SalvarAsync(dto);

        salva.Enderecos.Clear(); // chamada incompleta: nunca apaga em silêncio
        var erro = await RecusadaAsync(() => SalvarAsync(salva));

        Assert.Contains(erro.Erros, e => e.StartsWith("A ficha enviada não traz todos os endereços já gravados"));
    }

    [FatoSqlServer]
    public async Task Desativar_pelo_formulario_e_recusado()
    {
        var salva = await SalvarAsync(Fisica());
        salva.Situacao = SituacaoPessoa.Inativo;

        var erro = await RecusadaAsync(() => SalvarAsync(salva));

        Assert.Contains("use a ação \"Desativar\"", Assert.Single(erro.Erros));
    }

    [FatoSqlServer]
    public async Task Cadastro_arquivado_e_somente_leitura()
    {
        var salva = await SalvarAsync(Fisica());
        await using (var db = Banco.Contexto())
            await db.Pessoas.Where(p => p.Id == salva.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Situacao, SituacaoPessoa.Arquivado));

        var erro = await RecusadaAsync(() => SalvarAsync(salva));

        Assert.Equal("Cadastro arquivado é somente leitura.", Assert.Single(erro.Erros));
    }

    [FatoSqlServer]
    public async Task Contribuinte_sem_inscricao_estadual_e_recusado_no_campo()
    {
        var dto = Fisica();
        var fiscal = new EstabelecimentoDto { Id = Guid.NewGuid(), Principal = true, IndicadorIE = IndicadorIE.Contribuinte, ProdutorRural = true };
        dto.Estabelecimentos.Add(fiscal);

        var erro = await RecusadaAsync(() => SalvarAsync(dto));

        Assert.Contains(erro.Itens, i => i.Campo == CamposFichaPessoa.InscricaoEstadual && i.Item == fiscal.Id);
    }

    [FatoSqlServer]
    public async Task Etiqueta_que_nao_existe_e_recusada()
    {
        var dto = Fisica();
        dto.EtiquetaIds.Add(Guid.NewGuid());

        await RecusadaAsync(() => SalvarAsync(dto));
    }

    [FatoSqlServer]
    public async Task Nome_ja_cadastrado_e_aviso_e_nao_impede_a_gravacao()
    {
        var nome = "Homônimo " + Guid.NewGuid().ToString("N")[..8];
        await SalvarAsync(Fisica(nome: nome));

        var resultado = await SalvarComAvisosAsync(Fisica(nome: nome));

        Assert.True(resultado.Pessoa.Codigo > 0);
        Assert.Contains(resultado.Avisos, a => a.StartsWith("Possível cadastro duplicado"));
    }

    // =====================================================================================================
    // G2 — limites antes do banco
    // =====================================================================================================

    [FatoSqlServer]
    public async Task Texto_maior_que_a_coluna_e_recusado_no_campo_antes_do_banco()
    {
        var dto = Fisica();
        var endereco = EnderecoSaoPaulo(new string('R', 151));
        dto.Enderecos.Add(endereco);
        dto.NomeSocial = new string('s', 151);

        var erro = await RecusadaAsync(() => SalvarAsync(dto)); // ValidacaoException, nunca DbUpdateException

        Assert.Contains(erro.Itens, i => i.Campo == CamposFichaPessoa.Logradouro && i.Item == endereco.Id);
        Assert.Contains(erro.Itens, i => i.Campo == CamposFichaPessoa.NomeSocial);
    }

    // =====================================================================================================
    // G3 — violação conhecida do banco traduzida; desconhecida continua técnica
    // =====================================================================================================

    private async Task GravarPeloRepositorioAsync(Pessoa pessoa)
    {
        await using var requisicao = _ambiente.Requisicao();
        await requisicao.ServiceProvider.GetRequiredService<IPessoaRepositorio>()
            .SalvarAsync(pessoa, nova: true, OrigemAlteracao.Usuario, CancellationToken.None);
    }

    private static Pessoa PessoaFisica(string cpf) =>
        new() { Id = IdSequencial.Novo(), Nome = "Corrida " + cpf, Natureza = NaturezaPessoa.Fisica, DocumentoPrincipal = cpf };

    [FatoSqlServer]
    public async Task Cpf_gravado_ao_mesmo_tempo_por_outro_usuario_vira_erro_no_campo()
    {
        // A conferência do serviço passou para os dois; o índice único barra o segundo (simulado gravando direto).
        var cpf = DocumentosDeTeste.Cpf();
        await GravarPeloRepositorioAsync(PessoaFisica(cpf));

        var erro = await RecusadaAsync(() => GravarPeloRepositorioAsync(PessoaFisica(cpf)));

        var item = Assert.Single(erro.Itens);
        Assert.Equal((ConflitosDocumentoPessoa.CpfJaCadastrado, CamposFichaPessoa.Documento), (item.Mensagem, item.Campo));
        Assert.DoesNotContain("IX_", item.Mensagem); // nada do SQL vai para o usuário
    }

    [FatoSqlServer]
    public async Task Cnpj_de_filial_ja_usado_em_outra_pessoa_vira_erro_no_cartao_da_filial()
    {
        var raizA = DocumentosDeTeste.Raiz();
        var cnpjA = DocumentosDeTeste.Cnpj(raizA);
        var a = new Pessoa { Id = IdSequencial.Novo(), Nome = "A " + raizA, Natureza = NaturezaPessoa.Juridica, DocumentoPrincipal = raizA };
        a.Estabelecimentos.Add(new Estabelecimento { Id = IdSequencial.Novo(), Principal = true, Cnpj = cnpjA, Ativo = true });
        await GravarPeloRepositorioAsync(a);

        var raizB = DocumentosDeTeste.Raiz();
        var b = new Pessoa { Id = IdSequencial.Novo(), Nome = "B " + raizB, Natureza = NaturezaPessoa.Juridica, DocumentoPrincipal = raizB };
        b.Estabelecimentos.Add(new Estabelecimento { Id = IdSequencial.Novo(), Principal = true, Cnpj = DocumentosDeTeste.Cnpj(raizB), Ativo = true });
        var filial = new Estabelecimento { Id = IdSequencial.Novo(), Principal = false, Cnpj = cnpjA, Ativo = true };
        b.Estabelecimentos.Add(filial);

        var erro = await RecusadaAsync(() => GravarPeloRepositorioAsync(b));

        var item = Assert.Single(erro.Itens);
        Assert.Equal((ConflitosDocumentoPessoa.CnpjJaCadastrado, CamposFichaPessoa.Cnpj, (Guid?)filial.Id), (item.Mensagem, item.Campo, item.Item));
    }

    [FatoSqlServer]
    public async Task Falha_desconhecida_do_banco_continua_tecnica_e_nao_vira_validacao()
    {
        var pessoa = PessoaFisica(DocumentosDeTeste.Cpf());
        pessoa.GrupoEmpresarialId = Guid.NewGuid(); // FK (e CHECK) que nenhuma regra traduz

        var erro = await Assert.ThrowsAnyAsync<DbUpdateException>(() => GravarPeloRepositorioAsync(pessoa));
        Assert.IsNotType<ValidacaoException>(erro);
    }

    // =====================================================================================================
    // G4 — pessoa física que vira jurídica
    // =====================================================================================================

    [FatoSqlServer]
    public async Task Pessoa_fisica_gravada_so_vira_juridica_com_a_confirmacao_exata()
    {
        var dto = Fisica();
        dto.NomeSocial = "Aninha";
        var salva = await SalvarAsync(dto);

        var raiz = DocumentosDeTeste.Raiz();
        salva.Natureza = NaturezaPessoa.Juridica;
        salva.DocumentoPrincipal = null;
        salva.Estabelecimentos.Single(e => e.Principal).Cnpj = DocumentosDeTeste.Cnpj(raiz);

        // API direta, sem confirmação: recusada, com o que seria perdido, e nada gravado.
        var erro = await RecusadaAsync(() => SalvarAsync(salva));
        var item = Assert.Single(erro.Itens, i => i.Campo == CamposFichaPessoa.Natureza);
        Assert.Contains("seriam apagados: o CPF; o nome social.", item.Mensagem);
        Assert.Equal(NaturezaPessoa.Fisica, (await ObterAsync(salva.Id)).Natureza);

        // Confirmação de outra troca (para estrangeiro) não serve.
        salva.ConfirmacaoTrocaNatureza = new ConfirmacaoTrocaNaturezaDto
        {
            De = NaturezaPessoa.Fisica, Para = NaturezaPessoa.Estrangeiro, Campos = [CamposFichaPessoa.Documento, CamposFichaPessoa.NomeSocial]
        };
        await RecusadaAsync(() => SalvarAsync(salva));

        // A confirmação desta troca, cobrindo o que se perde: grava.
        salva.ConfirmacaoTrocaNatureza.Para = NaturezaPessoa.Juridica;
        var juridica = await SalvarAsync(salva);

        Assert.Equal(NaturezaPessoa.Juridica, juridica.Natureza);
        Assert.Equal(raiz, juridica.DocumentoPrincipal);
        Assert.Null(juridica.NomeSocial);
        await using var db = Banco.Contexto();
        Assert.True(await db.Auditoria.AnyAsync(a => a.RaizId == salva.Id && a.Acao == AcaoAuditoria.Evento &&
                                                     a.Descricao!.StartsWith("Tipo de pessoa trocado de pessoa física para pessoa jurídica")));
        Assert.True(await db.Auditoria.AnyAsync(a => a.RaizId == salva.Id && a.Acao == AcaoAuditoria.Alteracao && a.ValorAnterior == "Aninha"));
    }

    // =====================================================================================================
    // G5 — MescladaEmId
    // =====================================================================================================

    [FatoSqlServer]
    public async Task A_ficha_nao_marca_nem_desmarca_pessoa_mesclada()
    {
        var destino = await SalvarAsync(Fisica(nome: "Destino"));
        var dto = Fisica(nome: "Origem");
        dto.MescladaEmId = destino.Id; // cliente comum tentando marcar
        var origem = await SalvarAsync(dto);
        Assert.Null(origem.MescladaEmId);

        origem.MescladaEmId = destino.Id; // na alteração também não
        Assert.Null((await SalvarAsync(origem)).MescladaEmId);

        // Marcada por fora (futura operação de mesclar): a ficha não desmarca.
        await using (var db = Banco.Contexto())
            await db.Pessoas.Where(p => p.Id == origem.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.MescladaEmId, destino.Id));
        var aberta = await ObterAsync(origem.Id);
        aberta.MescladaEmId = null;
        aberta.Nome = "Origem alterada";
        Assert.Equal(destino.Id, (await SalvarAsync(aberta)).MescladaEmId);
    }

    // =====================================================================================================
    // G6 — endereço inativo legado
    // =====================================================================================================

    [FatoSqlServer]
    public async Task Endereco_inativo_legado_sem_logradouro_nao_impede_salvar_e_reativado_volta_a_exigir()
    {
        var salva = await SalvarAsync(Fisica());
        var legado = Guid.NewGuid();
        await using (var db = Banco.Contexto())
        {
            db.PessoaEnderecos.Add(new PessoaEndereco { Id = legado, PessoaId = salva.Id, Logradouro = "", Cidade = "", Ativo = false, Ordem = 0 });
            await db.SaveChangesAsync(); // cadastro migrado: inativo e incompleto
        }

        var aberta = await ObterAsync(salva.Id);
        aberta.Nome = "Alterada com legado";
        var gravada = await SalvarAsync(aberta);
        Assert.Equal("Alterada com legado", gravada.Nome);

        gravada.Enderecos.Single(e => e.Id == legado).Ativo = true;
        var erro = await RecusadaAsync(() => SalvarAsync(gravada));
        Assert.Contains(erro.Itens, i => i.Campo == CamposFichaPessoa.Logradouro && i.Item == legado);
    }

    // =====================================================================================================
    // G7 — telefone/e-mail repetido
    // =====================================================================================================

    [FatoSqlServer]
    public async Task Telefone_repetido_na_mesma_pessoa_e_recusado_no_item()
    {
        var dto = Fisica();
        dto.MeiosContato.Add(new MeioContatoDto { Id = Guid.NewGuid(), Tipo = TipoContato.Celular, Valor = "(11) 98765-4321" });
        var repetido = new MeioContatoDto { Id = Guid.NewGuid(), Tipo = TipoContato.Celular, Valor = "11 98765 4321" };
        dto.MeiosContato.Add(repetido);

        var erro = await RecusadaAsync(() => SalvarAsync(dto));

        var item = Assert.Single(erro.Itens, i => i.Campo == CamposFichaPessoa.MeioContatoValor);
        Assert.Equal(repetido.Id, item.Item);
    }
}
