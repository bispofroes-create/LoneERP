using Lone.Cliente.Plataforma;

namespace Lone.App.Plataforma;

/// <summary>Janelas de confirmação e de pergunta do sistema (Windows e Android), sobre a página atual.</summary>
public sealed class DialogosMaui : IDialogos
{
    private static Page Pagina =>
        Application.Current?.Windows.FirstOrDefault()?.Page ?? throw new InvalidOperationException("Nenhuma janela aberta.");

    public Task<bool> ConfirmarAsync(string titulo, string mensagem, string aceitar, string cancelar) =>
        MainThread.InvokeOnMainThreadAsync(() => Pagina.DisplayAlertAsync(titulo, mensagem, aceitar, cancelar));

    public Task<string?> PerguntarAsync(string titulo, string mensagem, string aceitar, string cancelar, string? dica = null, int tamanhoMaximo = 200) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
            (string?)await Pagina.DisplayPromptAsync(titulo, mensagem, aceitar, cancelar, dica ?? string.Empty, tamanhoMaximo, Keyboard.Text, string.Empty));

    public Task<string?> EscolherAsync(string titulo, string cancelar, IReadOnlyList<string> opcoes) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var escolha = await Pagina.DisplayActionSheetAsync(titulo, cancelar, null, [.. opcoes]);
            return escolha is null || escolha == cancelar ? null : escolha;
        });
}
