using Lone.Domain.Enderecos.ConferenciaCep;

namespace Lone.Application.Integracoes.ConferenciaCep;

/// <summary>
/// Uma fonte de CEP (ViaCEP, BrasilAPI; Correios no futuro). Traduz a resposta externa para o contrato interno e nunca
/// lança exceção por falha técnica: devolve <see cref="SituacaoProvedorCep.Indisponivel"/> ou
/// <see cref="SituacaoProvedorCep.RespostaInvalida"/>. Sem regra do motor (DM2): quem decide é o <c>MotorCep</c>.
/// </summary>
public interface IProvedorCep
{
    CepFonte Fonte { get; }

    /// <summary>A fonte também busca CEPs pelo endereço (UF + cidade + logradouro).</summary>
    bool BuscaPorEndereco { get; }

    /// <param name="cep">Só os 8 dígitos.</param>
    Task<ResultadoProvedorCep> ConsultarPorCepAsync(string cep, CancellationToken ct = default);

    Task<ResultadoBuscaProvedorCep> BuscarPorEnderecoAsync(BuscaEnderecoCep busca, CancellationToken ct = default);
}

/// <summary>
/// O que a fonte respondeu. Encontrado e NaoEncontrado são resultados <b>funcionais</b>; Indisponivel e RespostaInvalida
/// são falhas <b>técnicas</b> (DM2: não encontrado ≠ indisponível).
/// </summary>
public enum SituacaoProvedorCep
{
    Encontrado = 1,
    NaoEncontrado = 2,
    /// <summary>Tempo esgotado, sem conexão, erro do serviço, limite de taxa, disjuntor aberto.</summary>
    Indisponivel = 3,
    /// <summary>Respondeu, mas fora do contrato (JSON inválido ou sem os campos esperados).</summary>
    RespostaInvalida = 4
}

public static class SituacoesProvedorCep
{
    /// <summary>Falha técnica: pode passar à fonte reserva e nunca vai para o cache.</summary>
    public static bool EhFalhaTecnica(this SituacaoProvedorCep s) => s is SituacaoProvedorCep.Indisponivel or SituacaoProvedorCep.RespostaInvalida;
}

/// <summary>Resultado da consulta por CEP numa fonte.</summary>
public sealed record ResultadoProvedorCep(SituacaoProvedorCep Situacao, CepFonte Fonte, RegistroCep? Registro = null, string? Detalhe = null)
{
    public static ResultadoProvedorCep Encontrado(CepFonte fonte, RegistroCep registro) => new(SituacaoProvedorCep.Encontrado, fonte, registro);
    public static ResultadoProvedorCep NaoEncontrado(CepFonte fonte) => new(SituacaoProvedorCep.NaoEncontrado, fonte);
    public static ResultadoProvedorCep Indisponivel(CepFonte fonte, string detalhe) => new(SituacaoProvedorCep.Indisponivel, fonte, null, detalhe);
    public static ResultadoProvedorCep Invalida(CepFonte fonte, string detalhe) => new(SituacaoProvedorCep.RespostaInvalida, fonte, null, detalhe);
}

/// <summary>
/// Resultado da busca por endereço numa fonte (lista vazia = <see cref="SituacaoProvedorCep.NaoEncontrado"/>).
/// <paramref name="LimiteAtingido"/>: a fonte devolveu o máximo que ela devolve (ViaCEP: 50); a lista pode estar cortada.
/// </summary>
public sealed record ResultadoBuscaProvedorCep(SituacaoProvedorCep Situacao, CepFonte Fonte, IReadOnlyList<RegistroCep> Registros,
                                               string? Detalhe = null, bool LimiteAtingido = false)
{
    public static ResultadoBuscaProvedorCep Com(CepFonte fonte, IReadOnlyList<RegistroCep> registros, bool limiteAtingido = false) =>
        new(registros.Count > 0 ? SituacaoProvedorCep.Encontrado : SituacaoProvedorCep.NaoEncontrado, fonte, registros, null, limiteAtingido);
    public static ResultadoBuscaProvedorCep Indisponivel(CepFonte fonte, string detalhe) => new(SituacaoProvedorCep.Indisponivel, fonte, [], detalhe);
    public static ResultadoBuscaProvedorCep Invalida(CepFonte fonte, string detalhe) => new(SituacaoProvedorCep.RespostaInvalida, fonte, [], detalhe);
}

/// <summary>Dados para a busca por endereço. O logradouro vai normalizado pelo domínio (F1), sem segundo normalizador.</summary>
public sealed record BuscaEnderecoCep(string Uf, string Cidade, string Logradouro)
{
    /// <summary>Mínimo de letras ou dígitos do logradouro e da cidade aceito na busca (o ViaCEP exige 3).</summary>
    public const int TamanhoMinimo = 3;

    /// <summary>A busca do endereço, ou nulo quando faltam dados (UF de 2 letras, cidade e logradouro com 3 ou mais).</summary>
    public static BuscaEnderecoCep? De(EnderecoConferenciaCep endereco)
    {
        var uf = DuplicidadeEnderecoTexto(endereco.Uf);
        var cidade = (endereco.Cidade ?? string.Empty).Trim();
        var logradouro = NormalizadorLogradouro.Normalizar(endereco.Logradouro);
        if (uf.Length != 2 || uf == "EX" || DuplicidadeEnderecoTexto(cidade).Length < TamanhoMinimo || logradouro.Length < TamanhoMinimo)
            return null;
        return new BuscaEnderecoCep(uf, cidade, logradouro);
    }

    /// <summary>Chave do cache: UF, cidade e logradouro normalizados.</summary>
    public string Chave => $"{Uf}|{DuplicidadeEnderecoTexto(Cidade)}|{Logradouro}";

    private static string DuplicidadeEnderecoTexto(string? s) => Lone.Domain.Enderecos.DuplicidadeEndereco.Texto(s);
}
