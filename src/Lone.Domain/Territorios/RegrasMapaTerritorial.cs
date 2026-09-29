using Lone.Domain.Comum;
using Lone.Domain.Entidades;

namespace Lone.Domain.Territorios;

/// <summary>
/// Regras do mapa territorial (Fase 2b, T1/T9/T16). Não acessa banco. Depois que o mapa tem uso (regra publicada ou
/// atribuição, 2b-1b), empresa, exclusividade, endereço de referência e universo não mudam pelo cadastro: mudá-los
/// mudaria resultados de atribuição sem operação (T5). O código nunca muda depois de criado.
/// </summary>
public static class RegrasMapaTerritorial
{
    /// <summary>A ficha do mapa ficou velha: outro usuário (ou outra janela) gravou o cadastro depois que ela foi aberta.</summary>
    public const string MensagemMapaAlterado =
        "O mapa territorial foi alterado por outro usuário enquanto esta tela estava aberta. Nada foi salvo: recarregue os dados " +
        "antes de salvar.";

    /// <summary>
    /// Conferência do cadastro. <paramref name="todos"/>: os mapas gravados (com os desativados). <paramref name="anterior"/>:
    /// como estava gravado (nulo se novo). <paramref name="emUso"/>: o mapa tem regra publicada ou atribuição.
    /// </summary>
    public static List<string> Validar(MapaTerritorial dados, IReadOnlyCollection<MapaTerritorial> todos, MapaTerritorial? anterior, bool emUso)
    {
        var erros = RegrasCadastroTerritorial.ValidarCodigoENome(dados.Codigo, MapaTerritorial.TamanhoMaximoCodigo, dados.Nome,
            MapaTerritorial.TamanhoMaximoNome, "GEOGRAFIA").ToList();
        if (dados.Descricao?.Length > MapaTerritorial.TamanhoMaximoDescricao)
            erros.Add($"A descrição pode ter no máximo {MapaTerritorial.TamanhoMaximoDescricao} caracteres.");
        if (dados.Codigo.Length > 0 && todos.Any(t => t.Id != dados.Id && t.Codigo == dados.Codigo))
            erros.Add($"Já existe o mapa de código {dados.Codigo}.");
        if (dados.Nome.Length > 0 && todos.Any(t => t.Id != dados.Id && TextoBusca.Normalizar(t.Nome) == TextoBusca.Normalizar(dados.Nome)))
            erros.Add($"Já existe o mapa \"{dados.Nome}\" (ativo ou desativado; maiúsculas e acentos não contam).");
        if (dados.FinalidadeEnderecoReferenciaId == Guid.Empty)
            erros.Add("Escolha a finalidade do endereço de referência (o endereço que as regras de endereço deste mapa vão considerar).");
        if (!dados.ClassificacoesAceitas.Any())
            erros.Add("Escolha ao menos uma classificação no universo do mapa (ex.: Cliente).");

        if (anterior is not null)
        {
            if (anterior.Codigo != dados.Codigo)
                erros.Add("O código do mapa não muda depois de criado (relatórios e integrações dependem dele).");
            if (emUso)
            {
                const string Motivo = " O mapa já tem regras ou atribuições: mudar isto mudaria o resultado de clientes sem uma operação territorial.";
                if (anterior.EmpresaId != dados.EmpresaId) erros.Add("A empresa do mapa não muda depois do uso." + Motivo);
                if (anterior.Exclusivo != dados.Exclusivo) erros.Add("A exclusividade do mapa não muda depois do uso." + Motivo);
                if (anterior.FinalidadeEnderecoReferenciaId != dados.FinalidadeEnderecoReferenciaId)
                    erros.Add("O endereço de referência não muda depois do uso." + Motivo);
                if (!anterior.ClassificacoesAceitas.ToHashSet().SetEquals(dados.ClassificacoesAceitas))
                    erros.Add("O universo do mapa não muda depois do uso." + Motivo);
            }
        }
        return erros;
    }

    /// <summary>Mapa em uso não é desativado pelo cadastro (as atribuições abertas ficariam sem dono).</summary>
    public static IEnumerable<string> ValidarDesativacao(MapaTerritorial mapa, bool emUso)
    {
        if (emUso)
            yield return $"O mapa \"{mapa.Nome}\" tem regras ou atribuições: encerre-as por uma operação territorial antes de desativá-lo.";
    }

    /// <summary>
    /// Universo gravado × marcado: as gravadas continuam (desmarcar desativa, nunca apaga); as novas entram. Mesmo padrão de
    /// "Quem pode ser" do papel comercial.
    /// </summary>
    public static List<MapaTerritorialClassificacao> SincronizarClassificacoes(Guid mapaId, IEnumerable<MapaTerritorialClassificacao> gravadas,
                                                                              IEnumerable<Guid> marcadas)
    {
        var marcadasSet = marcadas.Where(id => id != Guid.Empty).ToHashSet();
        var resultado = gravadas.Select(g => new MapaTerritorialClassificacao
        {
            Id = g.Id, MapaId = mapaId, PapelId = g.PapelId, Ativo = marcadasSet.Contains(g.PapelId), CriadoEm = g.CriadoEm,
            AtualizadoEm = g.AtualizadoEm
        }).ToList();
        foreach (var id in marcadasSet.Where(id => resultado.All(r => r.PapelId != id)))
            resultado.Add(new MapaTerritorialClassificacao { Id = IdSequencial.Novo(), MapaId = mapaId, PapelId = id });
        return resultado;
    }
}
