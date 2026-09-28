using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Comercial;
using Lone.Contracts.Comum;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>Linha da tabela de papéis comerciais (tipos de carteira).</summary>
public sealed class LinhaTipoCarteira
{
    public LinhaTipoCarteira(TipoCarteiraDto item) => Item = item;

    public TipoCarteiraDto Item { get; }
    public Guid Id => Item.Id;
    public string Nome => Item.Nome;
    public string Detalhe => PoliticaPapel.Resumo(Item);
    public string Usos => Item.QuantidadeUsos.ToString("N0", TextoTela.Brasil);
    public string Ativo => Item.Ativo ? "Sim" : "Não";
}

/// <summary>Textos da política de um papel comercial (lista e ficha).</summary>
public static class PoliticaPapel
{
    public static readonly Opcao<TipoCreditoComercial>[] Creditos =
    [
        new(TipoCreditoComercial.Nenhum, "Não recebe crédito da venda"),
        new(TipoCreditoComercial.Receita, "Receita (divide os 100% da venda)"),
        new(TipoCreditoComercial.Sobreposicao, "Sobreposição (crédito extra, fora dos 100%)")
    ];

    /// <summary>"responsável da conta · 1 por vez · receita 70% · metas".</summary>
    public static string Resumo(TipoCarteiraDto t)
    {
        var partes = new List<string>();
        if (t.ResponsavelDaConta) partes.Add("responsável da conta");
        partes.Add(t.LimitePorVez is { } n ? $"{n} por vez" : "sem limite");
        var pct = t.PercentualPadrao is { } p ? $" {TextoTela.Decimal(p)}%" : string.Empty;
        if (t.TipoCredito == TipoCreditoComercial.Receita) partes.Add("receita" + pct);
        else if (t.TipoCredito == TipoCreditoComercial.Sobreposicao) partes.Add("sobreposição" + pct);
        if (t.ContaParaMetas) partes.Add("metas");
        return string.Join(" · ", partes);
    }
}

/// <summary>Ficha de um papel comercial. As regras finais (nome único, um responsável, limite) são da API.</summary>
public sealed partial class TipoCarteiraEdicao : ObservableObject
{
    private TipoCarteiraEdicao(Guid id, bool novo)
    {
        Id = id;
        Novo = novo;
        _credito = PoliticaPapel.Creditos[0];
    }

    public Guid Id { get; }
    public bool Novo { get; }
    public byte[]? Versao { get; private set; }
    public bool Ativo { get; private set; } = true;
    public int QuantidadeUsos { get; private set; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private bool _responsavelDaConta;
    [ObservableProperty] private string _ordem = string.Empty;
    [ObservableProperty] private string _limitePorVez = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(RecebeCredito))] private Opcao<TipoCreditoComercial> _credito;
    [ObservableProperty] private string _percentualPadrao = string.Empty;
    [ObservableProperty] private bool _contaParaMetas;

    public IReadOnlyList<Opcao<TipoCreditoComercial>> Creditos => PoliticaPapel.Creditos;
    public bool RecebeCredito => Credito.Valor != TipoCreditoComercial.Nenhum;

    /// <summary>Responsável da conta é sempre um por vez (é o vendedor padrão do cliente).</summary>
    partial void OnResponsavelDaContaChanged(bool value)
    {
        if (value) LimitePorVez = "1";
    }

    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Novo papel comercial" : Nome;
    public string SituacaoTexto => Novo ? "Novo papel comercial" : Ativo ? "Ativo" : "Desativado (não aparece para novas escolhas; continua onde já está)";
    public string UsoTexto => Novo ? string.Empty : $"{QuantidadeUsos.ToString("N0", TextoTela.Brasil)} vínculo(s) em aberto na carteira com este papel.";

    public static TipoCarteiraEdicao Criar() => new(IdSequencial.Novo(), novo: true);

    public static TipoCarteiraEdicao De(TipoCarteiraDto t) => new(t.Id, novo: false)
    {
        Versao = t.Versao,
        Ativo = t.Ativo,
        QuantidadeUsos = t.QuantidadeUsos,
        ResponsavelDaConta = t.ResponsavelDaConta,
        Ordem = t.Ordem.ToString(System.Globalization.CultureInfo.InvariantCulture),
        LimitePorVez = TextoTela.Inteiro(t.LimitePorVez),
        Credito = Opcao.De(PoliticaPapel.Creditos, t.TipoCredito),
        PercentualPadrao = TextoTela.Decimal(t.PercentualPadrao),
        ContaParaMetas = t.ContaParaMetas,
        Nome = t.Nome
    };

    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(Nome)) erros.Add("Informe o nome.");
        if (!TextoTela.TentarInteiro(Ordem, out _)) erros.Add("Ordem: use um número inteiro.");
        if (!TextoTela.TentarInteiro(LimitePorVez, out _)) erros.Add("Quantos ao mesmo tempo: use um número inteiro (vazio = sem limite).");
        if (!TextoTela.TentarDecimal(PercentualPadrao, out _)) erros.Add("Percentual padrão: número inválido.");
        return erros;
    }

    public TipoCarteiraDto ParaDto()
    {
        TextoTela.TentarInteiro(LimitePorVez, out var limite);
        TextoTela.TentarDecimal(PercentualPadrao, out var percentual);
        return new TipoCarteiraDto
        {
            Id = Id,
            Versao = Versao,
            Nome = Nome.Trim(),
            ResponsavelDaConta = ResponsavelDaConta,
            Ordem = TextoTela.TentarInteiro(Ordem, out var ordem) ? ordem ?? 0 : 0,
            LimitePorVez = limite,
            TipoCredito = Credito.Valor,
            // Papel sem crédito não guarda percentual (a API recusaria).
            PercentualPadrao = RecebeCredito ? percentual : null,
            ContaParaMetas = ContaParaMetas,
            Ativo = Ativo
        };
    }
}

/// <summary>Papéis comerciais (tipos de carteira). Nada é excluído: desativar esconde das escolhas novas.</summary>
public sealed partial class TiposCarteiraViewModel : CadastroViewModelBase<LinhaTipoCarteira>
{
    private readonly ComercialApi _api;

    public TiposCarteiraViewModel(ComercialApi api, IDialogos dialogos) : base(dialogos)
    {
        _api = api;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeDesativar), nameof(PodeReativar))]
    private TipoCarteiraEdicao? _formulario;

    public bool PodeDesativar => Formulario is { Novo: false, Ativo: true };
    public bool PodeReativar => Formulario is { Novo: false, Ativo: false };

    protected override string TextoDeBusca(LinhaTipoCarteira item) => item.Nome + " " + item.Detalhe;

    protected override async Task<IReadOnlyList<LinhaTipoCarteira>> ListarAsync()
    {
        return (await _api.ListarAsync<TipoCarteiraDto>(Rotas.Comercial.TiposCarteira, incluirInativos: true))
            .OrderBy(t => t.Ordem).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => new LinhaTipoCarteira(t)).ToList();
    }

    private TipoCarteiraEdicao Preparar(TipoCarteiraEdicao f)
    {
        
        return f;
    }

    protected override async Task AbrirAsync(LinhaTipoCarteira item) => Formulario = await ObterAsync(item.Id);

    protected override Task NovoItemAsync()
    {
        Formulario = Preparar(TipoCarteiraEdicao.Criar());
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override bool FichaNova => Formulario?.Novo ?? true;

    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Novo: false } formulario) return;
        Formulario = await ObterAsync(formulario.Id);
    }

    private async Task<TipoCarteiraEdicao> ObterAsync(Guid id) =>
        Preparar(TipoCarteiraEdicao.De(await _api.ObterAsync<TipoCarteiraDto>(Rotas.Comercial.TiposCarteira, id) ?? throw new ValidacaoException(["Este cadastro não existe mais."])));

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;
        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        TipoCarteiraDto? salvo = null;
        if (!await ExecutarAsync(async () => salvo = await _api.SalvarAsync(Rotas.Comercial.TiposCarteira, formulario.Id, formulario.ParaDto())))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(TipoCarteiraEdicao.De(salvo!));
        MarcarFichaSemAlteracoes();
        Mostrar(formulario.Novo ? "Cadastro criado." : "Alterações salvas.", TipoMensagem.Sucesso);
    }

    [RelayCommand]
    private Task DesativarAsync() => AlterarSituacaoAsync(desativar: true);

    [RelayCommand]
    private Task ReativarAsync() => AlterarSituacaoAsync(desativar: false);

    private async Task AlterarSituacaoAsync(bool desativar)
    {
        if (Formulario is not { Novo: false } formulario) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes de " + (desativar ? "desativar" : "reativar") + ".", TipoMensagem.Aviso);
            return;
        }

        TipoCarteiraDto? gravado = null;
        if (!await ExecutarAsync(async () => gravado = desativar
                ? await _api.DesativarAsync<TipoCarteiraDto>(Rotas.Comercial.TiposCarteira, formulario.Id, formulario.Versao)
                : await _api.ReativarAsync<TipoCarteiraDto>(Rotas.Comercial.TiposCarteira, formulario.Id, formulario.Versao)))
            return;

        await AtualizarListaAposGravarAsync();
        Formulario = Preparar(TipoCarteiraEdicao.De(gravado!));
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Desativado." : "Reativado.", TipoMensagem.Sucesso);
    }
}
