using Lone.Core.Entidades;
using Lone.Core.Enums;
using Lone.Core.ObjetosDeValor;

namespace Lone.Core.Validacao;

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

        for (var i = 0; i < p.Estabelecimentos.Count; i++)
            ValidarFiscal(p.Estabelecimentos[i],
                p.Natureza == NaturezaPessoa.Juridica ? $"Estabelecimento {i + 1}" : "Fiscal", erros);
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
        if (e.Cidade.Length == 0)
            erros.Add($"{rotulo}: informe a cidade.");

        if (e.EhBrasil)
        {
            if (e.Cep is not null && !Cep.EhValido(e.Cep))
                erros.Add($"{rotulo}: o CEP deve ter 8 dígitos.");
            if (!Ufs.Valida(e.Uf))
                erros.Add($"{rotulo}: UF inválida.");
            if (e.CodigoMunicipioIbge is not null && e.CodigoMunicipioIbge.Length != 7)
                erros.Add($"{rotulo}: o código IBGE do município deve ter 7 dígitos.");
        }
        else if (e.Pais.Length == 0)
        {
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
