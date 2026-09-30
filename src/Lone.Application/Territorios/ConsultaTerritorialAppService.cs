using Lone.Application.Seguranca;
using Lone.Contracts.Seguranca;
using Lone.Contracts.Territorios;
using Lone.Domain.Comercial;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Territorios;
using Lone.Domain.Validacao;

namespace Lone.Application.Territorios;

public interface IParametrosTerritoriaisAppService
{
    Task<ParametrosTerritoriaisDto> ObterAsync(CancellationToken ct = default);
    Task<ParametrosTerritoriaisDto> SalvarAsync(ParametrosTerritoriaisDto dto, CancellationToken ct = default);
}

/// <summary>
/// Parâmetros do motor territorial (DN-08): dias retroativos das operações, próprios dos territórios (não o das
/// coberturas), padrão 30, editável por quem configura os territórios.
/// </summary>
public sealed class ParametrosTerritoriaisAppService : IParametrosTerritoriaisAppService
{
    private readonly IParametrosTerritoriaisRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;

    public ParametrosTerritoriaisAppService(IParametrosTerritoriaisRepositorio repositorio, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _autorizacao = autorizacao;
    }

    public async Task<ParametrosTerritoriaisDto> ObterAsync(CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeituraDoMotor(_autorizacao);
        return ParaDto(await _repositorio.ObterAsync(ct));
    }

    public async Task<ParametrosTerritoriaisDto> SalvarAsync(ParametrosTerritoriaisDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Territorios.Configurar);
        if (dto.DiasRetroativosMaximo is < 0 or > ParametrosTerritoriais.MaximoDiasRetroativos)
            throw new ValidacaoException([$"Os dias no passado vão de 0 (só hoje ou datas futuras) a {ParametrosTerritoriais.MaximoDiasRetroativos}."]);
        var atual = await _repositorio.ObterAsync(ct);
        var dados = new ParametrosTerritoriais
        {
            Id = ParametrosTerritoriais.IdUnico, Versao = dto.Versao, DiasRetroativosMaximo = dto.DiasRetroativosMaximo, CriadoEm = atual.CriadoEm
        };
        if (atual.DiasRetroativosMaximo != dados.DiasRetroativosMaximo)
            dados.RegistrarEvento($"Operações territoriais: datas no passado até {dados.DiasRetroativosMaximo} dia(s) (antes {atual.DiasRetroativosMaximo}).");
        await _repositorio.SalvarAsync(dados, ct);
        return ParaDto(await _repositorio.ObterAsync(ct));
    }

    public static ParametrosTerritoriaisDto ParaDto(ParametrosTerritoriais p) => new()
    {
        Versao = p.Versao, DiasRetroativosMaximo = p.DiasRetroativosMaximo, Padrao = ParametrosTerritoriais.PadraoDiasRetroativos,
        Maximo = ParametrosTerritoriais.MaximoDiasRetroativos
    };
}

public interface IConsultaTerritorialAppService
{
    /// <summary>A aba "Regras e clientes" da ficha do território.</summary>
    Task<TerritorioMotorDto> DoTerritorioAsync(Guid territorioId, CancellationToken ct = default);

    /// <summary>"Territórios do cliente X na data D" (seção L: o que os documentos futuros vão copiar).</summary>
    Task<TerritoriosDoClienteDto> DoClienteAsync(Guid pessoaId, DateOnly data, CancellationToken ct = default);
}

/// <summary>
/// Leitura das regras, exceções e atribuições. Nunca recalcula: mostra o que está gravado (a atribuição é o fato; a
/// explicação está no item da operação que a abriu). Clientes filtrados pelo alcance do usuário (seção O).
/// </summary>
public sealed class ConsultaTerritorialAppService : IConsultaTerritorialAppService
{
    private readonly IConsultasTerritoriais _consultas;
    private readonly ITerritorioRepositorio _territorios;
    private readonly IMapaTerritorialRepositorio _mapas;
    private readonly IOperacaoTerritorialRepositorio _operacoes;
    private readonly IAutorizacao _autorizacao;
    private readonly IAlcanceDoUsuario _alcance;
    private readonly IEscopoPessoas _escopo;
    private readonly IPessoasNoEscopo _noEscopo;
    private readonly TimeProvider _relogio;

    public ConsultaTerritorialAppService(IConsultasTerritoriais consultas, ITerritorioRepositorio territorios, IMapaTerritorialRepositorio mapas,
                                         IOperacaoTerritorialRepositorio operacoes, IAutorizacao autorizacao, IAlcanceDoUsuario alcance,
                                         IEscopoPessoas escopo, IPessoasNoEscopo noEscopo, TimeProvider relogio)
    {
        _consultas = consultas;
        _territorios = territorios;
        _mapas = mapas;
        _operacoes = operacoes;
        _autorizacao = autorizacao;
        _alcance = alcance;
        _escopo = escopo;
        _noEscopo = noEscopo;
        _relogio = relogio;
    }

    private DateOnly Hoje => DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime);

    public async Task<TerritorioMotorDto> DoTerritorioAsync(Guid territorioId, CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeituraDoMotor(_autorizacao);
        var territorio = await _territorios.ObterAsync(territorioId, ct) ?? throw new ValidacaoException(["Este território não existe mais."]);
        var hoje = Hoje;
        var doMapa = (await _territorios.ListarDoMapaAsync(territorio.MapaId, ct)).ToDictionary(t => t.Id);
        var regras = await _consultas.RegrasDoTerritorioAsync(territorioId, ct);
        var excecoes = await _consultas.ExcecoesDoTerritorioAsync(territorioId, ct);

        // Alcance restrito (seção O): só os clientes que o usuário alcança, nas exceções e nas atribuições (e o total é o deles).
        IReadOnlySet<Guid>? alcance = null;
        int total;
        List<AtribuicaoTerritorio> clientes;
        if (_alcance.Alcance == AlcanceComercial.Tudo)
            (total, clientes) = await _consultas.AtribuicoesDoTerritorioAsync(territorioId, hoje, null, TerritorioMotorDto.LimiteClientes, ct);
        else
        {
            var escopo = await _escopo.ObterAsync(ct);
            var todas = (await _consultas.AtribuicoesDoTerritorioAsync(territorioId, hoje, null, int.MaxValue, ct)).Itens;
            var meus = await _noEscopo.ClientesDiretosAsync([.. todas.Select(a => a.PessoaId).Concat(excecoes.Select(x => x.PessoaId)).Distinct()], escopo, ct);
            alcance = meus;
            var minhas = todas.Where(a => meus.Contains(a.PessoaId)).ToList();
            total = minhas.Count;
            clientes = [.. minhas.Take(TerritorioMotorDto.LimiteClientes)];
        }

        var operacoes = await _operacoes.ListarAsync(territorio.MapaId, ct);
        var numeros = operacoes.ToDictionary(o => o.Id, o => o.Numero);
        var pessoas = await _consultas.NomesDePessoasAsync(
            [.. excecoes.Select(x => x.PessoaId).Concat(clientes.Select(a => a.PessoaId)).Distinct()], ct);
        var regraPorId = regras.ToDictionary(r => r.Id);

        return new TerritorioMotorDto
        {
            TerritorioId = territorioId,
            Regras = [.. regras.OrderByDescending(r => r.Numero).Select(r => new RegraTerritorioDto
            {
                Id = r.Id, Versao = r.Versao, TerritorioId = r.TerritorioId, Numero = r.Numero, Grupos = TextoRegraTerritorio.DeJson(r.Grupos),
                Criterios = r.Criterios, Prioridade = r.Prioridade, InicioEm = r.InicioEm, FimEm = r.FimEm, Ativo = r.Ativo, OperacaoId = r.OperacaoId,
                Operacao = numeros.GetValueOrDefault(r.OperacaoId), Vigente = r.VigenteEm(hoje),
                ComCondicaoRestrita = TextoRegraTerritorio.Campos(TextoRegraTerritorio.DeJson(r.Grupos))
                    .Select(Consultas.CatalogoFiltrosPessoas.Obter).Any(d => d is not null &&
                        ((d.Permissao is { } p && !_autorizacao.Possui(p)) || (d.PermissaoEmRegraTerritorio is { } q && !_autorizacao.Possui(q))))
            })],
            Excecoes = [.. excecoes.Where(x => alcance is null || alcance.Contains(x.PessoaId))
                .OrderByDescending(x => x.VigenteEm(hoje)).ThenByDescending(x => x.InicioEm).Select(x => new ExcecaoTerritorioDto
                {
                    Id = x.Id, Versao = x.Versao, TerritorioId = x.TerritorioId, Territorio = territorio.Nome, PessoaId = x.PessoaId,
                    Pessoa = pessoas.GetValueOrDefault(x.PessoaId), Tipo = x.Tipo, InicioEm = x.InicioEm, FimEm = x.FimEm, Motivo = x.Motivo,
                    Origem = x.Origem, Ativo = x.Ativo, OperacaoId = x.OperacaoId, Operacao = numeros.GetValueOrDefault(x.OperacaoId),
                    Vigente = x.VigenteEm(hoje)
                })],
            Clientes = [.. clientes.Select(a => ParaDto(a, doMapa, pessoas, regraPorId, numeros, null, hoje))],
            TotalClientes = total,
            OperacoesAbertas = [.. operacoes.Where(o => o.Aberta && o.Mudancas.Any(m => m.TerritorioId == territorioId)).Select(o =>
                new OperacaoTerritorialResumoDto { Id = o.Id, Numero = o.Numero, MapaId = o.MapaId, EfeitoEm = o.EfeitoEm, Motivo = o.Motivo,
                    Situacao = o.Situacao, SituacaoNome = RegrasOperacaoTerritorial.Nome(o.Situacao), CriadaPor = o.CriadaPor, CriadaEm = o.CriadoEm,
                    QuantidadeMudancas = o.Mudancas.Count })]
        };
    }

    public async Task<TerritoriosDoClienteDto> DoClienteAsync(Guid pessoaId, DateOnly data, CancellationToken ct = default)
    {
        AcessoTerritorios.ExigirLeituraDoMotor(_autorizacao);
        await _escopo.ExigirAsync(pessoaId, ct: ct);
        var atribuicoes = await _consultas.AtribuicoesDoClienteAsync(pessoaId, data, ct);
        var mapas = (await _mapas.ListarAsync(ct)).ToDictionary(m => m.Id);
        var dto = new TerritoriosDoClienteDto { PessoaId = pessoaId, Data = data };
        var numeros = await _operacoes.NumerosAsync([.. atribuicoes.Select(a => a.OperacaoId).Distinct()], ct);
        foreach (var grupo in atribuicoes.GroupBy(a => a.MapaId))
        {
            var doMapa = (await _territorios.ListarDoMapaAsync(grupo.Key, ct)).ToDictionary(t => t.Id);
            foreach (var a in grupo)
            {
                var item = ParaDto(a, doMapa, new Dictionary<Guid, string>(), new Dictionary<Guid, RegraTerritorio>(), numeros, mapas.GetValueOrDefault(a.MapaId), data);
                dto.Territorios.Add(item);
            }
        }
        dto.Territorios = [.. dto.Territorios.OrderBy(t => t.Mapa, StringComparer.CurrentCultureIgnoreCase).ThenBy(t => t.Caminho, StringComparer.CurrentCultureIgnoreCase)];
        return dto;
    }

    private static AtribuicaoTerritorioDto ParaDto(AtribuicaoTerritorio a, IReadOnlyDictionary<Guid, Territorio> doMapa, IReadOnlyDictionary<Guid, string> pessoas,
                                                   IReadOnlyDictionary<Guid, RegraTerritorio> regras, IReadOnlyDictionary<Guid, string> numeros,
                                                   MapaTerritorial? mapa, DateOnly data)
    {
        var territorio = doMapa.GetValueOrDefault(a.TerritorioId);
        return new AtribuicaoTerritorioDto
        {
            Id = a.Id, MapaId = a.MapaId, Mapa = mapa?.Nome, TerritorioId = a.TerritorioId, TerritorioCodigo = territorio?.Codigo, Territorio = territorio?.Nome,
            Caminho = Caminho(doMapa, a.TerritorioId, data), PessoaId = a.PessoaId, Pessoa = pessoas.GetValueOrDefault(a.PessoaId),
            InicioEm = a.InicioEm, FimEm = a.FimEm, Origem = a.Origem,
            OrigemDescricao = a.Origem == OrigemAtribuicaoTerritorio.Regra
                ? (a.RegraId is { } r && regras.TryGetValue(r, out var regra) ? $"Regra v{regra.Numero}" : "Regra")
                : "Exceção (fixação)",
            Ativo = a.Ativo, OperacaoId = a.OperacaoId, Operacao = numeros.GetValueOrDefault(a.OperacaoId),
            RegistrarNosDocumentos = mapa?.RegistrarNosDocumentos ?? false
        };
    }

    /// <summary>O caminho do território na data (pelas posições válidas em D; a árvore de hoje pode ser outra).</summary>
    private static string Caminho(IReadOnlyDictionary<Guid, Territorio> doMapa, Guid territorioId, DateOnly data)
    {
        var nomes = new List<string>();
        Guid? atual = territorioId;
        for (var passos = 0; atual is { } id && passos < RegrasArvoreTerritorial.ProfundidadeMaxima + 1; passos++)
        {
            if (!doMapa.TryGetValue(id, out var t)) break;
            nomes.Add(t.Nome);
            var posicao = t.Posicoes.FirstOrDefault(p => p.Ativo && p.InicioEm <= data && (p.FimEm is null || p.FimEm >= data));
            atual = posicao is null ? t.PaiId : posicao.PaiId;
        }
        nomes.Reverse();
        return string.Join(" › ", nomes);
    }
}
