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

    /// <summary>Escolhas dos campos do filtro que vêm do banco, por fonte (CatalogoFiltrosPessoas.FontesDoBanco).</summary>
    Task<Dictionary<string, List<OpcaoFiltroDto>>> OpcoesFiltroAsync(CancellationToken ct);

    /// <summary>Quantas pessoas cada critério traz (mesma regra da consulta), na ordem da lista.</summary>
    Task<List<int>> ContarAsync(IReadOnlyList<CriteriosPessoas> criterios, DateOnly hoje, CancellationToken ct);
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

    /// <summary>Campos do filtro (catálogo), só os que o usuário pode usar, com as escolhas de cada um.</summary>
    Task<CatalogoFiltrosPessoasDto> CatalogoAsync(CancellationToken ct = default);

    Task<FiltroSalvoDto> SalvarFiltroAsync(FiltroSalvoDto dto, CancellationToken ct = default);
    Task DesativarFiltroAsync(Guid id, CancellationToken ct = default);

    /// <summary>Quantas pessoas cada visão (visível ao usuário) traz: contador das abas de visão da lista.</summary>
    Task<Dictionary<Guid, int>> ContarFiltrosAsync(List<Guid> ids, CancellationToken ct = default);
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

    /// <summary>Leitura do layout da lista: aceita a direção da ordenação em número ou em texto ("Crescente").</summary>
    private static readonly JsonSerializerOptions JsonLayout = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly IConsultaPessoas _consulta;
    private readonly IFiltroSalvoRepositorio _filtros;
    private readonly IAutorizacao _autorizacao;
    private readonly IUsuarioAtual _usuario;
    private readonly TimeProvider _relogio;
    private readonly Menu.IPreferenciaMenuRepositorio _preferencias;

    public ConsultaPessoasAppService(IConsultaPessoas consulta, IFiltroSalvoRepositorio filtros, IAutorizacao autorizacao,
                                     IUsuarioAtual usuario, TimeProvider relogio, Menu.IPreferenciaMenuRepositorio preferencias)
    {
        _consulta = consulta;
        _filtros = filtros;
        _autorizacao = autorizacao;
        _usuario = usuario;
        _relogio = relogio;
        _preferencias = preferencias;
    }

    private DateOnly Hoje => DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime);

    public Task<PaginaPessoas> ConsultarAsync(ConsultaPessoasRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        requisicao.Criterios = Normalizar(requisicao.Criterios);
        ExigirPermissoesDosCampos(requisicao.Criterios);
        requisicao.Limite = Math.Clamp(requisicao.Limite, 1, ConsultaPessoasRequisicao.LimiteMaximo);
        return _consulta.ConsultarAsync(requisicao, Hoje, ct);
    }

    public async Task<ArquivoExportado> ExportarAsync(CriteriosPessoas criterios, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        _autorizacao.Exigir(Permissoes.Pessoas.Exportar);
        criterios = Normalizar(criterios);
        ExigirPermissoesDosCampos(criterios);

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

    public async Task<CatalogoFiltrosPessoasDto> CatalogoAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var opcoes = await _consulta.OpcoesAsync(Hoje, ct);
        var doBanco = await _consulta.OpcoesFiltroAsync(ct);
        static List<OpcaoFiltroDto> Da(List<OpcaoConsultaDto> lista) => lista.Select(o => new OpcaoFiltroDto(o.Id.ToString("D"), o.Nome)).ToList();

        var campos = CatalogoFiltrosPessoas.Campos
            .Where(d => d.Permissao is null || _autorizacao.Possui(d.Permissao))
            .Select(d => new CampoFiltroDto
            {
                Id = d.Id,
                Grupo = d.Grupo,
                Nome = d.Nome,
                Tipo = d.Tipo,
                Operadores = [.. d.Operadores],
                ValePara = d.ValePara,
                Dica = d.Dica,
                TextoLivre = d.TextoLivre,
                SomenteDigitos = d.SomenteDigitos,
                TamanhoMinimo = d.TamanhoMinimo,
                TamanhoMaximo = d.TamanhoMaximo,
                Opcoes = d.FonteOpcoes switch
                {
                    CatalogoFiltrosPessoas.FontePapeis => Da(opcoes.Papeis),
                    CatalogoFiltrosPessoas.FonteEtiquetas => Da(opcoes.Etiquetas),
                    CatalogoFiltrosPessoas.FonteVendedores => Da(opcoes.Vendedores),
                    { } fonte when doBanco.TryGetValue(fonte, out var lista) => lista,
                    _ => d.Opcoes?.ToList() ?? []
                },
                OpcoesSobDemanda = d.FonteOpcoes == CatalogoFiltrosPessoas.FonteMunicipios ? CatalogoFiltrosPessoas.FonteMunicipios : null
            })
            .ToList();

        // Campos personalizados pesquisáveis: um campo cada, no grupo "Informações adicionais" (antes de "Interações", como na ficha).
        var personalizados = opcoes.CamposPesquisaveis.Select(c => new CampoFiltroDto
        {
            Id = CamposFiltroPessoas.CampoPersonalizado(c.Id),
            Grupo = CatalogoFiltrosPessoas.GrupoInformacoesAdicionais,
            Nome = c.Nome,
            Tipo = TipoCampoFiltro.Texto,
            Operadores = [.. CatalogoFiltrosPessoas.OperadoresCampoPersonalizado],
            Dica = "Começo do valor."
        });
        var posicao = campos.FindIndex(c => c.Grupo == "Interações");
        campos.InsertRange(posicao < 0 ? campos.Count : posicao, personalizados);

        // Colunas da lista: as que o usuário pode ver; a linha de filtro da coluna usa o campo do catálogo (se ele pode usá-lo).
        var comFiltro = campos.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var colunas = ColunasListaPessoas.Colunas
            .Where(d => d.Permissao is null || _autorizacao.Possui(d.Permissao))
            .Select(d => new ColunaListaDto
            {
                Id = d.Id,
                Grupo = d.Grupo,
                Nome = d.Nome,
                Tipo = d.Tipo,
                Largura = d.Largura,
                Padrao = d.Padrao,
                CampoFiltro = d.CampoFiltro is { } f && comFiltro.Contains(f) ? f : null,
                Opcoes = d.Tipo == TipoColunaLista.Opcao && d.CampoFiltro is { } campo
                    ? CatalogoFiltrosPessoas.Obter(campo)?.Opcoes?.ToList() ?? []
                    : []
            })
            .ToList();

        return new CatalogoFiltrosPessoasDto { Campos = campos, Colunas = colunas, Layout = await LayoutDoUsuarioAsync(ct) };
    }

    /// <summary>Colunas e ordenação que o usuário deixou na lista (preferência da tela); ilegível ou sem usuário = padrão.</summary>
    private async Task<LayoutListaPessoas?> LayoutDoUsuarioAsync(CancellationToken ct)
    {
        if (_usuario.Id is not { } usuario) return null;
        var json = await _preferencias.ObterTelaAsync(usuario, ColunasPessoas.TelaLista, ct);
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}") return null; // vazio = padrão da tela
        try { return ColunasListaPessoas.Limpar(JsonSerializer.Deserialize<LayoutListaPessoas>(json, JsonLayout)); }
        catch (JsonException) { return null; }
    }

    /// <summary>Campo com permissão própria (dados sensíveis, financeiro...): quem não a tem não filtra por ele.</summary>
    private void ExigirPermissoesDosCampos(CriteriosPessoas criterios)
    {
        foreach (var condicao in criterios.Condicoes)
            if (CatalogoFiltrosPessoas.Obter(condicao.Campo)?.Permissao is { } permissao)
                _autorizacao.Exigir(permissao);
    }

    /// <summary>
    /// Todas as condições da consulta: os critérios no formato antigo (consulta avançada e filtros salvos por ela)
    /// traduzidos para o catálogo, mais as condições novas. É o que o banco recebe: um motor só.
    /// </summary>
    public static List<CondicaoFiltro> CondicoesDe(CriteriosPessoas c)
    {
        var lista = new List<CondicaoFiltro>();
        void Incluir(string campo, OperadorFiltro operador, params string[] valores) =>
            lista.Add(new CondicaoFiltro { Campo = campo, Operador = operador, Valores = [.. valores] });
        static string Dia(DateOnly d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        if (c.Naturezas.Count > 0) Incluir(CamposFiltroPessoas.Natureza, OperadorFiltro.UmDestes, [.. c.Naturezas.Select(n => n.ToString())]);
        if (c.Situacoes.Count > 0) Incluir(CamposFiltroPessoas.Situacao, OperadorFiltro.UmDestes, [.. c.Situacoes.Select(s => s.ToString())]);
        if (c.PapeisIds.Count > 0)
            Incluir(CamposFiltroPessoas.Papeis, c.TodosOsPapeis ? OperadorFiltro.TodosDestes : OperadorFiltro.UmDestes,
                [.. c.PapeisIds.Select(i => i.ToString("D"))]);
        if (c.EtiquetasIds.Count > 0) Incluir(CamposFiltroPessoas.Etiquetas, OperadorFiltro.UmDestes, [.. c.EtiquetasIds.Select(i => i.ToString("D"))]);
        if (c.Uf is { } uf) Incluir(CamposFiltroPessoas.Uf, OperadorFiltro.UmDestes, uf);
        if (c.MunicipioId is { } municipio)
            Incluir(CamposFiltroPessoas.Municipio, OperadorFiltro.UmDestes, municipio.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (c.Cnae is { } cnae)
            Incluir(c.SomenteCnaePrincipal ? CamposFiltroPessoas.CnaePrincipal : CamposFiltroPessoas.Cnae, OperadorFiltro.ComecaCom, cnae);
        if (c.ProdutorRural is { } rural) Incluir(CamposFiltroPessoas.ProdutorRural, rural ? OperadorFiltro.Sim : OperadorFiltro.Nao);
        if (c.Regime is { } regime) Incluir(CamposFiltroPessoas.Regime, OperadorFiltro.UmDestes, regime.ToString());
        if (c.VendedorId is { } vendedor) Incluir(CamposFiltroPessoas.Vendedor, OperadorFiltro.UmDestes, vendedor.ToString("D"));
        if (c.SemCarteira) Incluir(CamposFiltroPessoas.SemCarteira, OperadorFiltro.Sim);
        if (c.Relacionamento is { } relacionamento) Incluir(CamposFiltroPessoas.Relacionamento, OperadorFiltro.UmDestes, relacionamento.ToString());
        if (c.SemInteracaoDias is { } dias)
            Incluir(CamposFiltroPessoas.SemInteracao, OperadorFiltro.HaMaisDeDias, dias.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (c.Bloqueado is { } bloqueado) Incluir(CamposFiltroPessoas.Bloqueado, bloqueado ? OperadorFiltro.Sim : OperadorFiltro.Nao);
        if (c.DocumentosVencidos) Incluir(CamposFiltroPessoas.DocumentosVencidos, OperadorFiltro.Sim);
        if (c.DocumentosVencendoDias is { } vencendo)
            Incluir(CamposFiltroPessoas.DocumentosVencendo, OperadorFiltro.EmAteDias, vencendo.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (c.CampoId is { } campo)
        {
            if (c.CampoValor is { } valor) Incluir(CamposFiltroPessoas.CampoPersonalizado(campo), OperadorFiltro.ComecaCom, valor);
            else Incluir(CamposFiltroPessoas.CampoPersonalizado(campo), OperadorFiltro.NaoVazio);
        }
        if (c.CadastradoDe is { } de && c.CadastradoAte is { } ate) Incluir(CamposFiltroPessoas.CadastradoEm, OperadorFiltro.Entre, Dia(de), Dia(ate));
        else if (c.CadastradoDe is { } desde) Incluir(CamposFiltroPessoas.CadastradoEm, OperadorFiltro.APartirDe, Dia(desde));
        else if (c.CadastradoAte is { } ateDia) Incluir(CamposFiltroPessoas.CadastradoEm, OperadorFiltro.Ate, Dia(ateDia));

        lista.AddRange(c.Condicoes);
        return lista;
    }

    public async Task<FiltroSalvoDto> SalvarFiltroAsync(FiltroSalvoDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var usuario = _usuario.Id ?? throw new ValidacaoException(["Entre com um usuário para salvar filtros."]);
        var anterior = dto.Id == Guid.Empty ? null : await _filtros.ObterAsync(dto.Id, ct);
        if (anterior is not null && anterior.UsuarioId != usuario)
            throw new ValidacaoException(["Só quem criou o filtro pode alterá-lo. Salve uma cópia com outro nome."]);

        var normalizados = Normalizar(dto.Criterios);
        ExigirPermissoesDosCampos(normalizados);
        var criterios = JsonSerializer.Serialize(normalizados, Json);
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
        Criterios = NoFormatoDoCatalogo(LerCriterios(f.Criterios))
    };

    /// <summary>
    /// Contador das abas de visão. Conta o que o usuário veria ao aplicar a visão: campo sem permissão para ele fica de
    /// fora (a tela também ignora); visão com condição que não vale mais (opção apagada...) fica sem contador. Só visões
    /// que o usuário enxerga (dele ou compartilhadas), no máximo o número de abas.
    /// </summary>
    public async Task<Dictionary<Guid, int>> ContarFiltrosAsync(List<Guid> ids, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        if (_usuario.Id is not { } usuario || ids is null || ids.Count == 0) return new();
        var pedidos = ids.Distinct().Take(AbasPessoas.Maximo).ToHashSet();
        var contaveis = new List<(Guid Id, CriteriosPessoas Criterios)>();
        foreach (var filtro in await _filtros.ListarVisiveisAsync(usuario, ct))
        {
            if (!pedidos.Contains(filtro.Id)) continue;
            var criterios = NoFormatoDoCatalogo(LerCriterios(filtro.Criterios));
            criterios.Condicoes = criterios.Condicoes
                .Where(c => CatalogoFiltrosPessoas.Obter(c.Campo)?.Permissao is not { } permissao || _autorizacao.Possui(permissao))
                .ToList();
            if (CatalogoFiltrosPessoas.Normalizar(criterios.Condicoes).Count > 0) continue;
            contaveis.Add((filtro.Id, criterios));
        }
        if (contaveis.Count == 0) return new();
        var totais = await _consulta.ContarAsync([.. contaveis.Select(c => c.Criterios)], Hoje, ct);
        return contaveis.Select((c, i) => (c.Id, Total: totais[i])).ToDictionary(x => x.Id, x => x.Total);
    }

    /// <summary>
    /// Filtro salvo no formato antigo (consulta avançada) chega à tela já como condições do catálogo: a tela de Pessoas
    /// só entende condições. O gravado não muda; ao salvar de novo, fica no formato novo.
    /// </summary>
    public static CriteriosPessoas NoFormatoDoCatalogo(CriteriosPessoas c) =>
        new() { Texto = c.Texto, Condicoes = CondicoesDe(c), Layout = ColunasListaPessoas.Limpar(c.Layout) };

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
        c.Condicoes ??= new();
        erros.AddRange(CatalogoFiltrosPessoas.Normalizar(c.Condicoes));
        c.Layout = ColunasListaPessoas.Limpar(c.Layout); // visão salva com as colunas: só Ids conhecidos
        if (erros.Count > 0) throw new ValidacaoException(erros);
        return c;
    }
}
