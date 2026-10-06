using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Application.Seguranca;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Seguranca;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.ObjetosDeValor;
using Lone.Domain.Validacao;

namespace Lone.Application.Integracoes;

/// <summary>Consultas externas usadas no cadastro (a API chama os serviços; o aplicativo só chama a API).</summary>
public interface IConsultasAppService
{
    Task<DadosCep?> ConsultarCepAsync(string cep, CancellationToken ct = default);
    Task<DadosCnpj?> ConsultarCnpjAsync(string cnpj, CancellationToken ct = default);

    /// <summary>Conferência do CEP pelo motor (F2). Não grava nada; fonte fora do ar é resultado, não erro.</summary>
    Task<DecisaoCepDto> ConferirCepAsync(ConferirCepRequisicao requisicao, CancellationToken ct = default);

    /// <summary>Busca de CEP pelo endereço sem CEP: candidatos (nenhum é escolhido nem aplicado). Não grava nada.</summary>
    Task<DecisaoCepDto> BuscarCepPorEnderecoAsync(BuscarCepPorEnderecoRequisicao requisicao, CancellationToken ct = default);

    /// <summary>Segunda opinião (Checkpoint G): compara a resposta principal com a da outra fonte. Não grava nada.</summary>
    Task<SegundaOpiniaoCepDto> ConsultarOutraFonteAsync(SegundaOpiniaoCepRequisicao requisicao, CancellationToken ct = default);
}

public sealed class ConsultasAppService : IConsultasAppService
{
    private readonly ICepConsulta _cep;
    private readonly ICnpjConsulta _cnpj;
    private readonly IInscricaoEstadualConsulta _inscricoes;
    private readonly IAutorizacao _autorizacao;
    private readonly IServicoConferenciaCep _conferencia;
    private readonly IHistoricoConsultasCep _historico;
    private readonly TimeProvider _relogio;

    public ConsultasAppService(ICepConsulta cep, ICnpjConsulta cnpj, IInscricaoEstadualConsulta inscricoes, IAutorizacao autorizacao,
                               IServicoConferenciaCep conferencia, IHistoricoConsultasCep? historico = null, TimeProvider? relogio = null)
    {
        _cep = cep;
        _cnpj = cnpj;
        _inscricoes = inscricoes;
        _autorizacao = autorizacao;
        _conferencia = conferencia;
        _historico = historico ?? SemCachePostalCep.Instancia;
        _relogio = relogio ?? TimeProvider.System;
    }

    public async Task<DecisaoCepDto> ConferirCepAsync(ConferirCepRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        ArgumentNullException.ThrowIfNull(requisicao);
        if (!Cep.EhValido(requisicao.Cep))
            throw new ValidacaoException(["O CEP deve ter 8 dígitos."]);
        if (!string.IsNullOrWhiteSpace(requisicao.Uf) && requisicao.Uf.Trim().Length != 2)
            throw new ValidacaoException(["A UF deve ter 2 letras."]);

        var endereco = new EnderecoConferenciaCep(requisicao.Cep, requisicao.Logradouro, requisicao.Numero, requisicao.Bairro,
            requisicao.Cidade, requisicao.Uf?.Trim().ToUpperInvariant(), requisicao.CodigoMunicipioIbge);
        var resultado = await _conferencia.ConferirAsync(endereco, ct);
        var dto = ParaDto(resultado.Decisao, resultado.Avisos);
        if (resultado.Anterior is { } a)
            dto.InformacaoAnterior = new InformacaoAnteriorCepDto
            {
                Fonte = a.Fonte, ConsultadoEm = a.ConsultadoEm, Registro = Candidato(a.Registro),
                Resultado = a.DecisaoComEla.Resultado, Motivos = a.DecisaoComEla.Motivos.ToList()
            };
        return dto;
    }

    public async Task<DecisaoCepDto> BuscarCepPorEnderecoAsync(BuscarCepPorEnderecoRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        ArgumentNullException.ThrowIfNull(requisicao);
        if (!string.IsNullOrWhiteSpace(requisicao.Uf) && requisicao.Uf.Trim().Length != 2)
            throw new ValidacaoException(["A UF deve ter 2 letras."]);

        // Sem CEP: o motor ignora o CEP nesta operação; os dados mínimos são conferidos pelo serviço (BuscaEnderecoCep.De).
        var endereco = new EnderecoConferenciaCep(null, requisicao.Logradouro, requisicao.Numero, requisicao.Bairro,
            requisicao.Cidade, requisicao.Uf?.Trim().ToUpperInvariant(), requisicao.CodigoMunicipioIbge);
        var resultado = await _conferencia.BuscarPorEnderecoAsync(endereco, ct);
        return ParaDto(resultado.Decisao, resultado.Avisos);
    }

    public async Task<SegundaOpiniaoCepDto> ConsultarOutraFonteAsync(SegundaOpiniaoCepRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        ArgumentNullException.ThrowIfNull(requisicao);
        if (!Cep.EhValido(requisicao.Cep))
            throw new ValidacaoException(["O CEP deve ter 8 dígitos."]);
        var c = await _conferencia.ConsultarOutraFonteAsync(Documento.SomenteDigitos(requisicao.Cep), ct);
        return new SegundaOpiniaoCepDto
        {
            Cep = c.Cep, Resultado = c.Resultado, FontePrincipal = c.FontePrincipal, SegundaFonte = c.SegundaFonte, Mensagem = c.Mensagem,
            Componentes = c.Componentes.Select(x => new ComparacaoComponenteFontesDto
            {
                Componente = x.Componente, Situacao = x.Situacao, ValorPrincipal = x.ValorPrincipal, ValorSegunda = x.ValorSegunda, Motivo = x.Motivo
            }).ToList()
        };
    }

    /// <summary>A decisão do domínio como contrato da API (cópia; nada muda na decisão), com os avisos do serviço.</summary>
    public static DecisaoCepDto ParaDto(DecisaoCep d, IReadOnlyList<string>? avisos = null) => new()
    {
        Resultado = d.Resultado,
        Situacao = d.Situacao,
        CepInformado = d.CepInformado,
        CepSugerido = d.CepSugerido,
        Fonte = d.Fonte,
        RegistroConsultado = d.RegistroConsultado is { } r ? Candidato(r) : null,
        Candidatos = d.CandidatosAvaliados.Select(c => Candidato(c.Registro, c.Componentes)).ToList(),
        Motivos = d.Motivos.ToList(),
        Avisos = avisos?.ToList() ?? [],
        DeveBuscarPorEndereco = d.DeveBuscarPorEndereco,
        Componentes = d.Componentes
            .Select(c => new ComponenteCepDto { Componente = c.Componente, Situacao = c.Situacao, Motivo = c.Motivo }).ToList()
    };

    private static CandidatoCepDto Candidato(RegistroCep r, IReadOnlyList<ConferenciaComponenteCep>? componentes = null) => new()
    {
        Unidade = r.Unidade,
        Componentes = componentes?.Select(c => new ComponenteCepDto { Componente = c.Componente, Situacao = c.Situacao, Motivo = c.Motivo })
            .ToList() ?? new List<ComponenteCepDto>(),
        Cep = Documento.SomenteDigitos(r.Cep),
        Logradouro = r.Logradouro,
        Faixa = r.Complemento,
        Bairro = r.Bairro,
        Cidade = r.Cidade,
        Uf = r.Uf,
        CodigoMunicipioIbge = r.CodigoMunicipioIbge
    };

    public async Task<DadosCep?> ConsultarCepAsync(string cep, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        if (!Cep.EhValido(cep))
            throw new ValidacaoException(["O CEP deve ter 8 dígitos."]);

        // F3: a consulta antiga também entra no histórico técnico (sem mudar o contrato dela). Fonte: o ICepConsulta é o
        // ViaCEP (ConfiguracaoIntegracoes). Registrar nunca lança.
        var numero = Documento.SomenteDigitos(cep);
        var inicio = _relogio.GetTimestamp();
        try
        {
            var dados = await _cep.ConsultarAsync(numero, ct);
            await RegistrarConsultaDiretaAsync(numero, inicio, dados is null ? ResultadoConsultaCep.NaoEncontrado : ResultadoConsultaCep.Encontrado);
            return dados;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await RegistrarConsultaDiretaAsync(numero, inicio, ResultadoConsultaCep.Cancelado);
            throw;
        }
        catch (ServicoExternoException)
        {
            await RegistrarConsultaDiretaAsync(numero, inicio, ResultadoConsultaCep.Indisponivel);
            throw;
        }
    }

    private Task RegistrarConsultaDiretaAsync(string cep, long inicio, ResultadoConsultaCep resultado) =>
        _historico.RegistrarAsync(new ConsultaCepOcorrida(OperacaoConsultaCep.ConsultaDireta, TipoConsultaCep.PorCep,
            CacheConferenciaCep.ChaveCep(cep), cep, CepFonte.ViaCep, _relogio.GetUtcNow().UtcDateTime, _relogio.GetElapsedTime(inicio),
            resultado, OrigemRespostaCep.Fonte));

    /// <summary>Dados da Receita e, quando a fonte complementar tiver, as inscrições estaduais.</summary>
    public async Task<DadosCnpj?> ConsultarCnpjAsync(string cnpj, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        if (!Cnpj.EhValido(cnpj))
            throw new ValidacaoException(["Digite um CNPJ válido para consultar."]);

        var numero = Documento.Normalizar(cnpj);
        var dados = await _cnpj.ConsultarAsync(numero, ct);
        if (dados is not null && dados.InscricoesEstaduais.Count == 0)
            dados.InscricoesEstaduais = await _inscricoes.ConsultarAsync(numero, ct);
        return dados;
    }
}
