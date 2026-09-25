namespace Lone.Application.Seguranca;

/// <summary>
/// Motivo informado pelo usuário para a operação desta requisição (ex.: "correção pedida pelo cliente"). O serviço
/// que recebe o motivo o define aqui; a gravação copia para todas as linhas de auditoria da operação.
/// Na API é o mesmo objeto do usuário da requisição (um por requisição).
/// </summary>
public interface IMotivoDaOperacao
{
    string? Motivo { get; set; }
}
