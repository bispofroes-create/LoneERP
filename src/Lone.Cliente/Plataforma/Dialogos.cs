namespace Lone.Cliente.Plataforma;

/// <summary>Perguntas ao usuário em janela (confirmar, informar um texto). Implementado pelo aplicativo.</summary>
public interface IDialogos
{
    /// <summary>Verdadeiro se o usuário escolheu <paramref name="aceitar"/>.</summary>
    Task<bool> ConfirmarAsync(string titulo, string mensagem, string aceitar, string cancelar);

    /// <summary>Texto digitado, ou nulo se o usuário cancelou.</summary>
    Task<string?> PerguntarAsync(string titulo, string mensagem, string aceitar, string cancelar, string? dica = null, int tamanhoMaximo = 200);
}
