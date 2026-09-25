using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.ObjetosDeValor;

namespace Lone.Domain.Validacao;

/// <summary>Limpa e padroniza a pessoa antes de validar e gravar. Não acessa banco.</summary>
public static class PessoaNormalizador
{
    public static void Normalizar(Pessoa p) => Normalizar(p, DateOnly.FromDateTime(DateTime.Today));

    /// <param name="agoraUtc">Momento registrado nos consentimentos (padrão: agora).</param>
    public static void Normalizar(Pessoa p, DateOnly hoje, DateTime? agoraUtc = null)
    {
        p.Nome = Texto(p.Nome) ?? string.Empty;
        p.NomeSocial = Texto(p.NomeSocial);
        p.NomeExibicao = Texto(p.NomeExibicao);
        p.Apelido = Texto(p.Apelido);
        p.Observacoes = Texto(p.Observacoes);
        p.SituacaoMotivo = Texto(p.SituacaoMotivo);

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
        NormalizarDadosPessoais(p);
        NormalizarDadosEmpresa(p);
        NormalizarConsentimentos(p.Consentimentos, agoraUtc ?? DateTime.UtcNow);
        NormalizarEtiquetas(p.Etiquetas);
        p.OrigemCadastro = Texto(p.OrigemCadastro);

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
            p.Estabelecimentos.Add(new Estabelecimento { Id = IdSequencial.Novo(), PessoaId = p.Id, Principal = true });

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
                e.MunicipioId = null;
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

    /// <summary>Dados civis só existem na pessoa física; cor/raça, só para funcionário (dado sensível, LGPD).</summary>
    private static void NormalizarDadosPessoais(Pessoa p)
    {
        if (p.Natureza != NaturezaPessoa.Fisica)
        {
            p.Sexo = SexoRegistro.NaoInformado;
            p.IdentidadeGenero = IdentidadeGenero.NaoInformado;
            p.EstadoCivil = EstadoCivil.NaoInformado;
            p.Escolaridade = Escolaridade.NaoInformado;
            p.NomeMae = p.NomePai = p.Profissao = null;
            p.NaturalidadeMunicipioId = null;
        }

        if (p.Natureza != NaturezaPessoa.Fisica || !p.TemPapel(TipoPapel.Funcionario))
            p.CorRaca = CorRaca.NaoInformado;

        p.Nacionalidade = Texto(p.Nacionalidade);
        p.NomeMae = Texto(p.NomeMae);
        p.NomePai = Texto(p.NomePai);
        p.Profissao = Texto(p.Profissao);
    }

    /// <summary>Dados da Receita (abertura, porte, capital, sócios, CNAEs secundários) só na pessoa jurídica.</summary>
    private static void NormalizarDadosEmpresa(Pessoa p)
    {
        if (p.Natureza != NaturezaPessoa.Juridica)
        {
            p.DataAbertura = null;
            p.Porte = null;
            p.CapitalSocial = null;
            p.Socios.Clear();
            foreach (var e in p.Estabelecimentos) e.CnaesSecundarios = null;
            return;
        }

        p.Porte = Texto(p.Porte);
        foreach (var s in p.Socios)
        {
            s.Nome = Texto(s.Nome) ?? string.Empty;
            s.Qualificacao = Texto(s.Qualificacao);
            s.Documento = Texto(s.Documento);
        }

        foreach (var e in p.Estabelecimentos)
        {
            var codigos = (e.CnaesSecundarios ?? string.Empty)
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Documento.SomenteDigitos)
                .Where(c => c.Length == 7)
                .Distinct()
                .ToList();
            e.CnaesSecundarios = codigos.Count == 0 ? null : string.Join(",", codigos);
        }
    }

    /// <summary>
    /// Um registro por canal. Ao autorizar, grava quando (se ainda não houver data); ao retirar uma autorização
    /// dada, grava quando foi retirada. As datas são do servidor, não do aparelho.
    /// </summary>
    private static void NormalizarConsentimentos(List<PessoaConsentimento> consentimentos, DateTime agoraUtc)
    {
        foreach (var repetido in consentimentos.GroupBy(c => c.Canal).SelectMany(g => g.Skip(1)).ToList())
            consentimentos.Remove(repetido);

        foreach (var c in consentimentos)
        {
            c.Origem = Texto(c.Origem);
            if (c.Concedido)
            {
                c.ConcedidoEm ??= agoraUtc;
                c.RevogadoEm = null;
            }
            else if (c.ConcedidoEm is not null)
            {
                c.RevogadoEm ??= agoraUtc;
            }
        }
    }

    /// <summary>
    /// Sem espaços sobrando, sem vazias e sem repetidas (maiúsculas e minúsculas contam como iguais, com a
    /// mesma comparação usada pelo repositório ao casar etiquetas gravadas).
    /// </summary>
    /// <summary>Sem referência vazia e sem a mesma etiqueta duas vezes.</summary>
    private static void NormalizarEtiquetas(List<PessoaEtiqueta> etiquetas)
    {
        etiquetas.RemoveAll(e => e.EtiquetaId == Guid.Empty);
        foreach (var repetida in etiquetas.GroupBy(e => e.EtiquetaId).SelectMany(g => g.Skip(1)).ToList())
            etiquetas.Remove(repetida);
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
