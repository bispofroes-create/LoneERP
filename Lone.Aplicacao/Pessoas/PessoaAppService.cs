using Lone.Aplicacao.Auditoria;
using Lone.Aplicacao.Seguranca;
using Lone.Core.Entidades;
using Lone.Core.Enums;
using Lone.Core.Validacao;

namespace Lone.Aplicacao.Pessoas;

/// <summary>
/// Orquestra o cadastro de pessoas: permissão → normalização → regras → duplicidade → gravação.
/// Não conhece banco nem tela.
/// </summary>
public sealed class PessoaAppService : IPessoaAppService
{
    private const int LimiteHistorico = 500;

    private readonly IPessoaRepositorio _repositorio;
    private readonly IAuditoriaConsultas _auditoria;
    private readonly IAutorizacao _autorizacao;

    public PessoaAppService(IPessoaRepositorio repositorio, IAuditoriaConsultas auditoria, IAutorizacao autorizacao)
    {
        _repositorio = repositorio;
        _auditoria = auditoria;
        _autorizacao = autorizacao;
    }

    public Task<List<PessoaResumo>> ListarAsync(FiltroPessoas filtro, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return _repositorio.ListarAsync(filtro, ct);
    }

    public Task<Pessoa?> ObterAsync(int id, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return _repositorio.ObterAsync(id, ct);
    }

    public Task<int> ContarClientesAtivosAsync(CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return _repositorio.ContarClientesAtivosAsync(ct);
    }

    public Task<List<RegistroHistorico>> ListarHistoricoAsync(int pessoaId, CancellationToken ct = default)
    {
        _autorizacao.Exigir(Permissoes.Pessoas.Visualizar);
        return _auditoria.ListarPorRaizAsync(nameof(Pessoa), pessoaId, LimiteHistorico, ct);
    }

    public async Task<ResultadoSalvar> SalvarAsync(Pessoa dados, CancellationToken ct = default)
    {
        var anterior = dados.Id == 0 ? null : await _repositorio.ObterAsync(dados.Id, ct);
        if (dados.Id != 0 && anterior is null)
            throw new ValidacaoException(["Este cadastro não existe mais. Ele pode ter sido removido."]);

        if (anterior?.Situacao == SituacaoPessoa.Arquivado)
            throw new ValidacaoException(["Cadastro arquivado é somente leitura."]);

        PessoaNormalizador.Normalizar(dados);
        ExigirPermissoes(dados, anterior);

        var erros = PessoaValidador.Validar(dados);

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

        var avisos = await BuscarAvisosDeDuplicidadeAsync(dados, ct);
        var salva = await _repositorio.SalvarAsync(dados, OrigemAlteracao.Usuario, ct);
        return new ResultadoSalvar(salva, avisos);
    }

    /// <summary>Criar/editar, e permissões extras quando mudam situação, crédito ou o papel de empresa do grupo.</summary>
    private void ExigirPermissoes(Pessoa dados, Pessoa? anterior)
    {
        _autorizacao.Exigir(anterior is null ? Permissoes.Pessoas.Criar : Permissoes.Pessoas.Editar);

        var situacaoAnterior = anterior?.Situacao ?? SituacaoPessoa.Ativo;
        if (dados.Situacao != situacaoAnterior &&
            (dados.Situacao is SituacaoPessoa.Inativo or SituacaoPessoa.Arquivado ||
             situacaoAnterior is SituacaoPessoa.Inativo))
            _autorizacao.Exigir(Permissoes.Pessoas.Inativar);

        if (CreditoMudou(dados, anterior))
            _autorizacao.Exigir(Permissoes.Pessoas.AlterarCredito);

        if (dados.TemPapel(TipoPapel.EmpresaDoGrupo) != (anterior?.TemPapel(TipoPapel.EmpresaDoGrupo) ?? false))
            _autorizacao.Exigir(Permissoes.Pessoas.GerenciarEmpresasDoGrupo);
    }

    /// <summary>Compara os dados de crédito de cada conta de cliente (por empresa) antes e depois.</summary>
    private static bool CreditoMudou(Pessoa dados, Pessoa? anterior)
    {
        var semConta = ((decimal?)null, (int?)null, (decimal?)null, true);

        static Dictionary<int, (decimal?, int?, decimal?, bool)> PorEmpresa(IEnumerable<ContaCliente> contas) =>
            contas.GroupBy(c => c.EmpresaId ?? 0).ToDictionary(
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
        var contatos = p.MeiosContato.Select(m => m.Valor)
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
