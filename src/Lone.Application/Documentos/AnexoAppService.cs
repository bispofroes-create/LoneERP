using System.Security.Cryptography;
using Lone.Application.Seguranca;
using Lone.Contracts.Documentos;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Documentos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Application.Documentos;

public interface IAnexoAppService
{
    Task<AnexoDto> EnviarAsync(Guid pessoaId, Guid documentoId, EnviarAnexoRequisicao requisicao, CancellationToken ct = default);
    Task<AnexoConteudoDto> BaixarAsync(Guid id, CancellationToken ct = default);
    Task<AnexoDto> DesativarAsync(Guid id, CancellationToken ct = default);
    Task<AnexoDto> ReativarAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Anexos de documentos: permissão → conferência do documento → conteúdo (formato pela assinatura, tamanho) →
/// arquivo na pasta → registro no banco (histórico da pessoa). Remover só desativa; o arquivo fica.
/// </summary>
public sealed class AnexoAppService : IAnexoAppService
{
    private readonly IAnexoRepositorio _repositorio;
    private readonly IArmazenamentoAnexos _armazenamento;
    private readonly IAutorizacao _autorizacao;
    private readonly IUsuarioAtual _usuario;
    private readonly OpcoesAnexos _opcoes;

    public AnexoAppService(IAnexoRepositorio repositorio, IArmazenamentoAnexos armazenamento, IAutorizacao autorizacao,
                           IUsuarioAtual usuario, OpcoesAnexos opcoes)
    {
        _repositorio = repositorio;
        _armazenamento = armazenamento;
        _autorizacao = autorizacao;
        _usuario = usuario;
        _opcoes = opcoes;
    }

    public async Task<AnexoDto> EnviarAsync(Guid pessoaId, Guid documentoId, EnviarAnexoRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Editar);
        await ExigirDocumentoEditavelAsync(pessoaId, documentoId, "anexar arquivos", ct);

        var nome = RegrasAnexo.NomeSeguro(requisicao.NomeArquivo);
        var conteudo = requisicao.Conteudo ?? [];
        var erros = RegrasAnexo.Validar(nome, conteudo, _opcoes.TamanhoMaximoMb, out var tipo);
        if (erros.Count > 0) throw new ValidacaoException(erros);

        var anexo = new AnexoDocumento
        {
            Id = IdSequencial.Novo(),
            PessoaId = pessoaId,
            PessoaDocumentoId = documentoId,
            NomeArquivo = nome,
            TipoConteudo = tipo!,
            Tamanho = conteudo.LongLength,
            Hash = Convert.ToHexString(SHA256.HashData(conteudo)),
            EnviadoPor = _usuario.Nome.Length > 100 ? _usuario.Nome[..100] : _usuario.Nome
        };

        // Primeiro o arquivo, depois o registro: um registro nunca aponta para arquivo que não existe.
        anexo.Caminho = await _armazenamento.GravarAsync(anexo.Id, conteudo, ct);
        try
        {
            await _repositorio.IncluirAsync(anexo, ct);
        }
        catch
        {
            await _armazenamento.DescartarNaoRegistradoAsync(anexo.Caminho, CancellationToken.None);
            throw;
        }
        return ParaDto((await _repositorio.ObterAsync(anexo.Id, ct))!);
    }

    public async Task<AnexoConteudoDto> BaixarAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var anexo = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este anexo não existe."]);
        var conteudo = await _armazenamento.LerAsync(anexo.Caminho, ct)
            ?? throw new ValidacaoException([$"O arquivo \"{anexo.NomeArquivo}\" não foi encontrado no servidor. Avise o administrador."]);
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(conteudo)), anexo.Hash, StringComparison.OrdinalIgnoreCase))
            throw new ValidacaoException([$"O arquivo \"{anexo.NomeArquivo}\" está danificado no servidor (não confere com o enviado). Avise o administrador."]);

        return new AnexoConteudoDto { NomeArquivo = anexo.NomeArquivo, TipoConteudo = anexo.TipoConteudo, Conteudo = conteudo };
    }

    public Task<AnexoDto> DesativarAsync(Guid id, CancellationToken ct = default) => AlterarAtivoAsync(id, ativo: false, ct);

    public Task<AnexoDto> ReativarAsync(Guid id, CancellationToken ct = default) => AlterarAtivoAsync(id, ativo: true, ct);

    private async Task<AnexoDto> AlterarAtivoAsync(Guid id, bool ativo, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Editar);
        var anexo = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este anexo não existe."]);
        if (anexo.Ativo != ativo)
        {
            await ExigirDocumentoEditavelAsync(anexo.PessoaId, anexo.PessoaDocumentoId, ativo ? "reativar anexos" : "remover anexos", ct);
            await _repositorio.AlterarAtivoAsync(id, ativo, ct);
        }
        return ParaDto((await _repositorio.ObterAsync(id, ct))!);
    }

    private async Task ExigirDocumentoEditavelAsync(Guid pessoaId, Guid documentoId, string acao, CancellationToken ct)
    {
        var documento = await _repositorio.ConferirDocumentoAsync(pessoaId, documentoId, ct)
            ?? throw new ValidacaoException(["Documento não encontrado. Salve o cadastro antes de anexar arquivos."]);
        if (documento.SituacaoPessoa == SituacaoPessoa.Arquivado)
            throw new ValidacaoException(["Cadastro arquivado é somente leitura."]);
        if (!documento.DocumentoAtivo)
            throw new ValidacaoException([$"O documento foi removido (inativo): reative-o para {acao}."]);
    }

    public static AnexoDto ParaDto(AnexoDocumento a) => new()
    {
        Id = a.Id,
        PessoaDocumentoId = a.PessoaDocumentoId,
        NomeArquivo = a.NomeArquivo,
        TipoConteudo = a.TipoConteudo,
        Tamanho = a.Tamanho,
        EnviadoEm = a.CriadoEm,
        EnviadoPor = a.EnviadoPor,
        Ativo = a.Ativo
    };
}
