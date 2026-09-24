namespace Lone.Cliente.ViewModels.Pessoas;

public enum SecaoPessoa
{
    Geral,
    Pessoais,
    Estabelecimentos,
    Enderecos,
    Contatos,
    Documentos,
    Cliente,
    Fornecedor,
    Relacionamento,
    Historico,
    Adicionais,
    Situacao
}

/// <summary>Aba da ficha. Algumas só aparecem quando fazem sentido (ex.: "Cliente" com o papel ligado).</summary>
public sealed record SecaoOpcao(SecaoPessoa Secao, string Texto)
{
    /// <summary>
    /// Abas visíveis para a ficha como está agora, na ordem de uso: quem é (geral, dados pessoais ou empresa),
    /// como falar com ela (contatos, endereços), documentos e dados fiscais, relação comercial (cliente,
    /// fornecedor), informações adicionais do administrador, LGPD, situação e histórico.
    /// </summary>
    public static IReadOnlyList<SecaoOpcao> Para(PessoaFormulario f)
    {
        var secoes = new List<SecaoOpcao> { new(SecaoPessoa.Geral, "Geral") };

        if (f.EhFisica) secoes.Add(new(SecaoPessoa.Pessoais, "Dados pessoais"));
        if (f.EhJuridica) secoes.Add(new(SecaoPessoa.Estabelecimentos, "Empresa e estabelecimentos"));
        secoes.Add(new(SecaoPessoa.Contatos, "Contatos"));
        secoes.Add(new(SecaoPessoa.Enderecos, "Endereços"));
        if (!f.EhJuridica)
        {
            secoes.Add(new(SecaoPessoa.Documentos, "Documentos"));
            secoes.Add(new(SecaoPessoa.Estabelecimentos, "Dados fiscais"));
        }
        if (f.PapelCliente.Ativo) secoes.Add(new(SecaoPessoa.Cliente, "Cliente"));
        if (f.PapelFornecedor.Ativo) secoes.Add(new(SecaoPessoa.Fornecedor, "Fornecedor"));
        if (f.TemInformacoesAdicionais) secoes.Add(new(SecaoPessoa.Adicionais, "Informações adicionais"));
        secoes.Add(new(SecaoPessoa.Relacionamento, "Relacionamento e LGPD"));
        secoes.Add(new(SecaoPessoa.Situacao, "Situação"));
        if (f.Existente) secoes.Add(new(SecaoPessoa.Historico, "Histórico"));
        return secoes;
    }
}
