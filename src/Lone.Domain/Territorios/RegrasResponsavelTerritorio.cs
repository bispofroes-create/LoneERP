using Lone.Domain.Comercial;
using Lone.Domain.Entidades;

namespace Lone.Domain.Territorios;

/// <summary>
/// Responsáveis pelo território (Fase 2b-1a): uma pessoa <b>ou</b> uma equipe, numa função (o papel comercial), com período.
/// A pessoa precisa poder ocupar a função ("Quem pode ser" do papel comercial), como na carteira. O histórico não é
/// reescrito: quem já começou não muda de pessoa, equipe, função nem início (só o fim); quem ainda não começou pode ser
/// corrigido ou anulado. O período fica dentro da vida do território. Não acessa banco.
/// </summary>
public static class RegrasResponsavelTerritorio
{
    private static string Data(DateOnly d) => RegrasCadastroTerritorial.Data(d);

    /// <param name="dados">O território como será gravado (responsáveis já mesclados com os gravados).</param>
    /// <param name="anterior">Como estava gravado (nulo se novo).</param>
    /// <param name="inicioTerritorio">Desde quando o território existe.</param>
    /// <param name="funcoes">Papéis comerciais (com os desativados).</param>
    /// <param name="pessoas">Pessoas ativas entre as citadas, com as classificações ativas (ausente = inexistente ou inativa).</param>
    /// <param name="equipes">Equipes (com as desativadas).</param>
    public static List<string> Validar(Territorio dados, Territorio? anterior, DateOnly inicioTerritorio, DateOnly hoje,
                                       IReadOnlyDictionary<Guid, TipoCarteira> funcoes, IReadOnlyDictionary<Guid, PessoaElegivel> pessoas,
                                       IReadOnlyDictionary<Guid, Equipe> equipes, IReadOnlyDictionary<Guid, string> nomes)
    {
        var erros = new List<string>();
        var antes = anterior?.Responsaveis.ToDictionary(r => r.Id) ?? new Dictionary<Guid, TerritorioResponsavel>();

        foreach (var r in dados.Responsaveis)
        {
            antes.TryGetValue(r.Id, out var gravado);
            var jaComecou = gravado is not null && gravado.InicioEm <= hoje;
            var quem = Quem(r, nomes);

            if (gravado is not null && jaComecou)
            {
                if (gravado.PessoaId != r.PessoaId || gravado.EquipeId != r.EquipeId || gravado.TipoCarteiraId != r.TipoCarteiraId ||
                    gravado.InicioEm != r.InicioEm)
                    erros.Add($"{Quem(gravado, nomes)}: quem já começou em {Data(gravado.InicioEm)} não muda de pessoa, equipe, função nem início " +
                              "(o histórico não é reescrito). Informe o fim e inclua de novo a partir da data desejada.");
                if (gravado.Ativo && !r.Ativo)
                    erros.Add($"{Quem(gravado, nomes)}: quem já começou não é removido; informe o fim.");
            }
            if (!r.Ativo) continue;

            if ((r.PessoaId is null) == (r.EquipeId is null))
                erros.Add("Cada responsável é uma pessoa ou uma equipe (uma das duas).");
            if (r.InicioEm == default) erros.Add($"{quem}: informe o início.");
            if (r.FimEm is { } fim && fim < r.InicioEm) erros.Add($"{quem}: o fim ({Data(fim)}) é anterior ao início ({Data(r.InicioEm)}).");
            if (r.Observacao?.Length > TerritorioResponsavel.TamanhoMaximoObservacao)
                erros.Add($"{quem}: a observação pode ter no máximo {TerritorioResponsavel.TamanhoMaximoObservacao} caracteres.");

            var novoOuMudou = gravado is null || gravado.PessoaId != r.PessoaId || gravado.EquipeId != r.EquipeId ||
                              gravado.TipoCarteiraId != r.TipoCarteiraId || !gravado.Ativo;
            if (!funcoes.TryGetValue(r.TipoCarteiraId, out var funcao))
                erros.Add($"{quem}: escolha a função (papel comercial).");
            else if (novoOuMudou)
            {
                if (!funcao.Ativo) erros.Add($"{quem}: a função \"{funcao.Nome}\" está desativada.");
                if (r.PessoaId is { } pessoaId)
                {
                    if (!pessoas.TryGetValue(pessoaId, out var pessoa))
                        erros.Add($"{quem}: a pessoa não existe mais ou está inativa.");
                    else if (!pessoa.Classificacoes.Overlaps(funcao.ClassificacoesAceitas))
                        erros.Add($"{pessoa.Nome} não pode ser \"{funcao.Nome}\": falta uma das classificações aceitas pelo papel comercial (Quem pode ser).");
                }
                if (r.EquipeId is { } equipeId && (!equipes.TryGetValue(equipeId, out var equipe) || !equipe.Ativo))
                    erros.Add($"{quem}: a equipe não existe mais ou está desativada.");
            }

            if (r.InicioEm != default && r.InicioEm < inicioTerritorio)
                erros.Add($"{quem}: o início ({Data(r.InicioEm)}) é anterior à existência do território (desde {Data(inicioTerritorio)}).");
            if (!dados.Ativo && gravado is null)
                erros.Add("Território encerrado não recebe responsáveis novos.");
        }

        foreach (var grupo in dados.Responsaveis.Where(r => r.Ativo && r.InicioEm != default).GroupBy(r => (r.PessoaId, r.EquipeId, r.TipoCarteiraId)))
        {
            var lista = grupo.OrderBy(r => r.InicioEm).ToList();
            for (var i = 1; i < lista.Count; i++)
                if ((lista[i - 1].FimEm ?? DateOnly.MaxValue) >= lista[i].InicioEm)
                {
                    erros.Add($"{Quem(lista[i], nomes)} aparece duas vezes na mesma função ao mesmo tempo ({Data(lista[i].InicioEm)}): " +
                              "encerre o período anterior antes.");
                    break;
                }
        }
        return erros.Distinct().ToList();
    }

    private static string Quem(TerritorioResponsavel r, IReadOnlyDictionary<Guid, string> nomes) =>
        (r.PessoaId ?? r.EquipeId) is { } id && nomes.TryGetValue(id, out var nome) ? nome : "Responsável";
}
