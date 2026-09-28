using System.Globalization;
using System.Linq.Expressions;
using Lone.Application.Consultas;
using Lone.Contracts.Pessoas;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia.Configuracoes;
using Microsoft.EntityFrameworkCore;

namespace Lone.Infrastructure.Persistencia.Consultas;

/// <summary>
/// A condição no banco de cada campo do catálogo (CatalogoFiltrosPessoas), pelo mesmo Id. Tudo em LINQ com parâmetros:
/// nunca SQL montado com texto. As condições chegam já conferidas pelo serviço (campo, operador e valores válidos).
/// Cada campo usa o índice que a consulta avançada já usava (papéis, etiquetas, CNAE, carteira, interações, validade, UF).
/// </summary>
public static class FiltrosPessoasSql
{
    /// <summary>O que as condições precisam além da consulta: o banco, o dia de hoje e os parâmetros de relacionamento.</summary>
    public sealed record Contexto(LoneDbContext Db, DateOnly Hoje, ParametrosRelacionamento Parametros);

    private delegate IQueryable<Pessoa> Condicao(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x);

    private static readonly Dictionary<string, Condicao> PorCampo = new(StringComparer.Ordinal)
    {
        [CamposFiltroPessoas.Natureza] = Natureza,
        [CamposFiltroPessoas.Papeis] = Papeis,
        [CamposFiltroPessoas.Etiquetas] = Etiquetas,
        [CamposFiltroPessoas.Uf] = Uf,
        [CamposFiltroPessoas.Municipio] = Municipio,
        [CamposFiltroPessoas.MunicipioACorrigir] = MunicipioACorrigir,
        [CamposFiltroPessoas.DocumentosVencidos] = DocumentosVencidos,
        [CamposFiltroPessoas.DocumentosVencendo] = DocumentosVencendo,
        [CamposFiltroPessoas.DocumentosVencendoPeloAviso] = DocumentosVencendoPeloAviso,
        [CamposFiltroPessoas.ComPendenciaCadastral] = ComPendenciaCadastral,
        [CamposFiltroPessoas.ProdutorRural] = ProdutorRural,
        [CamposFiltroPessoas.Regime] = Regime,
        [CamposFiltroPessoas.Cnae] = (q, c, x) => Cnae(q, c, x, somentePrincipal: false),
        [CamposFiltroPessoas.CnaePrincipal] = (q, c, x) => Cnae(q, c, x, somentePrincipal: true),
        [CamposFiltroPessoas.Vendedor] = Vendedor,
        [CamposFiltroPessoas.SemCarteira] = SemCarteira,
        [CamposFiltroPessoas.Relacionamento] = Relacionamento,
        [CamposFiltroPessoas.SemInteracao] = SemInteracao,
        [CamposFiltroPessoas.Situacao] = Situacao,
        [CamposFiltroPessoas.Bloqueado] = Bloqueado,
        [CamposFiltroPessoas.CadastradoEm] = CadastradoEm,

        // ---- Lista com colunas (linha de filtro das colunas) ----
        [CamposFiltroPessoas.Nome] = (q, c, x) =>
            // O cast só ajusta a nulidade (string → string?): o filtro de texto aceita campos que podem ser nulos.
            q.Where(Ou(Texto<Pessoa>(p => p.Nome, c),
                       Texto((Expression<Func<Pessoa, string?>>)Repositorios.PessoaRepositorio.NomeParaExibirNoBanco, c))),
        [CamposFiltroPessoas.Codigo] = (q, c, x) => q.Where(Faixa<Pessoa>(p => (decimal?)p.Codigo, c)),
        [CamposFiltroPessoas.Documento] = (q, c, x) =>
        {
            var porCnpj = x.Db.Estabelecimentos.Where(Texto<Estabelecimento>(e => e.Cnpj, c)).Select(e => e.PessoaId);
            Expression<Func<Pessoa, bool>> temCnpj = p => porCnpj.Contains(p.Id);
            return q.Where(Ou(Texto<Pessoa>(p => p.DocumentoPrincipal, c), temCnpj));
        },
        [CamposFiltroPessoas.Cidade] = (q, c, x) =>
            ComAlgum(q, x.Db.PessoaEnderecos.Where(e => e.Ativo).Where(Texto<PessoaEndereco>(e => e.Cidade, c)), e => e.PessoaId),

        // ---- Fase 3 ----
        [CamposFiltroPessoas.NomeFantasia] = (q, c, x) =>
            ComAlgum(q, x.Db.Estabelecimentos.Where(Texto<Estabelecimento>(e => e.NomeFantasia, c)), e => e.PessoaId),
        [CamposFiltroPessoas.DataNascimento] = (q, c, x) => q.Where(Faixa<Pessoa>(p => p.DataNascimento, c)),
        [CamposFiltroPessoas.Aniversario] = Aniversario,
        [CamposFiltroPessoas.Idade] = Idade,
        [CamposFiltroPessoas.DataAbertura] = (q, c, x) => q.Where(Faixa<Pessoa>(p => p.DataAbertura, c)),
        [CamposFiltroPessoas.Porte] = (q, c, x) => Lista(q, c, c.Valores.ToList(), valores => p => valores.Contains(p.Porte!)),
        [CamposFiltroPessoas.NaturezaJuridica] = (q, c, x) =>
        {
            var naturezas = c.Valores.ToList();
            var com = x.Db.Estabelecimentos.Where(e => e.Ativo && naturezas.Contains(e.NaturezaJuridica!));
            return Nenhum(c) ? SemNenhum(q, com, e => e.PessoaId) : ComAlgum(q, com, e => e.PessoaId);
        },
        [CamposFiltroPessoas.GrupoEmpresarial] = (q, c, x) =>
        {
            List<Guid?> grupos = [.. Guids(c).Select(g => (Guid?)g)];
            return Nenhum(c) ? q.Where(p => !grupos.Contains(p.GrupoEmpresarialId)) : q.Where(p => grupos.Contains(p.GrupoEmpresarialId));
        },
        [CamposFiltroPessoas.Sexo] = (q, c, x) => Lista(q, c, Enums<SexoRegistro>(c), valores => p => valores.Contains(p.Sexo)),
        [CamposFiltroPessoas.EstadoCivil] = (q, c, x) => Lista(q, c, Enums<EstadoCivil>(c), valores => p => valores.Contains(p.EstadoCivil)),
        [CamposFiltroPessoas.Profissao] = (q, c, x) =>
        {
            List<Guid?> profissoes = [.. Guids(c).Select(g => (Guid?)g)];
            return Nenhum(c) ? q.Where(p => !profissoes.Contains(p.ProfissaoId)) : q.Where(p => profissoes.Contains(p.ProfissaoId));
        },
        [CamposFiltroPessoas.TipoContato] = (q, c, x) =>
        {
            var tipos = Enums<TipoContato>(c);
            var com = x.Db.MeiosContato.Where(m => m.Ativo && tipos.Contains(m.Tipo));
            return Nenhum(c) ? SemNenhum(q, com, m => m.PessoaId) : ComAlgum(q, com, m => m.PessoaId);
        },
        [CamposFiltroPessoas.Telefone] = (q, c, x) =>
        {
            var meios = x.Db.MeiosContato.Where(m => m.Ativo && m.Tipo != TipoContato.Email).Where(Texto<MeioContato>(m => m.Valor, c));
            var telefones = x.Db.Contatos.Where(Texto<Contato>(y => y.Telefone, c));
            var celulares = x.Db.Contatos.Where(Texto<Contato>(y => y.Celular, c));
            var ids = meios.Select(m => m.PessoaId).Concat(telefones.Select(y => y.PessoaId)).Concat(celulares.Select(y => y.PessoaId));
            return q.Where(p => ids.Contains(p.Id));
        },
        [CamposFiltroPessoas.Ddd] = (q, c, x) =>
        {
            // Gravado só com dígitos e DDD na frente (internacional começa com "+", 0800 com "0": não entram).
            var ddd = c.Valores[0];
            var meios = x.Db.MeiosContato.Where(m => m.Ativo && m.Tipo != TipoContato.Email && m.Valor.StartsWith(ddd));
            var contatos = x.Db.Contatos.Where(y => (y.Telefone != null && y.Telefone.StartsWith(ddd)) ||
                                                    (y.Celular != null && y.Celular.StartsWith(ddd)));
            var ids = meios.Select(m => m.PessoaId).Concat(contatos.Select(y => y.PessoaId));
            return q.Where(p => ids.Contains(p.Id));
        },
        [CamposFiltroPessoas.Email] = (q, c, x) =>
        {
            var meios = x.Db.MeiosContato.Where(m => m.Ativo && m.Tipo == TipoContato.Email).Where(Texto<MeioContato>(m => m.Valor, c));
            var contatos = x.Db.Contatos.Where(Texto<Contato>(y => y.Email, c));
            var ids = meios.Select(m => m.PessoaId).Concat(contatos.Select(y => y.PessoaId));
            return q.Where(p => ids.Contains(p.Id));
        },
        [CamposFiltroPessoas.WhatsApp] = (q, c, x) =>
            SimOuNao(q, c, x.Db.MeiosContato.Where(m => m.Ativo && (m.WhatsApp || m.Tipo == TipoContato.WhatsApp)), m => m.PessoaId),
        [CamposFiltroPessoas.FinalidadeContato] = FinalidadeContato,
        [CamposFiltroPessoas.AceitaComunicacoes] = (q, c, x) =>
            SimOuNao(q, c, x.Db.MeiosContato.Where(m => m.Ativo && m.PermiteComunicacao), m => m.PessoaId),
        [CamposFiltroPessoas.SemContato] = (q, c, x) =>
        {
            var com = x.Db.MeiosContato.Where(m => m.Ativo && m.Valor != "");
            return c.Operador == OperadorFiltro.Sim ? SemNenhum(q, com, m => m.PessoaId) : ComAlgum(q, com, m => m.PessoaId);
        },
        [CamposFiltroPessoas.CargoContato] = (q, c, x) =>
            ComAlgum(q, x.Db.Contatos.Where(Texto<Contato>(y => y.Cargo, c)), y => y.PessoaId),
        [CamposFiltroPessoas.FinalidadeEndereco] = (q, c, x) =>
        {
            var finalidades = Guids(c);
            var com = x.Db.PessoaEnderecoFinalidades.Where(f => f.Ativo && finalidades.Contains(f.FinalidadeId));
            return Nenhum(c) ? SemNenhum(q, com, f => f.PessoaId) : ComAlgum(q, com, f => f.PessoaId);
        },
        [CamposFiltroPessoas.Bairro] = (q, c, x) =>
            ComAlgum(q, x.Db.PessoaEnderecos.Where(e => e.Ativo).Where(Texto<PessoaEndereco>(e => e.Bairro, c)), e => e.PessoaId),
        [CamposFiltroPessoas.Cep] = (q, c, x) =>
            ComAlgum(q, x.Db.PessoaEnderecos.Where(e => e.Ativo).Where(Texto<PessoaEndereco>(e => e.Cep, c)), e => e.PessoaId),
        [CamposFiltroPessoas.SemEndereco] = (q, c, x) =>
        {
            var com = x.Db.PessoaEnderecos.Where(e => e.Ativo && e.Logradouro != "");
            return c.Operador == OperadorFiltro.Sim ? SemNenhum(q, com, e => e.PessoaId) : ComAlgum(q, com, e => e.PessoaId);
        },
        [CamposFiltroPessoas.TipoDocumento] = (q, c, x) =>
        {
            var tipos = Guids(c);
            var com = x.Db.PessoaDocumentos.Where(d => d.Ativo && tipos.Contains(d.TipoDocumentoId));
            return Nenhum(c) ? SemNenhum(q, com, d => d.PessoaId) : ComAlgum(q, com, d => d.PessoaId);
        },
        [CamposFiltroPessoas.DocumentoValidoAte] = (q, c, x) =>
            ComAlgum(q, x.Db.PessoaDocumentos.Where(d => d.Ativo).Where(Faixa<PessoaDocumento>(d => d.ValidoAte, c)), d => d.PessoaId),
        [CamposFiltroPessoas.IndicadorIE] = (q, c, x) =>
        {
            var indicadores = Enums<IndicadorIE>(c);
            var com = x.Db.Estabelecimentos.Where(e => e.Ativo && indicadores.Contains(e.IndicadorIE));
            return Nenhum(c) ? SemNenhum(q, com, e => e.PessoaId) : ComAlgum(q, com, e => e.PessoaId);
        },
        [CamposFiltroPessoas.InscricaoEstadual] = (q, c, x) =>
            ComAlgum(q, x.Db.Estabelecimentos.Where(e => e.Ativo).Where(Texto<Estabelecimento>(e => e.InscricaoEstadual, c)), e => e.PessoaId),
        [CamposFiltroPessoas.SituacaoReceita] = (q, c, x) =>
        {
            var situacoes = c.Valores.ToList();
            var com = x.Db.Estabelecimentos.Where(e => e.Ativo && situacoes.Contains(e.SituacaoReceita!));
            return Nenhum(c) ? SemNenhum(q, com, e => e.PessoaId) : ComAlgum(q, com, e => e.PessoaId);
        },
        [CamposFiltroPessoas.LimiteCredito] = (q, c, x) =>
            ComAlgum(q, x.Db.ContasCliente.Where(Faixa<ContaCliente>(y => y.LimiteCredito, c)), y => y.PessoaId),
        [CamposFiltroPessoas.PerfilComercial] = (q, c, x) =>
        {
            List<Guid?> perfis = [.. Guids(c).Select(g => (Guid?)g)];
            var com = x.Db.ContasCliente.Where(y => perfis.Contains(y.PerfilComercialId));
            return Nenhum(c) ? SemNenhum(q, com, y => y.PessoaId) : ComAlgum(q, com, y => y.PessoaId);
        },
        [CamposFiltroPessoas.CondicaoCliente] = (q, c, x) =>
        {
            List<Guid?> condicoes = [.. Guids(c).Select(g => (Guid?)g)];
            var com = x.Db.ContasCliente.Where(y => condicoes.Contains(y.CondicaoPagamentoId));
            return Nenhum(c) ? SemNenhum(q, com, y => y.PessoaId) : ComAlgum(q, com, y => y.PessoaId);
        },
        [CamposFiltroPessoas.CondicaoFornecedor] = (q, c, x) =>
        {
            List<Guid?> condicoes = [.. Guids(c).Select(g => (Guid?)g)];
            var com = x.Db.ContasFornecedor.Where(y => condicoes.Contains(y.CondicaoPagamentoId));
            return Nenhum(c) ? SemNenhum(q, com, y => y.PessoaId) : ComAlgum(q, com, y => y.PessoaId);
        },
        [CamposFiltroPessoas.AvaliacaoFornecedor] = (q, c, x) =>
            ComAlgum(q, x.Db.ContasFornecedor.Where(Faixa<ContaFornecedor>(y => (decimal?)y.Avaliacao, c)), y => y.PessoaId),
        [CamposFiltroPessoas.EmpresaVinculo] = (q, c, x) =>
        {
            var empresas = Guids(c);
            var com = x.Db.VinculosColaborador.Where(v => empresas.Contains(v.EmpresaId));
            return Nenhum(c) ? SemNenhum(q, com, v => v.PessoaId) : ComAlgum(q, com, v => v.PessoaId);
        },
        [CamposFiltroPessoas.TipoVinculo] = (q, c, x) =>
        {
            var tipos = Enums<TipoVinculo>(c);
            var com = x.Db.VinculosColaborador.Where(v => tipos.Contains(v.Tipo));
            return Nenhum(c) ? SemNenhum(q, com, v => v.PessoaId) : ComAlgum(q, com, v => v.PessoaId);
        },
        [CamposFiltroPessoas.Admissao] = (q, c, x) =>
            ComAlgum(q, x.Db.VinculosColaborador.Where(Faixa<VinculoColaborador>(v => (DateOnly?)v.AdmissaoEm, c)), v => v.PessoaId),
        [CamposFiltroPessoas.ColaboradorAtivo] = ColaboradorAtivo,
        [CamposFiltroPessoas.Cargo] = (q, c, x) => Lotacao(q, c, x, l => l.CargoId),
        [CamposFiltroPessoas.Departamento] = (q, c, x) => Lotacao(q, c, x, l => l.DepartamentoId),
        [CamposFiltroPessoas.Setor] = (q, c, x) => Lotacao(q, c, x, l => l.SetorId),
        [CamposFiltroPessoas.TipoRelacionamento] = TipoRelacionamento,
        [CamposFiltroPessoas.PessoaRelacionada] = PessoaRelacionada,
        [CamposFiltroPessoas.Origem] = (q, c, x) => Lista(q, c, c.Valores.ToList(), valores => p => valores.Contains(p.OrigemCadastro!)),
        [CamposFiltroPessoas.ConsentimentoEmVigor] = (q, c, x) =>
        {
            var finalidades = Guids(c);
            var com = x.Db.PessoaConsentimentos.Where(y => y.Concedido && y.RevogadoEm == null && finalidades.Contains(y.FinalidadeId));
            return Nenhum(c) ? SemNenhum(q, com, y => y.PessoaId) : ComAlgum(q, com, y => y.PessoaId);
        },
        [CamposFiltroPessoas.ConsentimentoRevogado] = (q, c, x) =>
        {
            var finalidades = Guids(c);
            return ComAlgum(q, x.Db.PessoaConsentimentos.Where(y => y.RevogadoEm != null && finalidades.Contains(y.FinalidadeId)), y => y.PessoaId);
        },
        [CamposFiltroPessoas.CanalConsentimento] = (q, c, x) =>
        {
            List<CanalComunicacao?> canais = [.. Enums<CanalComunicacao>(c).Select(v => (CanalComunicacao?)v)];
            return ComAlgum(q, x.Db.PessoaConsentimentos.Where(y => y.Concedido && y.RevogadoEm == null && canais.Contains(y.Canal)), y => y.PessoaId);
        },
        [CamposFiltroPessoas.EscopoBloqueio] = (q, c, x) =>
        {
            var escopos = Enums<EscopoBloqueio>(c);
            return ComAlgum(q, x.Db.Bloqueios.Where(b => b.FimEm == null && escopos.Contains(b.Escopo)), b => b.PessoaId);
        },
        [CamposFiltroPessoas.AlteradoEm] = AlteradoEm
    };

    /// <summary>Campos com condição implementada (um teste confere contra o catálogo).</summary>
    public static IReadOnlyCollection<string> Implementados => PorCampo.Keys;

    /// <summary>Aplica as condições em sequência (E entre condições; dentro de uma lista, "um destes" = OU).</summary>
    public static IQueryable<Pessoa> Aplicar(IQueryable<Pessoa> q, IEnumerable<CondicaoFiltro> condicoes, Contexto x)
    {
        foreach (var c in condicoes)
        {
            if (CamposFiltroPessoas.IdCampoPersonalizado(c.Campo) is { } campoId)
                q = CampoPersonalizado(q, c, campoId);
            else if (PorCampo.TryGetValue(c.Campo, out var aplicar))
                q = aplicar(q, c, x);
            else
                throw new InvalidOperationException($"Campo de filtro sem condição no banco: {c.Campo}");
        }
        return q;
    }

    // ---------------------------------------------------------------- Valores

    private static List<T> Enums<T>(CondicaoFiltro c) where T : struct, Enum =>
        c.Valores.Select(v => Enum.Parse<T>(v)).ToList();

    private static List<Guid> Guids(CondicaoFiltro c) => c.Valores.Select(Guid.Parse).ToList();

    private static int Inteiro(CondicaoFiltro c, int i = 0) => int.Parse(c.Valores[i], CultureInfo.InvariantCulture);

    private static DateOnly Data(CondicaoFiltro c, int i) => DateOnly.ParseExact(c.Valores[i], "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool Nenhum(CondicaoFiltro c) => c.Operador == OperadorFiltro.NenhumDestes;

    // ---------------------------------------------------------------- Identificação

    private static IQueryable<Pessoa> Natureza(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var naturezas = Enums<NaturezaPessoa>(c);
        return Nenhum(c) ? q.Where(p => !naturezas.Contains(p.Natureza)) : q.Where(p => naturezas.Contains(p.Natureza));
    }

    private static IQueryable<Pessoa> Papeis(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var papeis = Guids(c);
        return c.Operador switch
        {
            OperadorFiltro.TodosDestes => papeis.Aggregate(q, (atual, papel) => atual.Where(p => p.Papeis.Any(y => y.Ativo && y.PapelId == papel))),
            OperadorFiltro.NenhumDestes => q.Where(p => !p.Papeis.Any(y => y.Ativo && papeis.Contains(y.PapelId))),
            _ => q.Where(p => p.Papeis.Any(y => y.Ativo && papeis.Contains(y.PapelId)))
        };
    }

    private static IQueryable<Pessoa> Etiquetas(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var etiquetas = Guids(c);
        return Nenhum(c)
            ? q.Where(p => !p.Etiquetas.Any(e => etiquetas.Contains(e.EtiquetaId)))
            : q.Where(p => p.Etiquetas.Any(e => etiquetas.Contains(e.EtiquetaId)));
    }

    // ---------------------------------------------------------------- Endereços

    private static IQueryable<Pessoa> Uf(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var ufs = c.Valores.ToList();
        return Nenhum(c)
            ? q.Where(p => !p.Enderecos.Any(e => e.Ativo && ufs.Contains(e.Uf!)))
            : q.Where(p => p.Enderecos.Any(e => e.Ativo && ufs.Contains(e.Uf!)));
    }

    private static IQueryable<Pessoa> Municipio(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        List<int?> municipios = [.. c.Valores.Select(v => (int?)int.Parse(v, CultureInfo.InvariantCulture))];
        return Nenhum(c)
            ? q.Where(p => !p.Enderecos.Any(e => e.Ativo && municipios.Contains(e.MunicipioId)))
            : q.Where(p => p.Enderecos.Any(e => e.Ativo && municipios.Contains(e.MunicipioId)));
    }

    private static IQueryable<Pessoa> MunicipioACorrigir(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var db = x.Db;
        return c.Operador == OperadorFiltro.Sim
            ? q.Where(p => db.PendenciasMunicipio.Any(y => y.PessoaId == p.Id && y.ResolvidaEm == null))
            : q.Where(p => !db.PendenciasMunicipio.Any(y => y.PessoaId == p.Id && y.ResolvidaEm == null));
    }

    // ---------------------------------------------------------------- Documentos

    private static IQueryable<Pessoa> DocumentosVencidos(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var hoje = x.Hoje;
        return c.Operador == OperadorFiltro.Sim
            ? q.Where(p => p.Documentos.Any(d => d.Ativo && d.ValidoAte != null && d.ValidoAte < hoje))
            : q.Where(p => !p.Documentos.Any(d => d.Ativo && d.ValidoAte != null && d.ValidoAte < hoje));
    }

    private static IQueryable<Pessoa> DocumentosVencendo(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var hoje = x.Hoje;
        var ate = hoje.AddDays(Inteiro(c));
        return q.Where(p => p.Documentos.Any(d => d.Ativo && d.ValidoAte != null && d.ValidoAte >= hoje && d.ValidoAte <= ate));
    }

    /// <summary>
    /// Vence em breve pela antecedência do próprio tipo (a regra de RegrasDocumento.Situacao, usada no resumo da pessoa):
    /// hoje ≤ válido até ≤ hoje + dias de aviso do tipo. Tipo que não está no cadastro usa o aviso padrão (como a tela).
    /// </summary>
    private static IQueryable<Pessoa> DocumentosVencendoPeloAviso(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var db = x.Db;
        var hoje = x.Hoje;
        var padrao = hoje.AddDays(TipoDocumentoCadastro.DiasAvisoPadrao);
        var vencendo = db.PessoaDocumentos.Where(d => d.Ativo && d.ValidoAte != null && d.ValidoAte >= hoje &&
            (db.TiposDocumento.Any(t => t.Id == d.TipoDocumentoId && d.ValidoAte <= hoje.AddDays(t.DiasAvisoVencimento)) ||
             (!db.TiposDocumento.Any(t => t.Id == d.TipoDocumentoId) && d.ValidoAte <= padrao)));
        return SimOuNao(q, c, vencendo, d => d.PessoaId);
    }

    // ---------------------------------------------------------------- Cadastro: pendências

    /// <summary>
    /// Alguma das pendências de <see cref="PendenciaCadastral"/> (as mesmas que o resumo da pessoa mostra):
    /// PF sem CPF / PJ sem CNPJ no principal, sem endereço ativo preenchido, município a corrigir ou contribuinte sem IE.
    /// </summary>
    private static IQueryable<Pessoa> ComPendenciaCadastral(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var db = x.Db;
        var contribuinte = IndicadorIE.Contribuinte;
        Expression<Func<Pessoa, bool>> pendente = p =>
            // SemCpfCnpj
            (p.Natureza == NaturezaPessoa.Fisica && (p.DocumentoPrincipal == null || p.DocumentoPrincipal == "")) ||
            (p.Natureza == NaturezaPessoa.Juridica &&
             !p.Estabelecimentos.Any(e => e.Principal && e.Cnpj != null && e.Cnpj != "")) ||
            // SemEndereco
            !p.Enderecos.Any(e => e.Ativo && e.Logradouro != "") ||
            // MunicipioACorrigir: só de endereço ativo preenchido, como no resumo (a naturalidade a corrigir não conta aqui)
            db.PendenciasMunicipio.Any(y => y.PessoaId == p.Id && y.ResolvidaEm == null && y.Origem == OrigemPendenciaMunicipio.Endereco &&
                                            p.Enderecos.Any(e => e.Id == y.RegistroId && e.Ativo && e.Logradouro != "")) ||
            // ContribuinteSemIE
            p.Estabelecimentos.Any(e => e.Principal && e.IndicadorIE == contribuinte &&
                                        (e.InscricaoEstadual == null || e.InscricaoEstadual == ""));
        return c.Operador == OperadorFiltro.Sim ? q.Where(pendente) : q.Where(Negar(pendente));
    }

    // ---------------------------------------------------------------- Fiscal

    private static IQueryable<Pessoa> ProdutorRural(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x) =>
        c.Operador == OperadorFiltro.Sim
            ? q.Where(p => p.Estabelecimentos.Any(e => e.Ativo && e.ProdutorRural))
            : q.Where(p => !p.Estabelecimentos.Any(e => e.Ativo && e.ProdutorRural));

    private static IQueryable<Pessoa> Regime(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var regimes = Enums<RegimeTributario>(c);
        return Nenhum(c)
            ? q.Where(p => !p.Estabelecimentos.Any(e => e.Ativo && regimes.Contains(e.RegimeTributario)))
            : q.Where(p => p.Estabelecimentos.Any(e => e.Ativo && regimes.Contains(e.RegimeTributario)));
    }

    /// <summary>Prefixo de código numérico = faixa: "47" → 4700000..4799999 (usa o índice (Codigo, Principal)).</summary>
    private static IQueryable<Pessoa> Cnae(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x, bool somentePrincipal)
    {
        var db = x.Db;
        var cnae = c.Valores[0];
        var faltam = 7 - cnae.Length;
        var de = int.Parse(cnae, CultureInfo.InvariantCulture) * (int)Math.Pow(10, faltam);
        var ate = de + (int)Math.Pow(10, faltam) - 1;
        return q.Where(p => db.EstabelecimentoCnaes.Any(y => y.PessoaId == p.Id && y.Codigo >= de && y.Codigo <= ate &&
                                                           (!somentePrincipal || y.Principal)));
    }

    // ---------------------------------------------------------------- Comercial

    private static IQueryable<Pessoa> Vendedor(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var db = x.Db;
        var hoje = x.Hoje;
        var vendedores = Guids(c);
        return q.Where(p => db.CarteiraClientes.Any(y => y.PessoaId == p.Id && vendedores.Contains(y.VendedorId) && y.Ativo &&
                                                         y.InicioEm <= hoje && (y.FimEm == null || y.FimEm >= hoje)));
    }

    private static IQueryable<Pessoa> SemCarteira(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var db = x.Db;
        var hoje = x.Hoje;
        return c.Operador == OperadorFiltro.Sim
            ? q.Where(p => !db.CarteiraClientes.Any(y => y.PessoaId == p.Id && y.Ativo && y.InicioEm <= hoje && (y.FimEm == null || y.FimEm >= hoje)))
            : q.Where(p => db.CarteiraClientes.Any(y => y.PessoaId == p.Id && y.Ativo && y.InicioEm <= hoje && (y.FimEm == null || y.FimEm >= hoje)));
    }

    // ---------------------------------------------------------------- Interações

    /// <summary>Mesma regra de ParametrosRelacionamento.Situacao: dias completos desde a última interação. Várias = OU.</summary>
    private static IQueryable<Pessoa> Relacionamento(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var db = x.Db;
        var situacoes = Enums<SituacaoRelacionamento>(c);
        var sem = situacoes.Contains(SituacaoRelacionamento.SemInteracao);
        var ativo = situacoes.Contains(SituacaoRelacionamento.Ativo);
        var risco = situacoes.Contains(SituacaoRelacionamento.EmRisco);
        var inativo = situacoes.Contains(SituacaoRelacionamento.Inativo);
        var limiteRisco = x.Hoje.AddDays(-x.Parametros.DiasEmRisco + 1).ToDateTime(TimeOnly.MinValue);
        var limiteInativo = x.Hoje.AddDays(-x.Parametros.DiasInativo + 1).ToDateTime(TimeOnly.MinValue);

        Expression<Func<Pessoa, bool>> alguma = p =>
            (sem && !db.Interacoes.Any(i => i.PessoaId == p.Id)) ||
            (ativo && db.Interacoes.Any(i => i.PessoaId == p.Id && i.DataHora >= limiteRisco)) ||
            (risco && db.Interacoes.Any(i => i.PessoaId == p.Id && i.DataHora >= limiteInativo) &&
                      !db.Interacoes.Any(i => i.PessoaId == p.Id && i.DataHora >= limiteRisco)) ||
            (inativo && db.Interacoes.Any(i => i.PessoaId == p.Id) &&
                        !db.Interacoes.Any(i => i.PessoaId == p.Id && i.DataHora >= limiteInativo));

        return Nenhum(c)
            ? q.Where(Expression.Lambda<Func<Pessoa, bool>>(Expression.Not(alguma.Body), alguma.Parameters))
            : q.Where(alguma);
    }

    /// <summary>Sem interação registrada nos últimos N dias (inclui quem nunca teve).</summary>
    private static IQueryable<Pessoa> SemInteracao(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var db = x.Db;
        var desde = x.Hoje.AddDays(-Inteiro(c) + 1).ToDateTime(TimeOnly.MinValue);
        return q.Where(p => !db.Interacoes.Any(i => i.PessoaId == p.Id && i.DataHora >= desde));
    }

    // ---------------------------------------------------------------- Situação

    private static IQueryable<Pessoa> Situacao(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var situacoes = Enums<SituacaoPessoa>(c);
        return Nenhum(c) ? q.Where(p => !situacoes.Contains(p.Situacao)) : q.Where(p => situacoes.Contains(p.Situacao));
    }

    private static IQueryable<Pessoa> Bloqueado(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var db = x.Db;
        return c.Operador == OperadorFiltro.Sim
            ? q.Where(p => db.Bloqueios.Any(b => b.PessoaId == p.Id && b.FimEm == null))
            : q.Where(p => !db.Bloqueios.Any(b => b.PessoaId == p.Id && b.FimEm == null));
    }

    // ---------------------------------------------------------------- Cadastro

    /// <summary>Datas do dia local (inclusivas) comparadas com o instante gravado em UTC.</summary>
    private static IQueryable<Pessoa> CadastradoEm(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        static DateTime Inicio(DateOnly d) => d.ToDateTime(TimeOnly.MinValue).ToUniversalTime();

        if (c.Operador is OperadorFiltro.Entre or OperadorFiltro.APartirDe)
        {
            var de = Inicio(Data(c, 0));
            q = q.Where(p => p.CriadoEm >= de);
        }
        if (c.Operador is OperadorFiltro.Entre or OperadorFiltro.Ate)
        {
            var ate = Inicio(Data(c, c.Operador == OperadorFiltro.Entre ? 1 : 0).AddDays(1));
            q = q.Where(p => p.CriadoEm < ate);
        }
        return q;
    }

    // ---------------------------------------------------------------- Fase 3: auxiliares

    /// <summary>Pessoas com pelo menos uma linha da subconsulta (ex.: um endereço do bairro).</summary>
    private static IQueryable<Pessoa> ComAlgum<T>(IQueryable<Pessoa> q, IQueryable<T> linhas, Expression<Func<T, Guid>> pessoaId)
    {
        var ids = linhas.Select(pessoaId);
        return q.Where(p => ids.Contains(p.Id));
    }

    /// <summary>Pessoas sem nenhuma linha da subconsulta (ex.: sem documento do tipo).</summary>
    private static IQueryable<Pessoa> SemNenhum<T>(IQueryable<Pessoa> q, IQueryable<T> linhas, Expression<Func<T, Guid>> pessoaId)
    {
        var ids = linhas.Select(pessoaId);
        return q.Where(p => !ids.Contains(p.Id));
    }

    private static IQueryable<Pessoa> SimOuNao<T>(IQueryable<Pessoa> q, CondicaoFiltro c, IQueryable<T> linhas, Expression<Func<T, Guid>> pessoaId) =>
        c.Operador == OperadorFiltro.Sim ? ComAlgum(q, linhas, pessoaId) : SemNenhum(q, linhas, pessoaId);

    /// <summary>Campo da própria pessoa numa lista: "um destes" ou "nenhum destes".</summary>
    private static IQueryable<Pessoa> Lista<TValor>(IQueryable<Pessoa> q, CondicaoFiltro c, List<TValor> valores,
                                                    Func<List<TValor>, Expression<Func<Pessoa, bool>>> contem)
    {
        var filtro = contem(valores);
        return Nenhum(c) ? q.Where(Negar(filtro)) : q.Where(filtro);
    }

    /// <summary>Uma condição ou a outra (mesmo parâmetro nas duas).</summary>
    private static Expression<Func<T, bool>> Ou<T>(Expression<Func<T, bool>> a, Expression<Func<T, bool>> b) =>
        Expression.Lambda<Func<T, bool>>(
            Expression.OrElse(a.Body, new Substituir(b.Parameters[0], a.Parameters[0]).Visit(b.Body)), a.Parameters);

    private static Expression<Func<T, bool>> Negar<T>(Expression<Func<T, bool>> filtro) =>
        Expression.Lambda<Func<T, bool>>(Expression.Not(filtro.Body), filtro.Parameters);

    /// <summary>Texto: contém, começa com, igual, vazio ou preenchido (maiúsculas/acentos conforme o banco).</summary>
    private static Expression<Func<T, bool>> Texto<T>(Expression<Func<T, string?>> campo, CondicaoFiltro c)
    {
        var t = c.Valores.FirstOrDefault() ?? string.Empty;
        Expression<Func<string?, bool>> teste = c.Operador switch
        {
            OperadorFiltro.Contem => v => v != null && v.Contains(t),
            OperadorFiltro.ComecaCom => v => v != null && v.StartsWith(t),
            OperadorFiltro.Igual => v => v == t,
            OperadorFiltro.Vazio => v => v == null || v == "",
            _ => v => v != null && v != ""
        };
        return Juntar(campo, teste);
    }

    /// <summary>Data: entre, a partir de, até (inclusive).</summary>
    private static Expression<Func<T, bool>> Faixa<T>(Expression<Func<T, DateOnly?>> campo, CondicaoFiltro c)
    {
        var de = Data(c, 0);
        var ate = c.Operador == OperadorFiltro.Entre ? Data(c, 1) : de;
        Expression<Func<DateOnly?, bool>> teste = c.Operador switch
        {
            OperadorFiltro.Entre => v => v >= de && v <= ate,
            OperadorFiltro.APartirDe => v => v >= de,
            _ => v => v <= de
        };
        return Juntar(campo, teste);
    }

    /// <summary>Número: entre, a partir de, até (inclusive).</summary>
    private static Expression<Func<T, bool>> Faixa<T>(Expression<Func<T, decimal?>> campo, CondicaoFiltro c)
    {
        var de = decimal.Parse(c.Valores[0], CultureInfo.InvariantCulture);
        var ate = c.Operador == OperadorFiltro.Entre ? decimal.Parse(c.Valores[1], CultureInfo.InvariantCulture) : de;
        Expression<Func<decimal?, bool>> teste = c.Operador switch
        {
            OperadorFiltro.Entre => v => v >= de && v <= ate,
            OperadorFiltro.APartirDe => v => v >= de,
            _ => v => v <= de
        };
        return Juntar(campo, teste);
    }

    /// <summary>Aplica o teste ao campo: troca o parâmetro do teste pelo corpo do campo (o banco entende, sem Invoke).</summary>
    private static Expression<Func<T, bool>> Juntar<T, TCampo>(Expression<Func<T, TCampo>> campo, Expression<Func<TCampo, bool>> teste) =>
        Expression.Lambda<Func<T, bool>>(new Substituir(teste.Parameters[0], campo.Body).Visit(teste.Body), campo.Parameters);

    private sealed class Substituir(ParameterExpression de, Expression para) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == de ? para : base.VisitParameter(node);
    }

    private static IQueryable<Pessoa> Aniversario(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var meses = c.Valores.Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToList();
        return q.Where(p => p.DataNascimento != null && meses.Contains(p.DataNascimento.Value.Month));
    }

    /// <summary>Idade em anos completos hoje: "de 18 a 30" = nascidos entre (hoje − 31 anos + 1 dia) e (hoje − 18 anos).</summary>
    private static IQueryable<Pessoa> Idade(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var minimo = (int)Math.Floor(decimal.Parse(c.Valores[0], CultureInfo.InvariantCulture));
        var maximo = c.Operador == OperadorFiltro.Entre ? (int)Math.Floor(decimal.Parse(c.Valores[1], CultureInfo.InvariantCulture)) : minimo;
        var hoje = x.Hoje;
        DateOnly? nascidoAte = hoje.AddYears(-minimo);             // idade >= mínimo
        DateOnly? nascidoDepois = hoje.AddYears(-(maximo + 1));     // idade <= máximo
        return c.Operador switch
        {
            OperadorFiltro.Entre => q.Where(p => p.DataNascimento != null && p.DataNascimento <= nascidoAte && p.DataNascimento > nascidoDepois),
            OperadorFiltro.APartirDe => q.Where(p => p.DataNascimento != null && p.DataNascimento <= nascidoAte),
            _ => q.Where(p => p.DataNascimento != null && p.DataNascimento > nascidoDepois)
        };
    }

    /// <summary>Tem contato ativo (telefone/e-mail) marcado para a finalidade (NF-e, cobrança...). Várias = qualquer uma.</summary>
    private static IQueryable<Pessoa> FinalidadeContato(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var mascara = Enums<FinalidadeEmail>(c).Aggregate(FinalidadeEmail.Nenhuma, (a, f) => a | f);
        var com = x.Db.MeiosContato.Where(m => m.Ativo && (m.Finalidades & mascara) != FinalidadeEmail.Nenhuma);
        return Nenhum(c) ? SemNenhum(q, com, m => m.PessoaId) : ComAlgum(q, com, m => m.PessoaId);
    }

    /// <summary>Sim: algum vínculo sem desligamento (ou desligamento futuro). Não: tem vínculo, mas todos desligados.</summary>
    private static IQueryable<Pessoa> ColaboradorAtivo(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var hoje = x.Hoje;
        var ativos = x.Db.VinculosColaborador.Where(v => v.DesligamentoEm == null || v.DesligamentoEm > hoje);
        return c.Operador == OperadorFiltro.Sim
            ? ComAlgum(q, ativos, v => v.PessoaId)
            : SemNenhum(ComAlgum(q, x.Db.VinculosColaborador, v => v.PessoaId), ativos, v => v.PessoaId);
    }

    /// <summary>Lotação vigente hoje com o cargo/departamento/setor escolhido.</summary>
    private static IQueryable<Pessoa> Lotacao(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x, Expression<Func<LotacaoColaborador, Guid?>> campo)
    {
        var hoje = x.Hoje;
        List<Guid?> ids = [.. Guids(c).Select(g => (Guid?)g)];
        Expression<Func<Guid?, bool>> teste = v => ids.Contains(v);
        var com = x.Db.LotacoesColaborador.Where(l => l.InicioEm <= hoje && (l.FimEm == null || l.FimEm >= hoje)).Where(Juntar(campo, teste));
        return Nenhum(c) ? SemNenhum(q, com, l => l.PessoaId) : ComAlgum(q, com, l => l.PessoaId);
    }

    /// <summary>Relacionamento ativo do tipo, com a pessoa de qualquer um dos lados (sócio de / tem como sócio).</summary>
    private static IQueryable<Pessoa> TipoRelacionamento(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var tipos = Guids(c);
        var relacoes = x.Db.PessoaRelacionamentos.Where(r => r.Ativo && tipos.Contains(r.TipoRelacionamentoId));
        var ids = relacoes.Select(r => r.PessoaId).Concat(relacoes.Select(r => r.PessoaDestinoId));
        return Nenhum(c) ? q.Where(p => !ids.Contains(p.Id)) : q.Where(p => ids.Contains(p.Id));
    }

    /// <summary>Relacionamento ativo com alguém cujo nome contém/começa com o texto (de qualquer um dos lados).</summary>
    private static IQueryable<Pessoa> PessoaRelacionada(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        var db = x.Db;
        var outras = db.Pessoas.Where(Texto<Pessoa>(o => o.Nome, c)).Select(o => o.Id);
        var ativas = db.PessoaRelacionamentos.Where(r => r.Ativo);
        var ids = ativas.Where(r => outras.Contains(r.PessoaDestinoId)).Select(r => r.PessoaId)
            .Concat(ativas.Where(r => outras.Contains(r.PessoaId)).Select(r => r.PessoaDestinoId));
        return q.Where(p => ids.Contains(p.Id));
    }

    private static IQueryable<Pessoa> AlteradoEm(IQueryable<Pessoa> q, CondicaoFiltro c, Contexto x)
    {
        static DateTime Inicio(DateOnly d) => d.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        if (c.Operador is OperadorFiltro.Entre or OperadorFiltro.APartirDe)
        {
            var de = Inicio(Data(c, 0));
            q = q.Where(p => p.AtualizadoEm >= de);
        }
        if (c.Operador is OperadorFiltro.Entre or OperadorFiltro.Ate)
        {
            var ate = Inicio(Data(c, c.Operador == OperadorFiltro.Entre ? 1 : 0).AddDays(1));
            q = q.Where(p => p.AtualizadoEm < ate);
        }
        return q;
    }

    // ---------------------------------------------------------------- Informações adicionais

    /// <summary>Campo personalizado pesquisável: "começa com" (sem acento/maiúsculas, pelo índice) ou "tem valor".</summary>
    private static IQueryable<Pessoa> CampoPersonalizado(IQueryable<Pessoa> q, CondicaoFiltro c, Guid campo)
    {
        if (c.Operador != OperadorFiltro.ComecaCom)
            return q.Where(p => p.ValoresPersonalizados.Any(v => v.CampoId == campo));

        var valor = c.Valores[0];
        var inicio = valor.Length > ConfiguracaoValorPersonalizado.TamanhoBusca ? valor[..ConfiguracaoValorPersonalizado.TamanhoBusca] : valor;
        return q.Where(p => p.ValoresPersonalizados.Any(v => v.CampoId == campo &&
            EF.Property<string>(v, ConfiguracaoValorPersonalizado.ColunaBusca).StartsWith(inicio)));
    }
}
