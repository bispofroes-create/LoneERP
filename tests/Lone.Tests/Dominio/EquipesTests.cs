using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Metas;

namespace Lone.Tests.Dominio;

/// <summary>
/// Equipes na Fase 2a (decisões F3 e hierarquia): liderança como papel do membro, com vigência e um líder por vez;
/// histórico que não é reescrito; equipe acima sem ciclo; quem lidera o quê numa data.
/// </summary>
public class EquipesTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 28);
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Bruno = Guid.NewGuid();
    private static readonly Guid Carla = Guid.NewGuid();

    private static MembroEquipe Membro(Guid pessoa, DateOnly inicio, DateOnly? fim = null, PapelNaEquipe papel = PapelNaEquipe.Membro) =>
        new() { Id = Guid.NewGuid(), PessoaId = pessoa, InicioEm = inicio, FimEm = fim, Papel = papel };

    private static Equipe Equipe(string nome, Guid? pai = null, params MembroEquipe[] membros) =>
        new() { Id = Guid.NewGuid(), Nome = nome, EquipePaiId = pai, Membros = membros.ToList() };

    private static List<string> Validar(Equipe dados, IReadOnlyCollection<Equipe>? todas = null, Equipe? anterior = null) =>
        RegrasEquipe.Validar(dados, todas ?? [], anterior, Hoje);

    [Fact]
    public void Equipe_valida_com_um_lider_e_membros_passa()
    {
        var e = Equipe("Televendas Sul", null,
            Membro(Ana, new(2026, 1, 1), papel: PapelNaEquipe.Lider),
            Membro(Bruno, new(2026, 1, 1)));
        Assert.Empty(Validar(e));
    }

    [Fact]
    public void Dois_lideres_ao_mesmo_tempo_nao_passa_mas_em_sequencia_passa()
    {
        var junto = Equipe("Sul", null,
            Membro(Ana, new(2026, 1, 1), papel: PapelNaEquipe.Lider),
            Membro(Bruno, new(2026, 6, 1), papel: PapelNaEquipe.Lider));
        Assert.Contains(Validar(junto), e => e.Contains("dois líderes") && e.Contains("01/06/2026"));

        var emSequencia = Equipe("Sul", null,
            Membro(Ana, new(2026, 1, 1), new(2026, 5, 31), PapelNaEquipe.Lider),
            Membro(Bruno, new(2026, 6, 1), papel: PapelNaEquipe.Lider));
        Assert.Empty(Validar(emSequencia));
    }

    [Fact]
    public void Mesma_pessoa_em_periodos_que_se_cruzam_nao_passa()
    {
        var e = Equipe("Sul", null, Membro(Ana, new(2026, 1, 1), new(2026, 6, 30)), Membro(Ana, new(2026, 6, 1)));
        Assert.Contains(Validar(e), x => x.Contains("mesma pessoa"));
    }

    [Fact]
    public void Nome_repetido_em_outra_equipe_nao_passa()
    {
        var outra = Equipe("Televendas Sul");
        Assert.Contains(Validar(Equipe("televendas sul"), [outra]), e => e.Contains("Já existe"));
    }

    [Fact]
    public void Membro_que_ja_entrou_nao_muda_papel_nem_pessoa_mas_pode_receber_a_saida()
    {
        var gravado = Membro(Ana, new(2026, 1, 1));
        var anterior = Equipe("Sul", null, gravado);

        var promovido = Equipe("Sul", null, Membro(Ana, gravado.InicioEm, papel: PapelNaEquipe.Lider));
        promovido.Id = anterior.Id;
        promovido.Membros[0].Id = gravado.Id;
        Assert.Contains(Validar(promovido, anterior: anterior), e => e.Contains("histórico não é reescrito"));

        var comSaida = Equipe("Sul", null, Membro(Ana, gravado.InicioEm, new(2026, 9, 30)));
        comSaida.Id = anterior.Id;
        comSaida.Membros[0].Id = gravado.Id;
        Assert.Empty(Validar(comSaida, anterior: anterior));
    }

    [Fact]
    public void Membro_planejado_para_o_futuro_pode_mudar_de_papel()
    {
        var gravado = Membro(Ana, Hoje.AddDays(10));
        var anterior = Equipe("Sul", null, gravado);
        var dados = Equipe("Sul", null, Membro(Ana, gravado.InicioEm, papel: PapelNaEquipe.Lider));
        dados.Id = anterior.Id;
        dados.Membros[0].Id = gravado.Id;
        Assert.Empty(Validar(dados, anterior: anterior));
    }

    [Fact]
    public void Equipe_acima_nao_pode_ser_a_propria_nem_uma_de_baixo()
    {
        var regional = Equipe("Regional");
        var sul = Equipe("Sul", regional.Id);
        var litoral = Equipe("Litoral", sul.Id);
        var todas = new[] { regional, sul, litoral };

        var propria = Equipe("Regional", regional.Id);
        propria.Id = regional.Id;
        Assert.Contains(Validar(propria, todas), e => e.Contains("acima dela mesma"));

        var ciclo = Equipe("Regional", litoral.Id);
        ciclo.Id = regional.Id;
        Assert.Contains(Validar(ciclo, todas, regional), e => e.Contains("ciclo"));

        Assert.Empty(Validar(Equipe("Nova", litoral.Id), todas));
    }

    [Fact]
    public void Equipe_acima_desativada_so_vale_se_ja_era_a_gravada()
    {
        var velha = Equipe("Velha");
        velha.Ativo = false;
        var dados = Equipe("Sul", velha.Id);
        Assert.Contains(Validar(dados, [velha]), e => e.Contains("desativada"));

        var anterior = Equipe("Sul", velha.Id);
        anterior.Id = dados.Id;
        Assert.Empty(Validar(dados, [velha, anterior], anterior));
    }

    [Fact]
    public void Equipe_acima_que_nao_existe_nao_passa()
    {
        Assert.Contains(Validar(Equipe("Sul", Guid.NewGuid())), e => e.Contains("não existe mais"));
    }

    [Fact]
    public void Descendentes_trazem_filhas_e_netas_e_resistem_a_ciclo_gravado()
    {
        var a = Equipe("A");
        var b = Equipe("B", a.Id);
        var c = Equipe("C", b.Id);
        var d = Equipe("D");
        Assert.Equal(new HashSet<Guid> { b.Id, c.Id }, RegrasEquipe.Descendentes([a, b, c, d], a.Id));

        a.EquipePaiId = c.Id; // ciclo vindo de fora: não trava
        Assert.Equal(new HashSet<Guid> { b.Id, c.Id }, RegrasEquipe.Descendentes([a, b, c], a.Id));
    }

    [Fact]
    public void Lider_em_cada_data_segue_a_vigencia_e_lideradas_ignoram_equipe_desativada()
    {
        var sul = Equipe("Sul", null,
            Membro(Ana, new(2026, 1, 1), new(2026, 5, 31), PapelNaEquipe.Lider),
            Membro(Bruno, new(2026, 6, 1), papel: PapelNaEquipe.Lider),
            Membro(Carla, new(2026, 1, 1)));
        Assert.Equal(Ana, RegrasEquipe.LiderEm(sul, new(2026, 3, 1)));
        Assert.Equal(Bruno, RegrasEquipe.LiderEm(sul, Hoje));
        Assert.Null(RegrasEquipe.LiderEm(sul, new(2025, 12, 31)));

        var norte = Equipe("Norte", null, Membro(Bruno, new(2026, 1, 1), papel: PapelNaEquipe.Lider));
        norte.Ativo = false;
        Assert.Equal(new[] { sul.Id }, RegrasEquipe.LideradasPor([sul, norte], Bruno, Hoje));
        Assert.Empty(RegrasEquipe.LideradasPor([sul, norte], Carla, Hoje));
    }
}
