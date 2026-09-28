using System.Globalization;
using System.Linq.Expressions;
using Lone.Contracts.Pessoas;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Consultas;

/// <summary>
/// Valor e ordenação no banco de cada coluna da lista de pessoas (ColunasListaPessoas), pelo mesmo Id. Tudo em LINQ:
/// a ordenação vai para o ORDER BY (a paginação continua certa) e os valores das colunas extras são lidos só para as
/// linhas da página (uma consulta pequena por coluna). Mesmas regras de "principal" da ficha e da lista:
/// estabelecimento principal, telefone/e-mail marcado como principal, endereço de referência da listagem.
/// </summary>
public static class ColunasPessoasSql
{
    public sealed record Contexto(LoneDbContext Db, DateOnly Hoje);

    /// <summary>Linha lida de uma coluna extra (Id da pessoa + valor).</summary>
    public sealed class ValorColuna<T>
    {
        public Guid Id { get; set; }
        public T Valor { get; set; } = default!;
    }

    private abstract class Coluna
    {
        public abstract IOrderedQueryable<Pessoa> Ordenar(IQueryable<Pessoa> q, bool decrescente, Contexto x);
        public abstract Task<Dictionary<Guid, string?>> LerAsync(IQueryable<Pessoa> q, Contexto x, CancellationToken ct);
    }

    /// <param name="ordem">Chave de ordenação própria (ex.: opções na ordem alfabética do texto); nulo = o próprio valor.</param>
    private sealed class Coluna<T>(Func<Contexto, Expression<Func<Pessoa, T>>> valor, Func<T, string?> texto,
        Func<Contexto, Expression<Func<Pessoa, int>>>? ordem = null) : Coluna
    {
        public override IOrderedQueryable<Pessoa> Ordenar(IQueryable<Pessoa> q, bool decrescente, Contexto x) =>
            ordem is not null
                ? decrescente ? q.OrderByDescending(ordem(x)) : q.OrderBy(ordem(x))
                : decrescente ? q.OrderByDescending(valor(x)) : q.OrderBy(valor(x));

        public override async Task<Dictionary<Guid, string?>> LerAsync(IQueryable<Pessoa> q, Contexto x, CancellationToken ct)
        {
            var campo = valor(x);
            var p = campo.Parameters[0];
            var tipo = typeof(ValorColuna<T>);
            var linha = Expression.Lambda<Func<Pessoa, ValorColuna<T>>>(
                Expression.MemberInit(Expression.New(tipo),
                    Expression.Bind(tipo.GetProperty(nameof(ValorColuna<T>.Id))!, Expression.Property(p, nameof(Pessoa.Id))),
                    Expression.Bind(tipo.GetProperty(nameof(ValorColuna<T>.Valor))!, campo.Body)),
                p);
            var lidos = await q.Select(linha).ToListAsync(ct);
            return lidos.ToDictionary(l => l.Id, l => texto(l.Valor));
        }
    }

    private static Coluna Texto(Func<Contexto, Expression<Func<Pessoa, string?>>> valor) =>
        new Coluna<string?>(valor, v => string.IsNullOrWhiteSpace(v) ? null : v);

    private static Coluna Dia(Func<Contexto, Expression<Func<Pessoa, DateOnly?>>> valor) =>
        new Coluna<DateOnly?>(valor, v => v?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    /// <summary>Instante gravado em UTC mostrado como o dia local.</summary>
    private static Coluna DiaDoInstante(Func<Contexto, Expression<Func<Pessoa, DateTime?>>> valor) =>
        new Coluna<DateTime?>(valor, v => v is { } d
            ? DateOnly.FromDateTime(DateTime.SpecifyKind(d, DateTimeKind.Utc).ToLocalTime()).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null);

    private static Coluna Numero(Func<Contexto, Expression<Func<Pessoa, decimal?>>> valor) =>
        new Coluna<decimal?>(valor, v => v?.ToString(CultureInfo.InvariantCulture));

    private static Coluna Opcao<TEnum>(string campo, Func<Contexto, Expression<Func<Pessoa, TEnum>>> valor) where TEnum : struct, Enum =>
        new Coluna<TEnum>(valor, v => v.ToString(), x => OrdemDoTexto<TEnum>(campo, valor(x)));

    /// <summary>Opção que a linha já traz (natureza, situação): só a ordenação, pelo texto.</summary>
    private static Coluna SoOrdenaOpcao<TEnum>(string campo, Expression<Func<Pessoa, TEnum>> valor) where TEnum : struct, Enum =>
        new Coluna<TEnum>(_ => valor, _ => null, _ => OrdemDoTexto<TEnum>(campo, valor));

    private static readonly StringComparer Alfabetica = StringComparer.Create(new CultureInfo("pt-BR"), ignoreCase: true);

    /// <summary>
    /// Ordem alfabética do texto que a tela mostra (o do catálogo de filtros), não a do código do enum: vira um CASE no
    /// banco ("Casado" = 0, "Divorciado" = 1...). Valor nulo ou sem texto vai para o fim.
    /// </summary>
    private static Expression<Func<Pessoa, int>> OrdemDoTexto<TEnum>(string campo, LambdaExpression valor) where TEnum : struct, Enum
    {
        var opcoes = global::Lone.Application.Consultas.CatalogoFiltrosPessoas.Campos.FirstOrDefault(c => c.Id == campo)?.Opcoes ?? [];
        string Texto(TEnum v) => opcoes.FirstOrDefault(o => o.Valor == v.ToString())?.Texto ?? v.ToString();
        var ordenados = Enum.GetValues<TEnum>().OrderBy(Texto, Alfabetica).ToList();
        Expression corpo = Expression.Constant(ordenados.Count);
        for (var i = ordenados.Count - 1; i >= 0; i--)
            corpo = Expression.Condition(
                Expression.Equal(valor.Body, Expression.Constant(ordenados[i], valor.Body.Type)),
                Expression.Constant(i),
                corpo);
        return Expression.Lambda<Func<Pessoa, int>>(corpo, valor.Parameters);
    }

    /// <summary>Colunas que a linha já traz (PessoaResumo): aqui só a ordenação.</summary>
    private static Coluna SoOrdena<T>(Func<Contexto, Expression<Func<Pessoa, T>>> valor) => new Coluna<T>(valor, _ => null);

    private static readonly Dictionary<string, Coluna> PorId = new(StringComparer.Ordinal)
    {
        // ---- Colunas que a linha já traz ----
        [ColunasPessoas.Nome] = SoOrdena(_ => PessoaRepositorio.NomeParaExibirNoBanco),
        [CamposFiltroPessoas.Codigo] = SoOrdena<int>(_ => p => p.Codigo),
        [CamposFiltroPessoas.Documento] = SoOrdena<string?>(_ => p =>
            p.DocumentoPrincipal ?? p.Estabelecimentos.Where(e => e.Principal).Select(e => e.Cnpj).FirstOrDefault()),
        [CamposFiltroPessoas.Natureza] = SoOrdenaOpcao<NaturezaPessoa>(CamposFiltroPessoas.Natureza, p => p.Natureza),
        [CamposFiltroPessoas.Papeis] = SoOrdena<string?>(x => p => x.Db.Papeis
            .Where(cadastro => p.Papeis.Any(y => y.Ativo && y.PapelId == cadastro.Id))
            .OrderBy(cadastro => cadastro.Ordem)
            .Select(cadastro => cadastro.Nome)
            .FirstOrDefault()),
        [CamposFiltroPessoas.Cidade] = SoOrdena<string?>(CidadeReferencia),
        [CamposFiltroPessoas.Situacao] = SoOrdenaOpcao<SituacaoPessoa>(CamposFiltroPessoas.Situacao, p => p.Situacao),

        // ---- Identificação ----
        [CamposFiltroPessoas.NomeFantasia] = Texto(_ => p => p.Estabelecimentos.Where(e => e.Principal).Select(e => e.NomeFantasia).FirstOrDefault()),
        [CamposFiltroPessoas.DataNascimento] = Dia(_ => p => p.DataNascimento),
        [CamposFiltroPessoas.DataAbertura] = Dia(_ => p => p.DataAbertura),
        [CamposFiltroPessoas.Porte] = Texto(_ => p => p.Porte),
        [CamposFiltroPessoas.GrupoEmpresarial] = Texto(x => p =>
            x.Db.GruposEmpresariais.Where(g => g.Id == p.GrupoEmpresarialId).Select(g => g.Nome).FirstOrDefault()),

        // ---- Dados pessoais ----
        [CamposFiltroPessoas.Sexo] = Opcao<SexoRegistro>(CamposFiltroPessoas.Sexo, _ => p => p.Sexo),
        [CamposFiltroPessoas.EstadoCivil] = Opcao<EstadoCivil>(CamposFiltroPessoas.EstadoCivil, _ => p => p.EstadoCivil),
        [CamposFiltroPessoas.Profissao] = Texto(x => p =>
            x.Db.Profissoes.Where(o => o.Id == p.ProfissaoId).Select(o => o.Nome).FirstOrDefault()),

        // ---- Telefones e e-mails: o marcado como principal; sem principal, o primeiro cadastrado ----
        [CamposFiltroPessoas.Telefone] = Texto(x => p => x.Db.MeiosContato
            .Where(m => m.PessoaId == p.Id && m.Ativo && m.Tipo != TipoContato.Email && m.Valor != "")
            .OrderByDescending(m => m.Principal).ThenBy(m => m.CriadoEm).ThenBy(m => m.Id)
            .Select(m => m.Valor).FirstOrDefault()),
        [CamposFiltroPessoas.Email] = Texto(x => p => x.Db.MeiosContato
            .Where(m => m.PessoaId == p.Id && m.Ativo && m.Tipo == TipoContato.Email && m.Valor != "")
            .OrderByDescending(m => m.Principal).ThenBy(m => m.CriadoEm).ThenBy(m => m.Id)
            .Select(m => m.Valor).FirstOrDefault()),

        // ---- Endereços: o endereço de referência da listagem (o mesmo da cidade) ----
        [CamposFiltroPessoas.Bairro] = Texto(BairroReferencia),
        [CamposFiltroPessoas.Cep] = Texto(CepReferencia),

        // ---- Fiscal: estabelecimento principal ----
        [CamposFiltroPessoas.Regime] = new Coluna<RegimeTributario?>(RegimePrincipal, v => v?.ToString(),
            x => OrdemDoTexto<RegimeTributario>(CamposFiltroPessoas.Regime, RegimePrincipal(x))),
        [CamposFiltroPessoas.InscricaoEstadual] = Texto(_ => p =>
            p.Estabelecimentos.Where(e => e.Principal).Select(e => e.InscricaoEstadual).FirstOrDefault()),
        [CamposFiltroPessoas.SituacaoReceita] = Texto(_ => p =>
            p.Estabelecimentos.Where(e => e.Principal).Select(e => e.SituacaoReceita).FirstOrDefault()),
        [CamposFiltroPessoas.CnaePrincipal] = Texto(_ => p =>
            p.Estabelecimentos.Where(e => e.Principal).Select(e => e.CnaePrincipal).FirstOrDefault()),

        // ---- Comercial – Cliente ----
        // Vendedor da carteira vigente hoje (a exclusiva primeiro; depois a mais antiga).
        [CamposFiltroPessoas.Vendedor] = Texto(x =>
        {
            var hoje = x.Hoje;
            return p => x.Db.CarteiraClientes
                .Where(y => y.PessoaId == p.Id && y.Ativo && y.InicioEm <= hoje && (y.FimEm == null || y.FimEm >= hoje))
                .OrderByDescending(y => y.Exclusivo).ThenBy(y => y.InicioEm).ThenBy(y => y.Id)
                // Sem escopo: nomes e colunas das linhas da página, que já passou pelo escopo.
                .Select(y => x.Db.Pessoas.Where(v => v.Id == y.VendedorId).Select(v => v.NomeExibicao ?? v.Nome).FirstOrDefault())
                .FirstOrDefault();
        }),
        // Conta geral do cliente (sem empresa); sem ela, a primeira por empresa.
        [CamposFiltroPessoas.LimiteCredito] = Numero(x => p => x.Db.ContasCliente
            .Where(y => y.PessoaId == p.Id)
            .OrderBy(y => y.EmpresaId == null ? 0 : 1).ThenBy(y => y.Id)
            .Select(y => y.LimiteCredito).FirstOrDefault()),
        [CamposFiltroPessoas.PerfilComercial] = Texto(x => p => x.Db.ContasCliente
            .Where(y => y.PessoaId == p.Id && y.PerfilComercialId != null)
            .OrderBy(y => y.EmpresaId == null ? 0 : 1).ThenBy(y => y.Id)
            .Select(y => x.Db.PerfisComerciais.Where(f => f.Id == y.PerfilComercialId).Select(f => f.Nome).FirstOrDefault())
            .FirstOrDefault()),

        // ---- Interações ----
        [CamposFiltroPessoas.Origem] = Texto(_ => p => p.OrigemCadastro),

        // ---- Cadastro ----
        [CamposFiltroPessoas.CadastradoEm] = DiaDoInstante(_ => p => (DateTime?)p.CriadoEm),
        [CamposFiltroPessoas.AlteradoEm] = DiaDoInstante(_ => p => p.AtualizadoEm)
    };

    /// <summary>Colunas com valor/ordenação no banco (um teste confere contra ColunasListaPessoas).</summary>
    public static IReadOnlyCollection<string> Implementadas => PorId.Keys;

    /// <summary>Ordena pela coluna (nula = nenhuma ordenação pedida).</summary>
    public static IOrderedQueryable<Pessoa>? Ordenar(IQueryable<Pessoa> q, OrdenacaoLista? ordenacao, Contexto x) =>
        ordenacao is not null && PorId.TryGetValue(ordenacao.Coluna, out var coluna)
            ? coluna.Ordenar(q, ordenacao.Direcao == DirecaoOrdenacao.Decrescente, x)
            : null;

    /// <summary>Lê as colunas extras das linhas da página e põe em PessoaResumo.Valores.</summary>
    public static async Task PreencherAsync(List<PessoaResumo> linhas, IReadOnlyList<string> colunas, Contexto x, CancellationToken ct)
    {
        if (linhas.Count == 0) return;
        var ids = linhas.Select(l => l.Id).ToList();
        var porId = linhas.ToDictionary(l => l.Id);
        foreach (var id in colunas)
        {
            if (!PorId.TryGetValue(id, out var coluna)) continue;
            // Sem escopo: nomes e colunas das linhas da página, que já passou pelo escopo.
            var valores = await coluna.LerAsync(x.Db.Pessoas.AsNoTracking().Where(p => ids.Contains(p.Id)), x, ct);
            foreach (var (pessoa, valor) in valores)
                if (porId.TryGetValue(pessoa, out var linha)) linha.Valores[id] = valor;
        }
    }

    // ---- Endereço de referência (mesma regra de PessoaRepositorio.ComReferencia) ----
    // Escrito por campo (cidade, bairro, CEP) para o banco receber a mesma subconsulta simples de sempre.

    private static Expression<Func<Pessoa, RegimeTributario?>> RegimePrincipal(Contexto _) => p =>
        p.Estabelecimentos.Where(e => e.Principal).Select(e => (RegimeTributario?)e.RegimeTributario).FirstOrDefault();

    private static Expression<Func<Pessoa, string?>> CidadeReferencia(Contexto x) => p => p.Enderecos.Where(e => e.Ativo)
        .OrderBy(e => p.FinalidadesEnderecos
            .Where(u => u.PessoaEnderecoId == e.Id && u.Principal && u.Ativo)
            .Join(x.Db.FinalidadesEndereco.Where(f => f.Ativo), u => u.FinalidadeId, f => f.Id, (u, f) => (int?)f.Ordem)
            .Min() ?? int.MaxValue)
        .ThenBy(e => e.Ordem)
        .ThenBy(e => e.Id)
        .Select(e => e.Cidade).FirstOrDefault();

    private static Expression<Func<Pessoa, string?>> BairroReferencia(Contexto x) => p => p.Enderecos.Where(e => e.Ativo)
        .OrderBy(e => p.FinalidadesEnderecos
            .Where(u => u.PessoaEnderecoId == e.Id && u.Principal && u.Ativo)
            .Join(x.Db.FinalidadesEndereco.Where(f => f.Ativo), u => u.FinalidadeId, f => f.Id, (u, f) => (int?)f.Ordem)
            .Min() ?? int.MaxValue)
        .ThenBy(e => e.Ordem)
        .ThenBy(e => e.Id)
        .Select(e => e.Bairro).FirstOrDefault();

    private static Expression<Func<Pessoa, string?>> CepReferencia(Contexto x) => p => p.Enderecos.Where(e => e.Ativo)
        .OrderBy(e => p.FinalidadesEnderecos
            .Where(u => u.PessoaEnderecoId == e.Id && u.Principal && u.Ativo)
            .Join(x.Db.FinalidadesEndereco.Where(f => f.Ativo), u => u.FinalidadeId, f => f.Id, (u, f) => (int?)f.Ordem)
            .Min() ?? int.MaxValue)
        .ThenBy(e => e.Ordem)
        .ThenBy(e => e.Id)
        .Select(e => e.Cep).FirstOrDefault();
}
