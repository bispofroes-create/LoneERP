using Lone.Domain.Contatos;
using Lone.Domain.Enderecos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Etiquetas;
using Lone.Domain.GruposEmpresariais;
using Lone.Domain.ObjetosDeValor;
using Lone.Domain.Pessoas;
using C = Lone.Domain.Pessoas.CamposFichaPessoa;

namespace Lone.Domain.Validacao;

/// <summary>Regras de negócio do cadastro de pessoas. Não acessa banco. Espera a pessoa já normalizada.</summary>
public static class PessoaValidador
{
    /// <summary>Só os textos (o contrato de sempre).</summary>
    public static List<string> Validar(Pessoa p) => ValidarComCampos(p).Mensagens();

    /// <summary>Os mesmos erros, cada um com o campo da ficha e o registro (endereço, documento...) quando houver.</summary>
    public static ListaErros ValidarComCampos(Pessoa p)
    {
        var erros = new ListaErros();

        ValidarIdentificacao(p, erros);
        ValidarEstabelecimentos(p, erros);

        for (var i = 0; i < p.Enderecos.Count; i++)
            ValidarEndereco(p.Enderecos[i], $"Endereço {i + 1}", erros);

        for (var i = 0; i < p.MeiosContato.Count; i++)
            ValidarMeio(p.MeiosContato[i], $"Telefone/e-mail {i + 1}", erros);

        // Contato inativo (removido depois de gravado) não é validado: continua gravado como estava (P0).
        for (var i = 0; i < p.Contatos.Count; i++)
            if (p.Contatos[i].Ativo)
                ValidarContato(p.Contatos[i], $"Pessoa de contato {i + 1}", erros);

        for (var i = 0; i < p.Documentos.Count; i++)
            ValidarDocumento(p.Documentos[i], $"Documento {i + 1}", erros);

        foreach (var c in p.ContasCliente)
            ValidarContaCliente(c, erros);

        foreach (var f in p.ContasFornecedor)
            ValidarContaFornecedor(f, erros);

        ValidarDadosComplementares(p, erros);
        ValidarTamanhos(p, erros);

        return erros;
    }

    /// <summary>
    /// Bloco G (P1-1): textos maiores que a coluna do banco viram erro no campo, antes de gravar (a lista de limites é a de
    /// <see cref="LimitesCadastroPessoa"/>). Vale para todo registro, ativo ou não: o banco não guarda o excesso de nenhum.
    /// </summary>
    private static void ValidarTamanhos(Pessoa p, ListaErros erros)
    {
        Conferir(p, null, null, null);

        var juridica = p.Natureza == NaturezaPessoa.Juridica;
        for (var i = 0; i < p.Estabelecimentos.Count; i++)
        {
            var e = p.Estabelecimentos[i];
            Conferir(e, juridica ? $"Estabelecimento {i + 1}" : "Fiscal", e.Id,
                // Nome fantasia e natureza jurídica do principal ficam na Identificação (sem item); os das filiais, no cartão.
                campo => e.Principal && campo is C.NomeFantasia or C.NaturezaJuridica ? null : e.Id);
        }
        for (var i = 0; i < p.Enderecos.Count; i++) Conferir(p.Enderecos[i], $"Endereço {i + 1}", p.Enderecos[i].Id, null);
        for (var i = 0; i < p.MeiosContato.Count; i++) Conferir(p.MeiosContato[i], $"Telefone/e-mail {i + 1}", p.MeiosContato[i].Id, null);
        for (var i = 0; i < p.Contatos.Count; i++) Conferir(p.Contatos[i], $"Pessoa de contato {i + 1}", p.Contatos[i].Id, null);
        for (var i = 0; i < p.Documentos.Count; i++) Conferir(p.Documentos[i], $"Documento {i + 1}", p.Documentos[i].Id, null);
        foreach (var papel in p.Papeis) Conferir(papel, "Papel", null, null);
        foreach (var conta in p.ContasCliente) Conferir(conta, "Cliente", null, null);
        foreach (var conta in p.ContasFornecedor) Conferir(conta, "Fornecedor", null, null);

        void Conferir(object item, string? rotulo, Guid? id, Func<string?, Guid?>? itemDoCampo)
        {
            foreach (var limite in LimitesCadastroPessoa.De(item.GetType()))
            {
                if (LimitesCadastroPessoa.Valor(limite, item) is not { Length: var tamanho } || tamanho <= limite.Maximo) continue;
                // "as observações podem", "o logradouro pode".
                var pode = limite.Nome.StartsWith("as ", StringComparison.Ordinal) || limite.Nome.StartsWith("os ", StringComparison.Ordinal)
                    ? "podem" : "pode";
                var texto = rotulo is null
                    ? $"{char.ToUpperInvariant(limite.Nome[0])}{limite.Nome[1..]} {pode} ter no máximo {limite.Maximo} caracteres."
                    : $"{rotulo}: {limite.Nome} {pode} ter no máximo {limite.Maximo} caracteres.";
                erros.Add(new ErroValidacao(texto, limite.Campo, limite.Campo is null ? null : itemDoCampo is null ? id : itemDoCampo(limite.Campo)));
            }
        }
    }

    private static void ValidarIdentificacao(Pessoa p, ListaErros erros)
    {
        if (p.Nome.Length == 0)
            erros.Add(p.Natureza == NaturezaPessoa.Juridica ? "Informe a razão social." : "Informe o nome.", C.Nome);
        else if (p.Nome.Length > 150)
            erros.Add("O nome pode ter no máximo 150 caracteres.", C.Nome);

        if (p.Natureza == NaturezaPessoa.Fisica && p.DocumentoPrincipal is not null && !Cpf.EhValido(p.DocumentoPrincipal))
            erros.Add("CPF inválido.", C.Documento);

        if (p.Natureza == NaturezaPessoa.Estrangeiro && p.DocumentoPrincipal is { Length: > 20 })
            erros.Add("A identificação do estrangeiro pode ter no máximo 20 caracteres.", C.Documento);

        if (p.DataNascimento is { } nascimento && nascimento > DateOnly.FromDateTime(DateTime.Today))
            erros.Add("A data de nascimento não pode ser no futuro.", C.DataNascimento);
        if (RegrasGrupoEmpresarial.ValidarNatureza(p) is { } erroGrupo)
            erros.Add(erroGrupo, C.GrupoEmpresarial);
    }

    /// <summary>Dados pessoais, da empresa e de relacionamento (tamanhos batem com as colunas do banco).</summary>
    private static void ValidarDadosComplementares(Pessoa p, ListaErros erros)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        Limite(p.Nacionalidade, 60, "A nacionalidade", erros, C.Nacionalidade);
        Limite(p.SituacaoMotivo, Pessoa.TamanhoMaximoMotivo, "O motivo da situação", erros);
        Limite(p.NomeMae, 150, "O nome da mãe", erros, C.NomeMae);
        Limite(p.NomePai, 150, "O nome do pai", erros, C.NomePai);

        if (p.DataAbertura is { } abertura && abertura > hoje)
            erros.Add("A data de abertura da empresa não pode ser no futuro.", C.DataAbertura);
        if (p.CapitalSocial is < 0)
            erros.Add("O capital social não pode ser negativo.", C.CapitalSocial);
        Limite(p.Porte, 60, "O porte", erros, C.Porte);
        if (p.Socios.Any(s => s.Nome.Length == 0))
            erros.Add("Informe o nome de cada sócio.");
        foreach (var s in p.Socios)
        {
            Limite(s.Nome, 150, "O nome do sócio", erros);
            Limite(s.Qualificacao, 80, "A qualificação do sócio", erros);
            Limite(s.Documento, 20, "O documento do sócio", erros);
        }
        foreach (var e in p.Estabelecimentos)
            Limite(e.CnaesSecundarios, 1000, "A lista de CNAEs secundários", erros);

        if (p.PrimeiroContatoEm is { } primeiro && primeiro > hoje)
            erros.Add("A data do primeiro contato não pode ser no futuro.", C.PrimeiroContato);
        Limite(p.OrigemCadastro, 60, "A origem do cadastro", erros);

        if (p.Etiquetas.Count > RegrasEtiqueta.MaximoPorPessoa)
            erros.Add($"Use no máximo {RegrasEtiqueta.MaximoPorPessoa} etiquetas por cadastro.", C.Etiquetas);
    }

    private static void Limite(string? texto, int maximo, string campo, ListaErros erros, string? idCampo = null, Guid? item = null)
    {
        if (texto is { Length: var tamanho } && tamanho > maximo)
            erros.Add(new ErroValidacao($"{campo} pode ter no máximo {maximo} caracteres.", idCampo, item));
    }

    private static void ValidarEstabelecimentos(Pessoa p, ListaErros erros)
    {
        if (p.Natureza != NaturezaPessoa.Juridica)
        {
            if (p.Estabelecimentos.Count != 1)
                erros.Add("Pessoa física ou estrangeira tem um único conjunto de dados fiscais.");
        }
        else
        {
            var cnpjs = new HashSet<string>();
            var raiz = p.DocumentoPrincipal;

            for (var i = 0; i < p.Estabelecimentos.Count; i++)
            {
                var e = p.Estabelecimentos[i];
                var rotulo = $"Estabelecimento {i + 1}";
                // O CNPJ do principal fica na Identificação (é o documento da empresa); o das filiais, no cartão de cada uma.
                var (campoCnpj, itemCnpj) = e.Principal ? (C.Documento, (Guid?)null) : (C.Cnpj, (Guid?)e.Id);

                if (e.Cnpj is null)
                    erros.Add($"{rotulo}: informe o CNPJ.", campoCnpj, itemCnpj);
                else if (!Cnpj.EhValido(e.Cnpj))
                    erros.Add($"{rotulo}: CNPJ inválido.", campoCnpj, itemCnpj);
                else if (!cnpjs.Add(e.Cnpj))
                    erros.Add($"{rotulo}: CNPJ repetido.", campoCnpj, itemCnpj);
                else if (raiz is not null && !e.Cnpj.StartsWith(raiz, StringComparison.Ordinal))
                    erros.Add($"{rotulo}: o CNPJ é de outra empresa (raiz diferente). Filiais precisam ter a mesma raiz da matriz; outra raiz é outra pessoa.", campoCnpj, itemCnpj);
            }
        }

        // Endereço × finalidade depende do cadastro de finalidades: RegrasFinalidadeEndereco.Validar, no PessoaAppService.
        erros.AddRange(RegrasFinalidadeEndereco.ValidarConsolidacao(p));

        var idsEnderecos = p.Enderecos.Select(e => e.Id).ToHashSet();
        for (var i = 0; i < p.Estabelecimentos.Count; i++)
        {
            var e = p.Estabelecimentos[i];
            var rotulo = p.Natureza == NaturezaPessoa.Juridica ? $"Estabelecimento {i + 1}" : "Fiscal";
            // Integridade e formato valem para todo estabelecimento, ativo ou não (inativo continua existindo).
            ValidarFiscal(e, rotulo, erros);

            // O endereço fiscal precisa ser um dos endereços desta mesma pessoa.
            if (e.EnderecoFiscalId is Guid enderecoId && !idsEnderecos.Contains(enderecoId))
                erros.Add($"{rotulo}: o endereço fiscal escolhido não está entre os endereços do cadastro.", C.EnderecoFiscal, e.Id);
            // Operacional (só estabelecimento ativo): um desativado guarda o endereço fiscal da época, mesmo inativo.
            else if (e.Ativo && e.EnderecoFiscalId is Guid fiscalId && p.Enderecos.First(x => x.Id == fiscalId) is { Ativo: false })
                erros.Add($"{rotulo}: o endereço fiscal foi removido (inativo). Escolha outro endereço ou use o principal.", C.EnderecoFiscal, e.Id);
        }
    }

    private static void ValidarFiscal(Estabelecimento e, string rotulo, ListaErros erros)
    {
        // Operacional (só estabelecimento ativo): um desativado não emite nota; o indicador e a IE ficam como estavam.
        if (e.Ativo && e.IndicadorIE == IndicadorIE.Contribuinte && e.InscricaoEstadual is null)
            erros.Add($"{rotulo}: contribuinte do ICMS precisa ter inscrição estadual.", C.InscricaoEstadual, e.Id);
        if (e.InscricaoEstadual is { Length: > 14 })
            erros.Add($"{rotulo}: inscrição estadual com mais de 14 caracteres.", C.InscricaoEstadual, e.Id);
        if (e.InscricaoSuframa is { Length: not (8 or 9) })
            erros.Add($"{rotulo}: a inscrição SUFRAMA deve ter 8 ou 9 dígitos.", C.Suframa, e.Id);
        if (e.CnaePrincipal is { Length: not 7 })
            erros.Add($"{rotulo}: o CNAE deve ter 7 dígitos.", C.Cnae, e.Id);
    }

    private static void ValidarEndereco(PessoaEndereco e, string rotulo, ListaErros erros)
    {
        if (e.Observacoes is { Length: > RegrasEndereco.TamanhoMaximoObservacoes })
            erros.Add($"{rotulo}: as observações podem ter no máximo {RegrasEndereco.TamanhoMaximoObservacoes} caracteres.", C.ObservacoesEndereco, e.Id);

        // Endereço removido (inativo) fica como estava: dados antigos (até sem logradouro, de cadastros migrados) não
        // impedem a gravação (Bloco G, P1-14). Reativado, volta a ser conferido como qualquer endereço ativo.
        if (!e.Ativo) return;

        if (e.Logradouro.Length == 0)
            erros.Add($"{rotulo}: informe o logradouro.", C.Logradouro, e.Id);

        if (e.EhBrasil)
        {
            if (e.Cep is not null && !Cep.EhValido(e.Cep))
                erros.Add($"{rotulo}: o CEP deve ter 8 dígitos.", C.Cep, e.Id);
            // Município só da tabela do IBGE (a API confere se existe e copia nome, UF e código).
            if (e.MunicipioId is null)
                erros.Add($"{rotulo}: escolha a UF e o município na lista.", C.Municipio, e.Id);
        }
        else
        {
            if (e.Cidade.Length == 0)
                erros.Add($"{rotulo}: informe a cidade.", C.Cidade, e.Id);
            if (e.Pais.Length == 0)
                erros.Add($"{rotulo}: informe o país.", C.Pais, e.Id);
            // No exterior o código postal não passa pela regra do CEP (8 dígitos): só cabe na coluna (8).
            if (e.Cep is { Length: > 8 })
                erros.Add($"{rotulo}: o código postal pode ter no máximo 8 dígitos.", C.Cep, e.Id);
        }
    }

    private static void ValidarMeio(MeioContato m, string rotulo, ListaErros erros)
    {
        if (m.Valor.Length == 0)
        {
            erros.Add($"{rotulo}: informe o número ou e-mail.", C.MeioContatoValor, m.Id);
            return;
        }

        if (m.Tipo == TipoContato.Email && !Email.EhValido(m.Valor))
            erros.Add($"{rotulo}: e-mail inválido.", C.MeioContatoValor, m.Id);
        else if (m.Tipo is TipoContato.Telefone or TipoContato.Celular or TipoContato.WhatsApp && !Telefone.EhValido(m.Valor))
            erros.Add($"{rotulo}: telefone inválido. Informe com DDD, ou com + e o código do país se for do exterior.", C.MeioContatoValor, m.Id);
        if (m.Ramal is { Length: > RegrasMeioContato.TamanhoMaximoRamal })
            erros.Add($"{rotulo}: o ramal pode ter no máximo {RegrasMeioContato.TamanhoMaximoRamal} dígitos.", C.Ramal, m.Id);
    }

    private static void ValidarContato(Contato c, string rotulo, ListaErros erros)
    {
        if (c.Nome.Length == 0)
            erros.Add($"{rotulo}: informe o nome.", C.ContatoNome, c.Id);
        if (c.Telefone is not null && !Telefone.EhValido(c.Telefone))
            erros.Add($"{rotulo}: telefone inválido.", C.ContatoTelefone, c.Id);
        if (c.Celular is not null && !Telefone.EhValido(c.Celular))
            erros.Add($"{rotulo}: celular inválido.", C.ContatoCelular, c.Id);
        if (c.Email is not null && !Email.EhValido(c.Email))
            erros.Add($"{rotulo}: e-mail inválido.", C.ContatoEmail, c.Id);
    }

    private static void ValidarDocumento(PessoaDocumento d, string rotulo, ListaErros erros)
    {
        if (d.Observacoes is { Length: > 250 })
            erros.Add($"{rotulo}: as observações podem ter no máximo 250 caracteres.", C.DocumentoObservacoes, d.Id);
        // Documento removido (inativo) fica como estava: dados antigos não impedem a gravação.
        if (!d.Ativo) return;
        if (d.Numero.Length == 0)
            erros.Add($"{rotulo}: informe o número.", C.DocumentoNumero, d.Id);
        if (d.Uf is not null && !Ufs.Valida(d.Uf))
            erros.Add($"{rotulo}: UF inválida.", C.DocumentoUf, d.Id);
        if (d.EmitidoEm is { } emissao && d.ValidoAte is { } validade && validade < emissao)
            erros.Add($"{rotulo}: a validade é anterior à emissão.", C.DocumentoValidoAte, d.Id);
    }

    private static void ValidarContaCliente(ContaCliente c, ListaErros erros)
    {
        if (c.LimiteCredito < 0)
            erros.Add("Cliente: o limite de crédito não pode ser negativo.", C.LimiteCredito);
        if (c.DescontoMaximo is < 0m or > 100m)
            erros.Add("Cliente: o desconto máximo deve ficar entre 0% e 100%.", C.DescontoMaximo);
        if (c.DiasMaximoAtraso < 0)
            erros.Add("Cliente: dias máximos de atraso não podem ser negativos.", C.DiasMaximoAtraso);
    }

    private static void ValidarContaFornecedor(ContaFornecedor f, ListaErros erros)
    {
        if (f.PrazoMedioDias < 0 || f.LeadTimeDias < 0)
            erros.Add("Fornecedor: prazos não podem ser negativos.", C.PrazosFornecedor);
        if (f.Avaliacao is < 1 or > 5)
            erros.Add("Fornecedor: a avaliação deve ser de 1 a 5.", C.AvaliacaoFornecedor);
    }
}
