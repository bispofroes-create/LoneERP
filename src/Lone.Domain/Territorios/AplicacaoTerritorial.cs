using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Domain.Territorios;

/// <summary>Uma linha de fato a fechar (fim = véspera do efeito) ou anular (começava no próprio efeito), com o fim anterior.</summary>
public sealed record FechamentoPlanejado(TabelaFechamentoTerritorial Tabela, Guid LinhaId, DateOnly? FimAnterior, DateOnly? NovoFim, bool Anular);

/// <summary>A nova situação de um território depois de uma mudança estrutural (DN-01: efeito até hoje, então vale "hoje").</summary>
public sealed record TerritorioAlterado(Guid TerritorioId, Guid? PaiId, SituacaoTerritorio Situacao, DateOnly? FimEm, string Evento);

/// <summary>
/// Tudo que a aplicação grava, em forma pura (seção J): primeiro os fechamentos e anulações de cada tabela, depois as
/// linhas novas (a ordem fecha → anula → abre evita que um gatilho veja um estado intermediário sobreposto).
/// </summary>
public sealed class PlanoAplicacaoTerritorial
{
    public List<FechamentoPlanejado> Fechamentos { get; } = [];
    public List<RegraTerritorio> RegrasNovas { get; } = [];
    public List<ExcecaoTerritorio> ExcecoesNovas { get; } = [];
    public List<TerritorioPosicao> PosicoesNovas { get; } = [];
    public List<TerritorioAlterado> Territorios { get; } = [];
    public List<AtribuicaoTerritorio> AtribuicoesNovas { get; } = [];
    public List<OperacaoTerritorialItem> Itens { get; } = [];

    /// <summary>Territórios cujo uso operacional muda (ganham ou perdem regra/atribuição): a versão deles é trocada (seção J, passo 7).</summary>
    public HashSet<Guid> TerritoriosTocados { get; } = [];
}

/// <summary>
/// Monta o plano de gravação de uma operação a partir do estado oficial em D e do resultado re-simulado. Não acessa banco.
/// Regra, exceção e posição novas usam o id da própria mudança (o mesmo que a simulação usou), então a assinatura da
/// simulação e a da aplicação coincidem.
/// </summary>
public static class AplicacaoTerritorial
{
    public static PlanoAplicacaoTerritorial Planejar(OperacaoTerritorial op, EstadoTerritorialEmD e, ResultadoSimulacaoTerritorial resultado,
                                                     IReadOnlyDictionary<Guid, int> ultimaVersaoDaRegra,
                                                     IReadOnlyList<TerritorioResponsavel> responsaveisVigentes,
                                                     Func<Guid, string?>? explicacaoAtributos = null)
    {
        var d = op.EfeitoEm;
        var vespera = d.AddDays(-1);
        var plano = new PlanoAplicacaoTerritorial();

        FechamentoPlanejado Fechar(TabelaFechamentoTerritorial tabela, Guid id, DateOnly inicio, DateOnly? fim) =>
            inicio >= d ? new(tabela, id, fim, fim, Anular: true) : new(tabela, id, fim, vespera, Anular: false);

        var regrasVigentes = e.RegrasVigentes.Where(r => r.VigenteEm(d)).ToDictionary(r => r.TerritorioId);
        var mudancas = op.Mudancas.OrderBy(m => m.Ordem).ToList();
        var territoriosEncerrados = new HashSet<Guid>();

        foreach (var m in mudancas)
        {
            var dados = DadosMudancaTerritorial.DeJson(m.Depois);
            switch (m.Tipo)
            {
                case TipoMudancaTerritorial.NovaVersaoRegra when m.TerritorioId is { } t:
                    if (regrasVigentes.TryGetValue(t, out var atual))
                        plano.Fechamentos.Add(Fechar(TabelaFechamentoTerritorial.Regra, atual.Id, atual.InicioEm, atual.FimEm));
                    plano.RegrasNovas.Add(new RegraTerritorio
                    {
                        Id = m.Id, MapaId = op.MapaId, TerritorioId = t, Numero = ultimaVersaoDaRegra.GetValueOrDefault(t) + 1,
                        Grupos = dados.Grupos ?? string.Empty, Criterios = dados.Criterios ?? string.Empty, Prioridade = dados.Prioridade,
                        InicioEm = d, OperacaoId = op.Id, OperacaoMudancaId = m.Id
                    });
                    plano.TerritoriosTocados.Add(t);
                    break;

                case TipoMudancaTerritorial.EncerrarRegra when m.TerritorioId is { } t && regrasVigentes.TryGetValue(t, out var vigente):
                    plano.Fechamentos.Add(Fechar(TabelaFechamentoTerritorial.Regra, vigente.Id, vigente.InicioEm, vigente.FimEm));
                    plano.TerritoriosTocados.Add(t);
                    break;

                case TipoMudancaTerritorial.Fixar or TipoMudancaTerritorial.Retirar when m.TerritorioId is { } t && m.PessoaId is { } p:
                    plano.ExcecoesNovas.Add(new ExcecaoTerritorio
                    {
                        Id = m.Id, MapaId = op.MapaId, Exclusivo = e.Mapa.Exclusivo, TerritorioId = t, PessoaId = p,
                        Tipo = m.Tipo == TipoMudancaTerritorial.Fixar ? TipoExcecaoTerritorio.Fixar : TipoExcecaoTerritorio.Retirar,
                        InicioEm = d, FimEm = dados.FimEm, Motivo = (dados.Motivo ?? string.Empty).Trim(), Origem = OrigemExcecaoTerritorio.Manual,
                        OperacaoId = op.Id, OperacaoMudancaId = m.Id
                    });
                    break;

                case TipoMudancaTerritorial.EncerrarExcecao when m.ExcecaoBaseId is { } eid:
                    if (e.ExcecoesVigentes.FirstOrDefault(x => x.Id == eid) is { } excecao)
                        plano.Fechamentos.Add(Fechar(TabelaFechamentoTerritorial.Excecao, excecao.Id, excecao.InicioEm, excecao.FimEm));
                    break;

                case TipoMudancaTerritorial.MoverTerritorio when m.TerritorioId is { } t && e.Territorios.TryGetValue(t, out var territorio):
                    if (RegrasArvoreTerritorial.PosicaoAberta(territorio) is { FimEm: null } aberta)
                        plano.Fechamentos.Add(Fechar(TabelaFechamentoTerritorial.Posicao, aberta.Id, aberta.InicioEm, aberta.FimEm));
                    plano.PosicoesNovas.Add(NovaPosicao(op, m, territorio, dados.NovoPaiId));
                    plano.Territorios.Add(new TerritorioAlterado(t, dados.NovoPaiId, SituacaoTerritorio.Ativo, null,
                        $"Território '{territorio.Nome}' movido pela operação {op.Numero} (efeito em {RegrasCadastroTerritorial.Data(d)})."));
                    plano.TerritoriosTocados.Add(t);
                    break;

                case TipoMudancaTerritorial.EncerrarTerritorio when m.TerritorioId is { } t && e.Territorios.TryGetValue(t, out var territorio):
                    territoriosEncerrados.Add(t);
                    if (RegrasArvoreTerritorial.PosicaoAberta(territorio) is { FimEm: null } posicao)
                        plano.Fechamentos.Add(Fechar(TabelaFechamentoTerritorial.Posicao, posicao.Id, posicao.InicioEm, posicao.FimEm));
                    if (regrasVigentes.TryGetValue(t, out var regraDoEncerrado) &&
                        !mudancas.Any(o => o.Tipo is TipoMudancaTerritorial.EncerrarRegra && o.TerritorioId == t))
                        plano.Fechamentos.Add(Fechar(TabelaFechamentoTerritorial.Regra, regraDoEncerrado.Id, regraDoEncerrado.InicioEm, regraDoEncerrado.FimEm));
                    foreach (var x in e.ExcecoesVigentes.Where(x => x.TerritorioId == t && x.VigenteEm(d) &&
                                                                    !mudancas.Any(o => o.Tipo == TipoMudancaTerritorial.EncerrarExcecao && o.ExcecaoBaseId == x.Id)))
                        plano.Fechamentos.Add(Fechar(TabelaFechamentoTerritorial.Excecao, x.Id, x.InicioEm, x.FimEm));
                    // Consequência do encerramento (DN-09): quem não começou é anulado; quem vai além termina na véspera.
                    foreach (var r in responsaveisVigentes.Where(r => r.TerritorioId == t && r.Ativo && (r.FimEm is null || r.FimEm >= d)))
                        plano.Fechamentos.Add(Fechar(TabelaFechamentoTerritorial.Responsavel, r.Id, r.InicioEm, r.FimEm));
                    plano.Territorios.Add(new TerritorioAlterado(t, territorio.PaiId, SituacaoTerritorio.Encerrado, vespera,
                        $"Território '{territorio.Nome}' encerrado pela operação {op.Numero} (último dia {RegrasCadastroTerritorial.Data(vespera)})."));
                    plano.TerritoriosTocados.Add(t);
                    break;

                case TipoMudancaTerritorial.ReativarTerritorio when m.TerritorioId is { } t && e.Territorios.TryGetValue(t, out var territorio):
                    plano.PosicoesNovas.Add(NovaPosicao(op, m, territorio, territorio.PaiId));
                    plano.Territorios.Add(new TerritorioAlterado(t, territorio.PaiId, SituacaoTerritorio.Ativo, null,
                        $"Território '{territorio.Nome}' reativado pela operação {op.Numero} (a partir de {RegrasCadastroTerritorial.Data(d)})."));
                    plano.TerritoriosTocados.Add(t);
                    break;
            }
        }

        // Atribuições: só os clientes cujo resultado muda. Inconsistência nunca chega aqui (a aplicação é recusada antes).
        var vigentesPorCliente = e.AtribuicoesVigentes.Where(a => a.VigenteEm(d)).ToLookup(a => a.PessoaId);
        foreach (var a in resultado.Afetados.Where(a => a.Efeito is not (EfeitoNoCliente.Permanece or EfeitoNoCliente.Bloqueado)))
        {
            var atuais = vigentesPorCliente[a.Decisao.PessoaId].ToList();
            var novos = a.Decisao.Territorios.ToDictionary(t => t.TerritorioId);
            var encerrada = (Guid?)null;
            var nova = (Guid?)null;
            foreach (var at in atuais)
            {
                // Fica a linha que continua igual (mesmo território e mesma origem); fecha as outras.
                if (novos.TryGetValue(at.TerritorioId, out var n) && CenarioTerritorial.Atuais([at])[at.TerritorioId] == n) { novos.Remove(at.TerritorioId); continue; }
                plano.Fechamentos.Add(Fechar(TabelaFechamentoTerritorial.Atribuicao, at.Id, at.InicioEm, at.FimEm));
                encerrada ??= at.Id;
                plano.TerritoriosTocados.Add(at.TerritorioId);
            }
            foreach (var n in novos.Values.OrderBy(n => n.TerritorioId))
            {
                var linha = new AtribuicaoTerritorio
                {
                    Id = IdSequencial.Novo(), MapaId = op.MapaId, Exclusivo = e.Mapa.Exclusivo, TerritorioId = n.TerritorioId,
                    PessoaId = a.Decisao.PessoaId, InicioEm = d, Origem = n.Origem, RegraId = n.RegraId, ExcecaoId = n.ExcecaoId,
                    OperacaoId = op.Id
                };
                plano.AtribuicoesNovas.Add(linha);
                nova ??= linha.Id;
                plano.TerritoriosTocados.Add(n.TerritorioId);
            }
            plano.Itens.Add(new OperacaoTerritorialItem
            {
                Id = IdSequencial.Novo(), OperacaoId = op.Id, PessoaId = a.Decisao.PessoaId, Resultado = a.Decisao.Resultado, Efeito = a.Efeito,
                TerritorioAnteriorId = a.Atuais.Keys.Order().Cast<Guid?>().FirstOrDefault(),
                TerritorioNovoId = a.Decisao.Territorios.Select(t => t.TerritorioId).Order().Cast<Guid?>().FirstOrDefault(),
                AtribuicaoEncerradaId = encerrada, AtribuicaoNovaId = nova,
                Explicacao = SimulacaoTerritorial.Explicacao(a, explicacaoAtributos is null ? null : Atributos(explicacaoAtributos(a.Decisao.PessoaId)))
            });
        }
        return plano;
    }

    private static IReadOnlyDictionary<string, string?>? Atributos(string? texto) =>
        texto is null ? null : new Dictionary<string, string?> { ["lidos"] = texto };

    private static TerritorioPosicao NovaPosicao(OperacaoTerritorial op, OperacaoTerritorialMudanca m, Territorio t, Guid? pai) => new()
    {
        Id = m.Id, MapaId = t.MapaId, TerritorioId = t.Id, PaiId = pai, InicioEm = op.EfeitoEm, OperacaoId = op.Id, OperacaoMudancaId = m.Id
    };
}
