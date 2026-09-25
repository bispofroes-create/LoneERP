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
using DocumentoFiscal = Lone.Domain.Validacao.Documento;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Ficha de uma pessoa em edição. Cada parte tem a sua classe (estabelecimento, endereço, contato, documento,
/// papel, contas); esta classe compõe o conjunto, mantém as regras entre as partes e converte de/para o DTO.
/// </summary>
public sealed partial class PessoaFormulario : ObservableObject
{
    // Dados que a ficha ainda não edita, mas precisam voltar intactos ao salvar.
    private Guid? _grupoEconomicoId;
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
                              nameof(RotuloNome), nameof(RotuloDocumento), nameof(MascaraDocumento), nameof(MostrarCorRaca))]
    private Opcao<NaturezaPessoa> _natureza = OpcoesPessoa.Naturezas[0];

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
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Titulo))] private string _nome = string.Empty;
    [ObservableProperty] private string _nomeSocial = string.Empty;
    [ObservableProperty] private string _nomeExibicao = string.Empty;
    [ObservableProperty] private string _apelido = string.Empty;

    /// <summary>CPF (PF) ou identificação do estrangeiro. Na PJ, o CNPJ fica nos estabelecimentos.</summary>
    [ObservableProperty] private string _documento = string.Empty;

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

    // ---- Relacionamento ----

    [ObservableProperty] private string _origemCadastro = OpcoesPessoa.Origens[0];
    [ObservableProperty] private string _primeiroContatoEm = string.Empty;

    /// <summary>Etiquetas do cadastro de etiquetas, para marcar.</summary>
    public EtiquetasFormulario Etiquetas { get; private set; } = EtiquetasFormulario.Criar([], []);

    /// <summary>Um item por canal (e-mail, WhatsApp, SMS, ligações, correspondência).</summary>
    public IReadOnlyList<ConsentimentoFormulario> Consentimentos { get; private set; } =
        OpcoesPessoa.Canais.Select(ConsentimentoFormulario.Novo).ToList();

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

    /// <summary>Bloqueios ativos (resumo no topo da ficha).</summary>
    public string TextoBloqueios { get; private set; } = string.Empty;
    public bool TemBloqueios => TextoBloqueios.Length > 0;

    // ---- Calculados ----

    public IReadOnlyList<Opcao<NaturezaPessoa>> Naturezas => OpcoesPessoa.Naturezas;
    public IReadOnlyList<Opcao<SituacaoPessoa>> Situacoes => OpcoesPessoa.SituacoesEditaveis;

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
    public string RotuloDocumento => EhFisica ? "CPF" : "Identificação estrangeira";

    /// <summary>CPF com máscara; a identificação de estrangeiro tem formatos variados e fica livre.</summary>
    public TipoMascara MascaraDocumento => EhFisica ? TipoMascara.Cpf : TipoMascara.Nenhuma;

    /// <summary>Calculada ao digitar a data de nascimento (ex.: "34 anos").</summary>
    public string Idade
    {
        get
        {
            if (!TextoTela.TentarData(DataNascimento, out var data) || data is not { } nascimento) return string.Empty;
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            if (nascimento > hoje) return "Data no futuro";
            var anos = global::Lone.Domain.Comum.Idade.Em(nascimento, hoje);
            return anos switch { 0 => "Menos de 1 ano", 1 => "1 ano", _ => $"{anos} anos" };
        }
    }
    public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Nova pessoa" : Nome;
    public string CodigoTexto => Nova ? "Novo cadastro" : $"Código {Codigo:000000}";

    partial void OnNaturezaChanged(Opcao<NaturezaPessoa> value)
    {
        foreach (var e in Estabelecimentos) e.DaPessoaJuridica = EhJuridica;
    }

    // ---- Criação e conversão ----

    public static PessoaFormulario NovaPessoa(IReadOnlyList<CampoPersonalizadoDto>? campos = null, IReadOnlyList<EtiquetaDto>? etiquetas = null,
                                             IReadOnlyList<ProfissaoDto>? profissoes = null, IReadOnlyList<PapelCadastroDto>? papeis = null,
                                             IReadOnlyList<TipoMeioContatoDto>? tiposMeio = null, IReadOnlyList<TipoEnderecoDto>? tiposEndereco = null,
                                             IReadOnlyList<TipoDocumentoDto>? tiposDocumento = null,
                                             IReadOnlyList<CampoPersonalizadoDto>? camposDocumento = null)
    {
        var f = new PessoaFormulario(IdSequencial.Novo(), nova: true)
        {
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
        f.AdicionarEndereco(new EnderecoFormulario { Principal = true });
        f.AdicionarEstabelecimento();
        return f;
    }

    public static PessoaFormulario De(PessoaDto p, IReadOnlyList<CampoPersonalizadoDto>? campos = null, IReadOnlyList<EtiquetaDto>? etiquetas = null,
                                      IReadOnlyList<ProfissaoDto>? profissoes = null, IReadOnlyList<PapelCadastroDto>? papeis = null,
                                      IReadOnlyList<TipoMeioContatoDto>? tiposMeio = null, IReadOnlyList<TipoEnderecoDto>? tiposEndereco = null,
                                      IReadOnlyList<TipoDocumentoDto>? tiposDocumento = null,
                                      IReadOnlyList<CampoPersonalizadoDto>? camposDocumento = null)
    {
        var opcoesPapel = MontarPapeis(papeis, p.Papeis, out var papeisDesconhecidos);
        var f = new PessoaFormulario(p.Id, nova: false)
        {
            InformacoesAdicionais = MontarInformacoesAdicionais(campos, p.ValoresPersonalizados),
            Etiquetas = EtiquetasFormulario.Criar(etiquetas, p.EtiquetaIds),
            SituacaoGravada = p.Situacao,
            SituacaoMotivo = p.SituacaoMotivo,
            SituacaoAlteradaEm = p.SituacaoAlteradaEm,
            NaturalidadeACorrigir = p.PendenciasMunicipio.FirstOrDefault(x => x.DaNaturalidade)?.Texto ?? string.Empty,
            _grupoEconomicoId = p.GrupoEconomicoId,
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
            PrimeiroContatoEm = TextoTela.Data(p.PrimeiroContatoEm),
            Consentimentos = OpcoesPessoa.Canais
                .Select(canal => p.Consentimentos.FirstOrDefault(c => c.Canal == canal) is { } c
                    ? ConsentimentoFormulario.De(c)
                    : ConsentimentoFormulario.Novo(canal))
                .ToList()
        };

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

        foreach (var m in p.MeiosContato.OrderByDescending(m => m.Ativo).ThenBy(m => m.Tipo).ThenByDescending(m => m.Principal))
            f.AdicionarMeio(MeioContatoFormulario.De(m));
        foreach (var c in p.Contatos.OrderByDescending(c => c.Principal).ThenBy(c => c.Nome))
            f.AdicionarContato(ContatoFormulario.De(c));
        foreach (var d in p.Documentos)
            f.AdicionarDocumento(DocumentoFormulario.De(d));

        return f;
    }

    /// <summary>Datas e números que não dá para entender. O resto (CPF, CNPJ, obrigatórios) a API valida.</summary>
    public IReadOnlyList<string> ValidarLocalmente()
    {
        var erros = new List<string>();
        if (EhFisica)
        {
            if (!TextoTela.TentarData(DataNascimento, out var nascimento))
                erros.Add("Data de nascimento inválida (use dd/mm/aaaa).");
            else if (nascimento > DateOnly.FromDateTime(DateTime.Today))
                erros.Add("A data de nascimento está no futuro.");
        }
        if (EhFisica && Naturalidade.Validar("Naturalidade") is { } naturalidade)
            erros.Add(naturalidade);
        if (EhFisica && Profissao.Validar("Profissão") is { } profissao)
            erros.Add(profissao);
        // Endereço inativo não é mais conferido (pode ser antigo, de antes da tabela do IBGE).
        for (var i = 0; i < Enderecos.Count; i++)
            if (Enderecos[i].Ativo && Enderecos[i].ValidarMunicipio($"Endereço {i + 1}") is { } endereco)
                erros.Add(endereco);
        erros.AddRange(Documentos.SelectMany(d => d.Validar()));
        for (var i = 0; i < Vinculos.Count; i++)
            erros.AddRange(Vinculos[i].Validar($"Vínculo {i + 1}"));
        erros.AddRange(InformacoesAdicionais.Select(c => c.Validar()).OfType<string>());
        if (EhJuridica && !TextoTela.TentarData(DataAbertura, out _))
            erros.Add("Data de abertura inválida (use dd/mm/aaaa).");
        if (EhJuridica && !TextoTela.TentarDecimal(CapitalSocial, out _))
            erros.Add("Capital social inválido.");
        if (!TextoTela.TentarData(PrimeiroContatoEm, out _))
            erros.Add("Data do primeiro contato inválida (use dd/mm/aaaa).");
        if (PapelCliente.Ativo) erros.AddRange(ContaCliente.Validar());
        for (var i = 0; i < Excecoes.Count; i++) erros.AddRange(Excecoes[i].Validar($"Exceção comercial {i + 1}"));
        for (var i = 0; i < Carteira.Count; i++) erros.AddRange(Carteira[i].Validar($"Carteira {i + 1}"));
        if (PapelFornecedor.Ativo) erros.AddRange(ContaFornecedor.Validar());
        return erros;
    }

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
            GrupoEconomicoId = _grupoEconomicoId,
            MescladaEmId = _mescladaEmId,
            Observacoes = TextoTela.Nulo(Observacoes),
            // Só a PJ tem filiais; nas outras naturezas vai o estabelecimento principal (dados fiscais).
            Estabelecimentos = (EhJuridica ? Estabelecimentos.ToList() : [Principal]).Select(e => e.ParaDto()).ToList(),
            Enderecos = Enderecos.Select((e, i) => e.ParaDto(i)).ToList(),
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
            DataAbertura = abertura,
            Porte = TextoTela.Nulo(Porte),
            CapitalSocial = capital,
            Socios = Socios.ToList(),
            OrigemCadastro = OrigemCadastro == OpcoesPessoa.Origens[0] ? null : TextoTela.Nulo(OrigemCadastro),
            PrimeiroContatoEm = primeiroContato,
            Consentimentos = Consentimentos.Select(c => c.ParaDto()).OfType<ConsentimentoDto>().ToList(),
            EtiquetaIds = Etiquetas.Marcadas.ToList(),
            ValoresPersonalizados = InformacoesAdicionais.Select(c => c.ParaDto()).OfType<ValorPersonalizadoDto>().ToList(),
            MotivoAlteracao = TextoTela.Nulo(MotivoAlteracao)?.Trim()
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
        var itens = (cadastro ?? []).Select(c => (c.Id, c.PapelSistema, c.Nome, c.Ativo, c.Ordem)).ToList();
        foreach (var sistema in global::Lone.Domain.Papeis.PapeisSistema.Todos.Where(s => itens.All(i => i.PapelSistema != s.Tipo)))
            itens.Add((sistema.Id, sistema.Tipo, sistema.Nome, true, sistema.Ordem));

        // Período sem o Id do cadastro (dado antigo) é reconhecido pelo papel de sistema.
        bool DoPapel(PapelDto periodo, Guid id, TipoPapel? sistema) =>
            periodo.PapelId == id || (periodo.PapelId == Guid.Empty && periodo.Papel is not null && periodo.Papel == sistema);

        var opcoes = itens
            .Where(i => i.Ativo || periodos.Any(p => p.Ativo && DoPapel(p, i.Id, i.PapelSistema)))
            .OrderBy(i => i.Ordem).ThenBy(i => i.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(i => new PapelOpcao(i.Id, i.PapelSistema, i.Nome, i.Ativo, periodos.Where(p => DoPapel(p, i.Id, i.PapelSistema))))
            .ToList();
        desconhecidos = periodos.Where(p => !opcoes.Any(o => DoPapel(p, o.PapelId, o.Papel))).ToList();
        return opcoes;
    }

    // ---- Profissão ----

    /// <summary>Gravada, mas fora da lista lida (a lista não pôde ser lida): volta intacta ao salvar.</summary>
    private Guid? _profissaoDesconhecida;

    /// <summary>
    /// Oferece as profissões ativas e mostra a gravada (mesmo desativada). Sem a lista (falha ao ler), a gravada
    /// é mantida sem ser mostrada pelo nome.
    /// </summary>
    private void DefinirProfissoes(IReadOnlyList<ProfissaoDto>? profissoes, Guid? gravada)
    {
        var lista = profissoes ?? [];
        Profissao.DefinirItens(lista.Where(x => x.Ativo).OrderBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(ItemDe).ToList());
        var atual = gravada is { } id ? lista.FirstOrDefault(x => x.Id == id) : null;
        _profissaoDesconhecida = gravada is not null && atual is null ? gravada : null;
        Profissao.Definir(atual is null ? null : ItemDe(atual));
    }

    /// <summary>Profissão criada agora pelo atalho da ficha: entra na lista e já fica escolhida.</summary>
    public void IncluirProfissao(ProfissaoDto profissao)
    {
        var item = ItemDe(profissao);
        Profissao.DefinirItens([.. Profissao.Itens.Where(i => i.Chave != item.Chave), item]);
        _profissaoDesconhecida = null;
        Profissao.Definir(item);
    }

    private static ItemSeletor ItemDe(ProfissaoDto p) => new(p.Id.ToString(), p.Ativo ? p.Nome : p.Nome + " (desativada)");

    /// <summary>A escolhida; sem escolha e sem texto, a gravada que não veio na lista (se houver) continua.</summary>
    private Guid? ProfissaoEscolhida() =>
        Guid.TryParse(Profissao.Chave, out var id) ? id
        : string.IsNullOrWhiteSpace(Profissao.Texto) ? _profissaoDesconhecida
        : null;

    private static List<CampoPersonalizadoFormulario> MontarInformacoesAdicionais(
        IReadOnlyList<CampoPersonalizadoDto>? campos, IReadOnlyList<ValorPersonalizadoDto> valores) =>
        (campos ?? [])
            .Where(c => c.Ativo && c.Visivel) // oculto: o servidor mantém o valor gravado
            .OrderBy(c => c.Ordem)
            .Select(c => CampoPersonalizadoFormulario.Criar(c, valores.FirstOrDefault(v => v.CampoId == c.Id)))
            .ToList();

    /// <summary>A cor/raça depende do papel de funcionário: acompanha quando ele é ligado ou desligado.</summary>
    private void OuvirPapeis()
    {
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
        endereco.PropertyChanged += Endereco_PropertyChanged;
        Enderecos.Add(endereco);
        OnPropertyChanged(nameof(TemEnderecosInativos));
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
        {
            endereco.Principal = false;
            endereco.Ativo = false;
        }
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

    /// <summary>Só um endereço principal: marcar um desmarca os outros.</summary>
    private void Endereco_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EnderecoFormulario.Ativo)) OnPropertyChanged(nameof(TemEnderecosInativos));
        if (e.PropertyName != nameof(EnderecoFormulario.Principal) || sender is not EnderecoFormulario { Principal: true } marcado)
            return;
        foreach (var outro in Enderecos.Where(x => !ReferenceEquals(x, marcado)))
            outro.Principal = false;
    }

    // ---- Estabelecimentos ----

    public EstabelecimentoFormulario AdicionarEstabelecimento()
    {
        var estabelecimento = new EstabelecimentoFormulario(Enderecos);
        Incluir(estabelecimento);
        MarcarPrincipal();
        return estabelecimento;
    }

    public void RemoverEstabelecimento(EstabelecimentoFormulario estabelecimento)
    {
        if (Estabelecimentos.Count <= 1 || ReferenceEquals(estabelecimento, Principal)) return;
        Estabelecimentos.Remove(estabelecimento);
        MarcarPrincipal();
    }

    /// <summary>O principal é sempre o primeiro da lista.</summary>
    public void TornarPrincipal(EstabelecimentoFormulario estabelecimento)
    {
        var indice = Estabelecimentos.IndexOf(estabelecimento);
        if (indice <= 0) return;
        Estabelecimentos.Move(indice, 0);
        MarcarPrincipal();
        OnPropertyChanged(nameof(Principal));
    }

    private void Incluir(EstabelecimentoFormulario estabelecimento)
    {
        estabelecimento.DaPessoaJuridica = EhJuridica;
        estabelecimento.AoRemover = () => RemoverEstabelecimento(estabelecimento);
        estabelecimento.AoTornarPrincipal = () => TornarPrincipal(estabelecimento);
        estabelecimento.AoConsultarCnpj = e => ConsultaCnpj?.Invoke(e) ?? Task.CompletedTask;
        Estabelecimentos.Add(estabelecimento);
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
        };
        meio.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MeioContatoFormulario.Ativo)) OnPropertyChanged(nameof(TemMeiosInativos));
        };
        MeiosContato.Add(meio);
        OnPropertyChanged(nameof(TemMeiosInativos));
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

    public void AdicionarContato(ContatoFormulario contato)
    {
        contato.AoRemover = () => Contatos.Remove(contato);
        Contatos.Add(contato);
    }

    public void AdicionarDocumento(DocumentoFormulario documento)
    {
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
            if (e.PropertyName is nameof(DocumentoFormulario.Ativo) or nameof(DocumentoFormulario.AvisoValidade)) AvisarDocumentos();
        };
        Documentos.Add(documento);
        AvisarDocumentos();
    }

    // ---- Comercial (perfil, condição, exceções com vigência e carteira de clientes) ----

    public ObservableCollection<ExcecaoComercialFormulario> Excecoes { get; } = new();
    public ObservableCollection<CarteiraFormulario> Carteira { get; } = new();

    private OpcoesComercial? _opcoesComercial;

    public bool OpcoesComercialCarregadas => _opcoesComercial is not null;

    /// <summary>Chamado pela tela quando a aba "Cliente" abre e as opções chegam.</summary>
    public void DefinirOpcoesComercial(ComercialOpcoesDto dto)
    {
        _opcoesComercial = new OpcoesComercial(dto);
        ContaCliente.DefinirOpcoes(_opcoesComercial);
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
            if (carteira.Gravada) carteira.Ativo = false; // gravado por engano: fica inativo (histórico)
            else Carteira.Remove(carteira);
        };
        if (Carteira.Contains(carteira)) return;
        Carteira.Insert(0, carteira);
    }

    public void NovaExcecao() => AdicionarExcecao(ExcecaoComercialFormulario.Nova());

    public void NovaCarteira() => AdicionarCarteira(CarteiraFormulario.Nova(_opcoesComercial));

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

    public bool TemDocumentosInativos => Documentos.Any(d => !d.Ativo);

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

    /// <summary>Preenche o estabelecimento (e, no principal, a razão social e o endereço). O usuário confere e salva.</summary>
    public void AplicarCnpj(EstabelecimentoFormulario estabelecimento, DadosCnpj d)
    {
        estabelecimento.AplicarCnpj(d);

        EnderecoFormulario? endereco;
        if (ReferenceEquals(estabelecimento, Principal))
        {
            if (d.RazaoSocial.Length > 0) Nome = d.RazaoSocial;
            endereco = Enderecos.FirstOrDefault(e => e.Ativo && e.Principal) ?? Enderecos.FirstOrDefault(e => e.Ativo);
            if (endereco is null)
            {
                endereco = new EnderecoFormulario { Principal = true, Fiscal = true };
                AdicionarEndereco(endereco);
            }
        }
        else
        {
            // Filial: endereço próprio, usado como endereço fiscal dela.
            endereco = estabelecimento.EnderecoFiscal;
            if (endereco is null)
            {
                endereco = new EnderecoFormulario { Descricao = "Filial " + DocumentoFiscal.Formatar(d.Cnpj), Fiscal = true };
                AdicionarEndereco(endereco);
                estabelecimento.EnderecoFiscal = endereco;
            }
        }
        endereco.AplicarCnpj(d);

        AdicionarMeioSeNovo(TipoContato.Telefone, d.Telefone);
        AdicionarMeioSeNovo(TipoContato.Email, d.Email);

        // Dados da empresa são da raiz do CNPJ: valem para a pessoa, venha a consulta da matriz ou de uma filial.
        if (d.DataAbertura is not null && ReferenceEquals(estabelecimento, Principal)) DataAbertura = TextoTela.Data(d.DataAbertura);
        if (d.Porte is not null) Porte = d.Porte;
        if (d.CapitalSocial is not null) CapitalSocial = TextoTela.Decimal(d.CapitalSocial);
        if (d.Socios.Count > 0)
        {
            Socios.Clear();
            foreach (var socio in d.Socios) Socios.Add(socio);
            OnPropertyChanged(nameof(TemSocios));
        }
    }

    private void AdicionarMeioSeNovo(TipoContato tipo, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return;

        var digitos = DocumentoFiscal.SomenteDigitos(valor);
        var jaExiste = MeiosContato.Any(m =>
            string.Equals(m.Valor.Trim(), valor.Trim(), StringComparison.OrdinalIgnoreCase) ||
            (tipo != TipoContato.Email && digitos.Length > 0 && DocumentoFiscal.SomenteDigitos(m.Valor) == digitos));

        if (!jaExiste)
            AdicionarMeio(new MeioContatoFormulario
            {
                Tipo = Opcao.De(OpcoesPessoa.TiposContato, tipo),
                Valor = valor,
                Descricao = "Receita Federal"
            });
    }
}
