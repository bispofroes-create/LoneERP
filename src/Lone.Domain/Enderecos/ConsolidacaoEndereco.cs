using Lone.Domain.Comum;
using Lone.Domain.Entidades;

namespace Lone.Domain.Enderecos;

/// <summary>
/// Consolidação de endereço duplicado, decidida pelo SERVIDOR a partir do estado gravado (o aplicativo só informa a
/// intenção: "consolidar A em B"). Confere tudo, junta as finalidades no endereço mantido (sem repetir; o principal do
/// consolidado passa para o mantido; conflito de principal é recusado, nunca decidido em silêncio), deixa o consolidado
/// inativo com MescladoEmId e redireciona as filiais. Nada é apagado.
/// </summary>
public static class ConsolidacaoEndereco
{
    /// <summary>Aplica na pessoa carregada do banco. Lista vazia = consolidado; senão, nada foi alterado.</summary>
    public static List<string> Aplicar(Pessoa p, Guid origemId, Guid destinoId)
    {
        var erros = new List<string>();
        if (origemId == destinoId)
        {
            erros.Add("Escolha dois endereços diferentes para consolidar.");
            return erros;
        }

        var origem = p.Enderecos.FirstOrDefault(e => e.Id == origemId);
        var destino = p.Enderecos.FirstOrDefault(e => e.Id == destinoId);
        if (origem is null) erros.Add("O endereço a consolidar não existe nesta pessoa.");
        if (destino is null) erros.Add("O endereço a manter não existe nesta pessoa.");
        if (origem is null || destino is null) return erros;

        if (origem.MescladoEmId is not null) erros.Add($"O endereço {DuplicidadeEndereco.Resumo(origem)} já foi consolidado em outro.");
        else if (!origem.Ativo) erros.Add($"O endereço {DuplicidadeEndereco.Resumo(origem)} está inativo: só se consolida endereço ativo.");
        if (!destino.Ativo) erros.Add($"O endereço a manter ({DuplicidadeEndereco.Resumo(destino)}) precisa estar ativo.");
        if (DuplicidadeEndereco.Comparar(origem, destino) == SemelhancaEndereco.Diferente)
            erros.Add($"Os endereços {DuplicidadeEndereco.Resumo(origem)} e {DuplicidadeEndereco.Resumo(destino)} não são o mesmo " +
                      "endereço físico: não podem ser consolidados.");

        var usosOrigem = p.FinalidadesEnderecos.Where(u => u.PessoaEnderecoId == origem.Id && u.Ativo).ToList();
        var usosDestino = p.FinalidadesEnderecos.Where(u => u.PessoaEnderecoId == destino.Id).ToList();

        // Os dois principais da mesma finalidade (estado que o banco não deveria permitir): o usuário decide antes.
        foreach (var conflito in usosOrigem.Where(u => u.Principal &&
                     usosDestino.Any(d => d.Ativo && d.Principal && d.FinalidadeId == u.FinalidadeId)))
            erros.Add("Os dois endereços estão marcados como principal da mesma finalidade: defina o principal antes de consolidar.");
        if (erros.Count > 0) return erros.Distinct().ToList();

        foreach (var uso in usosOrigem)
        {
            var eraPrincipal = uso.Principal;
            uso.Principal = false; // fica no consolidado como histórico, sem principal
            var noDestino = usosDestino.FirstOrDefault(d => d.FinalidadeId == uso.FinalidadeId);
            if (noDestino is null)
            {
                noDestino = new PessoaEnderecoFinalidade
                {
                    Id = IdSequencial.Novo(), PessoaId = p.Id, PessoaEnderecoId = destino.Id, FinalidadeId = uso.FinalidadeId, Ativo = true
                };
                p.FinalidadesEnderecos.Add(noDestino);
                usosDestino.Add(noDestino);
            }
            else if (!noDestino.Ativo)
            {
                noDestino.Ativo = true;      // reaproveita a linha histórica (sem criar outra)
                noDestino.Principal = false;
            }
            if (eraPrincipal) noDestino.Principal = true; // o mesmo lugar físico continua sendo o principal
        }

        origem.Ativo = false;
        origem.MescladoEmId = destino.Id;
        foreach (var estabelecimento in p.Estabelecimentos.Where(e => e.EnderecoFiscalId == origem.Id))
        {
            estabelecimento.EnderecoFiscalId = destino.Id;
            estabelecimento.EnderecoFiscal = null;
        }
        return erros;
    }
}
