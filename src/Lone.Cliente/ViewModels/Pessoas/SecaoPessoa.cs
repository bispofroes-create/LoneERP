namespace Lone.Cliente.ViewModels.Pessoas;

public enum SecaoPessoa
{
    /// <summary>Aba "Identificação": quem é a pessoa (nomes, CPF/CNPJ, identidade da empresa, papéis, etiquetas).</summary>
    Geral,
    Pessoais,
    Estabelecimentos,
    Enderecos,
    Contatos,
    Documentos,
    /// <summary>Cliente e Fornecedor na mesma aba, cada um num bloco (uma pessoa pode ter os dois).</summary>
    Comercial,
    /// <summary>Aba "Interações e LGPD": segmentação, consentimentos e interações (as etiquetas ficam na Identificação).</summary>
    Relacionamento,
    Historico,
    Adicionais,
    Situacao,
    Colaborador,
    /// <summary>Relacionamentos com outros cadastros (sócio de, administrador de, contato de...).</summary>
    RelacionamentosPessoas
}

/// <summary>Aba da ficha. Algumas só aparecem quando fazem sentido (ex.: "Comercial" com o papel Cliente ou Fornecedor).</summary>
public sealed record SecaoOpcao(SecaoPessoa Secao, string Texto)
{
    /// <summary>
    /// Abas visíveis para a ficha como está agora, na ordem de uso: quem é (identificação, dados pessoais ou empresa),
    /// como falar com ela (contatos, endereços), documentos e dados fiscais, relação comercial (cliente e
    /// fornecedor juntos), colaborador, relacionamentos com outros cadastros, informações adicionais, interações e
    /// LGPD, situação e histórico.
    /// </summary>
    public static IReadOnlyList<SecaoOpcao> Para(PessoaFormulario f)
    {
        var secoes = new List<SecaoOpcao> { new(SecaoPessoa.Geral, "Identificação") };

        if (f.EhFisica) secoes.Add(new(SecaoPessoa.Pessoais, "Dados pessoais"));
        if (f.EhJuridica) secoes.Add(new(SecaoPessoa.Estabelecimentos, "Estabelecimentos"));
        secoes.Add(new(SecaoPessoa.Contatos, "Contatos"));
        secoes.Add(new(SecaoPessoa.Enderecos, "Endereços"));
        // Documentos valem para qualquer natureza (RG, CNH, passaporte, alvará, contrato social...).
        secoes.Add(new(SecaoPessoa.Documentos, "Documentos"));
        if (!f.EhJuridica) secoes.Add(new(SecaoPessoa.Estabelecimentos, "Dados fiscais"));
        if (f.PapelCliente.Ativo || f.PapelFornecedor.Ativo) secoes.Add(new(SecaoPessoa.Comercial, "Comercial"));
        if (f.TemColaborador) secoes.Add(new(SecaoPessoa.Colaborador, "Colaborador"));
        // Relacionamentos gravam na hora (fora do Salvar): só depois que o cadastro existe.
        if (f.Existente) secoes.Add(new(SecaoPessoa.RelacionamentosPessoas, "Relacionamentos"));
        if (f.TemInformacoesAdicionais) secoes.Add(new(SecaoPessoa.Adicionais, "Informações adicionais"));
        secoes.Add(new(SecaoPessoa.Relacionamento, "Interações e LGPD"));
        secoes.Add(new(SecaoPessoa.Situacao, "Situação"));
        if (f.Existente) secoes.Add(new(SecaoPessoa.Historico, "Histórico"));
        return secoes;
    }
}
