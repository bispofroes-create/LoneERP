using Lone.Application.Seguranca;
using Lone.Application.Territorios;
using Lone.Contracts.Territorios;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Territorios;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Operações TE-, mudanças e simulações (SQL Server via EF Core). Operação e simulação nunca são apagadas.</summary>
public class OperacaoTerritorialRepositorio : ServicoDadosBase, IOperacaoTerritorialRepositorio
{
    public OperacaoTerritorialRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<OperacaoTerritorial>> ListarAsync(Guid? mapaId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var q = db.OperacoesTerritoriais.AsNoTracking().Include(o => o.Mudancas).AsSplitQuery();
        if (mapaId is { } m) q = q.Where(o => o.MapaId == m);
        return await q.OrderByDescending(o => o.Ano).ThenByDescending(o => o.Sequencia).ToListAsync(ct);
    }

    public async Task<OperacaoTerritorial?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.OperacoesTerritoriais.AsNoTracking().Include(o => o.Mudancas).FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task CriarAsync(OperacaoTerritorial operacao, int ano, Action<OperacaoTerritorial> aoNumerar, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        operacao.Ano = ano;
        operacao.Sequencia = await MotorTerritorialSql.ProximoNumeroAsync(db, OperacaoTerritorial.Prefixo, ano, ct);
        aoNumerar(operacao);
        db.OperacoesTerritoriais.Add(operacao);
        await Filhos.GravarAsync(db, "Número de operação territorial repetido: tente de novo.", ct);
        await transacao.CommitAsync(ct);
    }

    public async Task SalvarAsync(OperacaoTerritorial operacao, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        foreach (var m in operacao.Mudancas)
        {
            m.OperacaoId = operacao.Id;
            m.MapaId = operacao.MapaId;
        }
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        var atual = await db.OperacoesTerritoriais.Include(o => o.Mudancas).FirstOrDefaultAsync(o => o.Id == operacao.Id, ct)
                    ?? throw new ConflitoDeEdicaoException(RegrasOperacaoTerritorial.MensagemOperacaoAlterada);
        // Renumerar a ordem não pode passar por um estado com duas iguais (índice único (OperacaoId, Ordem)): as que ficam
        // saem antes para fora da faixa, e a gravação abaixo põe cada uma no lugar final.
        var renumera = atual.Mudancas.Any(a => operacao.Mudancas.FirstOrDefault(n => n.Id == a.Id) is { } n && n.Ordem != a.Ordem);
        if (renumera)
            await db.OperacaoTerritorialMudancas.Where(m => m.OperacaoId == operacao.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Ordem, m => m.Ordem + 100000), ct);

        var versaoAberta = operacao.Versao;
        operacao.CriadoEm = atual.CriadoEm;
        operacao.AtualizadoEm = atual.AtualizadoEm;
        var entrada = db.Entry(atual);
        entrada.CurrentValues.SetValues(operacao);
        entrada.Property(x => x.Versao).OriginalValue = versaoAberta; // exige a versão que a tela abriu
        Filhos.Sincronizar(db, atual.Mudancas, operacao.Mudancas, apagarAusentes: true);
        atual.ReceberEventosDe(operacao);
        entrada.Property(x => x.AtualizadoEm).IsModified = true; // o cabeçalho sempre grava: a versão é conferida mesmo quando só as mudanças mudaram
        try
        {
            await Filhos.GravarAsync(db, "A mesma posição aparece duas vezes na ordem das mudanças.", ct);
        }
        catch (ConflitoDeEdicaoException ex) when (ex.Message == ConflitoDeEdicaoException.MensagemPadrao)
        {
            throw new ConflitoDeEdicaoException(RegrasOperacaoTerritorial.MensagemOperacaoAlterada, ex);
        }
        await transacao.CommitAsync(ct);
    }

    public async Task SalvarSimulacaoAsync(OperacaoTerritorial operacao, OperacaoTerritorialSimulacao simulacao, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        // A operação primeiro, exigindo a versão lida e ainda em aberto: duas simulações simultâneas não passam as duas.
        var agora = DateTime.UtcNow;
        var travada = await db.OperacoesTerritoriais
            .Where(o => o.Id == operacao.Id && o.Versao == operacao.Versao &&
                        (o.Situacao == SituacaoOperacaoTerritorial.Rascunho || o.Situacao == SituacaoOperacaoTerritorial.Simulada))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.AtualizadoEm, agora), ct);
        if (travada == 0) throw new ConflitoDeEdicaoException(RegrasOperacaoTerritorial.MensagemOperacaoAlterada);

        var itens = simulacao.Itens;
        simulacao.Itens = [];
        db.OperacaoTerritorialSimulacoes.Add(simulacao);
        await db.SaveChangesAsync(ct);
        foreach (var lote in itens.Chunk(1000))
        {
            db.OperacaoTerritorialSimulacaoItens.AddRange(lote);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }

        var atual = await db.OperacoesTerritoriais.FirstAsync(o => o.Id == operacao.Id, ct);
        atual.Situacao = operacao.Situacao;
        atual.SimulacaoAtualId = simulacao.Id;
        atual.ReceberEventosDe(operacao);
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        simulacao.Itens = itens;
    }

    public async Task<List<OperacaoTerritorialSimulacao>> SimulacoesAsync(Guid operacaoId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.OperacaoTerritorialSimulacoes.AsNoTracking().Where(s => s.OperacaoId == operacaoId)
            .OrderByDescending(s => s.SimuladaEm).ToListAsync(ct);
    }

    public async Task<OperacaoTerritorialSimulacao?> ObterSimulacaoAsync(Guid simulacaoId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.OperacaoTerritorialSimulacoes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == simulacaoId, ct);
    }

    public async Task<(int Total, List<OperacaoTerritorialSimulacaoItem> Itens)> ItensSimulacaoAsync(Guid simulacaoId, FiltroItensOperacaoTerritorialDto filtro,
                                                                                                    IReadOnlySet<Guid>? pessoas, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var q = db.OperacaoTerritorialSimulacaoItens.AsNoTracking().Where(i => i.SimulacaoId == simulacaoId);
        if (filtro.Efeito is { } e) q = q.Where(i => i.Efeito == e);
        if (filtro.Origem is { } o) q = q.Where(i => i.OrigemEfeito == o);
        if (filtro.SoProblemas)
            q = q.Where(i => i.Efeito == EfeitoNoCliente.Bloqueado || i.Resultado == ResultadoAtribuicao.Conflito ||
                             i.Resultado == ResultadoAtribuicao.PermaneceEmConflito);
        if (pessoas is not null) q = q.Where(i => pessoas.Contains(i.PessoaId));
        // Sem escopo: filtro por texto dentro dos itens da operação; quem chama já restringiu pelo alcance (lista de pessoas).
        if (filtro.Texto is { } texto) q = q.Where(i => db.Pessoas.Any(p => p.Id == i.PessoaId && (p.Nome.Contains(texto) || p.Codigo.ToString() == texto)));
        var total = await q.CountAsync(ct);
        var itens = await q.OrderBy(i => i.Efeito == EfeitoNoCliente.Bloqueado ? 0 : 1).ThenBy(i => i.Efeito).ThenBy(i => i.PessoaId)
            .Skip(filtro.Pular).Take(filtro.Quantidade).ToListAsync(ct);
        return (total, itens);
    }

    public async Task<(int Total, List<OperacaoTerritorialItem> Itens)> ItensAplicadosAsync(Guid operacaoId, FiltroItensOperacaoTerritorialDto filtro,
                                                                                           IReadOnlySet<Guid>? pessoas, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var q = db.OperacaoTerritorialItens.AsNoTracking().Where(i => i.OperacaoId == operacaoId);
        if (filtro.Efeito is { } e) q = q.Where(i => i.Efeito == e);
        if (filtro.SoProblemas)
            q = q.Where(i => i.Resultado == ResultadoAtribuicao.Conflito || i.Resultado == ResultadoAtribuicao.PermaneceEmConflito);
        if (pessoas is not null) q = q.Where(i => pessoas.Contains(i.PessoaId));
        // Sem escopo: idem: itens aplicados, já restritos pelo alcance em quem chama.
        if (filtro.Texto is { } texto) q = q.Where(i => db.Pessoas.Any(p => p.Id == i.PessoaId && (p.Nome.Contains(texto) || p.Codigo.ToString() == texto)));
        var total = await q.CountAsync(ct);
        var itens = await q.OrderBy(i => i.Efeito).ThenBy(i => i.PessoaId).Skip(filtro.Pular).Take(filtro.Quantidade).ToListAsync(ct);
        return (total, itens);
    }

    public async Task<List<Guid>> PessoasDosItensAsync(Guid? simulacaoId, Guid? operacaoId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        if (simulacaoId is { } s)
            return await db.OperacaoTerritorialSimulacaoItens.AsNoTracking().Where(i => i.SimulacaoId == s).Select(i => i.PessoaId).ToListAsync(ct);
        return await db.OperacaoTerritorialItens.AsNoTracking().Where(i => i.OperacaoId == operacaoId).Select(i => i.PessoaId).ToListAsync(ct);
    }

    public async Task RegistrarEventoAsync(Guid operacaoId, string evento, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // Linha de evento da auditoria gravada à parte (a transação da aplicação caiu). Descrição cabe em 500 caracteres.
        const int Maximo = 500;
        db.Auditoria.Add(new RegistroAuditoria
        {
            DataHora = DateTime.UtcNow, Usuario = db.Usuario, OperacaoId = Guid.NewGuid(), Origem = db.Origem,
            Entidade = nameof(OperacaoTerritorial), RegistroId = operacaoId.ToString(), RaizEntidade = nameof(OperacaoTerritorial), RaizId = operacaoId,
            Acao = AcaoAuditoria.Evento, Descricao = evento.Length > Maximo ? evento[..(Maximo - 1)] + "…" : evento
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<Guid, string>> NumerosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        await using var db = await AbrirAsync(ct);
        return (await db.OperacoesTerritoriais.AsNoTracking().Where(o => ids.Contains(o.Id)).Select(o => new { o.Id, o.Ano, o.Sequencia }).ToListAsync(ct))
            .ToDictionary(o => o.Id, o => RegrasOperacaoTerritorial.Numero(o.Ano, o.Sequencia));
    }
}

/// <summary>
/// As leituras do motor sobre um contexto aberto por quem chama (fora de transação na simulação; dentro da transação
/// travada na aplicação). Sem rastreamento: o que se grava vai por outro caminho.
/// </summary>
internal sealed class LeitorTerritorial : ILeitorTerritorial
{
    private readonly LoneDbContext _db;

    public LeitorTerritorial(LoneDbContext db) => _db = db;

    public Task<MapaTerritorialMotor?> MotorAsync(Guid mapaId, CancellationToken ct) =>
        _db.MapaTerritorialMotores.AsNoTracking().FirstOrDefaultAsync(m => m.MapaId == mapaId, ct);

    public Task<byte[]?> VersaoArvoreAsync(Guid mapaId, CancellationToken ct) =>
        _db.MapaTerritorialArvores.AsNoTracking().Where(a => a.MapaId == mapaId).Select(a => a.Versao).FirstOrDefaultAsync(ct);

    public async Task<ParametrosTerritoriais> ParametrosAsync(CancellationToken ct) =>
        await _db.ParametrosTerritoriais.AsNoTracking().FirstOrDefaultAsync(p => p.Id == ParametrosTerritoriais.IdUnico, ct)
        ?? new ParametrosTerritoriais { Id = ParametrosTerritoriais.IdUnico };

    public Task<MapaTerritorial?> MapaAsync(Guid mapaId, CancellationToken ct) =>
        _db.MapasTerritoriais.AsNoTracking().Include(m => m.Classificacoes).FirstOrDefaultAsync(m => m.Id == mapaId, ct);

    public async Task<EstadoTerritorialEmD> EstadoAsync(MapaTerritorial mapa, DateOnly efeito, bool comAtribuicoes, CancellationToken ct)
    {
        var territorios = await _db.Territorios.AsNoTracking().Where(t => t.MapaId == mapa.Id).Include(t => t.Posicoes).AsSplitQuery().ToListAsync(ct);
        var regras = await _db.RegrasTerritorio.AsNoTracking()
            .Where(r => r.MapaId == mapa.Id && r.Ativo && r.InicioEm <= efeito && (r.FimEm == null || r.FimEm >= efeito)).ToListAsync(ct);
        var excecoes = await _db.ExcecoesTerritorio.AsNoTracking()
            .Where(x => x.MapaId == mapa.Id && x.Ativo && x.InicioEm <= efeito && (x.FimEm == null || x.FimEm >= efeito)).ToListAsync(ct);
        var atribuicoes = comAtribuicoes
            ? await _db.AtribuicoesTerritorio.AsNoTracking()
                .Where(a => a.MapaId == mapa.Id && a.Ativo && a.InicioEm <= efeito && (a.FimEm == null || a.FimEm >= efeito)).ToListAsync(ct)
            : [];
        return new EstadoTerritorialEmD(mapa, efeito, territorios.ToDictionary(t => t.Id), regras, excecoes, atribuicoes);
    }

    public async Task<IReadOnlySet<Guid>> UniversoAsync(MapaTerritorial mapa, CancellationToken ct)
    {
        var classificacoes = mapa.ClassificacoesAceitas.ToList();
        return (await _db.PessoaPapeis.AsNoTracking().Where(p => p.Ativo && classificacoes.Contains(p.PapelId))
            .Select(p => p.PessoaId).Distinct().ToListAsync(ct)).ToHashSet();
    }

    public async Task<IReadOnlySet<Guid>> CandidatosAsync(MapaTerritorial mapa, GruposRegraTerritorioDto grupos, DateOnly hoje, CancellationToken ct)
    {
        var classificacoes = mapa.ClassificacoesAceitas.ToList();
        var parametros = await _db.ParametrosRelacionamento.AsNoTracking().FirstOrDefaultAsync(ct) ?? new ParametrosRelacionamento();
        var contexto = new Consultas.FiltrosPessoasSql.Contexto(_db, hoje, parametros, mapa.FinalidadeEnderecoReferenciaId);
        // Sem escopo: o motor avalia o mapa inteiro (alcance Tudo exigido para planejar e aplicar: DN-06).
        var universo = _db.Pessoas.AsNoTracking().Where(p => p.Papeis.Any(x => x.Ativo && classificacoes.Contains(x.PapelId)));

        var candidatos = new HashSet<Guid>();
        foreach (var grupo in grupos.Inclusao)
            candidatos.UnionWith(await Consultas.FiltrosPessoasSql.Aplicar(universo, grupo.Condicoes, contexto).Select(p => p.Id).ToListAsync(ct));
        foreach (var grupo in grupos.Exclusao)
        {
            if (candidatos.Count == 0) break;
            candidatos.ExceptWith(await Consultas.FiltrosPessoasSql.Aplicar(universo, grupo.Condicoes, contexto).Select(p => p.Id).ToListAsync(ct));
        }
        return candidatos;
    }

    public async Task<Dictionary<Guid, int>> UltimaVersaoDasRegrasAsync(Guid mapaId, CancellationToken ct) =>
        await _db.RegrasTerritorio.AsNoTracking().Where(r => r.MapaId == mapaId).GroupBy(r => r.TerritorioId)
            .Select(g => new { g.Key, Ultima = g.Max(r => r.Numero) }).ToDictionaryAsync(x => x.Key, x => x.Ultima, ct);

    public async Task<List<TerritorioResponsavel>> ResponsaveisAsync(IReadOnlyCollection<Guid> territorios, DateOnly efeito, CancellationToken ct) =>
        await _db.TerritorioResponsaveis.AsNoTracking()
            .Where(r => territorios.Contains(r.TerritorioId) && r.Ativo && (r.FimEm == null || r.FimEm >= efeito)).ToListAsync(ct);

    public async Task<string?> NumeroOperacaoAsync(Guid operacaoId, CancellationToken ct) =>
        await _db.OperacoesTerritoriais.AsNoTracking().Where(o => o.Id == operacaoId).Select(o => new { o.Ano, o.Sequencia }).FirstOrDefaultAsync(ct)
            is { } n ? RegrasOperacaoTerritorial.Numero(n.Ano, n.Sequencia) : null;

    public async Task<Dictionary<Guid, byte[]>> VersoesDasBasesAsync(IReadOnlyCollection<Guid> regras, IReadOnlyCollection<Guid> excecoes, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, byte[]>();
        if (regras.Count > 0)
            foreach (var r in await _db.RegrasTerritorio.AsNoTracking().Where(r => regras.Contains(r.Id)).Select(r => new { r.Id, r.Versao }).ToListAsync(ct))
                if (r.Versao is not null) resultado[r.Id] = r.Versao;
        if (excecoes.Count > 0)
            foreach (var x in await _db.ExcecoesTerritorio.AsNoTracking().Where(x => excecoes.Contains(x.Id)).Select(x => new { x.Id, x.Versao }).ToListAsync(ct))
                if (x.Versao is not null) resultado[x.Id] = x.Versao;
        return resultado;
    }

    /// <summary>
    /// Os valores lidos de cada campo usado nas regras, por cliente afetado ("UF de referência = MG; Etiquetas = VIP, Atacado").
    /// Em lotes de 1.000 clientes. Só os campos da lista fechada (DN-07).
    /// </summary>
    public async Task<Dictionary<Guid, string>> AtributosAsync(MapaTerritorial mapa, IReadOnlyCollection<Guid> pessoas, IReadOnlySet<string> campos,
                                                              CancellationToken ct)
    {
        var partes = new Dictionary<Guid, List<string>>();
        void Anotar(Guid pessoa, string texto)
        {
            if (!partes.TryGetValue(pessoa, out var lista)) partes[pessoa] = lista = [];
            lista.Add(texto);
        }
        static string Juntar(IEnumerable<string?> valores)
        {
            var lista = valores.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).Distinct().Order(StringComparer.CurrentCultureIgnoreCase).ToList();
            return lista.Count == 0 ? "(vazio)" : string.Join(", ", lista);
        }
        bool Usa(params string[] ids) => ids.Any(campos.Contains);
        var finalidade = mapa.FinalidadeEnderecoReferenciaId;

        foreach (var lote in pessoas.Distinct().Chunk(1000))
        {
            var ids = lote.ToList();
            if (Usa(Lone.Contracts.Pessoas.CamposFiltroPessoas.Natureza, Lone.Contracts.Pessoas.CamposFiltroPessoas.Porte, Lone.Contracts.Pessoas.CamposFiltroPessoas.GrupoEmpresarial))
            {
                // Sem escopo: atributos lidos dos clientes afetados pelo motor (mesmo universo, alcance Tudo).
                var linhas = await _db.Pessoas.AsNoTracking().Where(p => ids.Contains(p.Id))
                    .Select(p => new { p.Id, p.Natureza, p.Porte, Grupo = _db.GruposEmpresariais.Where(g => g.Id == p.GrupoEmpresarialId).Select(g => g.Nome).FirstOrDefault() })
                    .ToListAsync(ct);
                foreach (var l in linhas)
                {
                    if (campos.Contains(Lone.Contracts.Pessoas.CamposFiltroPessoas.Natureza)) Anotar(l.Id, $"Natureza = {l.Natureza}");
                    if (campos.Contains(Lone.Contracts.Pessoas.CamposFiltroPessoas.Porte)) Anotar(l.Id, $"Porte = {Juntar([l.Porte])}");
                    if (campos.Contains(Lone.Contracts.Pessoas.CamposFiltroPessoas.GrupoEmpresarial)) Anotar(l.Id, $"Grupo empresarial = {Juntar([l.Grupo])}");
                }
            }
            if (Usa(Lone.Contracts.Pessoas.CamposFiltroPessoas.Papeis))
                foreach (var g in (await _db.PessoaPapeis.AsNoTracking().Where(x => x.Ativo && ids.Contains(x.PessoaId))
                             .Select(x => new { x.PessoaId, Nome = _db.Papeis.Where(p => p.Id == x.PapelId).Select(p => p.Nome).FirstOrDefault() })
                             .ToListAsync(ct)).GroupBy(x => x.PessoaId))
                    Anotar(g.Key, $"Classificações = {Juntar(g.Select(x => x.Nome))}");
            if (Usa(Lone.Contracts.Pessoas.CamposFiltroPessoas.Etiquetas))
                foreach (var g in (await _db.PessoaEtiquetas.AsNoTracking().Where(x => ids.Contains(x.PessoaId))
                             .Select(x => new { x.PessoaId, Nome = _db.Etiquetas.Where(e => e.Id == x.EtiquetaId).Select(e => e.Nome).FirstOrDefault() })
                             .ToListAsync(ct)).GroupBy(x => x.PessoaId))
                    Anotar(g.Key, $"Etiquetas = {Juntar(g.Select(x => x.Nome))}");
            if (Usa(Lone.Contracts.Pessoas.CamposFiltroPessoas.Uf, Lone.Contracts.Pessoas.CamposFiltroPessoas.Municipio, Lone.Contracts.Pessoas.CamposFiltroPessoas.Cidade,
                    Lone.Contracts.Pessoas.CamposFiltroPessoas.Bairro, Lone.Contracts.Pessoas.CamposFiltroPessoas.Cep))
            {
                var principais = _db.PessoaEnderecoFinalidades.Where(f => f.Ativo && f.Principal && f.FinalidadeId == finalidade).Select(f => f.PessoaEnderecoId);
                var enderecos = await _db.PessoaEnderecos.AsNoTracking().Where(e => e.Ativo && ids.Contains(e.PessoaId) && principais.Contains(e.Id))
                    .Select(e => new { e.PessoaId, e.Uf, e.Cidade, e.Bairro, e.Cep, e.MunicipioId }).ToListAsync(ct);
                var comEndereco = enderecos.Select(e => e.PessoaId).ToHashSet();
                foreach (var e in enderecos)
                    Anotar(e.PessoaId, $"Endereço de referência = {Juntar([e.Cidade])}/{Juntar([e.Uf])}, bairro {Juntar([e.Bairro])}, CEP {Juntar([e.Cep])}" +
                                       (e.MunicipioId is { } m ? $", município IBGE {m}" : ", município a corrigir"));
                foreach (var p in ids.Where(p => !comEndereco.Contains(p)))
                    Anotar(p, "Endereço de referência = não tem (sem endereço principal na finalidade do mapa: não atende às condições de endereço)");
            }
            if (Usa(Lone.Contracts.Pessoas.CamposFiltroPessoas.ProdutorRural, Lone.Contracts.Pessoas.CamposFiltroPessoas.Regime,
                    Lone.Contracts.Pessoas.CamposFiltroPessoas.NaturezaJuridica))
                foreach (var g in (await _db.Estabelecimentos.AsNoTracking().Where(e => e.Ativo && ids.Contains(e.PessoaId))
                             .Select(e => new { e.PessoaId, e.ProdutorRural, e.RegimeTributario, e.NaturezaJuridica }).ToListAsync(ct)).GroupBy(e => e.PessoaId))
                {
                    if (campos.Contains(Lone.Contracts.Pessoas.CamposFiltroPessoas.ProdutorRural)) Anotar(g.Key, $"Produtor rural = {(g.Any(e => e.ProdutorRural) ? "sim" : "não")}");
                    if (campos.Contains(Lone.Contracts.Pessoas.CamposFiltroPessoas.Regime)) Anotar(g.Key, $"Regime = {Juntar(g.Select(e => e.RegimeTributario.ToString()))}");
                    if (campos.Contains(Lone.Contracts.Pessoas.CamposFiltroPessoas.NaturezaJuridica)) Anotar(g.Key, $"Natureza jurídica = {Juntar(g.Select(e => e.NaturezaJuridica))}");
                }
            if (Usa(Lone.Contracts.Pessoas.CamposFiltroPessoas.Cnae, Lone.Contracts.Pessoas.CamposFiltroPessoas.CnaePrincipal))
                foreach (var g in (await _db.EstabelecimentoCnaes.AsNoTracking().Where(c => ids.Contains(c.PessoaId))
                             .Select(c => new { c.PessoaId, c.Codigo, c.Principal }).ToListAsync(ct)).GroupBy(c => c.PessoaId))
                    Anotar(g.Key, $"CNAE principal = {Juntar(g.Where(c => c.Principal).Select(c => c.Codigo.ToString("0000000")))}; " +
                                  $"CNAEs = {Juntar(g.Select(c => c.Codigo.ToString("0000000")))}");
            if (Usa(Lone.Contracts.Pessoas.CamposFiltroPessoas.PerfilComercial, Lone.Contracts.Pessoas.CamposFiltroPessoas.CondicaoCliente))
                foreach (var g in (await _db.ContasCliente.AsNoTracking().Where(c => ids.Contains(c.PessoaId))
                             .Select(c => new
                             {
                                 c.PessoaId,
                                 Perfil = _db.PerfisComerciais.Where(p => p.Id == c.PerfilComercialId).Select(p => p.Nome).FirstOrDefault(),
                                 Condicao = _db.CondicoesPagamento.Where(p => p.Id == c.CondicaoPagamentoId).Select(p => p.Nome).FirstOrDefault()
                             }).ToListAsync(ct)).GroupBy(c => c.PessoaId))
                {
                    if (campos.Contains(Lone.Contracts.Pessoas.CamposFiltroPessoas.PerfilComercial)) Anotar(g.Key, $"Perfil comercial = {Juntar(g.Select(c => c.Perfil))}");
                    if (campos.Contains(Lone.Contracts.Pessoas.CamposFiltroPessoas.CondicaoCliente)) Anotar(g.Key, $"Condição de pagamento = {Juntar(g.Select(c => c.Condicao))}");
                }
        }
        return partes.ToDictionary(kv => kv.Key, kv =>
        {
            var texto = string.Join("; ", kv.Value);
            return texto.Length > 2000 ? texto[..1999] + "…" : texto;
        });
    }
}

/// <summary>
/// Leitura, aplicação e desfazer das operações (seção J e DN-05). A aplicação é uma transação só, com as travas sempre na
/// ordem árvore → motor → operação → territórios → linhas de fato (a mesma da 2b-1a, que usa árvore → motor → território),
/// e grava por tabela na ordem fecha → anula → abre: nenhum gatilho vê um estado intermediário sobreposto. Qualquer erro,
/// de regra ou de banco, desfaz tudo (DN-12: síncrona, sem aplicação parcial).
/// </summary>
public class MotorTerritorialDados : ServicoDadosBase, IMotorTerritorialDados
{
    /// <summary>Tempo máximo de cada comando da aplicação (o padrão de 30 s é curto para um mapa grande; DN-12).</summary>
    public static readonly TimeSpan TempoLimiteComando = TimeSpan.FromMinutes(5);

    private const int Lote = 1000;

    public MotorTerritorialDados(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<T> LerAsync<T>(Func<ILeitorTerritorial, Task<T>> ler, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.Database.SetCommandTimeout(TempoLimiteComando);
        return await ler(new LeitorTerritorial(db));
    }

    public async Task AplicarAsync(PedidoAplicacaoTerritorial pedido, Func<ILeitorTerritorial, Task<AplicacaoCalculada>> calcular, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.Database.SetCommandTimeout(TempoLimiteComando);
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        var agora = DateTime.UtcNow;

        // 1. Árvore (só com mudança de estrutura): a versão que a simulação viu.
        if (pedido.VersaoArvore is { } arvore &&
            await db.MapaTerritorialArvores.Where(a => a.MapaId == pedido.MapaId && a.Versao == arvore)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.AtualizadoEm, agora), ct) == 0)
            throw new ConflitoDeEdicaoException(RegrasOperacaoTerritorial.MensagemMapaMudou);

        // 2. Motor do mapa: a versão simulada. Serializa todas as aplicações do mapa.
        if (!await MotorTerritorialSql.ExigirAsync(db, pedido.MapaId, pedido.VersaoMotor, agora, ct))
            throw new ConflitoDeEdicaoException(RegrasOperacaoTerritorial.MensagemMapaMudou);

        // 3. A própria operação: a versão da tela e ainda Simulada (cancelar × aplicar: só um vence).
        if (await db.OperacoesTerritoriais
                .Where(o => o.Id == pedido.OperacaoId && o.Versao == pedido.VersaoOperacao && o.Situacao == SituacaoOperacaoTerritorial.Simulada)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.AtualizadoEm, agora), ct) == 0)
            throw new ConflitoDeEdicaoException(RegrasOperacaoTerritorial.MensagemOperacaoAlterada);

        // 4. Os territórios da estrutura, antes de ler: um responsável incluído no meio não escapa do encerramento.
        await TocarTerritoriosAsync(db, pedido.TerritoriosEstruturais, agora, ct);

        // 5. O cálculo (T8, bases, re-simulação e assinatura) com as travas tomadas.
        var calculo = await calcular(new LeitorTerritorial(db));
        var op = calculo.Operacao;

        try
        {
            await GravarPlanoAsync(db, calculo.Plano, op, agora, ct);
            foreach (var t in calculo.Plano.Territorios.Select(t => t.TerritorioId).Distinct())
                await TerritorioRepositorio.ConferirPosicaoAbertaAsync(db, t, ct);

            // 6. O motor recebe a última operação e o último efeito; a operação vai a Aplicada (com a auditoria).
            await db.MapaTerritorialMotores.Where(m => m.MapaId == op.MapaId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.UltimaOperacaoId, op.Id).SetProperty(m => m.UltimoEfeitoEm, op.EfeitoEm)
                                          .SetProperty(m => m.AtualizadoEm, agora), ct);
            await GravarOperacaoAsync(db, op, ct);
        }
        catch (Exception ex) when (RecusaDoBanco(ex) is { } mensagem)
        {
            throw new ConflitoDeEdicaoException(mensagem, ex);
        }
        await transacao.CommitAsync(ct);
    }

    public async Task DesfazerAsync(OperacaoTerritorial operacao, byte[] versaoOperacao, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.Database.SetCommandTimeout(TempoLimiteComando);
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        var agora = DateTime.UtcNow;

        // Motor: ainda é a última aplicada do mapa (sem cascata, DN-05); depois a operação (versão e Aplicada).
        if (await db.MapaTerritorialMotores.Where(m => m.MapaId == operacao.MapaId && m.UltimaOperacaoId == operacao.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.AtualizadoEm, agora), ct) == 0)
            throw new ConflitoDeEdicaoException($"A {operacao.Numero} não é mais a última operação aplicada neste mapa. Nada foi desfeito: recarregue.");
        if (await db.OperacoesTerritoriais
                .Where(o => o.Id == operacao.Id && o.Versao == versaoOperacao && o.Situacao == SituacaoOperacaoTerritorial.Aplicada)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.AtualizadoEm, agora), ct) == 0)
            throw new ConflitoDeEdicaoException(RegrasOperacaoTerritorial.MensagemOperacaoAlterada);

        var id = operacao.Id;
        var tocados = new HashSet<Guid>();
        try
        {
            // 1. Anula o que ela abriu (antes de reabrir: nada fica sobreposto no meio).
            foreach (var r in await db.RegrasTerritorio.Where(r => r.OperacaoId == id && r.Ativo).ToListAsync(ct))
            {
                r.Ativo = false;
                r.OperacaoAnulacaoId = id;
                tocados.Add(r.TerritorioId);
            }
            foreach (var x in await db.ExcecoesTerritorio.Where(x => x.OperacaoId == id && x.Ativo).ToListAsync(ct))
            {
                x.Ativo = false;
                x.OperacaoAnulacaoId = id;
                tocados.Add(x.TerritorioId);
            }
            if (await db.TerritorioPosicoes.AnyAsync(p => p.OperacaoId == id && p.Ativo, ct))
                throw new InvalidOperationException("Operação com mudança de estrutura não é desfeita (a estrutura só tem efeito até hoje: DN-01).");
            await db.SaveChangesAsync(ct);
            tocados.UnionWith(await db.AtribuicoesTerritorio.Where(a => a.OperacaoId == id && a.Ativo).Select(a => a.TerritorioId).Distinct().ToListAsync(ct));
            await db.AtribuicoesTerritorio.Where(a => a.OperacaoId == id && a.Ativo)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Ativo, false).SetProperty(a => a.OperacaoAnulacaoId, id).SetProperty(a => a.AtualizadoEm, agora), ct);

            // 2. Reabre o que ela fechou, com o fim exato de antes (registro de fechamentos); o que ela anulou volta a valer.
            var fechamentos = await db.OperacaoTerritorialFechamentos.AsNoTracking().Where(f => f.OperacaoId == id).ToListAsync(ct);
            var porTabela = fechamentos.ToLookup(f => f.Tabela);
            foreach (var f in porTabela[TabelaFechamentoTerritorial.Regra])
                if (await db.RegrasTerritorio.FirstOrDefaultAsync(r => r.Id == f.LinhaId, ct) is { } r)
                {
                    if (r.OperacaoAnulacaoId == id) { r.Ativo = true; r.OperacaoAnulacaoId = null; }
                    else { r.FimEm = f.FimAnterior; r.OperacaoEncerramentoId = null; }
                    tocados.Add(r.TerritorioId);
                }
            foreach (var f in porTabela[TabelaFechamentoTerritorial.Excecao])
                if (await db.ExcecoesTerritorio.FirstOrDefaultAsync(x => x.Id == f.LinhaId, ct) is { } x)
                {
                    if (x.OperacaoAnulacaoId == id) { x.Ativo = true; x.OperacaoAnulacaoId = null; }
                    else { x.FimEm = f.FimAnterior; x.OperacaoEncerramentoId = null; }
                    tocados.Add(x.TerritorioId);
                }
            if (porTabela[TabelaFechamentoTerritorial.Posicao].Any() || porTabela[TabelaFechamentoTerritorial.Responsavel].Any())
                throw new InvalidOperationException("Operação com mudança de estrutura não é desfeita (a estrutura só tem efeito até hoje: DN-01).");
            await db.SaveChangesAsync(ct);
            foreach (var lote in porTabela[TabelaFechamentoTerritorial.Atribuicao].Chunk(Lote))
            {
                var ids = lote.Select(f => f.LinhaId).ToList();
                var linhas = await db.AtribuicoesTerritorio.Where(a => ids.Contains(a.Id)).ToListAsync(ct);
                foreach (var a in linhas)
                {
                    var f = lote.First(y => y.LinhaId == a.Id);
                    if (a.OperacaoAnulacaoId == id) { a.Ativo = true; a.OperacaoAnulacaoId = null; }
                    else { a.FimEm = f.FimAnterior; a.OperacaoEncerramentoId = null; }
                    tocados.Add(a.TerritorioId);
                }
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }

            // 3. O motor volta à operação e ao efeito de antes; a operação vai a Desfeita.
            await db.MapaTerritorialMotores.Where(m => m.MapaId == operacao.MapaId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.UltimaOperacaoId, operacao.OperacaoAnteriorDoMapaId)
                                          .SetProperty(m => m.UltimoEfeitoEm, operacao.EfeitoAnteriorDoMapa).SetProperty(m => m.AtualizadoEm, agora), ct);
            await TocarTerritoriosAsync(db, tocados, agora, ct);
            await GravarOperacaoAsync(db, operacao, ct);
        }
        catch (Exception ex) when (RecusaDoBanco(ex) is { } mensagem)
        {
            throw new ConflitoDeEdicaoException(mensagem, ex);
        }
        await transacao.CommitAsync(ct);
    }

    /// <summary>
    /// Grava o plano: por tabela, fecha e anula (linhas existentes) antes de abrir (linhas novas); depois os itens aplicados e
    /// o registro dos fechamentos (o desfazer usa). As tabelas auditadas passam pelo EF (regra, exceção, posição, território,
    /// responsável); as de volume (atribuições, itens) vão em lotes de 1.000.
    /// </summary>
    private static async Task GravarPlanoAsync(LoneDbContext db, PlanoAplicacaoTerritorial plano, OperacaoTerritorial op, DateTime agora, CancellationToken ct)
    {
        var id = op.Id;
        var porTabela = plano.Fechamentos.ToLookup(f => f.Tabela);

        // Regras.
        await FecharAsync(db.RegrasTerritorio, porTabela[TabelaFechamentoTerritorial.Regra], (r, f) =>
        {
            if (f.Anular) { r.Ativo = false; r.OperacaoAnulacaoId = id; } else { r.FimEm = f.NovoFim; r.OperacaoEncerramentoId = id; }
        }, ct);
        await db.SaveChangesAsync(ct);
        db.RegrasTerritorio.AddRange(plano.RegrasNovas);
        await db.SaveChangesAsync(ct);

        // Exceções.
        await FecharAsync(db.ExcecoesTerritorio, porTabela[TabelaFechamentoTerritorial.Excecao], (x, f) =>
        {
            if (f.Anular) { x.Ativo = false; x.OperacaoAnulacaoId = id; } else { x.FimEm = f.NovoFim; x.OperacaoEncerramentoId = id; }
        }, ct);
        await db.SaveChangesAsync(ct);
        db.ExcecoesTerritorio.AddRange(plano.ExcecoesNovas);
        await db.SaveChangesAsync(ct);

        // Posições e territórios (DN-01: efeito até hoje; a situação e o pai do território acompanham a posição aberta).
        await FecharAsync(db.TerritorioPosicoes, porTabela[TabelaFechamentoTerritorial.Posicao], (p, f) =>
        {
            if (f.Anular) { p.Ativo = false; p.OperacaoAnulacaoId = id; } else { p.FimEm = f.NovoFim; p.OperacaoEncerramentoId = id; }
        }, ct);
        await db.SaveChangesAsync(ct);
        foreach (var alterado in plano.Territorios)
        {
            var t = await db.Territorios.FirstAsync(x => x.Id == alterado.TerritorioId, ct);
            t.PaiId = alterado.PaiId;
            t.Situacao = alterado.Situacao;
            t.FimEm = alterado.FimEm;
            t.RegistrarEvento(alterado.Evento);
        }
        db.TerritorioPosicoes.AddRange(plano.PosicoesNovas);
        await db.SaveChangesAsync(ct);

        // Responsáveis do território encerrado (consequência, DN-09): quem não começou é anulado; quem vai além termina na véspera.
        await FecharAsync(db.TerritorioResponsaveis, porTabela[TabelaFechamentoTerritorial.Responsavel], (r, f) =>
        {
            if (f.Anular) r.Ativo = false; else r.FimEm = f.NovoFim;
            r.OperacaoEncerramentoId = id;
        }, ct);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        // Atribuições: fecha/anula em lote (fora da auditoria campo a campo: o item aplicado é o registro), depois abre.
        foreach (var grupo in porTabela[TabelaFechamentoTerritorial.Atribuicao].GroupBy(f => (f.Anular, f.NovoFim)))
            foreach (var lote in grupo.Chunk(Lote))
            {
                var ids = lote.Select(f => f.LinhaId).ToList();
                var q = db.AtribuicoesTerritorio.Where(a => ids.Contains(a.Id));
                var afetadas = grupo.Key.Anular
                    ? await q.ExecuteUpdateAsync(s => s.SetProperty(a => a.Ativo, false).SetProperty(a => a.OperacaoAnulacaoId, id).SetProperty(a => a.AtualizadoEm, agora), ct)
                    : await q.ExecuteUpdateAsync(s => s.SetProperty(a => a.FimEm, grupo.Key.NovoFim).SetProperty(a => a.OperacaoEncerramentoId, id)
                                                       .SetProperty(a => a.AtualizadoEm, agora), ct);
                if (afetadas != ids.Count) throw new InvalidOperationException("Inconsistência interna: atribuição a encerrar não encontrada. Nada foi gravado.");
            }
        foreach (var lote in plano.AtribuicoesNovas.Chunk(Lote))
        {
            db.AtribuicoesTerritorio.AddRange(lote);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }

        // O que a aplicação fez com cada cliente (imutável) e o registro dos fechamentos.
        foreach (var lote in plano.Itens.Chunk(Lote))
        {
            db.OperacaoTerritorialItens.AddRange(lote);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
        foreach (var lote in plano.Fechamentos.Chunk(Lote))
        {
            db.OperacaoTerritorialFechamentos.AddRange(lote.Select(f => new OperacaoTerritorialFechamento
            {
                OperacaoId = id, Tabela = f.Tabela, LinhaId = f.LinhaId, FimAnterior = f.FimAnterior
            }));
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }

        // Os territórios que ganharam ou perderam uso trocam de versão: uma ficha da 2b-1a aberta antes não grava "sem uso".
        await TocarTerritoriosAsync(db, plano.TerritoriosTocados, agora, ct);
    }

    private static async Task FecharAsync<T>(DbSet<T> conjunto, IEnumerable<FechamentoPlanejado> fechamentos,
                                             Action<T, FechamentoPlanejado> aplicar, CancellationToken ct) where T : EntidadeBase
    {
        var lista = fechamentos.ToList();
        foreach (var lote in lista.Chunk(Lote))
        {
            var ids = lote.Select(f => f.LinhaId).ToList();
            var linhas = await conjunto.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
            if (linhas.Count != ids.Count) throw new InvalidOperationException("Inconsistência interna: linha a encerrar não encontrada. Nada foi gravado.");
            foreach (var linha in linhas) aplicar(linha, lote.First(f => f.LinhaId == linha.Id));
        }
    }

    private static async Task TocarTerritoriosAsync(LoneDbContext db, IReadOnlyCollection<Guid> territorios, DateTime agora, CancellationToken ct)
    {
        foreach (var lote in territorios.Distinct().Chunk(Lote))
        {
            var ids = lote.ToList();
            await db.Territorios.Where(t => ids.Contains(t.Id)).ExecuteUpdateAsync(s => s.SetProperty(t => t.AtualizadoEm, agora), ct);
        }
    }

    /// <summary>Cabeçalho da operação pelo EF (auditado, com os eventos), depois das travas.</summary>
    private static async Task GravarOperacaoAsync(LoneDbContext db, OperacaoTerritorial op, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var atual = await db.OperacoesTerritoriais.FirstAsync(o => o.Id == op.Id, ct);
        atual.Situacao = op.Situacao;
        atual.AplicadaEm = op.AplicadaEm;
        atual.AplicadaPorId = op.AplicadaPorId;
        atual.AplicadaPor = op.AplicadaPor;
        atual.DesfeitaEm = op.DesfeitaEm;
        atual.DesfeitaPorId = op.DesfeitaPorId;
        atual.DesfeitaPor = op.DesfeitaPor;
        atual.DesfeitaMotivo = op.DesfeitaMotivo;
        atual.OperacaoAnteriorDoMapaId = op.OperacaoAnteriorDoMapaId;
        atual.EfeitoAnteriorDoMapa = op.EfeitoAnteriorDoMapa;
        atual.Entraram = op.Entraram;
        atual.Sairam = op.Sairam;
        atual.Mudaram = op.Mudaram;
        atual.OrigemAtualizada = op.OrigemAtualizada;
        atual.ReceberEventosDe(op);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Gatilho ou índice único que barrou: a mensagem para o usuário (a transação cai inteira).</summary>
    private static string? RecusaDoBanco(Exception ex)
    {
        var sql = ex as Microsoft.Data.SqlClient.SqlException ?? ex.InnerException as Microsoft.Data.SqlClient.SqlException;
        return sql?.Number switch
        {
            >= SqlMigracaoTerritorios.ErroSobreposicao and <= SqlMigracaoTerritorios.ErroItens =>
                $"O banco de dados recusou a gravação ({sql.Message}). Nada foi gravado: simule de novo e, se persistir, avise o suporte.",
            2601 or 2627 =>
                "O banco de dados recusou a gravação: haveria duas linhas abertas onde só pode haver uma. Nada foi gravado: simule de novo.",
            _ => null
        };
    }
}

/// <summary>Leitura de regras, exceções e atribuições para as telas e o contrato dos documentos.</summary>
public class ConsultasTerritoriais : ServicoDadosBase, IConsultasTerritoriais
{
    public ConsultasTerritoriais(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<List<RegraTerritorio>> RegrasDoTerritorioAsync(Guid territorioId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.RegrasTerritorio.AsNoTracking().Where(r => r.TerritorioId == territorioId).ToListAsync(ct);
    }

    public async Task<List<ExcecaoTerritorio>> ExcecoesDoTerritorioAsync(Guid territorioId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.ExcecoesTerritorio.AsNoTracking().Where(x => x.TerritorioId == territorioId).ToListAsync(ct);
    }

    public async Task<(int Total, List<AtribuicaoTerritorio> Itens)> AtribuicoesDoTerritorioAsync(Guid territorioId, DateOnly data, IReadOnlySet<Guid>? pessoas,
                                                                                                  int limite, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var q = db.AtribuicoesTerritorio.AsNoTracking()
            .Where(a => a.TerritorioId == territorioId && a.Ativo && a.InicioEm <= data && (a.FimEm == null || a.FimEm >= data));
        if (pessoas is not null) q = q.Where(a => pessoas.Contains(a.PessoaId));
        var total = await q.CountAsync(ct);
        var itens = await q.OrderBy(a => a.InicioEm).ThenBy(a => a.PessoaId).Take(limite).ToListAsync(ct);
        return (total, itens);
    }

    public async Task<List<AtribuicaoTerritorio>> AtribuicoesDoClienteAsync(Guid pessoaId, DateOnly data, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.AtribuicoesTerritorio.AsNoTracking()
            .Where(a => a.PessoaId == pessoaId && a.Ativo && a.InicioEm <= data && (a.FimEm == null || a.FimEm >= data)).ToListAsync(ct);
    }

    public async Task<Dictionary<Guid, string>> NomesDePessoasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        await using var db = await AbrirAsync(ct);
        var resultado = new Dictionary<Guid, string>();
        foreach (var lote in ids.Distinct().Chunk(1000))
        {
            var loteIds = lote.ToList();
            // Mesmo nome da lista de Pessoas: o de exibição, senão o nome (projeção só com colunas da própria tabela).
            // Sem escopo: nomes por id de clientes que já passaram pelo alcance em quem chama.
            foreach (var p in await db.Pessoas.AsNoTracking().Where(p => loteIds.Contains(p.Id)).Select(p => new { p.Id, p.NomeExibicao, p.Nome }).ToListAsync(ct))
                resultado[p.Id] = p.NomeExibicao ?? p.Nome;
        }
        return resultado;
    }

    public async Task<Dictionary<Guid, long>> CodigosDePessoasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        await using var db = await AbrirAsync(ct);
        var resultado = new Dictionary<Guid, long>();
        foreach (var lote in ids.Distinct().Chunk(1000))
        {
            var loteIds = lote.ToList();
            // Sem escopo: códigos por id de clientes que já passaram pelo alcance em quem chama.
            foreach (var p in await db.Pessoas.AsNoTracking().Where(p => loteIds.Contains(p.Id)).Select(p => new { p.Id, p.Codigo }).ToListAsync(ct))
                resultado[p.Id] = p.Codigo;
        }
        return resultado;
    }
}

/// <summary>O registro único dos parâmetros territoriais (DN-08). Sem o registro, valem os padrões.</summary>
public class ParametrosTerritoriaisRepositorio : ServicoDadosBase, IParametrosTerritoriaisRepositorio
{
    public ParametrosTerritoriaisRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario) : base(fabrica, usuario) { }

    public async Task<ParametrosTerritoriais> ObterAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.ParametrosTerritoriais.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ParametrosTerritoriais.IdUnico, ct)
               ?? new ParametrosTerritoriais { Id = ParametrosTerritoriais.IdUnico };
    }

    public async Task SalvarAsync(ParametrosTerritoriais parametros, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var existe = await db.ParametrosTerritoriais.AnyAsync(x => x.Id == ParametrosTerritoriais.IdUnico, ct);
        parametros.Id = ParametrosTerritoriais.IdUnico;
        await GravacaoSimples.SalvarAsync(db, db.ParametrosTerritoriais, parametros, novo: !existe, "Parâmetros já existem.", ct);
    }
}
