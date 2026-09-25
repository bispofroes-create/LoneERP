using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Enderecos;

/// <summary>
/// Regras de endereço × finalidade (não acessa banco; o cadastro de finalidades chega pronto).
/// Principal é explícito em cada relação e nunca vem da ordem dos endereços.
/// </summary>
public static class RegrasFinalidadeEndereco
{
    /// <summary>
    /// Relação inativa, ou de endereço inativo, nunca é principal (desativar o endereço ou retirar a finalidade tira o
    /// principal; a relação fica como histórico). Não corrige repetições nem dois principais: isso o validador acusa.
    /// </summary>
    public static void Normalizar(Pessoa p)
    {
        var ativos = p.Enderecos.Where(e => e.Ativo).Select(e => e.Id).ToHashSet();
        foreach (var u in p.FinalidadesEnderecos)
        {
            u.PessoaId = p.Id;
            if (!u.Ativo || !ativos.Contains(u.PessoaEnderecoId)) u.Principal = false;
        }
    }

    /// <summary>
    /// Validação com o cadastro de finalidades. <paramref name="anteriores"/> = relações gravadas antes (vazio na
    /// inclusão): finalidade desativada no cadastro só continua onde já estava ativa.
    /// </summary>
    public static List<string> Validar(Pessoa p, IReadOnlyDictionary<Guid, FinalidadeEnderecoCadastro> cadastro,
                                       IEnumerable<PessoaEnderecoFinalidade> anteriores)
    {
        var erros = new List<string>();
        var enderecos = p.Enderecos.ToDictionary(e => e.Id);
        var jaAtivas = anteriores.Where(a => a.Ativo).Select(a => (a.PessoaEnderecoId, a.FinalidadeId)).ToHashSet();
        string Nome(Guid id) => cadastro.TryGetValue(id, out var f) ? f.Nome : "(finalidade)";
        string Resumo(Guid id) => enderecos.TryGetValue(id, out var e) ? DuplicidadeEndereco.Resumo(e) : "(endereço)";

        foreach (var u in p.FinalidadesEnderecos)
        {
            if (!enderecos.ContainsKey(u.PessoaEnderecoId))
                erros.Add("Uma finalidade aponta para um endereço que não é desta pessoa.");
            if (!cadastro.TryGetValue(u.FinalidadeId, out var finalidade))
                erros.Add("Finalidade de endereço que não existe no cadastro.");
            else if (!finalidade.Ativo && u.Ativo && !jaAtivas.Contains((u.PessoaEnderecoId, u.FinalidadeId)))
                erros.Add($"A finalidade {finalidade.Nome} está desativada no cadastro: não pode ser usada em associação nova.");
        }

        foreach (var repetida in p.FinalidadesEnderecos.Where(u => u.Ativo).GroupBy(u => (u.PessoaEnderecoId, u.FinalidadeId)).Where(g => g.Count() > 1))
            erros.Add($"O endereço {Resumo(repetida.Key.PessoaEnderecoId)} já possui a finalidade {Nome(repetida.Key.FinalidadeId)}.");

        foreach (var conflito in p.FinalidadesEnderecos.Where(u => u.Principal).GroupBy(u => u.FinalidadeId).Where(g => g.Count() > 1))
            erros.Add($"Mais de um endereço principal para {Nome(conflito.Key)}: escolha um só.");

        erros.AddRange(ValidarConsolidacao(p));
        return erros.Distinct().ToList();
    }

    /// <summary>Endereço consolidado em outro: fica inativo e aponta para outro endereço ativo da mesma pessoa.</summary>
    public static IEnumerable<string> ValidarConsolidacao(Pessoa p)
    {
        var enderecos = p.Enderecos.ToDictionary(e => e.Id);
        foreach (var e in p.Enderecos.Where(e => e.MescladoEmId is not null))
        {
            if (e.Ativo)
                yield return $"O endereço {DuplicidadeEndereco.Resumo(e)} foi consolidado em outro e precisa ficar inativo.";
            if (e.MescladoEmId == e.Id || !enderecos.TryGetValue(e.MescladoEmId!.Value, out var destino) || !destino.Ativo)
                yield return $"O endereço {DuplicidadeEndereco.Resumo(e)} foi consolidado num endereço que não está ativo nesta ficha.";
        }
    }

    /// <summary>Endereço marcado como principal para a finalidade (explícito; nulo = nenhum).</summary>
    public static PessoaEndereco? EnderecoPrincipal(Pessoa p, Guid finalidadeId) =>
        p.FinalidadesEnderecos.Where(u => u.Principal && u.Ativo && u.FinalidadeId == finalidadeId)
            .Select(u => p.Enderecos.FirstOrDefault(e => e.Id == u.PessoaEnderecoId && e.Ativo))
            .FirstOrDefault(e => e is not null);

    /// <summary>
    /// Endereço de referência da listagem (só exibição de cidade/UF em listas e exportações): o principal da finalidade
    /// ativa de menor ordem no cadastro; sem nenhum principal, o primeiro endereço ativo. Não é "o principal da pessoa"
    /// e não cria nem muda principalidade.
    /// </summary>
    public static PessoaEndereco? EnderecoReferencia(Pessoa p, IReadOnlyDictionary<Guid, FinalidadeEnderecoCadastro> cadastro)
    {
        var principal = p.FinalidadesEnderecos
            .Where(u => u.Principal && u.Ativo && cadastro.TryGetValue(u.FinalidadeId, out var f) && f.Ativo)
            .OrderBy(u => cadastro[u.FinalidadeId].Ordem)
            .Select(u => p.Enderecos.FirstOrDefault(e => e.Id == u.PessoaEnderecoId && e.Ativo))
            .FirstOrDefault(e => e is not null);
        return principal ?? p.Enderecos.Where(e => e.Ativo).OrderBy(e => e.Ordem).FirstOrDefault();
    }

    /// <summary>
    /// Coluna legada PessoaEnderecos.Finalidades (representação derivada, só compatibilidade): bits das finalidades
    /// ativas de cada endereço + bit 1 no endereço de referência da listagem. Regravada a cada gravação, na mesma
    /// transação. Nenhuma regra, consulta ou API lê essa coluna.
    /// </summary>
    public static void SincronizarLegado(Pessoa p, PessoaEndereco? referencia)
    {
        foreach (var e in p.Enderecos)
        {
            var bits = p.FinalidadesEnderecos
                .Where(u => u.PessoaEnderecoId == e.Id && u.Ativo)
                .Aggregate(FinalidadeEndereco.Nenhuma, (total, u) => total | FinalidadesEnderecoIniciais.BitLegado(u.FinalidadeId));
            if (ReferenceEquals(e, referencia)) bits |= FinalidadeEndereco.Principal;
            e.Finalidades = bits;
        }
    }

    /// <summary>
    /// Pendências da revisão de finalidades (só enquanto a pessoa está marcada pela migração): cada finalidade usada por
    /// 2 ou mais endereços ativos sem principal, e cada endereço ativo sem nenhuma finalidade. Vazio = revisão concluída.
    /// </summary>
    public static List<string> PendenciasRevisao(Pessoa p, Func<Guid, string> nomeFinalidade)
    {
        var pendencias = new List<string>();
        var ativos = p.Enderecos.Where(e => e.Ativo).ToList();
        foreach (var id in FinalidadesSemPrincipal(p))
        {
            var quantos = p.FinalidadesEnderecos.Count(u => u.Ativo && u.FinalidadeId == id && ativos.Any(e => e.Id == u.PessoaEnderecoId));
            pendencias.Add($"{nomeFinalidade(id)}: existem {quantos} endereços e nenhum foi definido como principal.");
        }
        foreach (var e in ativos.Where(e => !p.FinalidadesEnderecos.Any(u => u.Ativo && u.PessoaEnderecoId == e.Id)))
            pendencias.Add($"{DuplicidadeEndereco.Resumo(e)}: endereço sem finalidade (escolha para que ele serve).");
        return pendencias;
    }

    /// <summary>Finalidades usadas por 2 ou mais endereços ativos sem nenhum principal (definição manual pendente).</summary>
    public static List<Guid> FinalidadesSemPrincipal(Pessoa p)
    {
        var ativos = p.Enderecos.Where(e => e.Ativo).Select(e => e.Id).ToHashSet();
        return p.FinalidadesEnderecos
            .Where(u => u.Ativo && ativos.Contains(u.PessoaEnderecoId))
            .GroupBy(u => u.FinalidadeId)
            .Where(g => g.Count() > 1 && !g.Any(u => u.Principal))
            .Select(g => g.Key)
            .ToList();
    }
}
