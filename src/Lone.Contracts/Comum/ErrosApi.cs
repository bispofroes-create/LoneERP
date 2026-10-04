namespace Lone.Contracts.Comum;

/// <summary>
/// Como a API descreve os erros (corpo ProblemDetails, RFC 9457). O aplicativo lê o "codigo" e
/// transforma a resposta de volta na mesma exceção que o servidor lançou.
/// </summary>
public static class ErrosApi
{
    /// <summary>Nome da extensão do ProblemDetails com o código do erro.</summary>
    public const string CampoCodigo = "codigo";

    /// <summary>Nome da extensão com a lista de mensagens de validação.</summary>
    public const string CampoErros = "erros";

    /// <summary>
    /// Nome da extensão com os mesmos erros de validação e o campo de cada um (<see cref="ItemErroApi"/>), quando a regra
    /// sabe. Complementa "erros" (que continua igual): quem não conhece "itens" segue funcionando.
    /// </summary>
    public const string CampoItens = "itens";

    /// <summary>Nome da extensão com a permissão que faltou (acesso negado).</summary>
    public const string CampoPermissao = "permissao";

    /// <summary>Nome da extensão com a situação do login (credenciais, bloqueado, inativo).</summary>
    public const string CampoSituacaoLogin = "situacaoLogin";

    /// <summary>HTTP 400: regra de negócio (mensagens em "erros").</summary>
    public const string Validacao = "validacao";

    /// <summary>HTTP 409: outro usuário gravou o registro antes.</summary>
    public const string Conflito = "conflito";

    /// <summary>HTTP 403: falta permissão (código em "permissao").</summary>
    public const string AcessoNegado = "acesso_negado";

    /// <summary>HTTP 401: sessão ausente, expirada ou revogada.</summary>
    public const string NaoAutenticado = "nao_autenticado";

    /// <summary>HTTP 401 no login: credenciais, bloqueio ou usuário inativo (detalhe em "situacaoLogin").</summary>
    public const string LoginRecusado = "login_recusado";

    /// <summary>HTTP 403: o usuário precisa trocar a senha antes de usar o sistema.</summary>
    public const string TrocaDeSenhaObrigatoria = "troca_senha_obrigatoria";

    /// <summary>HTTP 404: registro não existe.</summary>
    public const string NaoEncontrado = "nao_encontrado";

    /// <summary>HTTP 502: serviço externo (CNPJ, CEP) fora do ar ou com erro.</summary>
    public const string ServicoExterno = "servico_externo";
}

/// <summary>Um erro de validação na resposta: o texto e, quando houver, o campo da ficha e o registro da lista.</summary>
public sealed class ItemErroApi
{
    public string Mensagem { get; set; } = string.Empty;

    /// <summary>Id estável do campo (ex.: "identificacao.documento"); nulo = erro geral.</summary>
    public string? Campo { get; set; }

    /// <summary>Id do registro da lista (endereço, documento...) a que o campo pertence; nulo = campo da própria ficha.</summary>
    public Guid? Item { get; set; }
}
