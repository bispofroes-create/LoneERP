using Lone.Application.Seguranca;
using Lone.Contracts.Comercial;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comercial;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Application.Comercial;

public interface ICarteiraEmDataAppService
{
    /// <summary>"Como estava a carteira" do cliente ou da pessoa (um dos dois) na data, a partir das vigências gravadas.</summary>
    Task<CarteiraEmDataDto> ConsultarAsync(Guid? clienteId, Guid? pessoaId, DateOnly data, CancellationToken ct = default);
}

/// <summary>
/// Consulta temporal da carteira (Motor Comercial, Fase 1d; princípio 99 do prompt mestre): responde pelo que foi gravado
/// com vigência, sem depender do estado atual. Por cliente: quem ocupava cada papel, o crédito de cada um naquele dia
/// (RegrasComercial.CreditosEmData) e as ausências que valiam; por pessoa: os clientes que ela atendia. Só leitura.
/// </summary>
public sealed class CarteiraEmDataAppService : ICarteiraEmDataAppService
{
    /// <summary>Máximo de vínculos na consulta por pessoa (quem atende milhares de clientes vê os primeiros e um aviso).</summary>
    public const int LimiteVinculos = 2000;

    private readonly ICoberturaConsultas _consultas;
    private readonly ICoberturaRepositorio _coberturas;
    private readonly ITipoAusenciaRepositorio _tiposAusencia;
    private readonly ITransferenciaCarteiraRepositorio _transferencias;
    private readonly ReferenciasComercial _comercial;
    private readonly IComercialConsultas _nomes;
    private readonly IAutorizacao _autorizacao;

    public CarteiraEmDataAppService(ICoberturaConsultas consultas, ICoberturaRepositorio coberturas, ITipoAusenciaRepositorio tiposAusencia,
                                    ITransferenciaCarteiraRepositorio transferencias, ReferenciasComercial comercial, IComercialConsultas nomes,
                                    IAutorizacao autorizacao)
    {
        _consultas = consultas;
        _coberturas = coberturas;
        _tiposAusencia = tiposAusencia;
        _transferencias = transferencias;
        _comercial = comercial;
        _nomes = nomes;
        _autorizacao = autorizacao;
    }

    public async Task<CarteiraEmDataDto> ConsultarAsync(Guid? clienteId, Guid? pessoaId, DateOnly data, CancellationToken ct = default)
    {
        if (!_autorizacao.Possui(Permissoes.Comercial.Coberturas) && !_autorizacao.Possui(Permissoes.Comercial.Transferir))
            _autorizacao.Exigir(Permissoes.Comercial.Visualizar);
        if ((clienteId is null) == (pessoaId is null))
            throw new ValidacaoException(["Escolha um cliente ou uma pessoa que atende (um dos dois)."]);
        if (data == default) throw new ValidacaoException(["Informe a data."]);

        var limite = clienteId is null ? LimiteVinculos + 1 : LimiteVinculos;
        var vinculos = await _consultas.VinculosEmDataAsync(clienteId, pessoaId, data, limite, ct);
        var cortada = vinculos.Count > LimiteVinculos;
        if (cortada) vinculos = [.. vinculos.Take(LimiteVinculos)];

        var tipos = await _comercial.TiposAsync(ct);
        var nomes = await _nomes.NomesAsync(
            [.. vinculos.SelectMany(v => new[] { v.PessoaId, v.VendedorId, v.EmpresaId ?? Guid.Empty })
                .Append(clienteId ?? pessoaId ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct()], ct);
        var numeros = await _transferencias.NumerosAsync([.. vinculos.Select(v => v.TransferenciaId).OfType<Guid>().Distinct()], ct);

        // Coberturas que valiam no dia (não canceladas e cobrindo a data).
        var coberturas = (await _coberturas.ListarAsync(data, incluirEncerradas: false, ct)).Where(c => c.Vigente(data)).ToList();
        var tiposAusencia = coberturas.Count == 0 ? new Dictionary<Guid, string>()
            : (await _tiposAusencia.ListarAsync(ct)).ToDictionary(t => t.Id, t => t.Nome);
        var quemCobre = coberturas.Count == 0 ? new Dictionary<Guid, string>()
            : await _nomes.NomesAsync([.. coberturas.Select(c => c.SubstitutoId).OfType<Guid>().Distinct()], ct);
        string DescreverCobertura(CoberturaComercial c) => RegrasCobertura.Descrever(c, tiposAusencia.GetValueOrDefault(c.TipoAusenciaId, "Ausência"),
            c.SubstitutoId is { } s ? quemCobre.GetValueOrDefault(s, "?") : "a equipe");

        // Crédito do dia: só na consulta por cliente, em que a carteira dele veio inteira (por empresa do vínculo).
        var creditos = new Dictionary<Guid, decimal?>();
        if (clienteId is not null)
            foreach (var empresa in vinculos.Select(v => v.EmpresaId).Distinct())
                foreach (var c in RegrasComercial.CreditosEmData(vinculos, tipos, empresa, data).Where(c => c.Vinculo.EmpresaId == empresa))
                    creditos[c.Vinculo.Id] = c.Percentual;

        var dto = new CarteiraEmDataDto
        {
            Data = data,
            ClienteId = clienteId,
            PessoaId = pessoaId,
            Nome = nomes.GetValueOrDefault(clienteId ?? pessoaId ?? Guid.Empty),
            Cortada = cortada,
            Vinculos = [.. vinculos.Select(v => new VinculoEmDataDto
                {
                    VinculoId = v.Id,
                    ClienteId = v.PessoaId,
                    Cliente = nomes.GetValueOrDefault(v.PessoaId, "?"),
                    TipoCarteiraId = v.TipoCarteiraId,
                    Papel = tipos.TryGetValue(v.TipoCarteiraId, out var t) ? t.Nome : "?",
                    PessoaId = v.VendedorId,
                    Pessoa = nomes.GetValueOrDefault(v.VendedorId, "?"),
                    Empresa = v.EmpresaId is { } e ? nomes.GetValueOrDefault(e) : null,
                    InicioEm = v.InicioEm,
                    FimEm = v.FimEm,
                    Exclusivo = v.Exclusivo,
                    Origem = v.Origem,
                    Transferencia = v.TransferenciaId is { } tr ? numeros.GetValueOrDefault(tr) : null,
                    TipoCredito = tipos.TryGetValue(v.TipoCarteiraId, out var papel) ? papel.TipoCredito : TipoCreditoComercial.Nenhum,
                    Credito = creditos.GetValueOrDefault(v.Id),
                    Cobertura = coberturas.FirstOrDefault(c => c.TitularId == v.VendedorId &&
                                                               (c.TipoCarteiraId is null || c.TipoCarteiraId == v.TipoCarteiraId) &&
                                                               (c.EmpresaId is null || v.EmpresaId is null || c.EmpresaId == v.EmpresaId)) is { } cob
                        ? DescreverCobertura(cob) : null
                })
                .OrderBy(v => clienteId is null ? v.Cliente : v.Papel, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(v => v.InicioEm)]
        };
        if (pessoaId is { } quem)
            dto.Ausencias = [.. coberturas.Where(c => c.TitularId == quem).Select(DescreverCobertura)];
        return dto;
    }
}
