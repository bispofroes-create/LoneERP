using System.Data.SqlTypes;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Enderecos;

/// <summary>
/// Regras de endereço × finalidade (não acessa banco; o cadastro de finalidades chega pronto).
/// Principal é explícito em cada relação e nunca vem da ordem dos endereços, do Id, do "único endereço" nem de
/// preenchimento automático. As mesmas regras estruturais também estão no banco (FK composta, únicos filtrados, CHECK
/// e gatilhos): aqui elas viram mensagens claras antes de gravar.
/// </summary>
public static class RegrasFinalidadeEndereco
{
    /// <summary>Chave natural da relação: um registro por endereço + finalidade (retirar desativa, voltar reativa).</summary>
    public static (Guid Endereco, Guid Finalidade) Chave(PessoaEnderecoFinalidade u) => (u.PessoaEnderecoId, u.FinalidadeId);

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
    /// inclusão): finalidade desativada no cadastro só continua onde já estava ativa (e só continua principal onde já era).
    /// </summary>
    public static List<string> Validar(Pessoa p, IReadOnlyDictionary<Guid, FinalidadeEnderecoCadastro> cadastro,
                                       IEnumerable<PessoaEnderecoFinalidade> anteriores)
    {
        var erros = new List<string>();
        var enderecos = p.Enderecos.ToDictionary(e => e.Id);
        var antes = anteriores.ToList();
        var jaAtivas = antes.Where(a => a.Ativo).Select(Chave).ToHashSet();
        var jaPrincipais = antes.Where(a => a.Principal).Select(Chave).ToHashSet();
        string Nome(Guid id) => cadastro.TryGetValue(id, out var f) ? f.Nome : "(finalidade)";
        string Resumo(Guid id) => enderecos.TryGetValue(id, out var e) ? DuplicidadeEndereco.Resumo(e) : "(endereço)";

        foreach (var u in p.FinalidadesEnderecos)
        {
            if (!enderecos.ContainsKey(u.PessoaEnderecoId))
                erros.Add("Uma finalidade aponta para um endereço que não é desta pessoa.");
            if (!cadastro.TryGetValue(u.FinalidadeId, out var finalidade))
                erros.Add("Finalidade de endereço que não existe no cadastro.");
            else if (!finalidade.Ativo)
            {
                if (u.Ativo && !jaAtivas.Contains(Chave(u)))
                    erros.Add($"A finalidade {finalidade.Nome} está desativada no cadastro: não pode ser usada em associação nova.");
                if (u.Principal && !jaPrincipais.Contains(Chave(u)))
                    erros.Add($"A finalidade {finalidade.Nome} está desativada no cadastro: não pode receber um endereço principal novo.");
            }
        }

        // Um registro por endereço + finalidade (ativo ou histórico): a reativação reaproveita a mesma linha.
        foreach (var repetida in p.FinalidadesEnderecos.GroupBy(Chave).Where(g => g.Count() > 1))
            erros.Add($"O endereço {Resumo(repetida.Key.Endereco)} já possui a finalidade {Nome(repetida.Key.Finalidade)}.");

        foreach (var conflito in p.FinalidadesEnderecos.Where(u => u.Principal).GroupBy(u => u.FinalidadeId).Where(g => g.Count() > 1))
            erros.Add($"Mais de um endereço principal para {Nome(conflito.Key)}: escolha um só.");

        erros.AddRange(ValidarConsolidacao(p));
        return erros.Distinct().ToList();
    }

    /// <summary>
    /// Contrato da gravação da ficha: ela envia o ESTADO COMPLETO dos endereços e das finalidades (inclusive os
    /// removidos, como inativos). Endereço ou relação gravados que não vierem são recusados — nada fica "esquecido" no
    /// banco sem passar pelas regras (ex.: um principal antigo que não veio e continuaria valendo).
    /// </summary>
    public static List<string> ValidarCompleto(Pessoa dados, Pessoa anterior)
    {
        var erros = new List<string>();
        var enderecos = dados.Enderecos.Select(e => e.Id).ToHashSet();
        var relacoes = dados.FinalidadesEnderecos.Select(Chave).ToHashSet();

        var faltaEndereco = anterior.Enderecos.Where(e => !enderecos.Contains(e.Id)).ToList();
        if (faltaEndereco.Count > 0)
            erros.Add("A ficha enviada não traz todos os endereços já gravados (ex.: " +
                      $"{DuplicidadeEndereco.Resumo(faltaEndereco[0])}). Nada foi salvo: recarregue o cadastro e tente de novo.");

        var faltaRelacao = anterior.FinalidadesEnderecos.Where(u => !relacoes.Contains(Chave(u))).ToList();
        if (faltaRelacao.Count > 0)
            erros.Add("A ficha enviada não traz todas as finalidades de endereço já gravadas (inclusive as retiradas). " +
                      "Nada foi salvo: recarregue o cadastro e tente de novo.");
        return erros;
    }

    /// <summary>
    /// Endereço consolidado em outro: fica inativo e aponta para outro endereço ativo da mesma pessoa. Reativar um
    /// consolidado não é permitido (o banco também recusa, pelo CHECK): usa-se o endereço mantido.
    /// </summary>
    public static IEnumerable<string> ValidarConsolidacao(Pessoa p)
    {
        var enderecos = p.Enderecos.ToDictionary(e => e.Id);
        foreach (var e in p.Enderecos.Where(e => e.MescladoEmId is not null))
        {
            if (e.Ativo)
                yield return $"O endereço {DuplicidadeEndereco.Resumo(e)} foi consolidado em outro e precisa ficar inativo (use o endereço mantido).";
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
    /// Endereço de referência da listagem (só exibição de cidade/UF em listas e exportações): entre os endereços ativos,
    /// o que é principal da finalidade ativa de menor ordem no cadastro; sem nenhum principal, o primeiro endereço
    /// ativo. Desempate determinístico: Ordem do endereço e depois o Id (comparado como o SQL Server compara, para dar
    /// o mesmo resultado de PessoaRepositorio.ComReferencia). Não é "o principal da pessoa" e não muda principalidade.
    /// </summary>
    public static PessoaEndereco? EnderecoReferencia(Pessoa p, IReadOnlyDictionary<Guid, FinalidadeEnderecoCadastro> cadastro)
    {
        int OrdemPrincipal(PessoaEndereco e) => p.FinalidadesEnderecos
            .Where(u => u.PessoaEnderecoId == e.Id && u.Principal && u.Ativo && cadastro.TryGetValue(u.FinalidadeId, out var f) && f.Ativo)
            .Select(u => (int?)cadastro[u.FinalidadeId].Ordem)
            .Min() ?? int.MaxValue;

        return p.Enderecos.Where(e => e.Ativo)
            .OrderBy(OrdemPrincipal)
            .ThenBy(e => e.Ordem)
            .ThenBy(e => new SqlGuid(e.Id))
            .FirstOrDefault();
    }

    /// <summary>
    /// Coluna legada PessoaEnderecos.Finalidades (representação derivada, só compatibilidade): bits das finalidades
    /// ativas de cada endereço + bit 1 no endereço de referência da listagem. Regravada a cada gravação, na mesma
    /// transação. Nenhuma regra, consulta ou API decide por essa coluna.
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

    // ---------------------------------------------------------------- Revisão deixada pela migração

    /// <summary>
    /// Mantém os motivos de revisão gravados (o aplicativo só pode APAGAR um motivo — "marcar como revisado" —, nunca
    /// ligar), apaga os já resolvidos e recalcula a marca geral da pessoa: ela só se mantém enquanto houver pendência,
    /// e nunca é ligada fora da migração. Endereço sem finalidade, por si só, NÃO é pendência.
    /// </summary>
    public static void AtualizarRevisao(Pessoa dados, Pessoa? anterior)
    {
        var gravados = anterior?.Enderecos.ToDictionary(e => e.Id, e => e.RevisaoMigracao) ?? new Dictionary<Guid, MotivoRevisaoEndereco>();
        foreach (var e in dados.Enderecos)
        {
            var motivo = (gravados.TryGetValue(e.Id, out var m) ? m : MotivoRevisaoEndereco.Nenhum) & e.RevisaoMigracao;
            if (!e.Ativo)
                motivo = MotivoRevisaoEndereco.Nenhum; // endereço fora de uso: não há o que revisar nele
            else if (dados.FinalidadesEnderecos.Any(u => u.Ativo && u.PessoaEnderecoId == e.Id))
                motivo &= ~MotivoRevisaoEndereco.AntigoPrincipalSemFinalidade; // o usuário já disse para que ele serve
            e.RevisaoMigracao = motivo;
        }

        dados.RevisarFinalidadesEndereco = (anterior?.RevisarFinalidadesEndereco ?? false) &&
                                           PendenciasRevisao(dados, _ => string.Empty).Count > 0;
    }

    /// <summary>
    /// Pendências da revisão (mostradas enquanto a pessoa está marcada): cada finalidade usada por 2 ou mais endereços
    /// ativos sem principal, e cada endereço ativo com motivo gravado pela migração. Vazio = revisão concluída.
    /// </summary>
    public static List<string> PendenciasRevisao(Pessoa p, Func<Guid, string> nomeFinalidade)
    {
        var pendencias = new List<string>();
        var ativos = p.Enderecos.Where(e => e.Ativo).ToList();
        foreach (var id in FinalidadesSemPrincipal(p))
        {
            var quantos = p.FinalidadesEnderecos.Count(u => u.Ativo && u.FinalidadeId == id && ativos.Any(e => e.Id == u.PessoaEnderecoId));
            pendencias.Add(TextoAmbiguidade(nomeFinalidade(id), quantos));
        }
        foreach (var e in ativos)
            pendencias.AddRange(TextosMotivo(DuplicidadeEndereco.Resumo(e), e.RevisaoMigracao));
        return pendencias;
    }

    /// <summary>Texto da ambiguidade de principal (o mesmo na API e na ficha).</summary>
    public static string TextoAmbiguidade(string finalidade, int quantos) =>
        $"Há {quantos} endereços com a finalidade {finalidade} e nenhum deles pôde ser identificado como principal a partir " +
        "dos dados antigos. Defina o principal (ou retire a finalidade dos endereços que não a usam).";

    /// <summary>Textos dos motivos gravados pela migração para um endereço (o mesmo na API e na ficha).</summary>
    public static IEnumerable<string> TextosMotivo(string endereco, MotivoRevisaoEndereco motivo)
    {
        if (motivo.HasFlag(MotivoRevisaoEndereco.AntigoPrincipalSemFinalidade))
            yield return $"{endereco}: nos dados antigos era o endereço principal, mas não tinha nenhuma finalidade (nenhuma foi " +
                         "inventada). Escolha para que ele serve ou marque como revisado.";
        if (motivo.HasFlag(MotivoRevisaoEndereco.AntigoPrincipalRepetido))
            yield return $"{endereco}: nos dados antigos mais de um endereço estava marcado como principal. Confira o principal " +
                         "de cada finalidade e marque como revisado.";
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
