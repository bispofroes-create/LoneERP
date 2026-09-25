using Lone.Application.Pessoas;
using Lone.Application.Seguranca;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Persistência do agregado Pessoa (SQL Server via EF Core).</summary>
public class PessoaRepositorio : ServicoDadosBase, IPessoaRepositorio
{
    public PessoaRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    // ---------------------------------------------------------------- Leitura

    public async Task<List<PessoaResumo>> ListarAsync(FiltroPessoas filtro, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        IQueryable<Pessoa> consulta = db.Pessoas.AsNoTracking();

        if (!filtro.IncluirInativos)
            consulta = consulta.Where(p => p.Situacao == SituacaoPessoa.Ativo || p.Situacao == SituacaoPessoa.EmAnalise);

        if (filtro.PapelId is Guid papel)
            consulta = consulta.Where(p => p.Papeis.Any(x => x.PapelId == papel && x.Ativo)); // índice (PapelId, Ativo)

        if (filtro.MunicipioACorrigir)
            consulta = consulta.Where(p => db.PendenciasMunicipio.Any(x => x.PessoaId == p.Id && x.ResolvidaEm == null));

        if (filtro.EtiquetaId is Guid etiqueta)
            consulta = consulta.Where(p => p.Etiquetas.Any(e => e.EtiquetaId == etiqueta)); // índice (EtiquetaId, PessoaId)

        // Etapa 4 troca esta busca por tabela de termos + paginação por chave.
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
            consulta = AplicarBusca(consulta, filtro.Texto.Trim());

        return await consulta
            .OrderBy(p => p.NomeExibicao ?? p.NomeSocial ?? p.Nome)
            .Take(filtro.Limite)
            .Select(p => new PessoaResumo
            {
                Id = p.Id,
                Codigo = p.Codigo,
                Nome = p.NomeExibicao ?? p.NomeSocial ?? p.Nome,
                Natureza = p.Natureza,
                DocumentoPrincipal = p.DocumentoPrincipal,
                CnpjPrincipal = p.Estabelecimentos.Where(e => e.Principal).Select(e => e.Cnpj).FirstOrDefault(),
                QuantidadeEstabelecimentos = p.Estabelecimentos.Count,
                Situacao = p.Situacao,
                Papeis = db.Papeis
                    .Where(cadastro => p.Papeis.Any(x => x.Ativo && x.PapelId == cadastro.Id))
                    .OrderBy(cadastro => cadastro.Ordem)
                    .Select(cadastro => cadastro.Nome)
                    .ToList(),
                Cidade = p.Enderecos
                    .Where(e => (e.Finalidades & FinalidadeEndereco.Principal) != FinalidadeEndereco.Nenhuma)
                    .Select(e => e.Cidade).FirstOrDefault(),
                Uf = p.Enderecos
                    .Where(e => (e.Finalidades & FinalidadeEndereco.Principal) != FinalidadeEndereco.Nenhuma)
                    .Select(e => e.Uf).FirstOrDefault(),
                MunicipioACorrigir = db.PendenciasMunicipio.Any(x => x.PessoaId == p.Id && x.ResolvidaEm == null)
            })
            .ToListAsync(ct);
    }

    private static IQueryable<Pessoa> AplicarBusca(IQueryable<Pessoa> consulta, string termo)
    {
        var documento = termo.Any(char.IsAsciiDigit) ? Documento.Normalizar(termo) : string.Empty;
        var digitos = Documento.SomenteDigitos(termo);
        var buscaDigitos = digitos.Length >= 4;
        var codigo = int.TryParse(termo, out var c) ? c : -1;

        return consulta.Where(p =>
            p.Nome.Contains(termo) ||
            (p.NomeExibicao != null && p.NomeExibicao.Contains(termo)) ||
            (p.NomeSocial != null && p.NomeSocial.Contains(termo)) ||
            (p.Apelido != null && p.Apelido.Contains(termo)) ||
            p.Codigo == codigo ||
            (documento != "" && p.DocumentoPrincipal != null && p.DocumentoPrincipal.Contains(documento)) ||
            p.Estabelecimentos.Any(e =>
                (e.NomeFantasia != null && e.NomeFantasia.Contains(termo)) ||
                (documento != "" && e.Cnpj != null && e.Cnpj.Contains(documento))) ||
            p.MeiosContato.Any(m => m.Valor.Contains(termo) || (buscaDigitos && m.Valor.Contains(digitos))) ||
            p.Contatos.Any(x =>
                x.Nome.Contains(termo) ||
                (x.Email != null && x.Email.Contains(termo)) ||
                (buscaDigitos && ((x.Telefone != null && x.Telefone.Contains(digitos)) ||
                                  (x.Celular != null && x.Celular.Contains(digitos))))));
    }

    public async Task<Pessoa?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Pessoas.AsNoTracking()
            .Include(p => p.Estabelecimentos)
            .Include(p => p.Documentos)
            .Include(p => p.Enderecos)
            .Include(p => p.MeiosContato)
            .Include(p => p.Contatos)
            .Include(p => p.Papeis)
            .Include(p => p.ContasCliente)
            .Include(p => p.ContasFornecedor)
            .Include(p => p.Socios)
            .Include(p => p.Consentimentos)
            .Include(p => p.Etiquetas)
            .Include(p => p.ValoresPersonalizados)
            .Include(p => p.Bloqueios)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<int> ContarClientesAtivosAsync(CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Pessoas.CountAsync(p =>
            (p.Situacao == SituacaoPessoa.Ativo || p.Situacao == SituacaoPessoa.EmAnalise) &&
            p.Papeis.Any(x => x.Papel == TipoPapel.Cliente && x.Ativo), ct);
    }

    public async Task<List<DateOnly?>> ListarNascimentosAsync(TipoPapel? papel, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var consulta = db.Pessoas.AsNoTracking().Where(p =>
            p.Natureza == NaturezaPessoa.Fisica &&
            (p.Situacao == SituacaoPessoa.Ativo || p.Situacao == SituacaoPessoa.EmAnalise));
        if (papel is { } tipo)
            consulta = consulta.Where(p => p.Papeis.Any(x => x.Papel == tipo && x.Ativo));
        return await consulta.Select(p => p.DataNascimento).ToListAsync(ct);
    }

    public async Task<PessoaIdentificacao?> BuscarPorDocumentoAsync(
        NaturezaPessoa natureza, string documento, Guid ignorarId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Pessoas.AsNoTracking()
            .Where(p => p.Natureza == natureza && p.DocumentoPrincipal == documento && p.Id != ignorarId)
            .Select(p => new PessoaIdentificacao(p.Id, p.Codigo, p.Nome))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<PessoaIdentificacao>> BuscarSemelhantesAsync(
        Guid ignorarId, string nome, IReadOnlyCollection<string> contatos, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var valores = contatos.ToList();

        return await db.Pessoas.AsNoTracking()
            .Where(p => p.Id != ignorarId &&
                        (p.Nome == nome ||
                         p.MeiosContato.Any(m => valores.Contains(m.Valor)) ||
                         p.Contatos.Any(x => (x.Telefone != null && valores.Contains(x.Telefone)) ||
                                             (x.Celular != null && valores.Contains(x.Celular)) ||
                                             (x.Email != null && valores.Contains(x.Email)))))
            .Select(p => new PessoaIdentificacao(p.Id, p.Codigo, p.Nome))
            .Take(5)
            .ToListAsync(ct);
    }

    // ---------------------------------------------------------------- Gravação

    public async Task SalvarAsync(Pessoa pessoa, bool nova, OrigemAlteracao origem, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.Origem = origem;

        if (nova)
        {
            // Bloqueios e relacionamentos têm operações próprias; nunca entram pelo cadastro.
            pessoa.Bloqueios.Clear();
            pessoa.Relacionamentos.Clear();
            db.Pessoas.Add(pessoa);
        }
        else
        {
            await AplicarAlteracoesAsync(db, pessoa, ct);
        }

        await ResolverPendenciasCorrigidasAsync(db, pessoa, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflitoDeEdicaoException(ex);
        }
    }

    public async Task<List<PendenciaMunicipio>> ListarPendenciasMunicipioAsync(Guid pessoaId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.PendenciasMunicipio.AsNoTracking()
            .Where(p => p.PessoaId == pessoaId && p.ResolvidaEm == null)
            .OrderBy(p => p.CriadaEm)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Texto antigo de município que o usuário acabou de trocar por um município da lista (ou cujo endereço saiu):
    /// a pendência é dada como resolvida por ele, na mesma gravação.
    /// </summary>
    private async Task ResolverPendenciasCorrigidasAsync(LoneDbContext db, Pessoa pessoa, CancellationToken ct)
    {
        var abertas = await db.PendenciasMunicipio.Where(p => p.PessoaId == pessoa.Id && p.ResolvidaEm == null).ToListAsync(ct);
        foreach (var pendencia in abertas)
        {
            int? municipio;
            bool corrigida;
            if (pendencia.Origem == OrigemPendenciaMunicipio.Naturalidade)
            {
                municipio = pessoa.NaturalidadeMunicipioId;
                corrigida = municipio is not null || pessoa.Natureza != NaturezaPessoa.Fisica;
            }
            else
            {
                var endereco = pessoa.Enderecos.FirstOrDefault(e => e.Id == pendencia.RegistroId);
                municipio = endereco?.MunicipioId;
                corrigida = endereco is null || municipio is not null || !endereco.EhBrasil;
            }

            if (!corrigida) continue;
            pendencia.MunicipioId = municipio;
            pendencia.ResolvidaEm = DateTime.UtcNow;
            pendencia.ResolvidaPor = db.Usuario;
            pendencia.Observacao = null;
        }
    }

    /// <summary>
    /// Carrega a pessoa do banco e aplica só o que mudou (é isso que alimenta a auditoria).
    /// A versão que o usuário abriu vira a "versão original": se o banco tiver outra, a gravação falha.
    /// </summary>
    private static async Task AplicarAlteracoesAsync(LoneDbContext db, Pessoa dados, CancellationToken ct)
    {
        var atual = await db.Pessoas
            .Include(p => p.Estabelecimentos)
            .Include(p => p.Documentos)
            .Include(p => p.Enderecos)
            .Include(p => p.MeiosContato)
            .Include(p => p.Contatos)
            .Include(p => p.Papeis)
            .Include(p => p.ContasCliente)
            .Include(p => p.ContasFornecedor)
            .Include(p => p.Socios)
            .Include(p => p.Consentimentos)
            .Include(p => p.Etiquetas)
            .Include(p => p.ValoresPersonalizados)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == dados.Id, ct)
            ?? throw new ConflitoDeEdicaoException();

        var versaoAberta = dados.Versao;
        dados.Versao = atual.Versao;
        dados.Codigo = atual.Codigo;
        dados.CriadoEm = atual.CriadoEm;
        dados.AtualizadoEm = atual.AtualizadoEm;

        var entrada = db.Entry(atual);
        entrada.CurrentValues.SetValues(dados);
        entrada.Property(p => p.Versao).OriginalValue = versaoAberta;

        // Os Ids já vêm definidos (inclusive dos endereços novos), então o endereço fiscal de um
        // estabelecimento aponta direto para o Id; o EF grava os endereços antes dos estabelecimentos.
        SincronizarFilhos(db, atual.Id, atual.Enderecos, dados.Enderecos);
        SincronizarFilhos(db, atual.Id, atual.Estabelecimentos, dados.Estabelecimentos);
        SincronizarFilhos(db, atual.Id, atual.Documentos, dados.Documentos);
        SincronizarFilhos(db, atual.Id, atual.MeiosContato, dados.MeiosContato);
        SincronizarFilhos(db, atual.Id, atual.Contatos, dados.Contatos);
        SincronizarFilhos(db, atual.Id, atual.Papeis, dados.Papeis, apagarAusentes: false); // períodos nunca são apagados
        SincronizarFilhos(db, atual.Id, atual.ContasCliente, dados.ContasCliente);
        SincronizarFilhos(db, atual.Id, atual.ContasFornecedor, dados.ContasFornecedor);
        SincronizarFilhos(db, atual.Id, atual.Socios, dados.Socios);

        // Consentimento e etiqueta são únicos por canal / por etiqueta: casa pelo que os identifica, não pelo Id,
        // para não apagar e incluir de novo o mesmo registro (histórico limpo e sem conflito no índice único).
        ReaproveitarIds(atual.Consentimentos, dados.Consentimentos, c => c.Canal);
        ReaproveitarIds(atual.Etiquetas, dados.Etiquetas, e => e.EtiquetaId);
        SincronizarFilhos(db, atual.Id, atual.Consentimentos, dados.Consentimentos);
        SincronizarFilhos(db, atual.Id, atual.Etiquetas, dados.Etiquetas);

        // Um valor por campo personalizado: casa pelo campo, e a mudança aparece no histórico como alteração.
        ReaproveitarIds(atual.ValoresPersonalizados, dados.ValoresPersonalizados, v => v.CampoId);
        SincronizarFilhos(db, atual.Id, atual.ValoresPersonalizados, dados.ValoresPersonalizados);

        // Eventos de negócio (ex.: desativação) foram registrados na instância editada: vão com a gravada.
        atual.ReceberEventosDe(dados);

        // A pessoa sempre é gravada (mesmo se só um endereço mudou), para a versão ser conferida e trocada.
        entrada.Property(p => p.AtualizadoEm).IsModified = true;
    }

    private static void ReaproveitarIds<T, TChave>(List<T> atuais, List<T> novos, Func<T, TChave> chave)
        where T : EntidadePessoaFilha where TChave : notnull
    {
        var porChave = atuais.GroupBy(chave).ToDictionary(g => g.Key, g => g.First().Id);
        foreach (var novo in novos)
            if (porChave.TryGetValue(chave(novo), out var id))
                novo.Id = id;
    }

    /// <summary>Inclui os novos, altera os existentes e remove os que saíram da lista.</summary>
    /// <param name="apagarAusentes">
    /// Falso = o que não veio fica como está no banco (registros históricos, como os períodos de papel, nunca são apagados).
    /// </param>
    private static void SincronizarFilhos<T>(LoneDbContext db, Guid pessoaId, List<T> atuais, List<T> novos, bool apagarAusentes = true)
        where T : EntidadePessoaFilha
    {
        if (apagarAusentes)
            foreach (var removido in atuais.Where(a => novos.All(n => n.Id != a.Id)).ToList())
            {
                atuais.Remove(removido);
                db.Remove(removido);
            }

        foreach (var novo in novos)
        {
            novo.PessoaId = pessoaId;
            var existente = novo.Id == Guid.Empty ? null : atuais.FirstOrDefault(a => a.Id == novo.Id);

            if (existente is null)
            {
                if (novo.Id == Guid.Empty)
                    novo.Id = IdSequencial.Novo();
                atuais.Add(novo);
                db.Add(novo); // explícito: Id preenchido não pode ser confundido com registro existente
            }
            else
            {
                novo.CriadoEm = existente.CriadoEm;
                novo.AtualizadoEm = existente.AtualizadoEm;
                db.Entry(existente).CurrentValues.SetValues(novo);
            }
        }
    }
}
