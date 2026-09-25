using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.CamposPersonalizados;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Como a tela desenha o campo (cada tipo cai num destes).</summary>
public enum VisualCampo
{
    Texto,
    TextoLongo,
    Escolha
}

/// <summary>Item de uma lista de escolha de campo personalizado ("—", "Sim", "Não" ou as opções da lista).</summary>
public sealed record OpcaoEscolha(Guid? OpcaoId, bool? Logico, string Texto)
{
    public static readonly OpcaoEscolha Nenhuma = new(null, null, "—");
    public override string ToString() => Texto;
}

/// <summary>
/// Um campo personalizado na ficha (aba "Informações adicionais"): mostra o controle certo para o tipo,
/// confere o que dá para conferir no aparelho e converte de/para o DTO. As regras finais são da API.
/// </summary>
public sealed partial class CampoPersonalizadoFormulario : ObservableObject
{
    private CampoPersonalizadoFormulario(CampoPersonalizadoDto definicao, IReadOnlyList<OpcaoEscolha> opcoes)
    {
        Definicao = definicao;
        Opcoes = opcoes;
        _escolha = opcoes.Count > 0 ? opcoes[0] : OpcaoEscolha.Nenhuma;
    }

    public CampoPersonalizadoDto Definicao { get; }
    public Guid CampoId => Definicao.Id;
    public TipoCampoPersonalizado Tipo => Definicao.Tipo;

    public string Rotulo => Definicao.Obrigatorio ? Definicao.Nome + " *" : Definicao.Nome;
    public string Dica => Definicao.Dica ?? Tipo switch
    {
        TipoCampoPersonalizado.Data => "dd/mm/aaaa",
        TipoCampoPersonalizado.Hora => "hh:mm",
        TipoCampoPersonalizado.DataHora => "dd/mm/aaaa hh:mm",
        TipoCampoPersonalizado.Moeda => "0,00",
        _ => string.Empty
    };

    public VisualCampo Visual => Tipo switch
    {
        TipoCampoPersonalizado.TextoLongo => VisualCampo.TextoLongo,
        TipoCampoPersonalizado.SimNao or TipoCampoPersonalizado.Lista => VisualCampo.Escolha,
        _ => VisualCampo.Texto
    };

    public TipoMascara Mascara => Tipo switch
    {
        TipoCampoPersonalizado.Data => TipoMascara.Data,
        TipoCampoPersonalizado.Hora => TipoMascara.Hora,
        TipoCampoPersonalizado.DataHora => TipoMascara.DataHora,
        TipoCampoPersonalizado.Telefone => TipoMascara.Telefone,
        TipoCampoPersonalizado.Inteiro or TipoCampoPersonalizado.Decimal or TipoCampoPersonalizado.Moeda => TipoMascara.Numero,
        TipoCampoPersonalizado.Email => TipoMascara.Email,
        TipoCampoPersonalizado.Cpf => TipoMascara.Cpf,
        TipoCampoPersonalizado.Cnpj => TipoMascara.Cnpj,
        _ => TipoMascara.Nenhuma
    };

    /// <summary>Onde o campo aparece, para as mensagens (ex.: "informações adicionais", "documento CNH").</summary>
    public string Onde { get; set; } = "informações adicionais";

    /// <summary>Texto digitado (tipos de texto, número, data e hora).</summary>
    [ObservableProperty] private string _texto = string.Empty;

    /// <summary>Escolha (Sim/Não e lista de opções).</summary>
    [ObservableProperty] private OpcaoEscolha _escolha;

    /// <summary>Itens da lista de escolha (array: o Picker precisa de IList).</summary>
    public IReadOnlyList<OpcaoEscolha> Opcoes { get; }

    public static CampoPersonalizadoFormulario Criar(CampoPersonalizadoDto campo, ValorPersonalizadoDto? valor)
    {
        var opcoes = MontarOpcoes(campo, valor);
        var f = new CampoPersonalizadoFormulario(campo, opcoes);
        if (valor is null) return f;

        switch (campo.Tipo)
        {
            case TipoCampoPersonalizado.SimNao:
                f.Escolha = opcoes.FirstOrDefault(o => o.Logico == valor.Logico) ?? OpcaoEscolha.Nenhuma;
                break;
            case TipoCampoPersonalizado.Lista:
                f.Escolha = opcoes.FirstOrDefault(o => o.OpcaoId is not null && o.OpcaoId == valor.OpcaoId) ?? OpcaoEscolha.Nenhuma;
                break;
            case TipoCampoPersonalizado.Inteiro or TipoCampoPersonalizado.Decimal:
                f.Texto = TextoTela.Numero(valor.Numero);
                break;
            case TipoCampoPersonalizado.Moeda:
                f.Texto = valor.Numero?.ToString("#,0.00", TextoTela.Brasil) ?? string.Empty;
                break;
            case TipoCampoPersonalizado.Data:
                f.Texto = valor.Data is { } d ? TextoTela.Data(DateOnly.FromDateTime(d)) : string.Empty;
                break;
            case TipoCampoPersonalizado.DataHora:
                f.Texto = TextoTela.DataHora(valor.Data);
                break;
            case TipoCampoPersonalizado.Cpf or TipoCampoPersonalizado.Cnpj:
                f.Texto = Lone.Domain.Validacao.Documento.Formatar(valor.Texto);
                break;
            default:
                f.Texto = valor.Texto ?? string.Empty;
                break;
        }
        return f;
    }

    /// <summary>Opções ativas; uma desativada só aparece se for a gravada (marcada como desativada).</summary>
    private static OpcaoEscolha[] MontarOpcoes(CampoPersonalizadoDto campo, ValorPersonalizadoDto? valor) => campo.Tipo switch
    {
        TipoCampoPersonalizado.SimNao => [OpcaoEscolha.Nenhuma, new(null, true, "Sim"), new(null, false, "Não")],
        TipoCampoPersonalizado.Lista =>
        [
            OpcaoEscolha.Nenhuma,
            .. campo.Opcoes
                .Where(o => o.Ativa || o.Id == valor?.OpcaoId)
                .OrderBy(o => o.Ordem)
                .Select(o => new OpcaoEscolha(o.Id, null, o.Ativa ? o.Texto : o.Texto + " (desativada)"))
        ],
        _ => []
    };

    /// <summary>Problema que dá para ver no aparelho (formato e obrigatório). Nulo = ok.</summary>
    public string? Validar()
    {
        var vazio = Visual == VisualCampo.Escolha ? Escolha == OpcaoEscolha.Nenhuma || Escolha is null : string.IsNullOrWhiteSpace(Texto);
        if (vazio)
            return Definicao.Obrigatorio ? $"Informe \"{Definicao.Nome}\" ({Onde})." : null;

        return Tipo switch
        {
            TipoCampoPersonalizado.Inteiro when !TextoTela.TentarInteiro(Texto, out _) => $"{Definicao.Nome}: use um número inteiro.",
            TipoCampoPersonalizado.Decimal or TipoCampoPersonalizado.Moeda when !TextoTela.TentarDecimal(Texto, out _) => $"{Definicao.Nome}: número inválido.",
            TipoCampoPersonalizado.Data when !TextoTela.TentarData(Texto, out _) => $"{Definicao.Nome}: data inválida (use dd/mm/aaaa).",
            TipoCampoPersonalizado.DataHora when !TextoTela.TentarDataHora(Texto, out _) => $"{Definicao.Nome}: data e hora inválidas (use dd/mm/aaaa hh:mm).",
            TipoCampoPersonalizado.Hora when !TimeOnly.TryParseExact(Texto.Trim(), ["H:mm", "HH:mm"], TextoTela.Brasil, System.Globalization.DateTimeStyles.None, out _)
                => $"{Definicao.Nome}: hora inválida (use hh:mm).",
            TipoCampoPersonalizado.Cpf when !Lone.Domain.Validacao.Documento.CpfValido(Texto) => $"{Definicao.Nome}: CPF inválido.",
            TipoCampoPersonalizado.Cnpj when !Lone.Domain.Validacao.Documento.CnpjValido(Texto) => $"{Definicao.Nome}: CNPJ inválido.",
            _ => null
        };
    }

    /// <summary>Nulo = campo vazio (não vai na lista).</summary>
    public ValorPersonalizadoDto? ParaDto()
    {
        var valor = new ValorPersonalizadoDto { CampoId = CampoId };
        switch (Tipo)
        {
            case TipoCampoPersonalizado.SimNao:
                valor.Logico = Escolha?.Logico;
                break;
            case TipoCampoPersonalizado.Lista:
                valor.OpcaoId = Escolha?.OpcaoId;
                break;
            case TipoCampoPersonalizado.Inteiro:
                TextoTela.TentarInteiro(Texto, out var inteiro);
                valor.Numero = inteiro;
                break;
            case TipoCampoPersonalizado.Decimal or TipoCampoPersonalizado.Moeda:
                TextoTela.TentarDecimal(Texto, out var numero);
                valor.Numero = numero;
                break;
            case TipoCampoPersonalizado.Data:
                TextoTela.TentarData(Texto, out var data);
                valor.Data = data?.ToDateTime(TimeOnly.MinValue);
                break;
            case TipoCampoPersonalizado.DataHora:
                TextoTela.TentarDataHora(Texto, out var dataHora);
                valor.Data = dataHora;
                break;
            default:
                valor.Texto = TextoTela.Nulo(Texto)?.Trim();
                break;
        }

        return valor.Texto is null && valor.Numero is null && valor.Data is null && valor.Logico is null && valor.OpcaoId is null
            ? null
            : valor;
    }
}
