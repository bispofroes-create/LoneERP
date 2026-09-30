using System.Globalization;
using System.Text.Json;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Territorios;

/// <summary>
/// O conteúdo proposto de uma mudança (coluna Depois, JSON). Forma fechada: só os campos do tipo entram, o resto fica nulo.
/// </summary>
public sealed class DadosMudancaTerritorial
{
    /// <summary>Nova versão da regra: grupos de inclusão/exclusão (JSON do catálogo), o texto congelado e a prioridade.</summary>
    public string? Grupos { get; set; }
    public string? Criterios { get; set; }
    public int? Prioridade { get; set; }

    /// <summary>Fixar/Retirar: motivo (obrigatório) e fim opcional.</summary>
    public string? Motivo { get; set; }
    public DateOnly? FimEm { get; set; }

    /// <summary>Mover território: o novo pai (nulo = primeiro nível).</summary>
    public Guid? NovoPaiId { get; set; }

    private static readonly JsonSerializerOptions Opcoes = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public string ParaJson() => JsonSerializer.Serialize(this, Opcoes);

    public static DadosMudancaTerritorial DeJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<DadosMudancaTerritorial>(json, Opcoes) ?? new();
}

/// <summary>
/// Regras da operação territorial TE- (Fase 2b-1b). Não acessa banco. Datas (T8, DN-01, RT-1, RT-6), situações e
/// transições (DN-04), quem cancela (DN-06/RT-2), quem e quando desfaz (T15/DN-05) e a validade de cada mudança contra o
/// estado que o serviço leu.
/// </summary>
public static class RegrasOperacaoTerritorial
{
    /// <summary>"TE-2026-0001".</summary>
    public static string Numero(int ano, int sequencia) =>
        $"{OperacaoTerritorial.Prefixo}-{ano}-{sequencia.ToString("0000", CultureInfo.InvariantCulture)}";

    private static string Data(DateOnly d) => RegrasCadastroTerritorial.Data(d);

    public const string MensagemMapaMudou =
        "O mapa mudou desde a simulação (outra operação foi aplicada, a árvore foi alterada ou o mapa foi desativado). Nada foi " +
        "gravado: simule de novo, confira e aplique.";

    public const string MensagemAssinaturaDiferente =
        "O resultado mudou desde a simulação (o cadastro de clientes foi alterado). Nada foi gravado: simule de novo, confira e aplique.";

    public const string MensagemOperacaoAlterada =
        "A operação foi alterada por outro usuário (ou em outra janela) enquanto esta tela estava aberta. Nada foi salvo: " +
        "recarregue a operação e refaça a mudança.";

    /// <summary>
    /// DN-12: a aplicação é síncrona e numa transação só, com o motor do mapa travado do começo ao fim. Acima deste número de
    /// clientes gravados (entram, saem, mudam de território ou têm a origem atualizada), a operação é recusada antes de travar
    /// qualquer coisa. O valor saiu do teste real de volume (VolumeOperacoesTerritoriaisTests), que aplica exatamente este
    /// número: com 10.000 clientes, simular levou 1,5 s e aplicar 2,8 s no SQL Server Express de desenvolvimento, e o
    /// timeout de comando da aplicação é de 5 minutos. Constante de propósito (não é parâmetro da tela): mudar exige medir de
    /// novo.
    /// </summary>
    public const int LimiteClientesPorOperacao = 50_000;

    /// <summary>A partir daqui a simulação avisa que a aplicação vai demorar e travar o mapa enquanto grava (DN-12).</summary>
    public const int AvisoClientesPorOperacao = 10_000;

    /// <summary>Clientes que a aplicação grava: os bloqueados (inconsistências) não contam, porque já impedem aplicar.</summary>
    public static int ClientesGravados(int entram, int saem, int mudam, int origemAtualizada) => entram + saem + mudam + origemAtualizada;

    /// <summary>
    /// A recusa acima do limite (DN-12). De propósito não prescreve como dividir (UF, região, segmento, território ou
    /// reformular a regra): isso é decisão de quem planeja, não do motor.
    /// </summary>
    public static string? AcimaDoLimite(int clientes) => clientes <= LimiteClientesPorOperacao
        ? null
        : $"Esta operação afetará {clientes.ToString("N0", Brasil)} clientes, ultrapassando o limite de " +
          $"{LimiteClientesPorOperacao.ToString("N0", Brasil)} clientes afetados por operação (a aplicação é feita numa " +
          "transação só). Reduza o escopo da operação ou divida o planejamento em operações menores.";

    /// <summary>O aviso prévio (DN-12) entre o aviso e o limite: nada impede, só avisa o tempo e a trava.</summary>
    public static string? AvisoDeVolume(int clientes) => clientes < AvisoClientesPorOperacao || clientes > LimiteClientesPorOperacao
        ? null
        : $"Esta operação afetará {clientes.ToString("N0", Brasil)} clientes: a aplicação pode levar alguns segundos e, enquanto " +
          "grava, outras aplicações e alterações da árvore deste mapa esperam ela terminar.";

    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Evento de uma edição do rascunho (incluir ou retirar mudança, trocar data, motivo ou observação). Qualquer usuário
    /// com PLANEJAR edita qualquer rascunho (DN-06; a rowversion impede editar às cegas), e por isso, quando quem edita não é
    /// quem criou, o evento diz os dois: "João alterou a operação TE-2026-0001, criada por Maria: ...". A comparação é pelo
    /// Id do usuário, não pelo nome (que pode mudar). O antes → depois de data, motivo e observação fica nas linhas campo a
    /// campo da auditoria, que a história da operação também mostra.
    /// </summary>
    public static string EventoDeEdicao(OperacaoTerritorial op, Guid? usuarioId, string usuario, string oQue)
    {
        const int Maximo = 500; // Descricao da auditoria
        var texto = op.CriadaPorId != usuarioId
            ? $"{usuario} alterou a operação {op.Numero}, criada por {op.CriadaPor}: {oQue}."
            : $"Operação {op.Numero}: {oQue}.";
        return texto.Length > Maximo ? texto[..(Maximo - 1)] + "…" : texto;
    }

    /// <summary>Tipos que mudam a estrutura da árvore: só com efeito até hoje (DN-01).</summary>
    public static bool Estrutural(TipoMudancaTerritorial tipo) =>
        tipo is TipoMudancaTerritorial.MoverTerritorio or TipoMudancaTerritorial.EncerrarTerritorio or TipoMudancaTerritorial.ReativarTerritorio;

    /// <summary>
    /// A data de efeito (T8, DN-01, RT-1): não antes de hoje − dias retroativos; nunca antes do último efeito aplicado no
    /// mapa (com o número da operação que bloqueia e o caminho: desfazê-la); com mudança de estrutura, nunca depois de hoje.
    /// </summary>
    public static List<string> ValidarEfeito(DateOnly efeito, DateOnly hoje, int diasRetroativos, DateOnly? ultimoEfeitoDoMapa,
                                             string? ultimaOperacaoDoMapa, bool temMudancaEstrutural)
    {
        var erros = new List<string>();
        if (efeito == default)
        {
            erros.Add("Informe a data de efeito (o primeiro dia em que as mudanças valem).");
            return erros;
        }
        var limite = hoje.AddDays(-Math.Max(0, diasRetroativos));
        if (efeito < limite)
            erros.Add(diasRetroativos <= 0
                ? "A data de efeito não pode ser anterior a hoje (Parâmetros territoriais › Datas no passado)."
                : $"A data de efeito pode ser no máximo {diasRetroativos} dia(s) antes de hoje ({Data(limite)}) (Parâmetros territoriais › Datas no passado).");
        if (ultimoEfeitoDoMapa is { } ultimo && efeito < ultimo)
            erros.Add(ultimo > hoje && ultimaOperacaoDoMapa is not null
                ? $"Esta operação não pode ser aplicada porque existe a {ultimaOperacaoDoMapa} com efeito futuro ({Data(ultimo)}) neste mapa. " +
                  $"Para realizar esta correção, primeiro desfaça a {ultimaOperacaoDoMapa}."
                : $"A data de efeito não pode ser anterior ao efeito da última operação aplicada neste mapa ({Data(ultimo)}" +
                  (ultimaOperacaoDoMapa is null ? ")." : $", {ultimaOperacaoDoMapa}).") + " A linha do tempo do mapa não volta para trás.");
        if (temMudancaEstrutural && efeito > hoje)
            erros.Add("Mover, encerrar ou reativar território só com efeito até hoje: a estrutura da árvore não tem data futura nesta fase.");
        return erros;
    }

    /// <summary>Motivo e observação da operação.</summary>
    public static List<string> ValidarTextos(string? motivo, string? observacao)
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(motivo)) erros.Add("Informe o motivo da operação.");
        else if (motivo.Trim().Length > OperacaoTerritorial.TamanhoMaximoMotivo)
            erros.Add($"Motivo de no máximo {OperacaoTerritorial.TamanhoMaximoMotivo} caracteres.");
        if (observacao?.Trim().Length > OperacaoTerritorial.TamanhoMaximoObservacao)
            erros.Add($"Observação de no máximo {OperacaoTerritorial.TamanhoMaximoObservacao} caracteres.");
        return erros;
    }

    // ------------------------------------------------------------------ Situações e transições (DN-04)

    public static void ExigirAberta(OperacaoTerritorial op)
    {
        if (!op.Aberta)
            throw new Validacao.ValidacaoException([$"A operação {op.Numero} está {Nome(op.Situacao)}: não pode mais ser alterada."]);
    }

    public static string Nome(SituacaoOperacaoTerritorial s) => s switch
    {
        SituacaoOperacaoTerritorial.Rascunho => "em rascunho",
        SituacaoOperacaoTerritorial.Simulada => "simulada",
        SituacaoOperacaoTerritorial.Aplicada => "aplicada",
        SituacaoOperacaoTerritorial.Cancelada => "cancelada",
        SituacaoOperacaoTerritorial.Desfeita => "desfeita",
        _ => s.ToString()
    };

    /// <summary>Qualquer edição das mudanças (ou da data) volta a operação para Rascunho: a simulação deixa de valer.</summary>
    public static void VoltarARascunho(OperacaoTerritorial op)
    {
        ExigirAberta(op);
        if (op.Situacao == SituacaoOperacaoTerritorial.Simulada)
        {
            op.Situacao = SituacaoOperacaoTerritorial.Rascunho;
            op.SimulacaoAtualId = null;
        }
    }

    public static void MarcarSimulada(OperacaoTerritorial op, Guid simulacaoId)
    {
        ExigirAberta(op);
        op.Situacao = SituacaoOperacaoTerritorial.Simulada;
        op.SimulacaoAtualId = simulacaoId;
        op.RegistrarEvento($"Operação {op.Numero} simulada.");
    }

    /// <summary>
    /// Quem pode cancelar (DN-06/RT-2): APLICAR cancela qualquer operação em Rascunho ou Simulada; PLANEJAR cancela só a
    /// própria. Aplicada nunca é cancelada (só desfeita, DN-05); Cancelada e Desfeita já terminaram.
    /// </summary>
    public static List<string> ValidarCancelamento(OperacaoTerritorial op, Guid? usuarioId, bool podeAplicar, bool podePlanejar, string? motivo)
    {
        var erros = new List<string>();
        if (op.Situacao == SituacaoOperacaoTerritorial.Aplicada)
            erros.Add($"A operação {op.Numero} já foi aplicada: não se cancela. Antes do efeito, e se for a última do mapa, ela pode ser desfeita.");
        else if (!op.Aberta)
            erros.Add($"A operação {op.Numero} já está {Nome(op.Situacao)}.");
        else if (!podeAplicar && !(podePlanejar && usuarioId is { } u && op.CriadaPorId == u))
            erros.Add("Só quem criou a operação (ou quem pode aplicar operações territoriais) pode cancelá-la.");
        if (string.IsNullOrWhiteSpace(motivo)) erros.Add("Informe o motivo do cancelamento.");
        else if (motivo.Trim().Length > OperacaoTerritorial.TamanhoMaximoMotivo)
            erros.Add($"Motivo de no máximo {OperacaoTerritorial.TamanhoMaximoMotivo} caracteres.");
        return erros;
    }

    public static void Cancelar(OperacaoTerritorial op, Guid? usuarioId, string usuario, string motivo, DateTime agora)
    {
        op.Situacao = SituacaoOperacaoTerritorial.Cancelada;
        op.CanceladaEm = agora;
        op.CanceladaPorId = usuarioId;
        op.CanceladaPor = usuario;
        op.CanceladaMotivo = motivo.Trim();
        op.RegistrarEvento($"Operação {op.Numero} cancelada. Motivo: {op.CanceladaMotivo}");
    }

    /// <summary>
    /// Desfazer (T15/DN-05): só Aplicada, só antes do efeito, só a última aplicada do mapa, com motivo. Sem cascata: havendo
    /// outra aplicada depois, a recusa diz qual é.
    /// </summary>
    public static List<string> ValidarDesfazer(OperacaoTerritorial op, DateOnly hoje, Guid? ultimaOperacaoDoMapaId,
                                               string? ultimaOperacaoDoMapa, string? motivo)
    {
        var erros = new List<string>();
        if (op.Situacao != SituacaoOperacaoTerritorial.Aplicada)
            erros.Add($"Só uma operação aplicada pode ser desfeita (a {op.Numero} está {Nome(op.Situacao)}).");
        else
        {
            if (op.EfeitoEm <= hoje)
                erros.Add($"A {op.Numero} já está em vigor (efeito em {Data(op.EfeitoEm)}): corrija com uma nova operação.");
            if (ultimaOperacaoDoMapaId != op.Id)
                erros.Add($"A {op.Numero} não é a última operação aplicada neste mapa" +
                          (ultimaOperacaoDoMapa is null ? "." : $" (a última é a {ultimaOperacaoDoMapa}): desfaça primeiro a {ultimaOperacaoDoMapa}."));
        }
        if (string.IsNullOrWhiteSpace(motivo)) erros.Add("Informe o motivo para desfazer a operação.");
        else if (motivo.Trim().Length > OperacaoTerritorial.TamanhoMaximoMotivo)
            erros.Add($"Motivo de no máximo {OperacaoTerritorial.TamanhoMaximoMotivo} caracteres.");
        return erros;
    }

    public static void Desfazer(OperacaoTerritorial op, Guid? usuarioId, string usuario, string motivo, DateTime agora)
    {
        op.Situacao = SituacaoOperacaoTerritorial.Desfeita;
        op.DesfeitaEm = agora;
        op.DesfeitaPorId = usuarioId;
        op.DesfeitaPor = usuario;
        op.DesfeitaMotivo = motivo.Trim();
        op.RegistrarEvento($"Operação {op.Numero} desfeita antes do efeito ({Data(op.EfeitoEm)}). Motivo: {op.DesfeitaMotivo}");
    }

    public static void MarcarAplicada(OperacaoTerritorial op, Guid? usuarioId, string usuario, DateTime agora,
                                      Guid? operacaoAnterior, DateOnly? efeitoAnterior)
    {
        op.Situacao = SituacaoOperacaoTerritorial.Aplicada;
        op.AplicadaEm = agora;
        op.AplicadaPorId = usuarioId;
        op.AplicadaPor = usuario;
        op.OperacaoAnteriorDoMapaId = operacaoAnterior;
        op.EfeitoAnteriorDoMapa = efeitoAnterior;
        op.RegistrarEvento($"Operação {op.Numero} aplicada com efeito em {Data(op.EfeitoEm)}: {op.Entraram} entraram, " +
                           $"{op.Sairam} saíram, {op.Mudaram} mudaram de território.");
    }

    // ------------------------------------------------------------------ Mudanças

    /// <summary>
    /// O estado que uma mudança é conferida contra (lido pelo serviço, com as outras mudanças da própria operação já
    /// consideradas quando fizer sentido).
    /// </summary>
    public sealed record ContextoMudanca(
        MapaTerritorial Mapa,
        IReadOnlyDictionary<Guid, Territorio> Territorios,
        IReadOnlySet<Guid> ComUso,
        IReadOnlyDictionary<Guid, RegraTerritorio> RegraVigentePorTerritorio,
        IReadOnlyDictionary<Guid, ExcecaoTerritorio> ExcecoesVigentes,
        Func<Guid, bool> NoUniverso,
        DateOnly Efeito);

    /// <summary>
    /// Confere uma mudança isolada. O conjunto (A abaixo de B e B abaixo de A na mesma operação; duas fixações do mesmo
    /// cliente) é conferido por <see cref="ValidarConjunto"/> e, no fim, pelo motor na simulação.
    /// </summary>
    public static List<string> ValidarMudanca(OperacaoTerritorialMudanca m, ContextoMudanca x)
    {
        var erros = new List<string>();
        var dados = DadosMudancaTerritorial.DeJson(m.Depois);
        Territorio? territorio = m.TerritorioId is { } tid && x.Territorios.TryGetValue(tid, out var t) ? t : null;
        if (m.TerritorioId is not null && territorio is null)
        {
            erros.Add("O território da mudança não existe neste mapa.");
            return erros;
        }

        switch (m.Tipo)
        {
            case TipoMudancaTerritorial.NovaVersaoRegra:
                if (territorio is null) { erros.Add("Escolha o território da regra."); break; }
                if (!territorio.Ativo) erros.Add($"O território \"{territorio.Nome}\" está encerrado: não recebe regra.");
                if (string.IsNullOrWhiteSpace(dados.Grupos)) erros.Add("A regra precisa de pelo menos um grupo de inclusão.");
                else if (dados.Grupos.Length > RegraTerritorio.TamanhoMaximoGrupos) erros.Add("A regra ficou grande demais: divida em menos condições.");
                if (dados.Criterios?.Length > RegraTerritorio.TamanhoMaximoCriterios) erros.Add("O texto dos critérios ficou grande demais.");
                if (dados.Prioridade is < 1) erros.Add("A prioridade é um número a partir de 1 (1 = mais alta), ou vazia.");
                ExigirBaseRegra(m, x, erros, obrigatoria: false);
                break;

            case TipoMudancaTerritorial.EncerrarRegra:
                if (territorio is null) { erros.Add("Escolha o território."); break; }
                if (!x.RegraVigentePorTerritorio.ContainsKey(territorio.Id)) erros.Add($"\"{territorio.Nome}\" não tem regra vigente para encerrar.");
                ExigirBaseRegra(m, x, erros, obrigatoria: true);
                break;

            case TipoMudancaTerritorial.Fixar:
            case TipoMudancaTerritorial.Retirar:
                if (territorio is null) { erros.Add("Escolha o território."); break; }
                if (m.PessoaId is null) erros.Add("Escolha o cliente.");
                else if (!x.NoUniverso(m.PessoaId.Value)) erros.Add("O cliente não faz parte do universo deste mapa (classificações do mapa).");
                if (m.Tipo == TipoMudancaTerritorial.Fixar && !territorio.Ativo)
                    erros.Add($"O território \"{territorio.Nome}\" está encerrado: não recebe fixação.");
                if (string.IsNullOrWhiteSpace(dados.Motivo)) erros.Add("Informe o motivo da exceção.");
                else if (dados.Motivo.Trim().Length > ExcecaoTerritorio.TamanhoMaximoMotivo)
                    erros.Add($"Motivo da exceção de no máximo {ExcecaoTerritorio.TamanhoMaximoMotivo} caracteres.");
                if (dados.FimEm is { } fim && fim < x.Efeito) erros.Add("O fim da exceção não pode ser anterior ao efeito da operação.");
                break;

            case TipoMudancaTerritorial.EncerrarExcecao:
                if (m.ExcecaoBaseId is not { } eid || !x.ExcecoesVigentes.TryGetValue(eid, out var excecao))
                    erros.Add("A exceção escolhida não está mais vigente.");
                else if (excecao.InicioEm >= x.Efeito)
                    erros.Add("A exceção começa no efeito ou depois: não há período para encerrar.");
                break;

            case TipoMudancaTerritorial.MoverTerritorio:
            case TipoMudancaTerritorial.EncerrarTerritorio:
            case TipoMudancaTerritorial.ReativarTerritorio:
                if (territorio is null) { erros.Add("Escolha o território."); break; }
                ValidarEstrutural(m, dados, territorio, x, erros);
                break;

            default:
                erros.Add("Tipo de mudança desconhecido.");
                break;
        }
        return erros;
    }

    private static void ExigirBaseRegra(OperacaoTerritorialMudanca m, ContextoMudanca x, List<string> erros, bool obrigatoria)
    {
        var vigente = m.TerritorioId is { } t && x.RegraVigentePorTerritorio.TryGetValue(t, out var r) ? r : null;
        if (vigente is null && m.RegraBaseId is null && !obrigatoria) return;
        if (vigente is null || vigente.Id != m.RegraBaseId)
            erros.Add(MensagemBaseVelha("a regra do território"));
    }

    public static string MensagemBaseVelha(string oque) =>
        $"Esta mudança foi planejada sobre uma versão de {oque} que não é mais a vigente (outra operação mudou depois). Revise a mudança.";

    /// <summary>
    /// Mudança estrutural com uso (T18) na operação: só com efeito até hoje (DN-01, conferido na data); o efeito tem de ser
    /// depois do início da posição aberta (RT-6); mesma regra de pai/ciclo/níveis da ficha (RegrasArvoreTerritorial).
    /// </summary>
    private static void ValidarEstrutural(OperacaoTerritorialMudanca m, DadosMudancaTerritorial dados, Territorio territorio,
                                          ContextoMudanca x, List<string> erros)
    {
        var aberta = RegrasArvoreTerritorial.PosicaoAberta(territorio);
        if (m.Tipo != TipoMudancaTerritorial.ReativarTerritorio)
        {
            if (!territorio.Ativo) { erros.Add($"O território \"{territorio.Nome}\" está encerrado."); return; }
            if (aberta is null || aberta.FimEm is not null) { erros.Add($"\"{territorio.Nome}\" não tem posição aberta na árvore."); return; }
            if (m.PosicaoBaseId != aberta.Id) erros.Add(MensagemBaseVelha("a posição do território"));
            if (x.Efeito <= aberta.InicioEm)
                erros.Add($"O efeito precisa ser depois do início da posição atual de \"{territorio.Nome}\" ({Data(aberta.InicioEm)}): " +
                          "não se fecha uma posição antes de ela começar.");
        }

        var doMapa = x.Territorios.Values.ToList();
        switch (m.Tipo)
        {
            case TipoMudancaTerritorial.MoverTerritorio:
                if (dados.NovoPaiId == territorio.PaiId) { erros.Add($"\"{territorio.Nome}\" já está nesse lugar da árvore."); break; }
                var movido = new Territorio { Id = territorio.Id, MapaId = territorio.MapaId, Nome = territorio.Nome, PaiId = dados.NovoPaiId };
                foreach (var e in RegrasArvoreTerritorial.ValidarPaiParaOperacao(movido, doMapa)) erros.Add(e);
                if (dados.NovoPaiId is { } pai && x.Territorios.TryGetValue(pai, out var novoPai) &&
                    RegrasArvoreTerritorial.InicioDe(novoPai) is { } inicioPai && inicioPai > x.Efeito)
                    erros.Add($"\"{novoPai.Nome}\" só existe a partir de {Data(inicioPai)}, depois do efeito.");
                if (doMapa.Any(o => o.Id != territorio.Id && o.Ativo && o.PaiId == dados.NovoPaiId &&
                                    Comum.TextoBusca.Normalizar(o.Nome) == Comum.TextoBusca.Normalizar(territorio.Nome)))
                    erros.Add($"Já existe outro território ativo chamado \"{territorio.Nome}\" no destino: renomeie um deles antes.");
                break;

            case TipoMudancaTerritorial.EncerrarTerritorio:
                var ativosAbaixo = doMapa.Count(o => o.PaiId == territorio.Id && o.Id != territorio.Id && o.Ativo);
                if (ativosAbaixo > 0)
                    erros.Add($"\"{territorio.Nome}\" tem {ativosAbaixo} território(s) ativo(s) logo abaixo: mova-os ou encerre-os antes (ou na mesma operação, antes desta mudança).");
                break;

            case TipoMudancaTerritorial.ReativarTerritorio:
                if (territorio.Ativo) { erros.Add($"O território \"{territorio.Nome}\" já está ativo."); break; }
                if (territorio.FimEm is { } fim && x.Efeito <= fim)
                    erros.Add($"\"{territorio.Nome}\" existiu até {Data(fim)}: a reativação começa depois disso.");
                if (territorio.PaiId is { } paiId && x.Territorios.TryGetValue(paiId, out var paiT) && !paiT.Ativo)
                    erros.Add($"O território acima, \"{paiT.Nome}\", está encerrado: reative-o primeiro.");
                if (doMapa.Any(o => o.Id != territorio.Id && o.Ativo && o.PaiId == territorio.PaiId &&
                                    Comum.TextoBusca.Normalizar(o.Nome) == Comum.TextoBusca.Normalizar(territorio.Nome)))
                    erros.Add($"Já existe outro território ativo chamado \"{territorio.Nome}\" no mesmo lugar da árvore: renomeie um deles antes.");
                break;
        }
        if (!x.Mapa.Ativo) erros.Add($"O mapa \"{x.Mapa.Nome}\" está desativado: reative-o para alterar a árvore.");
    }

    /// <summary>
    /// O conjunto das mudanças da operação: no máximo uma mudança de estrutura por território e uma mudança de regra por
    /// território; nada de Fixar e Retirar do mesmo cliente e território; no mapa exclusivo, um Fixar por cliente.
    /// </summary>
    public static List<string> ValidarConjunto(IReadOnlyCollection<OperacaoTerritorialMudanca> mudancas, bool exclusivo)
    {
        var erros = new List<string>();
        if (mudancas.Where(m => Estrutural(m.Tipo)).GroupBy(m => m.TerritorioId).Any(g => g.Count() > 1))
            erros.Add("Um território aparece em duas mudanças de estrutura na mesma operação: deixe só uma.");
        if (mudancas.Where(m => m.Tipo is TipoMudancaTerritorial.NovaVersaoRegra or TipoMudancaTerritorial.EncerrarRegra)
                .GroupBy(m => m.TerritorioId).Any(g => g.Count() > 1))
            erros.Add("Um território aparece em duas mudanças de regra na mesma operação: deixe só uma.");
        var excecoes = mudancas.Where(m => m.Tipo is TipoMudancaTerritorial.Fixar or TipoMudancaTerritorial.Retirar).ToList();
        if (excecoes.GroupBy(m => (m.PessoaId, m.TerritorioId)).Any(g => g.Count() > 1))
            erros.Add("O mesmo cliente aparece duas vezes no mesmo território (Fixar e Retirar, ou repetido): deixe só uma.");
        if (exclusivo && excecoes.Where(m => m.Tipo == TipoMudancaTerritorial.Fixar).GroupBy(m => m.PessoaId).Any(g => g.Count() > 1))
            erros.Add("No mapa exclusivo, um cliente só pode ser fixado em um território: as duas fixações entrariam em conflito.");
        if (mudancas.Where(m => m.Tipo == TipoMudancaTerritorial.EncerrarExcecao).GroupBy(m => m.ExcecaoBaseId).Any(g => g.Count() > 1))
            erros.Add("A mesma exceção aparece duas vezes para encerrar.");
        return erros;
    }

    /// <summary>
    /// Todas as mudanças da operação, na ordem: cada uma conferida contra a árvore já com as mudanças de estrutura
    /// anteriores aplicadas numa cópia (A abaixo de B e depois B abaixo de A é barrado; encerrar os filhos antes do pai na
    /// mesma operação é aceito). Devolve os erros com o número da mudança.
    /// </summary>
    public static List<string> ValidarTodas(IReadOnlyList<OperacaoTerritorialMudanca> mudancas, ContextoMudanca x)
    {
        var erros = ValidarConjunto(mudancas, x.Mapa.Exclusivo);
        var arvore = x.Territorios.ToDictionary(kv => kv.Key, kv => Copia(kv.Value));
        foreach (var m in mudancas.OrderBy(m => m.Ordem))
        {
            var contexto = x with { Territorios = arvore };
            foreach (var e in ValidarMudanca(m, contexto)) erros.Add($"Mudança {m.Ordem}: {e}");
            if (Estrutural(m.Tipo) && m.TerritorioId is { } id && arvore.TryGetValue(id, out var t))
            {
                var dados = DadosMudancaTerritorial.DeJson(m.Depois);
                switch (m.Tipo)
                {
                    case TipoMudancaTerritorial.MoverTerritorio: t.PaiId = dados.NovoPaiId; break;
                    case TipoMudancaTerritorial.EncerrarTerritorio: t.Situacao = SituacaoTerritorio.Encerrado; t.FimEm = x.Efeito.AddDays(-1); break;
                    case TipoMudancaTerritorial.ReativarTerritorio: t.Situacao = SituacaoTerritorio.Ativo; t.FimEm = null; break;
                }
            }
        }
        return erros.Distinct().ToList();
    }

    /// <summary>Cópia rasa para a conferência em sequência (as posições são só lidas).</summary>
    private static Territorio Copia(Territorio t) => new()
    {
        Id = t.Id, MapaId = t.MapaId, Codigo = t.Codigo, Nome = t.Nome, TipoId = t.TipoId, PaiId = t.PaiId, Situacao = t.Situacao,
        FimEm = t.FimEm, Posicoes = t.Posicoes, Responsaveis = t.Responsaveis, Versao = t.Versao
    };
}
