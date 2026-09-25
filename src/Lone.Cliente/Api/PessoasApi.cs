using Lone.Contracts.Auditoria;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Cliente.Api;

/// <summary>Chamadas do cadastro de pessoas. A API confere as permissões de cada operação.</summary>
public sealed class PessoasApi
{
    private readonly ClienteApi _api;

    public PessoasApi(ClienteApi api)
    {
        _api = api;
    }

    public Task<int> ContarClientesAtivosAsync(CancellationToken ct = default) =>
        _api.GetAsync<int>(Rotas.Pessoas.QuantidadeClientesAtivos, ct);

    /// <summary>Busca no servidor (nome, código, CPF/CNPJ, telefone ou e-mail), com filtro de papel.</summary>
    public Task<List<PessoaResumo>> ListarAsync(FiltroPessoas filtro, CancellationToken ct = default) =>
        _api.GetAsync<List<PessoaResumo>>(Rotas.Pessoas.Grupo + Consulta(filtro), ct);

    public Task<List<QuantidadePorFaixaEtaria>> ListarFaixasEtariasAsync(TipoPapel? papel, CancellationToken ct = default) =>
        _api.GetAsync<List<QuantidadePorFaixaEtaria>>(Rotas.Pessoas.FaixasEtarias + (papel is { } p ? "?papel=" + p : string.Empty), ct);

    public Task<PessoaDto?> ObterAsync(Guid id, CancellationToken ct = default) =>
        _api.GetOuNuloAsync<PessoaDto>(Rotas.Pessoas.PorId(id), ct);

    /// <summary>Inclui ou altera (o Id da pessoa nova é gerado no aparelho).</summary>
    public Task<ResultadoSalvarPessoa> SalvarAsync(PessoaDto pessoa, CancellationToken ct = default) =>
        _api.PutAsync<ResultadoSalvarPessoa>(Rotas.Pessoas.PorId(pessoa.Id), pessoa, ct);

    public Task<PessoaDto> DesativarAsync(Guid id, byte[]? versao, string? motivo, CancellationToken ct = default) =>
        _api.PostAsync<PessoaDto>(Rotas.Pessoas.Desativar(id), new AlterarSituacaoRequisicao { Versao = versao, Motivo = motivo }, ct: ct);

    public Task<PessoaDto> ReativarAsync(Guid id, byte[]? versao, string? motivo, CancellationToken ct = default) =>
        _api.PostAsync<PessoaDto>(Rotas.Pessoas.Reativar(id), new AlterarSituacaoRequisicao { Versao = versao, Motivo = motivo }, ct: ct);

    public Task<List<RegistroHistorico>> ListarHistoricoAsync(Guid id, CancellationToken ct = default) =>
        _api.GetAsync<List<RegistroHistorico>>(Rotas.Pessoas.Historico(id), ct);

    /// <summary>Monta "?texto=...&amp;papel=Cliente&amp;incluirInativos=true" só com o que foi informado.</summary>
    internal static string Consulta(FiltroPessoas filtro)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(filtro.Texto)) partes.Add("texto=" + Uri.EscapeDataString(filtro.Texto.Trim()));
        if (filtro.Papel is { } papel) partes.Add("papel=" + papel);
        if (filtro.EtiquetaId is { } etiqueta) partes.Add("etiquetaId=" + etiqueta);
        if (filtro.IncluirInativos) partes.Add("incluirInativos=true");
        if (filtro.MunicipioACorrigir) partes.Add("municipioACorrigir=true");
        if (filtro.Limite != FiltroPessoas.LimiteMaximo) partes.Add("limite=" + filtro.Limite);
        return partes.Count == 0 ? string.Empty : "?" + string.Join("&", partes);
    }
}
