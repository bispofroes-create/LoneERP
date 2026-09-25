using Lone.Application.Empresas;
using Lone.Contracts.Colaboradores;
using Lone.Domain.Colaboradores;
using Lone.Domain.Entidades;

namespace Lone.Application.Colaboradores;

/// <summary>
/// Liga os dados de colaborador da ficha de pessoa aos cadastros: confere as referências ao gravar e preenche os
/// nomes (empresa, gestor) ao ler. Um serviço só, para o cadastro de pessoas não depender de cada repositório.
/// </summary>
public sealed class ReferenciasColaborador
{
    private readonly ICargoRepositorio _cargos;
    private readonly IDepartamentoRepositorio _departamentos;
    private readonly ISetorRepositorio _setores;
    private readonly ICentroCustoRepositorio _centrosCusto;
    private readonly IEmpresaConsultas _empresas;
    private readonly IColaboradorConsultas _consultas;

    public ReferenciasColaborador(ICargoRepositorio cargos, IDepartamentoRepositorio departamentos, ISetorRepositorio setores,
                                  ICentroCustoRepositorio centrosCusto, IEmpresaConsultas empresas, IColaboradorConsultas consultas)
    {
        _cargos = cargos;
        _departamentos = departamentos;
        _setores = setores;
        _centrosCusto = centrosCusto;
        _empresas = empresas;
        _consultas = consultas;
    }

    public async Task<List<string>> ValidarAsync(Pessoa dados, Pessoa? anterior, CancellationToken ct)
    {
        if (dados.Vinculos.Count == 0 && dados.Lotacoes.Count == 0) return [];

        var lotacoes = dados.Lotacoes;
        var estrutura = new EstruturaParaConferir(
            await _cargos.ObterVariosAsync(lotacoes.Select(l => l.CargoId).OfType<Guid>().ToList(), ct),
            await _departamentos.ObterVariosAsync(lotacoes.Select(l => l.DepartamentoId).OfType<Guid>().ToList(), ct),
            await _setores.ObterVariosAsync(lotacoes.Select(l => l.SetorId).OfType<Guid>().ToList(), ct),
            await _centrosCusto.ObterVariosAsync(lotacoes.Select(l => l.CentroCustoId).OfType<Guid>().ToList(), ct),
            (await _empresas.ListarEmpresasAsync(ct)).Where(e => e.Ativa).Select(e => e.Id).ToHashSet(),
            await _consultas.PessoasFisicasAtivasAsync(lotacoes.Select(l => l.GestorId).OfType<Guid>().ToList(), ct));

        return RegrasColaborador.ValidarReferencias(dados,
            (anterior?.Lotacoes ?? []).ToDictionary(l => l.Id),
            (anterior?.Vinculos ?? []).Select(v => v.EmpresaId).ToHashSet(),
            estrutura);
    }

    /// <summary>Nomes das empresas (para as frases do histórico).</summary>
    public async Task<Dictionary<Guid, string>> NomesEmpresasAsync(Pessoa dados, CancellationToken ct) =>
        await _consultas.NomesAsync(dados.Vinculos.Select(v => v.EmpresaId).Distinct().ToList(), ct);

    /// <summary>Nome da empresa de cada vínculo e do gestor de cada lotação (a tela mostra mesmo antes de ler as opções).</summary>
    public async Task PreencherNomesAsync(IReadOnlyList<VinculoDto> vinculos, CancellationToken ct)
    {
        if (vinculos.Count == 0) return;
        var ids = vinculos.Select(v => v.EmpresaId)
            .Concat(vinculos.SelectMany(v => v.Lotacoes).Select(l => l.GestorId).OfType<Guid>())
            .Distinct().ToList();
        var nomes = await _consultas.NomesAsync(ids, ct);
        foreach (var v in vinculos)
        {
            v.Empresa = nomes.GetValueOrDefault(v.EmpresaId);
            foreach (var l in v.Lotacoes)
                l.Gestor = l.GestorId is { } g ? nomes.GetValueOrDefault(g) : null;
        }
    }
}
