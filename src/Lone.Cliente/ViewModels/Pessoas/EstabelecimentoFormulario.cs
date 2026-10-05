using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.Fiscal;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Um estabelecimento (CNPJ) da pessoa jurídica. Na pessoa física e no estrangeiro existe um só, que guarda
/// os dados fiscais (inscrições, indicador de IE, regime).
/// </summary>
public sealed partial class EstabelecimentoFormulario : ItemDeLista
{
    public EstabelecimentoFormulario(ObservableCollection<EnderecoFormulario> enderecosDaPessoa)
        : this(IdSequencial.Novo(), enderecosDaPessoa) => _pronto = true;

    /// <summary>Falso enquanto os dados gravados são carregados: carregar não dispara os preenchimentos automáticos.</summary>
    private bool _pronto;

    private EstabelecimentoFormulario(Guid id, ObservableCollection<EnderecoFormulario> enderecosDaPessoa)
    {
        Id = id;
        EnderecosDisponiveis = enderecosDaPessoa;
        NaturezaJuridicaLista.DefinirItens(ItensNaturezaJuridica);
        NaturezaJuridicaLista.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SeletorDeLista.Selecionado) or nameof(SeletorDeLista.Texto)) NaturezaJuridicaDaLista();
        };
    }

    public Guid Id { get; }

    /// <summary>Já está gravado: "remover" desativa (fica no histórico), nunca apaga.</summary>
    public bool Gravado { get; private init; }

    /// <summary>Os endereços da própria pessoa, para escolher o endereço fiscal da filial.</summary>
    public ObservableCollection<EnderecoFormulario> EnderecosDisponiveis { get; }

    /// <summary>Definidos pela ficha.</summary>
    public Func<EstabelecimentoFormulario, Task>? AoConsultarCnpj { get; set; }
    public Action? AoTornarPrincipal { get; set; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _cnpj = string.Empty;
    [ObservableProperty] private string _nomeFantasia = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Inativo), nameof(PodeTornarPrincipal), nameof(PodeDesativar), nameof(PodeReativar))]
    private bool _ativo = true;

    public bool Inativo => !Ativo;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(TextoReceita))] private string _situacaoReceita = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TextoReceita))] private DateTime? _consultadoReceitaEm;

    [ObservableProperty] private Opcao<IndicadorIE> _indicadorIE = OpcoesPessoa.IndicadoresIE[0];
    [ObservableProperty] private string _inscricaoEstadual = string.Empty;
    [ObservableProperty] private string _inscricaoMunicipal = string.Empty;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemDadosDeEmpresa), nameof(DadosDeEmpresa))]
    private string _inscricaoSuframa = string.Empty;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemDadosDeEmpresa), nameof(DadosDeEmpresa))]
    private Opcao<RegimeTributario> _regime = OpcoesPessoa.Regimes[0];

    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemDadosDeEmpresa), nameof(DadosDeEmpresa))]
    private string _cnaePrincipal = string.Empty;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(MostrarDadosIE), nameof(MostrarResumoFiscal))]
    private bool _produtorRural;

    /// <summary>Somente leitura: "0111-3/01 · Cultivo de arroz" (tabela CNAE, se carregada no servidor).</summary>
    public string CnaePrincipalDescricao { get; private set; } = string.Empty;

    /// <summary>
    /// Cadastro gravado de pessoa física com inscrição estadual (ou contribuinte/isento) sem ser produtor rural:
    /// os campos de IE continuam à vista para não esconder um dado gravado.
    /// </summary>
    private bool _inscricaoSemProdutor;

    /// <summary>Pessoa física: a inscrição municipal aparece se já existe ou se o usuário pediu ("Adicionar inscrição municipal").</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(MostrarInscricaoMunicipal), nameof(PodeAdicionarInscricaoMunicipal))]
    private bool _inscricaoMunicipalPedida;

    /// <summary>Somente leitura: situação fiscal por período ("desde 01/03/2026: Simples Nacional · contribuinte · IE 123").</summary>
    public IReadOnlyList<string> HistoricoFiscal { get; private set; } = [];
    public bool TemHistoricoFiscal => HistoricoFiscal.Count > 1;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(NaturezaJuridicaDescricao), nameof(NaturezaJuridicaTexto))]
    private string _naturezaJuridica = string.Empty;

    // ---- Natureza jurídica: escolhida da tabela (busca por código ou nome); grava o código de 4 dígitos ("2054") ----

    private static readonly ItemSeletor[] ItensNaturezaJuridica =
        [.. NaturezasJuridicas.Todas.Select(n => new ItemSeletor(n.Codigo, n.Texto, n.Codigo))];

    /// <summary>Campo com busca da natureza jurídica (Identificação e filial). Código fora da tabela aparece como está.</summary>
    public SeletorDeLista NaturezaJuridicaLista { get; } = new() { Dica = "Digite o código ou parte do nome" };

    private bool _sincronizandoNatureza;

    /// <summary>Escolheu na lista: grava o código. Apagou o texto: sem natureza. Digitando sem escolher: fica o anterior (a ficha avisa).</summary>
    private void NaturezaJuridicaDaLista()
    {
        if (_sincronizandoNatureza) return;
        string? codigo = NaturezaJuridicaLista.Selecionado?.Chave
            ?? (string.IsNullOrWhiteSpace(NaturezaJuridicaLista.Texto) ? string.Empty : null);
        if (codigo is null || codigo == NaturezaJuridica) return;
        _sincronizandoNatureza = true;
        try { NaturezaJuridica = codigo; }
        finally { _sincronizandoNatureza = false; }
    }

    /// <summary>Código vindo do gravado ou da consulta: mostra na lista (o da tabela com a descrição).</summary>
    partial void OnNaturezaJuridicaChanged(string value)
    {
        if (_sincronizandoNatureza) return;
        _sincronizandoNatureza = true;
        try
        {
            NaturezaJuridicaLista.Definir(value.Length == 0
                ? null
                : ItensNaturezaJuridica.FirstOrDefault(i => i.Chave == DocumentoFiscalNatureza(value)) ?? new ItemSeletor(value, value));
        }
        finally { _sincronizandoNatureza = false; }
    }

    /// <summary>"205-4" ou "2054" → "2054" (o que a tabela usa como chave).</summary>
    private static string DocumentoFiscalNatureza(string valor) => new(valor.Where(char.IsAsciiDigit).ToArray());

    /// <summary>"204-6 · Sociedade Anônima Aberta" (vazio sem código).</summary>
    public string NaturezaJuridicaDescricao => NaturezasJuridicas.Descrever(NaturezaJuridica);

    /// <summary>Para o campo só leitura: a descrição, ou o que estiver gravado se não for um código.</summary>
    public string NaturezaJuridicaTexto => NaturezaJuridicaDescricao.Length > 0 ? NaturezaJuridicaDescricao : NaturezaJuridica;

    /// <summary>Códigos separados por vírgula (vêm da consulta de CNPJ; podem ser editados).</summary>
    [ObservableProperty] private string _cnaesSecundarios = string.Empty;

    /// <summary>Nulo = usa o endereço principal da pessoa.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemEnderecoProprio))]
    private EnderecoFormulario? _enderecoFiscal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Titulo), nameof(FilialDaPJ), nameof(PrincipalDaPJ), nameof(MostrarNaFicha),
                              nameof(PodeTornarPrincipal), nameof(PodeDesativar), nameof(PodeReativar))]
    private bool _ehPrincipal;

    /// <summary>Definido pela ficha. Só a pessoa jurídica tem CNPJ, nome fantasia e filiais.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilialDaPJ), nameof(PrincipalDaPJ), nameof(MostrarNaFicha), nameof(PodeTornarPrincipal),
                              nameof(PodeDesativar), nameof(PodeReativar), nameof(DaPessoaFisica), nameof(IndicadoresIE),
                              nameof(MostrarDadosIE), nameof(MostrarResumoFiscal), nameof(MostrarInscricaoMunicipal),
                              nameof(PodeAdicionarInscricaoMunicipal), nameof(TemDadosDeEmpresa), nameof(TituloDadosDeEmpresa))]
    private bool _daPessoaJuridica;

    /// <summary>Definido pela ficha. No exterior: nas notas é sempre não contribuinte, sem inscrição estadual.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DaPessoaFisica), nameof(MostrarDadosIE), nameof(MostrarResumoFiscal), nameof(ResumoFiscal),
                              nameof(MostrarInscricaoMunicipal), nameof(PodeAdicionarInscricaoMunicipal), nameof(TituloDadosDeEmpresa))]
    private bool _daPessoaEstrangeira;

    public bool DaPessoaFisica => !DaPessoaJuridica && !DaPessoaEstrangeira;

    // ---- Aba fiscal por natureza: a PJ vê tudo; a PF só o que usa (produtor rural, IE de produtor, inscrição municipal);
    //      o estrangeiro, só o resumo. ----

    /// <summary>Indicador e inscrição estadual: PJ; PF produtor rural (ou com IE já gravada).</summary>
    public bool MostrarDadosIE => DaPessoaJuridica || (DaPessoaFisica && (ProdutorRural || _inscricaoSemProdutor));

    /// <summary>PF sem dados de IE e estrangeiro: o sistema usa o padrão, e a aba só o descreve.</summary>
    public bool MostrarResumoFiscal => !MostrarDadosIE;

    public string ResumoFiscal => DaPessoaEstrangeira
        ? "Não contribuinte do ICMS (destinatário no exterior) · sem inscrição estadual"
        : "Não contribuinte do ICMS · Consumidor final (padrão nas vendas)";

    public bool MostrarInscricaoMunicipal => DaPessoaJuridica || (DaPessoaFisica && InscricaoMunicipalPedida);
    public bool PodeAdicionarInscricaoMunicipal => DaPessoaFisica && !InscricaoMunicipalPedida;

    /// <summary>
    /// Regime, CNAE e SUFRAMA gravados numa PF ou num estrangeiro (cadastro antigo ou natureza trocada): não se aplicam,
    /// mas aparecem como leitura até o usuário removê-los; nada some sozinho.
    /// </summary>
    public bool TemDadosDeEmpresa => !DaPessoaJuridica &&
        (Regime.Valor != RegimeTributario.NaoInformado || CnaePrincipal.Length > 0 || InscricaoSuframa.Length > 0);

    public string TituloDadosDeEmpresa => DaPessoaEstrangeira
        ? "Dados que não se aplicam a pessoa no exterior"
        : "Dados que não se aplicam a pessoa física";

    public IReadOnlyList<string> DadosDeEmpresa => new[]
    {
        Regime.Valor != RegimeTributario.NaoInformado ? "Regime tributário: " + Regime.Texto : string.Empty,
        CnaePrincipal.Length > 0
            ? "CNAE principal: " + (CnaePrincipalDescricao.Length > 0 ? CnaePrincipalDescricao : CnaePrincipal)
            : string.Empty,
        InscricaoSuframa.Length > 0 ? "Inscrição SUFRAMA: " + InscricaoSuframa : string.Empty
    }.Where(t => t.Length > 0).ToList();

    /// <summary>Filial de pessoa jurídica: pode virar principal, ser removida e ter endereço próprio.</summary>
    public bool FilialDaPJ => DaPessoaJuridica && !EhPrincipal;

    /// <summary>
    /// Principal da pessoa jurídica: CNPJ, nome fantasia e natureza jurídica são editados só na Identificação
    /// (um ponto de edição); no cartão do estabelecimento aparecem como leitura.
    /// </summary>
    public bool PrincipalDaPJ => DaPessoaJuridica && EhPrincipal;

    /// <summary>Só uma filial ativa vira o estabelecimento principal.</summary>
    public bool PodeTornarPrincipal => FilialDaPJ && Ativo;

    /// <summary>
    /// Filial gravada: "Desativar" (continua gravada, com histórico, documentos e referências fiscais) e "Reativar".
    /// Filial nova, ainda não gravada: "Remover" tira da lista.
    /// </summary>
    public bool PodeDesativar => FilialDaPJ && Ativo;
    public bool PodeReativar => FilialDaPJ && !Ativo;
    public string TextoRemover => Gravado ? "Desativar filial" : "Remover";

    /// <summary>Aviso do cartão da filial desativada.</summary>
    public string TextoInativo => "Inativa: não opera mais, mas continua gravada (histórico, documentos e referências fiscais). Pode ser reativada.";

    /// <summary>Na pessoa física ou estrangeiro aparece só o principal (os dados fiscais).</summary>
    public bool MostrarNaFicha => DaPessoaJuridica || EhPrincipal;
    public bool TemEnderecoProprio => EnderecoFiscal is not null;

    /// <summary>Na PF não existe "não informado": a nota precisa do indicador (sem produtor rural, o padrão é "não contribuinte").</summary>
    public IReadOnlyList<Opcao<IndicadorIE>> IndicadoresIE => DaPessoaJuridica
        ? OpcoesPessoa.IndicadoresIE
        : IndicadoresPessoaFisica;

    private static readonly Opcao<IndicadorIE>[] IndicadoresPessoaFisica =
        OpcoesPessoa.IndicadoresIE.Where(o => o.Valor != global::Lone.Domain.Enums.IndicadorIE.NaoInformado).ToArray();
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

    /// <summary>Volta a filial desativada para operação (gravado ao salvar a ficha).</summary>
    [RelayCommand]
    private void Reativar() => Ativo = true;

    [RelayCommand]
    private void UsarEnderecoPrincipal() => EnderecoFiscal = null;

    [RelayCommand]
    private void AdicionarInscricaoMunicipal() => InscricaoMunicipalPedida = true;

    /// <summary>Tira da PF/estrangeiro o regime, o CNAE e a SUFRAMA (vale ao salvar, com auditoria e histórico).</summary>
    [RelayCommand]
    private void RemoverDadosDeEmpresa()
    {
        Regime = OpcoesPessoa.Regimes[0];
        CnaePrincipal = string.Empty;
        InscricaoSuframa = string.Empty;
        CnaePrincipalDescricao = string.Empty;
        OnPropertyChanged(nameof(CnaePrincipalDescricao));
    }

    /// <summary>
    /// PF: marcar produtor rural sugere "contribuinte do ICMS" (IE de produtor); desmarcar volta a "não contribuinte".
    /// A inscrição digitada não é apagada. Não age ao carregar dados gravados.
    /// </summary>
    partial void OnProdutorRuralChanged(bool value)
    {
        if (!_pronto || !DaPessoaFisica) return;
        var indicador = IndicadorIE.Valor;
        if (value && indicador is global::Lone.Domain.Enums.IndicadorIE.NaoInformado or global::Lone.Domain.Enums.IndicadorIE.NaoContribuinte)
            IndicadorIE = Opcao.De(OpcoesPessoa.IndicadoresIE, global::Lone.Domain.Enums.IndicadorIE.Contribuinte);
        else if (!value && !_inscricaoSemProdutor)
            IndicadorIE = Opcao.De(OpcoesPessoa.IndicadoresIE, global::Lone.Domain.Enums.IndicadorIE.NaoContribuinte);
    }

    public static EstabelecimentoFormulario De(EstabelecimentoDto e, ObservableCollection<EnderecoFormulario> enderecos)
    {
        var formulario = new EstabelecimentoFormulario(e.Id, enderecos)
        {
            _inscricaoSemProdutor = !e.ProdutorRural &&
                (e.InscricaoEstadual is not null || e.IndicadorIE is global::Lone.Domain.Enums.IndicadorIE.Contribuinte
                                                                   or global::Lone.Domain.Enums.IndicadorIE.Isento),
            InscricaoMunicipalPedida = e.InscricaoMunicipal is not null,
            Gravado = true,
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
        formulario._pronto = true;
        return formulario;
    }

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

    /// <summary>Preenche pela consulta (cadastro novo ou campo vazio); valor diferente num gravado vai para a conferência.</summary>
    public void AplicarCnpj(DadosCnpj d) => AplicarCnpj(d, new AplicacaoReceita(conferir: false));

    /// <summary>
    /// Preenche o estabelecimento pela consulta, campo a campo por <paramref name="r"/>: vazio é preenchido; com outro valor
    /// num cadastro gravado, fica para o usuário escolher. CNPJ e situação na Receita são da própria consulta (sempre valem).
    /// </summary>
    public void AplicarCnpj(DadosCnpj d, AplicacaoReceita r)
    {
        Cnpj = Documento.Formatar(d.Cnpj);
        SituacaoReceita = d.SituacaoCadastral ?? string.Empty;
        ConsultadoReceitaEm = DateTime.UtcNow;

        // O principal mostra nome fantasia e natureza jurídica na Identificação (sem item); a filial, no cartão dela.
        Guid? doCartao = EhPrincipal ? null : Id;
        r.Valor(new(CamposFichaPessoa.NomeFantasia, doCartao), "Nome fantasia", NomeFantasia, d.NomeFantasia, v => NomeFantasia = v);
        r.Valor(new(CamposFichaPessoa.Cnae, Id), "CNAE principal", CnaePrincipal, d.CnaePrincipal, v => CnaePrincipal = v);
        r.Valor(new(CamposFichaPessoa.NaturezaJuridica, doCartao), "Natureza jurídica", NaturezaJuridica, d.NaturezaJuridica, v => NaturezaJuridica = v);
        if (d.CnaesSecundarios.Count > 0)
            r.Valor(new(CamposFichaPessoa.CnaesSecundarios, Id), "CNAEs secundários", CnaesSecundarios, string.Join(", ", d.CnaesSecundarios),
                v => CnaesSecundarios = v);

        // MEI também é optante do Simples: o MEI é o mais específico. A Receita diz que não é optante: regime normal (lucro
        // presumido ou real; para o ICMS é o mesmo "regime normal"). Sem registro no Simples (a consulta respondeu os dados
        // da empresa, mas sem a opção): regime normal só no vazio. "Não informado" conta como vazio.
        RegimeTributario? regime = d.OpcaoMei == true ? RegimeTributario.Mei
            : d.OpcaoSimples == true ? RegimeTributario.SimplesNacional
            : d.OpcaoSimples == false ? RegimeTributario.RegimeNormal
            : d.OpcaoMei is null && d.CnaePrincipal is not null && Regime.Valor == RegimeTributario.NaoInformado ? RegimeTributario.RegimeNormal
            : null;
        if (regime is { } novoRegime)
        {
            var opcao = Opcao.De(OpcoesPessoa.Regimes, novoRegime);
            r.Valor(new(CamposFichaPessoa.Regime, Id), "Regime tributário",
                Regime.Valor == RegimeTributario.NaoInformado ? null : Regime.Texto, opcao.Texto, _ => Regime = opcao);
        }

        // Inscrição ativa no estado do endereço do CNPJ (com ela, contribuinte do ICMS).
        var inscricao = d.InscricoesEstaduais.FirstOrDefault(i => i.Ativa && string.Equals(i.Uf, d.Uf, StringComparison.OrdinalIgnoreCase));
        InscricaoVeioDaConsulta = inscricao is not null;
        if (inscricao is not null)
            r.Valor(new(CamposFichaPessoa.InscricaoEstadual, Id), "Inscrição estadual", InscricaoEstadual, inscricao.Numero, v =>
            {
                InscricaoEstadual = v;
                IndicadorIE = Opcao.De(OpcoesPessoa.IndicadoresIE, global::Lone.Domain.Enums.IndicadorIE.Contribuinte); // o nome curto seria a propriedade
            });
    }
}
