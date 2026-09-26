using Lone.Domain.Enums;

namespace Lone.Domain.Pessoas;

/// <summary>
/// Regra única do nome que identifica a pessoa na tela (cabeçalho da ficha e lista de Pessoas). Só apresentação: nenhum
/// valor gravado muda. Precedência: nome de exibição → (PJ) nome fantasia do estabelecimento principal → (demais) nome
/// social → nome civil / razão social. A consulta da lista no banco repete esta mesma ordem
/// (<c>PessoaRepositorio.NomeParaExibirNoBanco</c>); mudou aqui, mude lá.
/// </summary>
public static class NomePessoa
{
    public static string ParaExibir(NaturezaPessoa natureza, string? nome, string? nomeExibicao, string? nomeSocial, string? nomeFantasia)
    {
        if (Preenchido(nomeExibicao)) return nomeExibicao!.Trim();
        if (natureza == NaturezaPessoa.Juridica)
        {
            if (Preenchido(nomeFantasia)) return nomeFantasia!.Trim();
        }
        else if (Preenchido(nomeSocial))
        {
            return nomeSocial!.Trim();
        }
        return nome?.Trim() ?? string.Empty;
    }

    private static bool Preenchido(string? texto) => !string.IsNullOrWhiteSpace(texto);
}
