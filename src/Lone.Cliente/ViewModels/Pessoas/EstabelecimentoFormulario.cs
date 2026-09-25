using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Um estabelecimento (CNPJ) da pessoa jurídica. Na pessoa física e no estrangeiro existe um só, que guarda
/// os dados fiscais (inscrições, indicador de IE, regime).
/// </summary>
public sealed partial class EstabelecimentoFormulario : ItemDeLista
{
    public EstabelecimentoFormulario(ObservableCollection<EnderecoFormulario> enderecosDaPessoa)
        : this(IdSequencial.Novo(), enderecosDaPessoa) { }

    private EstabelecimentoFormulario(Guid id, ObservableCollection<EnderecoFormulario> enderecosDaPessoa)
    {
        Id = id;
        EnderecosDisponiveis = enderecosDaPessoa;
    }

    public Guid Id { get; }

    /// <summary>Os endereços da própria pessoa, para escolher o endereço fiscal da filial.</summary>
    public ObservableCollection<EnderecoFormulario> EnderecosDisponiveis { get; }

    /// <summary>Definidos pela ficha.</summary>
    public Func<EstabelecimentoFormulario, Task>? AoConsultarCnpj { get; set; }
    public Action? AoTornarPrincipal { get; set; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _cnpj = string.Empty;
    [ObservableProperty] private string _nomeFantasia = string.Empty;
    [ObservableProperty] private bool _ativo = true;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(TextoReceita))] private string _situacaoReceita = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TextoReceita))] private DateTime? _consultadoReceitaEm;

    [ObservableProperty] private Opcao<IndicadorIE> _indicadorIE = OpcoesPessoa.IndicadoresIE[0];
    [ObservableProperty] private string _inscricaoEstadual = string.Empty;
    [ObservableProperty] private string _inscricaoMunicipal = string.Empty;
    [ObservableProperty] private string _inscricaoSuframa = string.Empty;
    [ObservableProperty] private Opcao<RegimeTributario> _regime = OpcoesPessoa.Regimes[0];
    [ObservableProperty] private string _cnaePrincipal = string.Empty;
    [ObservableProperty] private bool _produtorRural;

    /// <summary>Somente leitura: "0111-3/01 · Cultivo de arroz" (tabela CNAE, se carregada no servidor).</summary>
    public string CnaePrincipalDescricao { get; private set; } = string.Empty;

    /// <summary>Somente leitura: situação fiscal por período ("desde 01/03/2026: Simples Nacional · contribuinte · IE 123").</summary>
    public IReadOnlyList<string> HistoricoFiscal { get; private set; } = [];
    public bool TemHistoricoFiscal => HistoricoFiscal.Count > 1;
    [ObservableProperty] private string _naturezaJuridica = string.Empty;

    /// <summary>Códigos separados por vírgula (vêm da consulta de CNPJ; podem ser editados).</summary>
    [ObservableProperty] private string _cnaesSecundarios = string.Empty;

    /// <summary>Nulo = usa o endereço principal da pessoa.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemEnderecoProprio))]
    private EnderecoFormulario? _enderecoFiscal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Titulo), nameof(FilialDaPJ), nameof(MostrarNaFicha))]
    private bool _ehPrincipal;

    /// <summary>Definido pela ficha. Só a pessoa jurídica tem CNPJ, nome fantasia e filiais.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilialDaPJ), nameof(MostrarNaFicha))]
    private bool _daPessoaJuridica;

    /// <summary>Filial de pessoa jurídica: pode virar principal, ser removida e ter endereço próprio.</summary>
    public bool FilialDaPJ => DaPessoaJuridica && !EhPrincipal;

    /// <summary>Na pessoa física ou estrangeiro aparece só o principal (os dados fiscais).</summary>
    public bool MostrarNaFicha => DaPessoaJuridica || EhPrincipal;
    public bool TemEnderecoProprio => EnderecoFiscal is not null;

    public IReadOnlyList<Opcao<IndicadorIE>> IndicadoresIE => OpcoesPessoa.IndicadoresIE;
    public IReadOnlyList<Opcao<RegimeTributario>> Regimes => OpcoesPessoa.Regimes;

    public string Titulo
    {
        get
        {
            var cnpj = Documento.Formatar(Cnpj);
            var tipo = EhPrincipal ? "Estabelecimento principal" : "Filial";
            return cnpj.Length == 0 ? tipo : $"{tipo} · {cnpj}";
        }
    }

    public string TextoReceita => SituacaoReceita.Length == 0
        ? string.Empty
        : $"Situação na Receita Federal: {SituacaoReceita}" +
          (ConsultadoReceitaEm is { } em ? $" (consultado em {em.ToLocalTime().ToString("dd/MM/yyyy HH:mm", TextoTela.Brasil)})" : string.Empty);

    [RelayCommand]
    private Task ConsultarCnpjAsync() => AoConsultarCnpj?.Invoke(this) ?? Task.CompletedTask;

    [RelayCommand]
    private void TornarPrincipal() => AoTornarPrincipal?.Invoke();

    [RelayCommand]
    private void UsarEnderecoPrincipal() => EnderecoFiscal = null;

    public static EstabelecimentoFormulario De(EstabelecimentoDto e, ObservableCollection<EnderecoFormulario> enderecos) =>
        new(e.Id, enderecos)
        {
            Cnpj = Documento.Formatar(e.Cnpj),
            NomeFantasia = e.NomeFantasia ?? string.Empty,
            Ativo = e.Ativo,
            SituacaoReceita = e.SituacaoReceita ?? string.Empty,
            ConsultadoReceitaEm = e.ConsultadoReceitaEm,
            IndicadorIE = Opcao.De(OpcoesPessoa.IndicadoresIE, e.IndicadorIE),
            InscricaoEstadual = e.InscricaoEstadual ?? string.Empty,
            InscricaoMunicipal = e.InscricaoMunicipal ?? string.Empty,
            InscricaoSuframa = e.InscricaoSuframa ?? string.Empty,
            Regime = Opcao.De(OpcoesPessoa.Regimes, e.RegimeTributario),
            CnaePrincipal = e.CnaePrincipal ?? string.Empty,
            ProdutorRural = e.ProdutorRural,
            CnaePrincipalDescricao = e.CnaePrincipalDescricao ?? string.Empty,
            HistoricoFiscal = e.HistoricoFiscal.Select(h =>
                (h.FimEm is { } fim ? $"{TextoTela.Data(h.InicioEm)} a {TextoTela.Data(fim)}" : $"Desde {TextoTela.Data(h.InicioEm)}") + ": " +
                string.Join(" · ", new[]
                {
                    Opcao.De(OpcoesPessoa.Regimes, h.RegimeTributario).Texto,
                    Opcao.De(OpcoesPessoa.IndicadoresIE, h.IndicadorIE).Texto,
                    h.InscricaoEstadual is { } ie ? "IE " + ie : string.Empty,
                    h.SituacaoReceita ?? string.Empty,
                    h.ProdutorRural ? "produtor rural" : string.Empty
                }.Where(t => t.Length > 0))).ToList(),
            NaturezaJuridica = e.NaturezaJuridica ?? string.Empty,
            CnaesSecundarios = e.CnaesSecundarios?.Replace(",", ", ") ?? string.Empty,
            EhPrincipal = e.Principal,
            EnderecoFiscal = e.EnderecoFiscalId is { } id ? enderecos.FirstOrDefault(x => x.Id == id) : null
        };

    public EstabelecimentoDto ParaDto() => new()
    {
        Id = Id,
        Cnpj = TextoTela.Nulo(Cnpj),
        Principal = EhPrincipal,
        NomeFantasia = TextoTela.Nulo(NomeFantasia),
        Ativo = Ativo,
        SituacaoReceita = TextoTela.Nulo(SituacaoReceita),
        ConsultadoReceitaEm = ConsultadoReceitaEm,
        IndicadorIE = IndicadorIE.Valor,
        InscricaoEstadual = TextoTela.Nulo(InscricaoEstadual),
        InscricaoMunicipal = TextoTela.Nulo(InscricaoMunicipal),
        InscricaoSuframa = TextoTela.Nulo(InscricaoSuframa),
        RegimeTributario = Regime.Valor,
        CnaePrincipal = TextoTela.Nulo(CnaePrincipal),
        ProdutorRural = ProdutorRural,
        NaturezaJuridica = TextoTela.Nulo(NaturezaJuridica),
        CnaesSecundarios = TextoTela.Nulo(CnaesSecundarios),
        // Endereço removido da ficha: volta ao principal em vez de apontar para um Id que não será gravado.
        EnderecoFiscalId = EnderecoFiscal is { } e && EnderecosDisponiveis.Contains(e) ? e.Id : null
    };

    /// <summary>Verdadeiro quando a última consulta de CNPJ trouxe a inscrição estadual.</summary>
    public bool InscricaoVeioDaConsulta { get; private set; }

    public void AplicarCnpj(DadosCnpj d)
    {
        Cnpj = Documento.Formatar(d.Cnpj);
        if (d.NomeFantasia is not null) NomeFantasia = d.NomeFantasia;
        SituacaoReceita = d.SituacaoCadastral ?? string.Empty;
        ConsultadoReceitaEm = DateTime.UtcNow;

        if (d.CnaePrincipal is not null) CnaePrincipal = d.CnaePrincipal;
        if (d.NaturezaJuridica is not null) NaturezaJuridica = d.NaturezaJuridica;
        if (d.CnaesSecundarios.Count > 0) CnaesSecundarios = string.Join(", ", d.CnaesSecundarios);

        // MEI também é optante do Simples: o MEI é o mais específico.
        if (d.OpcaoMei == true) Regime = Opcao.De(OpcoesPessoa.Regimes, RegimeTributario.Mei);
        else if (d.OpcaoSimples == true) Regime = Opcao.De(OpcoesPessoa.Regimes, RegimeTributario.SimplesNacional);

        // Inscrição ativa no estado do endereço do CNPJ.
        var inscricao = d.InscricoesEstaduais.FirstOrDefault(i => i.Ativa && string.Equals(i.Uf, d.Uf, StringComparison.OrdinalIgnoreCase));
        InscricaoVeioDaConsulta = inscricao is not null;
        if (inscricao is not null)
        {
            InscricaoEstadual = inscricao.Numero;
            IndicadorIE = Opcao.De(OpcoesPessoa.IndicadoresIE, global::Lone.Domain.Enums.IndicadorIE.Contribuinte); // o nome curto seria a propriedade
        }
    }
}
