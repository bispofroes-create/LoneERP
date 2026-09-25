using Lone.Application.Documentos;
using Lone.Application.Seguranca;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Seguranca;
using Lone.Domain.CamposPersonalizados;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Application.CamposPersonalizados;

public interface ICampoPersonalizadoAppService
{
    /// <summary>Só ativos: quem vê o cadastro. Com inativos (tela de administração): quem gerencia os campos.</summary>
    Task<List<CampoPersonalizadoDto>> ListarAsync(EntidadePersonalizavel entidade, bool incluirInativos, CancellationToken ct = default);
    Task<CampoPersonalizadoDto?> ObterAsync(Guid id, CancellationToken ct = default);
    Task<CampoPersonalizadoDto> SalvarAsync(CampoPersonalizadoDto dto, CancellationToken ct = default);
    Task<CampoPersonalizadoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task<CampoPersonalizadoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default);
    Task ReordenarAsync(EntidadePersonalizavel entidade, List<Guid> ids, CancellationToken ct = default);
}

/// <summary>Administração dos campos personalizados: permissão → normalização → regras → gravação (com eventos).</summary>
public sealed class CampoPersonalizadoAppService : ICampoPersonalizadoAppService
{
    private readonly ICampoPersonalizadoRepositorio _repositorio;
    private readonly IAutorizacao _autorizacao;
    private readonly ITipoDocumentoRepositorio _tiposDocumento;

    public CampoPersonalizadoAppService(ICampoPersonalizadoRepositorio repositorio, IAutorizacao autorizacao,
                                        ITipoDocumentoRepositorio tiposDocumento)
    {
        _repositorio = repositorio;
        _tiposDocumento = tiposDocumento;
        _autorizacao = autorizacao;
    }

    public async Task<List<CampoPersonalizadoDto>> ListarAsync(EntidadePersonalizavel entidade, bool incluirInativos, CancellationToken ct = default)
    {
        _autorizacao.Exigir(incluirInativos ? Permissoes.Cadastros.CamposPersonalizados : PermissaoDeLeitura(entidade));
        var campos = await _repositorio.ListarAsync(entidade, incluirInativos, ct);
        var comValores = incluirInativos ? await _repositorio.ComValoresAsync(campos.Select(c => c.Id).ToList(), ct) : new HashSet<Guid>();
        return campos.Select(c => CampoPersonalizadoMapeamento.ParaDto(c, comValores.Contains(c.Id))).ToList();
    }

    public async Task<CampoPersonalizadoDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.CamposPersonalizados);
        var campo = await _repositorio.ObterAsync(id, ct);
        return campo is null ? null : await ParaDtoAsync(campo, ct);
    }

    public async Task<CampoPersonalizadoDto> SalvarAsync(CampoPersonalizadoDto dto, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.CamposPersonalizados);

        var anterior = dto.Id == Guid.Empty ? null : await _repositorio.ObterAsync(dto.Id, ct);
        var novo = anterior is null;
        var dados = CampoPersonalizadoMapeamento.ParaEntidade(dto);

        if (anterior is not null)
        {
            // Ativar/desativar têm ações próprias; a entidade não muda de cadastro.
            dados.Ativo = anterior.Ativo;
            dados.Entidade = anterior.Entidade;
            dados.Ordem = anterior.Ordem;
            ManterOpcoesGravadas(dados, anterior);
        }
        else
        {
            dados.Ativo = true;
            dados.Ordem = await _repositorio.ProximaOrdemAsync(dados.Entidade, ct);
        }

        RegrasCampoPersonalizado.Normalizar(dados);
        var erros = RegrasCampoPersonalizado.Validar(dados);

        if (dados.Nome.Length > 0 && await _repositorio.NomeEmUsoAsync(dados.Entidade, dados.TipoDocumentoId, dados.Nome, dados.Id, ct))
            erros.Add($"Já existe um campo chamado \"{dados.Nome}\" (ativo ou desativado). Use outro nome ou reative o existente.");

        if (dados.TipoDocumentoId is { } tipoDocumentoId && await _tiposDocumento.ObterAsync(tipoDocumentoId, ct) is null)
            erros.Add("O tipo de documento escolhido não existe mais.");

        // Com valores gravados, o campo não muda de tipo de documento (os valores ficariam no documento errado).
        if (anterior is not null && anterior.TipoDocumentoId != dados.TipoDocumentoId &&
            (await _repositorio.ComValoresAsync([anterior.Id], ct)).Count > 0)
            erros.Add("O tipo de documento não pode ser mudado: o campo já tem valores gravados. Desative este campo e crie outro.");

        if (anterior is not null && anterior.Tipo != dados.Tipo &&
            (await _repositorio.ComValoresAsync([anterior.Id], ct)).Count > 0)
            erros.Add("O tipo não pode ser mudado: o campo já tem valores gravados. Desative este campo e crie outro.");

        if (erros.Count > 0)
            throw new ValidacaoException(erros);

        RegistrarEventos(dados, anterior);
        await _repositorio.SalvarAsync(dados, novo, ct);
        return await ReleAsync(dados.Id, ct);
    }

    public Task<CampoPersonalizadoDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, c => c.Desativar(), ct);

    public Task<CampoPersonalizadoDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, c => c.Reativar(), ct);

    public Task ReordenarAsync(EntidadePersonalizavel entidade, List<Guid> ids, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.CamposPersonalizados);
        return _repositorio.ReordenarAsync(entidade, ids.Distinct().ToList(), ct);
    }

    private async Task<CampoPersonalizadoDto> AlterarSituacaoAsync(
        Guid id, AlterarSituacaoRequisicao requisicao, Action<CampoPersonalizado> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Cadastros.CamposPersonalizados);
        var campo = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este campo não existe mais."]);
        campo.Versao = requisicao.Versao ?? campo.Versao; // a versão que o usuário via: se mudou, dá conflito
        acao(campo);
        await _repositorio.SalvarAsync(campo, novo: false, ct);
        return await ReleAsync(id, ct);
    }

    /// <summary>
    /// Opções gravadas nunca somem: a que não veio na lista é desativada (valores antigos continuam legíveis).
    /// </summary>
    private static void ManterOpcoesGravadas(CampoPersonalizado dados, CampoPersonalizado anterior)
    {
        foreach (var gravada in anterior.Opcoes.Where(g => dados.Opcoes.All(o => o.Id != g.Id)))
            dados.Opcoes.Add(new CampoPersonalizadoOpcao
            {
                Id = gravada.Id,
                CampoId = dados.Id,
                Texto = gravada.Texto,
                Ordem = int.MaxValue,
                Ativa = false
            });
    }

    private static void RegistrarEventos(CampoPersonalizado dados, CampoPersonalizado? anterior)
    {
        var tipo = TiposCampo.Obter(dados.Tipo).Nome.ToLowerInvariant();
        if (anterior is null)
        {
            dados.RegistrarEvento($"Campo personalizado '{dados.Nome}' criado ({tipo}{(dados.Obrigatorio ? ", obrigatório" : string.Empty)}).");
            return;
        }

        if (!string.Equals(anterior.Nome, dados.Nome, StringComparison.Ordinal))
            dados.RegistrarEvento($"Campo personalizado '{anterior.Nome}' renomeado para '{dados.Nome}'.");
        if (anterior.Tipo != dados.Tipo)
            dados.RegistrarEvento($"Campo personalizado '{dados.Nome}' mudou para o tipo {tipo}.");
        if (anterior.Visivel != dados.Visivel)
            dados.RegistrarEvento($"Campo personalizado '{dados.Nome}' passou a ser {(dados.Visivel ? "visível" : "oculto")} na ficha.");
        if (anterior.Pesquisavel != dados.Pesquisavel)
            dados.RegistrarEvento($"Campo personalizado '{dados.Nome}' {(dados.Pesquisavel ? "passou a entrar" : "deixou de entrar")} na busca.");
        if (anterior.Obrigatorio != dados.Obrigatorio)
            dados.RegistrarEvento($"Campo personalizado '{dados.Nome}' passou a ser {(dados.Obrigatorio ? "obrigatório" : "opcional")}.");
    }

    private async Task<CampoPersonalizadoDto> ReleAsync(Guid id, CancellationToken ct)
    {
        var gravado = await _repositorio.ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
        return await ParaDtoAsync(gravado, ct);
    }

    private async Task<CampoPersonalizadoDto> ParaDtoAsync(CampoPersonalizado campo, CancellationToken ct) =>
        CampoPersonalizadoMapeamento.ParaDto(campo, (await _repositorio.ComValoresAsync([campo.Id], ct)).Count > 0);

    private static string PermissaoDeLeitura(EntidadePersonalizavel entidade) => entidade switch
    {
        EntidadePersonalizavel.Pessoa or EntidadePersonalizavel.Documento => Permissoes.Pessoas.Visualizar,
        _ => Permissoes.Cadastros.CamposPersonalizados
    };
}
