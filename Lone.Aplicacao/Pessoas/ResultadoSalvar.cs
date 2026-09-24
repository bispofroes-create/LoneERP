using Lone.Core.Entidades;

namespace Lone.Aplicacao.Pessoas;

/// <summary>Resultado de uma gravação: a pessoa salva e avisos que não impedem o salvamento.</summary>
public sealed class ResultadoSalvar
{
    public ResultadoSalvar(Pessoa pessoa, IReadOnlyList<string> avisos)
    {
        Pessoa = pessoa;
        Avisos = avisos;
    }

    public Pessoa Pessoa { get; }
    public IReadOnlyList<string> Avisos { get; }
}
