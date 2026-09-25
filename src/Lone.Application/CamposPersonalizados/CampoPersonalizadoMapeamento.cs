using Lone.Contracts.CamposPersonalizados;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;

namespace Lone.Application.CamposPersonalizados;

public static class CampoPersonalizadoMapeamento
{
    public static CampoPersonalizadoDto ParaDto(CampoPersonalizado c, bool temValores) => new()
    {
        Id = c.Id,
        Versao = c.Versao,
        Entidade = c.Entidade,
        TipoDocumentoId = c.TipoDocumentoId,
        Visivel = c.Visivel,
        Pesquisavel = c.Pesquisavel,
        Nome = c.Nome,
        Tipo = c.Tipo,
        Obrigatorio = c.Obrigatorio,
        Ativo = c.Ativo,
        Ordem = c.Ordem,
        Dica = c.Dica,
        CasasDecimais = c.CasasDecimais,
        Minimo = c.Minimo,
        Maximo = c.Maximo,
        Opcoes = c.Opcoes.OrderBy(o => o.Ordem)
            .Select(o => new OpcaoCampoDto { Id = o.Id, Texto = o.Texto, Ordem = o.Ordem, Ativa = o.Ativa }).ToList(),
        TemValores = temValores
    };

    public static CampoPersonalizado ParaEntidade(CampoPersonalizadoDto d)
    {
        var id = d.Id == Guid.Empty ? IdSequencial.Novo() : d.Id;
        return new CampoPersonalizado
        {
            Id = id,
            Versao = d.Versao,
            Entidade = d.Entidade,
            TipoDocumentoId = d.TipoDocumentoId,
            Visivel = d.Visivel,
            Pesquisavel = d.Pesquisavel,
            Nome = d.Nome ?? string.Empty,
            Tipo = d.Tipo,
            Obrigatorio = d.Obrigatorio,
            Ativo = d.Ativo,
            Ordem = d.Ordem,
            Dica = d.Dica,
            CasasDecimais = d.CasasDecimais,
            Minimo = d.Minimo,
            Maximo = d.Maximo,
            Opcoes = (d.Opcoes ?? []).Select(o => new CampoPersonalizadoOpcao
            {
                Id = o.Id == Guid.Empty ? IdSequencial.Novo() : o.Id,
                CampoId = id,
                Texto = o.Texto ?? string.Empty,
                Ordem = o.Ordem,
                Ativa = o.Ativa
            }).ToList()
        };
    }
}
