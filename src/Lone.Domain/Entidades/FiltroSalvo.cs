using System.ComponentModel;

namespace Lone.Domain.Entidades;

/// <summary>
/// Filtro da consulta avançada de pessoas salvo com nome. Os critérios ficam em JSON de um tipo fechado
/// (CriteriosPessoas): nunca SQL nem fórmula. Do usuário que criou; "compartilhado" aparece para todos, mas só o
/// autor altera. Nunca é excluído: desativado.
/// </summary>
[DisplayName("Filtro salvo")]
public class FiltroSalvo : AgregadoRaiz
{
    public const int TamanhoMaximoNome = 80;
    public const int TamanhoMaximoCriterios = 4000;

    [DisplayName("Nome")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Usuário dono (Id do login).</summary>
    public Guid UsuarioId { get; set; }

    [DisplayName("Autor")]
    public string Autor { get; set; } = string.Empty;

    [DisplayName("Compartilhado")]
    public bool Compartilhado { get; set; }

    [DisplayName("Critérios")]
    public string Criterios { get; set; } = "{}";

    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;
}
