using System.Globalization;
using Lone.Application.Empresas;
using Lone.Application.Pessoas;
using Lone.Application.Seguranca;
using Lone.Contracts.Comercial;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comercial;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Application.Comercial;

public interface ITransferenciaCarteiraAppService
{
    /// <summary>O que aconteceria, cliente a cliente, com as mesmas regras da gravação. Nada é gravado.</summary>
    Task<PreviaTransferenciaDto> PreviaAsync(TransferenciaRequisicao requisicao, CancellationToken ct = default);

    /// <summary>Grava a transferência e cada cliente na própria transação; devolve o resultado por cliente.</summary>
    Task<TransferenciaDto> TransferirAsync(TransferenciaRequisicao requisicao, CancellationToken ct = default);

    Task<List<TransferenciaDto>> ListarAsync(CancellationToken ct = default);

    Task<TransferenciaDto?> ObterAsync(Guid id, CancellationToken ct = default);

    /// <summary>Pessoas, papéis, empresas e o limite de dias no passado, para o assistente.</summary>
    Task<TransferenciaOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default);
}

/// <summary>
/// Transferência de carteira (Motor Comercial, Fase 1d): permissão → pedido (RegrasTransferencia.ValidarPedido) → clientes
/// alcançados e divisão entre os destinos → por cliente, o plano, as mesmas regras da ficha (RegrasComercial) e a gravação
/// protegida da pessoa (rowversion + gatilho da carteira). Cada cliente vale por si: um erro não desfaz os outros. Só as
/// regras comerciais são conferidas (não a ficha inteira): uma pendência de cadastro não trava a transferência.
/// </summary>
public sealed class TransferenciaCarteiraAppService : ITransferenciaCarteiraAppService
{
    /// <summary>Quantas transferências a lista traz (as mais recentes).</summary>
    public const int LimiteLista = 200;

    private readonly ITransferenciaCarteiraRepositorio _repositorio;
    private readonly IPessoaRepositorio _pessoas;
    private readonly ReferenciasComercial _comercial;
    private readonly IComercialConsultas _consultas;
    private readonly IParametrosComerciaisRepositorio _parametros;
    private readonly ICoberturaRepositorio _coberturas;
    private readonly IEmpresaConsultas _empresas;
    private readonly IAutorizacao _autorizacao;
    private readonly IMotivoDaOperacao _motivo;
    private readonly IUsuarioAtual _usuario;
    private readonly TimeProvider _relogio;
    private readonly IEscopoPessoas _escopo;

    public TransferenciaCarteiraAppService(ITransferenciaCarteiraRepositorio repositorio, IPessoaRepositorio pessoas, ReferenciasComercial comercial,
                                           IComercialConsultas consultas, IParametrosComerciaisRepositorio parametros, ICoberturaRepositorio coberturas,
                                           IEmpresaConsultas empresas, IAutorizacao autorizacao, IMotivoDaOperacao motivo, IUsuarioAtual usuario,
                                           TimeProvider relogio, IEscopoPessoas escopo)
    {
        _escopo = escopo;
        _repositorio = repositorio;
        _pessoas = pessoas;
        _comercial = comercial;
        _consultas = consultas;
        _parametros = parametros;
        _coberturas = coberturas;
        _empresas = empresas;
        _autorizacao = autorizacao;
        _motivo = motivo;
        _usuario = usuario;
        _relogio = relogio;
    }

    private DateOnly Hoje => DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime);

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>O pedido conferido e o que ele alcança, igual na prévia e na gravação.</summary>
    private sealed record Preparo(
        FiltroTransferencia Filtro,
        IReadOnlyList<Guid> Destinos,
        IReadOnlyList<ClienteDaOrigem> Clientes,
        IReadOnlyDictionary<Guid, Guid> DestinoDoCliente,
        IReadOnlyDictionary<Guid, TipoCarteira> Tipos,
        Dictionary<Guid, string> Nomes,
        List<string> Avisos,
        string Motivo,
        string? Observacao,
        DateOnly Hoje);

    // ---------------------------------------------------------------- Prévia e gravação

    public async Task<PreviaTransferenciaDto> PreviaAsync(TransferenciaRequisicao requisicao, CancellationToken ct = default)
    {
        var p = await PrepararAsync(requisicao, ct);
        var itens = new List<TransferenciaCarteiraItem>();
        await ProcessarAsync(p, Guid.Empty, numero: string.Empty, gravar: false, itens, ct);

        var (transferir, _, _) = RegrasTransferencia.Contar(itens);
        await CompletarNomesAsync(p.Nomes, itens, ct);
        var dtos = ParaDtos(itens, p.Nomes, p.Tipos);
        return new PreviaTransferenciaDto
        {
            Transferir = transferir,
            NaoProcessar = itens.Select(i => i.ClienteId).Distinct().Count() - transferir,
            PorDestino = [.. itens.Where(i => i.Resultado == ResultadoItemTransferencia.Transferido && i.DestinoId is not null)
                .GroupBy(i => i.DestinoId!.Value)
                .Select(g => new DestinoTransferenciaDto(g.Key, p.Nomes.GetValueOrDefault(g.Key, "?"), g.Select(i => i.ClienteId).Distinct().Count()))
                .OrderBy(d => d.Destino, StringComparer.CurrentCultureIgnoreCase)],
            Avisos = p.Avisos,
            Itens = dtos
        };
    }

    public async Task<TransferenciaDto> TransferirAsync(TransferenciaRequisicao requisicao, CancellationToken ct = default)
    {
        var p = await PrepararAsync(requisicao, ct);
        var ano = p.Hoje.Year;
        var transferencia = new TransferenciaCarteira
        {
            Id = IdSequencial.Novo(),
            Ano = ano,
            Sequencia = await _repositorio.ProximaSequenciaAsync(ano, ct),
            EfeitoEm = p.Filtro.EfeitoEm,
            OrigemId = p.Filtro.OrigemId,
            TipoCarteiraId = p.Filtro.TipoCarteiraId,
            EmpresaId = p.Filtro.EmpresaId,
            Motivo = p.Motivo,
            Observacao = p.Observacao,
            Usuario = Cortar(_usuario.Nome, TransferenciaCarteira.TamanhoMaximoUsuario) ?? string.Empty
        };
        var papel = p.Filtro.TipoCarteiraId is { } papelId && p.Tipos.TryGetValue(papelId, out var t) ? t.Nome : "todos os papéis";
        transferencia.RegistrarEvento(
            $"Transferência {transferencia.Numero}: clientes de {p.Nomes.GetValueOrDefault(p.Filtro.OrigemId, "?")} ({papel}) para " +
            $"{string.Join(", ", p.Destinos.Select(d => p.Nomes.GetValueOrDefault(d, "?")))} a partir de {Data(p.Filtro.EfeitoEm)}. Motivo: {p.Motivo}");
        // O número e o motivo vão para a coluna Motivo da auditoria (da transferência e de cada cliente gravado).
        _motivo.Motivo = Cortar($"{transferencia.Numero}: {p.Motivo}", TransferenciaCarteira.TamanhoMaximoTexto);
        await _repositorio.IncluirAsync(transferencia, ct);

        var itens = new List<TransferenciaCarteiraItem>();
        var completa = false;
        try
        {
            await ProcessarAsync(p, transferencia.Id, transferencia.Numero, gravar: true, itens, ct);
            completa = true;
        }
        finally
        {
            // Mesmo interrompida (erro inesperado, requisição cancelada), o que já foi gravado fica registrado.
            foreach (var item in itens) item.TransferenciaId = transferencia.Id;
            (transferencia.Transferidos, transferencia.NaoProcessados, transferencia.Erros) = RegrasTransferencia.Contar(itens);
            transferencia.Concluida = completa;
            transferencia.RegistrarEvento(completa
                ? $"Transferência {transferencia.Numero} concluída: {transferencia.Transferidos} cliente(s) transferido(s), " +
                  $"{transferencia.NaoProcessados} não processado(s), {transferencia.Erros} com erro."
                : $"Transferência {transferencia.Numero} interrompida depois de {itens.Select(i => i.ClienteId).Distinct().Count()} cliente(s).");
            await _repositorio.ConcluirAsync(transferencia, itens, CancellationToken.None);
        }
        // Relê sem o filtro de hoje: quem gravou passou pela liderança na data de efeito (E12), que pode não ser a de hoje.
        var gravada = await _repositorio.ObterAsync(transferencia.Id, ct) ?? throw new ConflitoDeEdicaoException();
        return await MontarAsync(gravada, await _repositorio.ItensAsync(transferencia.Id, ct), ct);
    }

    /// <summary>
    /// Cliente a cliente: lê, planeja, aplica e confere com as regras da ficha; na gravação, registra a frase no histórico
    /// e grava a pessoa (a versão dela é conferida). Um item por vínculo alcançado.
    /// </summary>
    private async Task ProcessarAsync(Preparo p, Guid transferenciaId, string numero, bool gravar, List<TransferenciaCarteiraItem> itens,
                                      CancellationToken ct)
    {
        foreach (var cliente in p.Clientes)
        {
            ct.ThrowIfCancellationRequested();
            var destino = p.DestinoDoCliente[cliente.Id];
            var pessoa = await _pessoas.ObterAsync(cliente.Id, ct);
            if (pessoa is null)
            {
                itens.Add(Item(cliente.Id, null, null, destino, null, ResultadoItemTransferencia.Erro, "Este cadastro não existe mais."));
                continue;
            }
            if (pessoa.Situacao == SituacaoPessoa.Arquivado)
            {
                foreach (var v in RegrasTransferencia.Candidatos(pessoa.Carteira, p.Filtro))
                    itens.Add(Item(cliente.Id, v, v.FimEm, destino, null, ResultadoItemTransferencia.NaoProcessado, "Cadastro arquivado é somente leitura."));
                continue;
            }

            var anterior = RegrasTransferencia.ComoEstava(pessoa);
            var plano = RegrasTransferencia.Planejar(pessoa, p.Filtro, destino, transferenciaId, p.Tipos, IdSequencial.Novo);
            foreach (var m in plano.Manter)
                itens.Add(Item(cliente.Id, m.Origem, m.Origem.FimEm, destino, null, ResultadoItemTransferencia.NaoProcessado, m.Motivo));
            if (plano.Transferir.Count == 0) continue;

            var fimAntes = plano.Transferir.ToDictionary(x => x.Origem.Id, x => x.Origem.FimEm);
            RegrasTransferencia.Aplicar(pessoa, plano);

            var erros = new List<string>();
            erros.AddRange(RegrasComercial.Validar(pessoa, p.Tipos, anterior));
            erros.AddRange(RegrasComercial.ValidarHistorico(pessoa, anterior, p.Tipos, p.Hoje));
            erros.AddRange(RegrasComercial.ValidarCarteira(pessoa, anterior, p.Tipos));
            erros.AddRange(await _comercial.ValidarAsync(pessoa, anterior, p.Tipos, ct));
            if (erros.Count > 0)
            {
                var motivo = string.Join(" ", erros.Distinct());
                foreach (var x in plano.Transferir)
                    itens.Add(Item(cliente.Id, x.Origem, fimAntes[x.Origem.Id], destino, null, ResultadoItemTransferencia.NaoProcessado, motivo));
                continue;
            }

            if (gravar)
            {
                RegrasComercial.AtualizarVendedorPadrao(pessoa, p.Tipos, p.Hoje);
                foreach (var frase in RegrasTransferencia.Frases(plano, p.Tipos, id => p.Nomes.GetValueOrDefault(id, "?"), numero, p.Motivo))
                    pessoa.RegistrarEvento(frase);
                string? falha = null;
                try
                {
                    await _pessoas.SalvarAsync(pessoa, nova: false, OrigemAlteracao.Usuario, ct);
                }
                catch (ConflitoDeEdicaoException ex)
                {
                    falha = ex.Message;
                }
                catch (ValidacaoException ex)
                {
                    falha = string.Join(" ", ex.Erros);
                }
                if (falha is not null)
                {
                    foreach (var x in plano.Transferir)
                        itens.Add(Item(cliente.Id, x.Origem, fimAntes[x.Origem.Id], destino, null, ResultadoItemTransferencia.Erro, falha));
                    continue;
                }
            }
            foreach (var x in plano.Transferir)
                itens.Add(Item(cliente.Id, x.Origem, fimAntes[x.Origem.Id], destino, x.Novo, ResultadoItemTransferencia.Transferido, null));
        }
    }

    private static TransferenciaCarteiraItem Item(Guid cliente, CarteiraCliente? origem, DateOnly? fimOrigem, Guid destino, CarteiraCliente? novo,
                                                  ResultadoItemTransferencia resultado, string? motivo) => new()
    {
        Id = IdSequencial.Novo(),
        ClienteId = cliente,
        VinculoOrigemId = origem?.Id,
        VinculoNovoId = novo?.Id,
        DestinoId = destino,
        TipoCarteiraId = origem?.TipoCarteiraId,
        EmpresaId = origem?.EmpresaId,
        InicioOrigem = origem?.InicioEm,
        FimOrigem = fimOrigem,
        Resultado = resultado,
        Motivo = Cortar(motivo, TransferenciaCarteiraItem.TamanhoMaximoMotivo)
    };

    private static string? Cortar(string? texto, int maximo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim() is var t && t.Length > maximo ? t[..(maximo - 1)] + "…" : texto.Trim();

    // ---------------------------------------------------------------- Pedido

    private async Task<Preparo> PrepararAsync(TransferenciaRequisicao r, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Comercial.Transferir);
        var hoje = Hoje;
        var destinos = (r.Destinos ?? new List<Guid>()).ToList();
        var filtro = new FiltroTransferencia(r.OrigemId,
            r.TipoCarteiraId is { } papel && papel != Guid.Empty ? papel : null,
            r.EmpresaId is { } empresa && empresa != Guid.Empty ? empresa : null,
            r.EfeitoEm);
        var parametros = await _parametros.ObterAsync(ct);
        var erros = RegrasTransferencia.ValidarPedido(filtro, destinos, r.Motivo, r.Observacao, hoje, parametros.DiasRetroativosMaximo);
        var tipos = await _comercial.TiposAsync(ct);
        if (filtro.TipoCarteiraId is { } papelId && !tipos.ContainsKey(papelId)) erros.Add("O papel comercial escolhido não existe.");
        if (erros.Count > 0) throw new ValidacaoException(erros);

        // Destinos: pessoas ativas. Quem pode ocupar cada papel é conferido por cliente, pela mesma regra da ficha.
        var elegiveis = await _consultas.PessoasElegiveisAsync(destinos, ct);
        if (destinos.Any(d => !elegiveis.ContainsKey(d)))
            throw new ValidacaoException(["Um destino escolhido não existe mais ou não está ativo."]);

        // E12 (Fase 2a-3): origem e cada destino dentro do alcance, pela liderança na data de efeito. Sempre por esta
        // operação (o histórico fica), nunca editando o vínculo; destino fora do alcance bloqueia tudo.
        // A origem vale também pela véspera: no desligamento, quem sai deixa a equipe no dia anterior ao efeito.
        if (!await _escopo.GerenciaEmAsync(filtro.OrigemId, filtro.EfeitoEm, ct) &&
            !await _escopo.GerenciaEmAsync(filtro.OrigemId, filtro.EfeitoEm.AddDays(-1), ct))
            throw new ValidacaoException(["A origem da transferência não está no seu alcance (sua equipe) na data de efeito."]);
        foreach (var destino in destinos.Where(d => d != filtro.OrigemId).Distinct())
            if (!await _escopo.GerenciaEmAsync(destino, filtro.EfeitoEm, ct))
                throw new ValidacaoException([$"O destino {elegiveis.GetValueOrDefault(destino)?.Nome ?? "escolhido"} não está no seu alcance (sua equipe) na data de efeito: a transferência não foi feita."]);

        var clientes = await _repositorio.ClientesDaOrigemAsync(filtro, ct);
        if (r.Clientes is { } marcados)
        {
            var escolhidos = marcados.ToHashSet();
            clientes = [.. clientes.Where(c => escolhidos.Contains(c.Id))];
        }
        if (clientes.Count == 0)
            throw new ValidacaoException([$"Nenhum cliente a transferir: a origem não tem vínculo ativo nesse papel e empresa a partir de {Data(filtro.EfeitoEm)}."]);
        if (clientes.Count > RegrasTransferencia.MaximoClientes)
            throw new ValidacaoException([$"São {clientes.Count} clientes; transfira no máximo {RegrasTransferencia.MaximoClientes} por vez " +
                                          "(escolha um papel ou uma empresa, ou marque os clientes na prévia)."]);

        var cargas = destinos.Count > 1
            ? await _repositorio.CargasAsync(destinos, filtro.TipoCarteiraId, filtro.EfeitoEm, ct)
            : new Dictionary<Guid, int>();
        var divisao = RegrasTransferencia.DividirPelaMenorCarteira([.. clientes.Select(c => c.Id)], destinos, cargas);
        if (r.DestinoPorCliente is { } trocados)
            foreach (var (clienteId, destino) in trocados)
            {
                if (!divisao.ContainsKey(clienteId)) continue;
                if (!destinos.Contains(destino))
                    throw new ValidacaoException(["O destino escolhido para um cliente não está entre os destinos da transferência."]);
                divisao[clienteId] = destino;
            }

        var ids = new List<Guid>(destinos) { filtro.OrigemId };
        if (filtro.EmpresaId is { } empresaId) ids.Add(empresaId);
        var nomes = await _consultas.NomesAsync(ids, ct);
        foreach (var c in clientes) nomes[c.Id] = c.Nome;

        var avisos = new List<string>();
        if (RegrasTransferencia.AvisoRetroativo(filtro.EfeitoEm, hoje) is { } retroativo) avisos.Add(retroativo);
        var origem = nomes.GetValueOrDefault(filtro.OrigemId, "A origem");
        foreach (var c in (await _coberturas.DoTitularAsync(filtro.OrigemId, ct))
                     .Where(c => !c.Cancelada && c.FimEm >= filtro.EfeitoEm).OrderBy(c => c.InicioEm))
            avisos.Add($"{origem} tem ausência cadastrada de {Data(c.InicioEm)} a {Data(c.FimEm)}. Ela continua valendo para os clientes que " +
                       "ficarem com essa pessoa; se não fizer mais sentido, encerre ou cancele em Ausências e coberturas.");

        return new Preparo(filtro, destinos, clientes, divisao, tipos, nomes, avisos, r.Motivo!.Trim(),
            string.IsNullOrWhiteSpace(r.Observacao) ? null : r.Observacao.Trim(), hoje);
    }

    // ---------------------------------------------------------------- Consulta

    public async Task<TransferenciaOpcoesDto> ListarOpcoesAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Comercial.Transferir);
        var papeis = (await _comercial.TiposAsync(ct)).Values.OrderBy(p => p.Ordem).ThenBy(p => p.Nome).ToList();
        var pessoas = await _consultas.ListarAtendentesAsync([.. papeis.Where(p => p.Ativo).SelectMany(p => p.ClassificacoesAceitas).Distinct()], ct);
        return new TransferenciaOpcoesDto
        {
            Pessoas = pessoas,
            ClientesHoje = await _repositorio.CargasAsync([.. pessoas.Select(p => p.Id)], null, Hoje, ct),
            Papeis = [.. papeis.Select(p => TipoCarteiraAppService.ParaDto(p, 0))],
            Empresas = await _empresas.ListarEmpresasAsync(ct),
            DiasRetroativosMaximo = (await _parametros.ObterAsync(ct)).DiasRetroativosMaximo
        };
    }

    public async Task<List<TransferenciaDto>> ListarAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Comercial.Transferir);
        var lista = await _repositorio.ListarAsync(LimiteLista, ct);
        var escopo = await _escopo.ObterAsync(ct);
        if (!escopo.Tudo && lista.Count > 0)
        {
            // Só as que envolvem alguém do alcance (origem ou algum destino); alcance de hoje (E10).
            var destinos = await _repositorio.DestinosAsync([.. lista.Select(t => t.Id)], ct);
            lista = [.. lista.Where(t => escopo.AlcancaPessoa(t.OrigemId) ||
                                         destinos.GetValueOrDefault(t.Id, []).Any(escopo.AlcancaPessoa))];
        }
        var tipos = await _comercial.TiposAsync(ct);
        var nomes = await _consultas.NomesAsync(
            [.. lista.Select(t => t.OrigemId).Concat(lista.Select(t => t.EmpresaId).OfType<Guid>()).Distinct()], ct);
        return [.. lista.Select(t => ParaDto(t, nomes, tipos))];
    }

    public async Task<TransferenciaDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Comercial.Transferir);
        if (await _repositorio.ObterAsync(id, ct) is not { } t) return null;
        var itens = await _repositorio.ItensAsync(id, ct);
        var escopo = await _escopo.ObterAsync(ct);
        if (!escopo.AlcancaPessoa(t.OrigemId) && !itens.Any(i => i.DestinoId is { } d && escopo.AlcancaPessoa(d)))
            return null; // fora do alcance: como se não existisse
        return await MontarAsync(t, itens, ct);
    }

    /// <summary>A transferência com os itens para a tela (sem conferir o alcance: quem chama já conferiu).</summary>
    private async Task<TransferenciaDto> MontarAsync(TransferenciaCarteira t, List<TransferenciaCarteiraItem> itens, CancellationToken ct)
    {
        var tipos = await _comercial.TiposAsync(ct);
        var nomes = await _consultas.NomesAsync(
            [.. new[] { t.OrigemId }.Concat(new[] { t.EmpresaId }.OfType<Guid>()).Concat(itens.Select(i => i.ClienteId)).Distinct()], ct);
        await CompletarNomesAsync(nomes, itens, ct);
        var dto = ParaDto(t, nomes, tipos);
        dto.Itens = ParaDtos(itens, nomes, tipos);
        return dto;
    }

    private static TransferenciaDto ParaDto(TransferenciaCarteira t, IReadOnlyDictionary<Guid, string> nomes, IReadOnlyDictionary<Guid, TipoCarteira> tipos) => new()
    {
        Id = t.Id,
        Numero = t.Numero,
        EfeitoEm = t.EfeitoEm,
        OrigemId = t.OrigemId,
        Origem = nomes.GetValueOrDefault(t.OrigemId),
        TipoCarteiraId = t.TipoCarteiraId,
        Papel = t.TipoCarteiraId is { } papel && tipos.TryGetValue(papel, out var tipo) ? tipo.Nome : null,
        EmpresaId = t.EmpresaId,
        Empresa = t.EmpresaId is { } empresa ? nomes.GetValueOrDefault(empresa) : null,
        Motivo = t.Motivo,
        Observacao = t.Observacao,
        Usuario = t.Usuario,
        CriadaEm = t.CriadoEm,
        Transferidos = t.Transferidos,
        NaoProcessados = t.NaoProcessados,
        Erros = t.Erros,
        Concluida = t.Concluida
    };

    /// <summary>Nomes que faltam (destinos e empresas dos itens), numa consulta só.</summary>
    private async Task CompletarNomesAsync(Dictionary<Guid, string> nomes, IReadOnlyList<TransferenciaCarteiraItem> itens, CancellationToken ct)
    {
        var faltam = itens.SelectMany(i => new[] { i.ClienteId, i.DestinoId ?? Guid.Empty, i.EmpresaId ?? Guid.Empty })
            .Where(id => id != Guid.Empty && !nomes.ContainsKey(id)).Distinct().ToList();
        if (faltam.Count == 0) return;
        foreach (var (id, nome) in await _consultas.NomesAsync(faltam, ct)) nomes[id] = nome;
    }

    private static List<ItemTransferenciaDto> ParaDtos(IReadOnlyList<TransferenciaCarteiraItem> itens, IReadOnlyDictionary<Guid, string> nomes,
                                                        IReadOnlyDictionary<Guid, TipoCarteira> tipos) =>
        [.. itens.Select(i => new ItemTransferenciaDto
            {
                ClienteId = i.ClienteId,
                Cliente = nomes.GetValueOrDefault(i.ClienteId, "?"),
                TipoCarteiraId = i.TipoCarteiraId,
                Papel = i.TipoCarteiraId is { } papel && tipos.TryGetValue(papel, out var tipo) ? tipo.Nome : null,
                Empresa = i.EmpresaId is { } empresa ? nomes.GetValueOrDefault(empresa) : null,
                DestinoId = i.DestinoId,
                Destino = i.DestinoId is { } destino ? nomes.GetValueOrDefault(destino) : null,
                InicioOrigem = i.InicioOrigem,
                FimOrigem = i.FimOrigem,
                Resultado = i.Resultado,
                Motivo = i.Motivo
            })
            .OrderBy(i => i.Resultado).ThenBy(i => i.Cliente, StringComparer.CurrentCultureIgnoreCase)];
}
