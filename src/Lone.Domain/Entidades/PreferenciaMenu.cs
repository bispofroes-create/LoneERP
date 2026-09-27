using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

/// <summary>
/// Uso do menu por um usuário, uma linha por tela (rota do Shell): se é favorita e quando foi aberta por último.
/// Nunca é apagada: desfavoritar só desmarca, e os recentes são os acessos mais novos. Fica fora da auditoria de
/// cadastro, como <see cref="UsuarioAcesso"/>: muda a cada tela aberta e só geraria ruído no histórico.
/// </summary>
[NaoAuditar]
public class PreferenciaMenu : EntidadeBase
{
    public Guid UsuarioId { get; set; }

    /// <summary>Rota da tela no Shell (ex.: "pessoas", "papeis").</summary>
    public string Rota { get; set; } = string.Empty;

    public bool Favorito { get; set; }

    /// <summary>Quando foi marcada como favorita (ordena os favoritos); nulo quando não é favorita.</summary>
    public DateTime? FavoritadaEm { get; set; }

    /// <summary>Última vez que a tela foi aberta (ordena os recentes); nulo se só foi favoritada.</summary>
    public DateTime? UltimoAcessoEm { get; set; }
}
