using Lone.Core.Entidades;
using Lone.Core.Enums;
using Lone.Core.ObjetosDeValor;

namespace Lone.Core.Validacao;

/// <summary>Limpa e padroniza a pessoa antes de validar e gravar. Não acessa banco.</summary>
public static class PessoaNormalizador
{
    public static void Normalizar(Pessoa p) => Normalizar(p, DateOnly.FromDateTime(DateTime.Today));

    public static void Normalizar(Pessoa p, DateOnly hoje)
    {
        p.Nome = Texto(p.Nome) ?? string.Empty;
        p.NomeSocial = Texto(p.NomeSocial);
        p.NomeExibicao = Texto(p.NomeExibicao);
        p.Apelido = Texto(p.Apelido);
        p.Observacoes = Texto(p.Observacoes);

        if (p.Natureza != NaturezaPessoa.Fisica)
        {
            p.NomeSocial = null;
            p.Apelido = null;
            p.DataNascimento = null;
        }

        NormalizarEstabelecimentos(p);

        p.DocumentoPrincipal = p.Natureza switch
        {
            NaturezaPessoa.Fisica => Texto(Documento.Normalizar(p.DocumentoPrincipal)),
            NaturezaPessoa.Juridica => p.EstabelecimentoPrincipal()?.Cnpj is { Length: 14 } cnpj ? cnpj[..8] : null,
            _ => Texto(p.DocumentoPrincipal)
        };

        NormalizarEnderecos(p.Enderecos, p.Natureza);
        NormalizarMeios(p.MeiosContato);
        NormalizarContatos(p.Contatos);
        NormalizarDocumentos(p.Documentos);
        NormalizarPapeis(p.Papeis, hoje);

        foreach (var c in p.ContasCliente)
        {
            c.CondicaoPagamento = Texto(c.CondicaoPagamento);
            c.Observacoes = Texto(c.Observacoes);
        }
        foreach (var f in p.ContasFornecedor)
        {
            f.CondicaoPagamento = Texto(f.CondicaoPagamento);
            f.Observacoes = Texto(f.Observacoes);
        }
    }

    private static void NormalizarEstabelecimentos(Pessoa p)
    {
        if (p.Estabelecimentos.Count == 0)
            p.Estabelecimentos.Add(new Estabelecimento { Principal = true });

        foreach (var e in p.Estabelecimentos)
        {
            e.Cnpj = p.Natureza == NaturezaPessoa.Juridica ? Texto(Documento.Normalizar(e.Cnpj)) : null;
            e.NomeFantasia = p.Natureza == NaturezaPessoa.Juridica ? Texto(e.NomeFantasia) : null;
            e.InscricaoEstadual = Texto(Documento.Normalizar(e.InscricaoEstadual));
            e.InscricaoMunicipal = Texto(e.InscricaoMunicipal);
            e.InscricaoSuframa = Texto(Documento.SomenteDigitos(e.InscricaoSuframa));
            e.CnaePrincipal = Texto(Documento.SomenteDigitos(e.CnaePrincipal));
            e.NaturezaJuridica = Texto(e.NaturezaJuridica);
            e.SituacaoReceita = Texto(e.SituacaoReceita);

            // Na NF-e, destinatário no exterior é sempre "não contribuinte" e sem IE.
            if (p.Natureza == NaturezaPessoa.Estrangeiro)
            {
                e.IndicadorIE = IndicadorIE.NaoContribuinte;
                e.InscricaoEstadual = null;
            }
        }

        MarcarUm(p.Estabelecimentos, e => e.Principal, (e, v) => e.Principal = v);
    }

    private static void NormalizarEnderecos(List<PessoaEndereco> enderecos, NaturezaPessoa natureza)
    {
        for (var i = 0; i < enderecos.Count; i++)
        {
            var e = enderecos[i];
            e.Ordem = i;
            e.Descricao = Texto(e.Descricao);
            e.Cep = Texto(Documento.SomenteDigitos(e.Cep));
            e.Logradouro = Texto(e.Logradouro) ?? string.Empty;
            e.Numero = Texto(e.Numero);
            e.Complemento = Texto(e.Complemento);
            e.Bairro = Texto(e.Bairro);
            e.Cidade = Texto(e.Cidade) ?? string.Empty;
            e.Uf = Texto(e.Uf)?.ToUpperInvariant();
            e.CodigoMunicipioIbge = Texto(Documento.SomenteDigitos(e.CodigoMunicipioIbge));
            e.CodigoPais = Texto(Documento.SomenteDigitos(e.CodigoPais)) ?? PessoaEndereco.CodigoPaisBrasil;
            e.Pais = Texto(e.Pais) ?? (e.EhBrasil ? "Brasil" : string.Empty);

            if (!e.EhBrasil)
            {
                e.Uf = Ufs.Exterior;
                e.CodigoMunicipioIbge = null;
            }
        }

        // Exatamente um endereço principal (se houver endereços).
        MarcarUm(enderecos,
            e => e.Tem(FinalidadeEndereco.Principal),
            (e, v) => e.Finalidades = v ? e.Finalidades | FinalidadeEndereco.Principal : e.Finalidades & ~FinalidadeEndereco.Principal);
    }

    private static void NormalizarMeios(List<MeioContato> meios)
    {
        foreach (var m in meios)
        {
            m.Valor = NormalizarValor(m.Tipo, m.Valor) ?? string.Empty;
            m.Descricao = Texto(m.Descricao);
        }

        // Um principal por tipo.
        foreach (var grupo in meios.GroupBy(m => m.Tipo))
            MarcarUm(grupo.ToList(), m => m.Principal, (m, v) => m.Principal = v);
    }

    private static void NormalizarContatos(List<Contato> contatos)
    {
        foreach (var c in contatos)
        {
            c.Nome = Texto(c.Nome) ?? string.Empty;
            c.Cargo = Texto(c.Cargo);
            c.Departamento = Texto(c.Departamento);
            c.Telefone = NormalizarValor(TipoContato.Telefone, c.Telefone);
            c.Celular = NormalizarValor(TipoContato.Celular, c.Celular);
            c.Email = NormalizarValor(TipoContato.Email, c.Email);
            c.Observacoes = Texto(c.Observacoes);
            if (c.Celular is null) c.CelularWhatsApp = false;
        }

        // No máximo um contato principal.
        var principal = contatos.FirstOrDefault(c => c.Principal);
        foreach (var c in contatos)
            c.Principal = ReferenceEquals(c, principal);
    }

    private static void NormalizarDocumentos(List<PessoaDocumento> documentos)
    {
        foreach (var d in documentos)
        {
            d.Numero = (Texto(d.Numero) ?? string.Empty).ToUpperInvariant();
            d.OrgaoEmissor = Texto(d.OrgaoEmissor)?.ToUpperInvariant();
            d.Uf = Texto(d.Uf)?.ToUpperInvariant();
            d.Observacoes = Texto(d.Observacoes);
        }
    }

    private static void NormalizarPapeis(List<PessoaPapel> papeis, DateOnly hoje)
    {
        // Um registro por papel.
        foreach (var repetido in papeis.GroupBy(p => p.Papel).SelectMany(g => g.Skip(1)).ToList())
            papeis.Remove(repetido);

        foreach (var p in papeis)
        {
            if (p.InicioEm == default) p.InicioEm = hoje;
            if (p.Ativo) p.FimEm = null;
            else p.FimEm ??= hoje;
            p.Observacoes = Texto(p.Observacoes);
        }
    }

    /// <summary>Telefones: só dígitos (ou + e dígitos); e-mail: minúsculas. Valor inválido fica como digitado, para o validador acusar.</summary>
    private static string? NormalizarValor(TipoContato tipo, string? valor)
    {
        var texto = Texto(valor);
        if (texto is null) return null;

        if (tipo == TipoContato.Email)
            return Email.TentarCriar(texto, out var email) ? email!.Valor : texto;

        if (tipo is TipoContato.Telefone or TipoContato.Celular or TipoContato.WhatsApp)
            return Telefone.TentarCriar(texto, out var telefone) ? telefone!.Normalizado : texto;

        return texto;
    }

    /// <summary>Garante exatamente um item marcado quando a lista não está vazia (o primeiro marcado, ou o primeiro).</summary>
    private static void MarcarUm<T>(IList<T> itens, Func<T, bool> marcado, Action<T, bool> definir)
    {
        if (itens.Count == 0) return;
        var escolhido = itens.FirstOrDefault(marcado) ?? itens[0];
        foreach (var item in itens)
            definir(item, ReferenceEquals(item, escolhido));
    }

    private static string? Texto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
