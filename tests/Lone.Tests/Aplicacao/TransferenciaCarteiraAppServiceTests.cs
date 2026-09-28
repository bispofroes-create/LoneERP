using Lone.Application.Comercial;
using Lone.Application.Papeis;
using Lone.Application.Pessoas;
using Lone.Application.Seguranca;
using Lone.Contracts.Comercial;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Aplicacao;

/// <summary>
/// Transferência de carteira pelo serviço (Motor Comercial, Fase 1d): a prévia não grava nada; a gravação registra a
/// transferência, grava cada cliente por si (um conflito não desfaz os outros), deixa a frase no histórico e o motivo na
/// auditoria, e confere as regras da ficha ("Quem pode ser") cliente a cliente.
/// </summary>
public class TransferenciaCarteiraAppServiceTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 28);
    private static readonly DateOnly Efeito = new(2026, 10, 1);
    private static readonly Guid ClassificacaoVendedor = Guid.NewGuid();

    private readonly TipoCarteira _vendedor;
    private readonly Guid _joao = Guid.NewGuid();
    private readonly Guid _maria = Guid.NewGuid();
    private readonly Guid _pedro = Guid.NewGuid();

    private readonly PessoasEmMemoria _pessoas = new();
    private readonly TransferenciasEmMemoria _transferencias;
    private readonly ConsultasFixas _consultas = new();
    private readonly CoberturasFixas _coberturas = new();
    private readonly AutorizacaoFixa _autorizacao = new();
    private readonly MotivoEmMemoria _motivo = new();
    private readonly TransferenciaCarteiraAppService _servico;

    public TransferenciaCarteiraAppServiceTests()
    {
        _vendedor = new TipoCarteira
        {
            Id = Guid.NewGuid(), Nome = "Vendedor", ResponsavelDaConta = true, LimitePorVez = 1, TipoCredito = TipoCreditoComercial.Receita,
            Classificacoes = [new TipoCarteiraClassificacao { Id = Guid.NewGuid(), PapelId = ClassificacaoVendedor }]
        };
        _transferencias = new TransferenciasEmMemoria(_pessoas);
        _consultas.Pessoas[_joao] = new PessoaElegivel("João", new HashSet<Guid> { ClassificacaoVendedor });
        _consultas.Pessoas[_maria] = new PessoaElegivel("Maria", new HashSet<Guid> { ClassificacaoVendedor });
        _consultas.Pessoas[_pedro] = new PessoaElegivel("Pedro", new HashSet<Guid> { ClassificacaoVendedor });

        var tipos = new TiposFixos(_vendedor);
        var referencias = new ReferenciasComercial(new PerfisVazios(), new CondicoesVazias(), tipos, _consultas, new ClassificacoesFixas());
        _servico = new TransferenciaCarteiraAppService(_transferencias, _pessoas, referencias, _consultas, new ParametrosPadrao(), _coberturas,
            new Lone.Tests.Apoio.EmpresasFixas(), _autorizacao, _motivo, new UsuarioFixo(), new FakeTimeProvider(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero)));
    }

    private Pessoa Cliente(string nome, DateOnly inicioDoJoao)
    {
        var p = new Pessoa { Id = Guid.NewGuid(), Nome = nome };
        p.Carteira.Add(new CarteiraCliente
        {
            Id = Guid.NewGuid(), PessoaId = p.Id, TipoCarteiraId = _vendedor.Id, VendedorId = _joao, InicioEm = inicioDoJoao
        });
        _pessoas.Gravadas[p.Id] = p;
        _consultas.Nomes[p.Id] = nome;
        return p;
    }

    private TransferenciaRequisicao Pedido(params Guid[] destinos) => new()
    {
        OrigemId = _joao, TipoCarteiraId = _vendedor.Id, EfeitoEm = Efeito, Destinos = [.. destinos], Motivo = "Desligamento"
    };

    [Fact]
    public async Task Cada_cliente_vale_por_si_e_o_resultado_fica_registrado()
    {
        var abc = Cliente("ABC", new DateOnly(2025, 1, 1));
        var futuro = Cliente("Futuro", Efeito.AddDays(5));      // começa depois do efeito: fica como está
        var disputado = Cliente("XYZ", new DateOnly(2025, 1, 1));
        _pessoas.Conflito.Add(disputado.Id);                      // outro usuário gravou antes

        var resultado = await _servico.TransferirAsync(Pedido(_maria));

        Assert.Equal("TR-2026-0001", resultado.Numero);
        Assert.True(resultado.Concluida);
        Assert.Equal((1, 1, 1), (resultado.Transferidos, resultado.NaoProcessados, resultado.Erros));
        Assert.Equal(3, resultado.Itens.Count);
        Assert.Contains(resultado.Itens, i => i.Cliente == "XYZ" && i.Resultado == ResultadoItemTransferencia.Erro);
        Assert.Contains(resultado.Itens, i => i.Cliente == "Futuro" && i.Resultado == ResultadoItemTransferencia.NaoProcessado);

        var gravado = _pessoas.Gravadas[abc.Id];
        Assert.Equal(2, gravado.Carteira.Count);                                        // nada apagado
        Assert.Equal(Efeito.AddDays(-1), gravado.Carteira.Single(c => c.VendedorId == _joao).FimEm);
        var novo = gravado.Carteira.Single(c => c.VendedorId == _maria);
        Assert.Equal(OrigemVinculoCarteira.Transferencia, novo.Origem);
        Assert.Equal(resultado.Id, novo.TransferenciaId);
        Assert.Contains(_pessoas.Eventos[abc.Id], e => e.Contains("transferido para Maria a partir de 01/10/2026 (TR-2026-0001: Desligamento)"));
        Assert.Equal("TR-2026-0001: Desligamento", _motivo.Motivo);

        Assert.False(_pessoas.Eventos.ContainsKey(futuro.Id));   // nada gravado nele
        Assert.Null(_pessoas.Gravadas[disputado.Id].Carteira.Single().FimEm);
    }

    [Fact]
    public async Task Previa_nao_grava_nada_divide_pela_menor_carteira_e_avisa_da_ausencia_da_origem()
    {
        Cliente("A", new DateOnly(2025, 1, 1));
        Cliente("B", new DateOnly(2025, 1, 1));
        Cliente("C", new DateOnly(2025, 1, 1));
        _transferencias.Cargas[_maria] = 1;
        _coberturas.DoTitular.Add(new CoberturaComercial
        {
            Id = Guid.NewGuid(), TitularId = _joao, SubstitutoId = _pedro, TipoAusenciaId = Guid.NewGuid(),
            InicioEm = Efeito.AddDays(10), FimEm = Efeito.AddDays(20)
        });

        var previa = await _servico.PreviaAsync(Pedido(_maria, _pedro));

        Assert.Equal(3, previa.Transferir);
        Assert.Equal(0, previa.NaoProcessar);
        Assert.Equal(2, previa.PorDestino.Single(d => d.DestinoId == _pedro).Clientes); // Pedro tinha 0, Maria 1
        Assert.Equal(1, previa.PorDestino.Single(d => d.DestinoId == _maria).Clientes);
        Assert.Contains(previa.Avisos, a => a.Contains("ausência cadastrada"));
        Assert.Empty(_pessoas.Eventos);
        Assert.Empty(_transferencias.Gravadas);
        Assert.All(_pessoas.Gravadas.Values, p => Assert.Null(p.Carteira.Single().FimEm));
    }

    [Fact]
    public async Task Destino_fora_do_quem_pode_ser_nao_e_processado_com_o_motivo_da_ficha()
    {
        Cliente("ABC", new DateOnly(2025, 1, 1));
        _consultas.Pessoas[_pedro] = new PessoaElegivel("Pedro", new HashSet<Guid>()); // sem a classificação aceita

        var previa = await _servico.PreviaAsync(Pedido(_pedro));

        var item = Assert.Single(previa.Itens);
        Assert.Equal(ResultadoItemTransferencia.NaoProcessado, item.Resultado);
        Assert.Contains("não pode ser \"Vendedor\"", item.Motivo);
    }

    [Fact]
    public async Task Sem_permissao_nao_ve_nem_transfere_e_sem_clientes_explica()
    {
        _autorizacao.Negadas.Add(Permissoes.Comercial.Transferir);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => _servico.PreviaAsync(Pedido(_maria)));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => _servico.ListarAsync());

        _autorizacao.Negadas.Clear();
        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => _servico.PreviaAsync(Pedido(_maria)));
        Assert.Contains("Nenhum cliente a transferir", Assert.Single(erro.Erros));
    }

    [Fact]
    public async Task Opcoes_trazem_quem_pode_atender_com_a_carteira_de_hoje_e_o_limite_de_dias()
    {
        _transferencias.Cargas[_joao] = 42;

        var opcoes = await _servico.ListarOpcoesAsync();

        Assert.Equal(new[] { "João", "Maria", "Pedro" }, opcoes.Pessoas.Select(p => p.Nome).ToArray());
        Assert.Equal(42, opcoes.ClientesHoje[_joao]);
        Assert.Equal("Vendedor", Assert.Single(opcoes.Papeis).Nome);
        Assert.Equal(30, opcoes.DiasRetroativosMaximo);
    }

    [Fact]
    public async Task Carteira_em_uma_data_responde_pelo_historico_com_credito_transferencia_e_ausencia()
    {
        var abc = Cliente("ABC", new DateOnly(2025, 1, 1));
        await _servico.TransferirAsync(Pedido(_maria));
        _coberturas.DoTitular.Add(new CoberturaComercial
        {
            Id = Guid.NewGuid(), TitularId = _maria, SubstitutoId = _pedro, TipoAusenciaId = Ferias,
            InicioEm = Efeito.AddDays(2), FimEm = Efeito.AddDays(10)
        });
        var consulta = new CarteiraEmDataAppService(new CarteiraEmMemoria(_pessoas), _coberturas, new TiposAusenciaFixos(), _transferencias,
            new ReferenciasComercial(new PerfisVazios(), new CondicoesVazias(), new TiposFixos(_vendedor), _consultas, new ClassificacoesFixas()),
            _consultas, _autorizacao);

        var antes = Assert.Single((await consulta.ConsultarAsync(abc.Id, null, new DateOnly(2026, 9, 15))).Vinculos);
        Assert.Equal("João", antes.Pessoa);
        Assert.Equal(100, antes.Credito);
        Assert.Null(antes.Transferencia);

        var depois = Assert.Single((await consulta.ConsultarAsync(abc.Id, null, Efeito.AddDays(4))).Vinculos);
        Assert.Equal("Maria", depois.Pessoa);
        Assert.Equal(OrigemVinculoCarteira.Transferencia, depois.Origem);
        Assert.Equal("TR-2026-0001", depois.Transferencia);
        Assert.StartsWith("Férias de 03/10/2026 a 11/10/2026 · atendimento por Pedro", depois.Cobertura);

        var daMaria = await consulta.ConsultarAsync(null, _maria, Efeito.AddDays(4));
        Assert.Equal("ABC", Assert.Single(daMaria.Vinculos).Cliente);
        Assert.Single(daMaria.Ausencias);
        Assert.Empty((await consulta.ConsultarAsync(null, _maria, Efeito.AddDays(-1))).Vinculos); // antes do efeito, não atendia

        await Assert.ThrowsAsync<ValidacaoException>(() => consulta.ConsultarAsync(abc.Id, _maria, Efeito));
    }

    // ---------------------------------------------------------------- Apoio

    private static readonly Guid Ferias = Guid.NewGuid();

    private sealed class CarteiraEmMemoria : ICoberturaConsultas
    {
        private readonly PessoasEmMemoria _pessoas;
        public CarteiraEmMemoria(PessoasEmMemoria pessoas) => _pessoas = pessoas;

        public Task<List<CarteiraCliente>> VinculosEmDataAsync(Guid? clienteId, Guid? pessoaId, DateOnly data, int limite, CancellationToken ct) =>
            Task.FromResult(_pessoas.Gravadas.Values.SelectMany(p => p.Carteira)
                .Where(v => v.Vigente(data) && (clienteId == null || v.PessoaId == clienteId) && (pessoaId == null || v.VendedorId == pessoaId))
                .Take(limite).ToList());

        public Task<Dictionary<Guid, int>> ContarClientesAsync(IReadOnlyCollection<CoberturaComercial> coberturas, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<List<VinculoVencendoDto>> CarteiraVencendoAsync(DateOnly de, DateOnly ate, CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<Guid, string>> NomesEquipesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class TiposAusenciaFixos : ITipoAusenciaRepositorio
    {
        public Task<List<TipoAusencia>> ListarAsync(CancellationToken ct) =>
            Task.FromResult(new List<TipoAusencia> { new() { Id = Ferias, Nome = "Férias", Ordem = 1 } });
        public Task<TipoAusencia?> ObterAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct) => throw new NotImplementedException();
        public Task SalvarAsync(TipoAusencia item, bool novo, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class PessoasEmMemoria : IPessoaRepositorio
    {
        public Dictionary<Guid, Pessoa> Gravadas { get; } = new();
        public Dictionary<Guid, List<string>> Eventos { get; } = new();
        public HashSet<Guid> Conflito { get; } = new();

        /// <summary>Como o banco: cada leitura devolve outra instância.</summary>
        private static Pessoa Copia(Pessoa p)
        {
            var copia = RegrasTransferencia.ComoEstava(p);
            copia.Versao = p.Versao;
            return copia;
        }

        public Task<Pessoa?> ObterAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Gravadas.TryGetValue(id, out var p) ? Copia(p) : null);

        public Task SalvarAsync(Pessoa pessoa, bool nova, OrigemAlteracao origem, CancellationToken ct)
        {
            if (Conflito.Contains(pessoa.Id)) throw new ConflitoDeEdicaoException();
            Eventos[pessoa.Id] = [.. pessoa.RetirarEventos()];
            Gravadas[pessoa.Id] = Copia(pessoa);
            return Task.CompletedTask;
        }

        public Task<List<PessoaResumo>> ListarAsync(FiltroPessoas filtro, CancellationToken ct) => throw new NotImplementedException();
        public Task<PaginaListaPessoas> ListarPaginaAsync(FiltroPessoas filtro, int pagina, int tamanho, CancellationToken ct) => throw new NotImplementedException();
        public Task<PaginaListaPessoas> ListarPaginaAsync(FiltroPessoas filtro, IReadOnlyList<CondicaoFiltro> condicoes, DateOnly hoje,
                                                          int pagina, int tamanho, CancellationToken ct) => throw new NotImplementedException();
        public Task<PaginaListaPessoas> ListarPaginaAsync(FiltroPessoas filtro, IReadOnlyList<CondicaoFiltro> condicoes,
                                                          IReadOnlyList<string> colunas, OrdenacaoLista? ordenacao, DateOnly hoje,
                                                          int pagina, int tamanho, CancellationToken ct) => throw new NotImplementedException();
        public Task<int> ContarClientesAtivosAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<List<DateOnly?>> ListarNascimentosAsync(TipoPapel? papel, CancellationToken ct) => throw new NotImplementedException();
        public Task<PessoaIdentificacao?> BuscarPorDocumentoAsync(NaturezaPessoa natureza, string documento, Guid ignorarId, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<List<PessoaIdentificacao>> BuscarSemelhantesAsync(Guid ignorarId, string nome, IReadOnlyCollection<string> contatos, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<List<PendenciaMunicipio>> ListarPendenciasMunicipioAsync(Guid pessoaId, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class TransferenciasEmMemoria : ITransferenciaCarteiraRepositorio
    {
        private readonly PessoasEmMemoria _pessoas;
        public TransferenciasEmMemoria(PessoasEmMemoria pessoas) => _pessoas = pessoas;

        public Dictionary<Guid, TransferenciaCarteira> Gravadas { get; } = new();
        public List<TransferenciaCarteiraItem> Itens { get; } = new();
        public Dictionary<Guid, int> Cargas { get; } = new();

        public Task<List<ClienteDaOrigem>> ClientesDaOrigemAsync(FiltroTransferencia filtro, CancellationToken ct) =>
            Task.FromResult(_pessoas.Gravadas.Values
                .Where(p => RegrasTransferencia.Candidatos(p.Carteira, filtro).Any())
                .OrderBy(p => p.Nome).Select(p => new ClienteDaOrigem(p.Id, p.Nome)).ToList());

        public Task<Dictionary<Guid, int>> CargasAsync(IReadOnlyCollection<Guid> pessoas, Guid? tipoCarteiraId, DateOnly data, CancellationToken ct) =>
            Task.FromResult(Cargas.Where(c => pessoas.Contains(c.Key)).ToDictionary(c => c.Key, c => c.Value));

        public Task<int> ProximaSequenciaAsync(int ano, CancellationToken ct) =>
            Task.FromResult(Gravadas.Values.Where(t => t.Ano == ano).Select(t => t.Sequencia).DefaultIfEmpty(0).Max() + 1);

        public Task IncluirAsync(TransferenciaCarteira transferencia, CancellationToken ct)
        {
            transferencia.RetirarEventos();
            Gravadas[transferencia.Id] = transferencia;
            return Task.CompletedTask;
        }

        public Task ConcluirAsync(TransferenciaCarteira transferencia, IReadOnlyList<TransferenciaCarteiraItem> itens, CancellationToken ct)
        {
            Gravadas[transferencia.Id] = transferencia;
            Itens.AddRange(itens);
            return Task.CompletedTask;
        }

        public Task<List<TransferenciaCarteira>> ListarAsync(int limite, CancellationToken ct) => Task.FromResult(Gravadas.Values.ToList());

        public Task<TransferenciaCarteira?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(Gravadas.GetValueOrDefault(id));

        public Task<List<TransferenciaCarteiraItem>> ItensAsync(Guid transferenciaId, CancellationToken ct) =>
            Task.FromResult(Itens.Where(i => i.TransferenciaId == transferenciaId).ToList());

        public Task<Dictionary<Guid, string>> NumerosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult(Gravadas.Values.Where(t => ids.Contains(t.Id)).ToDictionary(t => t.Id, t => t.Numero));
    }

    private sealed class ConsultasFixas : IComercialConsultas
    {
        public Dictionary<Guid, PessoaElegivel> Pessoas { get; } = new();
        public Dictionary<Guid, string> Nomes { get; } = new();

        public Task<List<AtendenteOpcaoDto>> ListarAtendentesAsync(IReadOnlyCollection<Guid> classificacoes, CancellationToken ct) =>
            Task.FromResult(Pessoas.Where(p => p.Value.Classificacoes.Any(classificacoes.Contains))
                .Select(p => new AtendenteOpcaoDto(p.Key, p.Value.Nome, [.. p.Value.Classificacoes])).OrderBy(p => p.Nome).ToList());

        public Task<Dictionary<Guid, PessoaElegivel>> PessoasElegiveisAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult(Pessoas.Where(p => ids.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value));

        public Task<Dictionary<Guid, string>> NomesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult(ids.Distinct()
                .Select(id => (id, nome: Pessoas.TryGetValue(id, out var p) ? p.Nome : Nomes.GetValueOrDefault(id)))
                .Where(x => x.nome is not null).ToDictionary(x => x.id, x => x.nome!));
    }

    private sealed class TiposFixos : ITipoCarteiraRepositorio
    {
        private readonly TipoCarteira _tipo;
        public TiposFixos(TipoCarteira tipo) => _tipo = tipo;
        public Task<List<TipoCarteira>> ListarAsync(CancellationToken ct) => Task.FromResult(new List<TipoCarteira> { _tipo });
        public Task<TipoCarteira?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult(id == _tipo.Id ? _tipo : null);
        public Task<Dictionary<Guid, TipoCarteira>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct) => throw new NotImplementedException();
        public Task SalvarAsync(TipoCarteira item, bool novo, CancellationToken ct) => throw new NotImplementedException();
        public Task<List<CarteiraCliente>> VinculosAtivosAsync(Guid tipoId, DateOnly desde, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class PerfisVazios : IPerfilComercialRepositorio
    {
        public Task<List<PerfilComercial>> ListarAsync(CancellationToken ct) => Task.FromResult(new List<PerfilComercial>());
        public Task<PerfilComercial?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult<PerfilComercial?>(null);
        public Task<Dictionary<Guid, PerfilComercial>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult(new Dictionary<Guid, PerfilComercial>());
        public Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct) => throw new NotImplementedException();
        public Task SalvarAsync(PerfilComercial item, bool novo, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class CondicoesVazias : ICondicaoPagamentoRepositorio
    {
        public Task<List<CondicaoPagamento>> ListarAsync(CancellationToken ct) => Task.FromResult(new List<CondicaoPagamento>());
        public Task<CondicaoPagamento?> ObterAsync(Guid id, CancellationToken ct) => Task.FromResult<CondicaoPagamento?>(null);
        public Task<Dictionary<Guid, CondicaoPagamento>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult(new Dictionary<Guid, CondicaoPagamento>());
        public Task<Dictionary<Guid, int>> ContarUsosAsync(Guid? somenteId, CancellationToken ct) => throw new NotImplementedException();
        public Task SalvarAsync(CondicaoPagamento item, bool novo, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class ClassificacoesFixas : IPapelRepositorio
    {
        public Task<List<Papel>> ListarAsync(bool incluirInativos, CancellationToken ct) =>
            Task.FromResult(new List<Papel> { new() { Id = ClassificacaoVendedor, Codigo = "VENDEDOR", Nome = "Vendedor" } });
        public Task<Papel?> ObterAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<Guid, Papel>> ObterVariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotImplementedException();
        public Task<(bool Nome, bool Codigo)> EmUsoAsync(string nome, string codigo, Guid ignorarId, CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<Guid, int>> ContarPessoasAsync(Guid? somentePapelId, CancellationToken ct) => throw new NotImplementedException();
        public Task<int> ProximaOrdemAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task SalvarAsync(Papel papel, bool novo, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class ParametrosPadrao : IParametrosComerciaisRepositorio
    {
        public Task<ParametrosComerciais> ObterAsync(CancellationToken ct) => Task.FromResult(new ParametrosComerciais { Id = ParametrosComerciais.IdUnico });
        public Task SalvarAsync(ParametrosComerciais parametros, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class CoberturasFixas : ICoberturaRepositorio
    {
        public List<CoberturaComercial> DoTitular { get; } = new();
        public Task<CoberturaComercial?> ObterAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<List<CoberturaComercial>> DoTitularAsync(Guid titularId, CancellationToken ct) =>
            Task.FromResult(DoTitular.Where(c => c.TitularId == titularId).ToList());
        public Task<List<CoberturaComercial>> ListarAsync(DateOnly desde, bool incluirEncerradas, CancellationToken ct) =>
            Task.FromResult(DoTitular.Where(c => incluirEncerradas || (!c.Cancelada && c.FimEm >= desde)).ToList());
        public Task SalvarAsync(CoberturaComercial item, bool novo, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class AutorizacaoFixa : IAutorizacao
    {
        public HashSet<string> Negadas { get; } = new();
        public bool Possui(string permissao) => !Negadas.Contains(permissao);
        public void Exigir(string permissao)
        {
            if (!Possui(permissao)) throw new AcessoNegadoException(permissao);
        }
    }

    private sealed class MotivoEmMemoria : IMotivoDaOperacao
    {
        public string? Motivo { get; set; }
    }

    private sealed class UsuarioFixo : IUsuarioAtual
    {
        public Guid? Id => Guid.Empty;
        public string Nome => "Admin";
    }
}
