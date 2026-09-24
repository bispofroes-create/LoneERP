using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.CamposPersonalizados;
using Lone.Domain.CamposPersonalizados;
using Lone.Domain.Comum;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da tabela de campos personalizados (Campo · Tipo · Obrigatório · Ativo).</summary>
public sealed class LinhaCampoPersonalizado
{
    public LinhaCampoPersonalizado(CampoPersonalizadoDto campo) => Campo = campo;

    public CampoPersonalizadoDto Campo { get; }
    public Guid Id => Campo.Id;
    public string Nome => Campo.Nome;
    public string Tipo => CampoPersonalizadoEdicao.NomeDoTipo(Campo.Tipo);
    public string Obrigatorio => Campo.Obrigatorio ? "Sim" : "Não";
    public string Ativo => Campo.Ativo ? "Sim" : "Não";
    public bool Inativo => !Campo.Ativo;
    public string Detalhe => $"{Tipo} · {(Campo.Obrigatorio ? "obrigatório" : "opcional")}{(Campo.Ativo ? string.Empty : " · desativado")}";
}

/// <summary>Uma opção de campo do tipo lista. Opção já gravada não é apagada: é desativada.</summary>
public sealed partial class OpcaoEdicao : ItemDeLista
{
    public OpcaoEdicao(Guid id, bool gravada)
    {
        Id = id;
        Gravada = gravada;
    }

    public Guid Id { get; }

    /// <summary>Já existe no banco (pode estar em uso): "Remover" vira "Desativar".</summary>
    public bool Gravada { get; }

    [ObservableProperty] private string _texto = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoBotao))]
    private bool _ativa = true;

    public string TextoBotao => !Gravada ? "Remover" : Ativa ? "Desativar" : "Reativar";
}

/// <summary>
/// Ficha de um campo personalizado (administração). Converte de/para o DTO e confere formatos no aparelho;
/// regras finais (nome único, tipo travado quando há valores) são da API.
/// </summary>
public sealed partial class CampoPersonalizadoEdicao : ObservableObject
{
    public static readonly Opcao<TipoCampoPersonalizado>[] Tipos =
        [.. TiposCampo.Todos.OrderBy(t => t.Tipo).Select(t => new Opcao<TipoCampoPersonalizado>(t.Tipo, t.Nome))];

    private CampoPersonalizadoEdicao(Guid id, bool nova)
    {
        Id = id;
        Nova = nova;
    }

    public Guid Id { get; }
    public bool Nova { get; }
    public byte[]? Versao { get; private set; }
    public int Ordem { get; private set; }
    public bool Ativo { get; private set; } = true;

    /// <summary>Já há valores gravados: o tipo não pode mais mudar.</summary>
    public bool TemValores { get; private set; }
    public bool PodeMudarTipo => !TemValores;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsaOpcoes), nameof(UsaCasas), nameof(UsaLimites))]
    private Opcao<TipoCampoPersonalizado> _tipo = Tipos[0];

    [ObservableProperty] private bool _obrigatorio;
    [ObservableProperty] private string _dica = string.Empty;
    [ObservableProperty] private string _casasDecimais = "2";
    [ObservableProperty] private string _minimo = string.Empty;
    [ObservableProperty] private string _maximo = string.Empty;

    public ObservableCollection<OpcaoEdicao> Opcoes { get; } = new();

    public IReadOnlyList<Opcao<TipoCampoPersonalizado>> ListaTipos => Tipos;
    public bool UsaOpcoes => Tipo.Valor == TipoCampoPersonalizado.Lista;
    public bool UsaCasas => Tipo.Valor == TipoCampoPersonalizado.Decimal;
    public bool UsaLimites => Tipo.Valor is TipoCampoPersonalizado.Inteiro or TipoCampoPersonalizado.Decimal or TipoCampoPersonalizado.Moeda;

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo campo" : Nome;
    public string SituacaoTexto => Nova ? "Novo campo" : Ativo ? "Ativo" : "Desativado (não aparece nos cadastros; os valores gravados continuam guardados)";

    public static string NomeDoTipo(TipoCampoPersonalizado tipo) =>
        TiposCampo.Existe(tipo) ? TiposCampo.Obter(tipo).Nome : tipo.ToString();

    public static CampoPersonalizadoEdicao Novo() => new(IdSequencial.Novo(), nova: true);

    public static CampoPersonalizadoEdicao De(CampoPersonalizadoDto c)
    {
        var f = new CampoPersonalizadoEdicao(c.Id, nova: false)
        {
            Versao = c.Versao,
            Ordem = c.Ordem,
            Ativo = c.Ativo,
            TemValores = c.TemValores,
            Nome = c.Nome,
            Tipo = Opcao.De(Tipos, c.Tipo),
            Obrigatorio = c.Obrigatorio,
            Dica = c.Dica ?? string.Empty,
            CasasDecimais = (c.CasasDecimais ?? 2).ToString(TextoTela.Brasil),
            Minimo = TextoTela.Numero(c.Minimo),
            Maximo = TextoTela.Numero(c.Maximo)
        };
        foreach (var o in c.Opcoes.OrderBy(o => o.Ordem))
            f.Incluir(new OpcaoEdicao(o.Id, gravada: true) { Texto = o.Texto, Ativa = o.Ativa });
        return f;
    }

    [RelayCommand]
    private void AdicionarOpcao() => Incluir(new OpcaoEdicao(IdSequencial.Novo(), gravada: false));

    private void Incluir(OpcaoEdicao opcao)
    {
        opcao.AoRemover = () =>
        {
            if (opcao.Gravada) opcao.Ativa = !opcao.Ativa; // gravada: só desativa (valores antigos continuam legíveis)
            else Opcoes.Remove(opcao);
        };
        Opcoes.Add(opcao);
    }

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome do campo.");
        if (UsaCasas && !(byte.TryParse(CasasDecimais, out var casas) && casas <= TiposCampo.MaximoCasasDecimais))
            erros.Add($"Casas decimais: use um número de 0 a {TiposCampo.MaximoCasasDecimais}.");
        if (UsaLimites && (!TextoTela.TentarDecimal(Minimo, out _) || !TextoTela.TentarDecimal(Maximo, out _)))
            erros.Add("Valor mínimo ou máximo inválido.");
        if (UsaOpcoes && !Opcoes.Any(o => o.Ativa && !string.IsNullOrWhiteSpace(o.Texto)))
            erros.Add("Inclua ao menos uma opção.");
        return erros;
    }

    public CampoPersonalizadoDto ParaDto()
    {
        TextoTela.TentarDecimal(Minimo, out var minimo);
        TextoTela.TentarDecimal(Maximo, out var maximo);
        return new CampoPersonalizadoDto
        {
            Id = Id,
            Versao = Versao,
            Entidade = EntidadePersonalizavel.Pessoa,
            Nome = Nome.Trim(),
            Tipo = Tipo.Valor,
            Obrigatorio = Obrigatorio,
            Ativo = Ativo,
            Ordem = Ordem,
            Dica = TextoTela.Nulo(Dica)?.Trim(),
            CasasDecimais = UsaCasas && byte.TryParse(CasasDecimais, out var casas) ? casas : null,
            Minimo = UsaLimites ? minimo : null,
            Maximo = UsaLimites ? maximo : null,
            Opcoes = UsaOpcoes
                ? Opcoes.Select((o, i) => new OpcaoCampoDto { Id = o.Id, Texto = o.Texto.Trim(), Ordem = i, Ativa = o.Ativa }).ToList()
                : Opcoes.Where(o => o.Gravada).Select((o, i) => new OpcaoCampoDto { Id = o.Id, Texto = o.Texto.Trim(), Ordem = i, Ativa = o.Ativa }).ToList()
        };
    }
}
