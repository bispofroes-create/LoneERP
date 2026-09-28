using Lone.Application.Pessoas;
using Lone.Application.Seguranca;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Lone.Infrastructure.Persistencia.Configuracoes;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Repositorios;

/// <summary>Persistência do agregado Pessoa (SQL Server via EF Core).</summary>
public class PessoaRepositorio : ServicoDadosBase, IPessoaRepositorio
{
    private readonly IEscopoPessoas _escopo;

    public PessoaRepositorio(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario, IEscopoPessoas escopo)
        : base(fabrica, usuario)
    {
        _escopo = escopo;
    }

    // ---------------------------------------------------------------- Leitura

    public async Task<List<PessoaResumo>> ListarAsync(FiltroPessoas filtro, CancellationToken ct)
    {
        var escopo = await _escopo.ObterAsync(ct);
        await using var db = await AbrirAsync(ct);
        return await Resumir(Filtrar(db, filtro, escopo)
            .OrderBy(NomeParaExibirNoBanco)
            .Take(filtro.Limite), db, nomeComFantasia: true)
            .ToListAsync(ct);
    }

    public async Task<PaginaListaPessoas> ListarPaginaAsync(FiltroPessoas filtro, int pagina, int tamanho, CancellationToken ct)
    {
        var escopo = await _escopo.ObterAsync(ct);
        await using var db = await AbrirAsync(ct);
        var consulta = Filtrar(db, filtro, escopo);
        var total = await consulta.CountAsync(ct);
        var itens = await Resumir(consulta
            .OrderBy(NomeParaExibirNoBanco).ThenBy(p => p.Id) // desempate estável entre páginas
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho), db, nomeComFantasia: true)
            .ToListAsync(ct);
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        await Consultas.ColunasPessoasSql.PreencherAsync(itens, ContatosDaLinha, new Consultas.ColunasPessoasSql.Contexto(db, hoje), ct);
        return new PaginaListaPessoas
        {
            Itens = itens, Total = total, Pagina = pagina, TamanhoPagina = tamanho,
            Atalhos = pagina == 1 ? await ContarAtalhosAsync(SemAtalho(db, filtro, escopo), ct) : null
        };
    }

    /// <summary>Telefone e e-mail principais: sempre na página da tela de Pessoas (ligar, WhatsApp e e-mail na linha).</summary>
    private static readonly string[] ContatosDaLinha =
        [global::Lone.Contracts.Pessoas.CamposFiltroPessoas.Telefone, global::Lone.Contracts.Pessoas.CamposFiltroPessoas.Email];

    /// <summary>A mesma consulta da lista sem o atalho (natureza, papel, só ativos/inativos): base das contagens das abas.</summary>
    private static IQueryable<Pessoa> SemAtalho(LoneDbContext db, FiltroPessoas filtro, EscopoResolvido escopo) => Filtrar(db, new FiltroPessoas
    {
        Texto = filtro.Texto,
        EtiquetaId = filtro.EtiquetaId,
        IncluirInativos = filtro.IncluirInativos,
        MunicipioACorrigir = filtro.MunicipioACorrigir,
        Limite = filtro.Limite
    }, escopo);

    /// <summary>
    /// Quantos há em cada aba possível: duas consultas, qualquer que seja o número de abas escolhidas — uma por natureza e
    /// uma com todos os papéis (pessoas distintas com o papel ativo, a mesma regra do filtro por papel da lista).
    /// </summary>
    private static async Task<ContagensAtalhosPessoas> ContarAtalhosAsync(IQueryable<Pessoa> consulta, CancellationToken ct)
    {
        var porNatureza = await consulta.GroupBy(p => p.Natureza)
            .Select(g => new { Natureza = g.Key, Quantidade = g.Count() })
            .ToListAsync(ct);
        // Índice único (PessoaId, PapelId) com Ativo: cada pessoa tem no máximo um vínculo ativo por papel.
        var porPapel = await consulta.SelectMany(p => p.Papeis).Where(x => x.Ativo)
            .GroupBy(x => x.PapelId)
            .Select(g => new { PapelId = g.Key, Quantidade = g.Count() })
            .ToListAsync(ct);
        return new ContagensAtalhosPessoas
        {
            Todos = porNatureza.Sum(n => n.Quantidade),
            Naturezas = porNatureza.ToDictionary(n => n.Natureza.ToString(), n => n.Quantidade),
            Papeis = porPapel.ToDictionary(p => p.PapelId, p => p.Quantidade)
        };
    }

    public async Task<PaginaListaPessoas> ListarPaginaAsync(FiltroPessoas filtro, IReadOnlyList<CondicaoFiltro> condicoes, DateOnly hoje,
                                                            int pagina, int tamanho, CancellationToken ct) =>
        await ListarPaginaAsync(filtro, condicoes, [], null, hoje, pagina, tamanho, ct);

    public async Task<PaginaListaPessoas> ListarPaginaAsync(FiltroPessoas filtro, IReadOnlyList<CondicaoFiltro> condicoes,
                                                            IReadOnlyList<string> colunas, OrdenacaoLista? ordenacao, DateOnly hoje,
                                                            int pagina, int tamanho, CancellationToken ct)
    {
        var escopo = await _escopo.ObterAsync(ct);
        await using var db = await AbrirAsync(ct);
        var parametros = condicoes.Any(c => c.Campo == global::Lone.Contracts.Pessoas.CamposFiltroPessoas.Relacionamento)
            ? await db.ParametrosRelacionamento.AsNoTracking().FirstOrDefaultAsync(ct) ?? new ParametrosRelacionamento()
            : new ParametrosRelacionamento();
        var consulta = Consultas.FiltrosPessoasSql.Aplicar(Filtrar(db, filtro, escopo), condicoes,
            new Consultas.FiltrosPessoasSql.Contexto(db, hoje, parametros));
        var total = await consulta.CountAsync(ct);

        // Coluna clicada no cabeçalho; empate (e sem ordenação) pelo nome; por último o Id: desempate estável entre páginas.
        var colunasSql = new Consultas.ColunasPessoasSql.Contexto(db, hoje);
        var ordenada = Consultas.ColunasPessoasSql.Ordenar(consulta, ordenacao, colunasSql) is { } porColuna
            ? porColuna.ThenBy(NomeParaExibirNoBanco)
            : consulta.OrderBy(NomeParaExibirNoBanco);
        var itens = await Resumir(ordenada.ThenBy(p => p.Id)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho), db, nomeComFantasia: true)
            .ToListAsync(ct);
        await Consultas.ColunasPessoasSql.PreencherAsync(itens, [.. colunas.Union(ContatosDaLinha)], colunasSql, ct);

        // Abas: mesma busca e mesmas condições, sem o atalho escolhido.
        ContagensAtalhosPessoas? atalhos = null;
        if (pagina == 1)
            atalhos = await ContarAtalhosAsync(Consultas.FiltrosPessoasSql.Aplicar(SemAtalho(db, filtro, escopo), condicoes,
                new Consultas.FiltrosPessoasSql.Contexto(db, hoje, parametros)), ct);
        return new PaginaListaPessoas { Itens = itens, Total = total, Pagina = pagina, TamanhoPagina = tamanho, Atalhos = atalhos };
    }

    /// <summary>
    /// Filtros da lista de Pessoas (a mesma regra para a lista simples e a paginada). Começa pelo escopo de acesso (Fase
    /// 2a-2): lista, abas, indicadores e prévia só enxergam o que está no alcance do usuário.
    /// </summary>
    private static IQueryable<Pessoa> Filtrar(LoneDbContext db, FiltroPessoas filtro, EscopoResolvido escopo)
    {
        var consulta = Consultas.EscopoPessoasSql.Pessoas(db, escopo);

        if (filtro.SomenteInativos)
            consulta = consulta.Where(p => p.Situacao == SituacaoPessoa.Inativo || p.Situacao == SituacaoPessoa.Arquivado);
        else if (filtro.SomenteAtivos)
            consulta = consulta.Where(p => p.Situacao == SituacaoPessoa.Ativo);
        else if (!filtro.IncluirInativos)
            consulta = consulta.Where(p => p.Situacao == SituacaoPessoa.Ativo || p.Situacao == SituacaoPessoa.EmAnalise);

        if (filtro.Natureza is { } natureza)
            consulta = consulta.Where(p => p.Natureza == natureza);

        if (filtro.PapelId is Guid papel)
            consulta = consulta.Where(p => p.Papeis.Any(x => x.PapelId == papel && x.Ativo)); // índice (PapelId, Ativo)

        if (filtro.MunicipioACorrigir)
            consulta = consulta.Where(p => db.PendenciasMunicipio.Any(x => x.PessoaId == p.Id && x.ResolvidaEm == null));

        if (filtro.EtiquetaId is Guid etiqueta)
            consulta = consulta.Where(p => p.Etiquetas.Any(e => e.EtiquetaId == etiqueta)); // índice (EtiquetaId, PessoaId)

        // Etapa 4 troca esta busca por tabela de termos + paginação por chave.
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
            consulta = AplicarBusca(consulta, filtro.Texto.Trim(), db);

        return consulta;
    }

    /// <summary>
    /// A mesma regra de <see cref="Lone.Domain.Pessoas.NomePessoa.ParaExibir"/>, traduzida para o banco (lista de Pessoas):
    /// nome de exibição → (PJ) nome fantasia do estabelecimento principal → nome social → nome. O nome social só existe na
    /// pessoa física (o normalizador limpa nas outras naturezas). Mudou lá, mude aqui.
    /// </summary>
    internal static readonly System.Linq.Expressions.Expression<Func<Pessoa, string>> NomeParaExibirNoBanco = p =>
        p.NomeExibicao
        ?? (p.Natureza == NaturezaPessoa.Juridica
            ? p.Estabelecimentos.Where(e => e.Principal).Select(e => e.NomeFantasia).FirstOrDefault()
            : null)
        ?? p.NomeSocial
        ?? p.Nome;


    /// <summary>Linha da lista (compartilhada com a consulta avançada: uma só definição do resumo).</summary>
    /// <param name="nomeComFantasia">
    /// Verdadeiro na lista de Pessoas: o nome segue <see cref="NomeParaExibirNoBanco"/> (com o nome fantasia da PJ). A
    /// consulta avançada continua com o nome de antes (exibição → social → nome) nesta etapa.
    /// </param>
    internal static IQueryable<PessoaResumo> Resumir(IQueryable<Pessoa> consulta, LoneDbContext db, bool nomeComFantasia = false) =>
        ComReferencia(consulta, db)
            .Select(r => new PessoaResumo
            {
                Id = r.P.Id,
                Codigo = r.P.Codigo,
                Nome = r.P.NomeExibicao
                       ?? (nomeComFantasia && r.P.Natureza == NaturezaPessoa.Juridica
                           ? r.P.Estabelecimentos.Where(e => e.Principal).Select(e => e.NomeFantasia).FirstOrDefault()
                           : null)
                       ?? r.P.NomeSocial
                       ?? r.P.Nome,
                Natureza = r.P.Natureza,
                DocumentoPrincipal = r.P.DocumentoPrincipal,
                CnpjPrincipal = r.P.Estabelecimentos.Where(e => e.Principal).Select(e => e.Cnpj).FirstOrDefault(),
                QuantidadeEstabelecimentos = r.P.Estabelecimentos.Count,
                Situacao = r.P.Situacao,
                Papeis = db.Papeis
                    .Where(cadastro => r.P.Papeis.Any(x => x.Ativo && x.PapelId == cadastro.Id))
                    .OrderBy(cadastro => cadastro.Ordem)
                    .Select(cadastro => cadastro.Nome)
                    .ToList(),
                // Cidade/UF do endereço de referência da listagem (só exibição; ver ComReferencia).
                Cidade = r.Cidade,
                Uf = r.Uf,
                MunicipioACorrigir = db.PendenciasMunicipio.Any(x => x.PessoaId == r.P.Id && x.ResolvidaEm == null)
            });

    /// <summary>Pessoa + cidade/UF do endereço de referência da listagem (só exibição).</summary>
    internal sealed class PessoaComReferencia
    {
        public Pessoa P { get; init; } = null!;
        public string? Cidade { get; init; }
        public string? Uf { get; init; }
    }

    /// <summary>
    /// Endereço de referência da listagem (mesma regra de RegrasFinalidadeEndereco.EnderecoReferencia, no banco): o
    /// principal da finalidade ativa de menor ordem no cadastro; sem principal, o primeiro endereço ativo. Serve só para
    /// mostrar cidade/UF em listas e exportações; não é "o principal da pessoa" e não muda principalidade.
    /// A ordenação é TOTAL (último critério: o Id, único): cidade e UF, lidas com a mesma ordenação, vêm sempre do mesmo
    /// endereço — não há empate que deixe o banco escolher linhas diferentes para cada campo.
    /// </summary>
    internal static IQueryable<PessoaComReferencia> ComReferencia(IQueryable<Pessoa> consulta, LoneDbContext db) =>
        consulta.Select(p => new PessoaComReferencia
        {
            P = p,
            Cidade = p.Enderecos.Where(e => e.Ativo)
                .OrderBy(e => p.FinalidadesEnderecos
                    .Where(u => u.PessoaEnderecoId == e.Id && u.Principal && u.Ativo)
                    .Join(db.FinalidadesEndereco.Where(f => f.Ativo), u => u.FinalidadeId, f => f.Id, (u, f) => (int?)f.Ordem)
                    .Min() ?? int.MaxValue)
                .ThenBy(e => e.Ordem)
                .ThenBy(e => e.Id)
                .Select(e => e.Cidade).FirstOrDefault(),
            Uf = p.Enderecos.Where(e => e.Ativo)
                .OrderBy(e => p.FinalidadesEnderecos
                    .Where(u => u.PessoaEnderecoId == e.Id && u.Principal && u.Ativo)
                    .Join(db.FinalidadesEndereco.Where(f => f.Ativo), u => u.FinalidadeId, f => f.Id, (u, f) => (int?)f.Ordem)
                    .Min() ?? int.MaxValue)
                .ThenBy(e => e.Ordem)
                .ThenBy(e => e.Id)
                .Select(e => e.Uf).FirstOrDefault()
        });

    internal static IQueryable<Pessoa> AplicarBusca(IQueryable<Pessoa> consulta, string termo, LoneDbContext db)
    {
        var documento = termo.Any(char.IsAsciiDigit) ? Documento.Normalizar(termo) : string.Empty;
        var digitos = Documento.SomenteDigitos(termo);
        var buscaDigitos = digitos.Length >= 4;
        var codigo = int.TryParse(termo, out var c) ? c : -1;

        // Campos personalizados marcados como pesquisáveis (da pessoa e dos documentos): começo do texto, pelo índice
        // (CampoId, ValorTextoBusca). CPF/CNPJ ficam gravados sem máscara: procura também pelo termo normalizado.
        var inicio = termo.Length > ConfiguracaoValorPersonalizado.TamanhoBusca ? termo[..ConfiguracaoValorPersonalizado.TamanhoBusca] : termo;
        var inicioDocumento = documento.Length > 0 ? documento : inicio;
        var pesquisaveis = db.CamposPersonalizados.Where(x => x.Pesquisavel && x.Ativo).Select(x => x.Id);
        var numeroDocumento = termo.Trim().ToUpperInvariant();

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
            p.Documentos.Any(d => d.Numero.StartsWith(numeroDocumento)) ||
            p.ValoresPersonalizados.Any(v => pesquisaveis.Contains(v.CampoId) &&
                (EF.Property<string>(v, ConfiguracaoValorPersonalizado.ColunaBusca).StartsWith(inicio) ||
                 EF.Property<string>(v, ConfiguracaoValorPersonalizado.ColunaBusca).StartsWith(inicioDocumento))) ||
            p.ValoresDocumentos.Any(v => pesquisaveis.Contains(v.CampoId) &&
                (EF.Property<string>(v, ConfiguracaoValorPersonalizado.ColunaBusca).StartsWith(inicio) ||
                 EF.Property<string>(v, ConfiguracaoValorPersonalizado.ColunaBusca).StartsWith(inicioDocumento))) ||
            p.Contatos.Any(x =>
                x.Nome.Contains(termo) ||
                (x.Email != null && x.Email.Contains(termo)) ||
                (buscaDigitos && ((x.Telefone != null && x.Telefone.Contains(digitos)) ||
                                  (x.Celular != null && x.Celular.Contains(digitos))))));
    }

    public async Task<Pessoa?> ObterAsync(Guid id, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        // Sem escopo: por id, e toda rota com o id de uma pessoa passa antes pelo filtro de escopo da API (Fase 2a-2).
        return await db.Pessoas.AsNoTracking()
            .Include(p => p.Estabelecimentos)
            .Include(p => p.Documentos)
            .Include(p => p.Enderecos)
            .Include(p => p.FinalidadesEnderecos)
            .Include(p => p.MeiosContato)
            .Include(p => p.Contatos)
            .Include(p => p.Papeis)
            .Include(p => p.ContasCliente)
            .Include(p => p.ContasFornecedor)
            .Include(p => p.Socios)
            .Include(p => p.Etiquetas)
            .Include(p => p.ValoresPersonalizados)
            .Include(p => p.ValoresDocumentos)
            .Include(p => p.Vinculos)
            .Include(p => p.Lotacoes)
            .Include(p => p.ExcecoesComerciais)
            .Include(p => p.Carteira)
            .Include(p => p.HistoricoFiscal)
            .Include(p => p.Cnaes)
            .Include(p => p.Bloqueios)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<int> ContarClientesAtivosAsync(CancellationToken ct)
    {
        var escopo = await _escopo.ObterAsync(ct);
        await using var db = await AbrirAsync(ct);
        return await Consultas.EscopoPessoasSql.Pessoas(db, escopo).CountAsync(p =>
            (p.Situacao == SituacaoPessoa.Ativo || p.Situacao == SituacaoPessoa.EmAnalise) &&
            p.Papeis.Any(x => x.Papel == TipoPapel.Cliente && x.Ativo), ct);
    }

    public async Task<List<DateOnly?>> ListarNascimentosAsync(TipoPapel? papel, CancellationToken ct)
    {
        var escopo = await _escopo.ObterAsync(ct);
        await using var db = await AbrirAsync(ct);
        var consulta = Consultas.EscopoPessoasSql.Pessoas(db, escopo).Where(p =>
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
        // Sem escopo: a base inteira, de propósito (Fase 2a-2, E5): o documento não pode repetir nem fora do alcance.
        // Quem chama (PessoaAppService) esconde o nome e o código do que estiver fora dele.
        return await db.Pessoas.AsNoTracking()
            .Where(p => p.Natureza == natureza && p.DocumentoPrincipal == documento && p.Id != ignorarId)
            .Select(p => new PessoaIdentificacao(p.Id, p.Codigo, p.Nome))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<PessoaIdentificacao>> BuscarSemelhantesAsync(
        Guid ignorarId, string nome, IReadOnlyCollection<string> contatos, CancellationToken ct)
    {
        var escopo = await _escopo.ObterAsync(ct);
        await using var db = await AbrirAsync(ct);
        var valores = contatos.ToList();

        // Só as do alcance (Fase 2a-2): o aviso mostra nome e código.
        return await Consultas.EscopoPessoasSql.Pessoas(db, escopo)
            .Where(p => p.Id != ignorarId &&
                        (p.Nome == nome ||
                         p.MeiosContato.Any(m => m.Ativo && valores.Contains(m.Valor)) ||
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
            // Sem escopo: gravação (o cadastro novo com alcance restrito segue F4 e E4 no PessoaAppService).
            db.Pessoas.Add(pessoa);
        }
        else
        {
            await AplicarAlteracoesAsync(db, pessoa, ct);
        }

        await ResolverPendenciasCorrigidasAsync(db, pessoa, ct);

        // Ordem que o banco exige e o EF não garante entre UPDATEs (índice único filtrado e gatilhos):
        // 1. principais desmarcados primeiro (troca A -> B: A sai antes de B entrar; endereço desativado perde o
        //    principal antes de ficar inativo);
        // 2. endereços reativados em seguida (um principal marcado nele na mesma gravação encontra o endereço ativo).
        // Tudo na mesma transação da gravação: se a versão da pessoa não conferir, nada disso fica.
        var desmarcados = db.ChangeTracker.Entries<PessoaEnderecoFinalidade>()
            .Where(e => e.State == EntityState.Modified && e.OriginalValues.GetValue<bool>(nameof(PessoaEnderecoFinalidade.Principal)) && !e.Entity.Principal)
            .Select(e => e.Entity.Id)
            .ToList();
        var reativados = db.ChangeTracker.Entries<PessoaEndereco>()
            .Where(e => e.State == EntityState.Modified && !e.OriginalValues.GetValue<bool>(nameof(PessoaEndereco.Ativo)) && e.Entity.Ativo)
            .Select(e => e.Entity.Id)
            .ToList();
        // 3. carteira: o gatilho da carteira confere cada UPDATE, e o EF grava um vínculo por comando, sem ordem garantida.
        //    Cada vínculo alterado passa antes pela interseção do antes com o depois (nunca maior que nenhum dos dois):
        //    assim nenhum passo intermediário tem sobreposição se o estado final não tem.
        var carteiraIntermediaria = db.ChangeTracker.Entries<CarteiraCliente>()
            .Where(e => e.State == EntityState.Modified)
            .Select(PassoIntermediarioCarteira)
            .OfType<(Guid Id, DateOnly Inicio, DateOnly? Fim, bool Ativo, bool Exclusivo)>()
            .ToList();

        try
        {
            if (desmarcados.Count == 0 && reativados.Count == 0 && carteiraIntermediaria.Count == 0)
            {
                await db.SaveChangesAsync(ct);
                return;
            }

            await using var transacao = await db.Database.BeginTransactionAsync(ct);
            if (desmarcados.Count > 0)
                await db.PessoaEnderecoFinalidades.Where(u => desmarcados.Contains(u.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.Principal, false), ct);
            if (reativados.Count > 0)
                await db.PessoaEnderecos.Where(e => reativados.Contains(e.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.Ativo, true), ct);
            foreach (var (id, inicio, fim, ativo, exclusivo) in carteiraIntermediaria)
                await db.CarteiraClientes.Where(c => c.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.InicioEm, inicio).SetProperty(c => c.FimEm, fim)
                                              .SetProperty(c => c.Ativo, ativo).SetProperty(c => c.Exclusivo, exclusivo), ct);
            await db.SaveChangesAsync(ct); // usa a transação aberta; a auditoria registra as mudanças normalmente
            await transacao.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflitoDeEdicaoException(ex);
        }
        catch (Exception ex) when (ConflitosEnderecoFinalidade.Mensagem(ex) is { } mensagem)
        {
            // Índice único / gatilho barrou (ex.: outro usuário marcou outro principal ao mesmo tempo): 409 com mensagem
            // clara; o erro original vai junto (e para o log). Outros erros de banco seguem como erro inesperado.
            throw new ConflitoDeEdicaoException(mensagem, ex);
        }
    }

    /// <summary>
    /// Estado intermediário de um vínculo alterado: a interseção do período antes e depois, ativo só se estava e continua
    /// ativo no mesmo tipo e empresa, e exclusivo só se era e continua exclusivo. Vazio (ou mudou de tipo/empresa) = inativo
    /// por um instante, com as datas antigas.
    /// Nulo = nada a fazer antes (o vínculo só cresceu: a gravação final já é segura). Não passa pela auditoria: a
    /// gravação final registra o antes e o depois de verdade.
    /// </summary>
    private static (Guid Id, DateOnly Inicio, DateOnly? Fim, bool Ativo, bool Exclusivo)? PassoIntermediarioCarteira(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<CarteiraCliente> entrada)
    {
        var antes = entrada.OriginalValues;
        var depois = entrada.Entity;
        var ativoAntes = antes.GetValue<bool>(nameof(CarteiraCliente.Ativo));
        var inicioAntes = antes.GetValue<DateOnly>(nameof(CarteiraCliente.InicioEm));
        var fimAntes = antes.GetValue<DateOnly?>(nameof(CarteiraCliente.FimEm));
        var mesmoLugar = antes.GetValue<Guid>(nameof(CarteiraCliente.TipoCarteiraId)) == depois.TipoCarteiraId &&
                         antes.GetValue<Guid?>(nameof(CarteiraCliente.EmpresaId)) == depois.EmpresaId;

        var inicio = inicioAntes > depois.InicioEm ? inicioAntes : depois.InicioEm;
        DateOnly? fim = fimAntes is null ? depois.FimEm : depois.FimEm is null ? fimAntes : fimAntes < depois.FimEm ? fimAntes : depois.FimEm;
        var ativo = ativoAntes && depois.Ativo && mesmoLugar && (fim is null || fim >= inicio);

        var exclusivoAntes = antes.GetValue<bool>(nameof(CarteiraCliente.Exclusivo));
        var exclusivo = exclusivoAntes && depois.Exclusivo;

        if (!ativo) return ativoAntes ? (depois.Id, inicioAntes, fimAntes, false, exclusivoAntes) : null;
        if (inicio == inicioAntes && fim == fimAntes && exclusivo == exclusivoAntes) return null;
        return (depois.Id, inicio, fim, true, exclusivo);
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
                if (endereco is { Ativo: false } && municipio is null && endereco.EhBrasil)
                {
                    // Endereço desativado (removido ou consolidado) sem o município corrigido: a pendência é ENCERRADA,
                    // não "corrigida" — fica registrado que o texto antigo nunca foi ligado ao IBGE.
                    pendencia.ResolvidaEm = DateTime.UtcNow;
                    pendencia.ResolvidaPor = db.Usuario;
                    pendencia.Observacao = EncerradaPorInativacao;
                    continue;
                }
                corrigida = endereco is null || municipio is not null || !endereco.EhBrasil;
            }

            if (!corrigida) continue;
            pendencia.MunicipioId = municipio;
            pendencia.ResolvidaEm = DateTime.UtcNow;
            pendencia.ResolvidaPor = db.Usuario;
            pendencia.Observacao = null;
        }

        // Endereço reativado ainda sem município da tabela: a pendência encerrada pela desativação volta a valer.
        var reativadosSemMunicipio = pessoa.Enderecos.Where(e => e.Ativo && e.EhBrasil && e.MunicipioId is null).Select(e => e.Id).ToList();
        if (reativadosSemMunicipio.Count == 0) return;
        var encerradas = await db.PendenciasMunicipio
            .Where(p => p.PessoaId == pessoa.Id && p.Origem == OrigemPendenciaMunicipio.Endereco && p.ResolvidaEm != null &&
                        p.Observacao == EncerradaPorInativacao && reativadosSemMunicipio.Contains(p.RegistroId))
            .ToListAsync(ct);
        foreach (var pendencia in encerradas)
        {
            pendencia.ResolvidaEm = null;
            pendencia.ResolvidaPor = null;
            pendencia.Observacao = "Reaberta: endereço reativado sem município da tabela do IBGE.";
        }
    }

    /// <summary>Observação da pendência de município encerrada porque o endereço saiu de uso (não foi corrigida).</summary>
    internal const string EncerradaPorInativacao = "Encerrada sem correção: endereço desativado.";

    /// <summary>
    /// Carrega a pessoa do banco e aplica só o que mudou (é isso que alimenta a auditoria).
    /// A versão que o usuário abriu vira a "versão original": se o banco tiver outra, a gravação falha.
    /// </summary>
    private static async Task AplicarAlteracoesAsync(LoneDbContext db, Pessoa dados, CancellationToken ct)
    {
        // Sem escopo: gravação de um cadastro que a rota já conferiu (filtro de escopo da API).
        var atual = await db.Pessoas
            .Include(p => p.Estabelecimentos)
            .Include(p => p.Documentos)
            .Include(p => p.Enderecos)
            .Include(p => p.FinalidadesEnderecos)
            .Include(p => p.MeiosContato)
            .Include(p => p.Contatos)
            .Include(p => p.Papeis)
            .Include(p => p.ContasCliente)
            .Include(p => p.ContasFornecedor)
            .Include(p => p.Socios)
            .Include(p => p.Etiquetas)
            .Include(p => p.ValoresPersonalizados)
            .Include(p => p.ValoresDocumentos)
            .Include(p => p.Vinculos)
            .Include(p => p.Lotacoes)
            .Include(p => p.ExcecoesComerciais)
            .Include(p => p.Carteira)
            .Include(p => p.HistoricoFiscal)
            .Include(p => p.Cnaes)
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
        SincronizarFilhos(db, atual.Id, atual.Enderecos, dados.Enderecos, apagarAusentes: false); // removidos ficam inativos

        // Endereço × finalidade: casa pelo par (endereço, finalidade), nunca apaga (retirada = inativa, histórico).
        ReaproveitarIds(atual.FinalidadesEnderecos, dados.FinalidadesEnderecos, u => (u.PessoaEnderecoId, u.FinalidadeId));
        SincronizarFilhos(db, atual.Id, atual.FinalidadesEnderecos, dados.FinalidadesEnderecos, apagarAusentes: false);
        SincronizarFilhos(db, atual.Id, atual.Estabelecimentos, dados.Estabelecimentos, apagarAusentes: false); // removidos ficam inativos
        SincronizarFilhos(db, atual.Id, atual.Documentos, dados.Documentos, apagarAusentes: false); // removidos ficam inativos
        SincronizarFilhos(db, atual.Id, atual.MeiosContato, dados.MeiosContato, apagarAusentes: false); // removidos ficam inativos
        SincronizarFilhos(db, atual.Id, atual.Contatos, dados.Contatos);
        SincronizarFilhos(db, atual.Id, atual.Papeis, dados.Papeis, apagarAusentes: false); // períodos nunca são apagados
        SincronizarFilhos(db, atual.Id, atual.ContasCliente, dados.ContasCliente);
        SincronizarFilhos(db, atual.Id, atual.ContasFornecedor, dados.ContasFornecedor);
        SincronizarFilhos(db, atual.Id, atual.Socios, dados.Socios);

        // Etiqueta é única por pessoa: casa pela etiqueta, não pelo Id, para não apagar e incluir de novo o mesmo registro
        // (histórico limpo e sem conflito no índice único). Consentimentos NÃO passam pelo Salvar da ficha (Fase 3):
        // são gravados só pelas ações de conceder/revogar, em períodos (PrivacidadeRepositorio).
        ReaproveitarIds(atual.Etiquetas, dados.Etiquetas, e => e.EtiquetaId);
        SincronizarFilhos(db, atual.Id, atual.Etiquetas, dados.Etiquetas);

        // Um valor por campo personalizado: casa pelo campo, e a mudança aparece no histórico como alteração.
        ReaproveitarIds(atual.ValoresPersonalizados, dados.ValoresPersonalizados, v => v.CampoId);
        SincronizarFilhos(db, atual.Id, atual.ValoresPersonalizados, dados.ValoresPersonalizados);
        ReaproveitarIds(atual.ValoresDocumentos, dados.ValoresDocumentos, v => (v.PessoaDocumentoId, v.CampoId));
        SincronizarFilhos(db, atual.Id, atual.ValoresDocumentos, dados.ValoresDocumentos);

        // Colaborador: vínculos e lotações são histórico — nunca apagados (desligamento e fim de período encerram).
        SincronizarFilhos(db, atual.Id, atual.Vinculos, dados.Vinculos, apagarAusentes: false);
        SincronizarFilhos(db, atual.Id, atual.Lotacoes, dados.Lotacoes, apagarAusentes: false);

        // Comercial: exceções e carteira são histórico — nunca apagadas (encerram pelo fim).
        SincronizarFilhos(db, atual.Id, atual.ExcecoesComerciais, dados.ExcecoesComerciais, apagarAusentes: false);
        SincronizarFilhos(db, atual.Id, atual.Carteira, dados.Carteira, apagarAusentes: false);

        // Fiscal: o histórico nunca é apagado; a tabela de CNAEs é cópia dos campos de texto (acompanha o que está neles).
        SincronizarFilhos(db, atual.Id, atual.HistoricoFiscal, dados.HistoricoFiscal, apagarAusentes: false);
        SincronizarFilhos(db, atual.Id, atual.Cnaes, dados.Cnaes);

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
