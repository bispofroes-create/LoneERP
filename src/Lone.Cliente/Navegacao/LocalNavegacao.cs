namespace Lone.Cliente.Navegacao;

/// <summary>
/// Um registro aberto numa tela (a "ficha"): tipo, identificação e o nome que o usuário reconhece. Só o nome de exibição —
/// nunca documento, CPF ou outro dado sensível (a navegação aparece em dicas, leitor de tela e, no futuro, recentes).
/// <see cref="Novo"/> = ficha de inclusão, ainda não gravada. <see cref="Id"/> pode faltar numa tela que não expõe a
/// identificação: aí o registro é reconhecido pelo nome (e não pode ser reaberto por endereço).
/// </summary>
public sealed record ReferenciaRegistro(string Tipo, Guid? Id, string Titulo, bool Novo = false)
{
    /// <summary>Mesmo registro (tipo e identificação); o título pode ter mudado (ex.: renomeado ao salvar).</summary>
    public bool MesmoQue(ReferenciaRegistro? outro) =>
        outro is not null && string.Equals(Tipo, outro.Tipo, StringComparison.Ordinal) && Novo == outro.Novo &&
        (Id is { } id ? id == outro.Id : outro.Id is null && string.Equals(Titulo, outro.Titulo, StringComparison.Ordinal));
}

/// <summary>
/// Onde o usuário está: a tela (rota do menu) e, se houver, o registro aberto nela. Abas, filtros e rolagem não entram:
/// são estado interno da tela, não navegação (trocar de aba não cria um "Voltar").
/// </summary>
public sealed record LocalNavegacao(string Rota, ReferenciaRegistro? Registro = null)
{
    public const string Esquema = "lone://";

    /// <summary>Mesmo lugar: mesma tela e mesmo registro (ou nenhum). Títulos não contam.</summary>
    public bool MesmoQue(LocalNavegacao? outro) =>
        outro is not null && string.Equals(Rota, outro.Rota, StringComparison.Ordinal) &&
        (Registro is null ? outro.Registro is null : Registro.MesmoQue(outro.Registro));

    /// <summary>
    /// Endereço interno (base de links, notificações, busca global e deep link): <c>lone://pessoas</c> ou
    /// <c>lone://pessoas/pessoa/{id}</c>. Nunca leva o nome (dados pessoais fora de endereços).
    /// </summary>
    public string Endereco => Registro is { Id: { } id } r ? $"{Esquema}{Rota}/{r.Tipo}/{id:D}" : $"{Esquema}{Rota}";

    /// <summary>Lê um endereço interno. O título do registro fica vazio: quem abre descobre o nome ao carregar.</summary>
    public static bool TentarLer(string? endereco, out LocalNavegacao local)
    {
        local = null!;
        if (endereco is null || !endereco.StartsWith(Esquema, StringComparison.OrdinalIgnoreCase)) return false;
        var partes = endereco[Esquema.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 1 && Valida(partes[0]))
        {
            local = new LocalNavegacao(partes[0]);
            return true;
        }
        if (partes.Length == 3 && Valida(partes[0]) && Valida(partes[1]) && Guid.TryParse(partes[2], out var id))
        {
            local = new LocalNavegacao(partes[0], new ReferenciaRegistro(partes[1], id, string.Empty));
            return true;
        }
        return false;
    }

    private static bool Valida(string parte) => parte.Length > 0 && parte.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
}

/// <summary>Como o usuário chegou (entra no histórico; base de análise de uso no futuro, sem telemetria agora).</summary>
public enum OrigemNavegacao
{
    /// <summary>Menu lateral, favoritos ou recentes do menu: muda de área.</summary>
    Menu,
    /// <summary>Escolheu na lista da própria tela (navegação entre registros do mesmo conjunto).</summary>
    Lista,
    /// <summary>Link interno ("Abrir", "Abrir ficha", nome clicável): leva a outro lugar.</summary>
    Link,
    /// <summary>Voltar (botão, Alt+←, botão "voltar" do mouse ou do celular).</summary>
    Voltar,
    /// <summary>Busca global (futura).</summary>
    Busca,
    /// <summary>Endereço interno lone:// (futuro: notificações, integrações).</summary>
    Endereco,
    /// <summary>O próprio sistema (abertura, troca de contexto).</summary>
    Sistema
}

/// <summary>
/// Uma parada no histórico: o lugar, como se chegou e o nome da tela (para "Voltar para …"). Imutável.
/// <see cref="Estado"/> é o gancho da fase de preservação de contexto (filtros, busca, rolagem): estado de navegação,
/// só em memória, nunca dado de negócio nem banco.
/// </summary>
/// <param name="SoTela">Chegou pedindo só a tela (sem registro): um link desses pode ainda abrir a ficha logo depois.</param>
public sealed record EntradaNavegacao(LocalNavegacao Local, OrigemNavegacao Origem, string TituloTela, DateTimeOffset Quando,
                                      object? Estado = null, bool SoTela = false)
{
    /// <summary>O que o usuário reconhece: o registro, se houver; senão, a tela.</summary>
    public string Titulo => Local.Registro is { Titulo.Length: > 0 } r ? r.Titulo : TituloTela;
}
