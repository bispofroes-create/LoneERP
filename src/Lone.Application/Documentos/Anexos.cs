using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Application.Documentos;

/// <summary>Situação do documento a que se quer anexar (nulo = documento não existe nesta pessoa).</summary>
public sealed record DocumentoParaAnexo(bool DocumentoAtivo, SituacaoPessoa SituacaoPessoa);

/// <summary>Dados dos anexos no banco. Nada é apagado: anexos são desativados.</summary>
public interface IAnexoRepositorio
{
    /// <summary>Todos os anexos da pessoa (ativos e inativos), do mais novo para o mais antigo.</summary>
    Task<List<AnexoDocumento>> ListarPorPessoaAsync(Guid pessoaId, CancellationToken ct);

    Task<AnexoDocumento?> ObterAsync(Guid id, CancellationToken ct);

    Task<DocumentoParaAnexo?> ConferirDocumentoAsync(Guid pessoaId, Guid documentoId, CancellationToken ct);

    Task IncluirAsync(AnexoDocumento anexo, CancellationToken ct);

    /// <summary>Ativa ou desativa (fica no histórico da pessoa).</summary>
    Task AlterarAtivoAsync(Guid id, bool ativo, CancellationToken ct);
}

/// <summary>Onde o conteúdo dos anexos fica guardado (pasta no servidor da API; D7). Não apaga arquivos gravados.</summary>
public interface IArmazenamentoAnexos
{
    /// <summary>Grava o conteúdo e devolve a chave (caminho relativo) para guardar no banco.</summary>
    Task<string> GravarAsync(Guid id, byte[] conteudo, CancellationToken ct);

    /// <summary>Conteúdo do arquivo; nulo se ele não estiver mais no armazenamento.</summary>
    Task<byte[]?> LerAsync(string caminho, CancellationToken ct);

    /// <summary>Só para desfazer uma gravação cujo registro no banco falhou (arquivo que nunca foi anexado).</summary>
    Task DescartarNaoRegistradoAsync(string caminho, CancellationToken ct);
}

/// <summary>Limite de tamanho por arquivo (seção "Anexos" da configuração da API).</summary>
public sealed class OpcoesAnexos
{
    public const string Secao = "Anexos";

    /// <summary>Pasta onde os arquivos ficam. Vazio = ProgramData/Lone/Anexos (Windows) ou equivalente.</summary>
    public string Pasta { get; set; } = string.Empty;

    public int TamanhoMaximoMb { get; set; } = Lone.Domain.Documentos.RegrasAnexo.TamanhoMaximoMbPadrao;
}
