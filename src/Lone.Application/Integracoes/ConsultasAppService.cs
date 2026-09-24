using Lone.Application.Seguranca;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Seguranca;
using Lone.Domain.ObjetosDeValor;
using Lone.Domain.Validacao;

namespace Lone.Application.Integracoes;

/// <summary>Consultas externas usadas no cadastro (a API chama os serviços; o aplicativo só chama a API).</summary>
public interface IConsultasAppService
{
    Task<DadosCep?> ConsultarCepAsync(string cep, CancellationToken ct = default);
    Task<DadosCnpj?> ConsultarCnpjAsync(string cnpj, CancellationToken ct = default);
}

public sealed class ConsultasAppService : IConsultasAppService
{
    private readonly ICepConsulta _cep;
    private readonly ICnpjConsulta _cnpj;
    private readonly IInscricaoEstadualConsulta _inscricoes;
    private readonly IAutorizacao _autorizacao;

    public ConsultasAppService(ICepConsulta cep, ICnpjConsulta cnpj, IInscricaoEstadualConsulta inscricoes, IAutorizacao autorizacao)
    {
        _cep = cep;
        _cnpj = cnpj;
        _inscricoes = inscricoes;
        _autorizacao = autorizacao;
    }

    public Task<DadosCep?> ConsultarCepAsync(string cep, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        if (!Cep.EhValido(cep))
            throw new ValidacaoException(["O CEP deve ter 8 dígitos."]);
        return _cep.ConsultarAsync(Documento.SomenteDigitos(cep), ct);
    }

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
