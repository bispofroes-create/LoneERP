using Lone.Contracts.Enderecos;
using Lone.Domain.Enderecos;

namespace Lone.Tests.Cliente;

/// <summary>Cadastro de finalidades de endereço para os testes (as iniciais, como a API devolve).</summary>
public static class Finalidades
{
    public static List<FinalidadeEnderecoDto> Cadastro =>
        FinalidadesEnderecoIniciais.Todas
            .Select(f => new FinalidadeEnderecoDto { Id = f.Id, Codigo = f.Codigo, Nome = f.Nome, Ordem = f.Ordem, Ativo = true })
            .ToList();

    public static Guid Id(string codigo) => FinalidadesEnderecoIniciais.Id(codigo);

    public static Guid Entrega => Id(FinalidadesEnderecoIniciais.Entrega);
    public static Guid Cobranca => Id(FinalidadesEnderecoIniciais.Cobranca);
    public static Guid Residencial => Id(FinalidadesEnderecoIniciais.Residencial);
    public static Guid Fiscal => Id(FinalidadesEnderecoIniciais.Fiscal);
}
