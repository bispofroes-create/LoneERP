using Lone.Aplicacao.Comum;
using Lone.Aplicacao.Pessoas;
using Lone.Aplicacao.Seguranca;
using Lone.Core.Entidades;
using Lone.Core.Enums;
using Lone.Core.Validacao;
using Lone.Data.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Data.Repositorios;

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

        if (filtro.Papel is TipoPapel papel)
            consulta = consulta.Where(p => p.Papeis.Any(x => x.Papel == papel && x.Ativo));

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
                Papeis = p.Papeis.Where(x => x.Ativo).Select(x => x.Papel).ToList(),
                Cidade = p.Enderecos
                    .Where(e => (e.Finalidades & FinalidadeEndereco.Principal) != FinalidadeEndereco.Nenhuma)
                    .Select(e => e.Cidade).FirstOrDefault(),
                Uf = p.Enderecos
                    .Where(e => (e.Finalidades & FinalidadeEndereco.Principal) != FinalidadeEndereco.Nenhuma)
                    .Select(e => e.Uf).FirstOrDefault()
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

    public async Task<Pessoa?> ObterAsync(int id, CancellationToken ct)
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

    public async Task<PessoaIdentificacao?> BuscarPorDocumentoAsync(
        NaturezaPessoa natureza, string documento, int ignorarId, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        return await db.Pessoas.AsNoTracking()
            .Where(p => p.Natureza == natureza && p.DocumentoPrincipal == documento && p.Id != ignorarId)
            .Select(p => new PessoaIdentificacao(p.Id, p.Codigo, p.Nome))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<PessoaIdentificacao>> BuscarSemelhantesAsync(
        int ignorarId, string nome, IReadOnlyCollection<string> contatos, CancellationToken ct)
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

    public async Task<Pessoa> SalvarAsync(Pessoa pessoa, OrigemAlteracao origem, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        db.Origem = origem;

        Pessoa salva;
        if (pessoa.Id == 0)
        {
            // Bloqueios e relacionamentos têm operações próprias; nunca entram pelo cadastro.
            pessoa.Bloqueios.Clear();
            pessoa.Relacionamentos.Clear();
            db.Pessoas.Add(pessoa);
            salva = pessoa;
        }
        else
        {
            salva = await AplicarAlteracoesAsync(db, pessoa, ct);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflitoDeEdicaoException(ex);
        }

        return salva;
    }

    /// <summary>
    /// Carrega a pessoa do banco e aplica só o que mudou (é isso que alimenta a auditoria).
    /// A versão que o usuário abriu vira a "versão original": se o banco tiver outra, a gravação falha.
    /// </summary>
    private static async Task<Pessoa> AplicarAlteracoesAsync(LoneDbContext db, Pessoa dados, CancellationToken ct)
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

        // Endereços primeiro: estabelecimentos novos podem apontar para endereços novos.
        SincronizarFilhos(db, atual.Id, atual.Enderecos, dados.Enderecos);
        SincronizarFilhos(db, atual.Id, atual.Estabelecimentos, dados.Estabelecimentos, (existente, novo) =>
        {
            if (novo.EnderecoFiscal is { Id: 0 } enderecoNovo)
                existente.EnderecoFiscal = enderecoNovo; // mesma instância já incluída em atual.Enderecos
        });
        SincronizarFilhos(db, atual.Id, atual.Documentos, dados.Documentos);
        SincronizarFilhos(db, atual.Id, atual.MeiosContato, dados.MeiosContato);
        SincronizarFilhos(db, atual.Id, atual.Contatos, dados.Contatos);
        SincronizarFilhos(db, atual.Id, atual.Papeis, dados.Papeis);
        SincronizarFilhos(db, atual.Id, atual.ContasCliente, dados.ContasCliente);
        SincronizarFilhos(db, atual.Id, atual.ContasFornecedor, dados.ContasFornecedor);

        // A pessoa sempre é gravada (mesmo se só um endereço mudou), para a versão ser conferida e trocada.
        entrada.Property(p => p.AtualizadoEm).IsModified = true;

        return atual;
    }

    /// <summary>Inclui os novos, altera os existentes e remove os que saíram da lista.</summary>
    private static void SincronizarFilhos<T>(LoneDbContext db, int pessoaId, List<T> atuais, List<T> novos,
                                             Action<T, T>? aposAtualizar = null)
        where T : EntidadePessoaFilha
    {
        foreach (var removido in atuais.Where(a => novos.All(n => n.Id != a.Id)).ToList())
        {
            atuais.Remove(removido);
            db.Remove(removido);
        }

        foreach (var novo in novos)
        {
            novo.PessoaId = pessoaId;
            var existente = novo.Id == 0 ? null : atuais.FirstOrDefault(a => a.Id == novo.Id);

            if (existente is null)
            {
                novo.Id = 0;
                atuais.Add(novo);
            }
            else
            {
                novo.CriadoEm = existente.CriadoEm;
                novo.AtualizadoEm = existente.AtualizadoEm;
                db.Entry(existente).CurrentValues.SetValues(novo);
                aposAtualizar?.Invoke(existente, novo);
            }
        }
    }
}
