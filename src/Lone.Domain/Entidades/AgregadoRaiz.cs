namespace Lone.Domain.Entidades;

/// <summary>
/// Entidade principal de um agregado (ex.: Pessoa). Tem controle de concorrência:
/// se outro usuário gravar antes, a gravação é recusada em vez de sobrescrever.
/// Também guarda os eventos de negócio da operação (ex.: "Cadastro desativado"), que a auditoria grava
/// junto com a alteração, na mesma transação.
/// </summary>
public abstract class AgregadoRaiz : EntidadeBase
{
    private readonly List<string> _eventos = new();

    /// <summary>Versão do registro (rowversion do SQL Server), mudada pelo banco a cada gravação.</summary>
    public byte[]? Versao { get; set; }

    /// <summary>Eventos ainda não gravados, em ordem (não é coluna do banco).</summary>
    public IReadOnlyList<string> EventosPendentes => _eventos;

    /// <summary>Registra um fato em linguagem do usuário, ex.: "Cliente João da Silva foi desativado."</summary>
    public void RegistrarEvento(string descricao)
    {
        if (!string.IsNullOrWhiteSpace(descricao)) _eventos.Add(descricao.Trim());
    }

    /// <summary>Passa os eventos de outra instância do mesmo registro (a que foi editada) para esta (a que será gravada).</summary>
    public void ReceberEventosDe(AgregadoRaiz outro)
    {
        if (ReferenceEquals(outro, this)) return;
        _eventos.AddRange(outro._eventos);
        outro._eventos.Clear();
    }

    /// <summary>Entrega e esvazia os eventos (usado pela auditoria ao gravar).</summary>
    public IReadOnlyList<string> RetirarEventos()
    {
        var eventos = _eventos.ToList();
        _eventos.Clear();
        return eventos;
    }
}
