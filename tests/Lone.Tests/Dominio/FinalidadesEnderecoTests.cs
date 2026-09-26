using System.Data.SqlTypes;
using Lone.Domain.Enderecos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Tests.Dominio;

/// <summary>
/// Regras de endereço × finalidade no domínio (a API aplica estas regras; o banco reforça com FK composta, índices
/// únicos filtrados, CHECK e gatilhos — ver Infraestrutura/ModeloEnderecoFinalidadeTests e BancoEnderecoFinalidadeTests).
/// </summary>
public class FinalidadesEnderecoTests
{
    private static readonly Guid Entrega = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Entrega);
    private static readonly Guid Cobranca = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Cobranca);
    private static readonly Guid Comercial = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Comercial);
    private static readonly Guid Fiscal = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Fiscal);

    private static Dictionary<Guid, FinalidadeEnderecoCadastro> Cadastro(bool entregaAtiva = true, bool mesmaOrdem = false) =>
        FinalidadesEnderecoIniciais.Todas.Select(f => new FinalidadeEnderecoCadastro
        {
            Id = f.Id, Codigo = f.Codigo, Nome = f.Nome, Ordem = mesmaOrdem ? 1 : f.Ordem, DoSistema = true,
            Ativo = f.Codigo != FinalidadesEnderecoIniciais.Entrega || entregaAtiva
        }).ToDictionary(f => f.Id);

    private static PessoaEndereco Endereco(string numero, bool ativo = true, string cidade = "Curvelo", string uf = "MG", int ordem = 0) => new()
    {
        Id = Guid.NewGuid(), Logradouro = "Rua A", Numero = numero, Bairro = "Centro", Cep = "35790000", Cidade = cidade, Uf = uf,
        MunicipioId = 3120904, Ativo = ativo, Ordem = ordem
    };

    private static PessoaEnderecoFinalidade Uso(PessoaEndereco e, Guid finalidade, bool principal = false, bool ativo = true) => new()
    {
        Id = Guid.NewGuid(), PessoaEnderecoId = e.Id, FinalidadeId = finalidade, Principal = principal, Ativo = ativo
    };

    private static PessoaEnderecoFinalidade Copia(PessoaEnderecoFinalidade u) => new()
    {
        Id = u.Id, PessoaEnderecoId = u.PessoaEnderecoId, FinalidadeId = u.FinalidadeId, Principal = u.Principal, Ativo = u.Ativo
    };

    // ---------------------------------------------------------------- Modelo e principal

    [Fact]
    public void Finalidade_apontando_para_endereco_que_nao_e_da_pessoa_e_recusada()
    {
        var meu = Endereco("1");
        var deOutraPessoa = Endereco("2");
        var p = new Pessoa { Enderecos = [meu], FinalidadesEnderecos = [Uso(deOutraPessoa, Entrega)] };

        Assert.Contains(RegrasFinalidadeEndereco.Validar(p, Cadastro(), []), e => e.Contains("não é desta pessoa"));
    }

    [Fact]
    public void Dois_principais_para_a_mesma_finalidade_sao_recusados()
    {
        var a = Endereco("1");
        var b = Endereco("2");
        var p = new Pessoa { Enderecos = [a, b], FinalidadesEnderecos = [Uso(a, Entrega, true), Uso(b, Entrega, true)] };

        Assert.Contains(RegrasFinalidadeEndereco.Validar(p, Cadastro(), []), e => e.Contains("Mais de um endereço principal para Entrega"));
    }

    [Fact]
    public void Mesmo_endereco_pode_ser_principal_de_varias_finalidades_e_finalidades_podem_ter_principais_diferentes()
    {
        var a = Endereco("1");
        var b = Endereco("2");
        var p = new Pessoa
        {
            Enderecos = [a, b],
            FinalidadesEnderecos = [Uso(a, Comercial, true), Uso(a, Entrega, true), Uso(b, Fiscal, true), Uso(b, Entrega)]
        };

        Assert.Empty(RegrasFinalidadeEndereco.Validar(p, Cadastro(), []));
        Assert.Same(a, RegrasFinalidadeEndereco.EnderecoPrincipal(p, Comercial));
        Assert.Same(a, RegrasFinalidadeEndereco.EnderecoPrincipal(p, Entrega));
        Assert.Same(b, RegrasFinalidadeEndereco.EnderecoPrincipal(p, Fiscal));
    }

    [Fact]
    public void Um_registro_por_endereco_e_finalidade_ativo_repetido_ou_historico_repetido_sao_recusados()
    {
        var a = Endereco("1");
        var repetida = new Pessoa { Enderecos = [a], FinalidadesEnderecos = [Uso(a, Entrega), Uso(a, Entrega)] };
        var comHistoricoRepetido = new Pessoa { Enderecos = [a], FinalidadesEnderecos = [Uso(a, Entrega), Uso(a, Entrega, ativo: false)] };
        var umaSo = new Pessoa { Enderecos = [a], FinalidadesEnderecos = [Uso(a, Entrega, ativo: false)] };

        Assert.Contains(RegrasFinalidadeEndereco.Validar(repetida, Cadastro(), []), e => e.Contains("já possui a finalidade Entrega"));
        Assert.Contains(RegrasFinalidadeEndereco.Validar(comHistoricoRepetido, Cadastro(), []), e => e.Contains("já possui a finalidade Entrega"));
        Assert.Empty(RegrasFinalidadeEndereco.Validar(umaSo, Cadastro(), []));
    }

    [Fact]
    public void Finalidade_desativada_no_cadastro_nao_entra_em_associacao_nova_nem_recebe_principal_novo()
    {
        var a = Endereco("1");
        var uso = Uso(a, Entrega);
        var p = new Pessoa { Enderecos = [a], FinalidadesEnderecos = [uso] };

        Assert.Contains(RegrasFinalidadeEndereco.Validar(p, Cadastro(entregaAtiva: false), []), e => e.Contains("desativada"));
        Assert.Empty(RegrasFinalidadeEndereco.Validar(p, Cadastro(entregaAtiva: false), [Copia(uso)])); // já tinha: continua

        var gravada = Copia(uso);
        uso.Principal = true; // principal novo numa finalidade desativada
        Assert.Contains(RegrasFinalidadeEndereco.Validar(p, Cadastro(entregaAtiva: false), [gravada]), e => e.Contains("principal novo"));
        gravada.Principal = true; // já era principal: continua
        Assert.Empty(RegrasFinalidadeEndereco.Validar(p, Cadastro(entregaAtiva: false), [gravada]));
    }

    [Fact]
    public void Relacao_ou_endereco_inativo_nunca_e_principal()
    {
        var inativo = Endereco("1", ativo: false);
        var ativo = Endereco("2");
        var doInativo = Uso(inativo, Entrega, principal: true);
        var retirada = Uso(ativo, Cobranca, principal: true, ativo: false);
        var p = new Pessoa { Enderecos = [inativo, ativo], FinalidadesEnderecos = [doInativo, retirada] };

        RegrasFinalidadeEndereco.Normalizar(p);

        Assert.False(doInativo.Principal);
        Assert.False(retirada.Principal);
        Assert.True(doInativo.Ativo); // a relação fica como histórico
        Assert.Null(RegrasFinalidadeEndereco.EnderecoPrincipal(p, Entrega)); // ninguém herda o principal
    }

    // ---------------------------------------------------------------- Contrato: estado completo (I2)

    private static (Pessoa Gravada, Pessoa Enviada) FichaGravada()
    {
        var a = Endereco("1");
        var b = Endereco("2");
        var gravada = new Pessoa
        {
            Enderecos = [a, b],
            FinalidadesEnderecos = [Uso(a, Fiscal, true), Uso(b, Comercial, true), Uso(b, Entrega, ativo: false)]
        };
        var enviada = new Pessoa { Enderecos = [a, b], FinalidadesEnderecos = gravada.FinalidadesEnderecos.Select(Copia).ToList() };
        return (gravada, enviada);
    }

    [Fact]
    public void Estado_completo_passa()
    {
        var (gravada, enviada) = FichaGravada();
        Assert.Empty(RegrasFinalidadeEndereco.ValidarCompleto(enviada, gravada));
        Assert.Empty(RegrasFinalidadeEndereco.Validar(enviada, Cadastro(), gravada.FinalidadesEnderecos));
    }

    [Fact]
    public void Omitir_finalidade_gravada_mesmo_retirada_e_recusado()
    {
        var (gravada, enviada) = FichaGravada();
        enviada.FinalidadesEnderecos.RemoveAll(u => !u.Ativo);
        Assert.NotEmpty(RegrasFinalidadeEndereco.ValidarCompleto(enviada, gravada));
    }

    [Fact]
    public void Omitir_finalidade_principal_gravada_e_recusado()
    {
        var (gravada, enviada) = FichaGravada();
        enviada.FinalidadesEnderecos.RemoveAll(u => u.Principal && u.FinalidadeId == Fiscal);
        Assert.NotEmpty(RegrasFinalidadeEndereco.ValidarCompleto(enviada, gravada));
    }

    [Fact]
    public void Omitir_endereco_gravado_e_recusado()
    {
        var (gravada, enviada) = FichaGravada();
        enviada.Enderecos.RemoveAt(1);
        Assert.NotEmpty(RegrasFinalidadeEndereco.ValidarCompleto(enviada, gravada));
    }

    [Fact]
    public void Incluir_retirar_trocar_principal_e_reativar_sao_estados_completos_validos()
    {
        var (gravada, enviada) = FichaGravada();
        var a = enviada.Enderecos[0];
        var b = enviada.Enderecos[1];
        enviada.FinalidadesEnderecos.Add(Uso(a, Cobranca));                                           // inclusão
        enviada.FinalidadesEnderecos.Single(u => u.FinalidadeId == Comercial).Ativo = false;          // remoção (histórico)
        enviada.FinalidadesEnderecos.Single(u => u.FinalidadeId == Fiscal).Principal = false;         // troca de principal...
        enviada.FinalidadesEnderecos.Add(Uso(b, Fiscal, true));                                       // ...para B
        enviada.FinalidadesEnderecos.Single(u => u.FinalidadeId == Entrega).Ativo = true;             // reativação

        RegrasFinalidadeEndereco.Normalizar(enviada);

        Assert.Empty(RegrasFinalidadeEndereco.ValidarCompleto(enviada, gravada));
        Assert.Empty(RegrasFinalidadeEndereco.Validar(enviada, Cadastro(), gravada.FinalidadesEnderecos));
        Assert.False(enviada.FinalidadesEnderecos.Single(u => u.FinalidadeId == Comercial).Principal); // retirada perde o principal
        Assert.False(enviada.FinalidadesEnderecos.Single(u => u.FinalidadeId == Entrega).Principal);   // reativada volta sem principal
        Assert.Same(b, RegrasFinalidadeEndereco.EnderecoPrincipal(enviada, Fiscal));
    }

    [Fact]
    public void Endereco_desativado_no_estado_completo_perde_todos_os_principais()
    {
        var (gravada, enviada) = FichaGravada();
        enviada.Enderecos[0].Ativo = false;
        RegrasFinalidadeEndereco.Normalizar(enviada);

        Assert.Empty(RegrasFinalidadeEndereco.ValidarCompleto(enviada, gravada));
        Assert.Null(RegrasFinalidadeEndereco.EnderecoPrincipal(enviada, Fiscal));
    }

    // ---------------------------------------------------------------- Endereço de referência da listagem

    [Fact]
    public void Referencia_e_o_principal_da_finalidade_de_menor_ordem_e_o_fallback_nao_cria_principal()
    {
        // Ordens distintas: sem elas o desempate do fallback é pelo Id (Guid aleatório) e o teste falhava ~metade das vezes.
        var a = Endereco("1", cidade: "A", ordem: 0);
        var b = Endereco("2", cidade: "B", ordem: 1);
        var p = new Pessoa { Enderecos = [a, b], FinalidadesEnderecos = [Uso(a, Entrega, true), Uso(b, Comercial, true)] };

        Assert.Same(b, RegrasFinalidadeEndereco.EnderecoReferencia(p, Cadastro())); // Comercial tem ordem 1

        var semPrincipal = new Pessoa { Enderecos = [a, b], FinalidadesEnderecos = [Uso(a, Entrega), Uso(b, Entrega)] };
        Assert.Same(a, RegrasFinalidadeEndereco.EnderecoReferencia(semPrincipal, Cadastro())); // só exibição (1º ativo)
        Assert.All(semPrincipal.FinalidadesEnderecos, u => Assert.False(u.Principal));          // o fallback não vira principal
    }

    [Fact]
    public void Referencia_com_empate_de_ordem_e_deterministica_e_cidade_e_uf_sao_do_mesmo_endereco()
    {
        var a = Endereco("1", cidade: "A", uf: "MG");
        var b = Endereco("2", cidade: "B", uf: "SP");
        var usos = new List<PessoaEnderecoFinalidade> { Uso(a, Entrega, true), Uso(b, Comercial, true) };
        var cadastro = Cadastro(mesmaOrdem: true); // Entrega e Comercial com a mesma ordem

        var r1 = RegrasFinalidadeEndereco.EnderecoReferencia(new Pessoa { Enderecos = [a, b], FinalidadesEnderecos = usos }, cadastro);
        var r2 = RegrasFinalidadeEndereco.EnderecoReferencia(new Pessoa { Enderecos = [b, a], FinalidadesEnderecos = usos }, cadastro);

        Assert.Same(r1, r2); // a ordem da lista não muda o resultado
        var esperado = new SqlGuid(a.Id).CompareTo(new SqlGuid(b.Id)) < 0 ? a : b; // mesmo critério do SQL Server
        Assert.Same(esperado, r1);
        Assert.Equal((esperado.Cidade, esperado.Uf), (r1!.Cidade, r1.Uf)); // cidade e UF do mesmo endereço
    }

    [Fact]
    public void Sem_principal_a_referencia_e_o_primeiro_endereco_ativo()
    {
        var inativo = Endereco("1", ativo: false, ordem: 0);
        var segundo = Endereco("2", ordem: 2);
        var primeiro = Endereco("3", ordem: 1);
        var p = new Pessoa { Enderecos = [inativo, segundo, primeiro] };

        Assert.Same(primeiro, RegrasFinalidadeEndereco.EnderecoReferencia(p, Cadastro()));
    }

    // ---------------------------------------------------------------- Legado derivado

    [Fact]
    public void Coluna_legada_e_derivada_das_finalidades_ativas_e_da_referencia()
    {
        var a = Endereco("1");
        var b = Endereco("2");
        var p = new Pessoa { Enderecos = [a, b], FinalidadesEnderecos = [Uso(a, Entrega, true), Uso(a, Cobranca, ativo: false), Uso(b, Fiscal)] };
        b.Finalidades = FinalidadeEndereco.Principal | FinalidadeEndereco.Comercial; // lixo antigo: é sobrescrito

        RegrasFinalidadeEndereco.SincronizarLegado(p, a);

        Assert.Equal(FinalidadeEndereco.Entrega | FinalidadeEndereco.Principal, a.Finalidades);
        Assert.Equal(FinalidadeEndereco.Fiscal, b.Finalidades);
        Assert.Null(RegrasFinalidadeEndereco.EnderecoPrincipal(p, Fiscal)); // o bit não cria principal
    }

    // ---------------------------------------------------------------- Revisão deixada pela migração (I5)

    [Fact]
    public void Pendencias_sao_so_ambiguidade_e_motivos_da_migracao_nunca_endereco_sem_finalidade()
    {
        var a = Endereco("1");
        var b = Endereco("2");
        var semFinalidade = Endereco("3");
        var p = new Pessoa { Enderecos = [a, b, semFinalidade], FinalidadesEnderecos = [Uso(a, Fiscal), Uso(b, Fiscal), Uso(a, Cobranca, true), Uso(b, Cobranca)] };

        var pendencias = RegrasFinalidadeEndereco.PendenciasRevisao(p, id => id == Fiscal ? "Fiscal" : "Cobrança");

        Assert.Equal(RegrasFinalidadeEndereco.TextoAmbiguidade("Fiscal", 2), Assert.Single(pendencias));
        Assert.Contains("nenhum deles pôde ser identificado como principal a partir dos dados antigos", pendencias[0]);
    }

    [Fact]
    public void Motivos_da_migracao_so_desligam_e_a_marca_geral_sai_quando_tudo_foi_resolvido()
    {
        var antigoPrincipal = Endereco("1");
        antigoPrincipal.RevisaoMigracao = MotivoRevisaoEndereco.AntigoPrincipalSemFinalidade | MotivoRevisaoEndereco.AntigoPrincipalRepetido;
        var gravada = new Pessoa { RevisarFinalidadesEndereco = true, Enderecos = [antigoPrincipal] };

        // O aplicativo tenta LIGAR um motivo num endereço novo: não liga.
        var novo = Endereco("9");
        novo.RevisaoMigracao = MotivoRevisaoEndereco.AntigoPrincipalRepetido;
        var enviada = new Pessoa { RevisarFinalidadesEndereco = true, Enderecos = [Clonar(antigoPrincipal), novo] };
        RegrasFinalidadeEndereco.AtualizarRevisao(enviada, gravada);
        Assert.Equal(MotivoRevisaoEndereco.Nenhum, enviada.Enderecos[1].RevisaoMigracao);
        Assert.True(enviada.RevisarFinalidadesEndereco); // ainda há motivos

        // Ganhou finalidade: o motivo "sem finalidade" se resolve; o "repetido" só com a conferência do usuário.
        enviada.FinalidadesEnderecos.Add(Uso(enviada.Enderecos[0], Entrega, true));
        RegrasFinalidadeEndereco.AtualizarRevisao(enviada, gravada);
        Assert.Equal(MotivoRevisaoEndereco.AntigoPrincipalRepetido, enviada.Enderecos[0].RevisaoMigracao);
        Assert.True(enviada.RevisarFinalidadesEndereco);

        enviada.Enderecos[0].RevisaoMigracao = MotivoRevisaoEndereco.Nenhum; // "Marcar como revisado"
        RegrasFinalidadeEndereco.AtualizarRevisao(enviada, gravada);
        Assert.False(enviada.RevisarFinalidadesEndereco);
    }

    [Fact]
    public void Pessoa_sem_marca_nunca_ganha_marca_fora_da_migracao()
    {
        var a = Endereco("1");
        var b = Endereco("2");
        var gravada = new Pessoa { Enderecos = [a, b] };
        var enviada = new Pessoa { Enderecos = [a, b], FinalidadesEnderecos = [Uso(a, Fiscal), Uso(b, Fiscal)] }; // ambígua, mas não veio da migração

        RegrasFinalidadeEndereco.AtualizarRevisao(enviada, gravada);
        Assert.False(enviada.RevisarFinalidadesEndereco);
        RegrasFinalidadeEndereco.AtualizarRevisao(enviada, null);
        Assert.False(enviada.RevisarFinalidadesEndereco);
    }

    private static PessoaEndereco Clonar(PessoaEndereco e) => new()
    {
        Id = e.Id, Logradouro = e.Logradouro, Numero = e.Numero, Bairro = e.Bairro, Cep = e.Cep, Cidade = e.Cidade, Uf = e.Uf,
        MunicipioId = e.MunicipioId, Ativo = e.Ativo, RevisaoMigracao = e.RevisaoMigracao
    };

    // ---------------------------------------------------------------- Duplicidade física

    [Fact]
    public void Duplicata_nova_e_recusada_e_a_antiga_ja_gravada_so_e_apontada()
    {
        var gravadoA = Endereco("100");
        var gravadoB = Endereco("100"); // duplicado antigo
        var novo = Endereco("100");

        Assert.Empty(DuplicidadeEndereco.ErrosDeNovos([gravadoA, gravadoB], [gravadoA, gravadoB]));
        Assert.Single(DuplicidadeEndereco.ErrosDeNovos([gravadoA, novo], [gravadoA]));
        Assert.Single(DuplicidadeEndereco.Pares([gravadoA, gravadoB], incluirPossiveis: false));
    }

    [Fact]
    public void Possivel_duplicado_nao_e_tratado_como_igual()
    {
        var gravado = Endereco("100");
        var semCep = Endereco("100");
        semCep.Cep = null;

        Assert.Equal(SemelhancaEndereco.Possivel, DuplicidadeEndereco.Comparar(gravado, semCep));
        Assert.Empty(DuplicidadeEndereco.ErrosDeNovos([gravado, semCep], [gravado])); // a API só barra o "igual"
    }

    [Fact]
    public void Novo_igual_a_um_endereco_inativo_e_recusado_mas_reativar_o_existente_nao()
    {
        var inativo = Endereco("100", ativo: false);
        var novo = Endereco("100");

        var erros = DuplicidadeEndereco.ErrosDeNovos([inativo, novo], [inativo]);
        Assert.Contains(erros, e => e.Contains("porém ele está inativo"));

        var reativado = Endereco("100");
        reativado.Id = inativo.Id; // o mesmo registro, reativado
        var outroInativo = Endereco("100", ativo: false);
        Assert.Empty(DuplicidadeEndereco.ErrosDeNovos([reativado, outroInativo], [inativo, outroInativo]));
    }

    [Fact]
    public void Novo_igual_a_um_inativo_ja_consolidado_nao_aponta_o_consolidado()
    {
        var mantido = Endereco("100");
        var consolidado = Endereco("100", ativo: false);
        consolidado.MescladoEmId = mantido.Id;
        var novo = Endereco("100");

        var erros = DuplicidadeEndereco.ErrosDeNovos([mantido, consolidado, novo], [mantido, consolidado]);
        Assert.DoesNotContain(erros, e => e.Contains("inativo"));
        Assert.Contains(erros, e => e.Contains("já está cadastrado")); // aponta o ativo (mantido)
    }

    [Theory]
    [InlineData("PR 445", "Praça 445")]
    [InlineData("AL 101", "Alameda 101")]
    [InlineData("Est. do Mato", "Estrada do Mato")]
    public void Abreviacoes_ambiguas_nao_sao_expandidas(string a, string b) =>
        Assert.NotEqual(DuplicidadeEndereco.Logradouro(a), DuplicidadeEndereco.Logradouro(b));

    [Theory]
    [InlineData("R. das Flores", "Rua das Flores")]
    [InlineData("Av Brasil", "Avenida Brasil")]
    [InlineData("Rod. BR 040", "Rodovia BR 040")]
    public void Abreviacoes_seguras_sao_expandidas(string a, string b) =>
        Assert.Equal(DuplicidadeEndereco.Logradouro(a), DuplicidadeEndereco.Logradouro(b));

    [Theory]
    [InlineData("S/N", "")]
    [InlineData("s/nº", "SN")]
    [InlineData("100 A", "100a")]
    public void Numero_normalizado(string a, string b) =>
        Assert.Equal(DuplicidadeEndereco.Numero(a), DuplicidadeEndereco.Numero(b));

    [Fact]
    public void Consolidado_precisa_ficar_inativo_e_apontar_para_endereco_ativo()
    {
        var mantido = Endereco("1");
        var duplicado = Endereco("1");
        duplicado.MescladoEmId = mantido.Id;
        var p = new Pessoa { Enderecos = [mantido, duplicado] };

        Assert.NotEmpty(RegrasFinalidadeEndereco.ValidarConsolidacao(p)); // ainda ativo (reativar um consolidado é recusado)
        duplicado.Ativo = false;
        Assert.Empty(RegrasFinalidadeEndereco.ValidarConsolidacao(p));
    }

    // ---------------------------------------------------------------- Cadastro de finalidades (sistema)

    private static FinalidadeEnderecoCadastro Sistema() => new()
    {
        Id = Fiscal, Codigo = FinalidadesEnderecoIniciais.Fiscal, Nome = "Fiscal", Ordem = 3, DoSistema = true, Ativo = true
    };

    [Fact]
    public void Finalidade_de_sistema_nao_muda_de_codigo_nao_deixa_de_ser_de_sistema_e_nao_e_desativada()
    {
        var mudouCodigo = Sistema(); mudouCodigo.Codigo = "FISCAL2";
        var desativada = Sistema(); desativada.Ativo = false;
        var deixouDeSer = Sistema(); deixouDeSer.DoSistema = false;
        var renomeada = Sistema(); renomeada.Nome = "Endereço fiscal";

        Assert.NotEmpty(RegrasFinalidadeEnderecoCadastro.ValidarAlteracao(Sistema(), mudouCodigo));
        Assert.NotEmpty(RegrasFinalidadeEnderecoCadastro.ValidarAlteracao(Sistema(), desativada));
        Assert.NotEmpty(RegrasFinalidadeEnderecoCadastro.ValidarAlteracao(Sistema(), deixouDeSer));
        Assert.Empty(RegrasFinalidadeEnderecoCadastro.ValidarAlteracao(Sistema(), renomeada));
        Assert.NotEmpty(RegrasFinalidadeEnderecoCadastro.ValidarAlteracao(null, Sistema())); // código de sistema não é reusado
    }
}
