using Lone.Api.Erros;
using Lone.Application.CamposPersonalizados;
using Lone.Application.Etiquetas;
using Lone.Application.Profissoes;
using Lone.Application.Papeis;
using Lone.Application.Contatos;
using Lone.Application.Enderecos;
using Lone.Application.Municipios;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Etiquetas;
using Lone.Contracts.Profissoes;
using Lone.Contracts.Papeis;
using Lone.Contracts.Contatos;
using Lone.Contracts.Enderecos;
using Lone.Domain.Enums;

namespace Lone.Api.Endpoints;

/// <summary>Tabelas de apoio dos cadastros: municípios do IBGE, campos personalizados e etiquetas. Permissões nos AppServices.</summary>
public static class CadastrosEndpoints
{
    public static IEndpointRouteBuilder MapCadastros(this IEndpointRouteBuilder app)
    {
        MapMunicipios(app);
        MapCamposPersonalizados(app);
        MapEtiquetas(app);
        MapProfissoes(app);
        MapPapeis(app);
        MapTiposMeioContato(app);
        MapTiposEndereco(app);
        return app;
    }

    private static void MapMunicipios(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Municipios.Grupo).WithTags("Municípios").RequireAuthorization();

        // ?uf=MG — a lista inteira da UF (até ~850 itens); o aplicativo filtra enquanto o usuário digita.
        grupo.MapGet(string.Empty, (string uf, IMunicipioAppService servico, CancellationToken ct) => servico.ListarDaUfAsync(uf, ct));

        grupo.MapGet("situacao", (IMunicipioAppService servico, CancellationToken ct) => servico.ObterSituacaoAsync(ct));

        grupo.MapPost("atualizar", (IMunicipioAppService servico, CancellationToken ct) => servico.AtualizarAsync(ct));
    }

    private static void MapTiposMeioContato(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.TiposMeioContato.Grupo).WithTags("Tipos de telefone/e-mail").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, ITipoMeioContatoAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, ITipoMeioContatoAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } tipo
                ? Results.Ok(tipo)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este tipo não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, TipoMeioContatoDto tipo, ITipoMeioContatoAppService servico, CancellationToken ct) =>
        {
            if (tipo.Id != Guid.Empty && tipo.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id do tipo enviado."));
            tipo.Id = id;
            return Results.Ok(await servico.SalvarAsync(tipo, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ITipoMeioContatoAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ITipoMeioContatoAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));
    }

    private static void MapTiposEndereco(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.TiposEndereco.Grupo).WithTags("Tipos de endereço").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, ITipoEnderecoAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, ITipoEnderecoAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } tipo
                ? Results.Ok(tipo)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este tipo não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, TipoEnderecoDto tipo, ITipoEnderecoAppService servico, CancellationToken ct) =>
        {
            if (tipo.Id != Guid.Empty && tipo.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id do tipo enviado."));
            tipo.Id = id;
            return Results.Ok(await servico.SalvarAsync(tipo, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ITipoEnderecoAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ITipoEnderecoAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));
    }

    private static void MapPapeis(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Papeis.Grupo).WithTags("Papéis").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, IPapelAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, IPapelAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } papel
                ? Results.Ok(papel)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este papel não existe.")));

        // Inclui ou altera (o Id vem do aparelho).
        grupo.MapPut("{id:guid}", async (Guid id, PapelCadastroDto papel, IPapelAppService servico, CancellationToken ct) =>
        {
            if (papel.Id != Guid.Empty && papel.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id do papel enviado."));
            papel.Id = id;
            return Results.Ok(await servico.SalvarAsync(papel, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IPapelAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IPapelAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));
    }

    private static void MapProfissoes(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Profissoes.Grupo).WithTags("Profissões").RequireAuthorization();

        // Lista pequena (dezenas a centenas): vem inteira e o aplicativo filtra no aparelho.
        grupo.MapGet(string.Empty, (bool? incluirInativas, IProfissaoAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativas ?? false, ct));

        // Tabela oficial da CBO (rotas fixas antes das que têm {id}).
        grupo.MapGet("cbo", (IOcupacaoCboAppService servico, CancellationToken ct) => servico.ListarAsync(ct));
        grupo.MapGet("cbo/situacao", (IOcupacaoCboAppService servico, CancellationToken ct) => servico.ObterSituacaoAsync(ct));
        grupo.MapPost("cbo/importar", (ImportarCboRequisicao requisicao, IOcupacaoCboAppService servico, CancellationToken ct) =>
            servico.ImportarAsync(requisicao, ct));

        grupo.MapGet("{id:guid}", async (Guid id, IProfissaoAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } profissao
                ? Results.Ok(profissao)
                : Problemas.Resultado(Problemas.NaoEncontrado("Esta profissão não existe.")));

        // Inclui ou altera (o Id vem do aparelho).
        grupo.MapPut("{id:guid}", async (Guid id, ProfissaoDto profissao, IProfissaoAppService servico, CancellationToken ct) =>
        {
            if (profissao.Id != Guid.Empty && profissao.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id da profissão enviada."));
            profissao.Id = id;
            return Results.Ok(await servico.SalvarAsync(profissao, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IProfissaoAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IProfissaoAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/mesclar",
            (Guid id, MesclarProfissaoRequisicao requisicao, IProfissaoAppService servico, CancellationToken ct) =>
                servico.MesclarAsync(id, requisicao, ct));
    }

    private static void MapEtiquetas(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Etiquetas.Grupo).WithTags("Etiquetas").RequireAuthorization();

        // Lista pequena (dezenas a centenas): vem inteira e o aplicativo filtra no aparelho.
        grupo.MapGet(string.Empty, (bool? incluirInativas, IEtiquetaAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativas ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, IEtiquetaAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } etiqueta
                ? Results.Ok(etiqueta)
                : Problemas.Resultado(Problemas.NaoEncontrado("Esta etiqueta não existe.")));

        // Inclui ou altera (o Id vem do aparelho).
        grupo.MapPut("{id:guid}", async (Guid id, EtiquetaDto etiqueta, IEtiquetaAppService servico, CancellationToken ct) =>
        {
            if (etiqueta.Id != Guid.Empty && etiqueta.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id da etiqueta enviada."));
            etiqueta.Id = id;
            return Results.Ok(await servico.SalvarAsync(etiqueta, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IEtiquetaAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IEtiquetaAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/mesclar",
            (Guid id, MesclarEtiquetaRequisicao requisicao, IEtiquetaAppService servico, CancellationToken ct) =>
                servico.MesclarAsync(id, requisicao, ct));
    }

    private static void MapCamposPersonalizados(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.CamposPersonalizados.Grupo).WithTags("Campos personalizados").RequireAuthorization();

        grupo.MapGet(string.Empty,
            (EntidadePersonalizavel? entidade, bool? incluirInativos, ICampoPersonalizadoAppService servico, CancellationToken ct) =>
                servico.ListarAsync(entidade ?? EntidadePersonalizavel.Pessoa, incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, ICampoPersonalizadoAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } campo
                ? Results.Ok(campo)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este campo não existe.")));

        // Inclui ou altera (o Id vem do aparelho).
        grupo.MapPut("{id:guid}", async (Guid id, CampoPersonalizadoDto campo, ICampoPersonalizadoAppService servico, CancellationToken ct) =>
        {
            if (campo.Id != Guid.Empty && campo.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id do campo enviado."));
            campo.Id = id;
            return Results.Ok(await servico.SalvarAsync(campo, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ICampoPersonalizadoAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ICampoPersonalizadoAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));

        grupo.MapPut("ordem", async (EntidadePersonalizavel? entidade, List<Guid> ids, ICampoPersonalizadoAppService servico, CancellationToken ct) =>
        {
            await servico.ReordenarAsync(entidade ?? EntidadePersonalizavel.Pessoa, ids, ct);
            return Results.NoContent();
        });
    }
}
