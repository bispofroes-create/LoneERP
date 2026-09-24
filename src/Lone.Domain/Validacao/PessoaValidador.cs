using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.ObjetosDeValor;

namespace Lone.Domain.Validacao;

/// <summary>Regras de negócio do cadastro de pessoas. Não acessa banco. Espera a pessoa já normalizada.</summary>
public static class PessoaValidador
{
    public static List<string> Validar(Pessoa p)
    {
        var erros = new List<string>();

        ValidarIdentificacao(p, erros);
        ValidarEstabelecimentos(p, erros);

        for (var i = 0; i < p.Enderecos.Count; i++)
            ValidarEndereco(p.Enderecos[i], $"Endereço {i + 1}", erros);

        for (var i = 0; i < p.MeiosContato.Count; i++)
            ValidarMeio(p.MeiosContato[i], $"Telefone/e-mail {i + 1}", erros);

        for (var i = 0; i < p.Contatos.Count; i++)
            ValidarContato(p.Contatos[i], $"Pessoa de contato {i + 1}", erros);

        for (var i = 0; i < p.Documentos.Count; i++)
            ValidarDocumento(p.Documentos[i], $"Documento {i + 1}", erros);

        foreach (var c in p.ContasCliente)
            ValidarContaCliente(c, erros);

        foreach (var f in p.ContasFornecedor)
            ValidarContaFornecedor(f, erros);

        ValidarDadosComplementares(p, erros);

        return erros;
    }

    private static void ValidarIdentificacao(Pessoa p, List<string> erros)
    {
        if (p.Nome.Length == 0)
            erros.Add(p.Natureza == NaturezaPessoa.Juridica ? "Informe a razão social." : "Informe o nome.");
        else if (p.Nome.Length > 150)
            erros.Add("O nome pode ter no máximo 150 caracteres.");

        if (p.Natureza == NaturezaPessoa.Fisica && p.DocumentoPrincipal is not null && !Cpf.EhValido(p.DocumentoPrincipal))
            erros.Add("CPF inválido.");

        if (p.Natureza == NaturezaPessoa.Estrangeiro && p.DocumentoPrincipal is { Length: > 20 })
            erros.Add("A identificação do estrangeiro pode ter no máximo 20 caracteres.");

        if (p.DataNascimento is { } nascimento && nascimento > DateOnly.FromDateTime(DateTime.Today))
            erros.Add("A data de nascimento não pode ser no futuro.");
    }

    /// <summary>Dados pessoais, da empresa e de relacionamento (tamanhos batem com as colunas do banco).</summary>
    private static void ValidarDadosComplementares(Pessoa p, List<string> erros)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        Limite(p.Nacionalidade, 60, "A nacionalidade", erros);
        Limite(p.SituacaoMotivo, Pessoa.TamanhoMaximoMotivo, "O motivo da situação", erros);
        Limite(p.NomeMae, 150, "O nome da mãe", erros);
        Limite(p.NomePai, 150, "O nome do pai", erros);
        Limite(p.Profissao, 80, "A profissão", erros);

        if (p.DataAbertura is { } abertura && abertura > hoje)
            erros.Add("A data de abertura da empresa não pode ser no futuro.");
        if (p.CapitalSocial is < 0)
            erros.Add("O capital social não pode ser negativo.");
        Limite(p.Porte, 60, "O porte", erros);
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
        foreach (var c in p.Consentimentos)
        {
            if (!Enum.IsDefined(c.Canal)) erros.Add("Canal de comunicação inválido.");
            Limite(c.Origem, 80, "A forma de autorização", erros);
        }

        if (p.PrimeiroContatoEm is { } primeiro && primeiro > hoje)
            erros.Add("A data do primeiro contato não pode ser no futuro.");
        Limite(p.OrigemCadastro, 60, "A origem do cadastro", erros);

        if (p.Etiquetas.Count > 20)
            erros.Add("Use no máximo 20 etiquetas por cadastro.");
        if (p.Etiquetas.Any(e => e.Texto.Length > PessoaEtiqueta.TamanhoMaximo))
            erros.Add($"Cada etiqueta pode ter no máximo {PessoaEtiqueta.TamanhoMaximo} caracteres.");
    }

    private static void Limite(string? texto, int maximo, string campo, List<string> erros)
    {
        if (texto is { Length: var tamanho } && tamanho > maximo)
            erros.Add($"{campo} pode ter no máximo {maximo} caracteres.");
    }

    private static void ValidarEstabelecimentos(Pessoa p, List<string> erros)
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

                if (e.Cnpj is null)
                    erros.Add($"{rotulo}: informe o CNPJ.");
                else if (!Cnpj.EhValido(e.Cnpj))
                    erros.Add($"{rotulo}: CNPJ inválido.");
                else if (!cnpjs.Add(e.Cnpj))
                    erros.Add($"{rotulo}: CNPJ repetido.");
                else if (raiz is not null && !e.Cnpj.StartsWith(raiz, StringComparison.Ordinal))
                    erros.Add($"{rotulo}: o CNPJ é de outra empresa (raiz diferente). Filiais precisam ter a mesma raiz da matriz; outra raiz é outra pessoa.");
            }
        }

        var idsEnderecos = p.Enderecos.Select(e => e.Id).ToHashSet();
        for (var i = 0; i < p.Estabelecimentos.Count; i++)
        {
            var e = p.Estabelecimentos[i];
            var rotulo = p.Natureza == NaturezaPessoa.Juridica ? $"Estabelecimento {i + 1}" : "Fiscal";
            ValidarFiscal(e, rotulo, erros);

            // O endereço fiscal precisa ser um dos endereços desta mesma pessoa.
            if (e.EnderecoFiscalId is Guid enderecoId && !idsEnderecos.Contains(enderecoId))
                erros.Add($"{rotulo}: o endereço fiscal escolhido não está entre os endereços do cadastro.");
        }
    }

    private static void ValidarFiscal(Estabelecimento e, string rotulo, List<string> erros)
    {
        if (e.IndicadorIE == IndicadorIE.Contribuinte && e.InscricaoEstadual is null)
            erros.Add($"{rotulo}: contribuinte do ICMS precisa ter inscrição estadual.");
        if (e.InscricaoEstadual is { Length: > 14 })
            erros.Add($"{rotulo}: inscrição estadual com mais de 14 caracteres.");
        if (e.InscricaoSuframa is { Length: not (8 or 9) })
            erros.Add($"{rotulo}: a inscrição SUFRAMA deve ter 8 ou 9 dígitos.");
        if (e.CnaePrincipal is { Length: not 7 })
            erros.Add($"{rotulo}: o CNAE deve ter 7 dígitos.");
    }

    private static void ValidarEndereco(PessoaEndereco e, string rotulo, List<string> erros)
    {
        if (e.Logradouro.Length == 0)
            erros.Add($"{rotulo}: informe o logradouro.");

        if (e.EhBrasil)
        {
            if (e.Cep is not null && !Cep.EhValido(e.Cep))
                erros.Add($"{rotulo}: o CEP deve ter 8 dígitos.");
            // Município só da tabela do IBGE (a API confere se existe e copia nome, UF e código).
            if (e.MunicipioId is null)
                erros.Add($"{rotulo}: escolha a UF e o município na lista.");
        }
        else
        {
            if (e.Cidade.Length == 0)
                erros.Add($"{rotulo}: informe a cidade.");
            if (e.Pais.Length == 0)
                erros.Add($"{rotulo}: informe o país.");
        }
    }

    private static void ValidarMeio(MeioContato m, string rotulo, List<string> erros)
    {
        if (m.Valor.Length == 0)
        {
            erros.Add($"{rotulo}: informe o número ou e-mail.");
            return;
        }

        if (m.Tipo == TipoContato.Email && !Email.EhValido(m.Valor))
            erros.Add($"{rotulo}: e-mail inválido.");
        else if (m.Tipo is TipoContato.Telefone or TipoContato.Celular or TipoContato.WhatsApp && !Telefone.EhValido(m.Valor))
            erros.Add($"{rotulo}: telefone inválido. Informe com DDD, ou com + e o código do país se for do exterior.");
    }

    private static void ValidarContato(Contato c, string rotulo, List<string> erros)
    {
        if (c.Nome.Length == 0)
            erros.Add($"{rotulo}: informe o nome.");
        if (c.Telefone is not null && !Telefone.EhValido(c.Telefone))
            erros.Add($"{rotulo}: telefone inválido.");
        if (c.Celular is not null && !Telefone.EhValido(c.Celular))
            erros.Add($"{rotulo}: celular inválido.");
        if (c.Email is not null && !Email.EhValido(c.Email))
            erros.Add($"{rotulo}: e-mail inválido.");
    }

    private static void ValidarDocumento(PessoaDocumento d, string rotulo, List<string> erros)
    {
        if (d.Numero.Length == 0)
            erros.Add($"{rotulo}: informe o número.");
        if (d.Uf is not null && !Ufs.Valida(d.Uf))
            erros.Add($"{rotulo}: UF inválida.");
        if (d.EmitidoEm is { } emissao && d.ValidoAte is { } validade && validade < emissao)
            erros.Add($"{rotulo}: a validade é anterior à emissão.");
    }

    private static void ValidarContaCliente(ContaCliente c, List<string> erros)
    {
        if (c.LimiteCredito < 0)
            erros.Add("Cliente: o limite de crédito não pode ser negativo.");
        if (c.DescontoMaximo is < 0m or > 100m)
            erros.Add("Cliente: o desconto máximo deve ficar entre 0% e 100%.");
        if (c.DiasMaximoAtraso < 0)
            erros.Add("Cliente: dias máximos de atraso não podem ser negativos.");
    }

    private static void ValidarContaFornecedor(ContaFornecedor f, List<string> erros)
    {
        if (f.PrazoMedioDias < 0 || f.LeadTimeDias < 0)
            erros.Add("Fornecedor: prazos não podem ser negativos.");
        if (f.Avaliacao is < 1 or > 5)
            erros.Add("Fornecedor: a avaliação deve ser de 1 a 5.");
    }
}
