using System.Globalization;
using Lone.Application.Auditoria;
using Lone.Application.Seguranca;
using Lone.Contracts.Auditoria;
using Lone.Domain.CamposPersonalizados;
using Lone.Domain.Entidades;
using Lone.Infrastructure.Persistencia.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Consultas;

public class AuditoriaConsultas : ServicoDadosBase, IAuditoriaConsultas
{
    /// <summary>Campos gravados como código IBGE: o histórico mostra "Curvelo - MG" no lugar do número.</summary>
    private static readonly HashSet<string> CamposDeMunicipio =
        [nameof(Pessoa.NaturalidadeMunicipioId), nameof(PessoaEndereco.MunicipioId)];

    public AuditoriaConsultas(IDbContextFactory<LoneDbContext> fabrica, IUsuarioAtual usuario)
        : base(fabrica, usuario) { }

    public async Task<List<RegistroHistorico>> ListarPorRaizAsync(
        string raizEntidade, Guid raizId, int limite, long? antesDe, CancellationToken ct)
    {
        await using var db = await AbrirAsync(ct);
        var consulta = db.Auditoria.AsNoTracking().Where(a => a.RaizEntidade == raizEntidade && a.RaizId == raizId);

        // Paginação por chave (não por número de página): a próxima página continua exatamente depois do último
        // registro mostrado, mesmo que novas alterações tenham sido gravadas no meio tempo. Usa o índice
        // (RaizEntidade, RaizId, DataHora), que já termina no Id.
        if (antesDe is { } ultimo &&
            await db.Auditoria.AsNoTracking().Where(a => a.Id == ultimo).Select(a => (DateTime?)a.DataHora).FirstOrDefaultAsync(ct) is { } quando)
            consulta = consulta.Where(a => a.DataHora < quando || (a.DataHora == quando && a.Id < ultimo));

        var registros = await consulta
            .OrderByDescending(a => a.DataHora).ThenByDescending(a => a.Id)
            .Take(limite)
            .Select(a => new RegistroHistorico
            {
                Id = a.Id,
                DataHora = a.DataHora,
                Usuario = a.Usuario,
                Origem = a.Origem,
                Acao = a.Acao,
                Entidade = a.Entidade,
                Campo = a.Campo,
                ValorAnterior = a.ValorAnterior,
                ValorNovo = a.ValorNovo,
                Descricao = a.Descricao,
                Motivo = a.Motivo
            })
            .ToListAsync(ct);

        await TraduzirCamposPersonalizadosAsync(db, registros, ct);
        await TraduzirMunicipiosAsync(db, registros, ct);
        await TraduzirEtiquetasAsync(db, registros, ct);
        await TraduzirProfissoesAsync(db, registros, ct);
        await TraduzirOcupacoesCboAsync(db, registros, ct);

        // Referências a cadastros auxiliares gravadas como Id: mostra o nome atual (cadastros nunca são apagados).
        await TraduzirIdsAsync(registros, nameof(MeioContato), nameof(MeioContato.TipoMeioContatoId), "(tipo de telefone/e-mail)",
            ids => db.TiposMeioContato.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Nome, ct));
        await TraduzirIdsAsync(registros, nameof(PessoaEndereco), nameof(PessoaEndereco.TipoEnderecoId), "(tipo de endereço)",
            ids => db.TiposEndereco.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Nome, ct));
        await TraduzirIdsAsync(registros, nameof(PessoaEnderecoFinalidade), nameof(PessoaEnderecoFinalidade.FinalidadeId), "(finalidade)",
            ids => db.FinalidadesEndereco.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Nome, ct));
        await TraduzirIdsAsync(registros, nameof(PessoaEnderecoFinalidade), nameof(PessoaEnderecoFinalidade.PessoaEnderecoId), "(endereço)",
            ids => db.PessoaEnderecos.AsNoTracking().Where(e => ids.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.Logradouro + (e.Numero != null ? ", " + e.Numero : "") + " - " + e.Cidade, ct));
        await TraduzirIdsAsync(registros, nameof(PessoaEndereco), nameof(PessoaEndereco.MescladoEmId), "(endereço)",
            ids => db.PessoaEnderecos.AsNoTracking().Where(e => ids.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.Logradouro + (e.Numero != null ? ", " + e.Numero : "") + " - " + e.Cidade, ct));
        await TraduzirIdsAsync(registros, nameof(PessoaDocumento), nameof(PessoaDocumento.TipoDocumentoId), "(tipo de documento)",
            ids => db.TiposDocumento.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Nome, ct));
        await TraduzirIdsAsync(registros, nameof(CampoPersonalizado), nameof(CampoPersonalizado.TipoDocumentoId), "(tipo de documento)",
            ids => db.TiposDocumento.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Nome, ct));
        await TraduzirIdsAsync(registros, nameof(PessoaPapel), nameof(PessoaPapel.PapelId), "(papel)",
            ids => db.Papeis.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Nome, ct));
        await TraduzirIdsAsync(registros, nameof(AnexoDocumento), nameof(AnexoDocumento.PessoaDocumentoId), "(documento)",
            ids => db.PessoaDocumentos.AsNoTracking().Where(d => ids.Contains(d.Id))
                .Join(db.TiposDocumento, d => d.TipoDocumentoId, t => t.Id, (d, t) => new { d.Id, Texto = t.Nome + " " + d.Numero })
                .ToDictionaryAsync(x => x.Id, x => x.Texto, ct));
        await TraduzirIdsAsync(registros, nameof(DocumentoValorPersonalizado), nameof(DocumentoValorPersonalizado.PessoaDocumentoId), "(documento)",
            ids => db.PessoaDocumentos.AsNoTracking().Where(d => ids.Contains(d.Id))
                .Join(db.TiposDocumento, d => d.TipoDocumentoId, t => t.Id, (d, t) => new { d.Id, Texto = t.Nome + " " + d.Numero })
                .ToDictionaryAsync(x => x.Id, x => x.Texto, ct));

        // Estrutura empresarial: grupo da empresa, relacionamentos (tipo e a outra pessoa) e condição do fornecedor.
        await TraduzirIdsAsync(registros, nameof(Pessoa), nameof(Pessoa.GrupoEmpresarialId), "(grupo empresarial)",
            ids => db.GruposEmpresariais.AsNoTracking().Where(g => ids.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Nome, ct));
        await TraduzirIdsAsync(registros, nameof(PessoaRelacionamento), nameof(PessoaRelacionamento.TipoRelacionamentoId), "(tipo de relacionamento)",
            ids => db.TiposRelacionamento.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Nome, ct));
        await TraduzirIdsAsync(registros, nameof(PessoaRelacionamento), nameof(PessoaRelacionamento.PessoaDestinoId), "(cadastro)",
            ids => db.Pessoas.AsNoTracking().Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Nome + " (código " + p.Codigo + ")", ct));
        await TraduzirIdsAsync(registros, nameof(ContaFornecedor), nameof(ContaFornecedor.CondicaoPagamentoId), "(condição de pagamento)",
            ids => db.CondicoesPagamento.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Nome, ct));

        // Gravado em UTC; marcado como tal para o aplicativo converter para o fuso do aparelho.
        foreach (var r in registros)
            r.DataHora = DateTime.SpecifyKind(r.DataHora, DateTimeKind.Utc);
        return registros;
    }

    /// <summary>
    /// Valores personalizados são gravados com o Id do campo e da opção (os nomes podem mudar depois);
    /// aqui viram o nome atual do campo e o texto da opção. Campos e opções nunca são apagados.
    /// </summary>
    private static async Task TraduzirCamposPersonalizadosAsync(LoneDbContext db, List<RegistroHistorico> registros, CancellationToken ct)
    {
        var prefixo = PessoaValorPersonalizado.PrefixoAuditoria;
        var idsCampos = registros
            .Select(r => r.Campo is { } c && c.StartsWith(prefixo, StringComparison.Ordinal) && Guid.TryParse(c[prefixo.Length..], out var id) ? id : (Guid?)null)
            .OfType<Guid>().Distinct().ToList();
        if (idsCampos.Count == 0) return;

        var nomes = await db.CamposPersonalizados.AsNoTracking()
            .Where(c => idsCampos.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Nome, ct);
        var opcoes = await db.CampoPersonalizadoOpcoes.AsNoTracking()
            .Where(o => idsCampos.Contains(o.CampoId)).ToDictionaryAsync(o => o.Id, o => o.Texto, ct);

        foreach (var r in registros.Where(r => r.Campo?.StartsWith(prefixo, StringComparison.Ordinal) == true))
        {
            if (Guid.TryParse(r.Campo![prefixo.Length..], out var campoId))
                r.Campo = nomes.GetValueOrDefault(campoId, "Campo personalizado");
            r.ValorAnterior = TextoOpcao(r.ValorAnterior, opcoes);
            r.ValorNovo = TextoOpcao(r.ValorNovo, opcoes);
        }
    }

    private static string? TextoOpcao(string? valor, IReadOnlyDictionary<Guid, string> opcoes) =>
        valor is not null && valor.StartsWith(ValorCampo.PrefixoOpcao, StringComparison.Ordinal) &&
        Guid.TryParse(valor[ValorCampo.PrefixoOpcao.Length..], out var id)
            ? opcoes.GetValueOrDefault(id, "(opção)")
            : valor;

    /// <summary>Troca o Id gravado em (Entidade, Campo) pelo nome atual; Id que não existe mais vira o texto "desconhecido".</summary>
    private static async Task TraduzirIdsAsync(List<RegistroHistorico> registros, string entidade, string campo, string desconhecido,
                                               Func<List<Guid>, Task<Dictionary<Guid, string>>> nomesDe)
    {
        var alvo = registros.Where(r => r.Entidade == entidade && r.Campo == campo).ToList();
        if (alvo.Count == 0) return;

        var ids = alvo.SelectMany(r => new[] { r.ValorAnterior, r.ValorNovo })
            .Select(v => Guid.TryParse(v, out var id) ? id : (Guid?)null)
            .OfType<Guid>().Distinct().ToList();
        var nomes = ids.Count == 0 ? new Dictionary<Guid, string>() : await nomesDe(ids);

        foreach (var r in alvo)
        {
            r.ValorAnterior = Nome(r.ValorAnterior);
            r.ValorNovo = Nome(r.ValorNovo);
        }

        string? Nome(string? valor) => Guid.TryParse(valor, out var id) ? nomes.GetValueOrDefault(id, desconhecido) : valor;
    }

    /// <summary>Etiqueta marcada/desmarcada: o valor gravado é o Id da etiqueta; mostra o nome (mesmo renomeada, o nome atual).</summary>
    private static async Task TraduzirEtiquetasAsync(LoneDbContext db, List<RegistroHistorico> registros, CancellationToken ct)
    {
        var alvo = registros
            .Where(r => r.Entidade == nameof(PessoaEtiqueta) && r.Campo == nameof(PessoaEtiqueta.EtiquetaId))
            .ToList();
        if (alvo.Count == 0) return;

        var ids = alvo.SelectMany(r => new[] { r.ValorAnterior, r.ValorNovo })
            .Select(v => Guid.TryParse(v, out var id) ? id : (Guid?)null)
            .OfType<Guid>().Distinct().ToList();
        var nomes = await db.Etiquetas.AsNoTracking()
            .Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Nome, ct);

        foreach (var r in alvo)
        {
            r.ValorAnterior = Nome(r.ValorAnterior);
            r.ValorNovo = Nome(r.ValorNovo);
        }

        string? Nome(string? valor) =>
            Guid.TryParse(valor, out var id) ? nomes.GetValueOrDefault(id, "(etiqueta)") : valor;
    }

    /// <summary>Profissão da pessoa: o valor gravado é o Id da profissão; mostra o nome atual.</summary>
    private static async Task TraduzirProfissoesAsync(LoneDbContext db, List<RegistroHistorico> registros, CancellationToken ct)
    {
        var alvo = registros
            .Where(r => r.Entidade == nameof(Pessoa) && r.Campo == nameof(Pessoa.ProfissaoId))
            .ToList();
        if (alvo.Count == 0) return;

        var ids = alvo.SelectMany(r => new[] { r.ValorAnterior, r.ValorNovo })
            .Select(v => Guid.TryParse(v, out var id) ? id : (Guid?)null)
            .OfType<Guid>().Distinct().ToList();
        var nomes = await db.Profissoes.AsNoTracking()
            .Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Nome, ct);

        foreach (var r in alvo)
        {
            r.ValorAnterior = Nome(r.ValorAnterior);
            r.ValorNovo = Nome(r.ValorNovo);
        }

        string? Nome(string? valor) =>
            Guid.TryParse(valor, out var id) ? nomes.GetValueOrDefault(id, "(profissão)") : valor;
    }

    /// <summary>Ocupação CBO de uma profissão: o valor gravado é o código; mostra "0000-00 · título".</summary>
    private static async Task TraduzirOcupacoesCboAsync(LoneDbContext db, List<RegistroHistorico> registros, CancellationToken ct)
    {
        var alvo = registros
            .Where(r => r.Entidade == nameof(Profissao) && r.Campo == nameof(Profissao.OcupacaoCboId))
            .ToList();
        if (alvo.Count == 0) return;

        var codigos = alvo.SelectMany(r => new[] { r.ValorAnterior, r.ValorNovo })
            .Select(v => int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : (int?)null)
            .OfType<int>().Distinct().ToList();
        var titulos = await db.OcupacoesCbo.AsNoTracking()
            .Where(o => codigos.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Titulo, ct);

        foreach (var r in alvo)
        {
            r.ValorAnterior = Texto(r.ValorAnterior);
            r.ValorNovo = Texto(r.ValorNovo);
        }

        string? Texto(string? valor) =>
            int.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var codigo)
                ? $"{OcupacaoCbo.Formatar(codigo)} · {titulos.GetValueOrDefault(codigo, "(fora da tabela)")}"
                : valor;
    }

    private static async Task TraduzirMunicipiosAsync(LoneDbContext db, List<RegistroHistorico> registros, CancellationToken ct)
    {
        var alvo = registros.Where(r => r.Campo is { } c && CamposDeMunicipio.Contains(c)).ToList();
        if (alvo.Count == 0) return;

        var codigos = alvo.SelectMany(r => new[] { r.ValorAnterior, r.ValorNovo })
            .Select(v => int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : (int?)null)
            .OfType<int>().Distinct().ToList();
        var nomes = await db.Municipios.AsNoTracking()
            .Where(m => codigos.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Nome + " - " + m.Uf, ct);

        foreach (var r in alvo)
        {
            r.ValorAnterior = Nome(r.ValorAnterior);
            r.ValorNovo = Nome(r.ValorNovo);
        }

        string? Nome(string? valor) =>
            int.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var codigo) && nomes.TryGetValue(codigo, out var nome)
                ? nome
                : valor;
    }
}
