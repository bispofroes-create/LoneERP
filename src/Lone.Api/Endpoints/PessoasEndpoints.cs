using Lone.Api.Erros;
using Lone.Application.Documentos;
using Lone.Application.Pessoas;
using Lone.Application.Situacoes;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Documentos;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Api.Endpoints;

/// <summary>Cadastro de pessoas. As permissões são conferidas pelo PessoaAppService.</summary>
public static class PessoasEndpoints
{
    public static IEndpointRouteBuilder MapPessoas(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Pessoas.Grupo).WithTags("Pessoas").RequireAuthorization();

        // ?texto=...&papelId=...&etiquetaId=...&incluirInativos=true&municipioACorrigir=true&limite=100 (todos opcionais)
        grupo.MapGet(string.Empty,
            (string? texto, Guid? papelId, Guid? etiquetaId, bool? incluirInativos, bool? municipioACorrigir, int? limite,
             IPessoaAppService servico, CancellationToken ct) =>
                servico.ListarAsync(new FiltroPessoas
                {
                    Texto = texto,
                    PapelId = papelId,
                    EtiquetaId = etiquetaId,
                    IncluirInativos = incluirInativos ?? false,
                    MunicipioACorrigir = municipioACorrigir ?? false,
                    Limite = limite ?? FiltroPessoas.LimiteMaximo
                }, ct));

        grupo.MapGet("indicadores/faixas-etarias",
            (TipoPapel? papel, IPessoaAppService servico, CancellationToken ct) => servico.ListarFaixasEtariasAsync(papel, ct));

        grupo.MapGet("indicadores/clientes-ativos",
            (IPessoaAppService servico, CancellationToken ct) => servico.ContarClientesAtivosAsync(ct));

        grupo.MapGet("{id:guid}", async (Guid id, IPessoaAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } pessoa
                ? Results.Ok(pessoa)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este cadastro não existe.")));

        // Inclui ou altera: o Id vem do aparelho (IdSequencial), então a mesma chamada serve para os dois casos.
        grupo.MapPut("{id:guid}", async (Guid id, PessoaDto pessoa, IPessoaAppService servico, CancellationToken ct) =>
        {
            if (pessoa.Id != Guid.Empty && pessoa.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id do cadastro enviado."));

            pessoa.Id = id;
            return Results.Ok(await servico.SalvarAsync(pessoa, ct));
        });

        // Desativar/reativar: ações próprias (permissão, versão aberta, motivo e evento na auditoria). Nunca apagam.
        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IPessoaAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IPessoaAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));

        grupo.MapGet("{id:guid}/historico",
            (Guid id, long? antes, int? limite, IPessoaAppService servico, CancellationToken ct) =>
                servico.ListarHistoricoAsync(id, antes, limite, ct));

        // Anexos de documentos (D7): cada envio é gravado na hora, à parte da ficha. Conteúdo em base64 no JSON
        // (limite por arquivo em "Anexos:TamanhoMaximoMb", padrão 10 MB; o Kestrel aceita até 30 MB por requisição).
        grupo.MapPost("{pessoaId:guid}/documentos/{documentoId:guid}/anexos",
            (Guid pessoaId, Guid documentoId, EnviarAnexoRequisicao requisicao, IAnexoAppService servico, CancellationToken ct) =>
                servico.EnviarAsync(pessoaId, documentoId, requisicao, ct));

        // Situações (fase 10): bloqueios e interações são ações próprias, fora do "Salvar" da ficha.
        grupo.MapPost("{id:guid}/bloqueios", (Guid id, BloquearRequisicao requisicao, ISituacaoAppService servico, CancellationToken ct) =>
            servico.BloquearAsync(id, requisicao, ct));
        grupo.MapPost("{id:guid}/bloqueios/{bloqueioId:guid}/liberar",
            (Guid id, Guid bloqueioId, LiberarBloqueioRequisicao requisicao, ISituacaoAppService servico, CancellationToken ct) =>
                servico.LiberarAsync(id, bloqueioId, requisicao, ct));
        grupo.MapPost("{id:guid}/interacoes", (Guid id, RegistrarInteracaoRequisicao requisicao, ISituacaoAppService servico, CancellationToken ct) =>
            servico.RegistrarInteracaoAsync(id, requisicao, ct));
        grupo.MapGet("parametros-relacionamento", (ISituacaoAppService servico, CancellationToken ct) => servico.ObterParametrosAsync(ct));
        grupo.MapPut("parametros-relacionamento", (ParametrosRelacionamentoDto dto, ISituacaoAppService servico, CancellationToken ct) =>
            servico.SalvarParametrosAsync(dto, ct));

        var anexos = app.MapGroup(Rotas.Anexos.Grupo).WithTags("Anexos").RequireAuthorization();
        anexos.MapGet("{id:guid}/conteudo", (Guid id, IAnexoAppService servico, CancellationToken ct) => servico.BaixarAsync(id, ct));
        anexos.MapPost("{id:guid}/desativar", (Guid id, IAnexoAppService servico, CancellationToken ct) => servico.DesativarAsync(id, ct));
        anexos.MapPost("{id:guid}/reativar", (Guid id, IAnexoAppService servico, CancellationToken ct) => servico.ReativarAsync(id, ct));

        return app;
    }
}
