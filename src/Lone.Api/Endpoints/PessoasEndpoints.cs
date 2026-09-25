using Lone.Api.Erros;
using Lone.Application.Pessoas;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
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
            (Guid id, IPessoaAppService servico, CancellationToken ct) => servico.ListarHistoricoAsync(id, ct));

        return app;
    }
}
