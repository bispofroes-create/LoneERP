namespace Lone.Cliente.Plataforma;

/// <summary>Guarda segredos no cofre do aparelho (Windows: DPAPI; Android: Keystore). Implementado pelo aplicativo.</summary>
public interface IArmazenamentoSeguro
{
    Task<string?> LerAsync(string chave);
    Task GravarAsync(string chave, string valor);
    void Remover(string chave);
}

/// <summary>Preferências simples do aparelho (não secretas), como o endereço do servidor.</summary>
public interface IPreferencias
{
    string? Ler(string chave);
    void Gravar(string chave, string valor);
    void Remover(string chave);
}

/// <summary>Informações do aparelho mostradas nas sessões abertas do usuário (ex.: "Windows - PC-CAIXA").</summary>
public interface IDispositivo
{
    string Descricao { get; }

    /// <summary>Endereço padrão da API neste aparelho (o emulador Android enxerga o PC como 10.0.2.2).</summary>
    string ServidorPadrao { get; }
}
