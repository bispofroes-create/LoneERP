namespace Lone.Cliente.Plataforma;

/// <summary>Arquivo escolhido pelo usuário (nome e conteúdo).</summary>
public sealed record ArquivoEscolhido(string Nome, byte[] Conteudo);

/// <summary>Escolha de arquivo no aparelho (Windows e Android). Implementado pelo aplicativo.</summary>
public interface IArquivos
{
    /// <summary>O arquivo escolhido, ou nulo se o usuário cancelou.</summary>
    Task<ArquivoEscolhido?> EscolherAsync(string titulo);

    /// <summary>Abre um arquivo baixado com o aplicativo padrão do aparelho (PDF, imagem...).</summary>
    Task AbrirAsync(string nome, byte[] conteudo);
}
