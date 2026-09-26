using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.Enderecos;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>
/// Endereço físico × finalidade na ficha: o mesmo endereço não é cadastrado de novo, as finalidades são acrescentadas
/// a ele, o principal é explícito por finalidade e os duplicados antigos só são consolidados com confirmação.
/// </summary>
public class EnderecoFinalidadesTests
{
    private const int Curvelo = 3120904;

    private static PessoaFormulario Ficha() => PessoaFormulario.NovaPessoa(finalidades: Finalidades.Cadastro);

    private static void RuaA(EnderecoFormulario e, string cep = "35790-000", string numero = "100", string bairro = "Centro",
                             string logradouro = "Rua A")
    {
        e.Cep = cep;
        e.Logradouro = logradouro;
        e.Numero = numero;
        e.Bairro = bairro;
        e.Municipio.Definir(Curvelo, "Curvelo", "MG");
    }

    private static EnderecoFormulario Novo(PessoaFormulario f, Action<EnderecoFormulario> preencher)
    {
        var e = new EnderecoFormulario();
        f.AdicionarEndereco(e);
        preencher(e);
        return e;
    }

    // 1 e 2 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Primeiro_endereco_e_segundo_diferente_convivem_sem_aviso()
    {
        var f = Ficha();
        RuaA(f.Enderecos[0]);
        f.Enderecos[0].AdicionarFinalidade(Finalidades.Residencial);
        var outro = Novo(f, e => RuaA(e, numero: "200"));

        Assert.Null(outro.IgualA);
        Assert.Equal(2, f.ParaDto().Enderecos.Count);
        Assert.DoesNotContain(f.ValidarLocalmente(), m => m.Contains("já está cadastrado"));
    }

    // 3, 4, 5 e 7 ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Mesmo_endereco_com_outra_finalidade_reutiliza_o_existente_sem_duplicar()
    {
        var f = Ficha();
        var existente = f.Enderecos[0];
        RuaA(existente);
        existente.AdicionarFinalidade(Finalidades.Residencial);

        var repetido = Novo(f, e => RuaA(e));
        repetido.AdicionarFinalidade(Finalidades.Cobranca);

        Assert.Same(existente, repetido.IgualA);
        Assert.False(repetido.DuplicidadePossivel);
        Assert.Contains("já está cadastrado", repetido.TextoDuplicidade);
        Assert.Contains(f.ValidarLocalmente(), m => m.Contains("já está cadastrado")); // não grava a duplicata

        repetido.UsarExistenteCommand.Execute(null);

        var endereco = Assert.Single(f.ParaDto().Enderecos);
        Assert.Equal(new[] { Finalidades.Residencial, Finalidades.Cobranca }.OrderBy(x => x),
                     endereco.Usos.Where(u => u.Ativo).Select(u => u.FinalidadeId).OrderBy(x => x));
        Assert.Contains("Cobrança", existente.AvisoFinalidade);
    }

    // 6 -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Mesma_finalidade_duas_vezes_no_mesmo_endereco_nao_duplica()
    {
        var f = Ficha();
        var e = f.Enderecos[0];
        e.AdicionarFinalidade(Finalidades.Residencial);

        e.AdicionarFinalidade(Finalidades.Residencial);

        Assert.Equal("Este endereço já possui esta finalidade.", e.AvisoFinalidade);
        Assert.Single(e.Finalidades);
    }

    [Fact]
    public void Mesmo_endereco_com_a_mesma_finalidade_usa_o_existente_e_avisa()
    {
        var f = Ficha();
        var existente = f.Enderecos[0];
        RuaA(existente);
        existente.AdicionarFinalidade(Finalidades.Residencial);
        var repetido = Novo(f, e => RuaA(e));
        repetido.AdicionarFinalidade(Finalidades.Residencial);

        repetido.UsarExistenteCommand.Execute(null);

        Assert.Single(f.Enderecos);
        Assert.Single(existente.Finalidades);
        Assert.Contains("Este endereço já possui: Residencial", existente.AvisoFinalidade);
    }

    // 8, 9 e reativação ---------------------------------------------------------------------------------------------

    [Fact]
    public void Retirar_finalidade_gravada_desativa_e_adicionar_de_novo_reativa_a_mesma_relacao()
    {
        var relacao = Guid.NewGuid();
        var dto = new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica,
            Enderecos = [new EnderecoDto { Id = Guid.NewGuid(), Logradouro = "Rua A", Usos = [new() { Id = relacao, FinalidadeId = Finalidades.Entrega, Principal = true }] }]
        };
        var f = PessoaFormulario.De(dto, finalidades: Finalidades.Cadastro);
        var e = f.Enderecos[0];
        var entrega = e.Finalidades[0];

        entrega.RemoverCommand.Execute(null);                       // remover a finalidade
        var retirada = Assert.Single(f.ParaDto().Enderecos[0].Usos);
        Assert.False(retirada.Ativo);
        Assert.False(retirada.Principal);

        e.AdicionarFinalidade(Finalidades.Entrega);                 // alterar de novo: volta a mesma relação
        var reativada = Assert.Single(f.ParaDto().Enderecos[0].Usos);
        Assert.Equal(relacao, reativada.Id);
        Assert.True(reativada.Ativo);
        Assert.False(reativada.Principal);                          // sem principalidade antiga
    }

    // 10 e 11 -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Remover_e_editar_endereco()
    {
        var f = Ficha();
        RuaA(f.Enderecos[0]);
        var segundo = Novo(f, e => RuaA(e, numero: "200"));

        segundo.Numero = "100";            // editar: ficou igual ao primeiro
        Assert.Same(f.Enderecos[0], segundo.IgualA);
        segundo.Numero = "300";            // editar de novo: outro endereço
        Assert.Null(segundo.IgualA);

        segundo.RemoverCommand.Execute(null);
        Assert.Single(f.Enderecos);
    }

    // 12 ------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Principal_por_finalidade_e_explicito_e_um_endereco_pode_ser_principal_de_varias()
    {
        var f = Ficha();
        var a = f.Enderecos[0];
        RuaA(a);
        var b = Novo(f, e => RuaA(e, numero: "200"));
        var entregaA = a.AdicionarFinalidade(Finalidades.Entrega)!;
        var cobrancaA = a.AdicionarFinalidade(Finalidades.Cobranca)!;
        var entregaB = b.AdicionarFinalidade(Finalidades.Entrega)!;
        Assert.False(entregaA.Principal); // nada vira principal sozinho, nem pela ordem

        await f.AlternarPrincipalAsync(a, entregaA);
        await f.AlternarPrincipalAsync(a, cobrancaA);
        f.Confirmar = (_, _, _, _) => Task.FromResult(false); // o usuário cancela a troca
        await f.AlternarPrincipalAsync(b, entregaB);

        Assert.True(entregaA.Principal);
        Assert.True(cobrancaA.Principal);
        Assert.False(entregaB.Principal);
        var usos = f.ParaDto().Enderecos.SelectMany(e => e.Usos).Where(u => u.Principal).ToList();
        Assert.Equal(2, usos.Count);
        Assert.Single(usos, u => u.FinalidadeId == Finalidades.Entrega);
    }

    // 13, 14 e 15 ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("35790000", "100", "Centro", "Rua A")]            // CEP sem máscara
    [InlineData("35790-000", " 100 ", "  centro ", "RUA   A")]    // espaços e maiúsculas
    [InlineData("35.790-000", "100", "CENTRO", "R. A")]           // pontuação e abreviação segura
    public void Formatacoes_diferentes_do_mesmo_endereco_sao_reconhecidas(string cep, string numero, string bairro, string logradouro)
    {
        var f = Ficha();
        RuaA(f.Enderecos[0]);
        var repetido = Novo(f, e => RuaA(e, cep, numero, bairro, logradouro));

        Assert.Same(f.Enderecos[0], repetido.IgualA);
        Assert.False(repetido.DuplicidadePossivel);
    }

    // 16 ------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("35790-000", "101", "Centro", "Rua A")]           // outro número
    [InlineData("35790-000", "100", "Vila Nova", "Rua A")]        // outro bairro
    [InlineData("35790-000", "100", "Centro", "Rua B")]           // outra rua
    [InlineData("35790-001", "100", "Centro", "Rua A")]           // outro CEP
    public void Enderecos_realmente_diferentes_nao_sao_duplicados(string cep, string numero, string bairro, string logradouro)
    {
        var f = Ficha();
        RuaA(f.Enderecos[0]);
        var outro = Novo(f, e => RuaA(e, cep, numero, bairro, logradouro));

        Assert.Null(outro.IgualA);
    }

    [Fact]
    public void Sem_cep_nem_bairro_e_so_possivel_e_o_usuario_pode_dizer_que_e_outro()
    {
        var f = Ficha();
        RuaA(f.Enderecos[0]);
        var talvez = Novo(f, e => RuaA(e, cep: "", bairro: ""));

        Assert.True(talvez.DuplicidadeIncerta);
        talvez.ContinuarComOutroCommand.Execute(null);

        Assert.Null(talvez.IgualA);
        Assert.DoesNotContain(f.ValidarLocalmente(), m => m.Contains("parece o mesmo"));
    }

    // Consolidação assistida de duplicados já gravados -------------------------------------------------------------

    private static PessoaFormulario ComDuplicadosGravados(out Guid a, out Guid b)
    {
        a = Guid.NewGuid();
        b = Guid.NewGuid();
        EnderecoDto Rua(Guid id, Guid finalidade, bool principal) => new()
        {
            Id = id, Logradouro = "Rua X", Numero = "100", Bairro = "Centro", Cep = "35790000", MunicipioId = Curvelo, Cidade = "Curvelo", Uf = "MG",
            Usos = [new() { Id = Guid.NewGuid(), FinalidadeId = finalidade, Principal = principal }]
        };
        var dto = new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica,
            Enderecos = [Rua(a, Finalidades.Residencial, false), Rua(b, Finalidades.Cobranca, true)]
        };
        return PessoaFormulario.De(dto, finalidades: Finalidades.Cadastro);
    }

    [Fact]
    public async Task Consolidacao_mostra_a_previa_e_so_pede_ao_servidor_a_intencao_sem_mudar_a_ficha()
    {
        var f = ComDuplicadosGravados(out var a, out var b);
        var par = Assert.Single(f.DuplicadosGravados);
        string? resumo = null;
        (Guid Origem, Guid Destino)? pedido = null;
        f.Confirmar = (_, mensagem, _, _) => { resumo = mensagem; return Task.FromResult(true); };
        f.ConsolidarNoServidor = (origem, destino) => { pedido = (origem, destino); return Task.CompletedTask; };

        await par.ManterPrimeiroCommand.ExecuteAsync(null);

        Assert.Contains("Cobrança (principal)", resumo);             // o usuário vê como deve ficar antes
        Assert.Equal((b, a), pedido);                                // só a intenção: consolidar B em A
        var dto = f.ParaDto();                                       // a ficha não decide nada sozinha
        Assert.All(dto.Enderecos, e => Assert.True(e.Ativo));
        Assert.All(dto.Enderecos, e => Assert.Null(e.MescladoEmId));
    }

    [Fact]
    public async Task Consolidacao_cancelada_nao_chama_o_servidor_e_manter_separados_tira_o_aviso()
    {
        var f = ComDuplicadosGravados(out _, out _);
        var chamou = false;
        f.Confirmar = (_, _, _, _) => Task.FromResult(false);
        f.ConsolidarNoServidor = (_, _) => { chamou = true; return Task.CompletedTask; };

        await f.DuplicadosGravados[0].ManterSegundoCommand.ExecuteAsync(null);
        Assert.False(chamou);

        f.DuplicadosGravados[0].ManterSeparadosCommand.Execute(null);
        Assert.Empty(f.DuplicadosGravados);
    }

    [Fact]
    public async Task Consolidacao_com_alteracoes_nao_salvas_nao_pergunta_nem_chama_o_servidor()
    {
        var f = ComDuplicadosGravados(out _, out _);
        var perguntou = false;
        var chamou = false;
        f.TemAlteracoesNaoSalvas = () => true;
        f.Confirmar = (_, _, _, _) => { perguntou = true; return Task.FromResult(true); };
        f.ConsolidarNoServidor = (_, _) => { chamou = true; return Task.CompletedTask; };

        await f.DuplicadosGravados[0].ManterPrimeiroCommand.ExecuteAsync(null);

        Assert.False(perguntou);
        Assert.False(chamou);
    }

    // Endereço igual a um INATIVO já gravado ---------------------------------------------------------------------------

    private static PessoaFormulario ComInativoGravado(out Guid inativoId, bool consolidado = false)
    {
        inativoId = Guid.NewGuid();
        var mantidoId = Guid.NewGuid();
        var dto = new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica,
            Enderecos =
            [
                new EnderecoDto
                {
                    Id = inativoId, Ativo = false, MescladoEmId = consolidado ? mantidoId : null, Logradouro = "Rua X", Numero = "100",
                    Bairro = "Centro", Cep = "35790000", MunicipioId = Curvelo, Cidade = "Curvelo", Uf = "MG",
                    Usos = [new() { Id = Guid.NewGuid(), FinalidadeId = Finalidades.Entrega }]
                },
                new EnderecoDto
                {
                    Id = mantidoId, Logradouro = "Rua Y", Numero = "1", Bairro = "Centro", Cep = "35790000", MunicipioId = Curvelo,
                    Cidade = "Curvelo", Uf = "MG"
                }
            ]
        };
        return PessoaFormulario.De(dto, finalidades: Finalidades.Cadastro);
    }

    [Fact]
    public void Igual_a_um_inativo_oferece_reativar_o_existente_sem_criar_outra_linha()
    {
        var f = ComInativoGravado(out var inativoId);
        var novo = Novo(f, e => RuaA(e, logradouro: "Rua X"));
        novo.AdicionarFinalidade(Finalidades.Cobranca);

        Assert.True(novo.IgualAInativo);
        Assert.Equal("Reativar endereço existente", novo.TextoBotaoUsarExistente);
        Assert.Contains("porém ele está inativo", novo.TextoDuplicidade);

        novo.UsarExistenteCommand.Execute(null);

        var dto = f.ParaDto();
        Assert.Equal(2, dto.Enderecos.Count);                                  // nenhuma linha física nova
        var reativado = dto.Enderecos.Single(e => e.Id == inativoId);
        Assert.True(reativado.Ativo);
        Assert.Contains(reativado.Usos, u => u.FinalidadeId == Finalidades.Cobranca && u.Ativo);
        Assert.All(reativado.Usos, u => Assert.False(u.Principal));            // reativado volta sem principal
    }

    [Fact]
    public void Igual_a_um_inativo_cancelar_tira_o_novo_da_ficha()
    {
        var f = ComInativoGravado(out _);
        var novo = Novo(f, e => RuaA(e, logradouro: "Rua X"));

        novo.CancelarNovoCommand.Execute(null);

        Assert.Equal(2, f.Enderecos.Count);
        Assert.DoesNotContain(novo, f.Enderecos);
    }

    [Fact]
    public void Consolidado_nao_pode_ser_reativado_nem_e_oferecido_para_reuso()
    {
        var f = ComInativoGravado(out var inativoId, consolidado: true);
        var consolidado = f.Enderecos.Single(e => e.Id == inativoId);

        Assert.False(consolidado.PodeReativar);
        consolidado.ReativarCommand.Execute(null);
        Assert.False(consolidado.Ativo);

        var novo = Novo(f, e => RuaA(e, logradouro: "Rua X"));
        Assert.Null(novo.IgualA);
    }

    // Consulta de CNPJ nunca escolhe principal -------------------------------------------------------------------------

    [Fact]
    public void Aplicar_CNPJ_acrescenta_Fiscal_mas_nao_cria_principal_nem_no_unico_endereco()
    {
        var f = Ficha();
        f.Natureza = Lone.Cliente.ViewModels.Comum.Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        var dados = new Lone.Contracts.Integracoes.DadosCnpj
        {
            Cnpj = "11222333000181", RazaoSocial = "Minha Empresa Ltda", Cep = "35790000", Logradouro = "Rua A", Numero = "100",
            Bairro = "Centro", Cidade = "Curvelo", Uf = "MG", CodigoMunicipioIbge = "3120904", Fonte = "BrasilAPI"
        };

        f.AplicarCnpj(f.Principal, dados);

        var endereco = Assert.Single(f.Enderecos);                             // o único endereço (em branco) recebeu o CNPJ
        var fiscal = Assert.Single(endereco.Finalidades, x => x.FinalidadeId == Finalidades.Fiscal);
        Assert.False(fiscal.Principal);                                        // principal só por ação do usuário
        Assert.Contains("Tornar principal", endereco.AvisoFinalidade);         // a sugestão exige a ação explícita
    }

    [Fact]
    public void Aplicar_CNPJ_nao_sobrescreve_outro_lugar_nem_mexe_nos_principais_existentes()
    {
        var f = Ficha();
        f.Natureza = Lone.Cliente.ViewModels.Comum.Opcao.De(OpcoesPessoa.Naturezas, NaturezaPessoa.Juridica);
        var residencial = f.Enderecos[0];
        RuaA(residencial, numero: "999", logradouro: "Rua Casa");
        var principalResidencial = residencial.AdicionarFinalidade(Finalidades.Residencial)!;
        principalResidencial.Principal = true;
        var dados = new Lone.Contracts.Integracoes.DadosCnpj
        {
            Cnpj = "11222333000181", RazaoSocial = "Minha Empresa Ltda", Cep = "35790000", Logradouro = "Rua A", Numero = "100",
            Bairro = "Centro", Cidade = "Curvelo", Uf = "MG", CodigoMunicipioIbge = "3120904", Fonte = "BrasilAPI"
        };

        f.AplicarCnpj(f.Principal, dados);

        Assert.Equal("Rua Casa", residencial.Logradouro);                      // o residencial não virou o endereço do CNPJ
        Assert.True(principalResidencial.Principal);
        Assert.DoesNotContain(residencial.Finalidades, x => x.FinalidadeId == Finalidades.Fiscal);
        var doCnpj = Assert.Single(f.Enderecos, e => !ReferenceEquals(e, residencial));
        Assert.False(Assert.Single(doCnpj.Finalidades, x => x.FinalidadeId == Finalidades.Fiscal).Principal);
    }

    // Revisão vinda da migração -------------------------------------------------------------------------------------

    [Fact]
    public async Task Revisao_mostra_o_motivo_por_finalidade_e_some_quando_resolvida()
    {
        EnderecoDto Rua(string numero) => new()
        {
            Id = Guid.NewGuid(), Logradouro = "Rua X", Numero = numero,
            Usos = [new() { Id = Guid.NewGuid(), FinalidadeId = Finalidades.Entrega }]
        };
        var dto = new PessoaDto
        {
            Id = Guid.NewGuid(), Nome = "Ana", Natureza = NaturezaPessoa.Fisica, RevisarFinalidadesEndereco = true,
            Enderecos = [Rua("1"), Rua("2")]
        };
        var f = PessoaFormulario.De(dto, finalidades: Finalidades.Cadastro);

        Assert.Equal(Lone.Domain.Enderecos.RegrasFinalidadeEndereco.TextoAmbiguidade("Entrega", 2), Assert.Single(f.PendenciasRevisao));

        await f.AlternarPrincipalAsync(f.Enderecos[0], f.Enderecos[0].Finalidades[0]);

        Assert.Empty(f.PendenciasRevisao);
    }
}
