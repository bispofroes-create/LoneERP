using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Etiquetas;
using Lone.Contracts.Profissoes;
using Lone.Contracts.Papeis;
using Lone.Contracts.Colaboradores;
using Lone.Contracts.Comercial;
using Lone.Contracts.Contatos;
using Lone.Contracts.Documentos;
using Lone.Contracts.Enderecos;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Municipios;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;
using DocumentoFiscal = Lone.Domain.Validacao.Documento;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Ficha de uma pessoa em edição. Cada parte tem a sua classe (estabelecimento, endereço, contato, documento,
/// papel, contas); esta classe compõe o conjunto, mantém as regras entre as partes e converte de/para o DTO.
/// </summary>
public sealed partial class PessoaFormulario : ObservableObject
{
    // Dados que a ficha ainda não edita, mas precisam voltar intactos ao salvar.
    private Guid? _mescladaEmId;
    private List<ContaClienteDto> _outrasContasCliente = [];
    private List<ContaFornecedorDto> _outrasContasFornecedor = [];

    private PessoaFormulario(Guid id, bool nova)
    {
        Id = id;
        Nova = nova;
    }

    public Guid Id { get; }
    public bool Nova { get; }
    public bool Existente => !Nova;
    public byte[]? Versao { get; private set; }
    public int Codigo { get; private set; }

    /// <summary>Definidos pela tela: consultas feitas pela API.</summary>
    public Func<EnderecoFormulario, Task>? ConsultaCep { get; set; }

    /// <summary>Conferência do CEP pelo motor (F2): só consulta e mostra; aplicar a sugestão não salva.</summary>
    public Func<EnderecoFormulario, Task>? ConferenciaCep { get; set; }

    /// <summary>Busca de CEP pelo endereço sem CEP (Checkpoint D): só mostra candidatos; usar um não salva.</summary>
    public Func<EnderecoFormulario, Task>? BuscaCepPorEndereco { get; set; }

    /// <summary>Segunda opinião sobre o CEP conferido (Checkpoint G): compara duas fontes; nada é escolhido nem salvo.</summary>
    public Func<EnderecoFormulario, Task>? SegundaOpiniaoCep { get; set; }
    public Func<EstabelecimentoFormulario, Task>? ConsultaCnpj { get; set; }

    private Func<string, Task<IReadOnlyList<MunicipioDto>>>? _fonteMunicipios;

    /// <summary>Definida pela tela: municípios de uma UF (naturalidade e endereços usam a mesma fonte).</summary>
    public Func<string, Task<IReadOnlyList<MunicipioDto>>>? FonteMunicipios
    {
        get => _fonteMunicipios;
        set
        {
            _fonteMunicipios = value;
            Naturalidade.Fonte = value;
            foreach (var e in Enderecos) e.Municipio.Fonte = value;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EhFisica), nameof(EhJuridica), nameof(EhEstrangeiro), nameof(NaoEhJuridica),
                              nameof(RotuloNome), nameof(RotuloDocumento), nameof(MascaraDocumento), nameof(MostrarCorRaca),
                              nameof(AjudaNomeExibicao), nameof(NaturezaNaTela))]
    private Opcao<NaturezaPessoa> _natureza = OpcoesPessoa.Naturezas[0];

    // ---- Troca de natureza pela tela: sair de pessoa jurídica com dados da empresa pede confirmação ----

    /// <summary>
    /// O que a lista "Natureza" mostra e escolhe. Sair de pessoa jurídica com dados da empresa (CNPJ, consulta à Receita,
    /// razão social consultada...) pergunta antes e, se confirmado, limpa esses dados; se não, a lista volta para PJ.
    /// Sem dados da empresa, troca direto.
    /// </summary>
    public Opcao<NaturezaPessoa> NaturezaNaTela
    {
        get => Natureza;
        set
        {
            if (value is null || Equals(value, Natureza)) return;
            if (EhJuridica && value.Valor != NaturezaPessoa.Juridica && TemDadosDaEmpresa && Confirmar is not null)
            {
                _ = TrocarNaturezaComConfirmacaoAsync(value);
                return;
            }
            // Bloco G (P1-2): sair de pessoa física com dados que só existem nela pergunta antes (a API também exige a
            // confirmação, para a troca e os dados exatos).
            if (value.Valor != NaturezaPessoa.Fisica && (EhFisica || _trocaConfirmada is not null) && Confirmar is not null &&
                PerdasAoSairDaPessoaFisica(value.Valor) is { Count: > 0 } perdas && !Cobre(_trocaConfirmada, value.Valor, perdas))
            {
                _ = TrocarDePessoaFisicaComConfirmacaoAsync(value, perdas);
                return;
            }
            Natureza = value;
        }
    }

    // ---- Bloco G: sair de pessoa física ----

    /// <summary>Dados só de PF como vieram do cadastro gravado (nulo se ele não era PF ou se a ficha é nova).</summary>
    private DadosSoDaPessoaFisica? _dadosPessoaFisicaCarregados;

    /// <summary>A troca de pessoa física confirmada pelo usuário: vai na gravação enquanto a natureza for a confirmada.</summary>
    private ConfirmacaoTrocaNaturezaDto? _trocaConfirmada;

    /// <summary>O que seria apagado ao trocar para <paramref name="nova"/>: o que foi carregado e o que está na tela.</summary>
    public IReadOnlyList<PerdaNaTroca> PerdasAoSairDaPessoaFisica(NaturezaPessoa nova)
    {
        var tela = DadosPessoaFisicaDaTela();
        return RegrasNaturezaPessoa.PerdasAoSairDaPessoaFisica(_dadosPessoaFisicaCarregados?.Mais(tela) ?? tela, nova);
    }

    private static bool Cobre(ConfirmacaoTrocaNaturezaDto? confirmada, NaturezaPessoa nova, IReadOnlyList<PerdaNaTroca> perdas) =>
        confirmada is not null && confirmada.Para == nova && perdas.All(p => confirmada.Campos.Contains(p.Campo));

    private DadosSoDaPessoaFisica DadosPessoaFisicaDaTela() => new(
        Cpf: Documento.Trim().Length > 0,
        NomeSocial: NomeSocial.Trim().Length > 0,
        Apelido: Apelido.Trim().Length > 0,
        DataNascimento: DataNascimento.Trim().Length > 0,
        Sexo: Sexo.Valor != SexoRegistro.NaoInformado,
        IdentidadeGenero: Genero.Valor != global::Lone.Domain.Enums.IdentidadeGenero.NaoInformado,
        EstadoCivil: EstadoCivil.Valor != global::Lone.Domain.Enums.EstadoCivil.NaoInformado,
        Escolaridade: Escolaridade.Valor != global::Lone.Domain.Enums.Escolaridade.NaoInformado,
        NomeMae: NomeMae.Trim().Length > 0,
        NomePai: NomePai.Trim().Length > 0,
        Profissao: ProfissaoEscolhida() is not null || OcupacaoEscolhida() is not null,
        Naturalidade: Naturalidade.MunicipioId is not null,
        CorRaca: CorRaca.Valor != global::Lone.Domain.Enums.CorRaca.NaoInformado || CorRacaOculta,
        InscricaoEstadual: Estabelecimentos.Count > 0 && Principal.InscricaoEstadual.Trim().Length > 0);

    private static DadosSoDaPessoaFisica DadosPessoaFisicaCarregados(PessoaDto p) => new(
        Cpf: !string.IsNullOrWhiteSpace(p.DocumentoPrincipal),
        NomeSocial: !string.IsNullOrWhiteSpace(p.NomeSocial),
        Apelido: !string.IsNullOrWhiteSpace(p.Apelido),
        DataNascimento: p.DataNascimento is not null,
        Sexo: p.Sexo != SexoRegistro.NaoInformado,
        IdentidadeGenero: p.IdentidadeGenero != global::Lone.Domain.Enums.IdentidadeGenero.NaoInformado,
        EstadoCivil: p.EstadoCivil != global::Lone.Domain.Enums.EstadoCivil.NaoInformado,
        Escolaridade: p.Escolaridade != global::Lone.Domain.Enums.Escolaridade.NaoInformado,
        NomeMae: !string.IsNullOrWhiteSpace(p.NomeMae),
        NomePai: !string.IsNullOrWhiteSpace(p.NomePai),
        Profissao: p.ProfissaoId is not null,
        Naturalidade: p.NaturalidadeMunicipioId is not null,
        // Cor/raça escondida (sem permissão de dado sensível) também conta: ela existe e seria apagada.
        CorRaca: p.CorRaca != global::Lone.Domain.Enums.CorRaca.NaoInformado || p.CorRacaOculta,
        InscricaoEstadual: p.Estabelecimentos.FirstOrDefault(e => e.Principal)?.InscricaoEstadual is { Length: > 0 });

    private async Task TrocarDePessoaFisicaComConfirmacaoAsync(Opcao<NaturezaPessoa> nova, IReadOnlyList<PerdaNaTroca> perdas)
    {
        // A lista volta a mostrar a natureza atual enquanto o usuário decide (depois do clique que a mudou).
        await Task.Yield();
        OnPropertyChanged(nameof(NaturezaNaTela));

        var confirmar = Confirmar;
        if (confirmar is null || !await confirmar(
                "Trocar o tipo de pessoa",
                $"Ao trocar para \"{nova.Texto}\", serão apagados ao salvar os dados que só existem na pessoa física: " +
                $"{RegrasNaturezaPessoa.Lista(perdas)}." +
                (_dadosPessoaFisicaCarregados is null ? string.Empty : " Os valores atuais ficam no histórico do cadastro."),
                "Trocar e apagar",
                "Não trocar"))
            return;

        _trocaConfirmada = new ConfirmacaoTrocaNaturezaDto
        {
            De = NaturezaPessoa.Fisica,
            Para = nova.Valor,
            Campos = [.. perdas.Select(p => p.Campo)]
        };
        Natureza = nova;
    }

    /// <summary>Razão social que veio da última consulta de CNPJ (se o nome ainda for ela, sai junto com os dados da empresa).</summary>
    private string _razaoSocialConsultada = string.Empty;

    public bool TemDadosDaEmpresa =>
        Estabelecimentos.Count > 0 &&
        (Principal.Cnpj.Trim().Length > 0 || Principal.NomeFantasia.Trim().Length > 0 || Principal.SituacaoReceita.Length > 0 ||
         DataAbertura.Trim().Length > 0 || Porte.Trim().Length > 0 || CapitalSocial.Trim().Length > 0 || Socios.Count > 0);

    private async Task TrocarNaturezaComConfirmacaoAsync(Opcao<NaturezaPessoa> nova)
    {
        // A lista volta a mostrar "Pessoa jurídica" enquanto o usuário decide (depois do clique que a mudou).
        await Task.Yield();
        OnPropertyChanged(nameof(NaturezaNaTela));

        var confirmar = Confirmar;
        if (confirmar is null || !await confirmar(
                "Trocar a natureza",
                $"Esta ficha tem dados de empresa (CNPJ, consulta à Receita, razão social, dados fiscais). Ao trocar para " +
                $"\"{nova.Texto}\", esses dados serão limpos. Endereços e telefones vindos da consulta continuam: revise-os.",
                "Trocar e limpar",
                "Continuar como pessoa jurídica"))
            return;

        LimparDadosDaEmpresa();
        Natureza = nova;
    }

    /// <summary>Tira da ficha os dados que só existem na PJ (vale ao salvar; o servidor também os descarta fora da PJ).</summary>
    private void LimparDadosDaEmpresa()
    {
        var principal = Principal;
        if (_razaoSocialConsultada.Length > 0 && Nome.Trim() == _razaoSocialConsultada.Trim()) Nome = string.Empty;
        _razaoSocialConsultada = string.Empty;
        ConsultaAplicada = null;
        principal.Cnpj = string.Empty;
        principal.NomeFantasia = string.Empty;
        principal.NaturezaJuridica = string.Empty;
        principal.SituacaoReceita = string.Empty;
        principal.ConsultadoReceitaEm = null;
        principal.CnaePrincipal = string.Empty;
        principal.CnaesSecundarios = string.Empty;
        principal.Regime = OpcoesPessoa.Regimes[0];
        principal.InscricaoEstadual = string.Empty;
        principal.IndicadorIE = OpcoesPessoa.IndicadoresIE[0];
        DataAbertura = string.Empty;
        Porte = string.Empty;
        CapitalSocial = string.Empty;
        Socios.Clear();
        OnPropertyChanged(nameof(TemSocios));
        GrupoEmpresarial = SemGrupoEmpresarial;
    }

    /// <summary>Ativo ou em análise (o formulário só alterna entre os dois).</summary>
    [ObservableProperty] private Opcao<SituacaoPessoa> _situacao = OpcoesPessoa.SituacoesEditaveis[0];

    /// <summary>Situação como está gravada (inativo e arquivado só mudam pelas ações próprias).</summary>
    public SituacaoPessoa SituacaoGravada { get; private set; } = SituacaoPessoa.Ativo;
    public string? SituacaoMotivo { get; private set; }
    public DateTime? SituacaoAlteradaEm { get; private set; }

    public bool PodeEscolherSituacao => SituacaoGravada is SituacaoPessoa.Ativo or SituacaoPessoa.EmAnalise;
    public bool EstaInativo => SituacaoGravada == SituacaoPessoa.Inativo;
    public bool EstaArquivado => SituacaoGravada == SituacaoPessoa.Arquivado;
    public string SituacaoTexto => NomesPessoa.Situacao(SituacaoGravada);

    /// <summary>Ex.: "Desde 24/09/2026 14:02. Motivo: não compra há 2 anos."</summary>
    public string SituacaoDetalhe => string.Join(" ", new[]
    {
        SituacaoAlteradaEm is { } quando ? $"Desde {quando.ToLocalTime().ToString("dd/MM/yyyy HH:mm", TextoTela.Brasil)}." : null,
        SituacaoMotivo is { Length: > 0 } motivo ? $"Motivo: {motivo}" : null
    }.OfType<string>());
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo), nameof(NomeCompletoCabecalho))] private string _nome = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo), nameof(NomeCompletoCabecalho))] private string _nomeSocial = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo), nameof(NomeCompletoCabecalho))] private string _nomeExibicao = string.Empty;
    [ObservableProperty] private string _apelido = string.Empty;

    /// <summary>CPF (PF) ou identificação do estrangeiro. Na PJ, o CNPJ fica nos estabelecimentos.</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(DocumentoCabecalho), nameof(DocumentoPrincipalResumo))] private string _documento = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Idade))]
    private string _dataNascimento = string.Empty;
    [ObservableProperty] private string _observacoes = string.Empty;

    // ---- Dados pessoais (PF) ----

    [ObservableProperty] private Opcao<SexoRegistro> _sexo = OpcoesPessoa.Sexos[0];
    [ObservableProperty] private Opcao<IdentidadeGenero> _genero = OpcoesPessoa.Generos[0];
    [ObservableProperty] private Opcao<CorRaca> _corRaca = OpcoesPessoa.CoresRacas[0];
    [ObservableProperty] private Opcao<EstadoCivil> _estadoCivil = OpcoesPessoa.EstadosCivis[0];
    [ObservableProperty] private Opcao<Escolaridade> _escolaridade = OpcoesPessoa.Escolaridades[0];
    [ObservableProperty] private string _nacionalidade = string.Empty;

    /// <summary>Município de nascimento: UF + autocompletar da tabela do IBGE.</summary>
    public SeletorMunicipio Naturalidade { get; } = new();

    /// <summary>Texto antigo da naturalidade que ainda precisa virar um município da lista (vazio = nada a corrigir).</summary>
    public string NaturalidadeACorrigir { get; private set; } = string.Empty;
    public bool TemNaturalidadeACorrigir => NaturalidadeACorrigir.Length > 0;
    [ObservableProperty] private string _nomeMae = string.Empty;
    [ObservableProperty] private string _nomePai = string.Empty;
    /// <summary>Profissão do cadastro de profissões (autocompletar; só vale o que for escolhido da lista).</summary>
    public SeletorDeLista Profissao { get; } = new();

    /// <summary>A API escondeu a cor/raça (sem permissão): a ficha não mostra o campo e a gravação mantém o valor.</summary>
    public bool CorRacaOculta { get; private set; }

    /// <summary>Definido pela tela conforme a permissão de ver dados sensíveis.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarCorRaca))]
    private bool _podeVerDadosSensiveis;

    /// <summary>Cor/raça (dado sensível): só para funcionário, e só para quem pode ver dados sensíveis.</summary>
    public bool MostrarCorRaca => EhFisica && PodeVerDadosSensiveis && !CorRacaOculta && Papeis.Any(p => p.Papel == TipoPapel.Funcionario && p.Ativo);

    // ---- Dados da empresa (PJ, vindos da Receita) ----

    [ObservableProperty] private string _dataAbertura = string.Empty;
    [ObservableProperty] private string _porte = string.Empty;
    [ObservableProperty] private string _capitalSocial = string.Empty;
    public ObservableCollection<SocioDto> Socios { get; } = new();
    public bool TemSocios => Socios.Count > 0;

    /// <summary>
    /// Aviso da última consulta à Receita quando algum sócio não pôde ser identificado com segurança entre os gravados
    /// (entrou como novo; D7). Vazio = nada a conferir.
    /// </summary>
    public string AvisoSocios { get; private set; } = string.Empty;

    /// <summary>
    /// Quadro da Receita × sócios da ficha (P0, D7), em vez de trocar a lista inteira: quem continua fica com o mesmo
    /// registro (dados atualizados); ex-sócio que voltou é reativado (mesmo Id, sem data de saída); quem não veio mais fica
    /// inativo com a data de saída de hoje; na dúvida (mais de um candidato), o recebido entra como novo e os gravados
    /// envolvidos ficam como estão. Quadro vazio na consulta não muda nada (não se presume que todos saíram).
    /// </summary>
    internal void CasarSocios(IReadOnlyList<SocioDto> recebidos)
    {
        var gravados = Socios.ToList();
        static DadosSocio Dados(SocioDto s) => new(s.Documento, s.Nome, s.Qualificacao);
        var r = CasamentoSocios.Casar(gravados.Select(Dados).ToList(), recebidos.Select(Dados).ToList());
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        var resultado = new List<SocioDto>();
        for (var i = 0; i < recebidos.Count; i++)
        {
            var novo = recebidos[i];
            if (r.GravadoDoRecebido[i] is not { } g)
            {
                resultado.Add(new SocioDto
                {
                    Nome = novo.Nome, Qualificacao = novo.Qualificacao, Documento = novo.Documento, EntradaEm = novo.EntradaEm, Ativo = true
                });
                continue;
            }
            var gravado = gravados[g];
            resultado.Add(new SocioDto
            {
                Id = gravado.Id,
                Nome = string.IsNullOrWhiteSpace(novo.Nome) ? gravado.Nome : novo.Nome,
                Qualificacao = novo.Qualificacao ?? gravado.Qualificacao,
                Documento = novo.Documento ?? gravado.Documento,
                EntradaEm = novo.EntradaEm ?? gravado.EntradaEm,
                Ativo = true,
                SaiuEm = null
            });
        }
        foreach (var g in Enumerable.Range(0, gravados.Count).Where(g => !r.GravadoDoRecebido.Contains(g)))
        {
            var gravado = gravados[g];
            var saiu = gravado.Ativo && !r.GravadosPreservados.Contains(g);
            resultado.Add(new SocioDto
            {
                Id = gravado.Id, Nome = gravado.Nome, Qualificacao = gravado.Qualificacao, Documento = gravado.Documento,
                EntradaEm = gravado.EntradaEm,
                Ativo = !saiu && gravado.Ativo,
                SaiuEm = saiu ? hoje : gravado.SaiuEm
            });
        }

        Socios.Clear();
        foreach (var s in resultado) Socios.Add(s);
        AvisoSocios = r.Ambiguos.Count == 0 ? string.Empty
            : string.Format(System.Globalization.CultureInfo.InvariantCulture, CasamentoSocios.TextoConferencia, r.Ambiguos.Count);
        OnPropertyChanged(nameof(AvisoSocios));
        OnPropertyChanged(nameof(TemSocios));
    }

    /// <summary>Prévia, "Ver todos os N" e busca da lista de sócios (listas longas de empresas grandes).</summary>
    public QuadroSocios QuadroSocios => _quadroSocios ??= new QuadroSocios(Socios);
    private QuadroSocios? _quadroSocios;

    // ---- Relacionamento ----

    [ObservableProperty] private string _origemCadastro = OpcoesPessoa.Origens[0];
    [ObservableProperty] private string _primeiroContatoEm = string.Empty;

    /// <summary>Etiquetas do cadastro de etiquetas, para marcar.</summary>
    public EtiquetasFormulario Etiquetas { get; private set; } = EtiquetasFormulario.Criar([], []);

    /// <summary>
    /// Aba "Privacidade" (LGPD): consentimentos por finalidade, canais e decisões. Lida quando a aba abre; conceder e
    /// revogar são ações próprias (nunca pelo "Salvar" da ficha, que não envia consentimentos).
    /// </summary>
    public PrivacidadeFormulario Privacidade { get; } = new();

    /// <summary>Definido pela tela (permissão PESSOAS.PRIVACIDADE): a aba "Privacidade" só aparece com ela.</summary>
    public bool PodeVerPrivacidade { get; set; }

    /// <summary>Origens da lista, mais a gravada se for uma que a lista não tem.</summary>
    public IReadOnlyList<string> Origens { get; private set; } = OpcoesPessoa.Origens;

    public IReadOnlyList<Opcao<SexoRegistro>> Sexos => OpcoesPessoa.Sexos;
    public IReadOnlyList<Opcao<IdentidadeGenero>> Generos => OpcoesPessoa.Generos;
    public IReadOnlyList<Opcao<CorRaca>> CoresRacas => OpcoesPessoa.CoresRacas;
    public IReadOnlyList<Opcao<EstadoCivil>> EstadosCivis => OpcoesPessoa.EstadosCivis;
    public IReadOnlyList<Opcao<Escolaridade>> Escolaridades => OpcoesPessoa.Escolaridades;

    public ObservableCollection<EstabelecimentoFormulario> Estabelecimentos { get; } = new();
    public ObservableCollection<EnderecoFormulario> Enderecos { get; } = new();
    public ObservableCollection<MeioContatoFormulario> MeiosContato { get; } = new();
    public ObservableCollection<ContatoFormulario> Contatos { get; } = new();
    public ObservableCollection<DocumentoFormulario> Documentos { get; } = new();

    /// <summary>Motivo da alteração (opcional): vai para o histórico junto com o que mudou nesta gravação.</summary>
    [ObservableProperty] private string _motivoAlteracao = string.Empty;
    public IReadOnlyList<PapelOpcao> Papeis { get; private set; } = [];

    public ContaClienteFormulario ContaCliente { get; private set; } = new();
    public ContaFornecedorFormulario ContaFornecedor { get; private set; } = new();

    /// <summary>Bloqueios (com bloquear/liberar) e relacionamento (interações): ações próprias, gravadas na hora.</summary>
    public SituacoesFormulario Situacoes { get; } = new();

    /// <summary>Relacionamentos com outros cadastros (sócio de, administrador de...): ações próprias, gravadas na hora.</summary>
    public RelacionamentosFormulario Relacionamentos { get; } = new();

    /// <summary>Bloqueios ativos (resumo no topo da ficha).</summary>
    public string TextoBloqueios { get; private set; } = string.Empty;
    public bool TemBloqueios => TextoBloqueios.Length > 0;

    // ---- Calculados ----

    public IReadOnlyList<Opcao<NaturezaPessoa>> Naturezas => OpcoesPessoa.Naturezas;
    public IReadOnlyList<Opcao<SituacaoPessoa>> SituacoesDeUso => OpcoesPessoa.SituacoesEditaveis;

    /// <summary>Campos personalizados ativos (aba "Informações adicionais"), na ordem definida pelo administrador.</summary>
    public IReadOnlyList<CampoPersonalizadoFormulario> InformacoesAdicionais { get; private set; } = [];
    public bool TemInformacoesAdicionais => InformacoesAdicionais.Count > 0;

    public EstabelecimentoFormulario Principal => Estabelecimentos[0];
    public PapelOpcao PapelCliente => Papeis.First(p => p.Papel == TipoPapel.Cliente);
    public PapelOpcao PapelFornecedor => Papeis.First(p => p.Papel == TipoPapel.Fornecedor);
    public PapelOpcao PapelEmpresaDoGrupo => Papeis.First(p => p.Papel == TipoPapel.EmpresaDoGrupo);

    public bool EhFisica => Natureza.Valor == NaturezaPessoa.Fisica;
    public bool EhJuridica => Natureza.Valor == NaturezaPessoa.Juridica;
    public bool NaoEhJuridica => !EhJuridica;
    public bool EhEstrangeiro => Natureza.Valor == NaturezaPessoa.Estrangeiro;

    public string RotuloNome => EhJuridica ? "Razão social" : "Nome completo";

    /// <summary>
    /// Ajuda fixa sob o "Nome de exibição": diz o que entra no lugar dele quando fica vazio, na mesma ordem de
    /// <see cref="NomePessoa.ParaExibir"/> (o nome social só existe na pessoa física).
    /// </summary>
    public string AjudaNomeExibicao => Natureza.Valor switch
    {
        NaturezaPessoa.Juridica => "Nome usado pelo sistema nas telas e listas. Se não informado, será usado o Nome Fantasia e, na ausência dele, a Razão Social.",
        NaturezaPessoa.Fisica => "Nome usado pelo sistema nas telas e listas. Se não informado, será usado o Nome social e, na ausência dele, o Nome completo.",
        _ => "Nome usado pelo sistema nas telas e listas. Se não informado, será usado o Nome completo."
    };
    public string RotuloDocumento => EhFisica ? "CPF" : "Identificação estrangeira";

    /// <summary>CPF com máscara; a identificação de estrangeiro tem formatos variados e fica livre.</summary>
    public TipoMascara MascaraDocumento => EhFisica ? TipoMascara.Cpf : TipoMascara.Nenhuma;

    /// <summary>Data de referência da idade (a ficha usa a data do aparelho; os testes fixam uma).</summary>
    public DateOnly Hoje { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>
    /// Calculada da data de nascimento, nunca gravada (ex.: "43 anos"). Só com a data completa (dd/mm/aaaa): enquanto
    /// se digita "13/05/19" não aparece a idade de quem nasceu em 2019. Data apagada ou inválida = idade vazia na hora.
    /// </summary>
    public string Idade => global::Lone.Domain.Comum.Idade.Texto(TextoTela.DataCompleta(DataNascimento), Hoje);
    /// <summary>
    /// Nome que identifica a pessoa (regra única <see cref="NomePessoa.ParaExibir"/>): nome de exibição → (PJ) nome
    /// fantasia do estabelecimento principal → nome social → nome civil / razão social. Só apresentação.
    /// </summary>
    public string Titulo
    {
        get
        {
            var nome = NomePessoa.ParaExibir(Natureza.Valor, Nome, NomeExibicao, NomeSocial,
                Estabelecimentos.Count > 0 ? Principal.NomeFantasia : null);
            return string.IsNullOrWhiteSpace(nome) ? "Nova pessoa" : nome;
        }
    }

    // ---- Cabeçalho da ficha (só leitura: tudo vem dos campos da própria ficha) ----

    /// <summary>Nome civil / razão social, quando diferente do nome do título (vazio = não repete).</summary>
    public string NomeCompletoCabecalho =>
        string.IsNullOrWhiteSpace(Nome) || string.Equals(Nome.Trim(), Titulo, StringComparison.Ordinal) ? string.Empty
        : EhJuridica ? "Razão social: " + Nome.Trim()
        : Nome.Trim();

    /// <summary>"Pessoa jurídica · Cliente · Fornecedor".</summary>
    public string TipoEPapeisCabecalho => string.Join(" · ",
        new[] { NomesPessoa.Natureza(Natureza.Valor) }.Concat(Papeis.Where(p => p.Ativo).Select(p => p.Nome)));

    /// <summary>"CNPJ 12.345.678/0001-90 · Código 000123" (PJ: o CNPJ do principal; PF: o CPF; estrangeiro: a identificação).</summary>
    public string DocumentoCabecalho => string.Join(" · ", new[]
    {
        EhJuridica
            ? (Estabelecimentos.Count > 0 && Principal.Cnpj.Length > 0 ? "CNPJ " + Principal.Cnpj : string.Empty)
            : Documento.Length > 0 ? (EhFisica ? "CPF " : "Identificação ") + Documento : string.Empty,
        Nova ? "Novo cadastro" : $"Código {Codigo:000000}",
        "Situação: " + SituacaoTexto
    }.Where(t => t.Length > 0));

    /// <summary>
    /// Aba Documentos: uma linha com o documento principal ("CNPJ 12.345.678/0001-90 · alterado na aba Identificação").
    /// Um só ponto de edição: a Identificação.
    /// </summary>
    public string DocumentoPrincipalResumo
    {
        get
        {
            var (rotulo, valor) = EhJuridica
                ? ("CNPJ", Estabelecimentos.Count > 0 ? Principal.Cnpj : string.Empty)
                : (EhFisica ? "CPF" : "Identificação estrangeira", Documento);
            return (valor.Length > 0 ? $"{rotulo} {valor}" : $"{rotulo} não informado") + " · alterado na aba \"Identificação\"";
        }
    }

    /// <summary>PJ: grupo empresarial e quantos estabelecimentos (vazio = não se aplica).</summary>
    public string EstruturaCabecalho
    {
        get
        {
            if (!EhJuridica) return string.Empty;
            var ativos = Estabelecimentos.Count(e => e.Ativo);
            var estabelecimentos = Estabelecimentos.Count == 1
                ? "1 estabelecimento"
                : ativos == Estabelecimentos.Count
                    ? $"{Estabelecimentos.Count} estabelecimentos"
                    : $"{Estabelecimentos.Count} estabelecimentos ({(ativos == 1 ? "1 ativo" : $"{ativos} ativos")})";
            var grupo = GrupoEmpresarial.Valor is null ? string.Empty : "Grupo empresarial: " + GrupoEmpresarial.Texto;
            return string.Join(" · ", new[] { grupo, estabelecimentos }.Where(t => t.Length > 0));
        }
    }

    public bool TemEstruturaCabecalho => EstruturaCabecalho.Length > 0;
    public bool TemNomeCompletoCabecalho => NomeCompletoCabecalho.Length > 0;

    // ---- Cabeçalho redesenhado (linha 2: tipo + papéis como selos; linha 3: documento, código e local) ----

    /// <summary>"Pessoa física", "Pessoa jurídica" ou "Estrangeiro" (os papéis aparecem como selos ao lado).</summary>
    public string NaturezaCabecalho => NomesPessoa.Natureza(Natureza.Valor);

    /// <summary>"CNPJ 12.345.678/0001-90 · Código 000123 · Curvelo/MG" (sem a situação: ela vira o selo à direita do nome).</summary>
    public string IdentificacaoCabecalho => string.Join("  ·  ", new[]
    {
        EhJuridica
            ? (Estabelecimentos.Count > 0 && Principal.Cnpj.Length > 0 ? "CNPJ " + Principal.Cnpj : string.Empty)
            : Documento.Length > 0 ? (EhFisica ? "CPF " : "Identificação ") + Documento : string.Empty,
        Nova ? "Novo cadastro" : $"Código {Codigo:000000}",
        LocalCabecalho
    }.Where(t => t.Length > 0));

    /// <summary>Cidade/UF do primeiro endereço ativo com município escolhido (vazio = sem endereço).</summary>
    public string LocalCabecalho =>
        Enderecos.Where(e => e.Ativo).Select(e => e.Municipio.Selecionado).FirstOrDefault(m => m is not null) is { } m
            ? $"{m.Nome}/{m.Uf}"
            : string.Empty;

    /// <summary>Etiquetas marcadas (vazio = nenhuma).</summary>
    public string EtiquetasCabecalho => Etiquetas.Marcadas.Count == 0 ? string.Empty : "Etiquetas: " + Etiquetas.Resumo;

    /// <summary>Refaz as linhas do cabeçalho (chamado quando muda algo que elas mostram).</summary>
    private void AtualizarCabecalho()
    {
        OnPropertyChanged(nameof(Titulo));
        OnPropertyChanged(nameof(NomeCompletoCabecalho));
        OnPropertyChanged(nameof(TemNomeCompletoCabecalho));
        OnPropertyChanged(nameof(TipoEPapeisCabecalho));
        OnPropertyChanged(nameof(DocumentoCabecalho));
        OnPropertyChanged(nameof(DocumentoPrincipalResumo));
        OnPropertyChanged(nameof(EstruturaCabecalho));
        OnPropertyChanged(nameof(TemEstruturaCabecalho));
        OnPropertyChanged(nameof(EtiquetasCabecalho));
        OnPropertyChanged(nameof(NaturezaCabecalho));
        OnPropertyChanged(nameof(IdentificacaoCabecalho));
    }

    // ---- Grupo empresarial (só pessoa jurídica; opcional) ----

    public static readonly Opcao<Guid?> SemGrupoEmpresarial = new(null, "Nenhum (empresa independente)");

    /// <summary>Gravado ao abrir: volta intacto enquanto as opções não forem lidas.</summary>
    private Guid? _grupoEmpresarialGravado;

    /// <summary>Tipo de pessoa como está gravado (pessoa jurídica gravada não vira outra natureza).</summary>
    private NaturezaPessoa? _naturezaGravada;

    /// <summary>"Nenhum", os grupos ativos e o gravado (mesmo desativado). Array: o Picker precisa de IList.</summary>
    [ObservableProperty] private Opcao<Guid?>[] _gruposEmpresariais = [SemGrupoEmpresarial];

    [ObservableProperty][NotifyPropertyChangedFor(nameof(EstruturaCabecalho), nameof(TemEstruturaCabecalho))]
    private Opcao<Guid?> _grupoEmpresarial = SemGrupoEmpresarial;

    public bool OpcoesEstruturaCarregadas { get; private set; }

    /// <summary>Chamado pela tela quando as opções (grupos e tipos de relacionamento) chegam.</summary>
    public void DefinirGruposEmpresariais(IReadOnlyList<Lone.Contracts.GruposEmpresariais.GrupoEmpresarialDto> grupos)
    {
        var atual = GrupoEmpresarial.Valor;
        GruposEmpresariais =
        [
            SemGrupoEmpresarial,
            .. grupos.Where(g => g.Ativo || g.Id == _grupoEmpresarialGravado)
                .OrderBy(g => g.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Select(g => new Opcao<Guid?>(g.Id, g.Ativo ? g.Nome : g.Nome + " (desativado)"))
        ];
        GrupoEmpresarial = GruposEmpresariais.FirstOrDefault(o => o.Valor == atual) ?? SemGrupoEmpresarial;
        OpcoesEstruturaCarregadas = true;
    }
    public string CodigoTexto => Nova ? "Novo cadastro" : $"Código {Codigo:000000}";

    partial void OnNaturezaChanged(Opcao<NaturezaPessoa> value)
    {
        foreach (var e in Estabelecimentos)
        {
            e.DaPessoaJuridica = EhJuridica;
            e.DaPessoaEstrangeira = EhEstrangeiro;
        }
        foreach (var d in Documentos) d.DefinirNatureza(value.Valor);
        AtualizarCabecalho();
        ConferirDocumento();
    }

    // ---- Criação e conversão ----

    public static PessoaFormulario NovaPessoa(IReadOnlyList<CampoPersonalizadoDto>? campos = null, IReadOnlyList<EtiquetaDto>? etiquetas = null,
                                             IReadOnlyList<ProfissaoDto>? profissoes = null, IReadOnlyList<PapelCadastroDto>? papeis = null,
                                             IReadOnlyList<TipoMeioContatoDto>? tiposMeio = null, IReadOnlyList<TipoEnderecoDto>? tiposEndereco = null,
                                             IReadOnlyList<TipoDocumentoDto>? tiposDocumento = null,
                                             IReadOnlyList<CampoPersonalizadoDto>? camposDocumento = null,
                                             IReadOnlyList<FinalidadeEnderecoDto>? finalidades = null)
    {
        var f = new PessoaFormulario(IdSequencial.Novo(), nova: true)
        {
            _finalidades = finalidades ?? [],
            _camposDocumento = camposDocumento ?? [],
            _tiposMeio = tiposMeio ?? [],
            _tiposEndereco = tiposEndereco ?? [],
            _tiposDocumento = tiposDocumento ?? [],
            Papeis = MontarPapeis(papeis, [], out _),
            InformacoesAdicionais = MontarInformacoesAdicionais(campos, []),
            Etiquetas = EtiquetasFormulario.Criar(etiquetas, [])
        };
        f.DefinirProfissoes(profissoes, null);
        f.PapelCliente.Ativo = true;
        f.OuvirPapeis();
        f.AdicionarEndereco(new EnderecoFormulario()); // finalidades e principal: escolhidos pelo usuário
        f.AdicionarEstabelecimento();
        f.OuvirCabecalho();
        return f;
    }

    public static PessoaFormulario De(PessoaDto p, IReadOnlyList<CampoPersonalizadoDto>? campos = null, IReadOnlyList<EtiquetaDto>? etiquetas = null,
                                      IReadOnlyList<ProfissaoDto>? profissoes = null, IReadOnlyList<PapelCadastroDto>? papeis = null,
                                      IReadOnlyList<TipoMeioContatoDto>? tiposMeio = null, IReadOnlyList<TipoEnderecoDto>? tiposEndereco = null,
                                      IReadOnlyList<TipoDocumentoDto>? tiposDocumento = null,
                                      IReadOnlyList<CampoPersonalizadoDto>? camposDocumento = null,
                                      IReadOnlyList<FinalidadeEnderecoDto>? finalidades = null)
    {
        var opcoesPapel = MontarPapeis(papeis, p.Papeis, out var papeisDesconhecidos);
        var f = new PessoaFormulario(p.Id, nova: false)
        {
            _finalidades = finalidades ?? [],
            RevisarFinalidadesEndereco = p.RevisarFinalidadesEndereco,
            InformacoesAdicionais = MontarInformacoesAdicionais(campos, p.ValoresPersonalizados),
            Etiquetas = EtiquetasFormulario.Criar(etiquetas, p.EtiquetaIds),
            SituacaoGravada = p.Situacao,
            SituacaoMotivo = p.SituacaoMotivo,
            SituacaoAlteradaEm = p.SituacaoAlteradaEm,
            NaturalidadeACorrigir = p.PendenciasMunicipio.FirstOrDefault(x => x.DaNaturalidade)?.Texto ?? string.Empty,
            _grupoEmpresarialGravado = p.GrupoEmpresarialId,
            _naturezaGravada = p.Natureza,
            _dadosPessoaFisicaCarregados = p.Natureza == NaturezaPessoa.Fisica ? DadosPessoaFisicaCarregados(p) : null,
            _mescladaEmId = p.MescladaEmId,
            _outrasContasCliente = p.ContasCliente.Where(c => c.EmpresaId is not null).ToList(),
            _outrasContasFornecedor = p.ContasFornecedor.Where(c => c.EmpresaId is not null).ToList(),
            Versao = p.Versao,
            Codigo = p.Codigo,
            Natureza = Opcao.De(OpcoesPessoa.Naturezas, p.Natureza),
            Situacao = Opcao.De(OpcoesPessoa.SituacoesEditaveis, p.Situacao is SituacaoPessoa.EmAnalise ? SituacaoPessoa.EmAnalise : SituacaoPessoa.Ativo),
            Nome = p.Nome,
            NomeSocial = p.NomeSocial ?? string.Empty,
            NomeExibicao = p.NomeExibicao ?? string.Empty,
            Apelido = p.Apelido ?? string.Empty,
            Documento = p.Natureza switch
            {
                NaturezaPessoa.Juridica => string.Empty,
                NaturezaPessoa.Fisica => DocumentoFiscal.Formatar(p.DocumentoPrincipal),
                _ => p.DocumentoPrincipal ?? string.Empty
            },
            DataNascimento = TextoTela.Data(p.DataNascimento),
            Observacoes = p.Observacoes ?? string.Empty,
            ContaCliente = ContaClienteFormulario.De(p.ContasCliente.FirstOrDefault(c => c.EmpresaId is null)),
            ContaFornecedor = ContaFornecedorFormulario.De(p.ContasFornecedor.FirstOrDefault(c => c.EmpresaId is null)),
            TextoBloqueios = string.Join(Environment.NewLine, p.Bloqueios.Where(b => b.Ativo).Select(b =>
                $"{b.Escopo} desde {b.InicioEm.ToLocalTime().ToString("dd/MM/yyyy", TextoTela.Brasil)} por {b.InicioPor}: {b.Motivo}")),
            Papeis = opcoesPapel,
            _tiposMeio = tiposMeio ?? [],
            _tiposEndereco = tiposEndereco ?? [],
            _tiposDocumento = tiposDocumento ?? [],
            _camposDocumento = camposDocumento ?? [],
            _papeisDesconhecidos = papeisDesconhecidos,
            Sexo = Opcao.De(OpcoesPessoa.Sexos, p.Sexo),
            Genero = Opcao.De(OpcoesPessoa.Generos, p.IdentidadeGenero),
            CorRaca = Opcao.De(OpcoesPessoa.CoresRacas, p.CorRaca),
            CorRacaOculta = p.CorRacaOculta,
            EstadoCivil = Opcao.De(OpcoesPessoa.EstadosCivis, p.EstadoCivil),
            Escolaridade = Opcao.De(OpcoesPessoa.Escolaridades, p.Escolaridade),
            Nacionalidade = p.Nacionalidade ?? string.Empty,
            NomeMae = p.NomeMae ?? string.Empty,
            NomePai = p.NomePai ?? string.Empty,
            DataAbertura = TextoTela.Data(p.DataAbertura),
            Porte = p.Porte ?? string.Empty,
            CapitalSocial = TextoTela.Decimal(p.CapitalSocial),
            PrimeiroContatoEm = TextoTela.Data(p.PrimeiroContatoEm)
        };

        if (p.GrupoEmpresarialId is { } grupoGravado)
        {
            // Antes de ler as opções, a lista tem só o gravado (o cabeçalho já mostra o nome).
            var gravado = new Opcao<Guid?>(grupoGravado, p.GrupoEmpresarialNome ?? "(grupo empresarial)");
            f.GruposEmpresariais = [SemGrupoEmpresarial, gravado];
            f.GrupoEmpresarial = gravado;
        }
        f.Naturalidade.Definir(p.NaturalidadeMunicipioId, p.NaturalidadeNome, p.NaturalidadeUf);
        f.DefinirProfissoes(profissoes, p.ProfissaoId);
        f.DefinirOrigem(p.OrigemCadastro);
        foreach (var socio in p.Socios) f.Socios.Add(socio);

        f.OuvirPapeis();
        foreach (var e in p.Enderecos.OrderBy(e => e.Ordem))
            f.AdicionarEndereco(EnderecoFormulario.De(e,
                p.PendenciasMunicipio.FirstOrDefault(x => !x.DaNaturalidade && x.RegistroId == e.Id)?.Texto));

        f.ColaboradorOculto = p.ColaboradorOculto;
        f.Situacoes.Carregar(p.Bloqueios, p.Relacionamento);
        foreach (var e in p.ExcecoesComerciais.OrderBy(e => e.InicioEm)) f.AdicionarExcecao(ExcecaoComercialFormulario.De(e));
        foreach (var c in p.Carteira.OrderBy(c => c.InicioEm)) f.AdicionarCarteira(CarteiraFormulario.De(c));
        foreach (var v in p.Vinculos.OrderByDescending(v => v.AdmissaoEm)) f.AdicionarVinculo(VinculoFormulario.De(v));

        foreach (var e in p.Estabelecimentos.OrderByDescending(e => e.Principal).ThenBy(e => e.Cnpj))
            f.Incluir(EstabelecimentoFormulario.De(e, f.Enderecos));
        if (f.Estabelecimentos.Count == 0)
            f.AdicionarEstabelecimento();
        f.MarcarPrincipal();
        f.OuvirCabecalho();

        foreach (var m in p.MeiosContato.OrderByDescending(m => m.Ativo).ThenBy(m => m.Tipo).ThenByDescending(m => m.Principal))
            f.AdicionarMeio(MeioContatoFormulario.De(m));
        foreach (var c in p.Contatos.OrderByDescending(c => c.Principal).ThenBy(c => c.Nome))
            f.AdicionarContato(ContatoFormulario.De(c));
        foreach (var d in p.Documentos)
            f.AdicionarDocumento(DocumentoFormulario.De(d));

        f.CnpjGravado = f.Estabelecimentos.Count > 0 ? DocumentoFiscal.Normalizar(f.Principal.Cnpj) : string.Empty;
        f._documentoConferido = f.ChaveDocumento(); // o gravado não é conferido de novo ao abrir
        return f;
    }

    /// <summary>Datas e números que não dá para entender. O resto (CPF, CNPJ, obrigatórios) a API valida.</summary>
    /// <summary>Só os textos (o contrato de sempre).</summary>
    public IReadOnlyList<string> ValidarLocalmente() => ValidarLocalmenteComCampos().Mensagens();

    /// <summary>
    /// A conferência antes de chamar a API, com o campo da ficha de cada erro (Lone Contextual, Fase 1). Itens de lista
    /// (endereço, documento, estabelecimento, campo personalizado) levam o Id do registro. Vínculos, carteira, exceções e
    /// contas seguem sem campo (erro geral) nesta fase.
    /// </summary>
    public ListaErros ValidarLocalmenteComCampos()
    {
        var erros = new ListaErros();
        // Pessoa jurídica gravada não muda de natureza: CNPJ, estabelecimentos, grupo e vínculos seriam perdidos (a API também recusa).
        if (_naturezaGravada == NaturezaPessoa.Juridica && !EhJuridica)
            erros.Add("Uma pessoa jurídica gravada não pode virar pessoa física ou estrangeiro: o CNPJ, os estabelecimentos, " +
                      "o nome fantasia, o grupo empresarial e os vínculos de sócio/administrador seriam perdidos. Volte o tipo para " +
                      "\"Pessoa jurídica\". Se o tipo está errado, cadastre a pessoa correta e desative este cadastro.", CamposFichaPessoa.Natureza);
        else if (!EhJuridica && GrupoEmpresarial.Valor is not null)
            erros.Add("Só pessoa jurídica pode fazer parte de um grupo empresarial.", CamposFichaPessoa.GrupoEmpresarial);
        if (EhFisica)
        {
            if (!TextoTela.TentarData(DataNascimento, out var nascimento))
                erros.Add("Data de nascimento inválida (use dd/mm/aaaa).", CamposFichaPessoa.DataNascimento);
            else if (nascimento > DateOnly.FromDateTime(DateTime.Today))
                erros.Add("A data de nascimento está no futuro.", CamposFichaPessoa.DataNascimento);
        }
        if (EhFisica && Naturalidade.Validar("Naturalidade") is { } naturalidade)
            erros.Add(naturalidade, CamposFichaPessoa.Naturalidade);
        if (EhFisica && Profissao.Validar("Profissão") is { } profissao)
            erros.Add(profissao, CamposFichaPessoa.Profissao);
        if (EhJuridica)
            foreach (var e in Estabelecimentos)
                if (e.NaturezaJuridicaLista.Validar(e.EhPrincipal ? "Natureza jurídica" : $"Natureza jurídica ({e.Titulo})") is { } natureza)
                    erros.Add(natureza, CamposFichaPessoa.NaturezaJuridica, e.EhPrincipal ? null : e.Id); // principal: na Identificação
        // Endereço físico repetido: não grava um novo igual a um existente (usa-se o existente e acrescenta a finalidade).
        // Endereço em branco (o que a ficha nova já traz, sem nada digitado) não é conferido nem enviado.
        for (var i = 0; i < Enderecos.Count; i++)
            if (!Enderecos[i].EmBranco && Enderecos[i].IgualA is { } igual)
                erros.Add(Enderecos[i].DuplicidadePossivel
                    ? $"Endereço {i + 1}: parece o mesmo de \"{igual.Resumo}\". Use o endereço existente ou confirme que é outro endereço."
                    : $"Endereço {i + 1}: este endereço já está cadastrado para esta pessoa (\"{igual.Resumo}\"). Use o endereço existente.",
                    CamposFichaPessoa.Logradouro, Enderecos[i].Id);
        // Endereço inativo não é mais conferido (pode ser antigo, de antes da tabela do IBGE).
        for (var i = 0; i < Enderecos.Count; i++)
            if (Enderecos[i].Ativo && !Enderecos[i].EmBranco && Enderecos[i].ValidarMunicipio($"Endereço {i + 1}") is { } endereco)
                erros.Add(endereco, CamposFichaPessoa.Municipio, Enderecos[i].Id);
        // Exterior: código do país vazio ou 1058 viraria Brasil na API (Bloco A, D-3).
        for (var i = 0; i < Enderecos.Count; i++)
            if (!Enderecos[i].EmBranco && Enderecos[i].ValidarPais($"Endereço {i + 1}") is { } pais)
                erros.Add(pais, CamposFichaPessoa.Pais, Enderecos[i].Id);
        // Endereço novo ou alterado completo (a mesma regra da API) e o CEP conferido na consulta (inexistente/outro município).
        erros.AddRange(Lone.Domain.Enderecos.RegrasEndereco.ValidarCompletos(Enderecos.Select(e => e.ParaComparacao()).ToList(),
            Enderecos.Select(e => e.ComoGravado).OfType<Lone.Domain.Entidades.PessoaEndereco>()));
        for (var i = 0; i < Enderecos.Count; i++)
            if (Enderecos[i].ValidarCep($"Endereço {i + 1}") is { } cep)
                erros.Add(cep, CamposFichaPessoa.Cep, Enderecos[i].Id);
        erros.AddRange(Documentos.SelectMany(d => d.ValidarComCampos()));
        for (var i = 0; i < Vinculos.Count; i++)
            erros.AddRange(Vinculos[i].Validar($"Vínculo {i + 1}"));
        foreach (var c in InformacoesAdicionais)
            if (c.Validar() is { } adicional)
                erros.Add(adicional, CamposFichaPessoa.CampoPersonalizado, c.CampoId);
        if (EhJuridica && !TextoTela.TentarData(DataAbertura, out _))
            erros.Add("Data de abertura inválida (use dd/mm/aaaa).", CamposFichaPessoa.DataAbertura);
        if (EhJuridica && !TextoTela.TentarDecimal(CapitalSocial, out _))
            erros.Add("Capital social inválido.", CamposFichaPessoa.CapitalSocial);
        if (!TextoTela.TentarData(PrimeiroContatoEm, out _))
            erros.Add("Data do primeiro contato inválida (use dd/mm/aaaa).", CamposFichaPessoa.PrimeiroContato);
        if (PapelCliente.Ativo) erros.AddRange(ContaCliente.Validar());
        for (var i = 0; i < Excecoes.Count; i++) erros.AddRange(Excecoes[i].Validar($"Exceção comercial {i + 1}"));
        for (var i = 0; i < Carteira.Count; i++) erros.AddRange(Carteira[i].Validar($"Carteira {i + 1}"));
        if (PapelFornecedor.Ativo) erros.AddRange(ContaFornecedor.Validar());
        return erros;
    }

    /// <summary>
    /// Os itens das listas da ficha que ainda contam (para saber se o erro de um item deixou de valer porque ele saiu):
    /// endereços, documentos, telefones/e-mails e contatos só enquanto ativos (removido = não é mais conferido); filiais,
    /// vínculos, exceções e carteira enquanto estiverem na lista.
    /// </summary>
    public HashSet<Guid> ItensPresentes() =>
    [
        .. Enderecos.Where(e => e.Ativo).Select(e => e.Id),
        .. Documentos.Where(d => d.Ativo).Select(d => d.Id),
        .. MeiosContato.Where(m => m.Ativo).Select(m => m.Id),
        .. Contatos.Where(c => c.Ativo).Select(c => c.Id),
        .. Estabelecimentos.Select(e => e.Id),
        .. Vinculos.Select(v => v.Id),
        .. Excecoes.Select(e => e.Id),
        .. Carteira.Select(c => c.Id)
    ];

    public PessoaDto ParaDto()
    {
        TextoTela.TentarData(DataNascimento, out var nascimento);
        TextoTela.TentarData(DataAbertura, out var abertura);
        TextoTela.TentarDecimal(CapitalSocial, out var capital);
        TextoTela.TentarData(PrimeiroContatoEm, out var primeiroContato);

        var dto = new PessoaDto
        {
            Id = Id,
            Versao = Versao,
            Codigo = Codigo,
            Natureza = Natureza.Valor,
            Situacao = PodeEscolherSituacao ? Situacao.Valor : SituacaoGravada,
            Nome = Nome,
            NomeSocial = TextoTela.Nulo(NomeSocial),
            NomeExibicao = TextoTela.Nulo(NomeExibicao),
            Apelido = TextoTela.Nulo(Apelido),
            DocumentoPrincipal = EhJuridica ? null : TextoTela.Nulo(Documento),
            DataNascimento = EhFisica ? nascimento : null,
            // Vai sempre como está: nunca é limpo em silêncio (a API recusa grupo em quem não é pessoa jurídica).
            GrupoEmpresarialId = GrupoEmpresarial.Valor,
            MescladaEmId = _mescladaEmId,
            Observacoes = TextoTela.Nulo(Observacoes),
            // Só a PJ tem filiais; nas outras naturezas vai o estabelecimento principal (dados fiscais).
            Estabelecimentos = (EhJuridica ? Estabelecimentos.ToList() : [Principal]).Select(e => e.ParaDto()).ToList(),
            Enderecos = Enderecos.Where(e => !e.EmBranco).Select((e, i) => e.ParaDto(i)).ToList(),
            MeiosContato = MeiosContato.Select(m => m.ParaDto()).ToList(),
            Contatos = Contatos.Select(c => c.ParaDto()).ToList(),
            Documentos = Documentos.Select(d => d.ParaDto()).ToList(),
            Vinculos = Vinculos.Select(v => v.ParaDto()).ToList(),
            ExcecoesComerciais = Excecoes.Select(e => e.ParaDto()).ToList(),
            Carteira = Carteira.Select(c => c.ParaDto()).ToList(),
            Papeis = [.. Papeis.SelectMany(p => p.ParaDtos()), .. _papeisDesconhecidos],
            Sexo = Sexo.Valor,
            IdentidadeGenero = Genero.Valor,
            CorRaca = CorRaca.Valor,
            EstadoCivil = EstadoCivil.Valor,
            Escolaridade = Escolaridade.Valor,
            Nacionalidade = TextoTela.Nulo(Nacionalidade),
            NaturalidadeMunicipioId = EhFisica ? Naturalidade.MunicipioId : null,
            NomeMae = TextoTela.Nulo(NomeMae),
            NomePai = TextoTela.Nulo(NomePai),
            ProfissaoId = EhFisica ? ProfissaoEscolhida() : null,
            OcupacaoCboEscolhida = EhFisica ? OcupacaoEscolhida() : null,
            DataAbertura = abertura,
            Porte = TextoTela.Nulo(Porte),
            CapitalSocial = capital,
            Socios = Socios.ToList(),
            OrigemCadastro = OrigemCadastro == OpcoesPessoa.Origens[0] ? null : TextoTela.Nulo(OrigemCadastro),
            PrimeiroContatoEm = primeiroContato,
            EtiquetaIds = Etiquetas.Marcadas.ToList(),
            ValoresPersonalizados = InformacoesAdicionais.Select(c => c.ParaDto()).OfType<ValorPersonalizadoDto>().ToList(),
            MotivoAlteracao = TextoTela.Nulo(MotivoAlteracao)?.Trim(),
            RelacionarAoCriar = Nova ? RelacionarAoCriar : null,
            // Bloco G: só a troca confirmada para a natureza que está indo (a API confere de novo contra o gravado).
            ConfirmacaoTrocaNatureza = _trocaConfirmada is { } troca && troca.Para == Natureza.Valor ? troca : null
        };

        // Conta padrão entra quando o papel existe (ativo ou não); as de outras empresas voltam intactas.
        if (PapelCliente.Ativo || PapelCliente.Existia || ContaCliente.Existia)
            dto.ContasCliente.Add(ContaCliente.ParaDto());
        dto.ContasCliente.AddRange(_outrasContasCliente);

        if (PapelFornecedor.Ativo || PapelFornecedor.Existia || ContaFornecedor.Existia)
            dto.ContasFornecedor.Add(ContaFornecedor.ParaDto());
        dto.ContasFornecedor.AddRange(_outrasContasFornecedor);

        return dto;
    }

    // ---- Apoio ----

    // ---- Papéis ----

    /// <summary>Períodos de papéis fora do cadastro lido (ex.: o cadastro não pôde ser lido): voltam intactos.</summary>
    private List<PapelDto> _papeisDesconhecidos = [];

    /// <summary>
    /// Um item por papel: os ativos do cadastro de papéis (na ordem dele) e os desativados que a pessoa tem ativos.
    /// Sem o cadastro (falha ao ler), usa os papéis de sistema. Os de sistema sempre aparecem (as regras da ficha usam).
    /// </summary>
    private static List<PapelOpcao> MontarPapeis(IReadOnlyList<PapelCadastroDto>? cadastro, IReadOnlyList<PapelDto> periodos,
                                                 out List<PapelDto> desconhecidos)
    {
        var itens = (cadastro ?? []).Select(c => (c.Id, c.PapelSistema, c.Nome, c.Ativo, c.Ordem, c.Descricao)).ToList();
        foreach (var sistema in global::Lone.Domain.Papeis.PapeisSistema.Todos.Where(s => itens.All(i => i.PapelSistema != s.Tipo)))
            itens.Add((sistema.Id, sistema.Tipo, sistema.Nome, true, sistema.Ordem, (string?)null));

        // Período sem o Id do cadastro (dado antigo) é reconhecido pelo papel de sistema.
        bool DoPapel(PapelDto periodo, Guid id, TipoPapel? sistema) =>
            periodo.PapelId == id || (periodo.PapelId == Guid.Empty && periodo.Papel is not null && periodo.Papel == sistema);

        // Papel desativado no cadastro entra se a pessoa tem ou já teve período dele (o histórico aparece na ficha).
        var opcoes = itens
            .Where(i => i.Ativo || periodos.Any(p => DoPapel(p, i.Id, i.PapelSistema)))
            .OrderBy(i => i.Ordem).ThenBy(i => i.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(i => new PapelOpcao(i.Id, i.PapelSistema, i.Nome, i.Ativo, periodos.Where(p => DoPapel(p, i.Id, i.PapelSistema)), i.Descricao))
            .ToList();
        desconhecidos = periodos.Where(p => !opcoes.Any(o => DoPapel(p, o.PapelId, o.Papel))).ToList();
        return opcoes;
    }

    // ---- Profissão ----

    /// <summary>Gravada, mas fora da lista lida (a lista não pôde ser lida): volta intacta ao salvar.</summary>
    private Guid? _profissaoDesconhecida;
    private IReadOnlyList<ProfissaoDto> _profissoesCadastro = [];
    private IReadOnlyList<OcupacaoCboDto> _ocupacoesCbo = [];

    /// <summary>Chave das sugestões que vêm da CBO (as do cadastro usam o Id da profissão).</summary>
    public const string PrefixoCbo = "cbo:";

    /// <summary>
    /// Oferece as profissões ativas e mostra a gravada (mesmo desativada). Sem a lista (falha ao ler), a gravada
    /// é mantida sem ser mostrada pelo nome.
    /// </summary>
    private void DefinirProfissoes(IReadOnlyList<ProfissaoDto>? profissoes, Guid? gravada)
    {
        _profissoesCadastro = profissoes ?? [];
        MontarItensProfissao();
        var atual = gravada is { } id ? _profissoesCadastro.FirstOrDefault(x => x.Id == id) : null;
        _profissaoDesconhecida = gravada is not null && atual is null ? gravada : null;
        Profissao.Definir(atual is null ? null : ItemDe(atual));
    }

    /// <summary>
    /// Oferece também as ocupações da tabela oficial da CBO (depois das profissões cadastradas). Escolher uma delas
    /// faz a API usar ou criar a profissão ao salvar.
    /// </summary>
    public void OferecerOcupacoesCbo(IReadOnlyList<OcupacaoCboDto>? ocupacoes)
    {
        _ocupacoesCbo = ocupacoes ?? [];
        MontarItensProfissao();
    }

    /// <summary>Profissões ativas e, abaixo, as ocupações da CBO que ainda não têm profissão (pelo código ou pelo nome).</summary>
    private void MontarItensProfissao()
    {
        var ligadas = _profissoesCadastro.Where(p => p.OcupacaoCboId is not null).Select(p => p.OcupacaoCboId!.Value).ToHashSet();
        var nomes = _profissoesCadastro.Select(p => TextoBusca.Normalizar(p.Nome)).ToHashSet();
        var cadastradas = _profissoesCadastro.Where(x => x.Ativo).OrderBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(ItemDe);
        var daCbo = _ocupacoesCbo
            .Where(o => !ligadas.Contains(o.Codigo))
            .Select(o => (Ocupacao: o, Nome: global::Lone.Domain.Profissoes.RegrasProfissao.NomeDaOcupacao(o.Titulo)))
            .Where(x => !nomes.Contains(TextoBusca.Normalizar(x.Nome)))
            .Select(x => new ItemSeletor(PrefixoCbo + x.Ocupacao.Codigo.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.Nome, $"{x.Ocupacao.Codigo} {x.Ocupacao.CodigoFormatado}", $"CBO {x.Ocupacao.CodigoFormatado}", Grupo: 1));
        Profissao.DefinirItens([.. cadastradas, .. daCbo]);
    }

    /// <summary>Profissão criada agora pelo atalho da ficha: entra na lista e já fica escolhida.</summary>
    public void IncluirProfissao(ProfissaoDto profissao)
    {
        _profissoesCadastro = [.. _profissoesCadastro.Where(p => p.Id != profissao.Id), profissao];
        MontarItensProfissao();
        _profissaoDesconhecida = null;
        Profissao.Definir(ItemDe(profissao));
    }

    private static ItemSeletor ItemDe(ProfissaoDto p) =>
        new(p.Id.ToString(), p.Ativo ? p.Nome : p.Nome + " (desativada)",
            p.OcupacaoCboId is { } codigo ? $"{codigo} {Formatar(codigo)}" : null,
            p.OcupacaoCboId is { } c ? $"CBO {Formatar(c)}" : null);

    private static string Formatar(int codigo) => codigo.ToString("0000-00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A escolhida; sem escolha e sem texto, a gravada que não veio na lista (se houver) continua.</summary>
    private Guid? ProfissaoEscolhida() =>
        Guid.TryParse(Profissao.Chave, out var id) ? id
        : string.IsNullOrWhiteSpace(Profissao.Texto) ? _profissaoDesconhecida
        : null;

    /// <summary>Ocupação da CBO escolhida (ainda sem profissão no cadastro): a API resolve a profissão ao salvar.</summary>
    private int? OcupacaoEscolhida() =>
        Profissao.Chave is { } chave && chave.StartsWith(PrefixoCbo, StringComparison.Ordinal) &&
        int.TryParse(chave.AsSpan(PrefixoCbo.Length), System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var codigo)
            ? codigo
            : null;

    private static List<CampoPersonalizadoFormulario> MontarInformacoesAdicionais(
        IReadOnlyList<CampoPersonalizadoDto>? campos, IReadOnlyList<ValorPersonalizadoDto> valores) =>
        (campos ?? [])
            .Where(c => c.Ativo && c.Visivel) // oculto: o servidor mantém o valor gravado
            .OrderBy(c => c.Ordem)
            .Select(c => CampoPersonalizadoFormulario.Criar(c, valores.FirstOrDefault(v => v.CampoId == c.Id)))
            .ToList();

    /// <summary>Papéis na Identificação: só os que a pessoa tem ou já teve, com "Adicionar papel" para os outros.</summary>
    public PapeisDaFicha PapeisFicha { get; private set; } = new([]);

    /// <summary>A cor/raça depende do papel de funcionário: acompanha quando ele é ligado ou desligado.</summary>
    private void OuvirPapeis()
    {
        PapeisFicha = new PapeisDaFicha(Papeis);
        foreach (var papel in Papeis)
            papel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PapelOpcao.Ativo)) OnPropertyChanged(nameof(MostrarCorRaca));
            };
    }

    private void DefinirOrigem(string? origem)
    {
        if (string.IsNullOrWhiteSpace(origem))
        {
            OrigemCadastro = OpcoesPessoa.Origens[0];
            return;
        }
        if (!OpcoesPessoa.Origens.Contains(origem))
            Origens = OpcoesPessoa.Origens.Append(origem).ToArray(); // array: o Picker precisa de IList
        OrigemCadastro = origem;
    }

    // ---- Endereços ----

    public void AdicionarEndereco(EnderecoFormulario endereco)
    {
        endereco.Municipio.Fonte = _fonteMunicipios;
        endereco.DefinirCatalogo(_tiposEndereco);
        endereco.MostrarSeInativo = MostrarEnderecosInativos;
        endereco.AoRemover = () => RemoverEndereco(endereco);
        endereco.AoBuscarCep = e => ConsultaCep?.Invoke(e) ?? Task.CompletedTask;
        endereco.AoConferirCep = e => ConferenciaCep?.Invoke(e) ?? Task.CompletedTask;
        endereco.AoBuscarCepPorEndereco = e => BuscaCepPorEndereco?.Invoke(e) ?? Task.CompletedTask;
        endereco.AoConsultarOutraFonte = e => SegundaOpiniaoCep?.Invoke(e) ?? Task.CompletedTask;
        endereco.DefinirFinalidades(_finalidades);
        endereco.AoPedirPrincipal = AlternarPrincipalAsync;
        endereco.AoUsarExistente = UsarEnderecoExistente;
        endereco.AoConfirmarOutro = e => { e.OutroEnderecoConfirmado = true; AtualizarDuplicidades(); };
        endereco.AoCancelarNovo = RemoverEndereco;
        endereco.PropertyChanged += Endereco_PropertyChanged;
        endereco.Finalidades.CollectionChanged += (_, _) => AtualizarAvisosEnderecos();
        Enderecos.Add(endereco);
        OnPropertyChanged(nameof(TemEnderecosInativos));
        AtualizarDuplicidades();
    }

    /// <summary>
    /// Já gravado: fica gravado como inativo (histórico; notas e pedidos antigos apontam para ele). Novo: sai da lista.
    /// Nos dois casos, filiais que o usavam como endereço fiscal ficam sem (a lista não pode "pular" para outro).
    /// </summary>
    public void RemoverEndereco(EnderecoFormulario endereco)
    {
        foreach (var e in Estabelecimentos.Where(e => e.EnderecoFiscal == endereco))
            e.EnderecoFiscal = null;
        if (endereco.Gravado)
            endereco.Ativo = false; // perde todo principal (EnderecoFormulario.OnAtivoChanged); fica no histórico
        else
        {
            endereco.PropertyChanged -= Endereco_PropertyChanged;
            Enderecos.Remove(endereco);
        }
        OnPropertyChanged(nameof(TemEnderecosInativos));
    }

    /// <summary>Tipos de endereço do cadastro (Sede, Depósito...).</summary>
    private IReadOnlyList<TipoEnderecoDto> _tiposEndereco = [];

    /// <summary>Mostra também os endereços removidos (inativos), para consultar ou reativar.</summary>
    [ObservableProperty] private bool _mostrarEnderecosInativos;

    public bool TemEnderecosInativos => Enderecos.Any(e => !e.Ativo);

    partial void OnMostrarEnderecosInativosChanged(bool value)
    {
        foreach (var e in Enderecos) e.MostrarSeInativo = value;
    }

    /// <summary>Cadastro de finalidades de endereço (Comercial, Fiscal, Entrega...).</summary>
    private IReadOnlyList<FinalidadeEnderecoDto> _finalidades = [];

    /// <summary>Definido pela tela: pergunta ao usuário (título, mensagem, aceitar, cancelar). Sem ele, aceita.</summary>
    public Func<string, string, string, string, Task<bool>>? Confirmar { get; set; }

    private Task<bool> ConfirmarAsync(string titulo, string mensagem, string aceitar, string cancelar) =>
        Confirmar?.Invoke(titulo, mensagem, aceitar, cancelar) ?? Task.FromResult(true);

    private static readonly HashSet<string> CamposFisicos =
    [
        nameof(EnderecoFormulario.Logradouro), nameof(EnderecoFormulario.Numero), nameof(EnderecoFormulario.Complemento),
        nameof(EnderecoFormulario.Bairro), nameof(EnderecoFormulario.Cep), nameof(EnderecoFormulario.Cidade),
        nameof(EnderecoFormulario.NoExterior), nameof(EnderecoFormulario.Resumo), nameof(EnderecoFormulario.Ativo),
        nameof(EnderecoFormulario.CodigoPostal), nameof(EnderecoFormulario.CodigoPais)
    ];

    private void Endereco_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EnderecoFormulario.Ativo)) OnPropertyChanged(nameof(TemEnderecosInativos));
        if (e.PropertyName is not null && CamposFisicos.Contains(e.PropertyName)) AtualizarDuplicidades();
        else if (e.PropertyName is nameof(EnderecoFormulario.SemFinalidades) or nameof(EnderecoFormulario.RevisaoMigracao))
            AtualizarAvisosEnderecos();
    }

    /// <summary>Id da finalidade Fiscal no cadastro (pelo código, nunca por número fixo). Vazio = cadastro sem ela.</summary>
    private Guid IdFiscal => _finalidades.FirstOrDefault(f => f.Codigo == Lone.Domain.Enderecos.FinalidadesEnderecoIniciais.Fiscal)?.Id ?? Guid.Empty;

    /// <summary>
    /// Acrescenta a finalidade Fiscal (ou reativa a retirada) — NUNCA marca principal: preenchimento automático não
    /// escolhe principal (nem por ser o único endereço, o novo ou o do CNPJ). Sem principal fiscal definido, o aviso
    /// convida o usuário a definir, com a ação explícita "Tornar principal".
    /// </summary>
    private void MarcarFiscal(EnderecoFormulario endereco)
    {
        if (IdFiscal == Guid.Empty) return;
        var relacao = endereco.Finalidades.FirstOrDefault(f => f.Ativo && f.FinalidadeId == IdFiscal) ?? endereco.AdicionarFinalidade(IdFiscal);
        var temPrincipalFiscal = Enderecos.Any(e => e.Ativo && e.Finalidades.Any(f => f.Ativo && f.Principal && f.FinalidadeId == IdFiscal));
        endereco.AvisoFinalidade = relacao is null || temPrincipalFiscal ? string.Empty
            : "Este endereço foi definido como Fiscal pela consulta do CNPJ. Para usá-lo como endereço fiscal principal, " +
              "toque em \"Tornar principal\" na finalidade Fiscal.";
    }

    // ---- Principal por finalidade ----

    /// <summary>
    /// Marca/desmarca o endereço como principal da pessoa para a finalidade. Se outro endereço já é o principal dessa
    /// finalidade, pergunta antes de substituir. Nunca decide pela ordem dos endereços.
    /// </summary>
    public async Task AlternarPrincipalAsync(EnderecoFormulario endereco, FinalidadeNoEndereco finalidade)
    {
        endereco.AvisoFinalidade = string.Empty;
        if (finalidade.Principal)
        {
            finalidade.Principal = false;
            AtualizarAvisosEnderecos();
            return;
        }
        if (!endereco.Ativo || !finalidade.Ativo)
        {
            endereco.AvisoFinalidade = "Endereço ou finalidade inativa não pode ser principal.";
            return;
        }

        var atual = Enderecos
            .Where(e => !ReferenceEquals(e, endereco) && e.Ativo)
            .SelectMany(e => e.Finalidades.Where(f => f.Ativo && f.Principal && f.FinalidadeId == finalidade.FinalidadeId).Select(f => (Endereco: e, Relacao: f)))
            .FirstOrDefault();
        if (atual.Relacao is not null)
        {
            var nome = finalidade.Nome;
            if (!await ConfirmarAsync("Endereço principal",
                    $"Já existe um endereço principal para {nome}.\nEndereço atual: {atual.Endereco.Resumo}\n\n" +
                    $"Deseja definir este endereço como o principal para {nome}?",
                    "Tornar principal", "Cancelar"))
                return;
            atual.Relacao.Principal = false;
        }
        finalidade.Principal = true;
        AtualizarAvisosEnderecos();
    }

    // ---- Duplicidade de endereço físico ----

    /// <summary>
    /// Procura, para cada endereço novo, um endereço ativo igual (ou possivelmente igual) já na ficha. Igual: o novo não
    /// pode ser gravado; usa-se o existente. Possível: o usuário decide ("Usar endereço existente" ou "Continuar com outro").
    /// Igual a um endereço INATIVO gravado (não consolidado): oferece reativar o existente ou cancelar o novo — não se
    /// cria outra linha física para o mesmo lugar (a API também recusa).
    /// </summary>
    public void AtualizarDuplicidades()
    {
        foreach (var e in Enderecos)
        {
            EnderecoFormulario? igual = null;
            var possivel = false;
            if (!e.Gravado && e.Ativo)
            {
                foreach (var outro in Enderecos.Where(o => !ReferenceEquals(o, e) && o.Ativo && Enderecos.IndexOf(o) < Enderecos.IndexOf(e)))
                {
                    var s = Lone.Domain.Enderecos.DuplicidadeEndereco.Comparar(e.ParaComparacao(), outro.ParaComparacao());
                    if (s == Lone.Domain.Enderecos.SemelhancaEndereco.Igual) { igual = outro; possivel = false; break; }
                    if (s == Lone.Domain.Enderecos.SemelhancaEndereco.Possivel && igual is null && !e.OutroEnderecoConfirmado)
                    {
                        igual = outro;
                        possivel = true;
                    }
                }
                if (igual is null || possivel)
                {
                    var inativo = Enderecos.FirstOrDefault(o => o.Gravado && !o.Ativo && o.MescladoEmId is null &&
                        Lone.Domain.Enderecos.DuplicidadeEndereco.Comparar(e.ParaComparacao(), o.ParaComparacao()) == Lone.Domain.Enderecos.SemelhancaEndereco.Igual);
                    if (inativo is not null) { igual = inativo; possivel = false; }
                }
            }
            e.DuplicidadePossivel = possivel;
            e.IgualA = igual;
        }
        AtualizarAvisosEnderecos();
    }

    /// <summary>
    /// "Usar endereço existente": as finalidades do endereço novo passam para o existente (sem repetir; retirada é
    /// reativada; o principal vai junto) e o novo sai da ficha. Filiais que usavam o novo passam a usar o existente.
    /// </summary>
    public void UsarEnderecoExistente(EnderecoFormulario novo)
    {
        if (novo.IgualA is not { } existente || novo.Gravado) return;
        if (!existente.Ativo)
        {
            if (!existente.PodeReativar) return;
            existente.Ativo = true; // reativa o endereço histórico (volta sem principal); as finalidades do novo vão para ele
        }
        var jaTinha = new List<string>();
        var acrescentadas = new List<string>();
        foreach (var f in novo.Finalidades.Where(f => f.Ativo).ToList())
        {
            if (existente.Finalidades.Any(x => x.Ativo && x.FinalidadeId == f.FinalidadeId))
            {
                var ja = existente.Finalidades.First(x => x.Ativo && x.FinalidadeId == f.FinalidadeId);
                if (f.Principal) ja.Principal = true; // o principal escolhido no novo não se perde
                jaTinha.Add(f.Nome);
                continue;
            }
            var levada = existente.AdicionarFinalidade(f.FinalidadeId);
            if (levada is not null) levada.Principal = f.Principal;
            acrescentadas.Add(f.Nome);
        }
        foreach (var e in Estabelecimentos.Where(e => ReferenceEquals(e.EnderecoFiscal, novo))) e.EnderecoFiscal = existente;
        novo.PropertyChanged -= Endereco_PropertyChanged;
        Enderecos.Remove(novo);
        existente.AvisoFinalidade = string.Join(" ", new[]
        {
            acrescentadas.Count > 0 ? $"Finalidades acrescentadas: {string.Join(", ", acrescentadas)}." : null,
            jaTinha.Count > 0 ? $"Este endereço já possui: {string.Join(", ", jaTinha)}." : null
        }.OfType<string>());
        AtualizarDuplicidades();
    }

    // ---- Duplicados já gravados (consolidação assistida) ----

    /// <summary>Pares de endereços gravados que são o mesmo endereço físico (iguais ou possivelmente iguais).</summary>
    public ObservableCollection<ParDeEnderecos> DuplicadosGravados { get; } = new();
    public bool TemDuplicadosGravados => DuplicadosGravados.Count > 0;

    private readonly HashSet<(Guid, Guid)> _mantidosSeparados = new();

    // ---- Revisão das finalidades (marca vinda da migração) ----

    /// <summary>Somente leitura: a migração deixou pendências (a API desliga quando todas forem resolvidas).</summary>
    public bool RevisarFinalidadesEndereco { get; private set; }

    /// <summary>Motivo detalhado por finalidade (ex.: "Entrega: existem 2 endereços e nenhum foi definido como principal.").</summary>
    public ObservableCollection<string> PendenciasRevisao { get; } = new();
    public bool TemPendenciasRevisao => PendenciasRevisao.Count > 0;

    /// <summary>Refaz os avisos que dependem de todos os endereços (duplicados gravados e pendências da revisão).</summary>
    private void AtualizarAvisosEnderecos()
    {
        DuplicadosGravados.Clear();
        var gravados = Enderecos.Where(e => e.Gravado && e.Ativo).ToList();
        for (var i = 0; i < gravados.Count; i++)
            for (var j = i + 1; j < gravados.Count; j++)
            {
                var s = Lone.Domain.Enderecos.DuplicidadeEndereco.Comparar(gravados[i].ParaComparacao(), gravados[j].ParaComparacao());
                if (s == Lone.Domain.Enderecos.SemelhancaEndereco.Diferente || _mantidosSeparados.Contains((gravados[i].Id, gravados[j].Id))) continue;
                DuplicadosGravados.Add(new ParDeEnderecos(gravados[i], gravados[j], s == Lone.Domain.Enderecos.SemelhancaEndereco.Possivel)
                {
                    AoConsolidar = ConsolidarAsync,
                    AoManterSeparados = par => { _mantidosSeparados.Add((par.A.Id, par.B.Id)); AtualizarAvisosEnderecos(); }
                });
            }
        OnPropertyChanged(nameof(TemDuplicadosGravados));

        // Mesmas regras e textos da API (RegrasFinalidadeEndereco): ambiguidade de principal e motivos gravados pela
        // migração. Endereço sem finalidade, por si só, não é pendência.
        PendenciasRevisao.Clear();
        if (RevisarFinalidadesEndereco)
        {
            var ativos = Enderecos.Where(e => e.Ativo).ToList();
            foreach (var grupo in ativos.SelectMany(e => e.Finalidades.Where(f => f.Ativo).Select(f => (Endereco: e, Relacao: f)))
                         .GroupBy(x => x.Relacao.FinalidadeId)
                         .Where(g => g.Count() > 1 && !g.Any(x => x.Relacao.Principal)))
                PendenciasRevisao.Add(Lone.Domain.Enderecos.RegrasFinalidadeEndereco.TextoAmbiguidade(grupo.First().Relacao.Nome, grupo.Count()));
            foreach (var e in ativos)
            {
                var motivo = e.RevisaoMigracao;
                if (!e.SemFinalidades) motivo &= ~MotivoRevisaoEndereco.AntigoPrincipalSemFinalidade; // já resolvido
                foreach (var texto in Lone.Domain.Enderecos.RegrasFinalidadeEndereco.TextosMotivo(e.Resumo, motivo))
                    PendenciasRevisao.Add(texto);
            }
        }
        OnPropertyChanged(nameof(TemPendenciasRevisao));
    }

    /// <summary>
    /// Definido pela tela: pede ao SERVIDOR a consolidação (só a intenção: origem → destino, com a versão aberta) e
    /// recarrega a ficha com o resultado. O servidor confere tudo e decide as finalidades resultantes.
    /// </summary>
    public Func<Guid, Guid, Task>? ConsolidarNoServidor { get; set; }

    /// <summary>Definido pela tela: há alterações não salvas (a consolidação exige a ficha salva ou descartada).</summary>
    public Func<bool>? TemAlteracoesNaoSalvas { get; set; }

    /// <summary>
    /// Consolida o duplicado no endereço mantido: mostra antes como deve ficar (prévia calculada aqui; quem decide é o
    /// servidor, a partir do que está gravado) e, confirmado, pede a operação ao servidor. A ficha não altera nada por
    /// conta própria: nada de marcar inativo, MescladoEmId ou finalidades aqui.
    /// </summary>
    public async Task ConsolidarAsync(EnderecoFormulario mantido, EnderecoFormulario duplicado)
    {
        if (ConsolidarNoServidor is null || !mantido.Gravado || !duplicado.Gravado) return;
        if (TemAlteracoesNaoSalvas?.Invoke() == true)
        {
            mantido.AvisoFinalidade = "Salve ou descarte as alterações da ficha antes de consolidar endereços.";
            return;
        }

        var resultado = new List<string>();
        foreach (var grupo in mantido.Finalidades.Where(f => f.Ativo).Concat(duplicado.Finalidades.Where(f => f.Ativo))
                     .GroupBy(f => f.FinalidadeId))
            resultado.Add(grupo.First().Nome + (grupo.Any(f => f.Principal) ? " (principal)" : string.Empty));

        var diferencas = new List<string>();
        if (!string.Equals(mantido.Descricao, duplicado.Descricao, StringComparison.Ordinal) && duplicado.Descricao.Length > 0)
            diferencas.Add($"descrição \"{duplicado.Descricao}\"");
        if (mantido.Tipo?.Valor != duplicado.Tipo?.Valor && duplicado.Tipo?.Valor is not null)
            diferencas.Add($"tipo \"{duplicado.Tipo.Texto}\"");
        if (!string.Equals(mantido.Observacoes, duplicado.Observacoes, StringComparison.Ordinal) && duplicado.Observacoes.Length > 0)
            diferencas.Add($"observações \"{duplicado.Observacoes}\"");

        var mensagem =
            $"Endereço mantido: {mantido.Resumo}\n" +
            $"Endereço consolidado (fica inativo, no histórico): {duplicado.Resumo}\n\n" +
            $"Finalidades depois de consolidar: {(resultado.Count > 0 ? string.Join(", ", resultado) : "nenhuma")}\n" +
            (diferencas.Count > 0 ? $"Do endereço consolidado não passam: {string.Join("; ", diferencas)} (ficam registrados nele).\n" : string.Empty) +
            (Estabelecimentos.Any(e => ReferenceEquals(e.EnderecoFiscal, duplicado)) ? "Filiais que usavam o endereço consolidado passam a usar o mantido.\n" : string.Empty) +
            "\nA consolidação é gravada na hora, conferida pelo servidor com o que está salvo.";
        if (!await ConfirmarAsync("Consolidar endereços", mensagem, "Consolidar", "Cancelar")) return;

        await ConsolidarNoServidor(duplicado.Id, mantido.Id);
    }

    // ---- Documento principal completo: confere se já está em outro cadastro e, na PJ nova, consulta a Receita ----

    /// <summary>Definido pela tela: chamado quando o CPF (PF) ou o CNPJ do principal (PJ) fica completo, válido e diferente do último.</summary>
    public Func<Task>? AoCompletarDocumento { get; set; }

    private string _documentoConferido = string.Empty;

    /// <summary>CPF (PF) ou CNPJ do principal (PJ), completo e válido, sem máscara; vazio se incompleto. Estrangeiro: vazio.</summary>
    public string DocumentoCompleto => EhJuridica
        ? (Estabelecimentos.Count > 0 && DocumentoFiscal.CnpjValido(Principal.Cnpj) ? DocumentoFiscal.Normalizar(Principal.Cnpj) : string.Empty)
        : EhFisica && DocumentoFiscal.CpfValido(Documento) ? DocumentoFiscal.Normalizar(Documento) : string.Empty;

    private string ChaveDocumento() => DocumentoCompleto is { Length: > 0 } d ? $"{Natureza.Valor}:{d}" : string.Empty;

    /// <summary>
    /// Cadastro novo que nasce relacionado a outro (Fase 2a-3, E9): o relacionamento vai junto na gravação. Nulo = normal.
    /// </summary>
    public IncluirRelacionamentoRequisicao? RelacionarAoCriar { get; private set; }

    /// <summary>"Ao salvar, esta pessoa será registrada como Contato de ABC Ltda." (vazio = cadastro comum).</summary>
    public string AvisoRelacionarAoCriar { get; private set; } = string.Empty;

    public bool TemRelacionarAoCriar => AvisoRelacionarAoCriar.Length > 0;

    public void NascerRelacionada(IncluirRelacionamentoRequisicao pedido, string tipo, string outraPessoa)
    {
        if (!Nova) return;
        RelacionarAoCriar = pedido;
        AvisoRelacionarAoCriar = $"Ao salvar, esta pessoa será registrada como \"{tipo}\" {outraPessoa}.";
        OnPropertyChanged(nameof(AvisoRelacionarAoCriar));
        OnPropertyChanged(nameof(TemRelacionarAoCriar));
    }

    /// <summary>"Esta empresa já está cadastrada: 000012 - ..." (vazio = sem aviso).</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemAvisoDocumentoEmUso), nameof(PodeAbrirDocumentoEmUso))]
    private string _avisoDocumentoEmUso = string.Empty;

    public bool TemAvisoDocumentoEmUso => AvisoDocumentoEmUso.Length > 0;

    /// <summary>"Abrir cadastro" só quando o outro cadastro está no alcance do usuário (E5: fora dele não vem o Id).</summary>
    public bool PodeAbrirDocumentoEmUso => TemAvisoDocumentoEmUso && DocumentoEmUsoId is not null;

    /// <summary>O outro cadastro com o mesmo documento (para "Abrir cadastro").</summary>
    public Guid? DocumentoEmUsoId { get; private set; }

    partial void OnDocumentoChanged(string value) => ConferirDocumento();

    private void ConferirDocumento()
    {
        var chave = ChaveDocumento();
        if (chave == _documentoConferido) return;
        _documentoConferido = chave;
        DocumentoEmUsoId = null;
        AvisoDocumentoEmUso = string.Empty;
        if (chave.Length > 0) _ = AoCompletarDocumento?.Invoke();
    }

    /// <summary>Resposta da API. A gravação continua recusando o duplicado; aqui é só o aviso antecipado.</summary>
    public void DefinirDocumentoEmUso(DocumentoEmUsoResposta resposta)
    {
        DocumentoEmUsoId = resposta.EmUso && !resposta.ForaDoAlcance ? resposta.Id : null;
        AvisoDocumentoEmUso = !resposta.EmUso ? string.Empty
            : resposta.ForaDoAlcance
                ? (EhJuridica ? "Esta empresa (mesma raiz de CNPJ) já está cadastrada" : "Este CPF já está cadastrado") +
                  ", fora do seu alcance. Peça acesso ao responsável pelo cliente."
            : EhJuridica
                ? $"Esta empresa (mesma raiz de CNPJ) já está cadastrada: {resposta.Codigo:000000} - {resposta.Nome}. " +
                  "Para uma filial, abra esse cadastro e adicione o CNPJ como estabelecimento."
                : $"Este CPF já está cadastrado: {resposta.Codigo:000000} - {resposta.Nome}.";
    }

    // ---- Estabelecimentos ----

    public EstabelecimentoFormulario AdicionarEstabelecimento()
    {
        var estabelecimento = new EstabelecimentoFormulario(Enderecos);
        Incluir(estabelecimento);
        MarcarPrincipal();
        return estabelecimento;
    }

    /// <summary>
    /// Filial gravada é desativada (continua na lista e no banco, com histórico, documentos e referências fiscais);
    /// filial nova, ainda não gravada, sai da lista. O principal não é removido.
    /// </summary>
    public void RemoverEstabelecimento(EstabelecimentoFormulario estabelecimento)
    {
        if (Estabelecimentos.Count <= 1 || ReferenceEquals(estabelecimento, Principal)) return;
        if (estabelecimento.Gravado)
        {
            estabelecimento.Ativo = false;
        }
        else
        {
            Estabelecimentos.Remove(estabelecimento);
            MarcarPrincipal();
        }
        AtualizarCabecalho();
    }

    /// <summary>O principal é sempre o primeiro da lista.</summary>
    public void TornarPrincipal(EstabelecimentoFormulario estabelecimento)
    {
        var indice = Estabelecimentos.IndexOf(estabelecimento);
        if (indice <= 0) return;
        Estabelecimentos.Move(indice, 0);
        MarcarPrincipal();
        OnPropertyChanged(nameof(Principal));
        AtualizarCabecalho();
    }

    private void Incluir(EstabelecimentoFormulario estabelecimento)
    {
        estabelecimento.DaPessoaJuridica = EhJuridica;
        estabelecimento.DaPessoaEstrangeira = EhEstrangeiro;
        estabelecimento.AoRemover = () => RemoverEstabelecimento(estabelecimento);
        estabelecimento.AoTornarPrincipal = () => TornarPrincipal(estabelecimento);
        estabelecimento.AoConsultarCnpj = e => ConsultaCnpj?.Invoke(e) ?? Task.CompletedTask;
        estabelecimento.PropertyChanged += Estabelecimento_PropertyChanged;
        Estabelecimentos.Add(estabelecimento);
    }

    /// <summary>Nome fantasia, CNPJ e situação dos estabelecimentos aparecem no cabeçalho.</summary>
    private void Estabelecimento_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EstabelecimentoFormulario.NomeFantasia) or nameof(EstabelecimentoFormulario.Cnpj)
            or nameof(EstabelecimentoFormulario.Ativo))
            AtualizarCabecalho();
        if (e.PropertyName == nameof(EstabelecimentoFormulario.Cnpj) && Estabelecimentos.Count > 0 && ReferenceEquals(sender, Principal))
            ConferirDocumento();
    }

    /// <summary>Papéis, etiquetas e estabelecimentos incluídos depois mudam o cabeçalho.</summary>
    private void OuvirCabecalho()
    {
        foreach (var papel in Papeis)
            papel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PapelOpcao.Ativo)) AtualizarCabecalho();
            };
        Etiquetas.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EtiquetasFormulario.Resumo)) AtualizarCabecalho();
        };
        Estabelecimentos.CollectionChanged += (_, _) => AtualizarCabecalho();
    }

    private void MarcarPrincipal()
    {
        for (var i = 0; i < Estabelecimentos.Count; i++)
            Estabelecimentos[i].EhPrincipal = i == 0;
    }

    // ---- Contatos e documentos ----

    public void AdicionarMeio(MeioContatoFormulario meio)
    {
        meio.DefinirCatalogo(_tiposMeio);
        meio.MostrarSeInativo = MostrarMeiosInativos;
        meio.AoRemover = () =>
        {
            // Já gravado: fica gravado como inativo (histórico e busca por número antigo). Novo: sai da lista.
            if (meio.Gravado) meio.Ativo = false;
            else MeiosContato.Remove(meio);
            OnPropertyChanged(nameof(TemMeiosInativos));
            AvisarListasDeMeios();
        };
        meio.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MeioContatoFormulario.Ativo)) OnPropertyChanged(nameof(TemMeiosInativos));
            if (e.PropertyName is nameof(MeioContatoFormulario.NaListaTelefones) or nameof(MeioContatoFormulario.NaListaEmails))
                AvisarListasDeMeios();
        };
        MeiosContato.Add(meio);
        OnPropertyChanged(nameof(TemMeiosInativos));
        AvisarListasDeMeios();
    }

    /// <summary>Aba Contatos: a mesma coleção aparece em "Telefones" e "E-mails" (só apresentação).</summary>
    public bool SemTelefones => !MeiosContato.Any(m => m.NaListaTelefones);
    public bool SemEmails => !MeiosContato.Any(m => m.NaListaEmails);

    private void AvisarListasDeMeios()
    {
        OnPropertyChanged(nameof(SemTelefones));
        OnPropertyChanged(nameof(SemEmails));
    }

    /// <summary>Novo telefone ou e-mail já com o tipo; vira principal se ainda não houver outro ativo do mesmo tipo.</summary>
    public MeioContatoFormulario NovoMeio(TipoContato tipo)
    {
        var meio = new MeioContatoFormulario
        {
            Tipo = Opcao.De(OpcoesPessoa.TiposContato, tipo),
            Principal = !MeiosContato.Any(m => m.Ativo && m.Tipo.Valor == tipo)
        };
        AdicionarMeio(meio);
        return meio;
    }

    /// <summary>Tipos de telefone/e-mail do cadastro (Comercial, Residencial...).</summary>
    private IReadOnlyList<TipoMeioContatoDto> _tiposMeio = [];

    /// <summary>Mostra também os telefones/e-mails removidos (inativos), para consultar ou reativar.</summary>
    [ObservableProperty] private bool _mostrarMeiosInativos;

    public bool TemMeiosInativos => MeiosContato.Any(m => !m.Ativo);

    partial void OnMostrarMeiosInativosChanged(bool value)
    {
        foreach (var m in MeiosContato) m.MostrarSeInativo = value;
    }

    /// <summary>
    /// Pessoa de contato na lista. Remover: o já gravado fica inativo (mesmo registro, para o histórico); o que ainda não
    /// foi gravado sai da lista e nada vai para o banco (P0).
    /// </summary>
    public void AdicionarContato(ContatoFormulario contato)
    {
        contato.MostrarSeInativo = MostrarContatosInativos;
        contato.AoRemover = () =>
        {
            if (contato.Gravado) contato.Ativo = false;
            else Contatos.Remove(contato);
            OnPropertyChanged(nameof(TemContatosInativos));
        };
        contato.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ContatoFormulario.Ativo)) OnPropertyChanged(nameof(TemContatosInativos));
        };
        Contatos.Add(contato);
        OnPropertyChanged(nameof(TemContatosInativos));
    }

    /// <summary>Mostra também as pessoas de contato removidas (inativas), para consultar ou reativar.</summary>
    [ObservableProperty] private bool _mostrarContatosInativos;

    public bool TemContatosInativos => Contatos.Any(c => !c.Ativo);

    partial void OnMostrarContatosInativosChanged(bool value)
    {
        foreach (var c in Contatos) c.MostrarSeInativo = value;
    }

    public void AdicionarDocumento(DocumentoFormulario documento)
    {
        documento.DefinirNatureza(Natureza.Valor);
        documento.DefinirCatalogo(_tiposDocumento);
        documento.DefinirCampos(_camposDocumento);
        documento.Acoes = AcoesAnexos;
        documento.MostrarSeInativo = MostrarDocumentosInativos;
        documento.AoRemover = () =>
        {
            // Já gravado: fica gravado como inativo (histórico, consulta por número antigo). Novo: sai da lista.
            if (documento.Gravado) documento.Ativo = false;
            else Documentos.Remove(documento);
            AvisarDocumentos();
        };
        documento.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DocumentoFormulario.Ativo) or nameof(DocumentoFormulario.AvisoValidade)
                or nameof(DocumentoFormulario.TemAnexosInativos)) AvisarDocumentos();
        };
        Documentos.Add(documento);
        AvisarDocumentos();
    }

    // ---- Comercial (perfil, condição, exceções com vigência e carteira de clientes) ----

    public ObservableCollection<ExcecaoComercialFormulario> Excecoes { get; } = new();
    /// <summary>Todos os vínculos da carteira (o que vai para a API).</summary>
    public ObservableCollection<CarteiraFormulario> Carteira { get; } = new();

    /// <summary>Na tela: os vigentes, os a começar e os novos (os novos primeiro).</summary>
    public ObservableCollection<CarteiraFormulario> CarteiraAtual { get; } = new();

    /// <summary>Na tela, recolhido: os que já estavam encerrados ou desativados quando a ficha abriu (mais recentes primeiro).</summary>
    public ObservableCollection<CarteiraFormulario> CarteiraHistorico { get; } = new();

    [ObservableProperty][NotifyPropertyChangedFor(nameof(TextoHistoricoCarteira))] private bool _mostrarHistoricoCarteira;

    public string TextoHistoricoCarteira => (MostrarHistoricoCarteira ? "Ocultar histórico" : "Mostrar histórico") + $" ({CarteiraHistorico.Count})";
    public bool TemHistoricoCarteira => CarteiraHistorico.Count > 0;
    public bool SemCarteiraAtual => CarteiraAtual.Count == 0;

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void AlternarHistoricoCarteira() => MostrarHistoricoCarteira = !MostrarHistoricoCarteira;

    /// <summary>Separa a carteira em atual e histórico (ao abrir, incluir, trocar ou remover; não enquanto se digita).</summary>
    private void OrganizarCarteira()
    {
        CarteiraAtual.Clear();
        foreach (var c in Carteira.Where(c => !c.Gravada)) CarteiraAtual.Add(c);
        foreach (var c in Carteira.Where(c => c.Gravada && !c.NoHistorico).OrderByDescending(c => c.ParaDto().InicioEm)) CarteiraAtual.Add(c);
        CarteiraHistorico.Clear();
        foreach (var c in Carteira.Where(c => c.NoHistorico).OrderByDescending(c => c.ParaDto().InicioEm)) CarteiraHistorico.Add(c);
        OnPropertyChanged(nameof(TextoHistoricoCarteira));
        OnPropertyChanged(nameof(TemHistoricoCarteira));
        OnPropertyChanged(nameof(SemCarteiraAtual));
    }

    private OpcoesComercial? _opcoesComercial;

    public bool OpcoesComercialCarregadas => _opcoesComercial is not null;

    /// <summary>Chamado pela tela quando a aba "Comercial" abre e as opções chegam.</summary>
    public void DefinirOpcoesComercial(ComercialOpcoesDto dto)
    {
        _opcoesComercial = new OpcoesComercial(dto);
        ContaCliente.DefinirOpcoes(_opcoesComercial);
        ContaFornecedor.DefinirOpcoes(_opcoesComercial);
        foreach (var e in Excecoes) e.DefinirOpcoes(_opcoesComercial);
        foreach (var c in Carteira) c.DefinirOpcoes(_opcoesComercial);
        OnPropertyChanged(nameof(OpcoesComercialCarregadas));
        OnPropertyChanged(nameof(ResumoComercial));
    }

    public void AdicionarExcecao(ExcecaoComercialFormulario excecao)
    {
        if (_opcoesComercial is not null) excecao.DefinirOpcoes(_opcoesComercial);
        excecao.AoRemover = () =>
        {
            if (!excecao.Gravada) Excecoes.Remove(excecao); // gravada é histórico: encerra pelo fim
        };
        Excecoes.Insert(0, excecao);
    }

    public void AdicionarCarteira(CarteiraFormulario carteira)
    {
        carteira.AoRemover = () =>
        {
            if (carteira.Gravada)
            {
                carteira.Ativo = false; // gravado por engano: fica inativo (histórico)
                return;
            }
            carteira.Substitui?.DesfazerTroca(); // a troca não vai mais acontecer: o anterior volta como estava
            Carteira.Remove(carteira);
            OrganizarCarteira();
        };
        carteira.AoTrocar = () => TrocarCarteira(carteira);
        if (Carteira.Contains(carteira)) return;
        Carteira.Insert(0, carteira);
        OrganizarCarteira();
    }

    /// <summary>
    /// "Trocar": um vínculo novo no lugar do gravado (mesmo papel, empresa e crédito; a pessoa é escolhida), e o gravado
    /// fica até a véspera do início do novo. Nada é apagado; desfazer = remover o novo.
    /// </summary>
    public CarteiraFormulario? TrocarCarteira(CarteiraFormulario atual)
    {
        if (!atual.PodeTrocar) return null;
        var novo = atual.IniciarTroca(_opcoesComercial);
        AdicionarCarteira(novo);
        return novo;
    }

    public void NovaExcecao() => AdicionarExcecao(ExcecaoComercialFormulario.Nova());

    /// <summary>
    /// "Adicionar papel": vínculo novo no primeiro papel ativo (na ordem do cadastro) que ainda não tem ninguém vigente
    /// nesta ficha; se todos têm, o responsável da conta. Para substituir quem está, o caminho é "Trocar".
    /// </summary>
    public void NovaCarteira()
    {
        var ocupados = Carteira.Where(c => c.Ativo && !c.NoHistorico).Select(c => c.Tipo.Valor).ToHashSet();
        var livre = _opcoesComercial?.Dados.TiposCarteira.Where(t => t.Ativo).OrderBy(t => t.Ordem)
            .FirstOrDefault(t => !ocupados.Contains(t.Id))?.Id;
        AdicionarCarteira(CarteiraFormulario.Nova(_opcoesComercial, livre));
    }

    /// <summary>Vínculos novos da carteira que entram no lugar de um vigente (a ficha confirma antes de gravar).</summary>
    public List<SubstituicaoVendedor> SubstituicoesDeVendedor() => SubstituicaoVendedor.Planejar([.. Carteira], _opcoesComercial);

    /// <summary>
    /// O que vale hoje para a conta padrão: exceção vigente → perfil → conta (a API usa a mesma ordem).
    /// Vazio enquanto as opções não chegam.
    /// </summary>
    public string ResumoComercial
    {
        get
        {
            if (_opcoesComercial is not { } opcoes) return string.Empty;
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var excecao = Excecoes.Select(e => e.ParaDto())
                .Where(e => e.EmpresaId is null && e.InicioEm <= hoje && (e.FimEm is null || e.FimEm >= hoje))
                .OrderByDescending(e => e.InicioEm).FirstOrDefault();
            var perfil = opcoes.Dados.Perfis.FirstOrDefault(p => p.Id == ContaCliente.PerfilId);
            var conta = ContaCliente.ParaDto();
            var limite = excecao?.LimiteCredito ?? perfil?.LimiteCredito ?? conta.LimiteCredito;
            var desconto = excecao?.DescontoMaximo ?? perfil?.DescontoMaximo ?? conta.DescontoMaximo;
            var condicaoId = excecao?.CondicaoPagamentoId ?? perfil?.CondicaoPagamentoId ?? conta.CondicaoPagamentoId;
            var condicao = opcoes.Dados.Condicoes.FirstOrDefault(c => c.Id == condicaoId)?.Nome ?? conta.CondicaoPagamento;
            return string.Join(" · ", new[]
            {
                "Em vigor hoje: limite " + (limite is { } l ? l.ToString("C", TextoTela.Brasil) : "sem limite"),
                "desconto máximo " + (desconto is { } d ? d.ToString("0.##", TextoTela.Brasil) + "%" : "—"),
                "condição " + (condicao ?? "—"),
                excecao is null ? string.Empty : "(com exceção vigente)"
            }.Where(t => t.Length > 0));
        }
    }

    // ---- Colaborador (vínculos com as empresas do grupo e lotações) ----

    public ObservableCollection<VinculoFormulario> Vinculos { get; } = new();

    /// <summary>O usuário não tem a permissão de colaborador: a aba não aparece e a API mantém os dados gravados.</summary>
    public bool ColaboradorOculto { get; private set; }

    /// <summary>Aba "Colaborador": pessoa física com o papel Funcionário ou com vínculo gravado.</summary>
    public bool TemColaborador => EhFisica && !ColaboradorOculto &&
                                  (Vinculos.Count > 0 || Papeis.Any(p => p.Papel == TipoPapel.Funcionario && p.Ativo));

    private OpcoesColaborador? _opcoesColaborador;

    public bool OpcoesColaboradorCarregadas => _opcoesColaborador is not null;
    public bool AguardandoOpcoesColaborador => !OpcoesColaboradorCarregadas;

    /// <summary>Chamado pela tela quando a aba abre e as opções chegam.</summary>
    public void DefinirOpcoesColaborador(ColaboradorOpcoesDto dto)
    {
        _opcoesColaborador = new OpcoesColaborador(dto);
        foreach (var v in Vinculos) v.DefinirOpcoes(_opcoesColaborador);
        OnPropertyChanged(nameof(OpcoesColaboradorCarregadas));
        OnPropertyChanged(nameof(AguardandoOpcoesColaborador));
    }

    public void AdicionarVinculo(VinculoFormulario vinculo)
    {
        if (_opcoesColaborador is not null) vinculo.DefinirOpcoes(_opcoesColaborador);
        vinculo.AoRemover = () =>
        {
            if (!vinculo.Gravado) Vinculos.Remove(vinculo); // vínculo gravado não sai: o desligamento o encerra
        };
        Vinculos.Add(vinculo);
    }

    public void NovoVinculo() => AdicionarVinculo(VinculoFormulario.Novo(_opcoesColaborador));

    /// <summary>Campos personalizados dos documentos (cada um vale para um tipo de documento).</summary>
    private IReadOnlyList<CampoPersonalizadoDto> _camposDocumento = [];

    /// <summary>Ações de anexos (enviar, abrir, remover), ligadas pela tela de pessoas.</summary>
    public AcoesAnexos AcoesAnexos { get; } = new();

    /// <summary>Tipos de documento do cadastro (RG, CNH, Alvará...), com validade obrigatória e dias de aviso.</summary>
    private IReadOnlyList<TipoDocumentoDto> _tiposDocumento = [];

    /// <summary>Mostra também os documentos removidos (inativos), para consultar ou reativar.</summary>
    [ObservableProperty] private bool _mostrarDocumentosInativos;

    /// <summary>
    /// Há o que mostrar em "Mostrar inativos" na aba Documentos: documento removido ou anexo removido de qualquer documento
    /// (o anexo removido continua guardado e só aparece por ali, mesmo com todos os documentos ativos).
    /// </summary>
    public bool TemDocumentosInativos => Documentos.Any(d => !d.Ativo || d.TemAnexosInativos);

    partial void OnMostrarDocumentosInativosChanged(bool value)
    {
        foreach (var d in Documentos) d.MostrarSeInativo = value;
    }

    /// <summary>Resumo dos vencimentos dos documentos ativos (ex.: "1 documento vencido, 2 vencem em breve."). Vazio = nada a avisar.</summary>
    public string AvisoDocumentos
    {
        get
        {
            var vencidos = Documentos.Count(d => d.Vencido);
            var emBreve = Documentos.Count(d => d.VenceEmBreve);
            var partes = new List<string>();
            if (vencidos > 0) partes.Add(vencidos == 1 ? "1 documento vencido" : $"{vencidos} documentos vencidos");
            if (emBreve > 0) partes.Add(emBreve == 1 ? "1 vence em breve" : $"{emBreve} vencem em breve");
            return partes.Count == 0 ? string.Empty : string.Join(", ", partes) + ".";
        }
    }

    public bool TemAvisoDocumentos => AvisoDocumentos.Length > 0;

    private void AvisarDocumentos()
    {
        OnPropertyChanged(nameof(TemDocumentosInativos));
        OnPropertyChanged(nameof(AvisoDocumentos));
        OnPropertyChanged(nameof(TemAvisoDocumentos));
    }

    // ---- Consulta de CNPJ ----

    /// <summary>
    /// Endereço existente que recebe o endereço do CNPJ (nulo = criar um novo): o principal fiscal definido pelo
    /// usuário; senão um ativo que é o mesmo lugar físico (igual ou possível); senão um ativo ainda sem logradouro.
    /// <paramref name="evitar"/>: endereço que não pode receber (o da consulta anterior que o usuário alterou e fica).
    /// </summary>
    private EnderecoFormulario? EnderecoParaCnpj(DadosCnpj d, EnderecoFormulario? evitar = null)
    {
        var ativos = Enderecos.Where(e => e.Ativo && !ReferenceEquals(e, evitar)).ToList();
        var principalFiscal = ativos.FirstOrDefault(e => e.Finalidades.Any(f => f.Ativo && f.Principal && f.FinalidadeId == IdFiscal));
        if (principalFiscal is not null) return principalFiscal;

        var doCnpj = new EnderecoFormulario();
        doCnpj.AplicarCnpj(d);
        var comparacao = doCnpj.ParaComparacao();
        return ativos.FirstOrDefault(e => Lone.Domain.Enderecos.DuplicidadeEndereco.Comparar(e.ParaComparacao(), comparacao)
                                          != Lone.Domain.Enderecos.SemelhancaEndereco.Diferente)
               ?? ativos.FirstOrDefault(e => string.IsNullOrWhiteSpace(e.Logradouro));
    }

    /// <summary>
    /// Preenche o estabelecimento (e, no principal, a razão social e o endereço). O usuário confere e salva. Num cadastro já
    /// gravado, o que a Receita traz de diferente de um valor que já existe não é trocado: vai para <see cref="ConferenciaReceita"/>
    /// (o usuário marca o que trocar). O que foi preenchido fica marcado como vindo da Receita (destaque).
    /// Ficha nova que já teve uma consulta no principal (outro CNPJ, ou o mesmo de novo): <see cref="SubstituirConsultaAnterior"/>.
    /// </summary>
    public void AplicarCnpj(EstabelecimentoFormulario estabelecimento, DadosCnpj d)
    {
        if (Nova && ReferenceEquals(estabelecimento, Principal) && ConsultaAplicada is not null)
        {
            SubstituirConsultaAnterior(d);
            return;
        }
        Aplicar(estabelecimento, d, new AplicacaoReceita(conferir: Existente), destino: null, enderecoCriado: null, evitar: null,
            trocarSocios: false);
    }

    private void Aplicar(EstabelecimentoFormulario estabelecimento, DadosCnpj d, AplicacaoReceita r, EnderecoFormulario? destino,
        bool? enderecoCriado, EnderecoFormulario? evitar, bool trocarSocios)
    {
        estabelecimento.AplicarCnpj(d, r);

        EnderecoFormulario? endereco;
        var principal = ReferenceEquals(estabelecimento, Principal);
        var criado = false;
        if (principal)
        {
            r.Valor(new(CamposFichaPessoa.Nome, null), "Razão social", Nome, d.RazaoSocial, v =>
            {
                Nome = v;
                _razaoSocialConsultada = v;
            });
            // Onde entra o endereço do CNPJ: o principal fiscal já definido pelo usuário; senão um endereço ativo que é o
            // mesmo lugar físico; senão um endereço ainda em branco; senão um novo. Nunca sobrescreve outro lugar (ex.: o
            // residencial) só por ser o único endereço, e nunca marca principal.
            endereco = destino ?? EnderecoParaCnpj(d, evitar);
            criado = enderecoCriado ?? (endereco is null || string.IsNullOrWhiteSpace(endereco.Logradouro));
            if (endereco is null)
            {
                endereco = new EnderecoFormulario();
                AdicionarEndereco(endereco);
            }
            MarcarFiscal(endereco);
        }
        else
        {
            // Filial: endereço próprio, usado como endereço fiscal dela.
            endereco = estabelecimento.EnderecoFiscal;
            if (endereco is null)
            {
                endereco = new EnderecoFormulario { Descricao = "Filial " + DocumentoFiscal.Formatar(d.Cnpj) };
                AdicionarEndereco(endereco);
                MarcarFiscal(endereco); // o principal fiscal é escolhido pelo usuário
                estabelecimento.EnderecoFiscal = endereco;
            }
        }
        endereco.AplicarCnpj(d, r);

        var meios = new List<MeioContatoFormulario>();
        if (AdicionarMeioSeNovo(TipoContato.Telefone, d.Telefone) is { } telefone)
        {
            r.Marcar(new(CamposFichaPessoa.MeioContatoValor, telefone.Id));
            meios.Add(telefone);
        }
        if (AdicionarMeioSeNovo(TipoContato.Email, d.Email) is { } email)
        {
            r.Marcar(new(CamposFichaPessoa.MeioContatoValor, email.Id));
            meios.Add(email);
        }

        // Dados da empresa são da raiz do CNPJ: valem para a pessoa, venha a consulta da matriz ou de uma filial.
        if (principal)
            r.Valor(new(CamposFichaPessoa.DataAbertura, null), "Data de abertura", DataAbertura, TextoTela.Data(d.DataAbertura), v => DataAbertura = v);
        r.Valor(new(CamposFichaPessoa.Porte, null), "Porte", Porte, d.Porte, v => Porte = v);
        r.Valor(new(CamposFichaPessoa.CapitalSocial, null), "Capital social (R$)", CapitalSocial, TextoTela.Decimal(d.CapitalSocial), v => CapitalSocial = v);
        if (trocarSocios)
        {
            // Troca de empresa na ficha nova: os sócios da consulta anterior nunca foram gravados; a lista é a da nova.
            Socios.Clear();
            foreach (var socio in d.Socios) Socios.Add(socio);
            AvisoSocios = string.Empty;
            OnPropertyChanged(nameof(TemSocios));
        }
        else if (d.Socios.Count > 0)
            CasarSocios(d.Socios);

        RegistrarOrigemReceita(r.Aplicados);
        ConferenciaReceita.Abrir(r.Conflitos, d.Fonte);

        // Ficha nova: guarda o que esta consulta pôs, para uma nova consulta no principal trocar exatamente isso (§3.1).
        if (Nova && principal)
            ConsultaAplicada = new ConsultaCnpjAplicada(
                DocumentoFiscal.Normalizar(d.Cnpj), d.RazaoSocial.Trim(), endereco.Id, criado, endereco.Localizacao(),
                meios.Select(m => new MeioDaConsulta(m.Id, m.Valor, m.Descricao)).ToList());
    }

    // ---- Troca de empresa numa ficha nova (nova consulta de CNPJ no principal) ----

    /// <summary>Telefone ou e-mail que a consulta criou, com o valor e a descrição que ela pôs.</summary>
    public sealed record MeioDaConsulta(Guid Id, string Valor, string Descricao);

    /// <summary>
    /// O que a última consulta de CNPJ do principal pôs nesta ficha nova (só em memória; some ao gravar, descartar ou trocar a
    /// natureza). <paramref name="EnderecoCriado"/>: o endereço nasceu da consulta ou estava sem logradouro (o lugar é todo
    /// dela); falso: já existia com logradouro e só recebeu os campos.
    /// </summary>
    public sealed record ConsultaCnpjAplicada(string Cnpj, string RazaoSocial, Guid EnderecoId, bool EnderecoCriado,
        IReadOnlyList<string> Localizacao, IReadOnlyList<MeioDaConsulta> Meios);

    /// <summary>Última consulta de CNPJ aplicada no principal desta ficha nova (nulo: nenhuma, ou ficha gravada).</summary>
    public ConsultaCnpjAplicada? ConsultaAplicada { get; private set; }

    /// <summary>
    /// Troca os dados da consulta anterior pelos de <paramref name="d"/> (outro CNPJ confirmado pelo usuário, ou o mesmo CNPJ
    /// consultado de novo). Só na ficha nova e só em memória: nada é gravado nem excluído no banco (a ficha nova não existe lá).
    /// Regra (D-T1, pelo valor): um dado é da consulta enquanto continua com o valor que ela pôs; esses são limpos e recebem
    /// os da nova consulta (o que ela não trouxer fica vazio). O que o usuário digitou ou alterou fica. O endereço da
    /// consulta é uma unidade: alterado pelo usuário, fica inteiro e a nova consulta entra em outro endereço.
    /// </summary>
    public void SubstituirConsultaAnterior(DadosCnpj d)
    {
        if (!Nova || ConsultaAplicada is not { } anterior) return; // guarda: cadastro gravado nunca passa por aqui

        var valores = AlteracoesDaFicha.Valores(ParaDto());
        var endereco = Enderecos.FirstOrDefault(e => e.Id == anterior.EnderecoId && e.Ativo);
        var meiosAnteriores = anterior.Meios.Select(m => m.Id).ToHashSet();

        // 1. Campos da pessoa e do estabelecimento principal ainda com o valor da consulta: limpos.
        foreach (var (chave, origem) in _origemReceita.ToList())
        {
            if (chave.Item is { } item && item != Principal.Id) continue; // endereço, meios (abaixo) e filiais (não mexe)
            if (!AindaDaConsulta(chave, origem.Valor, valores)) continue;
            LimparCampoDaConsulta(chave);
            _origemReceita.Remove(chave);
        }

        // 2. Endereço: o da consulta, sem alteração, é reaproveitado (o lugar é limpo); alterado, fica e não recebe a nova.
        EnderecoFormulario? destino = null;
        EnderecoFormulario? mantido = null;
        bool? criado = null;
        if (endereco is not null)
        {
            if (anterior.EnderecoCriado)
            {
                if (endereco.MesmaLocalizacao(anterior.Localizacao))
                {
                    endereco.LimparLocalizacao();
                    destino = endereco;
                    criado = true;
                }
                else mantido = endereco;
            }
            else
            {
                // Já existia com logradouro (o usuário digitou o lugar): o que a consulta anterior completou e continua com o
                // valor dela é limpo (o endereço volta a ser como o usuário o deixou) e a nova consulta não entra nele: os
                // campos digitados são do lugar de A e misturá-los com os de B daria um endereço que não existe.
                foreach (var campo in EnderecoFormulario.CamposDaLocalizacao)
                {
                    var chave = new ChaveCampo(campo, endereco.Id);
                    if (_origemReceita.TryGetValue(chave, out var origem) && AindaDaConsulta(chave, origem.Valor, valores))
                        endereco.LimparCampoDaConsulta(campo);
                }
                mantido = endereco;
            }
            foreach (var chave in _origemReceita.Keys.Where(c => c.Item == endereco.Id).ToList()) _origemReceita.Remove(chave);
        }

        // 3. Telefones e e-mails que a consulta criou, com o valor e a descrição dela: saem (objetos só da memória).
        foreach (var m in anterior.Meios)
        {
            if (MeiosContato.FirstOrDefault(x => x.Id == m.Id) is not { } meio) continue;
            if (AplicacaoReceita.Igual(meio.Valor, m.Valor) && AplicacaoReceita.Igual(meio.Descricao, m.Descricao))
                meio.AoRemover?.Invoke();
        }
        foreach (var chave in _origemReceita.Keys.Where(c => c.Item is { } i && meiosAnteriores.Contains(i)).ToList())
            _origemReceita.Remove(chave);

        // 4. A nova consulta preenche só o que ficou vazio (sócios: a lista é sempre da consulta, trocada inteira).
        Aplicar(Principal, d, new AplicacaoReceita(conferir: false, somenteVazios: true), destino, criado, mantido, trocarSocios: true);
    }

    /// <summary>
    /// Para a confirmação da troca: quantos dados da consulta anterior serão substituídos e o endereço dela que o usuário
    /// alterou (fica). Mesma regra de <see cref="SubstituirConsultaAnterior"/>.
    /// </summary>
    public (int Substituidos, string? EnderecoMantido) PrevisaoDaTroca()
    {
        if (!Nova || ConsultaAplicada is not { } anterior) return (0, null);
        var valores = AlteracoesDaFicha.Valores(ParaDto());
        var endereco = Enderecos.FirstOrDefault(e => e.Id == anterior.EnderecoId && e.Ativo);
        var enderecoMantido = endereco is not null && anterior.EnderecoCriado && !endereco.MesmaLocalizacao(anterior.Localizacao);
        var meios = anterior.Meios.ToDictionary(m => m.Id);
        var quantos = _origemReceita.Count(o =>
        {
            if (o.Key.Item is { } item && item != Principal.Id && item != endereco?.Id && !meios.ContainsKey(item)) return false;
            if (enderecoMantido && o.Key.Item == endereco!.Id) return false;
            // Telefone/e-mail: sai só com o valor e a descrição que a consulta pôs (mesma regra da troca).
            if (o.Key.Item is { } id && meios.TryGetValue(id, out var daConsulta) &&
                MeiosContato.FirstOrDefault(m => m.Id == id) is { } meio && !AplicacaoReceita.Igual(meio.Descricao, daConsulta.Descricao))
                return false;
            return AindaDaConsulta(o.Key, o.Value.Valor, valores);
        });
        return (quantos, enderecoMantido ? endereco!.Resumo : null);
    }

    private static bool AindaDaConsulta(ChaveCampo chave, string valorDaConsulta, IReadOnlyDictionary<ChaveCampo, string> valores) =>
        valores.TryGetValue(chave, out var atual) && AplicacaoReceita.Igual(atual, valorDaConsulta);

    /// <summary>Esvazia um campo da pessoa ou do estabelecimento principal que a consulta anterior preencheu.</summary>
    private void LimparCampoDaConsulta(ChaveCampo chave)
    {
        var p = Principal;
        switch (chave.Campo)
        {
            case CamposFichaPessoa.Nome:
                Nome = string.Empty;
                _razaoSocialConsultada = string.Empty;
                break;
            case CamposFichaPessoa.NomeFantasia: p.NomeFantasia = string.Empty; break;
            case CamposFichaPessoa.NaturezaJuridica: p.NaturezaJuridica = string.Empty; break;
            case CamposFichaPessoa.Cnae: p.CnaePrincipal = string.Empty; break;
            case CamposFichaPessoa.CnaesSecundarios: p.CnaesSecundarios = string.Empty; break;
            case CamposFichaPessoa.Regime: p.Regime = OpcoesPessoa.Regimes[0]; break;
            case CamposFichaPessoa.InscricaoEstadual:
                p.InscricaoEstadual = string.Empty;
                p.IndicadorIE = OpcoesPessoa.IndicadoresIE[0];
                break;
            case CamposFichaPessoa.DataAbertura: DataAbertura = string.Empty; break;
            case CamposFichaPessoa.Porte: Porte = string.Empty; break;
            case CamposFichaPessoa.CapitalSocial: CapitalSocial = string.Empty; break;
        }
    }

    /// <summary>
    /// Volta o CNPJ do principal sem disparar nova conferência nem consulta (troca cancelada, ou outra empresa recusada num
    /// cadastro gravado).
    /// </summary>
    public void RestaurarCnpj(string cnpj)
    {
        var normal = DocumentoFiscal.Normalizar(cnpj ?? string.Empty);
        _documentoConferido = normal.Length > 0 ? $"{Natureza.Valor}:{normal}" : string.Empty;
        DocumentoEmUsoId = null;
        AvisoDocumentoEmUso = string.Empty;
        Principal.Cnpj = normal.Length > 0 ? DocumentoFiscal.Formatar(normal) : string.Empty;
    }

    /// <summary>CNPJ do principal como está gravado (sem máscara; vazio na ficha nova ou sem CNPJ).</summary>
    public string CnpjGravado { get; private set; } = string.Empty;

    // ---- Origem dos valores (destaque de alterações) ----

    private readonly Dictionary<ChaveCampo, (string Valor, DateTime Quando)> _origemReceita = new();

    /// <summary>Conferência "atual × Receita" da última consulta de CNPJ (cadastro gravado com valores diferentes).</summary>
    public ConferenciaReceita ConferenciaReceita { get; } = new();

    /// <summary>
    /// Campos preenchidos pela Receita e o valor que ela pôs (como texto de tela). Vale enquanto o campo continua com esse
    /// valor: editado depois, passa a ser "digitado". A ficha nova (aberta depois de gravar) começa sem origens.
    /// </summary>
    public IReadOnlyDictionary<ChaveCampo, (string Valor, DateTime Quando)> OrigemReceita => _origemReceita;

    /// <summary>Guarda o valor atual dos campos que a Receita acabou de preencher (o destaque compara com ele).</summary>
    public void RegistrarOrigemReceita(IEnumerable<ChaveCampo> campos)
    {
        var valores = AlteracoesDaFicha.Valores(ParaDto());
        var agora = DateTime.UtcNow;
        foreach (var chave in campos)
            if (valores.TryGetValue(chave, out var valor) && valor.Length > 0)
                _origemReceita[chave] = (valor, agora);
    }

    private MeioContatoFormulario? AdicionarMeioSeNovo(TipoContato tipo, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;

        // Telefone: compara pela mesma regra com que o número é gravado (Telefone: sem o 55 do Brasil e sem o zero de
        // operadora antes do DDD). A Receita manda "01170844621" para o "(11) 7084-4621" que já existe. O valor recebido
        // não é alterado aqui: só a comparação usa a forma normalizada.
        var chave = ChaveTelefone(valor);
        var jaExiste = MeiosContato.Any(m =>
            string.Equals(m.Valor.Trim(), valor.Trim(), StringComparison.OrdinalIgnoreCase) ||
            (tipo != TipoContato.Email && chave.Length > 0 && ChaveTelefone(m.Valor) == chave));

        if (jaExiste) return null;
        var meio = new MeioContatoFormulario
        {
            Tipo = Opcao.De(OpcoesPessoa.TiposContato, tipo),
            Valor = valor,
            Descricao = "Receita Federal"
        };
        AdicionarMeio(meio);
        return meio;
    }

    /// <summary>
    /// Forma de comparar telefones: a normalizada do domínio (a mesma gravada no banco) quando o número é válido; senão,
    /// só os dígitos. Vazio quando não há dígitos.
    /// </summary>
    internal static string ChaveTelefone(string? texto) =>
        Lone.Domain.ObjetosDeValor.Telefone.TentarCriar(texto, out var telefone)
            ? telefone!.Normalizado
            : DocumentoFiscal.SomenteDigitos(texto ?? string.Empty);
}

/// <summary>
/// Dois endereços já gravados que são o mesmo endereço físico. A ficha avisa e oferece consolidar (escolhendo qual
/// fica) ou manter separados; nada é juntado sem a confirmação do usuário.
/// </summary>
public sealed partial class ParDeEnderecos : ObservableObject
{
    public ParDeEnderecos(EnderecoFormulario a, EnderecoFormulario b, bool possivel)
    {
        A = a;
        B = b;
        Possivel = possivel;
    }

    public EnderecoFormulario A { get; }
    public EnderecoFormulario B { get; }

    /// <summary>Faltam dados (CEP ou bairro) para ter certeza.</summary>
    public bool Possivel { get; }

    public string Titulo => Possivel ? "Possível endereço duplicado" : "Endereços duplicados";
    public string TextoA => $"1) {A.Resumo} — finalidades: {A.TextoFinalidades}";
    public string TextoB => $"2) {B.Resumo} — finalidades: {B.TextoFinalidades}";

    public Func<EnderecoFormulario, EnderecoFormulario, Task>? AoConsolidar { get; set; }
    public Action<ParDeEnderecos>? AoManterSeparados { get; set; }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private Task ManterPrimeiroAsync() => AoConsolidar?.Invoke(A, B) ?? Task.CompletedTask;

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private Task ManterSegundoAsync() => AoConsolidar?.Invoke(B, A) ?? Task.CompletedTask;

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ManterSeparados() => AoManterSeparados?.Invoke(this);
}
