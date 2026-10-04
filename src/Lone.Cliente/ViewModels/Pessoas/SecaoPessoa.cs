using Lone.Domain.Pessoas;

namespace Lone.Cliente.ViewModels.Pessoas;

public enum SecaoPessoa
{
    /// <summary>Aba "Identificação": quem é a pessoa (nomes, CPF/CNPJ, identidade da empresa, papéis, etiquetas).</summary>
    Geral,
    Pessoais,
    /// <summary>Aba fiscal: "Fiscal e estabelecimentos" (PJ: matriz, filiais e dados fiscais de cada CNPJ) ou "Fiscal" (demais).</summary>
    Estabelecimentos,
    Enderecos,
    Contatos,
    Documentos,
    /// <summary>Cliente e Fornecedor na mesma aba, cada um num bloco (uma pessoa pode ter os dois).</summary>
    Comercial,
    /// <summary>Aba "Interações": origem, primeiro contato e interações (as etiquetas ficam na Identificação).</summary>
    Relacionamento,
    Historico,
    Adicionais,
    Situacao,
    Colaborador,
    /// <summary>Relacionamentos com outros cadastros (sócio de, administrador de, contato de...).</summary>
    RelacionamentosPessoas,
    /// <summary>Aba "Privacidade" (LGPD): consentimentos por finalidade, canais e decisões (permissão PESSOAS.PRIVACIDADE).</summary>
    Privacidade
}

/// <summary>Aba da ficha. Algumas só aparecem quando fazem sentido (ex.: "Comercial" com o papel Cliente ou Fornecedor).</summary>
public sealed record SecaoOpcao(SecaoPessoa Secao, string Texto)
{
    /// <summary>
    /// Abas visíveis para a ficha como está agora, na ordem de uso: quem é (identificação, dados pessoais),
    /// como falar com ela (contatos, endereços), documentos, fiscal (e estabelecimentos, na PJ), relação comercial (cliente e
    /// fornecedor juntos), colaborador, relacionamentos com outros cadastros, informações adicionais, interações e
    /// LGPD, situação e histórico.
    /// </summary>
    public static IReadOnlyList<SecaoOpcao> Para(PessoaFormulario f)
    {
        var secoes = new List<SecaoOpcao> { new(SecaoPessoa.Geral, "Identificação") };

        if (f.EhFisica) secoes.Add(new(SecaoPessoa.Pessoais, "Dados pessoais"));
        secoes.Add(new(SecaoPessoa.Contatos, "Contatos"));
        secoes.Add(new(SecaoPessoa.Enderecos, "Endereços"));
        // Documentos valem para qualquer natureza (RG, CNH, passaporte, alvará, contrato social...).
        secoes.Add(new(SecaoPessoa.Documentos, "Documentos"));
        // Fiscal depois de Endereços: o endereço fiscal da filial é escolhido entre os endereços da pessoa.
        secoes.Add(new(SecaoPessoa.Estabelecimentos, f.EhJuridica ? "Fiscal e estabelecimentos" : "Fiscal"));
        if (f.PapelCliente.Ativo || f.PapelFornecedor.Ativo) secoes.Add(new(SecaoPessoa.Comercial, "Comercial"));
        if (f.TemColaborador) secoes.Add(new(SecaoPessoa.Colaborador, "Colaborador"));
        // Relacionamentos gravam na hora (fora do Salvar): só depois que o cadastro existe.
        if (f.Existente) secoes.Add(new(SecaoPessoa.RelacionamentosPessoas, "Relacionamentos"));
        if (f.TemInformacoesAdicionais) secoes.Add(new(SecaoPessoa.Adicionais, "Informações adicionais"));
        secoes.Add(new(SecaoPessoa.Relacionamento, "Interações"));
        // Consentimentos são gravados na hora (fora do Salvar): só depois que o cadastro existe, e com a permissão.
        if (f.Existente && f.PodeVerPrivacidade) secoes.Add(new(SecaoPessoa.Privacidade, "Privacidade"));
        secoes.Add(new(SecaoPessoa.Situacao, "Situação"));
        if (f.Existente) secoes.Add(new(SecaoPessoa.Historico, "Histórico"));
        return secoes;
    }
}

/// <summary>
/// Em que aba da ficha fica cada campo (Lone Contextual, Fase 1: o erro leva ao campo). Pela área do id
/// (<see cref="CamposFichaPessoa"/>), com as poucas exceções escritas aqui; a única tabela desse tipo.
/// </summary>
public static class AbaDoCampo
{
    private static readonly Dictionary<string, SecaoPessoa> PorArea = new(StringComparer.Ordinal)
    {
        ["identificacao"] = SecaoPessoa.Geral,
        ["pessoais"] = SecaoPessoa.Pessoais,
        ["contatos"] = SecaoPessoa.Contatos,
        ["pessoasContato"] = SecaoPessoa.Contatos,
        ["enderecos"] = SecaoPessoa.Enderecos,
        ["documentos"] = SecaoPessoa.Documentos,
        ["fiscal"] = SecaoPessoa.Estabelecimentos,
        ["cliente"] = SecaoPessoa.Comercial,
        ["fornecedor"] = SecaoPessoa.Comercial,
        ["colaborador"] = SecaoPessoa.Colaborador,
        ["interacoes"] = SecaoPessoa.Relacionamento,
        ["adicionais"] = SecaoPessoa.Adicionais
    };

    /// <summary>
    /// Campos que, num registro de lista (com item), ficam fora da aba da sua área: a natureza jurídica do principal fica na
    /// Identificação (sem item); a de cada filial, no cartão dela, na aba Fiscal e estabelecimentos.
    /// </summary>
    private static readonly Dictionary<string, SecaoPessoa> ExcecoesComItem = new(StringComparer.Ordinal)
    {
        [CamposFichaPessoa.NaturezaJuridica] = SecaoPessoa.Estabelecimentos
    };

    /// <summary>A aba do campo; nula para um id desconhecido (o erro continua no resumo, sem levar a lugar nenhum).</summary>
    public static SecaoPessoa? De(string? campo, Guid? item = null) =>
        campo is null ? null
        : item is not null && ExcecoesComItem.TryGetValue(campo, out var excecao) ? excecao
        : PorArea.TryGetValue(CamposFichaPessoa.Area(campo), out var aba) ? aba
        : null;
}
