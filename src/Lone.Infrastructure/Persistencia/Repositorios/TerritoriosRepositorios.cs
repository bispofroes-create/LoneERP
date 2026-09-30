using Lone.Application.Seguranca;
using Lone.Application.Territorios;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Territorios;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Tipos de território (SQL Server via EF Core). Nunca apaga.</summary>
public class TipoTerritorioRepositorio : ServicoDadosBase, ITipoTerritorioRepositorio
{
    public TipoTerritorioRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<TipoTerritorio>> ListarAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TiposTerritorio.AsNoTracking().ToListAsync(ct);
    }

    public async Task<TipoTerritorio?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.TiposTerritorio.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Dictionary<Guid, int>> ContarUsosAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Territorios.AsNoTracking().GroupBy(t => t.TipoId).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
    }

    public async Task SalvarAsync(TipoTerritorio tipo, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        if (novo)
            db.TiposTerritorio.Add(tipo);
        else
        {
            var atual = await db.TiposTerritorio.FirstOrDefaultAsync(x => x.Id == tipo.Id, ct) ?? throw new ConflitoDeEdicaoException();
            var versaoAberta = tipo.Versao;
            tipo.Versao = atual.Versao;
            tipo.CriadoEm = atual.CriadoEm;
            tipo.AtualizadoEm = atual.AtualizadoEm;
            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(tipo);
            entrada.Property(x => x.Versao).OriginalValue = versaoAberta;
            atual.ReceberEventosDe(tipo);
            entrada.Property(x => x.AtualizadoEm).IsModified = true;
        }
        await Filhos.GravarAsync(db, "Já existe um tipo de território com este código ou nome.", ct);
    }
}

/// <summary>Mapas territoriais com o universo (SQL Server via EF Core). Nunca apaga: desmarcar uma classificação a desativa.</summary>
public class MapaTerritorialRepositorio : ServicoDadosBase, IMapaTerritorialRepositorio
{
    public MapaTerritorialRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<MapaTerritorial>> ListarAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.MapasTerritoriais.AsNoTracking().Include(m => m.Classificacoes).AsSplitQuery().ToListAsync(ct);
    }

    public async Task<MapaTerritorial?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.MapasTerritoriais.AsNoTracking().Include(m => m.Classificacoes).FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<Dictionary<Guid, int>> ContarTerritoriosAtivosAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Territorios.AsNoTracking().Where(t => t.Situacao == SituacaoTerritorio.Ativo)
            .GroupBy(t => t.MapaId).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
    }

    /// <summary>
    /// Mapa novo nasce com a sua trava da árvore e a do motor (mesma transação). Ativar ou desativar troca também a versão
    /// da árvore: mapa desativado não aceita mudança de estrutura, e uma mudança conferida com o mapa ainda ativo não pode
    /// gravar depois da desativação. As outras alterações do cadastro não tocam na árvore (e a árvore não toca no cadastro).
    /// Fase 2b-1b (DN-02): ativar/desativar e mudar um campo travado pelo uso (empresa, exclusividade, endereço de
    /// referência, universo) trocam também a versão do motor, depois da árvore (ordem única de travas), e o uso é relido ali
    /// dentro: se uma operação foi aplicada no mapa enquanto a tela estava aberta, nada é gravado (L3).
    /// </summary>
    public async Task SalvarAsync(MapaTerritorial mapa, bool novo, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        foreach (var c in mapa.Classificacoes)
        {
            c.MapaId = mapa.Id;
            if (c.Id == Guid.Empty) c.Id = IdSequencial.Novo();
        }
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        if (novo)
        {
            db.MapasTerritoriais.Add(mapa);
            db.MapaTerritorialArvores.Add(new MapaTerritorialArvore { MapaId = mapa.Id, AtualizadoEm = DateTime.UtcNow });
            db.MapaTerritorialMotores.Add(new MapaTerritorialMotor { MapaId = mapa.Id, AtualizadoEm = DateTime.UtcNow });
        }
        else
        {
            var atual = await db.MapasTerritoriais.Include(m => m.Classificacoes).FirstOrDefaultAsync(m => m.Id == mapa.Id, ct)
                        ?? throw new ConflitoDeEdicaoException(RegrasMapaTerritorial.MensagemMapaAlterado);
            var mudouAtivo = atual.Ativo != mapa.Ativo;
            var mudouTravado = RegrasMapaTerritorial.CamposTravadosMudaram(atual, mapa);
            if (mudouAtivo)
                await db.MapaTerritorialArvores.Where(a => a.MapaId == mapa.Id)
                    .ExecuteUpdateAsync(x => x.SetProperty(a => a.AtualizadoEm, DateTime.UtcNow), ct);
            if (mudouAtivo || mudouTravado)
            {
                await MotorTerritorialSql.TocarAsync(db, mapa.Id, DateTime.UtcNow, ct);
                // Relido depois da trava do motor: toda aplicação de operação passa por ela, então o que se lê aqui é o
                // uso confirmado. A tela só deixa mudar estes campos (e desativar) sem uso.
                if ((mudouTravado || !mapa.Ativo) && await Consultas.UsoTerritorialSql.MapaEmUsoAsync(db, mapa.Id, ct))
                    throw new ConflitoDeEdicaoException(RegrasMapaTerritorial.MensagemPassouATerUso);
            }
            var versaoAberta = mapa.Versao;
            mapa.Versao = atual.Versao;
            mapa.CriadoEm = atual.CriadoEm;
            mapa.AtualizadoEm = atual.AtualizadoEm;
            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(mapa);
            entrada.Property(x => x.Versao).OriginalValue = versaoAberta;
            Filhos.Sincronizar(db, atual.Classificacoes, mapa.Classificacoes, apagarAusentes: false);
            atual.ReceberEventosDe(mapa);
            entrada.Property(x => x.AtualizadoEm).IsModified = true;
        }
        try
        {
            await Filhos.GravarAsync(db, "Já existe um mapa territorial com este código ou nome.", ct);
        }
        catch (ConflitoDeEdicaoException ex) when (ex.Message == ConflitoDeEdicaoException.MensagemPadrao)
        {
            throw new ConflitoDeEdicaoException(RegrasMapaTerritorial.MensagemMapaAlterado, ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 } sql &&
                                           (sql.Message.Contains(SqlMigracaoTerritorios.ChaveExcecoesExclusivo, StringComparison.Ordinal) ||
                                            sql.Message.Contains(SqlMigracaoTerritorios.ChaveAtribuicoesExclusivo, StringComparison.Ordinal)))
        {
            // Última barreira (FK (MapaId, Exclusivo), criada pela migration): o mapa tem exceção ou atribuição gravada.
            throw new ConflitoDeEdicaoException(RegrasMapaTerritorial.MensagemPassouATerUso, ex);
        }
        await transacao.CommitAsync(ct);
    }
}

/// <summary>
/// Territórios com posições e responsáveis (SQL Server via EF Core). Nunca apaga nada. Mudança de estrutura troca, na mesma
/// transação e antes de tudo, a versão da trava da árvore do mapa, exigindo a que a tela mostrava: é a serialização da
/// árvore por mapa (plano, seção 9.3; D1 = B).
/// </summary>
public class TerritorioRepositorio : ServicoDadosBase, ITerritorioRepositorio
{
    public TerritorioRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<Territorio>> ListarDoMapaAsync(Guid mapaId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Territorios.AsNoTracking().Where(t => t.MapaId == mapaId)
            .Include(t => t.Posicoes).Include(t => t.Responsaveis).AsSplitQuery().ToListAsync(ct);
    }

    public async Task<Territorio?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Territorios.AsNoTracking().Include(t => t.Posicoes).Include(t => t.Responsaveis).AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<byte[]?> ObterVersaoArvoreAsync(Guid mapaId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.MapaTerritorialArvores.AsNoTracking().Where(a => a.MapaId == mapaId).Select(a => a.Versao).FirstOrDefaultAsync(ct);
    }

    public async Task SalvarAsync(Territorio territorio, bool novo, byte[]? versaoArvoreVista, IReadOnlySet<Guid>? usoConferido, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        foreach (var p in territorio.Posicoes)
        {
            p.TerritorioId = territorio.Id;
            p.MapaId = territorio.MapaId;
            if (p.Id == Guid.Empty) p.Id = IdSequencial.Novo();
        }
        foreach (var r in territorio.Responsaveis)
        {
            r.TerritorioId = territorio.Id;
            if (r.Id == Guid.Empty) r.Id = IdSequencial.Novo();
        }

        // Tudo numa transação, e as travas vêm ANTES de qualquer outra gravação, sempre na mesma ordem (árvore, depois
        // território): quem chega depois espera na primeira linha, e quando passa encontra outra versão e é recusado sem ter
        // gravado nada. A ordem fixa evita impasse (deadlock) entre duas gravações.
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        var agora = DateTime.UtcNow;

        // 1. Mudança de estrutura: exige a versão da árvore que a tela mostrava (UPDATE ... WHERE Versao = @vista).
        if (versaoArvoreVista is not null)
        {
            var afetadas = await db.MapaTerritorialArvores.Where(a => a.MapaId == territorio.MapaId && a.Versao == versaoArvoreVista)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.AtualizadoEm, agora), ct);
            if (afetadas == 0) throw new ConflitoDeEdicaoException(RegrasArvoreTerritorial.MensagemArvoreAlterada);

            // 1b. (2b-1b, DN-02) A especificidade do motor usa a árvore: toda mudança de estrutura troca a versão do motor e
            //     deixa desatualizadas as simulações abertas (RT-7). É também a espera por uma aplicação em curso no mapa.
            await MotorTerritorialSql.TocarAsync(db, territorio.MapaId, agora, ct);

            // 1c. (2b-1b, L3) O uso operacional relido aqui, depois das duas travas: uma operação aplicada enquanto a tela
            //     estava aberta pode ter dado uso a um território deste ramo, e sem uso é que a mudança foi conferida.
            if (usoConferido is not null &&
                !(await Consultas.UsoTerritorialSql.TerritoriosComUsoAsync(db, territorio.MapaId, ct)).SetEquals(usoConferido))
                throw new ConflitoDeEdicaoException(RegrasArvoreTerritorial.MensagemUsoMudou);
        }

        if (novo)
            db.Territorios.Add(territorio);
        else
        {
            // 2. O território: exige a versão que o usuário abriu. É o que torna "dois usuários incluindo responsáveis
            //    conflitantes ao mesmo tempo" impossível: o segundo espera aqui e é recusado por versão.
            var versaoAberta = territorio.Versao;
            var travado = await db.Territorios.Where(t => t.Id == territorio.Id && t.Versao == versaoAberta)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.AtualizadoEm, agora), ct);
            if (travado == 0) throw new ConflitoDeEdicaoException();

            var atual = await db.Territorios.Include(t => t.Posicoes).Include(t => t.Responsaveis).AsSplitQuery()
                            .FirstOrDefaultAsync(t => t.Id == territorio.Id, ct) ?? throw new ConflitoDeEdicaoException();
            territorio.Versao = atual.Versao; // a lida agora, dentro da trava
            territorio.CriadoEm = atual.CriadoEm;
            territorio.AtualizadoEm = atual.AtualizadoEm;
            var entrada = db.Entry(atual);
            entrada.CurrentValues.SetValues(territorio);
            Filhos.Sincronizar(db, atual.Posicoes, territorio.Posicoes, apagarAusentes: false);
            Filhos.Sincronizar(db, atual.Responsaveis, territorio.Responsaveis, apagarAusentes: false);
            atual.ReceberEventosDe(territorio);
            entrada.Property(x => x.AtualizadoEm).IsModified = true;
        }

        // 3. Responsáveis alterados: o gatilho de sobreposição confere cada UPDATE, e o EF grava um por comando, sem ordem
        //    garantida. Como na carteira, cada um passa antes pela interseção do antes com o depois (nunca maior que nenhum
        //    dos dois): nenhum passo intermediário tem sobreposição se o estado final não tem.
        var intermediarios = db.ChangeTracker.Entries<TerritorioResponsavel>()
            .Where(e => e.State == EntityState.Modified)
            .Select(PassoIntermediarioResponsavel)
            .OfType<(Guid Id, DateOnly Inicio, DateOnly? Fim, bool Ativo)>()
            .ToList();
        try
        {
            foreach (var (id, inicio, fim, ativo) in intermediarios)
                await db.TerritorioResponsaveis.Where(r => r.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.InicioEm, inicio).SetProperty(r => r.FimEm, fim).SetProperty(r => r.Ativo, ativo), ct);
            await Filhos.GravarAsync(db, "Já existe neste mapa um território com este código, ou outro ativo com este nome no mesmo lugar da árvore.", ct);
        }
        catch (Exception ex) when (ErroDoBanco(ex) is { } numero && Mensagens.TryGetValue(numero, out var mensagem))
        {
            // Um gatilho barrou (ex.: gravação feita por fora que o domínio não viu): nada desta gravação fica (a transação cai).
            throw new ConflitoDeEdicaoException(mensagem, ex);
        }

        // 4. Conferência final, antes de confirmar: o pai do território e a posição aberta andam juntos (válido = uma posição
        //    aberta com o mesmo pai; encerrado = nenhuma). São duas tabelas gravadas em comandos separados, então isto não
        //    cabe num gatilho (ele veria o passo intermediário); aqui, dentro da mesma transação, vale o estado final.
        await ConferirPosicaoAbertaAsync(db, territorio.Id, ct);
        await transacao.CommitAsync(ct);
    }

    private static int? ErroDoBanco(Exception ex) => (ex as SqlException ?? ex.InnerException as SqlException)?.Number;

    private static readonly Dictionary<int, string> Mensagens = new()
    {
        [SqlMigracaoTerritorios.ErroSobreposicao] = ResponsavelSobreposto,
        [SqlMigracaoTerritorios.ErroArvore] = RegrasArvoreTerritorial.MensagemArvoreRecusadaPeloBanco,
        [SqlMigracaoTerritorios.ErroPosicoes] = RegrasArvoreTerritorial.MensagemArvoreRecusadaPeloBanco
    };

    public const string MensagemPosicaoIncoerente =
        "Inconsistência interna: o território e a sua posição aberta na árvore não conferem. Nada foi gravado.";

    internal static async Task ConferirPosicaoAbertaAsync(LoneDbContext db, Guid territorioId, CancellationToken ct)
    {
        var gravado = await db.Territorios.AsNoTracking().Where(t => t.Id == territorioId)
            .Select(t => new { t.Situacao, t.PaiId }).SingleAsync(ct);
        var abertas = await db.TerritorioPosicoes.AsNoTracking()
            .Where(p => p.TerritorioId == territorioId && p.Ativo && p.FimEm == null).Select(p => p.PaiId).ToListAsync(ct);
        var coerente = gravado.Situacao == SituacaoTerritorio.Ativo ? abertas.Count == 1 && abertas[0] == gravado.PaiId : abertas.Count == 0;
        if (!coerente) throw new InvalidOperationException(MensagemPosicaoIncoerente);
    }

    public const string ResponsavelSobreposto =
        "Não foi possível salvar porque a mesma pessoa (ou equipe) ficaria duas vezes na mesma função deste território no mesmo " +
        "período. Atualize os dados e tente novamente.";

    /// <summary>
    /// Estado intermediário de um responsável alterado: a interseção do período antes e depois, ativo só se estava e
    /// continua ativo com a mesma pessoa/equipe e função. Vazio (ou mudou quem/função) = inativo por um instante, com as
    /// datas antigas. Nulo = nada a fazer antes (só cresceu ou não mudou o período: a gravação final já é segura). Não passa
    /// pela auditoria: a gravação final registra o antes e o depois de verdade. Mesmo desenho do PessoaRepositorio (carteira).
    /// </summary>
    private static (Guid Id, DateOnly Inicio, DateOnly? Fim, bool Ativo)? PassoIntermediarioResponsavel(EntityEntry<TerritorioResponsavel> entrada)
    {
        var antes = entrada.OriginalValues;
        var depois = entrada.Entity;
        var ativoAntes = antes.GetValue<bool>(nameof(TerritorioResponsavel.Ativo));
        var inicioAntes = antes.GetValue<DateOnly>(nameof(TerritorioResponsavel.InicioEm));
        var fimAntes = antes.GetValue<DateOnly?>(nameof(TerritorioResponsavel.FimEm));
        var mesmaChave = antes.GetValue<Guid?>(nameof(TerritorioResponsavel.PessoaId)) == depois.PessoaId &&
                         antes.GetValue<Guid?>(nameof(TerritorioResponsavel.EquipeId)) == depois.EquipeId &&
                         antes.GetValue<Guid>(nameof(TerritorioResponsavel.TipoCarteiraId)) == depois.TipoCarteiraId;

        var inicio = inicioAntes > depois.InicioEm ? inicioAntes : depois.InicioEm;
        DateOnly? fim = fimAntes is null ? depois.FimEm : depois.FimEm is null ? fimAntes : fimAntes < depois.FimEm ? fimAntes : depois.FimEm;
        var ativo = ativoAntes && depois.Ativo && mesmaChave && (fim is null || fim >= inicio);

        if (!ativo) return ativoAntes ? (depois.Id, inicioAntes, fimAntes, false) : null;
        if (inicio == inicioAntes && fim == fimAntes) return null;
        return (depois.Id, inicio, fim, true);
    }
}
