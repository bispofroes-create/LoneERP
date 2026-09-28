using System.Globalization;
using Lone.Domain.Entidades;

namespace Lone.Domain.Comercial;

/// <summary>Valores comerciais que valem para o cliente numa data (exceção vigente → perfil → conta).</summary>
public sealed record ValoresComerciais(
    decimal? LimiteCredito, decimal? DescontoMaximo, int? DiasMaximoAtraso, Guid? CondicaoPagamentoId, bool ExigeAprovacaoAcimaLimite);

/// <summary>Cadastros usados na conferência dos dados comerciais do cliente (com os desativados).</summary>
public sealed record ComercialParaConferir(
    IReadOnlyDictionary<Guid, PerfilComercial> Perfis,
    IReadOnlyDictionary<Guid, CondicaoPagamento> Condicoes,
    IReadOnlyDictionary<Guid, TipoCarteira> TiposCarteira,
    IReadOnlySet<Guid> Vendedores);

/// <summary>
/// Resultado de <see cref="RegrasComercial.PlanejarSubstituicao"/>: os vínculos vigentes que um vínculo novo substitui e
/// os que impedem a substituição (começam no mesmo dia ou depois dele).
/// </summary>
public sealed record PlanoSubstituicao(IReadOnlyList<CarteiraCliente> Encerrar, IReadOnlyList<CarteiraCliente> Impedem)
{
    public bool TemConflito => Encerrar.Count > 0 || Impedem.Count > 0;
    public bool Impedida => Impedem.Count > 0;
}

/// <summary>Os tipos de carteira iniciais (Ids fixos; o usuário pode criar outros).</summary>
public static class TiposCarteiraIniciais
{
    public static IReadOnlyList<(Guid Id, string Nome, int Ordem, bool Principal)> Todos { get; } =
    [
        (new Guid("7a9e1c04-0000-0000-0000-000000000001"), "Vendedor", 1, true),
        (new Guid("7a9e1c04-0000-0000-0000-000000000002"), "Representante", 2, false),
        (new Guid("7a9e1c04-0000-0000-0000-000000000003"), "Televendas", 3, false),
        (new Guid("7a9e1c04-0000-0000-0000-000000000004"), "Supervisor", 4, false)
    ];
}

/// <summary>Regras comerciais: condições de pagamento, perfis, exceções com vigência e carteira de clientes. Não acessa banco.</summary>
public static class RegrasComercial
{
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

    /// <summary>Datas, valores e sobreposições das exceções e da carteira (sem consultar cadastros).</summary>
    public static List<string> Validar(Pessoa p, IReadOnlyDictionary<Guid, TipoCarteira> tipos)
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
            if (c.TipoCarteiraId == Guid.Empty) erros.Add($"{rotulo}: escolha o tipo.");
            if (c.InicioEm == default) erros.Add($"{rotulo}: informe o início.");
            if (c.FimEm is { } fim && fim < c.InicioEm) erros.Add($"{rotulo}: o fim é anterior ao início.");
            if (c.Observacao is { Length: > 250 }) erros.Add($"{rotulo}: observação de no máximo 250 caracteres.");
        }

        // Exclusivo: ninguém mais do mesmo tipo no período. Principal: um só vendedor principal por vez (vira o vendedor padrão).
        foreach (var a in ativos)
            foreach (var b in ativos.Where(b => Conflitam(a, b, tipos)))
            {
                var principal = tipos.TryGetValue(a.TipoCarteiraId, out var tipo) && tipo.Principal;
                erros.Add(principal
                    ? $"Carteira: só um \"{tipo!.Nome}\" (principal) por vez; os períodos se sobrepõem em {Data(Maior(a.InicioEm, b.InicioEm))}."
                    : $"Carteira: o vínculo exclusivo de {Data((a.Exclusivo ? a : b).InicioEm)} se sobrepõe a outro do mesmo tipo.");
            }
        return erros.Distinct().ToList();
    }

    // ---------------------------------------------------------------- Carteira: conflito e substituição

    /// <summary>
    /// Dois vínculos que não podem valer ao mesmo tempo (a regra única da carteira, usada na validação, na substituição
    /// da ficha e no gatilho do banco, SqlMigracaoCarteira): ambos ativos, mesmo tipo, mesma empresa (nula = todas), períodos sobrepostos e o tipo é
    /// principal ou algum dos dois é exclusivo. Tipos diferentes ou empresas diferentes nunca conflitam.
    /// </summary>
    public static bool Conflitam(CarteiraCliente a, CarteiraCliente b, IReadOnlyDictionary<Guid, TipoCarteira> tipos) =>
        !ReferenceEquals(a, b) && a.Ativo && b.Ativo &&
        a.TipoCarteiraId == b.TipoCarteiraId && a.EmpresaId == b.EmpresaId &&
        Sobrepoem(a.InicioEm, a.FimEm, b.InicioEm, b.FimEm) &&
        (a.Exclusivo || b.Exclusivo || (tipos.TryGetValue(a.TipoCarteiraId, out var tipo) && tipo.Principal));

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
    /// Frases do histórico para as substituições feitas nesta gravação: um vínculo já gravado que passou a terminar na
    /// véspera do início de um vínculo novo do mesmo tipo e empresa ("João (Vendedor) encerrado em 14/03/2026 e
    /// substituído por Maria a partir de 15/03/2026"). Roda na gravação da pessoa: vale para a ficha e, quando existir, para o
    /// lote da Etapa 4b, que também grava pela pessoa.
    /// </summary>
    public static IEnumerable<string> Substituicoes(IEnumerable<CarteiraCliente> anteriores, IEnumerable<CarteiraCliente> atuais,
                                                    IReadOnlyDictionary<Guid, TipoCarteira> tipos, Func<Guid, string> nomeVendedor)
    {
        var antes = anteriores.ToDictionary(c => c.Id);
        var lista = atuais.ToList();
        var novos = lista.Where(c => c.Ativo && !antes.ContainsKey(c.Id)).ToList();
        foreach (var encerrado in lista)
        {
            if (!antes.TryGetValue(encerrado.Id, out var gravado) || !encerrado.Ativo || encerrado.FimEm is not { } fim) continue;
            if (gravado.FimEm is { } fimAntes && fimAntes <= fim) continue; // não foi encurtado agora
            // Sucessor: novo, começa no dia seguinte e conflitaria com o anterior como estava gravado (tipo principal ou
            // exclusivo). Encerrar um e incluir outro de um tipo que aceita vários não é substituição.
            var sucessor = novos.FirstOrDefault(n => n.InicioEm == fim.AddDays(1) && Conflitam(gravado, n, tipos));
            if (sucessor is null) continue;
            var tipo = tipos.TryGetValue(encerrado.TipoCarteiraId, out var t) ? t.Nome : "carteira";
            yield return $"Carteira: {nomeVendedor(encerrado.VendedorId)} ({tipo}) encerrado em {Data(fim)} e substituído por " +
                         $"{nomeVendedor(sucessor.VendedorId)} a partir de {Data(sucessor.InicioEm)}.";
        }
    }

    private static DateOnly Maior(DateOnly a, DateOnly b) => a > b ? a : b;

    /// <summary>
    /// Referências: perfil, condições e tipos existentes (desativado só se já era o gravado ali) e vendedores com o papel
    /// Vendedor/Representante (um vendedor antigo que perdeu o papel continua nos períodos gravados).
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
            Conferir((Guid?)v.TipoCarteiraId, antes?.TipoCarteiraId, c.TiposCarteira, x => x.Ativo, x => x.Nome, "tipo de carteira", erros);
            if (v.VendedorId != Guid.Empty && v.VendedorId != antes?.VendedorId && !c.Vendedores.Contains(v.VendedorId))
                erros.Add("Carteira: o vendedor escolhido precisa estar ativo e ter o papel Vendedor ou Representante.");
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
    /// D5: o vendedor padrão de cada conta passa a ser o vendedor principal vigente da carteira (mesma empresa).
    /// Sem vendedor principal vigente, a conta fica como estava (compatibilidade com o que já existia).
    /// </summary>
    public static void AtualizarVendedorPadrao(Pessoa p, IReadOnlyDictionary<Guid, TipoCarteira> tipos, DateOnly hoje)
    {
        foreach (var conta in p.ContasCliente)
        {
            var principal = p.Carteira
                .Where(c => c.Vigente(hoje) && c.EmpresaId == conta.EmpresaId && tipos.TryGetValue(c.TipoCarteiraId, out var t) && t.Principal)
                .OrderByDescending(c => c.InicioEm).FirstOrDefault();
            if (principal is not null) conta.VendedorPadraoId = principal.VendedorId;
        }
    }
}
