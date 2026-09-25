using System.Text;
using System.Text.Json;
using Lone.Application.Seguranca;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Validacao;

namespace Lone.Application.Consultas;

/// <summary>Consulta no banco (Infraestrutura): monta a consulta com LINQ a partir dos critérios tipados.</summary>
public interface IConsultaPessoas
{
    Task<PaginaPessoas> ConsultarAsync(ConsultaPessoasRequisicao requisicao, DateOnly hoje, CancellationToken ct);

    /// <summary>Linhas para exportar (até <paramref name="limite"/>), já com as colunas do CSV.</summary>
    Task<List<string[]>> LinhasParaExportarAsync(CriteriosPessoas criterios, DateOnly hoje, int limite, CancellationToken ct);

    /// <summary>Evento na auditoria (quem exportou, quantas linhas, critérios) — LGPD.</summary>
    Task RegistrarExportacaoAsync(string descricao, CancellationToken ct);

    Task<OpcoesConsultaPessoasDto> OpcoesAsync(DateOnly hoje, CancellationToken ct);
}

public interface IFiltroSalvoRepositorio
{
    Task<List<FiltroSalvo>> ListarVisiveisAsync(Guid usuarioId, CancellationToken ct);
    Task<FiltroSalvo?> ObterAsync(Guid id, CancellationToken ct);
    Task SalvarAsync(FiltroSalvo filtro, bool novo, CancellationToken ct);
}

public interface IConsultaPessoasAppService
{
    Task<PaginaPessoas> ConsultarAsync(ConsultaPessoasRequisicao requisicao, CancellationToken ct = default);
    Task<ArquivoExportado> ExportarAsync(CriteriosPessoas criterios, CancellationToken ct = default);
    Task<OpcoesConsultaPessoasDto> OpcoesAsync(CancellationToken ct = default);
    Task<FiltroSalvoDto> SalvarFiltroAsync(FiltroSalvoDto dto, CancellationToken ct = default);
    Task DesativarFiltroAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Consulta avançada de pessoas (Fase 12): critérios tipados, paginação por chave (nome + Id), exportação CSV com
/// permissão própria e registro na auditoria, filtros salvos por usuário (com opção de compartilhar).
/// </summary>
public sealed class ConsultaPessoasAppService : IConsultaPessoasAppService
{
    /// <summary>Teto da exportação: acima disso, refinar o filtro (evita arquivos enormes e consultas pesadas).</summary>
    public const int LimiteExportacao = 50_000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IConsultaPessoas _consulta;
    private readonly IFiltroSalvoRepositorio _filtros;
    private readonly IAutorizacao _autorizacao;
    private readonly IUsuarioAtual _usuario;
    private readonly TimeProvider _relogio;

    public ConsultaPessoasAppService(IConsultaPessoas consulta, IFiltroSalvoRepositorio filtros, IAutorizacao autorizacao,
                                     IUsuarioAtual usuario, TimeProvider relogio)
    {
        _consulta = consulta;
        _filtros = filtros;
        _autorizacao = autorizacao;
        _usuario = usuario;
        _relogio = relogio;
    }

    private DateOnly Hoje => DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime);

    public Task<PaginaPessoas> ConsultarAsync(ConsultaPessoasRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        requisicao.Criterios = Normalizar(requisicao.Criterios);
        requisicao.Limite = Math.Clamp(requisicao.Limite, 1, ConsultaPessoasRequisicao.LimiteMaximo);
        return _consulta.ConsultarAsync(requisicao, Hoje, ct);
    }

    public async Task<ArquivoExportado> ExportarAsync(CriteriosPessoas criterios, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        _autorizacao.Exigir(Permissoes.Pessoas.Exportar);
        criterios = Normalizar(criterios);

        var linhas = await _consulta.LinhasParaExportarAsync(criterios, Hoje, LimiteExportacao + 1, ct);
        if (linhas.Count > LimiteExportacao)
            throw new ValidacaoException([$"A consulta passa de {LimiteExportacao:N0} pessoas: refine os critérios para exportar."]);

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(';', Colunas));
        foreach (var l in linhas) csv.AppendLine(string.Join(';', l.Select(Celula)));

        await _consulta.RegistrarExportacaoAsync(
            $"Exportação CSV da consulta de pessoas: {linhas.Count} linha(s). Critérios: {Resumo(criterios)}", ct);

        return new ArquivoExportado
        {
            NomeArquivo = $"pessoas-{_relogio.GetLocalNow():yyyyMMdd-HHmm}.csv",
            Conteudo = csv.ToString(),
            Linhas = linhas.Count
        };
    }

    public static readonly string[] Colunas =
        ["Código", "Nome", "Natureza", "CPF/CNPJ", "Situação", "Papéis", "Cidade", "UF", "Telefone", "E-mail", "Cadastrado em"];

    /// <summary>Célula CSV: aspas quando precisa; fórmula (=, +, -, @) vira texto (evita injeção de fórmula no Excel).</summary>
    public static string Celula(string? valor)
    {
        var v = valor ?? string.Empty;
        if (v.Length > 0 && "=+-@\t\r".Contains(v[0])) v = "'" + v;
        return v.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }

    private static string Resumo(CriteriosPessoas c)
    {
        var json = JsonSerializer.Serialize(c, Json);
        return json.Length > 800 ? json[..800] + "…" : json;
    }

    public async Task<OpcoesConsultaPessoasDto> OpcoesAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var opcoes = await _consulta.OpcoesAsync(Hoje, ct);
        var usuario = _usuario.Id ?? Guid.Empty;
        opcoes.Filtros = (await _filtros.ListarVisiveisAsync(usuario, ct))
            .OrderBy(f => f.UsuarioId != usuario).ThenBy(f => f.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(f => ParaDto(f, usuario)).ToList();
        return opcoes;
    }

    public async Task<FiltroSalvoDto> SalvarFiltroAsync(FiltroSalvoDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var usuario = _usuario.Id ?? throw new ValidacaoException(["Entre com um usuário para salvar filtros."]);
        var anterior = dto.Id == Guid.Empty ? null : await _filtros.ObterAsync(dto.Id, ct);
        if (anterior is not null && anterior.UsuarioId != usuario)
            throw new ValidacaoException(["Só quem criou o filtro pode alterá-lo. Salve uma cópia com outro nome."]);

        var criterios = JsonSerializer.Serialize(Normalizar(dto.Criterios), Json);
        var nome = (dto.Nome ?? string.Empty).Trim();
        var erros = new List<string>();
        if (nome.Length == 0) erros.Add("Dê um nome ao filtro.");
        else if (nome.Length > FiltroSalvo.TamanhoMaximoNome) erros.Add($"O nome pode ter no máximo {FiltroSalvo.TamanhoMaximoNome} caracteres.");
        if (criterios.Length > FiltroSalvo.TamanhoMaximoCriterios) erros.Add("Critérios demais para salvar (use menos itens nas listas).");
        var visiveis = await _filtros.ListarVisiveisAsync(usuario, ct);
        if (visiveis.Any(f => f.Id != dto.Id && f.UsuarioId == usuario && TextoBusca.Normalizar(f.Nome) == TextoBusca.Normalizar(nome)))
            erros.Add($"Você já tem um filtro \"{nome}\".");
        if (erros.Count > 0) throw new ValidacaoException(erros);

        var filtro = new FiltroSalvo
        {
            Id = dto.Id == Guid.Empty ? IdSequencial.Novo() : dto.Id,
            Versao = dto.Versao,
            Nome = nome,
            UsuarioId = usuario,
            Autor = anterior?.Autor ?? (_usuario.Nome.Length > 100 ? _usuario.Nome[..100] : _usuario.Nome),
            Compartilhado = dto.Compartilhado,
            Criterios = criterios,
            Ativo = true
        };
        if (anterior is null) filtro.RegistrarEvento($"Filtro '{nome}' criado.");
        await _filtros.SalvarAsync(filtro, anterior is null, ct);
        return ParaDto((await _filtros.ObterAsync(filtro.Id, ct))!, usuario);
    }

    public async Task DesativarFiltroAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var usuario = _usuario.Id ?? Guid.Empty;
        var filtro = await _filtros.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este filtro não existe mais."]);
        if (filtro.UsuarioId != usuario) throw new ValidacaoException(["Só quem criou o filtro pode removê-lo."]);
        if (!filtro.Ativo) return;
        filtro.Ativo = false;
        filtro.RegistrarEvento($"Filtro '{filtro.Nome}' removido.");
        await _filtros.SalvarAsync(filtro, novo: false, ct);
    }

    private static FiltroSalvoDto ParaDto(FiltroSalvo f, Guid usuario) => new()
    {
        Id = f.Id, Versao = f.Versao, Nome = f.Nome, Compartilhado = f.Compartilhado, Autor = f.Autor, Proprio = f.UsuarioId == usuario,
        Criterios = LerCriterios(f.Criterios)
    };

    /// <summary>Critério gravado que não se lê mais (versão antiga) vira "sem critérios" em vez de erro.</summary>
    public static CriteriosPessoas LerCriterios(string json)
    {
        try { return JsonSerializer.Deserialize<CriteriosPessoas>(json, Json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    /// <summary>Limpa e confere os critérios (textos, dias e datas em faixa razoável).</summary>
    public static CriteriosPessoas Normalizar(CriteriosPessoas? c)
    {
        c ??= new();
        var erros = new List<string>();
        c.Texto = string.IsNullOrWhiteSpace(c.Texto) ? null : c.Texto.Trim();
        c.Uf = string.IsNullOrWhiteSpace(c.Uf) ? null : c.Uf.Trim().ToUpperInvariant();
        if (c.Uf is { Length: not 2 }) erros.Add("UF inválida (use a sigla, ex.: SP).");
        c.Cnae = string.IsNullOrWhiteSpace(c.Cnae) ? null : new string(c.Cnae.Where(char.IsAsciiDigit).ToArray());
        if (c.Cnae is { Length: 0 or > 7 }) erros.Add("CNAE inválido (até 7 dígitos, ex.: 4711302 ou 47).");
        c.CampoValor = string.IsNullOrWhiteSpace(c.CampoValor) ? null : c.CampoValor.Trim();
        if (c.CampoValor is not null && c.CampoId is null) erros.Add("Escolha o campo personalizado do valor.");
        if (c.SemInteracaoDias is < 1 or > 3650) erros.Add("\"Sem interação há\" deve ficar entre 1 e 3.650 dias.");
        if (c.DocumentosVencendoDias is < 1 or > 3650) erros.Add("\"Documentos vencendo em\" deve ficar entre 1 e 3.650 dias.");
        if (c.CadastradoDe is { } de && c.CadastradoAte is { } ate && ate < de) erros.Add("O fim do período de cadastro é anterior ao início.");
        if (c.VendedorId is not null && c.SemCarteira) erros.Add("Escolha um vendedor ou \"sem carteira\", não os dois.");
        c.PapeisIds = c.PapeisIds.Distinct().Take(20).ToList();
        c.EtiquetasIds = c.EtiquetasIds.Distinct().Take(20).ToList();
        c.Naturezas = c.Naturezas.Distinct().ToList();
        c.Situacoes = c.Situacoes.Distinct().ToList();
        if (erros.Count > 0) throw new ValidacaoException(erros);
        return c;
    }
}
