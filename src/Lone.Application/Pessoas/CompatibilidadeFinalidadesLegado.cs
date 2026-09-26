using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enderecos;
using Lone.Domain.Enums;

namespace Lone.Application.Pessoas;

/// <summary>
/// Pedido no formato antigo (endereço só com os bits de PessoaEnderecos.Finalidades, sem <see cref="EnderecoDto.Usos"/>).
/// Princípio: o legado NUNCA destrói informação do modelo novo. Com o estado gravado em mãos:
/// - relação gravada cuja finalidade tem bit: bit ligado = continua (ou volta) ativa, com o principal que já tinha;
///   bit desligado = retirada pelo usuário no aplicativo antigo (inativa, e relação inativa não é principal);
/// - relação gravada de finalidade sem bit (criada no cadastro novo): fica exatamente como está;
/// - bit ligado sem relação gravada: finalidade nova, SEM principal (os bits não dizem de qual finalidade seria);
/// - o bit 1 ("Principal" antigo) é ignorado: não vira finalidade nem principal.
/// Endereço no formato novo (com Usos) nunca passa por aqui: vale o que veio.
/// </summary>
public static class CompatibilidadeFinalidadesLegado
{
    /// <summary>Endereço no formato antigo: sem Usos e com algum bit de finalidade (o bit "Principal" sozinho não conta).</summary>
    public static bool EhFormatoAntigo(EnderecoDto e) =>
        e.Usos.Count == 0 && (e.Finalidades & ~FinalidadeEndereco.Principal) != FinalidadeEndereco.Nenhuma;

    /// <summary>
    /// Completa <paramref name="dados"/> (já mapeado do DTO, mesma ordem de endereços) com as relações traduzidas dos
    /// endereços em formato antigo.
    /// </summary>
    public static void Aplicar(Pessoa dados, IReadOnlyList<EnderecoDto> enderecos, Pessoa? anterior)
    {
        var gravadas = anterior?.FinalidadesEnderecos ?? [];
        for (var i = 0; i < enderecos.Count && i < dados.Enderecos.Count; i++)
        {
            if (!EhFormatoAntigo(enderecos[i])) continue;
            var endereco = dados.Enderecos[i];
            var bits = enderecos[i].Finalidades;
            var doEndereco = gravadas.Where(u => u.PessoaEnderecoId == endereco.Id).ToList();

            foreach (var gravada in doEndereco)
            {
                var relacao = new PessoaEnderecoFinalidade
                {
                    Id = gravada.Id, PessoaId = dados.Id, PessoaEnderecoId = endereco.Id, FinalidadeId = gravada.FinalidadeId,
                    Ativo = gravada.Ativo, Principal = gravada.Principal
                };
                var bit = FinalidadesEnderecoIniciais.BitLegado(gravada.FinalidadeId);
                if (bit != FinalidadeEndereco.Nenhuma)
                {
                    var ligado = (bits & bit) != FinalidadeEndereco.Nenhuma;
                    if (ligado && !relacao.Ativo) { relacao.Ativo = true; relacao.Principal = false; } // reativada: sem principal
                    else if (!ligado) { relacao.Ativo = false; relacao.Principal = false; }         // retirada
                }
                dados.FinalidadesEnderecos.Add(relacao);
            }

            foreach (var inicial in FinalidadesEnderecoIniciais.Todas.Where(f =>
                         (bits & f.BitLegado) != FinalidadeEndereco.Nenhuma && doEndereco.All(g => g.FinalidadeId != f.Id)))
                dados.FinalidadesEnderecos.Add(new PessoaEnderecoFinalidade
                {
                    Id = IdSequencial.Novo(), PessoaId = dados.Id, PessoaEnderecoId = endereco.Id, FinalidadeId = inicial.Id,
                    Ativo = true, Principal = false
                });
        }
    }
}
