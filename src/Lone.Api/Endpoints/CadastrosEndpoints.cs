using Lone.Api.Erros;
using Lone.Application.CamposPersonalizados;
using Lone.Application.Etiquetas;
using Lone.Application.Profissoes;
using Lone.Application.Papeis;
using Lone.Application.Contatos;
using Lone.Application.Enderecos;
using Lone.Application.Documentos;
using Lone.Application.Colaboradores;
using Lone.Application.Comercial;
using Lone.Application.Fiscal;
using Lone.Application.GruposEmpresariais;
using Lone.Application.Municipios;
using Lone.Application.Privacidade;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Comum;
using Lone.Contracts.Etiquetas;
using Lone.Contracts.Profissoes;
using Lone.Contracts.Papeis;
using Lone.Contracts.Contatos;
using Lone.Contracts.Enderecos;
using Lone.Contracts.Documentos;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Comercial;
using Lone.Contracts.GruposEmpresariais;
using Lone.Contracts.Pessoas;
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
        MapFinalidadesTratamento(app);
        MapTiposDocumento(app);
        MapCargos(app);
        MapDepartamentos(app);
        MapSetores(app);
        MapCentrosCusto(app);
        MapColaboradores(app);
        MapPerfisComerciais(app);
        MapCondicoesPagamento(app);
        MapTiposCarteira(app);
        MapComercial(app);
        MapTiposAusencia(app);
        MapParametrosComerciais(app);
        MapCoberturas(app);
        MapTransferencias(app);
        MapCnaes(app);
        MapGruposEmpresariais(app);
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

    private static void MapCargos(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Estrutura.Cargos).WithTags("Cargos").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, ICargoAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, ICargoAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este cadastro não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, CargoDto item, ICargoAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ICargoAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ICargoAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));
    }

    /// <summary>Grupos empresariais: conjuntos de pessoas jurídicas independentes. Nada é excluído: desativa.</summary>
    private static void MapGruposEmpresariais(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.GruposEmpresariais.Grupo).WithTags("Grupos empresariais").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, IGrupoEmpresarialAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, IGrupoEmpresarialAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este cadastro não existe.")));

        // As pessoas jurídicas do grupo, cada uma com a sua contagem de estabelecimentos (matriz e filiais).
        grupo.MapGet("{id:guid}/empresas", (Guid id, IGrupoEmpresarialAppService servico, CancellationToken ct) =>
            servico.ListarEmpresasAsync(id, ct));

        grupo.MapPut("{id:guid}", async (Guid id, GrupoEmpresarialDto item, IGrupoEmpresarialAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IGrupoEmpresarialAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IGrupoEmpresarialAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));
    }

    private static void MapDepartamentos(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Estrutura.Departamentos).WithTags("Departamentos").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, IDepartamentoAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, IDepartamentoAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este cadastro não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, DepartamentoDto item, IDepartamentoAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IDepartamentoAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IDepartamentoAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));
    }

    private static void MapSetores(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Estrutura.Setores).WithTags("Setores").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, ISetorAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, ISetorAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este cadastro não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, SetorDto item, ISetorAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ISetorAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ISetorAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));
    }

    private static void MapCentrosCusto(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Estrutura.CentrosCusto).WithTags("Centros de custo").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, ICentroCustoAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, ICentroCustoAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este cadastro não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, CentroCustoDto item, ICentroCustoAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ICentroCustoAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ICentroCustoAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));
    }

    private static void MapColaboradores(IEndpointRouteBuilder app) =>
        app.MapGet(Rotas.Colaboradores.Opcoes, (IColaboradorAppService servico, CancellationToken ct) => servico.ListarOpcoesAsync(ct))
            .WithTags("Colaboradores").RequireAuthorization();

    private static void MapPerfisComerciais(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Comercial.Perfis).WithTags("Perfis comerciais").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, IPerfilComercialAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, IPerfilComercialAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este cadastro não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, PerfilComercialDto item, IPerfilComercialAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IPerfilComercialAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IPerfilComercialAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));
    }

    private static void MapCondicoesPagamento(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Comercial.Condicoes).WithTags("Condições de pagamento").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, ICondicaoPagamentoAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, ICondicaoPagamentoAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este cadastro não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, CondicaoPagamentoDto item, ICondicaoPagamentoAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ICondicaoPagamentoAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ICondicaoPagamentoAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));
    }

    private static void MapTiposCarteira(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Comercial.TiposCarteira).WithTags("Papéis comerciais").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, ITipoCarteiraAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, ITipoCarteiraAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este cadastro não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, TipoCarteiraDto item, ITipoCarteiraAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ITipoCarteiraAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ITipoCarteiraAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));
    }

    private static void MapComercial(IEndpointRouteBuilder app) =>
        app.MapGet(Rotas.Comercial.Opcoes, (IComercialAppService servico, CancellationToken ct) => servico.ListarOpcoesAsync(ct))
            .WithTags("Comercial").RequireAuthorization();

    // ---- Motor Comercial, Fase 1c: ausências, coberturas, parâmetros e carteira vencendo ----

    private static void MapTiposAusencia(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Comercial.TiposAusencia).WithTags("Tipos de ausência").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, ITipoAusenciaAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, ITipoAusenciaAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este cadastro não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, TipoAusenciaDto item, ITipoAusenciaAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ITipoAusenciaAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ITipoAusenciaAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));
    }

    private static void MapParametrosComerciais(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Comercial.Parametros).WithTags("Parâmetros comerciais").RequireAuthorization();
        grupo.MapGet(string.Empty, (IParametrosComerciaisAppService servico, CancellationToken ct) => servico.ObterAsync(ct));
        grupo.MapPut(string.Empty, (ParametrosComerciaisDto dto, IParametrosComerciaisAppService servico, CancellationToken ct) =>
            servico.SalvarAsync(dto, ct));
    }

    private static void MapCoberturas(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Comercial.Coberturas).WithTags("Coberturas de ausência").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirEncerradas, ICoberturaAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirEncerradas ?? false, ct));

        grupo.MapGet("opcoes", (ICoberturaAppService servico, CancellationToken ct) => servico.ListarOpcoesAsync(ct));

        grupo.MapGet("{id:guid}", async (Guid id, ICoberturaAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Esta cobertura não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, CoberturaDto item, ICoberturaAppService servico, CancellationToken ct) =>
        {
            if (item.Id != Guid.Empty && item.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            item.Id = id;
            return Results.Ok(await servico.SalvarAsync(item, ct));
        });

        grupo.MapPost("{id:guid}/cancelar", (Guid id, CancelarCoberturaRequisicao requisicao, ICoberturaAppService servico, CancellationToken ct) =>
            servico.CancelarAsync(id, requisicao, ct));

        app.MapGet(Rotas.Comercial.CarteiraVencendo, (int? dias, ICoberturaAppService servico, CancellationToken ct) =>
                servico.CarteiraVencendoAsync(dias, ct))
            .WithTags("Coberturas de ausência").RequireAuthorization();
    }

    /// <summary>Transferência de carteira (Motor Comercial, Fase 1d): prévia sem gravar, gravação e consulta.</summary>
    private static void MapTransferencias(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Comercial.Transferencias).WithTags("Transferências de carteira").RequireAuthorization();

        grupo.MapGet(string.Empty, (ITransferenciaCarteiraAppService servico, CancellationToken ct) => servico.ListarAsync(ct));

        grupo.MapGet("opcoes", (ITransferenciaCarteiraAppService servico, CancellationToken ct) => servico.ListarOpcoesAsync(ct));

        grupo.MapGet("{id:guid}", async (Guid id, ITransferenciaCarteiraAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } item
                ? Results.Ok(item)
                : Problemas.Resultado(Problemas.NaoEncontrado("Esta transferência não existe.")));

        grupo.MapPost("previa", (TransferenciaRequisicao requisicao, ITransferenciaCarteiraAppService servico, CancellationToken ct) =>
            servico.PreviaAsync(requisicao, ct));

        grupo.MapPost(string.Empty, (TransferenciaRequisicao requisicao, ITransferenciaCarteiraAppService servico, CancellationToken ct) =>
            servico.TransferirAsync(requisicao, ct));

        // ?data=2026-04-15&clienteId=... ou &pessoaId=... (um dos dois).
        app.MapGet(Rotas.Comercial.CarteiraEmData, (DateOnly data, Guid? clienteId, Guid? pessoaId, ICarteiraEmDataAppService servico, CancellationToken ct) =>
                servico.ConsultarAsync(clienteId, pessoaId, data, ct))
            .WithTags("Transferências de carteira").RequireAuthorization();
    }

    private static void MapCnaes(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.Cnaes.Grupo).WithTags("CNAE").RequireAuthorization();
        grupo.MapGet(string.Empty, (string? texto, ICnaeAppService servico, CancellationToken ct) => servico.BuscarAsync(texto, ct));
        grupo.MapGet("situacao", (ICnaeAppService servico, CancellationToken ct) => servico.ObterSituacaoAsync(ct));
        grupo.MapPost("atualizar", (ICnaeAppService servico, CancellationToken ct) => servico.AtualizarAsync(ct));
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

    private static void MapFinalidadesTratamento(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.FinalidadesTratamento.Grupo).WithTags("Finalidades de tratamento").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, IFinalidadeTratamentoAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, IFinalidadeTratamentoAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } finalidade
                ? Results.Ok(finalidade)
                : Problemas.Resultado(Problemas.NaoEncontrado("Esta finalidade não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, FinalidadeTratamentoDto finalidade, IFinalidadeTratamentoAppService servico, CancellationToken ct) =>
        {
            if (finalidade.Id != Guid.Empty && finalidade.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id da finalidade enviada."));
            finalidade.Id = id;
            return Results.Ok(await servico.SalvarAsync(finalidade, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IFinalidadeTratamentoAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, IFinalidadeTratamentoAppService servico, CancellationToken ct) =>
                servico.ReativarAsync(id, requisicao, ct));
    }

    private static void MapTiposDocumento(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Rotas.TiposDocumento.Grupo).WithTags("Tipos de documento").RequireAuthorization();

        grupo.MapGet(string.Empty, (bool? incluirInativos, ITipoDocumentoAppService servico, CancellationToken ct) =>
            servico.ListarAsync(incluirInativos ?? false, ct));

        grupo.MapGet("{id:guid}", async (Guid id, ITipoDocumentoAppService servico, CancellationToken ct) =>
            await servico.ObterAsync(id, ct) is { } tipo
                ? Results.Ok(tipo)
                : Problemas.Resultado(Problemas.NaoEncontrado("Este tipo não existe.")));

        grupo.MapPut("{id:guid}", async (Guid id, TipoDocumentoDto tipo, ITipoDocumentoAppService servico, CancellationToken ct) =>
        {
            if (tipo.Id != Guid.Empty && tipo.Id != id)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id do tipo enviado."));
            tipo.Id = id;
            return Results.Ok(await servico.SalvarAsync(tipo, ct));
        });

        grupo.MapPost("{id:guid}/desativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ITipoDocumentoAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, requisicao, ct));

        grupo.MapPost("{id:guid}/reativar",
            (Guid id, AlterarSituacaoRequisicao requisicao, ITipoDocumentoAppService servico, CancellationToken ct) =>
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
