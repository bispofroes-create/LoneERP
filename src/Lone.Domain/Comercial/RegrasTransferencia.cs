using System.Globalization;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Comercial;

/// <summary>O que a transferência leva: os vínculos ativos da origem, no papel e na empresa escolhidos, a partir do efeito.</summary>
/// <param name="TipoCarteiraId">Nulo = todos os papéis da origem.</param>
/// <param name="EmpresaId">Nulo = vínculos de todas as empresas.</param>
public sealed record FiltroTransferencia(Guid OrigemId, Guid? TipoCarteiraId, Guid? EmpresaId, DateOnly EfeitoEm);

/// <summary>Um vínculo da origem que passa para o destino: a origem termina na véspera do efeito, o novo começa no efeito.</summary>
public sealed record VinculoTransferido(CarteiraCliente Origem, CarteiraCliente Novo);

/// <summary>Um vínculo da origem que fica como está, com o motivo.</summary>
public sealed record VinculoNaoTransferido(CarteiraCliente Origem, string Motivo);

/// <summary>O plano de um cliente (<see cref="RegrasTransferencia.Planejar"/>): o que passa e o que fica.</summary>
public sealed record PlanoTransferenciaCliente(Guid ClienteId, Guid DestinoId, IReadOnlyList<VinculoTransferido> Transferir,
                                               IReadOnlyList<VinculoNaoTransferido> Manter);

/// <summary>
/// Regras da transferência de carteira (Motor Comercial, Fase 1d). Não acessa banco. A transferência usa as mesmas regras da
/// ficha (RegrasComercial): encerra o vínculo da origem na véspera do efeito e abre o do destino no efeito, com o mesmo
/// papel, empresa, exclusivo, fim planejado e percentual de crédito (a soma de 100% não quebra). Nada é apagado; vínculo
/// que começa no efeito ou depois fica como está (decisão T4); data de efeito até N dias no passado (decisão T1).
/// </summary>
public static class RegrasTransferencia
{
    /// <summary>Máximo de clientes numa transferência (processada na hora, cliente a cliente; decisão T3).</summary>
    public const int MaximoClientes = 500;

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>"TR-2026-0001".</summary>
    public static string Numero(int ano, int sequencia) => $"TR-{ano}-{sequencia.ToString("0000", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// O pedido, antes de olhar os clientes: origem, destinos (sem repetir e sem a própria origem), efeito dentro do limite de
    /// dias para trás, motivo obrigatório e textos dentro do tamanho.
    /// </summary>
    public static List<string> ValidarPedido(FiltroTransferencia f, IReadOnlyCollection<Guid> destinos, string? motivo, string? observacao,
                                             DateOnly hoje, int diasRetroativos)
    {
        var erros = new List<string>();
        if (f.OrigemId == Guid.Empty) erros.Add("Escolha de quem a carteira sai (origem).");
        if (destinos.Count == 0 || destinos.Any(d => d == Guid.Empty)) erros.Add("Escolha para quem a carteira vai (destino).");
        if (f.OrigemId != Guid.Empty && destinos.Contains(f.OrigemId)) erros.Add("O destino não pode ser a própria origem.");
        if (destinos.Distinct().Count() != destinos.Count) erros.Add("Um destino foi escolhido duas vezes.");

        if (f.EfeitoEm == default) erros.Add("Informe a data de efeito (o primeiro dia do destino).");
        else if (f.EfeitoEm < hoje.AddDays(-Math.Max(0, diasRetroativos)))
            erros.Add(diasRetroativos <= 0
                ? "A data de efeito não pode ser anterior a hoje (Parâmetros comerciais › Datas no passado)."
                : $"A data de efeito pode ser no máximo {diasRetroativos} dia(s) antes de hoje ({Data(hoje.AddDays(-diasRetroativos))}), " +
                  "para não reescrever períodos que podem já ter sido apurados (Parâmetros comerciais › Datas no passado).");

        if (string.IsNullOrWhiteSpace(motivo)) erros.Add("Informe o motivo da transferência (ex.: desligamento, reorganização da equipe).");
        else if (motivo.Trim().Length > TransferenciaCarteira.TamanhoMaximoTexto)
            erros.Add($"Motivo de no máximo {TransferenciaCarteira.TamanhoMaximoTexto} caracteres.");
        if (observacao is { } obs && obs.Trim().Length > TransferenciaCarteira.TamanhoMaximoTexto)
            erros.Add($"Observação de no máximo {TransferenciaCarteira.TamanhoMaximoTexto} caracteres.");
        return erros;
    }

    /// <summary>Aviso da prévia quando o efeito é no passado: o crédito desse intervalo muda de dono.</summary>
    public static string? AvisoRetroativo(DateOnly efeito, DateOnly hoje) =>
        efeito < hoje
            ? $"O efeito ({Data(efeito)}) é anterior a hoje: de {Data(efeito)} a {Data(hoje.AddDays(-1))}, os clientes transferidos " +
              "passam a constar como atendidos pelo destino (inclusive no crédito das vendas desse período)."
            : null;

    /// <summary>Os vínculos da origem que o filtro alcança: ativos, do papel e da empresa escolhidos, valendo no efeito ou depois.</summary>
    public static IEnumerable<CarteiraCliente> Candidatos(IEnumerable<CarteiraCliente> carteira, FiltroTransferencia f) =>
        carteira.Where(c => c.Ativo && c.VendedorId == f.OrigemId &&
                            (f.TipoCarteiraId is null || c.TipoCarteiraId == f.TipoCarteiraId) &&
                            (f.EmpresaId is null || c.EmpresaId == f.EmpresaId) &&
                            (c.FimEm is null || c.FimEm >= f.EfeitoEm));

    /// <summary>
    /// O plano de um cliente, sem mudar nada: cada vínculo da origem passa para o destino, exceto o que começa no efeito ou
    /// depois (fica como está: encerrá-lo apagaria o período dele) e o do papel em que o destino já atende o cliente no
    /// mesmo período. As demais regras (um por vez, limite, crédito, "Quem pode ser") são as da ficha, conferidas depois de
    /// <see cref="Aplicar"/>.
    /// </summary>
    public static PlanoTransferenciaCliente Planejar(Pessoa cliente, FiltroTransferencia f, Guid destinoId, Guid transferenciaId,
                                                     IReadOnlyDictionary<Guid, TipoCarteira> tipos, Func<Guid> novoId)
    {
        var transferir = new List<VinculoTransferido>();
        var manter = new List<VinculoNaoTransferido>();
        string Papel(Guid id) => tipos.TryGetValue(id, out var t) ? t.Nome : "carteira";

        foreach (var c in Candidatos(cliente.Carteira, f).OrderBy(c => c.InicioEm).ToList())
        {
            if (c.InicioEm >= f.EfeitoEm)
            {
                manter.Add(new VinculoNaoTransferido(c, $"{Papel(c.TipoCarteiraId)} começa em {Data(c.InicioEm)}, no dia do efeito ou depois: " +
                                                        "fica como está (corrija na ficha do cliente se precisar)."));
                continue;
            }
            if (destinoId == cliente.Id)
            {
                manter.Add(new VinculoNaoTransferido(c, "O destino é o próprio cliente."));
                continue;
            }
            var fim = c.FimEm;
            var jaAtende = cliente.Carteira.Any(o => o.Ativo && o.VendedorId == destinoId && o.TipoCarteiraId == c.TipoCarteiraId &&
                                                     o.EmpresaId == c.EmpresaId && o.InicioEm <= (fim ?? DateOnly.MaxValue) &&
                                                     (o.FimEm is null || o.FimEm >= f.EfeitoEm));
            if (jaAtende)
            {
                manter.Add(new VinculoNaoTransferido(c, $"O destino já atende este cliente como {Papel(c.TipoCarteiraId)} no período."));
                continue;
            }

            transferir.Add(new VinculoTransferido(c, new CarteiraCliente
            {
                Id = novoId(),
                PessoaId = cliente.Id,
                EmpresaId = c.EmpresaId,
                TipoCarteiraId = c.TipoCarteiraId,
                VendedorId = destinoId,
                InicioEm = f.EfeitoEm,
                FimEm = fim,
                Exclusivo = c.Exclusivo,
                PercentualCredito = c.PercentualCredito,
                Origem = OrigemVinculoCarteira.Transferencia,
                TransferenciaId = transferenciaId,
                Ativo = true
            }));
        }
        return new PlanoTransferenciaCliente(cliente.Id, destinoId, transferir, manter);
    }

    /// <summary>Aplica o plano na carteira do cliente: encerra cada vínculo da origem na véspera do efeito e inclui o do destino.</summary>
    public static void Aplicar(Pessoa cliente, PlanoTransferenciaCliente plano)
    {
        foreach (var (origem, novo) in plano.Transferir)
        {
            origem.FimEm = novo.InicioEm.AddDays(-1);
            cliente.Carteira.Add(novo);
        }
    }

    /// <summary>
    /// Cópia da pessoa só com o que as regras da carteira comparam ("como estava gravado"): contas, exceções e a carteira com
    /// os vínculos copiados (o plano muda os originais). Evita ler o cliente duas vezes do banco.
    /// </summary>
    public static Pessoa ComoEstava(Pessoa p) => new()
    {
        Id = p.Id,
        Nome = p.Nome,
        Situacao = p.Situacao,
        ContasCliente = p.ContasCliente,
        ContasFornecedor = p.ContasFornecedor,
        ExcecoesComerciais = p.ExcecoesComerciais,
        Carteira = [.. p.Carteira.Select(Copiar)]
    };

    public static CarteiraCliente Copiar(CarteiraCliente c) => new()
    {
        Id = c.Id,
        PessoaId = c.PessoaId,
        EmpresaId = c.EmpresaId,
        TipoCarteiraId = c.TipoCarteiraId,
        VendedorId = c.VendedorId,
        InicioEm = c.InicioEm,
        FimEm = c.FimEm,
        Exclusivo = c.Exclusivo,
        PercentualCredito = c.PercentualCredito,
        Origem = c.Origem,
        TransferenciaId = c.TransferenciaId,
        Observacao = c.Observacao,
        Ativo = c.Ativo,
        CriadoEm = c.CriadoEm,
        AtualizadoEm = c.AtualizadoEm
    };

    /// <summary>
    /// Divide os clientes entre vários destinos pela menor carteira (decisão T2): cada cliente, na ordem recebida, vai para
    /// quem tem menos clientes naquele momento (a carga atual mais o que já recebeu); no empate, o primeiro da lista de
    /// destinos. Com um destino só, todos vão para ele.
    /// </summary>
    public static Dictionary<Guid, Guid> DividirPelaMenorCarteira(IReadOnlyList<Guid> clientes, IReadOnlyList<Guid> destinos,
                                                                  IReadOnlyDictionary<Guid, int> cargaAtual)
    {
        if (destinos.Count == 0) throw new ArgumentException("Informe ao menos um destino.", nameof(destinos));
        var carga = destinos.Distinct().ToDictionary(d => d, d => cargaAtual.GetValueOrDefault(d));
        var resultado = new Dictionary<Guid, Guid>();
        foreach (var cliente in clientes.Distinct())
        {
            var menor = destinos.Distinct().OrderBy(d => carga[d]).First(); // OrderBy é estável: o empate fica com o primeiro
            resultado[cliente] = menor;
            carga[menor]++;
        }
        return resultado;
    }

    /// <summary>
    /// "Carteira: João (Vendedor) transferido para Maria a partir de 01/10/2026 (TR-2026-0003: desligamento)." Uma frase
    /// por vínculo, no histórico do cliente.
    /// </summary>
    public static IEnumerable<string> Frases(PlanoTransferenciaCliente plano, IReadOnlyDictionary<Guid, TipoCarteira> tipos,
                                             Func<Guid, string> nome, string numero, string motivo) =>
        plano.Transferir.Select(t =>
            $"Carteira: {nome(t.Origem.VendedorId)} ({(tipos.TryGetValue(t.Origem.TipoCarteiraId, out var p) ? p.Nome : "carteira")}) " +
            $"transferido para {nome(t.Novo.VendedorId)} a partir de {Data(t.Novo.InicioEm)} ({numero}: {motivo}).");

    /// <summary>
    /// As contagens por cliente: transferido se algum vínculo dele passou; com erro se falhou ao gravar; senão não
    /// processado (um cliente conta uma vez só).
    /// </summary>
    public static (int Transferidos, int NaoProcessados, int Erros) Contar(IEnumerable<TransferenciaCarteiraItem> itens)
    {
        var porCliente = itens.GroupBy(i => i.ClienteId).Select(g => g.Select(i => i.Resultado).ToList()).ToList();
        var transferidos = porCliente.Count(r => r.Contains(ResultadoItemTransferencia.Transferido));
        var erros = porCliente.Count(r => !r.Contains(ResultadoItemTransferencia.Transferido) && r.Contains(ResultadoItemTransferencia.Erro));
        return (transferidos, porCliente.Count - transferidos - erros, erros);
    }
}
