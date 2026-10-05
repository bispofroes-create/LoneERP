namespace Lone.Cliente.ViewModels.Comum;

/// <summary>
/// Um valor que a consulta (Receita) traz para um campo que já tinha outro valor: fica para o usuário escolher na
/// conferência "atual × Receita" (nada é trocado sem ele marcar).
/// </summary>
public sealed record MudancaReceita(ChaveCampo Chave, string Rotulo, string Atual, string DaReceita, Action Aplicar);

/// <summary>
/// Aplica o resultado de uma consulta (CNPJ) campo a campo. Campo vazio, ou cadastro novo: preenche direto. Campo de um
/// cadastro gravado com outro valor: não troca; vira uma <see cref="MudancaReceita"/> para a conferência. Valor igual (sem
/// contar maiúsculas, espaços e pontuação de números): nada a fazer. Guarda os campos preenchidos para o destaque saber que
/// o valor veio da consulta.
/// </summary>
public sealed class AplicacaoReceita
{
    private readonly bool _conferir;
    private readonly bool _somenteVazios;
    private readonly List<MudancaReceita> _conflitos = new();
    private readonly List<ChaveCampo> _aplicados = new();

    /// <param name="conferir">Cadastro já gravado: valores diferentes dos atuais vão para a conferência.</param>
    /// <param name="somenteVazios">
    /// Troca de empresa numa ficha nova: os valores da consulta anterior já foram limpos; o que ainda tem valor foi digitado
    /// pelo usuário e fica (a consulta só preenche os vazios, sem conferência).
    /// </param>
    public AplicacaoReceita(bool conferir, bool somenteVazios = false)
    {
        _conferir = conferir;
        _somenteVazios = somenteVazios;
    }

    public IReadOnlyList<MudancaReceita> Conflitos => _conflitos;
    public IReadOnlyList<ChaveCampo> Aplicados => _aplicados;

    /// <summary>Um campo de texto (ou mostrado como texto): <paramref name="aplicar"/> recebe o valor da consulta.</summary>
    public void Valor(ChaveCampo chave, string rotulo, string? atual, string? daReceita, Action<string> aplicar)
    {
        if (string.IsNullOrWhiteSpace(daReceita)) return; // a consulta não trouxe: fica o que está
        var valor = daReceita.Trim();
        if (Igual(atual, valor)) return;
        if (_somenteVazios && !string.IsNullOrWhiteSpace(atual)) return; // digitado pelo usuário: fica
        if (_conferir && !string.IsNullOrWhiteSpace(atual))
        {
            _conflitos.Add(new MudancaReceita(chave, rotulo, atual.Trim(), valor, () => aplicar(valor)));
            return;
        }
        aplicar(valor);
        _aplicados.Add(chave);
    }

    /// <summary>Preenchido direto, sem passar por <see cref="Valor"/> (ex.: telefone novo da consulta).</summary>
    public void Marcar(ChaveCampo chave) => _aplicados.Add(chave);

    /// <summary>Mesmo valor para quem lê: sem diferença de maiúsculas e espaços; números sem pontuação ("35.790-000" = "35790000").</summary>
    public static bool Igual(string? a, string? b)
    {
        var x = Normalizar(a);
        var y = Normalizar(b);
        if (x == y) return true;
        var dx = SoDigitos(x);
        var dy = SoDigitos(y);
        return dx is not null && dx == dy;
    }

    private static string Normalizar(string? s) =>
        string.Join(' ', (s ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    /// <summary>Os dígitos, se o texto é só número com pontuação (".", ",", "-", "/", espaço); senão nulo.</summary>
    private static string? SoDigitos(string s)
    {
        if (s.Length == 0 || !s.All(c => char.IsAsciiDigit(c) || c is '.' or ',' or '-' or '/' or ' ')) return null;
        return new string(s.Where(char.IsAsciiDigit).ToArray());
    }
}
