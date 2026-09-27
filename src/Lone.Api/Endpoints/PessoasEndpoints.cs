using Lone.Api.Erros;
using Lone.Application.Consultas;
using Lone.Application.Documentos;
using Lone.Application.Pessoas;
using Lone.Application.Privacidade;
using Lone.Application.Relacionamentos;
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

        // Tela de Pessoas: uma página com o total. Mesmos filtros da lista + natureza, só ativos, só inativos.
        grupo.MapGet("pagina",
            (int? pagina, int? tamanho, string? texto, Guid? papelId, Guid? etiquetaId, bool? incluirInativos, bool? municipioACorrigir,
             NaturezaPessoa? natureza, bool? somenteAtivos, bool? somenteInativos, IPessoaAppService servico, CancellationToken ct) =>
                servico.ListarPaginaAsync(new FiltroPessoas
                {
                    Texto = texto,
                    PapelId = papelId,
                    EtiquetaId = etiquetaId,
                    IncluirInativos = incluirInativos ?? false,
                    MunicipioACorrigir = municipioACorrigir ?? false,
                    Natureza = natureza,
                    SomenteAtivos = somenteAtivos ?? false,
                    SomenteInativos = somenteInativos ?? false
                }, pagina ?? 1, tamanho ?? PaginaListaPessoas.TamanhoPadrao, ct));

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

        // Ficha: CPF/raiz de CNPJ já cadastrado em outra pessoa (POST: o documento não vai na URL).
        grupo.MapPost("documento-em-uso", (DocumentoEmUsoRequisicao requisicao, IPessoaAppService servico, CancellationToken ct) =>
            servico.DocumentoEmUsoAsync(requisicao, ct));

        // Consulta avançada: critérios tipados no corpo (POST para não expor dados pessoais na URL).
        grupo.MapPost("consulta", (ConsultaPessoasRequisicao requisicao, IConsultaPessoasAppService servico, CancellationToken ct) =>
            servico.ConsultarAsync(requisicao, ct));

        grupo.MapPost("consulta/exportar", (CriteriosPessoas criterios, IConsultaPessoasAppService servico, CancellationToken ct) =>
            servico.ExportarAsync(criterios, ct));

        grupo.MapGet("consulta/opcoes", (IConsultaPessoasAppService servico, CancellationToken ct) => servico.OpcoesAsync(ct));

        grupo.MapPut("consulta/filtros/{filtroId:guid}", async (Guid filtroId, FiltroSalvoDto filtro, IConsultaPessoasAppService servico, CancellationToken ct) =>
        {
            if (filtro.Id != Guid.Empty && filtro.Id != filtroId)
                return Problemas.Resultado(Problemas.Validacao("O Id da URL não confere com o Id enviado."));
            filtro.Id = filtroId;
            return Results.Ok(await servico.SalvarFiltroAsync(filtro, ct));
        });

        grupo.MapPost("consulta/filtros/{filtroId:guid}/desativar", async (Guid filtroId, IConsultaPessoasAppService servico, CancellationToken ct) =>
        {
            await servico.DesativarFiltroAsync(filtroId, ct);
            return Results.NoContent();
        });

        // Rotina que aponta endereços duplicados (só leitura; a consolidação é assistida, na ficha).
        grupo.MapGet("enderecos-duplicados", (Guid? apos, int? limite, Lone.Application.Enderecos.IEnderecosDuplicadosAppService servico, CancellationToken ct) =>
            servico.ListarAsync(apos, limite ?? 100, ct));

        app.MapGroup(Rotas.FinalidadesEndereco.Grupo).WithTags("Finalidades de endereço").RequireAuthorization()
            .MapGet(string.Empty, (Lone.Application.Enderecos.IFinalidadeEnderecoAppService servico, CancellationToken ct) => servico.ListarAsync(ct));

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

        // Consolidação de endereço duplicado: o aplicativo manda só a intenção (origem → destino) e a versão aberta;
        // o servidor confere e decide tudo a partir do gravado, numa transação.
        grupo.MapPost("{id:guid}/enderecos/consolidar",
            (Guid id, Lone.Contracts.Enderecos.ConsolidarEnderecosRequisicao requisicao, IPessoaAppService servico, CancellationToken ct) =>
                servico.ConsolidarEnderecosAsync(id, requisicao, ct));

        grupo.MapGet("{id:guid}/historico",
            (Guid id, long? antes, int? limite, IPessoaAppService servico, CancellationToken ct) =>
                servico.ListarHistoricoAsync(id, antes, limite, ct));

        // Anexos de documentos (D7): cada envio é gravado na hora, à parte da ficha. Conteúdo em base64 no JSON
        // (limite por arquivo em "Anexos:TamanhoMaximoMb", padrão 10 MB; o Kestrel aceita até 30 MB por requisição).
        grupo.MapPost("{pessoaId:guid}/documentos/{documentoId:guid}/anexos",
            (Guid pessoaId, Guid documentoId, EnviarAnexoRequisicao requisicao, IAnexoAppService servico, CancellationToken ct) =>
                servico.EnviarAsync(pessoaId, documentoId, requisicao, ct));

        // Privacidade (Fase 3): leitura da aba e as ações próprias de conceder/revogar (nunca pelo "Salvar" da ficha).
        grupo.MapGet("{id:guid}/privacidade", (Guid id, IPrivacidadeAppService servico, CancellationToken ct) =>
            servico.ObterAsync(id, ct));
        grupo.MapPost("{id:guid}/consentimentos",
            (Guid id, ConcederConsentimentoRequisicao requisicao, IPrivacidadeAppService servico, CancellationToken ct) =>
                servico.ConcederAsync(id, requisicao, ct));
        grupo.MapPost("{id:guid}/consentimentos/{consentimentoId:guid}/revogar",
            (Guid id, Guid consentimentoId, RevogarConsentimentoRequisicao requisicao, IPrivacidadeAppService servico, CancellationToken ct) =>
                servico.RevogarAsync(id, consentimentoId, requisicao, ct));

        // Situações (fase 10): bloqueios e interações são ações próprias, fora do "Salvar" da ficha.
        grupo.MapPost("{id:guid}/bloqueios", (Guid id, BloquearRequisicao requisicao, ISituacaoAppService servico, CancellationToken ct) =>
            servico.BloquearAsync(id, requisicao, ct));
        grupo.MapPost("{id:guid}/bloqueios/{bloqueioId:guid}/liberar",
            (Guid id, Guid bloqueioId, LiberarBloqueioRequisicao requisicao, ISituacaoAppService servico, CancellationToken ct) =>
                servico.LiberarAsync(id, bloqueioId, requisicao, ct));
        grupo.MapPost("{id:guid}/interacoes", (Guid id, RegistrarInteracaoRequisicao requisicao, ISituacaoAppService servico, CancellationToken ct) =>
            servico.RegistrarInteracaoAsync(id, requisicao, ct));
        // Relacionamentos entre pessoas (sócio de, administrador de, contato de...): ações próprias, gravadas na hora e fora
        // do "Salvar" da ficha. Nunca apagam: encerrar preenche o fim; desativar marca o lançado por engano.
        grupo.MapGet("estrutura/opcoes", (IPessoaRelacionamentoAppService servico, CancellationToken ct) => servico.OpcoesAsync(ct));
        grupo.MapGet("{id:guid}/relacionamentos", (Guid id, IPessoaRelacionamentoAppService servico, CancellationToken ct) =>
            servico.ListarAsync(id, ct));
        grupo.MapPost("{id:guid}/relacionamentos",
            (Guid id, IncluirRelacionamentoRequisicao requisicao, IPessoaRelacionamentoAppService servico, CancellationToken ct) =>
                servico.IncluirAsync(id, requisicao, ct));
        grupo.MapPost("{id:guid}/relacionamentos/{relacionamentoId:guid}/encerrar",
            (Guid id, Guid relacionamentoId, EncerrarRelacionamentoRequisicao requisicao, IPessoaRelacionamentoAppService servico, CancellationToken ct) =>
                servico.EncerrarAsync(id, relacionamentoId, requisicao, ct));
        grupo.MapPost("{id:guid}/relacionamentos/{relacionamentoId:guid}/desativar",
            (Guid id, Guid relacionamentoId, DesativarRelacionamentoRequisicao requisicao, IPessoaRelacionamentoAppService servico, CancellationToken ct) =>
                servico.DesativarAsync(id, relacionamentoId, requisicao, ct));

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
