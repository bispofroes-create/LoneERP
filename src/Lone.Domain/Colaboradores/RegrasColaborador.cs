using System.Globalization;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Colaboradores;

/// <summary>Cadastros usados na conferência das lotações (com os desativados).</summary>
public sealed record EstruturaParaConferir(
    IReadOnlyDictionary<Guid, Cargo> Cargos,
    IReadOnlyDictionary<Guid, Departamento> Departamentos,
    IReadOnlyDictionary<Guid, Setor> Setores,
    IReadOnlyDictionary<Guid, CentroCusto> CentrosCusto,
    IReadOnlySet<Guid> Empresas,
    IReadOnlySet<Guid> Gestores);

/// <summary>
/// Regras dos dados de colaborador: vínculos (admissão/desligamento) e lotações com vigência. Não acessa banco.
/// Vínculos e lotações nunca são apagados; o período de uma lotação fica dentro do vínculo, sem sobreposição.
/// </summary>
public static class RegrasColaborador
{
    public static void Normalizar(Pessoa p)
    {
        foreach (var v in p.Vinculos)
        {
            v.Matricula = Texto(v.Matricula)?.ToUpperInvariant();
            v.MotivoDesligamento = v.DesligamentoEm is null ? null : Texto(v.MotivoDesligamento);
            v.Observacoes = Texto(v.Observacoes);
        }

        // Lotação de vínculo desconhecido não tem onde ficar.
        var vinculos = p.Vinculos.Select(v => v.Id).ToHashSet();
        p.Lotacoes.RemoveAll(l => !vinculos.Contains(l.VinculoId));
        foreach (var l in p.Lotacoes)
        {
            if (l.CargoId == Guid.Empty) l.CargoId = null;
            if (l.DepartamentoId == Guid.Empty) l.DepartamentoId = null;
            if (l.SetorId == Guid.Empty) l.SetorId = null;
            if (l.CentroCustoId == Guid.Empty) l.CentroCustoId = null;
            if (l.GestorId == Guid.Empty) l.GestorId = null;
        }
    }

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string? Texto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Datas, tamanhos e períodos (sem consultar cadastros).</summary>
    public static List<string> Validar(Pessoa p)
    {
        var erros = new List<string>();
        if (p.Vinculos.Count > 0 && p.Natureza != NaturezaPessoa.Fisica)
            erros.Add("Dados de colaborador são só de pessoa física.");

        for (var i = 0; i < p.Vinculos.Count; i++)
        {
            var v = p.Vinculos[i];
            var rotulo = $"Vínculo {i + 1}";
            if (v.EmpresaId == Guid.Empty) erros.Add($"{rotulo}: escolha a empresa.");
            if (v.AdmissaoEm == default) erros.Add($"{rotulo}: informe a data de admissão.");
            if (v.DesligamentoEm is { } fim && fim < v.AdmissaoEm) erros.Add($"{rotulo}: o desligamento é anterior à admissão.");
            if (v.Matricula is { Length: > VinculoColaborador.TamanhoMaximoMatricula })
                erros.Add($"{rotulo}: a matrícula pode ter no máximo {VinculoColaborador.TamanhoMaximoMatricula} caracteres.");
            if (v.MotivoDesligamento is { Length: > VinculoColaborador.TamanhoMaximoTexto } || v.Observacoes is { Length: > VinculoColaborador.TamanhoMaximoTexto })
                erros.Add($"{rotulo}: textos de no máximo {VinculoColaborador.TamanhoMaximoTexto} caracteres.");
            if (v.JornadaSemanal is < 0 or > 168) erros.Add($"{rotulo}: jornada semanal entre 0 e 168 horas.");

            var lotacoes = p.Lotacoes.Where(l => l.VinculoId == v.Id).OrderBy(l => l.InicioEm).ToList();
            if (lotacoes.Count(l => l.FimEm is null) > 1)
                erros.Add($"{rotulo}: só uma lotação pode ficar em aberto (sem fim).");
            foreach (var l in lotacoes)
            {
                if (l.FimEm is { } f && f < l.InicioEm) erros.Add($"{rotulo}: uma lotação termina antes de começar.");
                if (l.InicioEm < v.AdmissaoEm) erros.Add($"{rotulo}: uma lotação começa antes da admissão.");
                if (v.DesligamentoEm is { } desligado && (l.InicioEm > desligado || (l.FimEm ?? DateOnly.MaxValue) > desligado))
                    erros.Add($"{rotulo}: uma lotação passa da data de desligamento (encerre-a no desligamento).");
            }
            for (var j = 1; j < lotacoes.Count; j++)
                if ((lotacoes[j - 1].FimEm ?? DateOnly.MaxValue) >= lotacoes[j].InicioEm)
                    erros.Add($"{rotulo}: os períodos de lotação se sobrepõem ({Data(lotacoes[j].InicioEm)}).");
        }

        var duplicada = p.Vinculos.Where(v => v.Matricula is not null)
            .GroupBy(v => (v.EmpresaId, v.Matricula)).FirstOrDefault(g => g.Count() > 1);
        if (duplicada is not null) erros.Add($"A matrícula {duplicada.Key.Matricula} está repetida na mesma empresa.");
        return erros.Distinct().ToList();
    }

    /// <summary>
    /// Referências das lotações: cadastros existentes (desativado só se já era o daquela lotação), setor do departamento
    /// escolhido, centro de custo analítico, empresa do grupo, gestor que não é a própria pessoa.
    /// </summary>
    /// <param name="anteriores">Lotações gravadas (Id → lotação), para aceitar o que já estava lá.</param>
    public static List<string> ValidarReferencias(Pessoa p, IReadOnlyDictionary<Guid, LotacaoColaborador> anteriores,
                                                  IReadOnlySet<Guid> empresasAnteriores, EstruturaParaConferir estrutura)
    {
        var erros = new List<string>();
        foreach (var v in p.Vinculos.Where(v => v.EmpresaId != Guid.Empty))
            if (!estrutura.Empresas.Contains(v.EmpresaId) && !empresasAnteriores.Contains(v.EmpresaId))
                erros.Add("Um vínculo aponta para uma empresa que não é do grupo.");

        foreach (var l in p.Lotacoes)
        {
            var antes = anteriores.GetValueOrDefault(l.Id);
            Conferir(l.CargoId, antes?.CargoId, estrutura.Cargos, c => c.Ativo, c => c.Nome, "cargo", erros);
            Conferir(l.DepartamentoId, antes?.DepartamentoId, estrutura.Departamentos, d => d.Ativo, d => d.Nome, "departamento", erros);
            Conferir(l.SetorId, antes?.SetorId, estrutura.Setores, s => s.Ativo, s => s.Nome, "setor", erros);
            Conferir(l.CentroCustoId, antes?.CentroCustoId, estrutura.CentrosCusto, c => c.Ativo, c => c.Descricao, "centro de custo", erros);

            if (l.SetorId is { } setorId && estrutura.Setores.TryGetValue(setorId, out var setor) && setor.DepartamentoId != l.DepartamentoId)
                erros.Add($"O setor \"{setor.Nome}\" não é do departamento escolhido.");
            if (l.CentroCustoId is { } ccId && ccId != antes?.CentroCustoId &&
                estrutura.CentrosCusto.TryGetValue(ccId, out var cc) && !cc.Analitico)
                erros.Add($"O centro de custo \"{cc.Descricao}\" é sintético (agrupa outros): escolha um analítico.");
            if (l.GestorId is { } gestor)
            {
                if (gestor == p.Id) erros.Add("A pessoa não pode ser gestora de si mesma.");
                else if (!estrutura.Gestores.Contains(gestor) && antes?.GestorId != gestor)
                    erros.Add("O gestor escolhido não foi encontrado (precisa ser uma pessoa física ativa).");
            }
        }
        return erros.Distinct().ToList();
    }

    private static void Conferir<T>(Guid? id, Guid? anterior, IReadOnlyDictionary<Guid, T> cadastro, Func<T, bool> ativo,
                                    Func<T, string> nome, string oQue, List<string> erros)
    {
        if (id is not { } valor) return;
        if (!cadastro.TryGetValue(valor, out var item))
            erros.Add($"Um {oQue} escolhido não existe mais. Reabra o cadastro e escolha de novo.");
        else if (!ativo(item) && anterior != valor)
            erros.Add($"O {oQue} \"{nome(item)}\" está desativado e não pode ser escolhido em novas lotações.");
    }

    /// <summary>Frases do histórico para admissão e desligamento (os demais campos já aparecem campo a campo).</summary>
    public static IEnumerable<string> Mudancas(IReadOnlyCollection<VinculoColaborador> anteriores, IEnumerable<VinculoColaborador> atuais,
                                               IReadOnlyDictionary<Guid, string> nomesEmpresas)
    {
        var porId = anteriores.ToDictionary(v => v.Id);
        foreach (var v in atuais)
        {
            var empresa = nomesEmpresas.GetValueOrDefault(v.EmpresaId, "empresa do grupo");
            var antes = porId.GetValueOrDefault(v.Id);
            if (antes is null)
                yield return $"Admitido em {Data(v.AdmissaoEm)} em {empresa}.";
            if (v.DesligamentoEm is { } fim && antes?.DesligamentoEm is null)
                yield return $"Desligado em {Data(fim)} de {empresa}" + (v.MotivoDesligamento is { } m ? $" ({m})." : ".");
        }
    }
}
