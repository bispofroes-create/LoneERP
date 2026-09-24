using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Tests.Dominio;

public class SituacaoEEventosTests
{
    private static readonly DateTime Agora = new(2026, 9, 24, 13, 0, 0, DateTimeKind.Utc);

    private static Pessoa Cliente() => new()
    {
        Id = Guid.NewGuid(),
        Nome = "João da Silva",
        Papeis = [new PessoaPapel { Papel = TipoPapel.Cliente, Ativo = true }]
    };

    [Fact]
    public void Desativar_guarda_motivo_e_data_e_registra_o_evento()
    {
        var pessoa = Cliente();

        pessoa.Desativar("  Não compra há 2 anos ", Agora);

        Assert.Equal(SituacaoPessoa.Inativo, pessoa.Situacao);
        Assert.Equal("Não compra há 2 anos", pessoa.SituacaoMotivo);
        Assert.Equal(Agora, pessoa.SituacaoAlteradaEm);
        Assert.Equal("Cliente João da Silva foi desativado. Motivo: Não compra há 2 anos", Assert.Single(pessoa.EventosPendentes));
        Assert.False(pessoa.EmUso);
    }

    [Fact]
    public void Reativar_so_vale_para_inativo_e_nao_ha_desativar_duas_vezes()
    {
        var pessoa = Cliente();
        Assert.Throws<ValidacaoException>(() => pessoa.Reativar(null, Agora));

        pessoa.Desativar(null, Agora);
        Assert.Throws<ValidacaoException>(() => pessoa.Desativar(null, Agora));

        pessoa.Reativar(null, Agora);
        Assert.Equal(SituacaoPessoa.Ativo, pessoa.Situacao);
        Assert.Equal(new[] { "Cliente João da Silva foi desativado.", "Cliente João da Silva foi reativado." }, pessoa.EventosPendentes);
    }

    [Fact]
    public void Arquivado_nao_pode_ser_desativado_e_motivo_tem_limite()
    {
        var arquivada = Cliente();
        arquivada.Situacao = SituacaoPessoa.Arquivado;
        Assert.Throws<ValidacaoException>(() => arquivada.Desativar(null, Agora));

        Assert.Throws<ValidacaoException>(() => Cliente().Desativar(new string('x', 201), Agora));
    }

    [Fact]
    public void Eventos_passam_da_instancia_editada_para_a_gravada_uma_vez_so()
    {
        var editada = Cliente();
        var gravada = new Pessoa { Id = editada.Id };
        editada.RegistrarEvento("Algo aconteceu.");

        gravada.ReceberEventosDe(editada);

        Assert.Empty(editada.EventosPendentes);
        Assert.Equal("Algo aconteceu.", Assert.Single(gravada.RetirarEventos()));
        Assert.Empty(gravada.EventosPendentes);
    }

    [Fact]
    public void Campo_personalizado_registra_desativacao_e_reativacao()
    {
        var campo = new CampoPersonalizado { Nome = "Bebe?" };

        campo.Desativar();
        campo.Desativar(); // de novo: nada muda
        campo.Reativar();

        Assert.Equal(new[] { "Campo personalizado 'Bebe?' desativado.", "Campo personalizado 'Bebe?' reativado." }, campo.EventosPendentes);
        Assert.True(campo.Ativo);
    }
}
