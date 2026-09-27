using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>Gravidade de um item do resumo: define a cor e a ordem (alertas primeiro).</summary>
public enum NivelResumo
{
    Alerta,
    Atencao,
    Informacao,
    Ok
}

/// <summary>Uma linha do resumo. Tocar leva à aba onde o assunto é resolvido (quando houver).</summary>
public sealed class ItemResumo
{
    public ItemResumo(string texto, NivelResumo nivel, Action? ir = null)
    {
        Texto = texto;
        Nivel = nivel;
        IrCommand = ir is null ? null : new RelayCommand(ir);
    }

    public string Texto { get; }
    public NivelResumo Nivel { get; }
    public IRelayCommand? IrCommand { get; }
    public bool PodeIr => IrCommand is not null;

    public bool EhAlerta => Nivel == NivelResumo.Alerta;
    public bool EhAtencao => Nivel == NivelResumo.Atencao;
    public bool EhOk => Nivel == NivelResumo.Ok;

    /// <summary>Marcador curto antes do texto (não depende só da cor).</summary>
    public string Marcador => Nivel switch
    {
        NivelResumo.Alerta => "●",
        NivelResumo.Atencao => "▲",
        NivelResumo.Ok => "✓",
        _ => "·"
    };
}

/// <summary>Um bloco do resumo (ex.: "Documentos"), com os itens já na ordem de gravidade.</summary>
public sealed record BlocoResumo(string Titulo, IReadOnlyList<ItemResumo> Itens);

/// <summary>
/// Quem monta um bloco do resumo da pessoa. Cada módulo acrescenta a sua fonte (comercial: limite e títulos;
/// financeiro: vencidos...) sem mudar a tela. Recebe a ficha aberta e o "ir para a aba".
/// </summary>
public interface IFonteResumoPessoa
{
    IEnumerable<BlocoResumo> Montar(PessoaFormulario ficha, Action<SecaoPessoa> irPara);
}

/// <summary>
/// "Resumo da pessoa": o que importa saber sem abrir as abas (situação, bloqueios, relacionamento, documentos vencendo,
/// pendências do cadastro). Em tela larga fica num painel à direita (pode ser recolhido); em tela estreita, num cartão
/// acima das abas, fechado por padrão. Só lê a ficha: não grava nada.
/// </summary>
public sealed partial class ResumoPessoa : ObservableObject
{
    /// <summary>Largura da ficha (em pontos) a partir da qual o resumo vai para o lado.</summary>
    public const double LarguraAoLado = 1280;

    private readonly IReadOnlyList<IFonteResumoPessoa> _fontes;

    public ResumoPessoa(IEnumerable<IFonteResumoPessoa>? fontes = null)
    {
        _fontes = fontes?.ToList() ?? [new FonteSituacaoResumo(), new FonteDocumentosResumo(), new FontePendenciasResumo()];
    }

    public ObservableCollection<BlocoResumo> Blocos { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarPainelLateral), nameof(MostrarCartao), nameof(MostrarCorpoNoCartao), nameof(TextoBotao))]
    private bool _existe;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarPainelLateral), nameof(MostrarCartao), nameof(MostrarCorpoNoCartao), nameof(TextoBotao))]
    private bool _aoLado;

    /// <summary>Tela larga: o usuário recolheu o painel (vale para as próximas fichas nesta sessão).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarPainelLateral), nameof(MostrarCartao), nameof(MostrarCorpoNoCartao), nameof(TextoBotao))]
    private bool _recolhido;

    /// <summary>Tela estreita: o cartão está aberto.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarCorpoNoCartao), nameof(TextoBotao))]
    private bool _expandido;

    [ObservableProperty] private string _textoCabecalho = string.Empty;

    public bool MostrarPainelLateral => Existe && AoLado && !Recolhido;
    public bool MostrarCartao => Existe && !MostrarPainelLateral;
    public bool MostrarCorpoNoCartao => MostrarCartao && !AoLado && Expandido;
    public string TextoBotao => AoLado ? "Mostrar ao lado" : Expandido ? "Ocultar" : "Mostrar";

    public void DefinirLargura(double largura)
    {
        if (largura > 0) AoLado = largura >= LarguraAoLado;
    }

    [RelayCommand]
    private void Alternar()
    {
        if (AoLado) Recolhido = !Recolhido;
        else Expandido = !Expandido;
    }

    [RelayCommand]
    private void Recolher() => Recolhido = true;

    /// <summary>Refaz os blocos a partir da ficha (ao abrir, ao trocar de aba e depois de salvar). Nula = sem ficha.</summary>
    public void Atualizar(PessoaFormulario? ficha, Action<SecaoPessoa> irPara)
    {
        Blocos.Clear();
        Existe = ficha is not null;
        if (ficha is null)
        {
            TextoCabecalho = string.Empty;
            return;
        }

        foreach (var bloco in _fontes.SelectMany(f => f.Montar(ficha, irPara)).Where(b => b.Itens.Count > 0))
            Blocos.Add(bloco with { Itens = [.. bloco.Itens.OrderBy(i => i.Nivel)] });

        var itens = Blocos.SelectMany(b => b.Itens).ToList();
        var alertas = itens.Count(i => i.EhAlerta);
        var atencoes = itens.Count(i => i.EhAtencao);
        TextoCabecalho = (alertas, atencoes) switch
        {
            (0, 0) => "Resumo: nada pendente",
            _ => "Resumo: " + string.Join(" · ", new[]
            {
                alertas == 0 ? string.Empty : alertas == 1 ? "1 alerta" : $"{alertas} alertas",
                atencoes == 0 ? string.Empty : atencoes == 1 ? "1 ponto de atenção" : $"{atencoes} pontos de atenção"
            }.Where(t => t.Length > 0))
        };
    }
}

/// <summary>Situação do cadastro, bloqueios ativos e relacionamento (última interação).</summary>
public sealed class FonteSituacaoResumo : IFonteResumoPessoa
{
    public IEnumerable<BlocoResumo> Montar(PessoaFormulario f, Action<SecaoPessoa> irPara)
    {
        var itens = new List<ItemResumo>();
        if (f.Nova)
            itens.Add(new ItemResumo("Novo cadastro, ainda não salvo", NivelResumo.Informacao));
        else
            itens.Add(f.SituacaoGravada switch
            {
                SituacaoPessoa.Inativo => new ItemResumo("Cadastro inativo", NivelResumo.Alerta, () => irPara(SecaoPessoa.Situacao)),
                SituacaoPessoa.Arquivado => new ItemResumo("Cadastro arquivado", NivelResumo.Alerta, () => irPara(SecaoPessoa.Situacao)),
                SituacaoPessoa.EmAnalise => new ItemResumo("Cadastro em análise", NivelResumo.Atencao, () => irPara(SecaoPessoa.Situacao)),
                _ => new ItemResumo("Cadastro ativo", NivelResumo.Ok)
            });

        foreach (var b in f.Situacoes.Bloqueios.Where(b => b.Ativo))
            itens.Add(new ItemResumo("Bloqueio: " + b.Titulo, NivelResumo.Alerta, () => irPara(SecaoPessoa.Situacao)));

        if (f.Existente)
            itens.Add(f.Situacoes.EstadoRelacionamento switch
            {
                SituacaoRelacionamento.Inativo => new ItemResumo(f.Situacoes.TextoRelacionamento, NivelResumo.Alerta, () => irPara(SecaoPessoa.Relacionamento)),
                SituacaoRelacionamento.EmRisco => new ItemResumo(f.Situacoes.TextoRelacionamento, NivelResumo.Atencao, () => irPara(SecaoPessoa.Relacionamento)),
                null => new ItemResumo("Nenhuma interação registrada", NivelResumo.Informacao, () => irPara(SecaoPessoa.Relacionamento)),
                _ => new ItemResumo(f.Situacoes.TextoRelacionamento, NivelResumo.Informacao, () => irPara(SecaoPessoa.Relacionamento))
            });

        yield return new BlocoResumo("Situação", itens);
    }
}

/// <summary>Documentos ativos vencidos ou vencendo (pela antecedência de cada tipo).</summary>
public sealed class FonteDocumentosResumo : IFonteResumoPessoa
{
    public IEnumerable<BlocoResumo> Montar(PessoaFormulario f, Action<SecaoPessoa> irPara)
    {
        var itens = f.Documentos
            .Where(d => d.Vencido || d.VenceEmBreve)
            .Select(d => new ItemResumo(
                $"{d.Tipo.Texto}{(d.Numero.Length > 0 ? " " + d.Numero : string.Empty)}: {d.AvisoValidade}",
                d.Vencido ? NivelResumo.Alerta : NivelResumo.Atencao,
                () => irPara(SecaoPessoa.Documentos)))
            .ToList();
        yield return new BlocoResumo("Documentos", itens);
    }
}

/// <summary>O que falta ou está errado no cadastro (documento, endereço, contato, fiscal).</summary>
public sealed class FontePendenciasResumo : IFonteResumoPessoa
{
    public IEnumerable<BlocoResumo> Montar(PessoaFormulario f, Action<SecaoPessoa> irPara)
    {
        var itens = new List<ItemResumo>();
        void Pendente(string texto, NivelResumo nivel, SecaoPessoa aba) => itens.Add(new ItemResumo(texto, nivel, () => irPara(aba)));

        if (f.TemAvisoDocumentoEmUso)
            Pendente(f.EhJuridica ? "CNPJ já usado em outro cadastro" : "CPF já usado em outro cadastro", NivelResumo.Alerta, SecaoPessoa.Geral);
        if (f.EhFisica && f.Documento.Trim().Length == 0)
            Pendente("CPF não informado", NivelResumo.Atencao, SecaoPessoa.Geral);
        if (f.EhJuridica && f.Principal.Cnpj.Trim().Length == 0)
            Pendente("CNPJ não informado", NivelResumo.Atencao, SecaoPessoa.Geral);

        var enderecos = f.Enderecos.Where(e => e.Ativo && e.Logradouro.Trim().Length > 0).ToList();
        if (enderecos.Count == 0)
            Pendente("Nenhum endereço", NivelResumo.Atencao, SecaoPessoa.Enderecos);
        else if (enderecos.Any(e => e.TemMunicipioACorrigir))
            Pendente("Endereço com município a corrigir", NivelResumo.Atencao, SecaoPessoa.Enderecos);

        if (!f.MeiosContato.Any(m => m.Ativo && m.Valor.Trim().Length > 0))
            Pendente("Nenhum telefone ou e-mail", NivelResumo.Informacao, SecaoPessoa.Contatos);

        var principal = f.Principal;
        if (principal.IndicadorIE.Valor == IndicadorIE.Contribuinte && principal.InscricaoEstadual.Trim().Length == 0)
            Pendente("Contribuinte do ICMS sem inscrição estadual", NivelResumo.Alerta, SecaoPessoa.Estabelecimentos);
        if (f.EhJuridica && principal.Regime.Valor == RegimeTributario.NaoInformado)
            Pendente("Regime tributário não informado", NivelResumo.Informacao, SecaoPessoa.Estabelecimentos);

        if (itens.Count == 0)
            itens.Add(new ItemResumo("Cadastro completo", NivelResumo.Ok));
        yield return new BlocoResumo("Cadastro", itens);
    }
}
