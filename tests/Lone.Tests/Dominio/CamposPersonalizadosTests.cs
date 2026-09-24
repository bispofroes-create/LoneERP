using Lone.Domain.CamposPersonalizados;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

namespace Lone.Tests.Dominio;

public class CamposPersonalizadosTests
{
    private static CampoPersonalizado Campo(TipoCampoPersonalizado tipo, string nome = "Campo", bool obrigatorio = false, bool ativo = true) =>
        new() { Id = Guid.NewGuid(), Nome = nome, Tipo = tipo, Obrigatorio = obrigatorio, Ativo = ativo };

    private static PessoaValorPersonalizado Valor(CampoPersonalizado campo) => new() { Id = Guid.NewGuid(), CampoId = campo.Id };

    [Fact]
    public void Todos_os_tipos_pedidos_existem_e_cada_um_diz_onde_guarda()
    {
        Assert.Equal(12, TiposCampo.Todos.Count);
        Assert.Equal(ColunaValor.Numero, TiposCampo.Obter(TipoCampoPersonalizado.Inteiro).Coluna);
        Assert.Equal(ColunaValor.Logico, TiposCampo.Obter(TipoCampoPersonalizado.SimNao).Coluna);
        Assert.Equal(ColunaValor.Opcao, TiposCampo.Obter(TipoCampoPersonalizado.Lista).Coluna);
        Assert.Equal(ColunaValor.Data, TiposCampo.Obter(TipoCampoPersonalizado.DataHora).Coluna);
        Assert.Equal(ColunaValor.Texto, TiposCampo.Obter(TipoCampoPersonalizado.Email).Coluna);
    }

    [Fact]
    public void Inteiro_recusa_fracao_e_respeita_limites()
    {
        var filhos = Campo(TipoCampoPersonalizado.Inteiro, "Quantidade de filhos");
        filhos.Minimo = 0;
        filhos.Maximo = 30;

        var fracao = Valor(filhos); fracao.ValorNumero = 2.5m;
        var negativo = Valor(filhos); negativo.ValorNumero = -1;
        var bom = Valor(filhos); bom.ValorNumero = 2;

        Assert.Equal("use um número inteiro", filhos.Definicao.Normalizar(filhos, fracao));
        Assert.Equal("o mínimo é 0", filhos.Definicao.Normalizar(filhos, negativo));
        Assert.Null(filhos.Definicao.Normalizar(filhos, bom));
    }

    [Fact]
    public void Moeda_arredonda_em_2_casas_email_e_telefone_ficam_no_formato_gravado()
    {
        var preco = Campo(TipoCampoPersonalizado.Moeda);
        var v = Valor(preco); v.ValorNumero = 10.005m;
        Assert.Null(preco.Definicao.Normalizar(preco, v));
        Assert.Equal(10.01m, v.ValorNumero);

        var email = Campo(TipoCampoPersonalizado.Email);
        var e = Valor(email); e.ValorTexto = "  Ana@Exemplo.COM ";
        Assert.Null(email.Definicao.Normalizar(email, e));
        Assert.Equal("ana@exemplo.com", e.ValorTexto);

        var hora = Campo(TipoCampoPersonalizado.Hora);
        var h = Valor(hora); h.ValorTexto = "7:05";
        Assert.Null(hora.Definicao.Normalizar(hora, h));
        Assert.Equal("07:05", h.ValorTexto);
    }

    [Fact]
    public void Validador_exige_obrigatorio_limpa_colunas_de_outro_tipo_e_descarta_vazio()
    {
        var time = Campo(TipoCampoPersonalizado.Texto, "Time que torce", obrigatorio: true);
        var bebe = Campo(TipoCampoPersonalizado.SimNao, "Bebe?");
        var valorBebe = Valor(bebe);
        valorBebe.ValorLogico = true;
        valorBebe.ValorTexto = "lixo de outro tipo";
        var valorTime = Valor(time);
        valorTime.ValorTexto = "   ";
        var valores = new List<PessoaValorPersonalizado> { valorBebe, valorTime };

        var erros = ValidadorValoresPersonalizados.Aplicar(valores, [time, bebe], []);

        Assert.Equal("Informe \"Time que torce\" (informações adicionais).", Assert.Single(erros));
        var ficou = Assert.Single(valores);
        Assert.True(ficou.ValorLogico);
        Assert.Null(ficou.ValorTexto);
    }

    [Fact]
    public void Campo_desativado_mantem_o_valor_gravado_mesmo_que_o_aparelho_mande_outro()
    {
        var antigo = Campo(TipoCampoPersonalizado.Texto, "Antigo", obrigatorio: true, ativo: false);
        var gravado = Valor(antigo); gravado.ValorTexto = "Valor de antes";
        var enviado = Valor(antigo); enviado.ValorTexto = "Tentativa de mudar";
        var valores = new List<PessoaValorPersonalizado> { enviado };

        var erros = ValidadorValoresPersonalizados.Aplicar(valores, [antigo], [gravado]);

        Assert.Empty(erros); // obrigatório só vale para campo ativo
        Assert.Equal("Valor de antes", Assert.Single(valores).ValorTexto);
    }

    [Fact]
    public void Opcao_desativada_so_vale_se_ja_era_a_gravada()
    {
        var estadoCivil = Campo(TipoCampoPersonalizado.Lista, "Estado civil");
        var casado = new CampoPersonalizadoOpcao { Id = Guid.NewGuid(), CampoId = estadoCivil.Id, Texto = "Casado", Ativa = true };
        var viuvo = new CampoPersonalizadoOpcao { Id = Guid.NewGuid(), CampoId = estadoCivil.Id, Texto = "Viúvo", Ativa = false };
        estadoCivil.Opcoes = [casado, viuvo];

        var nova = Valor(estadoCivil); nova.OpcaoId = viuvo.Id;
        var erros = ValidadorValoresPersonalizados.Aplicar([nova], [estadoCivil], []);
        Assert.Contains("desativada", Assert.Single(erros));

        var gravada = Valor(estadoCivil); gravada.OpcaoId = viuvo.Id;
        var mantida = Valor(estadoCivil); mantida.OpcaoId = viuvo.Id;
        Assert.Empty(ValidadorValoresPersonalizados.Aplicar([mantida], [estadoCivil], [gravada]));

        var deOutroCampo = Valor(estadoCivil); deOutroCampo.OpcaoId = Guid.NewGuid();
        Assert.Contains("opção inexistente", Assert.Single(ValidadorValoresPersonalizados.Aplicar([deOutroCampo], [estadoCivil], [])));
    }

    [Fact]
    public void Definicao_de_lista_precisa_de_opcao_ativa_e_sem_repeticao()
    {
        var campo = Campo(TipoCampoPersonalizado.Lista, " Estado civil ");
        campo.Opcoes =
        [
            new CampoPersonalizadoOpcao { Id = Guid.NewGuid(), Texto = "Casado" },
            new CampoPersonalizadoOpcao { Id = Guid.NewGuid(), Texto = "CASADO " }
        ];

        RegrasCampoPersonalizado.Normalizar(campo);
        var erros = RegrasCampoPersonalizado.Validar(campo);

        Assert.Equal("Estado civil", campo.Nome);
        Assert.Contains("A opção \"Casado\" está repetida.", erros);

        campo.Opcoes.ForEach(o => o.Ativa = false);
        Assert.Contains("Uma lista de opções precisa de ao menos uma opção ativa.", RegrasCampoPersonalizado.Validar(campo));
    }

    [Fact]
    public void Historico_mostra_valor_legivel_de_qualquer_tipo()
    {
        Assert.Equal("Sim", ValorCampo.Descrever(null, null, null, true, null));
        Assert.Equal("1.234,5", ValorCampo.Descrever(null, 1234.5m, null, null, null));
        Assert.Equal("05/01/2026", ValorCampo.Descrever(null, null, new DateTime(2026, 1, 5), null, null));
        Assert.Equal("05/01/2026 14:30", ValorCampo.Descrever(null, null, new DateTime(2026, 1, 5, 14, 30, 0), null, null));
        var opcao = Guid.NewGuid();
        Assert.Equal(ValorCampo.PrefixoOpcao + opcao, ValorCampo.Descrever(null, null, null, null, opcao));
    }
}
