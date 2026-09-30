using Lone.Application.Seguranca;
using Lone.Application.Territorios;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Territorios;
using Lone.Domain.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Enderecos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Papeis;
using Lone.Domain.Territorios;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Consultas;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// Apoio dos testes do motor territorial (Fase 2b-1b) contra o SQL Server: os serviços de verdade (Application +
/// Infraestrutura) sobre o banco temporário, com usuário, permissões, alcance e relógio fixos.
/// </summary>
internal static class MotorTerritorialTeste
{
    /// <summary>O "hoje" dos testes (fixo: nada depende do relógio da máquina).</summary>
    public static readonly DateOnly Hoje = new(2026, 9, 29);

    public static readonly Guid Cliente = PapeisSistema.Id(TipoPapel.Cliente);

    public sealed class Fabrica(BancoDeTeste banco) : IDbContextFactory<LoneDbContext>
    {
        public LoneDbContext CreateDbContext() => banco.Contexto();
    }

    /// <summary>
    /// Usuário dos serviços. Padrão: sem cadastro de usuário (Id nulo: as FKs de "quem" ficam nulas, o nome vai no texto);
    /// com Id, precisa existir em Usuarios (<see cref="UsuarioAsync"/>).
    /// </summary>
    public sealed class UsuarioTeste(Guid? id = null, string nome = "Teste") : IUsuarioAtual
    {
        public Guid? Id => id;
        public string Nome => nome;
    }

    /// <summary>Um usuário cadastrado (para as FKs de "quem" e a autoria das edições).</summary>
    public static async Task<UsuarioTeste> UsuarioAsync(BancoDeTeste banco, string nome)
    {
        await using var db = banco.Contexto();
        var u = new Usuario { Id = IdSequencial.Novo(), Login = "u" + Guid.NewGuid().ToString("N")[..12], Nome = nome, SenhaHash = "x" };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return new UsuarioTeste(u.Id, nome);
    }

    /// <summary>Permissões (todas, menos as negadas) e alcance.</summary>
    public sealed class Acesso : IAutorizacao, IAlcanceDoUsuario
    {
        public HashSet<string> Negadas { get; } = new();
        public AlcanceComercial Alcance { get; set; } = AlcanceComercial.Tudo;
        public Guid? PessoaId => null;
        public bool Possui(string permissao) => !Negadas.Contains(permissao);
        public void Exigir(string permissao)
        {
            if (!Possui(permissao)) throw new Lone.Contracts.Seguranca.AcessoNegadoException(permissao);
        }
    }

    public sealed class Relogio(DateOnly hoje) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(hoje.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    public sealed class EscopoTudo(DateOnly hoje) : IEscopoPessoas
    {
        public Task<EscopoResolvido> ObterAsync(CancellationToken ct = default) => Task.FromResult(EscopoResolvido.Todos(hoje));
        public Task ExigirAsync(Guid pessoaId, bool podeSerNovo = false, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> GerenciaEmAsync(Guid pessoaId, DateOnly data, CancellationToken ct = default) => Task.FromResult(true);
    }

    public sealed class NoEscopoTudo : IPessoasNoEscopo
    {
        public Task<SituacaoNoEscopo> SituacaoAsync(Guid pessoaId, EscopoResolvido escopo, CancellationToken ct) => throw new NotSupportedException();
        public Task<HashSet<Guid>> ClientesDiretosAsync(IReadOnlyCollection<Guid> ids, EscopoResolvido escopo, CancellationToken ct) => Task.FromResult(ids.ToHashSet());
    }

    public static OperacaoTerritorialAppService Operacoes(BancoDeTeste banco, Acesso? acesso = null, DateOnly? hoje = null, UsuarioTeste? usuario = null)
    {
        var f = new Fabrica(banco);
        var u = usuario ?? new UsuarioTeste();
        var a = acesso ?? new Acesso();
        var dia = hoje ?? Hoje;
        var escopo = new EscopoTudo(dia);
        return new OperacaoTerritorialAppService(new OperacaoTerritorialRepositorio(f, u), new MotorTerritorialDados(f, u), new MapaTerritorialRepositorio(f, u),
            new TerritorioRepositorio(f, u), new ConsultasTerritoriais(f, u), new UsoTerritorialSql(f, u), new ConsultaPessoas(f, u, escopo),
            new MunicipioRepositorio(f, u), a, u, a, escopo, new NoEscopoTudo(), new AuditoriaConsultas(f, u), new Relogio(dia));
    }

    public static ConsultaTerritorialAppService Consultas(BancoDeTeste banco, DateOnly? hoje = null)
    {
        var f = new Fabrica(banco);
        var u = new UsuarioTeste();
        var a = new Acesso();
        var dia = hoje ?? Hoje;
        return new ConsultaTerritorialAppService(new ConsultasTerritoriais(f, u), new TerritorioRepositorio(f, u), new MapaTerritorialRepositorio(f, u),
            new OperacaoTerritorialRepositorio(f, u), a, a, new EscopoTudo(dia), new NoEscopoTudo(), new Relogio(dia));
    }

    public static ParametrosTerritoriaisAppService Parametros(BancoDeTeste banco)
    {
        var f = new Fabrica(banco);
        return new ParametrosTerritoriaisAppService(new ParametrosTerritoriaisRepositorio(f, new UsuarioTeste()), new Acesso());
    }

    /// <summary>
    /// Um mapa (universo: Cliente) com Brasil › MG e Brasil › SP (desde 01/01/2026), as etiquetas "MG" e "SP" e clientes com
    /// cada uma. Nomes com sufixo: vários cenários convivem no mesmo banco temporário.
    /// </summary>
    public sealed record Cenario(MapaTerritorial Mapa, Territorio Brasil, Territorio Mg, Territorio Sp, Guid EtiquetaMg, Guid EtiquetaSp,
                                 List<Guid> ClientesMg, List<Guid> ClientesSp);

    public static async Task<Cenario> CenarioAsync(BancoDeTeste banco, int porEtiqueta = 3, bool exclusivo = true)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var mapa = new MapaTerritorial
        {
            Id = IdSequencial.Novo(), Codigo = "M" + sufixo.ToUpperInvariant(), Nome = "Mapa " + sufixo, Exclusivo = exclusivo,
            FinalidadeEnderecoReferenciaId = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Comercial)
        };
        mapa.Classificacoes.Add(new MapaTerritorialClassificacao { Id = IdSequencial.Novo(), MapaId = mapa.Id, PapelId = Cliente });
        var etiquetaMg = new Etiqueta { Id = IdSequencial.Novo(), Nome = "MG " + sufixo };
        var etiquetaSp = new Etiqueta { Id = IdSequencial.Novo(), Nome = "SP " + sufixo };
        await using (var db = banco.Contexto())
        {
            db.MapasTerritoriais.Add(mapa);
            db.MapaTerritorialArvores.Add(new MapaTerritorialArvore { MapaId = mapa.Id, AtualizadoEm = DateTime.UtcNow });
            db.MapaTerritorialMotores.Add(new MapaTerritorialMotor { MapaId = mapa.Id, AtualizadoEm = DateTime.UtcNow });
            db.Etiquetas.AddRange(etiquetaMg, etiquetaSp);
            await db.SaveChangesAsync();
        }
        var clientesMg = await ClientesAsync(banco, porEtiqueta, etiquetaMg.Id);
        var clientesSp = await ClientesAsync(banco, porEtiqueta, etiquetaSp.Id);

        var f = new Fabrica(banco);
        var repositorio = new TerritorioRepositorio(f, new UsuarioTeste());
        var brasil = Novo(mapa.Id, "Brasil", null);
        await repositorio.SalvarAsync(brasil, novo: true, null, null, default);
        var mg = Novo(mapa.Id, "MG", brasil.Id);
        await repositorio.SalvarAsync(mg, novo: true, null, null, default);
        var sp = Novo(mapa.Id, "SP", brasil.Id);
        await repositorio.SalvarAsync(sp, novo: true, null, null, default);
        return new Cenario(mapa, brasil, mg, sp, etiquetaMg.Id, etiquetaSp.Id, clientesMg, clientesSp);
    }

    /// <summary>Clientes (papel Cliente ativo) com a etiqueta, gravados em lotes (o teste de volume grava milhares).</summary>
    public static async Task<List<Guid>> ClientesAsync(BancoDeTeste banco, int quantos, Guid? etiqueta, Guid? papel = null)
    {
        var ids = new List<Guid>();
        foreach (var lote in Enumerable.Range(0, quantos).Chunk(1000))
        {
            await using var db = banco.Contexto();
            foreach (var _ in lote)
            {
                var id = IdSequencial.Novo();
                ids.Add(id);
                db.Pessoas.Add(new Pessoa { Id = id, Nome = "Cliente " + Guid.NewGuid().ToString("N")[..10], Natureza = NaturezaPessoa.Fisica });
                db.PessoaPapeis.Add(new PessoaPapel { Id = IdSequencial.Novo(), PessoaId = id, PapelId = papel ?? Cliente, InicioEm = new(2026, 1, 1) });
                if (etiqueta is { } e) db.PessoaEtiquetas.Add(new PessoaEtiqueta { Id = IdSequencial.Novo(), PessoaId = id, EtiquetaId = e });
            }
            await db.SaveChangesAsync();
        }
        return ids;
    }

    public static Territorio Novo(Guid mapaId, string nome, Guid? pai)
    {
        var t = new Territorio
        {
            Id = IdSequencial.Novo(), MapaId = mapaId, Codigo = RegrasCadastroTerritorial.NormalizarCodigo(nome), Nome = nome,
            TipoId = TiposTerritorioIniciais.Todos[0].Id, PaiId = pai
        };
        t.Posicoes.Add(new TerritorioPosicao { Id = IdSequencial.Novo(), MapaId = mapaId, TerritorioId = t.Id, PaiId = pai, InicioEm = new(2026, 1, 1) });
        return t;
    }

    public static GruposRegraTerritorioDto PorEtiqueta(Guid etiqueta) => new()
    {
        Inclusao = [new GrupoCondicoesTerritorioDto { Condicoes = [new CondicaoFiltro { Campo = CamposFiltroPessoas.Etiquetas, Operador = OperadorFiltro.UmDestes, Valores = [etiqueta.ToString()] }] }]
    };

    public static IncluirMudancaTerritorialRequisicao Regra(Guid territorio, Guid etiqueta, int? prioridade = null) => new()
    {
        Tipo = TipoMudancaTerritorial.NovaVersaoRegra, TerritorioId = territorio, Grupos = PorEtiqueta(etiqueta), Prioridade = prioridade
    };

    /// <summary>Cria o rascunho e inclui as mudanças, na ordem.</summary>
    public static async Task<OperacaoTerritorialDto> RascunhoAsync(OperacaoTerritorialAppService s, Guid mapaId, DateOnly efeito,
                                                                   params IncluirMudancaTerritorialRequisicao[] mudancas)
    {
        var op = await s.CriarAsync(new CriarOperacaoTerritorialRequisicao { MapaId = mapaId, EfeitoEm = efeito, Motivo = "Teste" });
        foreach (var m in mudancas)
        {
            m.Versao = op.Versao;
            op = await s.IncluirMudancaAsync(op.Id, m);
        }
        return op;
    }

    public static async Task<OperacaoTerritorialDto> SimularAsync(OperacaoTerritorialAppService s, OperacaoTerritorialDto op) =>
        await s.SimularAsync(op.Id, new VersaoOperacaoTerritorialRequisicao { Versao = op.Versao });

    public static async Task<OperacaoTerritorialDto> AplicarAsync(OperacaoTerritorialAppService s, OperacaoTerritorialDto simulada) =>
        await s.AplicarAsync(simulada.Id, new AplicarOperacaoTerritorialRequisicao { Versao = simulada.Versao, SimulacaoId = simulada.SimulacaoAtual!.Id });

    public static async Task<OperacaoTerritorialDto> SimularEAplicarAsync(OperacaoTerritorialAppService s, OperacaoTerritorialDto op) =>
        await AplicarAsync(s, await SimularAsync(s, op));

    /// <summary>Linhas de fato válidas do mapa (regras, exceções, atribuições): a simulação não pode mudar nada disto.</summary>
    public static async Task<(int Regras, int Excecoes, int Atribuicoes)> FatosAsync(BancoDeTeste banco, Guid mapaId)
    {
        await using var db = banco.Contexto();
        return (await db.RegrasTerritorio.CountAsync(r => r.MapaId == mapaId),
                await db.ExcecoesTerritorio.CountAsync(x => x.MapaId == mapaId),
                await db.AtribuicoesTerritorio.CountAsync(a => a.MapaId == mapaId));
    }

    public static async Task<byte[]> VersaoMotorAsync(BancoDeTeste banco, Guid mapaId)
    {
        await using var db = banco.Contexto();
        return (await db.MapaTerritorialMotores.AsNoTracking().Where(m => m.MapaId == mapaId).Select(m => m.Versao).SingleAsync())!;
    }

    /// <summary>Territórios atribuídos hoje (válidos) a cada cliente do mapa.</summary>
    public static async Task<Dictionary<Guid, Guid>> AtribuidosAsync(BancoDeTeste banco, Guid mapaId, DateOnly data)
    {
        await using var db = banco.Contexto();
        return await db.AtribuicoesTerritorio.AsNoTracking()
            .Where(a => a.MapaId == mapaId && a.Ativo && a.InicioEm <= data && (a.FimEm == null || a.FimEm >= data))
            .ToDictionaryAsync(a => a.PessoaId, a => a.TerritorioId);
    }
}
