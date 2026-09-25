using Lone.Application.Auditoria;
using Lone.Application.CamposPersonalizados;
using Lone.Application.Etiquetas;
using Lone.Application.Profissoes;
using Lone.Application.Papeis;
using Lone.Application.Contatos;
using Lone.Application.Enderecos;
using Lone.Application.Documentos;
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
using Lone.Domain.Validacao;

namespace Lone.Application.Pessoas;

/// <summary>
/// Orquestra o cadastro de pessoas: permissão → normalização → regras → duplicidade → gravação.
/// Não conhece banco, tela nem HTTP.
/// </summary>
public sealed class PessoaAppService : IPessoaAppService
{
    private const int LimiteHistorico = 500;

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
    private readonly TimeProvider _relogio;

    public PessoaAppService(IPessoaRepositorio repositorio, IAuditoriaConsultas auditoria, IAutorizacao autorizacao,
                            IMunicipioRepositorio municipios, ICampoPersonalizadoRepositorio campos, IEtiquetaRepositorio etiquetas,
                            IProfissaoRepositorio profissoes, IPapelRepositorio papeis,
                            ITipoMeioContatoRepositorio tiposMeio, ITipoEnderecoRepositorio tiposEndereco,
                            ITipoDocumentoRepositorio tiposDocumento, TimeProvider relogio)
    {
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
        _relogio = relogio;
    }

    public Task<List<PessoaResumo>> ListarAsync(FiltroPessoas filtro, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        filtro.Limite = Math.Clamp(filtro.Limite, 1, FiltroPessoas.LimiteMaximo);
        return _repositorio.ListarAsync(filtro, ct);
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

        OcultarDadosSensiveis(dto);
        return dto;
    }

    public async Task<List<QuantidadePorFaixaEtaria>> ListarFaixasEtariasAsync(TipoPapel? papel, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        var nascimentos = await _repositorio.ListarNascimentosAsync(papel, ct);
        return FaixasEtarias.Contar(nascimentos, DateOnly.FromDateTime(DateTime.Today));
    }

    /// <summary>
    /// As datas de autorização e revogação são prova (LGPD): valem as já gravadas, nunca as enviadas pelo
    /// aparelho. Mudou a situação, o normalizador grava a data do servidor.
    /// </summary>
    private static void ManterDatasDosConsentimentos(Pessoa dados, Pessoa? anterior)
    {
        foreach (var c in dados.Consentimentos)
        {
            var gravado = anterior?.Consentimentos.FirstOrDefault(x => x.Canal == c.Canal);
            var reautorizado = c.Concedido && gravado is { Concedido: false };
            c.ConcedidoEm = reautorizado ? null : gravado?.ConcedidoEm;
            c.RevogadoEm = gravado?.RevogadoEm;
        }
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

    public Task<List<RegistroHistorico>> ListarHistoricoAsync(Guid pessoaId, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return _auditoria.ListarPorRaizAsync(nameof(Pessoa), pessoaId, LimiteHistorico, ct);
    }

    public async Task<ResultadoSalvarPessoa> SalvarAsync(PessoaDto dto, CancellationToken ct = default)
    {
        // Id que ainda não existe = inclusão (o aparelho pode ter gerado o Id, inclusive offline).
        var anterior = dto.Id == Guid.Empty ? null : await _repositorio.ObterAsync(dto.Id, ct);
        var nova = anterior is null;

        if (anterior?.Situacao == SituacaoPessoa.Arquivado)
            throw new ValidacaoException(["Cadastro arquivado é somente leitura."]);

        var dados = PessoaMapeamento.ParaEntidade(dto);

        // Papéis: ligados ao cadastro de papéis antes de tudo, porque as regras seguintes (empresa do grupo,
        // dados de funcionário) usam o papel de sistema copiado de lá, nunca o que veio do aplicativo.
        RegrasPapel.CompletarIds(dados);
        RegrasDocumento.CompletarIds(dados);
        var papeisAnteriores = (anterior?.Papeis ?? []).Where(p => p.Ativo).Select(p => p.PapelId).ToHashSet();
        var cadastroPapeis = await _papeis.ObterVariosAsync(dados.Papeis.Select(p => p.PapelId).Concat(papeisAnteriores).ToList(), ct);
        var errosPapeis = RegrasPapel.Aplicar(dados, papeisAnteriores, cadastroPapeis);
        if (nova) dados.Codigo = 0; // o código é dado pelo banco

        ManterSituacao(dados, anterior);
        ManterDatasDosConsentimentos(dados, anterior);

        // Sem permissão para dados sensíveis, a cor/raça não é vista nem alterada: fica a gravada.
        if (!_autorizacao.Possui(Permissoes.Pessoas.VisualizarDadosSensiveis))
            dados.CorRaca = anterior?.CorRaca ?? CorRaca.NaoInformado;

        PessoaNormalizador.Normalizar(dados);
        ExigirPermissoes(dados, anterior);

        var erros = PessoaValidador.Validar(dados);
        erros.AddRange(errosPapeis);

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

        // Documentos: o tipo vem do cadastro (desativado só se já era o dele), que também diz se a validade é obrigatória;
        // o enum antigo é copiado do tipo escolhido.
        erros.AddRange(RegrasDocumento.Aplicar(
            dados.Documentos,
            (anterior?.Documentos ?? []).ToDictionary(d => d.Id, d => d.TipoDocumentoId),
            await _tiposDocumento.ObterVariosAsync(dados.Documentos.Select(d => d.TipoDocumentoId).Distinct().ToList(), ct)));

        // Profissão: do cadastro de profissões; uma desativada só continua em quem já a tinha.
        if (RegrasProfissao.ValidarEscolhida(
                dados.ProfissaoId,
                anterior?.ProfissaoId,
                dados.ProfissaoId is { } profissaoId ? await _profissoes.ObterAsync(profissaoId, ct) : null) is { } erroProfissao)
            erros.Add(erroProfissao);

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

        var avisos = await BuscarAvisosDeDuplicidadeAsync(dados, ct);
        avisos.AddRange(ConferenciaInscricaoEstadual.Avisos(dados));
        await _repositorio.SalvarAsync(dados, nova, OrigemAlteracao.Usuario, ct);

        // Relê do banco: volta com o código, a versão nova e tudo como ficou gravado.
        var salva = await _repositorio.ObterAsync(dados.Id, ct) ?? throw new ConflitoDeEdicaoException();
        return new ResultadoSalvarPessoa { Pessoa = await ParaTelaAsync(salva, ct), Avisos = avisos };
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

    /// <summary>Criar/editar, e permissões extras quando mudam crédito ou o papel de empresa do grupo.</summary>
    private void ExigirPermissoes(Pessoa dados, Pessoa? anterior)
    {
        _autorizacao.Exigir(anterior is null ? Permissoes.Pessoas.Criar : Permissoes.Pessoas.Editar);

        if (CreditoMudou(dados, anterior))
            _autorizacao.Exigir(Permissoes.Pessoas.AlterarCredito);

        if (dados.TemPapel(TipoPapel.EmpresaDoGrupo) != (anterior?.TemPapel(TipoPapel.EmpresaDoGrupo) ?? false))
            _autorizacao.Exigir(Permissoes.Pessoas.GerenciarEmpresasDoGrupo);
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
