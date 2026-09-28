using System.Globalization;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Comercial;

/// <summary>Valores comerciais que valem para o cliente numa data (exceção vigente → perfil → conta).</summary>
public sealed record ValoresComerciais(
    decimal? LimiteCredito, decimal? DescontoMaximo, int? DiasMaximoAtraso, Guid? CondicaoPagamentoId, bool ExigeAprovacaoAcimaLimite);

/// <summary>Pessoa ativa que pode ser escolhida na carteira, com as classificações (papéis do cadastro) ativas dela.</summary>
public sealed record PessoaElegivel(string Nome, IReadOnlySet<Guid> Classificacoes);

/// <summary>Cadastros usados na conferência dos dados comerciais do cliente (com os desativados).</summary>
/// <param name="Pessoas">As pessoas escolhidas na carteira que estão ativas (as que faltam não existem ou estão inativas).</param>
/// <param name="Classificacoes">Nomes das classificações (papéis do cadastro), para as mensagens.</param>
public sealed record ComercialParaConferir(
    IReadOnlyDictionary<Guid, PerfilComercial> Perfis,
    IReadOnlyDictionary<Guid, CondicaoPagamento> Condicoes,
    IReadOnlyDictionary<Guid, TipoCarteira> TiposCarteira,
    IReadOnlyDictionary<Guid, PessoaElegivel> Pessoas,
    IReadOnlyDictionary<Guid, string> Classificacoes);

/// <summary>
/// Resultado de <see cref="RegrasComercial.PlanejarSubstituicao"/>: os vínculos vigentes que um vínculo novo substitui e
/// os que impedem a substituição (começam no mesmo dia ou depois dele).
/// </summary>
public sealed record PlanoSubstituicao(IReadOnlyList<CarteiraCliente> Encerrar, IReadOnlyList<CarteiraCliente> Impedem)
{
    public bool TemConflito => Encerrar.Count > 0 || Impedem.Count > 0;
    public bool Impedida => Impedem.Count > 0;
}

/// <summary>Um papel comercial inicial, com a sua política.</summary>
public sealed record PapelComercialInicial(Guid Id, string Nome, int Ordem, bool ResponsavelDaConta, int? LimitePorVez,
                                           TipoCreditoComercial TipoCredito, bool ContaParaMetas);

/// <summary>
/// Os papéis comerciais (tipos de carteira) iniciais, com Ids fixos; o usuário pode criar outros. A política inicial
/// mantém o comportamento de antes do Motor Comercial: só o Vendedor é um por vez e recebe o crédito; os demais ficam sem
/// limite e sem crédito; todos contam para metas (como antes) até a empresa desmarcar os que não devem contar.
/// </summary>
public static class TiposCarteiraIniciais
{
    public static IReadOnlyList<PapelComercialInicial> Todos { get; } =
    [
        new(new Guid("7a9e1c04-0000-0000-0000-000000000001"), "Vendedor", 1, true, 1, TipoCreditoComercial.Receita, true),
        new(new Guid("7a9e1c04-0000-0000-0000-000000000002"), "Representante", 2, false, null, TipoCreditoComercial.Nenhum, true),
        new(new Guid("7a9e1c04-0000-0000-0000-000000000003"), "Televendas", 3, false, null, TipoCreditoComercial.Nenhum, true),
        new(new Guid("7a9e1c04-0000-0000-0000-000000000004"), "Supervisor", 4, false, null, TipoCreditoComercial.Nenhum, true)
    ];
}

/// <summary>Crédito de um vínculo numa data (<see cref="RegrasComercial.CreditosEmData"/>).</summary>
/// <param name="Percentual">Percentual efetivo; nulo quando a divisão de receita não está definida (falta o % de algum vínculo).</param>
public sealed record CreditoDoVinculo(CarteiraCliente Vinculo, TipoCreditoComercial Tipo, decimal? Percentual);

/// <summary>Regras comerciais: condições de pagamento, perfis, exceções com vigência e carteira de clientes. Não acessa banco.</summary>
public static class RegrasComercial
{
    /// <summary>Antecedência padrão (dias) com que o fim de um vínculo da carteira é destacado.</summary>
    public const int DiasAvisoFimPadrao = 30;

    // ---------------------------------------------------------------- Condição de pagamento

    /// <summary>"30, 60 ,90" / "30 60 90" / "30/60/90" → "30/60/90". Nulo se algum pedaço não for número.</summary>
    public static string? NormalizarParcelas(string? texto)
    {
        var partes = (texto ?? string.Empty).Split(['/', ',', ';', ' ', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0) return null;
        var dias = new List<int>();
        foreach (var p in partes)
        {
            if (!int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var d)) return null;
            dias.Add(d);
        }
        return string.Join('/', dias);
    }

    public static IReadOnlyList<int> Dias(string parcelas) =>
        parcelas.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToList();

    /// <summary>Prazo médio em dias (média simples das parcelas).</summary>
    public static decimal PrazoMedio(string parcelas)
    {
        var dias = Dias(parcelas);
        return dias.Count == 0 ? 0 : Math.Round((decimal)dias.Sum() / dias.Count, 1);
    }

    public static List<string> Validar(CondicaoPagamento c, string? parcelasDigitadas)
    {
        var erros = new List<string>();
        if (c.Nome.Length == 0) erros.Add("Informe o nome da condição.");
        else if (c.Nome.Length > CondicaoPagamento.TamanhoMaximoNome)
            erros.Add($"O nome pode ter no máximo {CondicaoPagamento.TamanhoMaximoNome} caracteres.");

        if (NormalizarParcelas(parcelasDigitadas) is not { } parcelas)
            erros.Add("Parcelas: informe os dias separados por \"/\" (ex.: 0/30/60; 0 = à vista).");
        else
        {
            var dias = Dias(parcelas);
            if (dias.Count > CondicaoPagamento.MaximoParcelas) erros.Add($"Use no máximo {CondicaoPagamento.MaximoParcelas} parcelas.");
            if (dias.Any(d => d > CondicaoPagamento.MaximoDias)) erros.Add($"Parcelas de até {CondicaoPagamento.MaximoDias} dias.");
            if (dias.Zip(dias.Skip(1)).Any(p => p.Second <= p.First)) erros.Add("Os dias das parcelas precisam ser crescentes (ex.: 30/60/90).");
        }
        if (c.AcrescimoPercentual is < -100 or > 100) erros.Add("Acréscimo/desconto entre -100% e 100%.");
        return erros;
    }

    // ---------------------------------------------------------------- Perfil e exceções

    public static List<string> ValidarValores(string rotulo, decimal? limite, decimal? desconto, int? dias)
    {
        var erros = new List<string>();
        if (limite < 0) erros.Add($"{rotulo}: o limite de crédito não pode ser negativo.");
        if (desconto is < 0 or > 100) erros.Add($"{rotulo}: o desconto máximo deve ficar entre 0% e 100%.");
        if (dias < 0) erros.Add($"{rotulo}: dias máximos de atraso não podem ser negativos.");
        return erros;
    }

    /// <summary>Valores em vigor: para cada campo, a exceção vigente (se definir), depois o perfil, depois a conta.</summary>
    public static ValoresComerciais Efetivos(ContaCliente? conta, PerfilComercial? perfil, IEnumerable<ExcecaoComercial> excecoes, DateOnly data)
    {
        var excecao = excecoes.Where(e => e.Vigente(data) && e.EmpresaId == conta?.EmpresaId)
            .OrderByDescending(e => e.InicioEm).FirstOrDefault();
        return new ValoresComerciais(
            excecao?.LimiteCredito ?? perfil?.LimiteCredito ?? conta?.LimiteCredito,
            excecao?.DescontoMaximo ?? perfil?.DescontoMaximo ?? conta?.DescontoMaximo,
            excecao?.DiasMaximoAtraso ?? perfil?.DiasMaximoAtraso ?? conta?.DiasMaximoAtraso,
            excecao?.CondicaoPagamentoId ?? perfil?.CondicaoPagamentoId ?? conta?.CondicaoPagamentoId,
            excecao?.ExigeAprovacaoAcimaLimite ?? perfil?.ExigeAprovacaoAcimaLimite ?? conta?.ExigeAprovacaoAcimaLimite ?? true);
    }

    public static void Normalizar(Pessoa p)
    {
        foreach (var f in p.ContasFornecedor)
            if (f.CondicaoPagamentoId == Guid.Empty) f.CondicaoPagamentoId = null;
        foreach (var e in p.ExcecoesComerciais) e.Motivo = Texto(e.Motivo);
        foreach (var c in p.Carteira) c.Observacao = Texto(c.Observacao);
    }

    private static string? Texto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static bool Sobrepoem(DateOnly inicioA, DateOnly? fimA, DateOnly inicioB, DateOnly? fimB) =>
        inicioA <= (fimB ?? DateOnly.MaxValue) && inicioB <= (fimA ?? DateOnly.MaxValue);

    /// <summary>
    /// Datas, valores e sobreposições das exceções e da carteira (sem consultar cadastros). Os conflitos da carteira só
    /// contam no que esta gravação acrescenta a algum vínculo (<see cref="Crescimentos"/>; sem <paramref name="anterior"/>,
    /// tudo é novo): uma política mais nova do papel não trava a ficha por sobreposições antigas.
    /// </summary>
    public static List<string> Validar(Pessoa p, IReadOnlyDictionary<Guid, TipoCarteira> tipos, Pessoa? anterior = null)
    {
        var erros = new List<string>();
        for (var i = 0; i < p.ExcecoesComerciais.Count; i++)
        {
            var e = p.ExcecoesComerciais[i];
            var rotulo = $"Exceção comercial {i + 1}";
            if (e.InicioEm == default) erros.Add($"{rotulo}: informe o início.");
            if (e.FimEm is { } fim && fim < e.InicioEm) erros.Add($"{rotulo}: o fim é anterior ao início.");
            if (e.LimiteCredito is null && e.DescontoMaximo is null && e.DiasMaximoAtraso is null &&
                e.CondicaoPagamentoId is null && e.ExigeAprovacaoAcimaLimite is null)
                erros.Add($"{rotulo}: informe ao menos um valor (o que não for informado continua valendo do perfil).");
            if (e.Motivo is { Length: > 250 }) erros.Add($"{rotulo}: motivo de no máximo 250 caracteres.");
            erros.AddRange(ValidarValores(rotulo, e.LimiteCredito, e.DescontoMaximo, e.DiasMaximoAtraso));
        }
        foreach (var grupo in p.ExcecoesComerciais.GroupBy(e => e.EmpresaId))
        {
            var lista = grupo.OrderBy(e => e.InicioEm).ToList();
            for (var j = 1; j < lista.Count; j++)
                if (Sobrepoem(lista[j - 1].InicioEm, lista[j - 1].FimEm, lista[j].InicioEm, lista[j].FimEm))
                    erros.Add($"Exceções comerciais com períodos sobrepostos ({Data(lista[j].InicioEm)}): encerre a anterior antes.");
        }

        var ativos = p.Carteira.Where(c => c.Ativo).ToList();
        for (var i = 0; i < p.Carteira.Count; i++)
        {
            var c = p.Carteira[i];
            var rotulo = $"Carteira {i + 1}";
            if (c.VendedorId == Guid.Empty) erros.Add($"{rotulo}: escolha o vendedor.");
            if (c.VendedorId == p.Id) erros.Add($"{rotulo}: o cliente não pode ser vendedor de si mesmo.");
            if (c.TipoCarteiraId == Guid.Empty) erros.Add($"{rotulo}: escolha o papel.");
            if (c.InicioEm == default) erros.Add($"{rotulo}: informe o início.");
            if (c.FimEm is { } fim && fim < c.InicioEm) erros.Add($"{rotulo}: o fim é anterior ao início.");
            if (c.Observacao is { Length: > 250 }) erros.Add($"{rotulo}: observação de no máximo 250 caracteres.");
        }

        // Exclusivo: ninguém mais do mesmo papel no período. Um por vez: um só vínculo do papel de cada vez.
        var antes = (anterior?.Carteira ?? []).ToDictionary(c => c.Id);
        bool NoQueCresceu(CarteiraCliente a, CarteiraCliente b) =>
            Crescimentos(a, antes.GetValueOrDefault(a.Id)).Any(f => Sobrepoem(f.De, f.Ate, b.InicioEm, b.FimEm)) ||
            Crescimentos(b, antes.GetValueOrDefault(b.Id)).Any(f => Sobrepoem(f.De, f.Ate, a.InicioEm, a.FimEm));
        foreach (var a in ativos)
            foreach (var b in ativos.Where(b => Conflitam(a, b, tipos) && NoQueCresceu(a, b)))
            {
                var umPorVez = tipos.TryGetValue(a.TipoCarteiraId, out var tipo) && tipo.UmPorVez;
                erros.Add(umPorVez
                    ? $"Carteira: só um \"{tipo!.Nome}\" por vez; os períodos se sobrepõem em {Data(Maior(a.InicioEm, b.InicioEm))}."
                    : $"Carteira: o vínculo exclusivo de {Data((a.Exclusivo ? a : b).InicioEm)} se sobrepõe a outro do mesmo papel.");
            }
        return erros.Distinct().ToList();
    }

    // ---------------------------------------------------------------- Carteira: conflito e substituição

    /// <summary>
    /// Dois vínculos que não podem valer ao mesmo tempo (a regra única da carteira, usada na validação, na substituição
    /// da ficha e no gatilho do banco, SqlMigracaoCarteira): ambos ativos, mesmo papel, mesma empresa (nula = todas), períodos
    /// sobrepostos e o papel aceita um por vez ou algum dos dois é exclusivo. Papéis ou empresas diferentes nunca conflitam.
    /// Papéis com limite maior que 1 são conferidos pela contagem (<see cref="ValidarCarteira"/>).
    /// </summary>
    public static bool Conflitam(CarteiraCliente a, CarteiraCliente b, IReadOnlyDictionary<Guid, TipoCarteira> tipos) =>
        !ReferenceEquals(a, b) && a.Ativo && b.Ativo &&
        a.TipoCarteiraId == b.TipoCarteiraId && a.EmpresaId == b.EmpresaId &&
        Sobrepoem(a.InicioEm, a.FimEm, b.InicioEm, b.FimEm) &&
        (a.Exclusivo || b.Exclusivo || (tipos.TryGetValue(a.TipoCarteiraId, out var tipo) && tipo.UmPorVez));

    /// <summary>
    /// O que acontece se <paramref name="novo"/> entrar na carteira da pessoa: os vínculos que podem ser encerrados no dia
    /// anterior ao início dele (substituição) e os que impedem (começam no mesmo dia do novo ou depois: encerrá-los
    /// apagaria o período deles, e o histórico não é alterado retroativamente).
    /// </summary>
    public static PlanoSubstituicao PlanejarSubstituicao(IEnumerable<CarteiraCliente> carteira, CarteiraCliente novo,
                                                         IReadOnlyDictionary<Guid, TipoCarteira> tipos)
    {
        var conflitos = carteira.Where(c => Conflitam(c, novo, tipos)).ToList();
        return new PlanoSubstituicao(
            [.. conflitos.Where(c => c.InicioEm < novo.InicioEm)],
            [.. conflitos.Where(c => c.InicioEm >= novo.InicioEm)]);
    }

    /// <summary>
    /// Encerra os vínculos do plano no dia anterior ao início do novo (o fim conta como dia de vigência: assim não há
    /// sobreposição nem dia sem responsável). Nada é apagado nem desativado: o anterior fica com o período em que valeu.
    /// Só aplica se nada impede (quem chama confere <see cref="PlanoSubstituicao.Impedida"/> antes).
    /// </summary>
    public static void Substituir(PlanoSubstituicao plano, CarteiraCliente novo)
    {
        if (plano.Impedida) throw new InvalidOperationException("Substituição impedida: há vínculo que começa no mesmo dia ou depois do novo.");
        var fim = novo.InicioEm.AddDays(-1);
        foreach (var anterior in plano.Encerrar) anterior.FimEm = fim;
    }

    /// <summary>
    /// As substituições feitas nesta gravação: um vínculo já gravado que passou a terminar na véspera do início de um
    /// vínculo novo que conflitaria com ele como estava gravado (papel de um por vez ou exclusivo). Encerrar um e incluir
    /// outro de um papel que aceita vários não é substituição.
    /// </summary>
    public static IEnumerable<(CarteiraCliente Encerrado, CarteiraCliente Sucessor)> ParesSubstituidos(
        IEnumerable<CarteiraCliente> anteriores, IEnumerable<CarteiraCliente> atuais, IReadOnlyDictionary<Guid, TipoCarteira> tipos)
    {
        var antes = anteriores.ToDictionary(c => c.Id);
        var lista = atuais.ToList();
        var novos = lista.Where(c => c.Ativo && !antes.ContainsKey(c.Id)).ToList();
        foreach (var encerrado in lista)
        {
            if (!antes.TryGetValue(encerrado.Id, out var gravado) || !encerrado.Ativo || encerrado.FimEm is not { } fim) continue;
            if (gravado.FimEm is { } fimAntes && fimAntes <= fim) continue; // não foi encurtado agora
            var sucessor = novos.FirstOrDefault(n => n.InicioEm == fim.AddDays(1) && Conflitam(gravado, n, tipos));
            if (sucessor is not null) yield return (encerrado, sucessor);
        }
    }

    /// <summary>
    /// Frases do histórico para as substituições desta gravação ("João (Vendedor) encerrado em 14/03/2026 e substituído
    /// por Maria a partir de 15/03/2026"). Roda na gravação da pessoa: vale para a ficha e para o que grava pela pessoa.
    /// </summary>
    public static IEnumerable<string> Substituicoes(IEnumerable<CarteiraCliente> anteriores, IEnumerable<CarteiraCliente> atuais,
                                                    IReadOnlyDictionary<Guid, TipoCarteira> tipos, Func<Guid, string> nomeVendedor)
    {
        foreach (var (encerrado, sucessor) in ParesSubstituidos(anteriores, atuais, tipos))
        {
            var tipo = tipos.TryGetValue(encerrado.TipoCarteiraId, out var t) ? t.Nome : "carteira";
            yield return $"Carteira: {nomeVendedor(encerrado.VendedorId)} ({tipo}) encerrado em {Data(encerrado.FimEm!.Value)} e substituído por " +
                         $"{nomeVendedor(sucessor.VendedorId)} a partir de {Data(sucessor.InicioEm)}.";
        }
    }

    /// <summary>
    /// A origem do vínculo é do servidor (a ficha não a envia): o gravado mantém a sua; o novo recebe
    /// <paramref name="origemDosNovos"/> ("Manual" na ficha), ou "Substituição" quando entra no lugar de um vigente
    /// encerrado na véspera pela confirmação da ficha.
    /// </summary>
    public static void DefinirOrigens(Pessoa p, Pessoa? anterior, IReadOnlyDictionary<Guid, TipoCarteira> tipos,
                                      OrigemVinculoCarteira origemDosNovos = OrigemVinculoCarteira.Manual)
    {
        var antes = (anterior?.Carteira ?? []).ToDictionary(c => c.Id);
        foreach (var c in p.Carteira)
            c.Origem = antes.TryGetValue(c.Id, out var gravado) ? gravado.Origem : origemDosNovos;
        if (origemDosNovos != OrigemVinculoCarteira.Manual) return;
        foreach (var (_, sucessor) in ParesSubstituidos(anterior?.Carteira ?? [], p.Carteira, tipos))
            sucessor.Origem = OrigemVinculoCarteira.Substituicao;
    }

    /// <summary>
    /// O histórico da carteira não é reescrito: num vínculo gravado que já começou (início até hoje), papel, pessoa,
    /// empresa, início, exclusivo e crédito não mudam. Muda só o fim (encerrar, inclusive com data passada, ou reabrir), a
    /// observação e o "ativo" (lançado por engano). Mudar a partir de uma data é encerrar e incluir outro ("Trocar" na
    /// ficha). Vínculo que ainda não começou (planejado) pode ser corrigido à vontade.
    /// </summary>
    public static List<string> ValidarHistorico(Pessoa p, Pessoa? anterior, IReadOnlyDictionary<Guid, TipoCarteira> tipos, DateOnly hoje)
    {
        var erros = new List<string>();
        var antes = (anterior?.Carteira ?? []).ToDictionary(c => c.Id);
        for (var i = 0; i < p.Carteira.Count; i++)
        {
            var c = p.Carteira[i];
            if (!antes.TryGetValue(c.Id, out var g) || g.InicioEm > hoje) continue;
            var mudou = new List<string>();
            if (g.TipoCarteiraId != c.TipoCarteiraId) mudou.Add("papel");
            if (g.VendedorId != c.VendedorId) mudou.Add("pessoa");
            if (g.EmpresaId != c.EmpresaId) mudou.Add("empresa");
            if (g.InicioEm != c.InicioEm) mudou.Add("início");
            if (g.Exclusivo != c.Exclusivo) mudou.Add("exclusivo");
            if (g.PercentualCredito != c.PercentualCredito) mudou.Add("crédito");
            if (mudou.Count == 0) continue;
            var papel = tipos.TryGetValue(g.TipoCarteiraId, out var t) ? t.Nome : "carteira";
            erros.Add($"Carteira {i + 1} ({papel}, desde {Data(g.InicioEm)}): {string.Join(", ", mudou)} não muda(m) depois que o vínculo " +
                      "começou, para não reescrever o histórico. Use \"Trocar\" para mudar a partir de uma data (encerra este e abre outro), " +
                      "ou desative se foi lançado por engano.");
        }
        return erros;
    }

    // ---------------------------------------------------------------- Carteira: política dos papéis e crédito

    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private static bool Cobre(CarteiraCliente c, DateOnly data) => c.InicioEm <= data && (c.FimEm is null || c.FimEm >= data);

    private static TipoCreditoComercial TipoDe(CarteiraCliente c, IReadOnlyDictionary<Guid, TipoCarteira> tipos) =>
        tipos.TryGetValue(c.TipoCarteiraId, out var t) ? t.TipoCredito : TipoCreditoComercial.Nenhum;

    /// <summary>Percentual efetivo do vínculo: o dele ou, sem ele, o padrão do papel (nulo se nenhum dos dois).</summary>
    public static decimal? PercentualEfetivo(CarteiraCliente c, IReadOnlyDictionary<Guid, TipoCarteira> tipos) =>
        c.PercentualCredito ?? (tipos.TryGetValue(c.TipoCarteiraId, out var t) ? t.PercentualPadrao : null);

    /// <summary>Datas em que o conjunto de vínculos vigentes muda: cada início e o dia seguinte a cada fim.</summary>
    private static IEnumerable<DateOnly> Mudancas(IEnumerable<CarteiraCliente> vinculos) =>
        vinculos.Select(c => c.InicioEm)
            .Concat(vinculos.Where(c => c.FimEm is { } f && f < DateOnly.MaxValue).Select(c => c.FimEm!.Value.AddDays(1)))
            .Distinct().Order();

    /// <summary>
    /// O que esta gravação acrescenta ao período em que o vínculo vale com as mesmas regras (mesmo papel e empresa, ativo,
    /// sem passar a exclusivo): o período todo se é novo, foi reativado ou mudou de papel, empresa ou para exclusivo; senão
    /// só os trechos que cresceram (início antecipado, fim adiado). Encurtar não acrescenta nada. A mesma ideia está no
    /// gatilho do banco (SqlMigracaoCarteira.CriarProtecaoPorLimite).
    /// </summary>
    public static List<(DateOnly De, DateOnly? Ate)> Crescimentos(CarteiraCliente atual, CarteiraCliente? gravado)
    {
        if (!atual.Ativo || atual.InicioEm == default || atual.FimEm < atual.InicioEm) return [];
        if (gravado is null || !gravado.Ativo || gravado.TipoCarteiraId != atual.TipoCarteiraId || gravado.EmpresaId != atual.EmpresaId ||
            (atual.Exclusivo && !gravado.Exclusivo))
            return [(atual.InicioEm, atual.FimEm)];

        var trechos = new List<(DateOnly De, DateOnly? Ate)>();
        if (atual.InicioEm < gravado.InicioEm)
        {
            var ate = gravado.InicioEm.AddDays(-1);
            trechos.Add((atual.InicioEm, atual.FimEm is { } f && f < ate ? f : ate));
        }
        if (gravado.FimEm is { } fimAntes && fimAntes < DateOnly.MaxValue && (atual.FimEm is null || atual.FimEm > fimAntes))
        {
            var de = fimAntes.AddDays(1);
            trechos.Add((atual.InicioEm > de ? atual.InicioEm : de, atual.FimEm));
        }
        return trechos;
    }

    /// <summary>
    /// Política dos papéis (Motor Comercial, Fase 1): o percentual de crédito de cada vínculo, o limite de vínculos
    /// simultâneos acima de 1 (o de 1 é o conflito par a par de <see cref="Validar"/>) e a divisão do crédito de receita
    /// (com dois ou mais vínculos vigentes, cada um com o seu % e a soma em 100%, por empresa).
    /// Confere só o que esta gravação afeta: o limite, nos trechos acrescentados (<see cref="Crescimentos"/>); a divisão do
    /// crédito, no período antigo e no novo de cada vínculo incluído ou alterado (e no dia seguinte ao fim), porque tirar um
    /// vínculo também muda a soma dos que ficam. Mudar a política de um papel não trava a ficha de quem não mexe na
    /// carteira, e o histórico não é revalidado com regra nova.
    /// </summary>
    public static List<string> ValidarCarteira(Pessoa p, Pessoa? anterior, IReadOnlyDictionary<Guid, TipoCarteira> tipos)
    {
        var erros = new List<string>();
        var antes = (anterior?.Carteira ?? []).ToDictionary(c => c.Id);
        bool Mexido(CarteiraCliente c) => !antes.TryGetValue(c.Id, out var g) || g.TipoCarteiraId != c.TipoCarteiraId ||
            g.EmpresaId != c.EmpresaId || g.InicioEm != c.InicioEm || g.FimEm != c.FimEm || g.Ativo != c.Ativo ||
            g.Exclusivo != c.Exclusivo || g.PercentualCredito != c.PercentualCredito;

        for (var i = 0; i < p.Carteira.Count; i++)
        {
            var c = p.Carteira[i];
            if (c.PercentualCredito is not { } pct) continue;
            var rotulo = $"Carteira {i + 1}";
            if (pct is < 0 or > 100) erros.Add($"{rotulo}: o crédito deve ficar entre 0% e 100%.");
            else if (decimal.Round(pct, 2) != pct) erros.Add($"{rotulo}: crédito com no máximo 2 casas decimais.");
            if (Mexido(c) && tipos.TryGetValue(c.TipoCarteiraId, out var t) && t.TipoCredito == TipoCreditoComercial.Nenhum)
                erros.Add($"{rotulo}: o papel \"{t.Nome}\" não recebe crédito da venda; deixe o crédito vazio.");
        }

        var faixas = p.Carteira.Where(Mexido)
            .SelectMany(c => antes.TryGetValue(c.Id, out var g)
                ? new[] { (c.InicioEm, c.FimEm), (g.InicioEm, g.FimEm) }
                : new[] { (c.InicioEm, c.FimEm) })
            .Where(f => f.InicioEm != default).ToList();
        bool Afetada(DateOnly d) => faixas.Any(f => f.InicioEm <= d && (f.FimEm is null || f.FimEm >= d.AddDays(-1)));

        var validos = p.Carteira.Where(c => c.Ativo && c.InicioEm != default && (c.FimEm is null || c.FimEm >= c.InicioEm)).ToList();

        foreach (var grupo in validos.GroupBy(c => (c.TipoCarteiraId, c.EmpresaId)))
        {
            if (!tipos.TryGetValue(grupo.Key.TipoCarteiraId, out var tipo) || tipo.LimitePorVez is not ({ } limite and > 1)) continue;
            // Só no que esta gravação acrescenta; em cada trecho, o máximo simultâneo está no começo dele ou no início de
            // algum vínculo dentro dele.
            var datas = grupo.SelectMany(c => Crescimentos(c, antes.GetValueOrDefault(c.Id)))
                .SelectMany(f => grupo.Select(c => c.InicioEm).Where(d => d > f.De && (f.Ate is null || d <= f.Ate)).Append(f.De))
                .Distinct().Order();
            foreach (var data in datas)
            {
                var quantos = grupo.Count(c => Cobre(c, data));
                if (quantos <= limite) continue;
                erros.Add($"Carteira: no máximo {limite} \"{tipo.Nome}\" ao mesmo tempo; em {Data(data)} seriam {quantos}. " +
                          "Encerre um vínculo antes de incluir outro.");
                break;
            }
        }

        foreach (var grupo in validos.Where(c => TipoDe(c, tipos) == TipoCreditoComercial.Receita).GroupBy(c => c.EmpresaId))
        {
            foreach (var data in Mudancas(grupo).Where(Afetada))
            {
                var vigentes = grupo.Where(c => Cobre(c, data)).ToList();
                if (vigentes.Count < 2) continue; // um só fica com 100%
                var percentuais = vigentes.Select(c => PercentualEfetivo(c, tipos)).ToList();
                if (percentuais.Any(x => x is null))
                {
                    erros.Add($"Carteira: há mais de um vínculo com crédito de receita ao mesmo tempo (em {Data(data)}); " +
                              "informe o crédito (%) de cada um.");
                    break;
                }
                var soma = percentuais.Sum(x => x!.Value);
                if (soma == 100) continue;
                erros.Add($"Carteira: o crédito de receita soma {soma.ToString("0.##", Brasil)}% em {Data(data)}; precisa somar 100%.");
                break;
            }
        }
        return erros.Distinct().ToList();
    }

    /// <summary>
    /// Quem recebe crédito de uma venda do cliente nesta data e empresa (consulta temporal, base do motor de crédito).
    /// Para cada tipo de crédito, os vínculos da própria empresa têm preferência; sem eles, valem os de "todas" (empresa
    /// nula). Receita: um só vigente fica com 100%; com vários, cada um com o seu percentual (nulo em todos se faltar
    /// algum: divisão indefinida). Sobreposição: o % do vínculo ou do papel, senão 100%. Papéis sem crédito não entram.
    /// </summary>
    public static List<CreditoDoVinculo> CreditosEmData(IEnumerable<CarteiraCliente> carteira, IReadOnlyDictionary<Guid, TipoCarteira> tipos,
                                                        Guid? empresaId, DateOnly data)
    {
        var vigentes = carteira.Where(c => c.Vigente(data) && (c.EmpresaId == empresaId || c.EmpresaId is null)).ToList();
        List<CarteiraCliente> DoTipo(TipoCreditoComercial tipo)
        {
            var doTipo = vigentes.Where(c => TipoDe(c, tipos) == tipo).ToList();
            var daEmpresa = doTipo.Where(c => c.EmpresaId is not null && c.EmpresaId == empresaId).ToList();
            return daEmpresa.Count > 0 ? daEmpresa : doTipo.Where(c => c.EmpresaId is null).ToList();
        }

        var resultado = new List<CreditoDoVinculo>();
        var receita = DoTipo(TipoCreditoComercial.Receita);
        if (receita.Count == 1)
            resultado.Add(new CreditoDoVinculo(receita[0], TipoCreditoComercial.Receita, 100m));
        else if (receita.Count > 1)
        {
            var percentuais = receita.Select(c => PercentualEfetivo(c, tipos)).ToList();
            var definido = percentuais.All(x => x is not null) && percentuais.Sum(x => x!.Value) == 100;
            resultado.AddRange(receita.Select((c, i) => new CreditoDoVinculo(c, TipoCreditoComercial.Receita, definido ? percentuais[i] : null)));
        }
        resultado.AddRange(DoTipo(TipoCreditoComercial.Sobreposicao)
            .Select(c => new CreditoDoVinculo(c, TipoCreditoComercial.Sobreposicao, PercentualEfetivo(c, tipos) ?? 100m)));
        return resultado;
    }

    /// <summary>
    /// As classificações aceitas pelo papel depois da edição: as já gravadas continuam (ativas se ainda marcadas, senão
    /// desativadas: nada é apagado) e as marcadas agora que não existiam entram novas.
    /// </summary>
    public static List<TipoCarteiraClassificacao> SincronizarClassificacoes(Guid tipoId, IEnumerable<TipoCarteiraClassificacao> gravadas,
                                                                             IEnumerable<Guid> marcadas)
    {
        var marcadasSet = marcadas.Where(id => id != Guid.Empty).ToHashSet();
        var resultado = gravadas.Select(g => new TipoCarteiraClassificacao
        {
            Id = g.Id, TipoCarteiraId = tipoId, PapelId = g.PapelId, Ativo = marcadasSet.Contains(g.PapelId),
            CriadoEm = g.CriadoEm, AtualizadoEm = g.AtualizadoEm
        }).ToList();
        foreach (var id in marcadasSet.Where(id => resultado.All(r => r.PapelId != id)))
            resultado.Add(new TipoCarteiraClassificacao { Id = IdSequencial.Novo(), TipoCarteiraId = tipoId, PapelId = id });
        return resultado;
    }

    /// <summary>Regras do cadastro de um papel comercial (a lista <paramref name="todos"/> inclui os desativados).</summary>
    public static List<string> ValidarPapel(TipoCarteira dados, IReadOnlyCollection<TipoCarteira> todos)
    {
        var erros = new List<string>();
        if (dados.Nome.Length == 0) erros.Add("Informe o nome do papel.");
        else if (dados.Nome.Length > TipoCarteira.TamanhoMaximoNome) erros.Add($"O nome pode ter no máximo {TipoCarteira.TamanhoMaximoNome} caracteres.");
        if (dados.Nome.Length > 0 && todos.Any(t => t.Id != dados.Id && TextoBusca.Normalizar(t.Nome) == TextoBusca.Normalizar(dados.Nome)))
            erros.Add($"Já existe \"{dados.Nome}\" (ativo ou desativado; maiúsculas e acentos não contam).");

        if (dados.LimitePorVez is < 1 or > TipoCarteira.MaximoPorVez)
            erros.Add($"Quantos ao mesmo tempo: de 1 a {TipoCarteira.MaximoPorVez} (vazio = sem limite).");
        if (dados.ResponsavelDaConta)
        {
            if (todos.FirstOrDefault(t => t.ResponsavelDaConta && t.Id != dados.Id) is { } outro)
                erros.Add($"\"{outro.Nome}\" já é o responsável da conta: desmarque-o antes (só um papel define o vendedor padrão).");
            if (dados.LimitePorVez != 1)
                erros.Add("O responsável da conta precisa ser um por vez (\"Quantos ao mesmo tempo\" = 1): ele é o vendedor padrão do cliente.");
        }

        if (!dados.ClassificacoesAceitas.Any())
            erros.Add("Quem pode ser: marque ao menos uma classificação de pessoa (ex.: Vendedor).");

        if (dados.PercentualPadrao is { } pct)
        {
            if (dados.TipoCredito == TipoCreditoComercial.Nenhum)
                erros.Add("Percentual padrão: este papel não recebe crédito da venda; deixe vazio ou escolha o tipo de crédito.");
            else if (pct is < 0 or > 100) erros.Add("Percentual padrão: entre 0% e 100%.");
            else if (decimal.Round(pct, 2) != pct) erros.Add("Percentual padrão: no máximo 2 casas decimais.");
        }
        return erros;
    }

    /// <summary>
    /// Clientes que passariam do limite novo do papel (vínculos ativos de hoje em diante, por cliente e empresa). Usado ao
    /// reduzir o "Quantos ao mesmo tempo": o limite só pode baixar quando ninguém fica acima dele.
    /// </summary>
    public static int ClientesAcimaDoLimite(IEnumerable<CarteiraCliente> vinculosDoPapel, int limite, DateOnly hoje)
    {
        var quantos = 0;
        foreach (var grupo in vinculosDoPapel.Where(c => c.Ativo && (c.FimEm is null || c.FimEm >= hoje)).GroupBy(c => (c.PessoaId, c.EmpresaId)))
        {
            var datas = grupo.Select(c => c.InicioEm < hoje ? hoje : c.InicioEm).Distinct();
            if (datas.Any(d => grupo.Count(c => Cobre(c, d)) > limite)) quantos++;
        }
        return quantos;
    }

    private static DateOnly Maior(DateOnly a, DateOnly b) => a > b ? a : b;

    /// <summary>
    /// Referências: perfil, condições e papéis comerciais existentes (desativado só se já era o gravado ali) e, na carteira,
    /// quem pode ocupar cada papel: pessoa ativa com uma das classificações aceitas pelo papel ("Quem pode ser"). Só no
    /// vínculo novo ou quando a pessoa ou o papel mudam: quem perdeu a classificação continua nos períodos gravados.
    /// </summary>
    public static List<string> ValidarReferencias(Pessoa p, Pessoa? anterior, ComercialParaConferir c)
    {
        var erros = new List<string>();
        var contasAntes = (anterior?.ContasCliente ?? []).ToDictionary(x => x.Id);
        foreach (var conta in p.ContasCliente)
        {
            var antes = contasAntes.GetValueOrDefault(conta.Id);
            Conferir(conta.PerfilComercialId, antes?.PerfilComercialId, c.Perfis, x => x.Ativo, x => x.Nome, "perfil comercial", erros);
            Conferir(conta.CondicaoPagamentoId, antes?.CondicaoPagamentoId, c.Condicoes, x => x.Ativo, x => x.Nome, "condição de pagamento", erros);
        }

        var fornecedorAntes = (anterior?.ContasFornecedor ?? []).ToDictionary(x => x.Id);
        foreach (var conta in p.ContasFornecedor)
            Conferir(conta.CondicaoPagamentoId, fornecedorAntes.GetValueOrDefault(conta.Id)?.CondicaoPagamentoId, c.Condicoes,
                x => x.Ativo, x => x.Nome, "condição de pagamento do fornecedor", erros);

        var excecoesAntes = (anterior?.ExcecoesComerciais ?? []).ToDictionary(x => x.Id);
        foreach (var e in p.ExcecoesComerciais)
            Conferir(e.CondicaoPagamentoId, excecoesAntes.GetValueOrDefault(e.Id)?.CondicaoPagamentoId, c.Condicoes, x => x.Ativo, x => x.Nome, "condição de pagamento", erros);

        var carteiraAntes = (anterior?.Carteira ?? []).ToDictionary(x => x.Id);
        foreach (var v in p.Carteira)
        {
            var antes = carteiraAntes.GetValueOrDefault(v.Id);
            Conferir((Guid?)v.TipoCarteiraId, antes?.TipoCarteiraId, c.TiposCarteira, x => x.Ativo, x => x.Nome, "papel comercial", erros);
            if (v.VendedorId == Guid.Empty || !v.Ativo) continue;
            if (antes is not null && antes.VendedorId == v.VendedorId && antes.TipoCarteiraId == v.TipoCarteiraId) continue;
            if (!c.Pessoas.TryGetValue(v.VendedorId, out var pessoa))
            {
                erros.Add("Carteira: a pessoa escolhida não existe mais ou não está ativa.");
                continue;
            }
            if (!c.TiposCarteira.TryGetValue(v.TipoCarteiraId, out var papel)) continue;
            var aceitas = papel.ClassificacoesAceitas.ToList();
            if (aceitas.Any(pessoa.Classificacoes.Contains)) continue;
            var nomes = aceitas.Select(id => c.Classificacoes.GetValueOrDefault(id, "?")).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
            erros.Add(nomes.Count == 0
                ? $"Carteira: o papel \"{papel.Nome}\" não tem quem possa ocupá-lo; marque as classificações aceitas em Configurações › Papéis comerciais."
                : $"Carteira: {pessoa.Nome} não pode ser \"{papel.Nome}\": precisa ter a classificação {string.Join(" ou ", nomes)} ativa " +
                  "(Configurações › Papéis comerciais › Quem pode ser).");
        }
        return erros.Distinct().ToList();
    }

    private static void Conferir<T>(Guid? id, Guid? anterior, IReadOnlyDictionary<Guid, T> cadastro, Func<T, bool> ativo,
                                    Func<T, string> nome, string oQue, List<string> erros)
    {
        if (id is not { } valor || valor == Guid.Empty) return;
        if (!cadastro.TryGetValue(valor, out var item))
            erros.Add($"Um(a) {oQue} escolhido(a) não existe mais. Reabra o cadastro e escolha de novo.");
        else if (!ativo(item) && anterior != valor)
            erros.Add($"\"{nome(item)}\" ({oQue}) está desativado(a) e não pode ser escolhido(a) de novo.");
    }

    /// <summary>
    /// D5: o vendedor padrão de cada conta passa a ser o vendedor vigente do papel "responsável da conta" (mesma empresa).
    /// Sem responsável vigente, a conta fica como estava (compatibilidade com o que já existia).
    /// </summary>
    public static void AtualizarVendedorPadrao(Pessoa p, IReadOnlyDictionary<Guid, TipoCarteira> tipos, DateOnly hoje)
    {
        foreach (var conta in p.ContasCliente)
        {
            var principal = p.Carteira
                .Where(c => c.Vigente(hoje) && c.EmpresaId == conta.EmpresaId && tipos.TryGetValue(c.TipoCarteiraId, out var t) && t.ResponsavelDaConta)
                .OrderByDescending(c => c.InicioEm).FirstOrDefault();
            if (principal is not null) conta.VendedorPadraoId = principal.VendedorId;
        }
    }
}
