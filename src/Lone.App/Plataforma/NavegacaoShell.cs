using Lone.Cliente.Navegacao;

namespace Lone.App.Plataforma;

/// <summary>
/// O que o Shell oferece ao motor de navegação (<see cref="GerenciadorNavegacao"/>): trocar de tela, dizer a rota aberta
/// e a tela (ViewModel) dela. Não guarda páginas: consulta o Shell na hora, então nunca aponta para uma tela destruída.
/// </summary>
public sealed class NavegacaoShell : IPlataformaNavegacao
{
    private readonly Shell _shell;

    public NavegacaoShell(Shell shell) => _shell = shell;

    public string? RotaAtual => Rota(_shell.CurrentState?.Location?.OriginalString);

    public ITelaNavegavel? TelaAtual => _shell.CurrentPage?.BindingContext as ITelaNavegavel;

    /// <summary>Rotas do Lone são absolutas ("//pessoas"): cada tela do menu é uma área, sem pilha do Shell por baixo.</summary>
    public Task IrParaRotaAsync(string rota) => _shell.GoToAsync("//" + rota);

    /// <summary>"//pessoas" ou "//pessoas?x=1" → "pessoas".</summary>
    public static string? Rota(string? localizacao) =>
        (localizacao ?? string.Empty).Split('?')[0].TrimEnd('/').Split('/').LastOrDefault(r => r.Length > 0);
}
