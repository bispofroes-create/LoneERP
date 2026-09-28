using Lone.Domain.Auditoria;

namespace Lone.Domain.Entidades;

/// <summary>
/// Preferência de uma tela por usuário (ex.: colunas e ordenação da lista de pessoas), guardada como JSON que só a tela
/// entende. Uma linha por usuário + tela. Fica fora da auditoria, como <see cref="PreferenciaMenu"/>: é ajuste pessoal de
/// apresentação e mudaria a cada clique.
/// </summary>
[NaoAuditar]
public class PreferenciaTela : EntidadeBase
{
    public Guid UsuarioId { get; set; }

    /// <summary>Nome da tela no formato de rota (ex.: "pessoas-lista").</summary>
    public string Tela { get; set; } = string.Empty;

    public string Conteudo { get; set; } = "{}";

    public DateTime AlteradaEm { get; set; }
}
