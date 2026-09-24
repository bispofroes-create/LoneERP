namespace Lone.Core.Entidades;

/// <summary>
/// Entidade principal de um agregado (ex.: Pessoa). Tem controle de concorrência:
/// se outro usuário gravar antes, a gravação é recusada em vez de sobrescrever.
/// </summary>
public abstract class AgregadoRaiz : EntidadeBase
{
    /// <summary>Versão do registro (rowversion do SQL Server), mudada pelo banco a cada gravação.</summary>
    public byte[]? Versao { get; set; }
}
