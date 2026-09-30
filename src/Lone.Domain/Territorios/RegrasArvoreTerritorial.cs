using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Territorios;

/// <summary>
/// Regras da árvore de territórios (Fase 2b-1a). Não acessa banco; a mesma conferência roda de novo contra a árvore lida
/// antes de gravar, e a gravação é serializada pela trava da árvore do mapa (duas mudanças simultâneas que juntas formariam
/// um ciclo não passam: a segunda falha no banco). O banco também recusa ciclo, mais de 12 níveis e posições que se cruzam. Sem uso operacional (regra publicada ou atribuição no nó ou abaixo dele), a
/// árvore se reorganiza livremente e a posição é só corrigida (nada dependia dela; a auditoria registra). Com uso, mover,
/// encerrar, reativar e mudar o código ou o início só por operação territorial (T14/T18, 2b-1b).
/// </summary>
public static class RegrasArvoreTerritorial
{
    /// <summary>Profundidade máxima (Brasil › Região › UF › Mesorregião › Microrregião › Município › Bairro › Setor = 8). Proteção, não regra de negócio.</summary>
    public const int ProfundidadeMaxima = 12;

    /// <summary>
    /// A árvore mudou depois que a tela foi carregada (outro usuário, outra janela ou gravação simultânea): a mudança de
    /// estrutura foi decidida olhando uma árvore velha e não é gravada (trava da árvore, D1 = B).
    /// </summary>
    public const string MensagemArvoreAlterada =
        "A árvore deste mapa foi alterada por outro usuário (ou em outra janela) enquanto esta tela estava aberta. Nada foi " +
        "salvo: recarregue a árvore, confira e refaça a mudança.";

    /// <summary>O banco barrou a árvore (ciclo, mais de 12 níveis ou posições que se cruzam): só por dado gravado por fora.</summary>
    /// <summary>
    /// Fase 2b-1b (L3): enquanto a tela estava aberta, uma operação territorial foi aplicada no mapa e mudou quais territórios
    /// têm uso operacional; a mudança de estrutura foi conferida com o uso antigo.
    /// </summary>
    public const string MensagemUsoMudou =
        "Enquanto esta tela estava aberta, uma operação territorial foi aplicada neste mapa e mudou quais territórios têm regras " +
        "ou clientes. Nada foi salvo: recarregue a árvore e confira a mudança de novo (território com uso só muda de estrutura " +
        "por operação territorial).";

    public const string MensagemArvoreRecusadaPeloBanco =
        "O banco de dados recusou a gravação porque a árvore ficaria inválida (ciclo, mais de 12 níveis ou posições que se " +
        "cruzam). Nada foi salvo: recarregue a árvore e confira; se persistir, avise o suporte.";

    /// <summary>Mudança de estrutura sem dizer qual árvore a tela mostrava (ex.: integração que não leu a árvore antes).</summary>
    public const string MensagemSemVersaoArvore =
        "Mudança na árvore sem a versão da árvore que foi consultada: carregue a árvore do mapa antes e envie a versão dela.";

    private const string SoPorOperacao =
        "só por uma operação territorial, porque ele (ou um território abaixo dele) já tem regras ou atribuições";

    private static string Data(DateOnly d) => RegrasCadastroTerritorial.Data(d);

    /// <summary>Desde quando o território existe: o início da primeira posição válida (nulo se ainda não tem posição).</summary>
    public static DateOnly? InicioDe(Territorio t) => t.Posicoes.Where(p => p.Ativo).Select(p => (DateOnly?)p.InicioEm).Min();

    /// <summary>A posição aberta (a de hoje em diante), se houver.</summary>
    public static TerritorioPosicao? PosicaoAberta(Territorio t) =>
        t.Posicoes.Where(p => p.Ativo).OrderByDescending(p => p.InicioEm).FirstOrDefault(p => p.FimEm is null)
        ?? t.Posicoes.Where(p => p.Ativo).OrderByDescending(p => p.InicioEm).FirstOrDefault();

    /// <summary>Os territórios abaixo de <paramref name="id"/> (filhos, netos...), ativos e encerrados. Resiste a ciclo gravado.</summary>
    public static HashSet<Guid> Descendentes(IEnumerable<Territorio> doMapa, Guid id)
    {
        var filhos = doMapa.Where(t => t.PaiId is not null).ToLookup(t => t.PaiId!.Value, t => t.Id);
        var resultado = new HashSet<Guid>();
        var fila = new Queue<(Guid Id, int Nivel)>();
        fila.Enqueue((id, 0));
        while (fila.Count > 0)
        {
            var (atual, nivel) = fila.Dequeue();
            if (nivel >= ProfundidadeMaxima * 2) continue;
            foreach (var filho in filhos[atual])
                if (filho != id && resultado.Add(filho)) fila.Enqueue((filho, nivel + 1));
        }
        return resultado;
    }

    /// <summary>Os ancestrais de <paramref name="paiId"/> em diante, do pai até a raiz (sem repetir; resiste a ciclo gravado).</summary>
    public static List<Territorio> Caminho(IReadOnlyDictionary<Guid, Territorio> porId, Guid? paiId)
    {
        var caminho = new List<Territorio>();
        var vistos = new HashSet<Guid>();
        while (paiId is { } atual && porId.TryGetValue(atual, out var t) && vistos.Add(atual) && caminho.Count <= ProfundidadeMaxima * 2)
        {
            caminho.Add(t);
            paiId = t.PaiId;
        }
        return caminho;
    }

    /// <summary>Nível do território na árvore (raiz = 1).</summary>
    public static int Nivel(IReadOnlyDictionary<Guid, Territorio> porId, Territorio t) => Caminho(porId, t.PaiId).Count + 1;

    /// <summary>Quantos níveis há abaixo do território (sem filhos = 0).</summary>
    public static int Altura(IEnumerable<Territorio> doMapa, Guid id)
    {
        var filhos = doMapa.Where(t => t.PaiId is not null).ToLookup(t => t.PaiId!.Value, t => t.Id);
        var altura = 0;
        var nivel = new List<Guid> { id };
        var vistos = new HashSet<Guid> { id };
        while (altura < ProfundidadeMaxima * 2)
        {
            var proximo = nivel.SelectMany(n => filhos[n]).Where(vistos.Add).ToList();
            if (proximo.Count == 0) break;
            altura++;
            nivel = proximo;
        }
        return altura;
    }

    /// <summary>O território ou algum abaixo dele tem uso operacional (T18: mover muda o desempate por especificidade).</summary>
    public static bool UsoNaSubarvore(IEnumerable<Territorio> doMapa, Guid id, IReadOnlySet<Guid> comUso) =>
        comUso.Contains(id) || (comUso.Count > 0 && Descendentes(doMapa, id).Any(comUso.Contains));

    /// <summary>
    /// Monta as posições que serão gravadas. Novo: uma posição desde <paramref name="inicio"/>, sob o pai escolhido. Sem uso:
    /// corrige a posição aberta (pai e, se for a única, o início). Com uso: o pai não é corrigido e um início diferente só
    /// entra para que a conferência o recuse.
    /// </summary>
    public static void AjustarPosicoes(Territorio dados, Territorio? anterior, DateOnly inicio, bool usoNaSubarvore)
    {
        if (anterior is null)
        {
            dados.Posicoes =
            [
                new TerritorioPosicao { Id = IdSequencial.Novo(), MapaId = dados.MapaId, TerritorioId = dados.Id, PaiId = dados.PaiId, InicioEm = inicio }
            ];
            return;
        }

        dados.Posicoes = anterior.Posicoes.Select(Copia).ToList();
        if (PosicaoAberta(dados) is not { } aberta) return;
        // Com uso a posição não é corrigida; o início pedido entra só para a conferência recusar (nunca some em silêncio).
        if (!usoNaSubarvore) aberta.PaiId = dados.PaiId;
        if (dados.Posicoes.Count(p => p.Ativo) == 1) aberta.InicioEm = inicio;
    }

    private static TerritorioPosicao Copia(TerritorioPosicao p) => new()
    {
        Id = p.Id, MapaId = p.MapaId, TerritorioId = p.TerritorioId, PaiId = p.PaiId, InicioEm = p.InicioEm, FimEm = p.FimEm, Ativo = p.Ativo,
        CriadoEm = p.CriadoEm, AtualizadoEm = p.AtualizadoEm
    };

    /// <summary>
    /// Conferência completa de um território (novo ou alterado; o encerramento e a reativação têm conferência própria).
    /// <paramref name="doMapa"/>: os territórios gravados do mapa, com as posições (o próprio pode vir junto).
    /// <paramref name="comUso"/>: territórios com regra publicada ou atribuição (vazio na 2b-1a).
    /// </summary>
    public static List<string> Validar(Territorio dados, Territorio? anterior, IReadOnlyCollection<Territorio> doMapa, MapaTerritorial? mapa,
                                       IReadOnlyDictionary<Guid, TipoTerritorio> tipos, IReadOnlySet<Guid> comUso, DateOnly hoje)
    {
        var erros = new List<string>();
        if (mapa is null)
        {
            erros.Add("O mapa territorial escolhido não existe mais.");
            return erros;
        }
        if (!mapa.Ativo) erros.Add($"O mapa \"{mapa.Nome}\" está desativado: reative-o para alterar a árvore.");
        if (anterior is not null && anterior.MapaId != dados.MapaId) erros.Add("Um território não muda de mapa.");
        if (anterior is { Ativo: false })
        {
            erros.Add($"O território \"{anterior.Nome}\" está encerrado: reative-o antes de alterar.");
            return erros;
        }

        erros.AddRange(RegrasCadastroTerritorial.ValidarCodigoENome(dados.Codigo, Territorio.TamanhoMaximoCodigo, dados.Nome,
            Territorio.TamanhoMaximoNome, "MG_NORTE"));
        if (dados.Descricao?.Length > Territorio.TamanhoMaximoDescricao)
            erros.Add($"A descrição pode ter no máximo {Territorio.TamanhoMaximoDescricao} caracteres.");

        var outros = doMapa.Where(t => t.Id != dados.Id).ToList();
        if (dados.Codigo.Length > 0 && outros.Any(t => t.Codigo == dados.Codigo))
            erros.Add($"Já existe um território de código {dados.Codigo} neste mapa (ativo ou encerrado).");
        if (dados.Nome.Length > 0 && outros.Any(t => t.Ativo && t.PaiId == dados.PaiId && TextoBusca.Normalizar(t.Nome) == TextoBusca.Normalizar(dados.Nome)))
            erros.Add(dados.PaiId is null
                ? $"Já existe o território \"{dados.Nome}\" no primeiro nível deste mapa."
                : $"Já existe o território \"{dados.Nome}\" abaixo do mesmo território.");

        if (!tipos.TryGetValue(dados.TipoId, out var tipo)) erros.Add("Escolha o tipo do território.");
        else if (!tipo.Ativo && anterior?.TipoId != dados.TipoId) erros.Add($"O tipo \"{tipo.Nome}\" está desativado e não pode ser escolhido.");

        var usoNaSubarvore = anterior is not null && UsoNaSubarvore(doMapa, dados.Id, comUso);
        if (anterior is not null && comUso.Contains(dados.Id) && anterior.Codigo != dados.Codigo)
            erros.Add("O código não muda depois que o território tem regras ou atribuições (documentos e integrações dependem dele).");

        var mudouPai = anterior is null || anterior.PaiId != dados.PaiId;
        if (mudouPai) erros.AddRange(ValidarPai(dados, doMapa, anterior is null));
        if (anterior is not null && anterior.PaiId != dados.PaiId && usoNaSubarvore)
            erros.Add($"Mover \"{dados.Nome}\" {SoPorOperacao}: mudar a posição muda o desempate por especificidade.");

        erros.AddRange(ValidarInicio(dados, anterior, doMapa, usoNaSubarvore, hoje));
        return erros.Distinct().ToList();
    }

    /// <summary>A mesma conferência de pai da ficha, para a mudança "Mover território" de uma operação (T18; Fase 2b-1b).</summary>
    public static IEnumerable<string> ValidarPaiParaOperacao(Territorio dados, IReadOnlyCollection<Territorio> doMapa) =>
        ValidarPai(dados, doMapa, novo: false);

    /// <summary>O pai existe no mesmo mapa, está ativo, não é o próprio nem um de baixo (sem ciclo) e não passa do limite de níveis.</summary>
    private static IEnumerable<string> ValidarPai(Territorio dados, IReadOnlyCollection<Territorio> doMapa, bool novo)
    {
        if (dados.PaiId is not { } paiId) yield break;
        if (paiId == dados.Id)
        {
            yield return "O território não pode ficar abaixo dele mesmo.";
            yield break;
        }
        var porId = doMapa.Where(t => t.Id != dados.Id).ToDictionary(t => t.Id);
        if (!porId.TryGetValue(paiId, out var pai) || pai.MapaId != dados.MapaId)
        {
            yield return "O território acima escolhido não existe neste mapa.";
            yield break;
        }
        if (!pai.Ativo) yield return $"O território \"{pai.Nome}\" está encerrado e não pode receber territórios abaixo dele.";
        if (!novo && Descendentes(doMapa, dados.Id).Contains(paiId))
        {
            yield return $"\"{pai.Nome}\" está abaixo de \"{dados.Nome}\": colocá-lo acima formaria um ciclo.";
            yield break;
        }
        var nivelPai = Nivel(porId, pai);
        var altura = novo ? 0 : Altura(doMapa, dados.Id);
        if (nivelPai + 1 + altura > ProfundidadeMaxima)
            yield return $"A árvore passaria de {ProfundidadeMaxima} níveis abaixo de \"{pai.Nome}\".";
    }

    /// <summary>
    /// "Existe desde": não no futuro (planejar estrutura futura é da operação territorial), não antes do território acima
    /// existir, não depois dos que estão abaixo. Com uso, não muda.
    /// </summary>
    private static IEnumerable<string> ValidarInicio(Territorio dados, Territorio? anterior, IReadOnlyCollection<Territorio> doMapa,
                                                     bool usoNaSubarvore, DateOnly hoje)
    {
        if (InicioDe(dados) is not { } inicio) yield break;
        if (inicio > hoje) yield return "\"Existe desde\" não pode ser uma data futura.";
        if (anterior is not null && usoNaSubarvore && InicioDe(anterior) != inicio)
            yield return $"\"Existe desde\" muda {SoPorOperacao}.";

        var porId = doMapa.ToDictionary(t => t.Id);
        if (dados.PaiId is { } paiId && porId.TryGetValue(paiId, out var pai) && InicioDe(pai) is { } inicioPai && inicio < inicioPai)
            yield return $"\"Existe desde\" ({Data(inicio)}) é anterior ao território acima, \"{pai.Nome}\" (desde {Data(inicioPai)}).";
        if (anterior is not null)
            foreach (var filho in doMapa.Where(t => t.PaiId == dados.Id && t.Id != dados.Id))
                if (InicioDe(filho) is { } inicioFilho && inicioFilho < inicio)
                {
                    yield return $"\"{filho.Nome}\", abaixo deste território, existe desde {Data(inicioFilho)}: \"Existe desde\" não pode ser posterior.";
                    break;
                }
    }

    /// <summary>Encerrar sem operação: só sem uso e sem território ativo abaixo.</summary>
    public static List<string> ValidarEncerramento(Territorio t, IReadOnlyCollection<Territorio> doMapa, MapaTerritorial? mapa, IReadOnlySet<Guid> comUso)
    {
        var erros = new List<string>();
        if (!t.Ativo) erros.Add($"O território \"{t.Nome}\" já está encerrado.");
        if (mapa is { Ativo: false }) erros.Add($"O mapa \"{mapa.Nome}\" está desativado: reative-o para alterar a árvore.");
        var ativosAbaixo = doMapa.Count(x => x.PaiId == t.Id && x.Id != t.Id && x.Ativo);
        if (ativosAbaixo > 0)
            erros.Add($"\"{t.Nome}\" tem {ativosAbaixo} território(s) ativo(s) logo abaixo: mova-os ou encerre-os antes.");
        if (UsoNaSubarvore(doMapa, t.Id, comUso)) erros.Add($"Encerrar \"{t.Nome}\" {SoPorOperacao}.");
        return erros;
    }

    /// <summary>
    /// Encerra hoje (hoje é o último dia): a posição aberta termina hoje; responsáveis que já começaram terminam hoje (no
    /// máximo) e os que ainda não começaram são anulados. Nada é apagado.
    /// </summary>
    public static void Encerrar(Territorio t, DateOnly hoje)
    {
        t.Situacao = SituacaoTerritorio.Encerrado;
        t.FimEm = hoje;
        foreach (var p in t.Posicoes.Where(p => p.Ativo && (p.FimEm is null || p.FimEm > hoje)))
            if (p.InicioEm <= hoje) p.FimEm = hoje;
            else p.Ativo = false;
        foreach (var r in t.Responsaveis.Where(r => r.Ativo))
            if (r.InicioEm > hoje) r.Ativo = false;
            else if (r.FimEm is null || r.FimEm > hoje) r.FimEm = hoje;
        t.RegistrarEvento($"Território '{t.Nome}' encerrado (último dia {Data(hoje)}).");
    }

    /// <summary>Reativar sem operação: só sem uso, com o território acima ativo e sem outro irmão ativo de mesmo nome.</summary>
    public static List<string> ValidarReativacao(Territorio t, IReadOnlyCollection<Territorio> doMapa, MapaTerritorial? mapa, IReadOnlySet<Guid> comUso)
    {
        var erros = new List<string>();
        if (t.Ativo) erros.Add($"O território \"{t.Nome}\" já está ativo.");
        if (mapa is { Ativo: false }) erros.Add($"O mapa \"{mapa.Nome}\" está desativado: reative-o para alterar a árvore.");
        if (t.PaiId is { } paiId && doMapa.FirstOrDefault(x => x.Id == paiId) is { Ativo: false } pai)
            erros.Add($"O território acima, \"{pai.Nome}\", está encerrado: reative-o primeiro.");
        if (doMapa.Any(x => x.Id != t.Id && x.Ativo && x.PaiId == t.PaiId && TextoBusca.Normalizar(x.Nome) == TextoBusca.Normalizar(t.Nome)))
            erros.Add($"Já existe outro território ativo chamado \"{t.Nome}\" no mesmo lugar da árvore: renomeie um deles antes.");
        if (UsoNaSubarvore(doMapa, t.Id, comUso)) erros.Add($"Reativar \"{t.Nome}\" {SoPorOperacao}.");
        if (!t.Posicoes.Any(p => p.Ativo))
            erros.Add($"\"{t.Nome}\" não tem posição válida na árvore para reabrir: crie um território novo no lugar dele.");
        return erros;
    }

    /// <summary>Reativa sem uso: a última posição volta a ficar aberta (correção; nada dependia do encerramento).</summary>
    public static void Reativar(Territorio t)
    {
        t.Situacao = SituacaoTerritorio.Ativo;
        t.FimEm = null;
        if (t.Posicoes.Where(p => p.Ativo).OrderByDescending(p => p.InicioEm).FirstOrDefault() is { } ultima) ultima.FimEm = null;
        t.RegistrarEvento($"Território '{t.Nome}' reativado.");
    }
}
