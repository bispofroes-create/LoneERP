using Lone.Application.Auditoria;
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
using Lone.Application.Relacionamentos;
using Lone.Application.Situacoes;
using Lone.Application.Municipios;
using Lone.Application.Seguranca;
using Lone.Contracts.Auditoria;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.CamposPersonalizados;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Etiquetas;
using Lone.Domain.Profissoes;
using Lone.Domain.Papeis;
using Lone.Domain.Contatos;
using Lone.Domain.Enderecos;
using Lone.Domain.Documentos;
using Lone.Domain.Colaboradores;
using Lone.Domain.Comercial;
using Lone.Domain.Fiscal;
using Lone.Domain.GruposEmpresariais;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;

namespace Lone.Application.Pessoas;

/// <summary>
/// Orquestra o cadastro de pessoas: permissão → normalização → regras → duplicidade → gravação.
/// Não conhece banco, tela nem HTTP.
/// </summary>
public sealed class PessoaAppService : IPessoaAppService
{
    private const int LimiteHistorico = 500;

    /// <summary>Registros por página do histórico (o aplicativo pede mais sob demanda).</summary>
    private const int PaginaHistorico = 100;

    private readonly IPessoaRepositorio _repositorio;
    private readonly IAuditoriaConsultas _auditoria;
    private readonly IAutorizacao _autorizacao;
    private readonly IMunicipioRepositorio _municipios;
    private readonly ICampoPersonalizadoRepositorio _campos;
    private readonly IEtiquetaRepositorio _etiquetas;
    private readonly IProfissaoRepositorio _profissoes;
    private readonly IPapelRepositorio _papeis;
    private readonly ITipoMeioContatoRepositorio _tiposMeio;
    private readonly ITipoEnderecoRepositorio _tiposEndereco;
    private readonly ITipoDocumentoRepositorio _tiposDocumento;
    private readonly IAnexoRepositorio _anexos;
    private readonly IMotivoDaOperacao _motivo;
    private readonly ReferenciasColaborador _colaborador;
    private readonly ReferenciasComercial _comercial;
    private readonly ICnaeRepositorio _cnaes;
    private readonly ISituacaoAppService _situacoes;
    private readonly TimeProvider _relogio;
    private readonly IFinalidadeEnderecoRepositorio _finalidades;
    private readonly IGrupoEmpresarialRepositorio _gruposEmpresariais;
    private readonly IPessoaRelacionamentoRepositorio _relacionamentos;

    public PessoaAppService(IPessoaRepositorio repositorio, IAuditoriaConsultas auditoria, IAutorizacao autorizacao,
                            IMunicipioRepositorio municipios, ICampoPersonalizadoRepositorio campos, IEtiquetaRepositorio etiquetas,
                            IProfissaoRepositorio profissoes, IPapelRepositorio papeis,
                            ITipoMeioContatoRepositorio tiposMeio, ITipoEnderecoRepositorio tiposEndereco,
                            ITipoDocumentoRepositorio tiposDocumento, IAnexoRepositorio anexos,
                            IMotivoDaOperacao motivo, ReferenciasColaborador colaborador,
                            ReferenciasComercial comercial, ICnaeRepositorio cnaes,
                            ISituacaoAppService situacoes, TimeProvider relogio, IFinalidadeEnderecoRepositorio finalidades,
                            IGrupoEmpresarialRepositorio gruposEmpresariais, IPessoaRelacionamentoRepositorio relacionamentos)
    {
        _finalidades = finalidades;
        _gruposEmpresariais = gruposEmpresariais;
        _relacionamentos = relacionamentos;
        _repositorio = repositorio;
        _auditoria = auditoria;
        _autorizacao = autorizacao;
        _municipios = municipios;
        _campos = campos;
        _etiquetas = etiquetas;
        _profissoes = profissoes;
        _papeis = papeis;
        _tiposMeio = tiposMeio;
        _tiposEndereco = tiposEndereco;
        _tiposDocumento = tiposDocumento;
        _anexos = anexos;
        _relogio = relogio;
        _motivo = motivo;
        _colaborador = colaborador;
        _comercial = comercial;
        _cnaes = cnaes;
        _situacoes = situacoes;
    }

    public Task<List<PessoaResumo>> ListarAsync(FiltroPessoas filtro, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        filtro.Limite = Math.Clamp(filtro.Limite, 1, FiltroPessoas.LimiteMaximo);
        return _repositorio.ListarAsync(filtro, ct);
    }

    /// <summary>Tela de Pessoas: página com total (nunca a base inteira de uma vez).</summary>
    public Task<PaginaListaPessoas> ListarPaginaAsync(FiltroPessoas filtro, int pagina, int tamanho, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return _repositorio.ListarPaginaAsync(filtro, Math.Max(1, pagina), Math.Clamp(tamanho, 1, PaginaListaPessoas.TamanhoMaximo), ct);
    }

    /// <summary>
    /// Mesma regra da gravação (que continua recusando o duplicado): PF pelo CPF; PJ pela raiz do CNPJ (a filial de
    /// uma empresa já cadastrada entra como estabelecimento dela). Estrangeiro e documento inválido: não confere.
    /// </summary>
    public async Task<DocumentoEmUsoResposta> DocumentoEmUsoAsync(DocumentoEmUsoRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var chave = requisicao.Natureza switch
        {
            NaturezaPessoa.Fisica when global::Lone.Domain.Validacao.Documento.CpfValido(requisicao.Documento) =>
                global::Lone.Domain.Validacao.Documento.Normalizar(requisicao.Documento),
            NaturezaPessoa.Juridica when global::Lone.Domain.Validacao.Documento.CnpjValido(requisicao.Documento) =>
                global::Lone.Domain.Validacao.Documento.Normalizar(requisicao.Documento)[..8],
            _ => null
        };
        if (chave is null) return new DocumentoEmUsoResposta();

        var outra = await _repositorio.BuscarPorDocumentoAsync(requisicao.Natureza, chave, requisicao.IgnorarId, ct);
        return outra is null
            ? new DocumentoEmUsoResposta()
            : new DocumentoEmUsoResposta { EmUso = true, Id = outra.Id, Codigo = outra.Codigo, Nome = outra.Nome };
    }

    public Task<PaginaListaPessoas> ListarPaginaAsync(ListaPessoasRequisicao requisicao, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var condicoes = requisicao.Condicoes ?? [];
        var erros = global::Lone.Application.Consultas.CatalogoFiltrosPessoas.Normalizar(condicoes);
        if (erros.Count > 0) throw new ValidacaoException(erros);
        // Campo com permissão própria (dados sensíveis, financeiro...): quem não a tem não filtra por ele.
        foreach (var condicao in condicoes)
            if (global::Lone.Application.Consultas.CatalogoFiltrosPessoas.Obter(condicao.Campo)?.Permissao is { } permissao)
                _autorizacao.Exigir(permissao);

        // Colunas e ordenação: Ids conhecidos; coluna com permissão própria (ex.: limite de crédito) exige a permissão,
        // para mostrar e para ordenar (a ordem também revelaria o valor).
        var colunas = requisicao.Colunas ?? [];
        erros = global::Lone.Application.Consultas.ColunasListaPessoas.Normalizar(colunas, requisicao.Ordenacao, out var definicoes);
        if (erros.Count > 0) throw new ValidacaoException(erros);
        foreach (var coluna in definicoes)
            if (coluna.Permissao is { } permissao) _autorizacao.Exigir(permissao);
        if (requisicao.Ordenacao is { } ordem &&
            global::Lone.Application.Consultas.ColunasListaPessoas.Obter(ordem.Coluna)?.Permissao is { } permissaoOrdem)
            _autorizacao.Exigir(permissaoOrdem);

        return _repositorio.ListarPaginaAsync(requisicao.Filtro ?? new FiltroPessoas(), condicoes, colunas, requisicao.Ordenacao,
            DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime),
            Math.Max(1, requisicao.Pagina), Math.Clamp(requisicao.Tamanho, 1, PaginaListaPessoas.TamanhoMaximo), ct);
    }

    public async Task<PessoaDto?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var pessoa = await _repositorio.ObterAsync(id, ct);
        return pessoa is null ? null : await ParaTelaAsync(pessoa, ct);
    }

    /// <summary>DTO para a tela: nome da naturalidade, textos antigos de município a corrigir e dados sensíveis ocultos.</summary>
    private async Task<PessoaDto> ParaTelaAsync(Pessoa pessoa, CancellationToken ct)
    {
        var dto = PessoaMapeamento.ParaDto(pessoa);
        if (pessoa.NaturalidadeMunicipioId is { } naturalidade &&
            (await _municipios.ObterAsync([naturalidade], ct)).TryGetValue(naturalidade, out var municipio))
        {
            dto.NaturalidadeNome = municipio.Nome;
            dto.NaturalidadeUf = municipio.Uf;
        }

        dto.PendenciasMunicipio = (await _repositorio.ListarPendenciasMunicipioAsync(pessoa.Id, ct))
            .Select(p => new PendenciaMunicipioDto
            {
                DaNaturalidade = p.Origem == OrigemPendenciaMunicipio.Naturalidade,
                RegistroId = p.RegistroId,
                TextoOriginal = p.TextoOriginal,
                UfOriginal = p.UfOriginal,
                Observacao = p.Observacao
            }).ToList();

        // Anexos dos documentos (só os dados; o conteúdo é baixado sob demanda).
        if (dto.Documentos.Count > 0)
        {
            var anexos = (await _anexos.ListarPorPessoaAsync(pessoa.Id, ct)).ToLookup(a => a.PessoaDocumentoId);
            foreach (var documento in dto.Documentos)
                documento.Anexos = anexos[documento.Id].Select(AnexoAppService.ParaDto).ToList();
        }

        await _comercial.PreencherNomesAsync(dto.Carteira, ct);
        if (pessoa.GrupoEmpresarialId is { } grupoId)
            dto.GrupoEmpresarialNome = (await _gruposEmpresariais.ObterAsync(grupoId, ct))?.Nome;
        dto.Relacionamento = await _situacoes.ObterRelacionamentoAsync(pessoa.Id, ct);

        // Fiscal: histórico por período e a descrição do CNAE principal (tabela do IBGE, se já carregada).
        var cnaes = await _cnaes.ObterVariosAsync(pessoa.Estabelecimentos.Select(e => Cnae.Codigo(e.CnaePrincipal)).OfType<int>().ToList(), ct);
        foreach (var e in dto.Estabelecimentos)
        {
            e.HistoricoFiscal = pessoa.HistoricoFiscal.Where(h => h.EstabelecimentoId == e.Id).OrderByDescending(h => h.InicioEm)
                .Select(h => new HistoricoFiscalDto
                {
                    InicioEm = h.InicioEm, FimEm = h.FimEm, RegimeTributario = h.RegimeTributario, IndicadorIE = h.IndicadorIE,
                    InscricaoEstadual = h.InscricaoEstadual, SituacaoReceita = h.SituacaoReceita, ProdutorRural = h.ProdutorRural
                }).ToList();
            if (Cnae.Codigo(e.CnaePrincipal) is { } codigo && cnaes.TryGetValue(codigo, out var cnae))
                e.CnaePrincipalDescricao = $"{Cnae.Formatar(codigo)} · {cnae.Descricao}";
        }

        // Dados de colaborador (RH) só para quem tem a permissão; sem ela, a gravação mantém os gravados.
        if (_autorizacao.Possui(Permissoes.Pessoas.Colaborador))
            await _colaborador.PreencherNomesAsync(dto.Vinculos, ct);
        else
        {
            dto.Vinculos = [];
            dto.ColaboradorOculto = true;
        }

        OcultarDadosSensiveis(dto);
        return dto;
    }

    public async Task<List<QuantidadePorFaixaEtaria>> ListarFaixasEtariasAsync(TipoPapel? papel, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var nascimentos = await _repositorio.ListarNascimentosAsync(papel, ct);
        return FaixasEtarias.Contar(nascimentos, DateOnly.FromDateTime(DateTime.Today));
    }

    /// <summary>Cor/raça (LGPD, art. 11) só sai da API para quem tem a permissão de ver dados sensíveis.</summary>
    private void OcultarDadosSensiveis(PessoaDto dto)
    {
        if (_autorizacao.Possui(Permissoes.Pessoas.VisualizarDadosSensiveis)) return;
        dto.CorRacaOculta = dto.CorRaca != CorRaca.NaoInformado;
        dto.CorRaca = CorRaca.NaoInformado;
    }

    public Task<int> ContarClientesAtivosAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return _repositorio.ContarClientesAtivosAsync(ct);
    }

    public Task<List<RegistroHistorico>> ListarHistoricoAsync(Guid pessoaId, long? antesDe = null, int? limite = null, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return _auditoria.ListarPorRaizAsync(nameof(Pessoa), pessoaId,
            Math.Clamp(limite ?? PaginaHistorico, 1, LimiteHistorico), antesDe, ct);
    }

    public async Task<ResultadoSalvarPessoa> SalvarAsync(PessoaDto dto, CancellationToken ct = default)
    {
        // Id que ainda não existe = inclusão (o aparelho pode ter gerado o Id, inclusive offline).
        var anterior = dto.Id == Guid.Empty ? null : await _repositorio.ObterAsync(dto.Id, ct);
        var nova = anterior is null;

        // Motivo da alteração (opcional): gravado em todas as linhas de auditoria desta gravação.
        _motivo.Motivo = dto.MotivoAlteracao;

        if (anterior?.Situacao == SituacaoPessoa.Arquivado)
            throw new ValidacaoException(["Cadastro arquivado é somente leitura."]);

        var dados = PessoaMapeamento.ParaEntidade(dto);

        // Endereço × finalidade: pedido antigo (só bits) traduzido sem apagar principal; campos que só a API controla
        // (consolidação, revisão) e Ids das relações casados com o gravado.
        CompatibilidadeFinalidadesLegado.Aplicar(dados, dto.Enderecos, anterior);
        ManterControleDosEnderecos(dados, anterior);

        // Papéis: ligados ao cadastro de papéis antes de tudo, porque as regras seguintes (empresa do grupo,
        // dados de funcionário) usam o papel de sistema copiado de lá, nunca o que veio do aplicativo.
        RegrasPapel.CompletarIds(dados);
        RegrasDocumento.CompletarIds(dados);
        var papeisAnteriores = (anterior?.Papeis ?? []).Where(p => p.Ativo).Select(p => p.PapelId).ToHashSet();
        var cadastroPapeis = await _papeis.ObterVariosAsync(dados.Papeis.Select(p => p.PapelId).Concat(papeisAnteriores).ToList(), ct);
        var errosPapeis = RegrasPapel.Aplicar(dados, papeisAnteriores, cadastroPapeis);
        if (nova) dados.Codigo = 0; // o código é dado pelo banco

        ManterSituacao(dados, anterior);

        // Sem permissão para dados sensíveis, a cor/raça não é vista nem alterada: fica a gravada.
        if (!_autorizacao.Possui(Permissoes.Pessoas.VisualizarDadosSensiveis))
            dados.CorRaca = anterior?.CorRaca ?? CorRaca.NaoInformado;

        if (!_autorizacao.Possui(Permissoes.Pessoas.Colaborador))
        {
            dados.Vinculos = [.. anterior?.Vinculos ?? []];
            dados.Lotacoes = [.. anterior?.Lotacoes ?? []];
        }
        RegrasColaborador.Normalizar(dados);
        RegrasComercial.Normalizar(dados);

        PessoaNormalizador.Normalizar(dados);
        ExigirPermissoes(dados, anterior);

        var erros = PessoaValidador.Validar(dados);
        erros.AddRange(errosPapeis);
        erros.AddRange(RegrasFiscal.ValidarCamposDeEmpresa(dados, anterior));
        erros.AddRange(RegrasColaborador.Validar(dados));
        erros.AddRange(await _colaborador.ValidarAsync(dados, anterior, ct));

        // Comercial: perfis, condições, exceções com vigência e carteira (D5: o vendedor principal vigente vira o vendedor padrão).
        var tiposCarteira = await _comercial.TiposAsync(ct);
        erros.AddRange(RegrasComercial.Validar(dados, tiposCarteira));
        erros.AddRange(await _comercial.ValidarAsync(dados, anterior, tiposCarteira, ct));
        RegrasComercial.AtualizarVendedorPadrao(dados, tiposCarteira, DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime));

        // Fiscal: CNAEs em tabela (a partir dos campos de texto) e histórico com vigência (regime, IE, situação, produtor rural).
        RegrasFiscal.SincronizarCnaes(dados, anterior?.Cnaes ?? []);
        RegrasFiscal.AtualizarHistorico(dados, anterior?.HistoricoFiscal ?? [], DateOnly.FromDateTime(_relogio.GetLocalNow().DateTime));

        // Municípios só da tabela do IBGE: confere os Ids e copia nome, UF e código para o endereço.
        erros.AddRange(ReferenciasMunicipio.Aplicar(dados, await _municipios.ObterAsync(ReferenciasMunicipio.Ids(dados).ToList(), ct)));

        // Informações adicionais: conferidas contra as definições (desativado mantém o gravado).
        var campos = await _campos.ListarAsync(EntidadePersonalizavel.Pessoa, incluirInativos: true, ct);
        erros.AddRange(ValidadorValoresPersonalizados.Aplicar(dados.ValoresPersonalizados, campos, anterior?.ValoresPersonalizados ?? []));

        // Etiquetas: do cadastro de etiquetas; uma desativada só continua em quem já a tinha.
        erros.AddRange(RegrasEtiqueta.ValidarMarcadas(
            dados.Etiquetas,
            (anterior?.Etiquetas ?? []).Select(e => e.EtiquetaId).ToHashSet(),
            await _etiquetas.ObterVariasAsync(dados.Etiquetas.Select(e => e.EtiquetaId).ToList(), ct)));

        // Telefones/e-mails: a classificação vem do cadastro de tipos (mesma categoria; desativado só se já era o dele).
        var tiposEscolhidos = dados.MeiosContato.Select(m => m.TipoMeioContatoId).OfType<Guid>().Distinct().ToList();
        erros.AddRange(RegrasMeioContato.ValidarTipos(
            dados.MeiosContato,
            (anterior?.MeiosContato ?? []).ToDictionary(m => m.Id, m => m.TipoMeioContatoId),
            await _tiposMeio.ObterVariosAsync(tiposEscolhidos, ct)));

        // Endereços: o tipo vem do cadastro de tipos de endereço (desativado só se já era o dele).
        erros.AddRange(RegrasEndereco.ValidarTipos(
            dados.Enderecos,
            (anterior?.Enderecos ?? []).ToDictionary(e => e.Id, e => e.TipoEnderecoId),
            await _tiposEndereco.ObterVariosAsync(dados.Enderecos.Select(e => e.TipoEnderecoId).OfType<Guid>().Distinct().ToList(), ct)));

        // Endereço × finalidade (fonte oficial das finalidades e do principal de cada uma). A ficha manda o estado
        // COMPLETO (contrato): conferido aqui, as regras abaixo valem sobre exatamente o que ficará gravado.
        // Endereço físico repetido (novo, reativado ou alterado; igual a um inativo também) não entra.
        var finalidades = (await _finalidades.ListarAsync(ct)).ToDictionary(f => f.Id);
        if (anterior is not null)
            erros.AddRange(RegrasFinalidadeEndereco.ValidarCompleto(dados, anterior));
        erros.AddRange(RegrasFinalidadeEndereco.Validar(dados, finalidades, anterior?.FinalidadesEnderecos ?? []));
        erros.AddRange(DuplicidadeEndereco.ErrosDeNovos(dados.Enderecos, anterior?.Enderecos ?? []));

        // Revisão da migração: motivos só desligam (resolvidos ou conferidos); a marca geral só se mantém enquanto houver
        // pendência real (a API nunca liga).
        RegrasFinalidadeEndereco.AtualizarRevisao(dados, anterior);

        // Coluna legada de bits: cópia derivada, regravada na mesma transação (compatibilidade; ninguém lê dela).
        var referencia = RegrasFinalidadeEndereco.EnderecoReferencia(dados, finalidades);
        RegrasFinalidadeEndereco.SincronizarLegado(dados, referencia);

        // Documentos: o tipo vem do cadastro (desativado só se já era o dele), que também diz se a validade é obrigatória;
        // o enum antigo é copiado do tipo escolhido.
        erros.AddRange(RegrasDocumento.Aplicar(
            dados.Documentos,
            (anterior?.Documentos ?? []).ToDictionary(d => d.Id, d => d.TipoDocumentoId),
            await _tiposDocumento.ObterVariosAsync(dados.Documentos.Select(d => d.TipoDocumentoId).Distinct().ToList(), ct),
            dados.Natureza));

        // Campos personalizados dos documentos (D4): cada documento só com os campos do seu tipo.
        erros.AddRange(AplicarValoresDocumentos(dados, anterior,
            dados.Documentos.Count == 0 && (anterior?.ValoresDocumentos.Count ?? 0) == 0
                ? new List<CampoPersonalizado>()
                : await _campos.ListarAsync(EntidadePersonalizavel.Documento, incluirInativos: true, ct)));

        // Profissão: do cadastro de profissões; uma desativada só continua em quem já a tinha.
        if (RegrasProfissao.ValidarEscolhida(
                dados.ProfissaoId,
                anterior?.ProfissaoId,
                dados.ProfissaoId is { } profissaoId ? await _profissoes.ObterAsync(profissaoId, ct) : null) is { } erroProfissao)
            erros.Add(erroProfissao);

        // Pessoa jurídica gravada não vira outra natureza (nada é perdido em silêncio: recusa com a lista do que se perderia).
        if (anterior is { Natureza: NaturezaPessoa.Juridica } && dados.Natureza != NaturezaPessoa.Juridica &&
            RegrasNaturezaPessoa.ValidarTroca(anterior, dados, await _relacionamentos.ContarSocietariosComoEmpresaAsync(dados.Id, ct)) is { } erroNatureza)
            erros.Add(erroNatureza);

        // Estabelecimentos: gravado nunca é apagado (vem desativado).
        erros.AddRange(RegrasEstabelecimento.ValidarCompleto(dados, anterior));

        // Grupo empresarial (só PJ, validado no PessoaValidador): do cadastro; um desativado só continua em quem já o tinha.
        if (RegrasGrupoEmpresarial.ValidarEscolhido(
                dados.GrupoEmpresarialId,
                anterior?.GrupoEmpresarialId,
                dados.GrupoEmpresarialId is { } grupoEscolhido ? await _gruposEmpresariais.ObterAsync(grupoEscolhido, ct) : null) is { } erroGrupo)
            erros.Add(erroGrupo);

        if (dados.DocumentoPrincipal is not null && dados.Natureza != NaturezaPessoa.Estrangeiro)
        {
            var mesmoDocumento = await _repositorio.BuscarPorDocumentoAsync(dados.Natureza, dados.DocumentoPrincipal, dados.Id, ct);
            if (mesmoDocumento is not null)
                erros.Add(dados.Natureza == NaturezaPessoa.Juridica
                    ? $"Esta empresa (mesma raiz de CNPJ) já está cadastrada: {mesmoDocumento}. Para uma filial, abra esse cadastro e adicione o CNPJ como estabelecimento."
                    : $"Este CPF já está cadastrado: {mesmoDocumento}.");
        }

        if (erros.Count > 0)
            throw new ValidacaoException(erros);

        // Papel que começou ou terminou vira frase no histórico (os períodos em si também ficam gravados).
        if (anterior is not null)
            foreach (var mudanca in RegrasPapel.Mudancas(papeisAnteriores, dados.Papeis, cadastroPapeis))
                dados.RegistrarEvento(mudanca);

        // Estrutura empresarial: estabelecimento incluído/desativado/reativado, troca do principal e entrada/saída do grupo.
        foreach (var mudanca in RegrasEstabelecimento.Mudancas(anterior, dados))
            dados.RegistrarEvento(mudanca);
        if (dados.GrupoEmpresarialId != anterior?.GrupoEmpresarialId)
        {
            var nomesGrupos = (await _gruposEmpresariais.ListarAsync(ct)).ToDictionary(g => g.Id, g => g.Nome);
            if (RegrasGrupoEmpresarial.Mudanca(anterior?.GrupoEmpresarialId, dados.GrupoEmpresarialId,
                    id => nomesGrupos.GetValueOrDefault(id, "(grupo)")) is { } frase)
                dados.RegistrarEvento(frase);
        }

        // Admissão e desligamento viram frase no histórico (os demais campos aparecem campo a campo).
        if (_autorizacao.Possui(Permissoes.Pessoas.Colaborador) && dados.Vinculos.Count > 0)
        {
            var empresas = await _colaborador.NomesEmpresasAsync(dados, ct);
            foreach (var mudanca in RegrasColaborador.Mudancas(anterior?.Vinculos ?? [], dados.Vinculos, empresas))
                dados.RegistrarEvento(mudanca);
        }

        // Vendedor substituído (o vigente encerrado na véspera de um novo que conflitaria com ele, pela confirmação da
        // ficha) vira frase no histórico; os campos de cada vínculo também ficam na auditoria.
        if (anterior is not null && RegrasComercial.Substituicoes(anterior.Carteira, dados.Carteira, tiposCarteira, _ => string.Empty).Any())
        {
            var nomes = await _comercial.NomesAsync(
                [.. anterior.Carteira.Select(c => c.VendedorId).Concat(dados.Carteira.Select(c => c.VendedorId)).Distinct()], ct);
            foreach (var frase in RegrasComercial.Substituicoes(anterior.Carteira, dados.Carteira, tiposCarteira,
                         id => nomes.GetValueOrDefault(id, "(vendedor)")))
                dados.RegistrarEvento(frase);
        }

        var avisos = await BuscarAvisosDeDuplicidadeAsync(dados, ct);
        avisos.AddRange(ConferenciaInscricaoEstadual.Avisos(dados, referencia));
        if (DuplicidadeEndereco.Pares(dados.Enderecos, incluirPossiveis: false).Count > 0)
            avisos.Add("Há endereços iguais já gravados nesta ficha. Use \"Consolidar endereços\" para juntar as finalidades num só.");

        await _repositorio.SalvarAsync(dados, nova, OrigemAlteracao.Usuario, ct);

        // Relê do banco: volta com o código, a versão nova e tudo como ficou gravado.
        var salva = await _repositorio.ObterAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
        return new ResultadoSalvarPessoa { Pessoa = await ParaTelaAsync(salva, ct), Avisos = avisos };
    }

    /// <summary>
    /// Confere os valores de cada documento contra os campos do tipo dele. Valor de campo de outro tipo (o documento
    /// mudou de tipo) é descartado sem erro; documento removido (inativo) ou que não veio mantém o que estava gravado.
    /// </summary>
    private static List<string> AplicarValoresDocumentos(Pessoa dados, Pessoa? anterior, IReadOnlyCollection<CampoPersonalizado> campos)
    {
        var erros = new List<string>();
        var gravados = anterior?.ValoresDocumentos ?? [];
        var idsDeCampos = campos.Select(c => c.Id).ToHashSet();
        var resultado = new List<DocumentoValorPersonalizado>();

        for (var i = 0; i < dados.Documentos.Count; i++)
        {
            var documento = dados.Documentos[i];
            var valores = dados.ValoresDocumentos.Where(v => v.PessoaDocumentoId == documento.Id).ToList();
            var gravadosDoDocumento = gravados.Where(v => v.PessoaDocumentoId == documento.Id).ToList();
            if (!documento.Ativo)
            {
                ValidadorValoresPersonalizados.ManterGravados(valores, gravadosDoDocumento);
            }
            else
            {
                var doTipo = campos.Where(c => c.TipoDocumentoId == documento.TipoDocumentoId).ToList();
                var idsDoTipo = doTipo.Select(c => c.Id).ToHashSet();
                valores.RemoveAll(v => idsDeCampos.Contains(v.CampoId) && !idsDoTipo.Contains(v.CampoId));
                gravadosDoDocumento.RemoveAll(v => !idsDoTipo.Contains(v.CampoId));
                erros.AddRange(ValidadorValoresPersonalizados.Aplicar(valores, doTipo, gravadosDoDocumento, $"documento {i + 1}"));
            }
            resultado.AddRange(valores);
        }

        // Documento que não veio na lista fica como está no banco (nunca é apagado), com os valores dele.
        var enviados = dados.Documentos.Select(d => d.Id).ToHashSet();
        resultado.AddRange(gravados.Where(v => !enviados.Contains(v.PessoaDocumentoId)).Select(v => (DocumentoValorPersonalizado)v.Clonar()));

        dados.ValoresDocumentos = resultado;
        return erros;
    }

    /// <summary>
    /// Campos de endereço que a ficha não controla: MescladoEmId volta ao gravado (consolidar é operação própria) e as
    /// relações que casam pela chave (endereço + finalidade) recebem o Id gravado; relação nova com Id de outra gravada
    /// ganha Id novo (uma relação nunca "muda" de endereço ou de finalidade).
    /// </summary>
    private static void ManterControleDosEnderecos(Pessoa dados, Pessoa? anterior)
    {
        var gravados = anterior?.Enderecos.ToDictionary(e => e.Id) ?? new Dictionary<Guid, PessoaEndereco>();
        foreach (var e in dados.Enderecos)
            e.MescladoEmId = gravados.TryGetValue(e.Id, out var g) ? g.MescladoEmId : null;

        var porChave = (anterior?.FinalidadesEnderecos ?? []).GroupBy(RegrasFinalidadeEndereco.Chave).ToDictionary(x => x.Key, x => x.First().Id);
        var idsGravados = porChave.Values.ToHashSet();
        foreach (var u in dados.FinalidadesEnderecos)
        {
            if (porChave.TryGetValue(RegrasFinalidadeEndereco.Chave(u), out var id)) u.Id = id;
            else if (idsGravados.Contains(u.Id) || u.Id == Guid.Empty) u.Id = IdSequencial.Novo();
        }
    }

    /// <summary>
    /// Consolida um endereço duplicado em outro. O aplicativo só informa a intenção (origem → destino); o servidor
    /// carrega o estado gravado, confere (mesma pessoa, origem e destino ativos, mesmo endereço físico, conflito de
    /// principal), decide as finalidades resultantes e grava tudo numa gravação só (transação), com evento na auditoria.
    /// </summary>
    public async Task<PessoaDto> ConsolidarEnderecosAsync(Guid id, Lone.Contracts.Enderecos.ConsolidarEnderecosRequisicao requisicao,
                                                          CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Editar);
        var pessoa = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        var anterior = await _repositorio.ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
        if (pessoa.Situacao == SituacaoPessoa.Arquivado)
            throw new ValidacaoException(["Cadastro arquivado é somente leitura."]);
        pessoa.Versao = requisicao.Versao ?? pessoa.Versao;

        var erros = ConsolidacaoEndereco.Aplicar(pessoa, requisicao.OrigemId, requisicao.DestinoId);
        if (erros.Count > 0) throw new ValidacaoException(erros);

        var finalidades = (await _finalidades.ListarAsync(ct)).ToDictionary(f => f.Id);
        RegrasFinalidadeEndereco.Normalizar(pessoa);
        erros.AddRange(RegrasFinalidadeEndereco.Validar(pessoa, finalidades, anterior.FinalidadesEnderecos));
        if (erros.Count > 0) throw new ValidacaoException(erros);
        RegrasFinalidadeEndereco.AtualizarRevisao(pessoa, anterior);
        RegrasFinalidadeEndereco.SincronizarLegado(pessoa, RegrasFinalidadeEndereco.EnderecoReferencia(pessoa, finalidades));

        var origem = pessoa.Enderecos.First(e => e.Id == requisicao.OrigemId);
        var destino = pessoa.Enderecos.First(e => e.Id == requisicao.DestinoId);
        var usos = pessoa.FinalidadesEnderecos.Where(u => u.PessoaEnderecoId == destino.Id && u.Ativo)
            .Select(u => (finalidades.TryGetValue(u.FinalidadeId, out var f) ? f.Nome : "?") + (u.Principal ? " (principal)" : string.Empty));
        pessoa.RegistrarEvento($"Endereço '{DuplicidadeEndereco.Resumo(origem)}' consolidado em '{DuplicidadeEndereco.Resumo(destino)}'. " +
                               $"Finalidades do endereço mantido: {string.Join(", ", usos)}.");

        await _repositorio.SalvarAsync(pessoa, nova: false, OrigemAlteracao.Usuario, ct);
        var salva = await _repositorio.ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
        return await ParaTelaAsync(salva, ct);
    }

    public Task<PessoaDto> DesativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, (p, agora) => p.Desativar(requisicao.Motivo, agora), ct);

    public Task<PessoaDto> ReativarAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default) =>
        AlterarSituacaoAsync(id, requisicao, (p, agora) => p.Reativar(requisicao.Motivo, agora), ct);

    /// <summary>
    /// Desativar e reativar são ações próprias (não passam pelo formulário): exigem a permissão de inativar,
    /// conferem a versão que o usuário tinha aberta e registram o evento na auditoria.
    /// </summary>
    private async Task<PessoaDto> AlterarSituacaoAsync(
        Guid id, AlterarSituacaoRequisicao requisicao, Action<Pessoa, DateTime> acao, CancellationToken ct)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Inativar);
        _motivo.Motivo = requisicao.Motivo; // também na coluna Motivo da auditoria, além do texto do evento
        var pessoa = await _repositorio.ObterAsync(id, ct) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        pessoa.Versao = requisicao.Versao ?? pessoa.Versao;

        acao(pessoa, _relogio.GetUtcNow().UtcDateTime);
        await _repositorio.SalvarAsync(pessoa, nova: false, OrigemAlteracao.Usuario, ct);

        var salva = await _repositorio.ObterAsync(id, ct) ?? throw new ConflitoDeEdicaoException();
        return await ParaTelaAsync(salva, ct);
    }

    /// <summary>
    /// Pelo formulário só se alterna entre "ativo" e "em análise". Desativar, reativar e arquivar são ações
    /// próprias (com permissão, motivo e evento); o motivo e a data gravados não vêm do aparelho.
    /// </summary>
    private static void ManterSituacao(Pessoa dados, Pessoa? anterior)
    {
        var atual = anterior?.Situacao ?? SituacaoPessoa.Ativo;
        var pedida = dados.Situacao;
        var emUso = atual is SituacaoPessoa.Ativo or SituacaoPessoa.EmAnalise;

        dados.Situacao = emUso && pedida is SituacaoPessoa.Ativo or SituacaoPessoa.EmAnalise ? pedida : atual;
        if (emUso && pedida is SituacaoPessoa.Inativo or SituacaoPessoa.Arquivado)
            throw new ValidacaoException(["Para desativar o cadastro, use a ação \"Desativar\" (fica registrado quem, quando e por quê)."]);

        dados.SituacaoMotivo = anterior?.SituacaoMotivo;
        dados.SituacaoAlteradaEm = anterior?.SituacaoAlteradaEm;
    }

    /// <summary>Criar/editar, e permissões extras quando mudam crédito, o papel de empresa do grupo ou o grupo empresarial.</summary>
    private void ExigirPermissoes(Pessoa dados, Pessoa? anterior)
    {
        _autorizacao.Exigir(anterior is null ? Permissoes.Pessoas.Criar : Permissoes.Pessoas.Editar);

        if (CreditoMudou(dados, anterior) || CondicoesComerciaisMudaram(dados, anterior))
            _autorizacao.Exigir(Permissoes.Pessoas.AlterarCredito);

        if (dados.TemPapel(TipoPapel.EmpresaDoGrupo) != (anterior?.TemPapel(TipoPapel.EmpresaDoGrupo) ?? false))
            _autorizacao.Exigir(Permissoes.Pessoas.GerenciarEmpresasDoGrupo);

        // Entrar, sair ou trocar de grupo empresarial é mudança de estrutura empresarial.
        if (dados.GrupoEmpresarialId != anterior?.GrupoEmpresarialId)
            _autorizacao.Exigir(Permissoes.Pessoas.EstruturaEmpresarial);
    }

    /// <summary>Perfil comercial ou exceções (que mudam limite, desconto e prazos) também exigem "alterar crédito".</summary>
    private static bool CondicoesComerciaisMudaram(Pessoa dados, Pessoa? anterior)
    {
        static string Chave(IEnumerable<ExcecaoComercial> excecoes) => string.Join("|", excecoes.OrderBy(e => e.Id).Select(e =>
            $"{e.Id};{e.EmpresaId};{e.InicioEm};{e.FimEm};{e.LimiteCredito};{e.DescontoMaximo};{e.DiasMaximoAtraso};{e.CondicaoPagamentoId};{e.ExigeAprovacaoAcimaLimite}"));
        static string Perfis(IEnumerable<ContaCliente> contas) =>
            string.Join("|", contas.OrderBy(c => c.EmpresaId).Select(c => $"{c.EmpresaId};{c.PerfilComercialId}"));

        return Chave(dados.ExcecoesComerciais) != Chave(anterior?.ExcecoesComerciais ?? []) ||
               Perfis(dados.ContasCliente) != Perfis(anterior?.ContasCliente ?? []);
    }

    /// <summary>Compara os dados de crédito de cada conta de cliente (por empresa; Guid.Empty = conta padrão).</summary>
    private static bool CreditoMudou(Pessoa dados, Pessoa? anterior)
    {
        var semConta = ((decimal?)null, (int?)null, (decimal?)null, true);

        static Dictionary<Guid, (decimal?, int?, decimal?, bool)> PorEmpresa(IEnumerable<ContaCliente> contas) =>
            contas.GroupBy(c => c.EmpresaId ?? Guid.Empty).ToDictionary(
                g => g.Key,
                g => (g.First().LimiteCredito, g.First().DiasMaximoAtraso, g.First().DescontoMaximo, g.First().ExigeAprovacaoAcimaLimite));

        var antes = PorEmpresa(anterior?.ContasCliente ?? []);
        var depois = PorEmpresa(dados.ContasCliente);

        return antes.Keys.Union(depois.Keys).Any(empresa =>
            (antes.TryGetValue(empresa, out var a) ? a : semConta) != (depois.TryGetValue(empresa, out var d) ? d : semConta));
    }

    /// <summary>Nome, telefone ou e-mail iguais geram aviso, mas não impedem a gravação.</summary>
    private async Task<List<string>> BuscarAvisosDeDuplicidadeAsync(Pessoa p, CancellationToken ct)
    {
        var contatos = p.MeiosContato.Where(m => m.Ativo).Select(m => m.Valor)
            .Concat(p.Contatos.SelectMany(c => new[] { c.Telefone, c.Celular, c.Email }))
            .OfType<string>()
            .Where(v => v.Length > 0)
            .Distinct()
            .ToList();

        var semelhantes = await _repositorio.BuscarSemelhantesAsync(p.Id, p.Nome, contatos, ct);
        return semelhantes
            .Select(s => $"Possível cadastro duplicado (mesmo nome, telefone ou e-mail): {s}")
            .ToList();
    }
}
