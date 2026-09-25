using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.Api;
using Lone.Cliente.Plataforma;
using Lone.Cliente.Sessao;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Etiquetas;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Enums;
using Lone.Domain.ObjetosDeValor;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Central de Pessoas: lista com busca no servidor e filtros; ficha com abas (geral, dados pessoais ou empresa,
/// contatos, endereços, documentos, cliente, fornecedor, informações adicionais, LGPD, situação e histórico),
/// consultas de CNPJ e CEP, municípios do IBGE e as ações de desativar e reativar.
/// </summary>
public sealed partial class PessoasViewModel : CadastroViewModelBase<PessoaResumo>
{
    private readonly PessoasApi _pessoas;
    private readonly ConsultasApi _consultas;
    private readonly SessaoCliente _sessao;
    private readonly ServicoAutenticacao _autenticacao;
    private readonly MunicipiosApi _municipios;
    private readonly CamposPersonalizadosApi _camposApi;
    private readonly EtiquetasApi _etiquetasApi;

    /// <summary>Campos personalizados ativos (lidos ao abrir a tela).</summary>
    private IReadOnlyList<CampoPersonalizadoDto> _campos = [];

    /// <summary>Cadastro de etiquetas, com as desativadas (lido ao abrir a tela; a ficha oferece só as ativas).</summary>
    private List<EtiquetaDto> _etiquetas = [];

    public PessoasViewModel(PessoasApi pessoas, ConsultasApi consultas, SessaoCliente sessao, ServicoAutenticacao autenticacao,
                            MunicipiosApi municipios, CamposPersonalizadosApi camposApi, EtiquetasApi etiquetasApi, IDialogos dialogos)
        : base(dialogos)
    {
        _pessoas = pessoas;
        _consultas = consultas;
        _sessao = sessao;
        _autenticacao = autenticacao;
        _municipios = municipios;
        _camposApi = camposApi;
        _etiquetasApi = etiquetasApi;
    }

    protected override bool BuscaNoServidor => true;

    // ---- Lista ----

    public IReadOnlyList<Opcao<TipoPapel?>> FiltrosPapel => OpcoesPessoa.FiltrosPapel;

    [ObservableProperty] private Opcao<TipoPapel?> _filtroPapel = OpcoesPessoa.FiltrosPapel[0];
    [ObservableProperty] private bool _mostrarInativos;

    /// <summary>Só cadastros com município antigo (texto) a escolher na tabela do IBGE.</summary>
    [ObservableProperty] private bool _somenteMunicipioACorrigir;

    /// <summary>"Todas" e cada etiqueta do cadastro (as desativadas também: podem estar em cadastros antigos).</summary>
    public ObservableCollection<Opcao<Guid?>> FiltrosEtiqueta { get; } = new() { TodasEtiquetas };
    [ObservableProperty] private Opcao<Guid?> _filtroEtiqueta = TodasEtiquetas;
    private static readonly Opcao<Guid?> TodasEtiquetas = new(null, "Todas as etiquetas");

    /// <summary>Atalho "Nova etiqueta" na ficha: só para quem gerencia etiquetas (a API confere de novo).</summary>
    public bool PodeCriarEtiqueta => _sessao.Possui(Permissoes.Cadastros.Etiquetas);

    public bool PodeCriar => _sessao.Possui(Permissoes.Pessoas.Criar);

    partial void OnFiltroPapelChanged(Opcao<TipoPapel?> value) => _ = RecarregarAsync();
    partial void OnMostrarInativosChanged(bool value) => _ = RecarregarAsync();
    partial void OnSomenteMunicipioACorrigirChanged(bool value) => _ = RecarregarAsync();
    partial void OnFiltroEtiquetaChanged(Opcao<Guid?> value)
    {
        // A lista de escolha manda nulo quando o item escolhido sai dela: volta para "todas".
        if (value is null)
        {
            FiltroEtiqueta = TodasEtiquetas;
            return;
        }
        _ = RecarregarAsync();
    }

    /// <summary>
    /// Falha ao ler as etiquetas não impede a lista de aparecer (o filtro e a ficha ficam sem opções, e as já
    /// marcadas numa pessoa voltam intactas ao salvar). Falha ao ler os
    /// campos personalizados também não: a ficha abre sem a aba "Informações adicionais" e avisa.
    /// </summary>
    protected override async Task AntesDeListarAsync()
    {
        try { await AtualizarEtiquetasAsync(); }
        catch (Exception ex) when (ex is not SessaoExpiradaException) { /* filtro sem etiquetas por enquanto */ }

        try { _campos = await _camposApi.ListarAsync(EntidadePersonalizavel.Pessoa, incluirInativos: false); }
        catch (Exception ex) when (ex is not SessaoExpiradaException) { _campos = []; }
    }

    private async Task AtualizarEtiquetasAsync()
    {
        _etiquetas = await _etiquetasApi.ListarAsync(incluirInativas: true);
        AtualizarFiltrosEtiqueta();
    }

    private void AtualizarFiltrosEtiqueta()
    {
        var escolhida = FiltroEtiqueta?.Valor;
        var opcoes = _etiquetas
            .OrderBy(e => !e.Ativo).ThenBy(e => e.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(e => new Opcao<Guid?>(e.Id, e.Ativo ? e.Nome : e.Nome + " (desativada)"))
            .ToList();
        while (FiltrosEtiqueta.Count > 1) FiltrosEtiqueta.RemoveAt(1);
        foreach (var opcao in opcoes) FiltrosEtiqueta.Add(opcao);
        // Mesma escolha se ela ainda existir (outra instância, mesmo valor); senão, "todas".
        var mesma = FiltrosEtiqueta.FirstOrDefault(o => o.Valor == escolhida) ?? TodasEtiquetas;
        if (!ReferenceEquals(mesma, FiltroEtiqueta)) FiltroEtiqueta = mesma;
    }

    protected override string TextoDeBusca(PessoaResumo item) => item.Nome;

    protected override async Task<IReadOnlyList<PessoaResumo>> ListarAsync() =>
        await _pessoas.ListarAsync(new FiltroPessoas
        {
            Texto = Busca,
            Papel = FiltroPapel.Valor,
            IncluirInativos = MostrarInativos,
            MunicipioACorrigir = SomenteMunicipioACorrigir,
            EtiquetaId = FiltroEtiqueta?.Valor
        });

    // ---- Ficha ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeSalvar), nameof(PodeDesativar), nameof(PodeReativar))]
    private PessoaFormulario? _formulario;

    public ObservableCollection<SecaoOpcao> Secoes { get; } = new();
    public ObservableCollection<HistoricoItem> Historico { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NaGeral), nameof(NosPessoais), nameof(NosEstabelecimentos), nameof(NosEnderecos),
                              nameof(NosContatos), nameof(NosDocumentos), nameof(NoCliente), nameof(NoFornecedor),
                              nameof(NoRelacionamento), nameof(NoHistorico), nameof(NasAdicionais), nameof(NaSituacao))]
    private SecaoOpcao? _secaoSelecionada;

    public bool NaGeral => Aba == SecaoPessoa.Geral;
    public bool NosPessoais => Aba == SecaoPessoa.Pessoais;
    public bool NoRelacionamento => Aba == SecaoPessoa.Relacionamento;
    public bool NosEstabelecimentos => Aba == SecaoPessoa.Estabelecimentos;
    public bool NosEnderecos => Aba == SecaoPessoa.Enderecos;
    public bool NosContatos => Aba == SecaoPessoa.Contatos;
    public bool NosDocumentos => Aba == SecaoPessoa.Documentos;
    public bool NoCliente => Aba == SecaoPessoa.Cliente;
    public bool NoFornecedor => Aba == SecaoPessoa.Fornecedor;
    public bool NoHistorico => Aba == SecaoPessoa.Historico;
    public bool NasAdicionais => Aba == SecaoPessoa.Adicionais;
    public bool NaSituacao => Aba == SecaoPessoa.Situacao;

    private SecaoPessoa Aba => SecaoSelecionada?.Secao ?? SecaoPessoa.Geral;

    /// <summary>Incluir exige "cadastrar"; alterar exige "alterar". A API confere de novo ao gravar.</summary>
    public bool PodeSalvar => Formulario is { EstaArquivado: false } f && _sessao.Possui(f.Nova ? Permissoes.Pessoas.Criar : Permissoes.Pessoas.Editar);

    /// <summary>Desativar/reativar: cadastro já gravado e permissão de inativar (a API confere de novo).</summary>
    public bool PodeDesativar => Formulario is { Existente: true, PodeEscolherSituacao: true } && _sessao.Possui(Permissoes.Pessoas.Inativar);
    public bool PodeReativar => Formulario is { Existente: true, EstaInativo: true } && _sessao.Possui(Permissoes.Pessoas.Inativar);

    protected override async Task AbrirAsync(PessoaResumo item)
    {
        var dto = await _pessoas.ObterAsync(item.Id) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        Formulario = PessoaFormulario.De(dto, _campos, _etiquetas);
    }

    protected override Task NovoItemAsync()
    {
        Formulario = PessoaFormulario.NovaPessoa(_campos, _etiquetas);
        return Task.CompletedTask;
    }

    protected override object? DadosDaFicha() => Formulario?.ParaDto();
    protected override bool FichaNova => Formulario?.Nova ?? true;

    /// <summary>Volta a ficha ao que está gravado, na mesma aba.</summary>
    protected override async Task RecarregarFichaAsync()
    {
        if (Formulario is not { Existente: true } formulario) return;
        var dto = await _pessoas.ObterAsync(formulario.Id) ?? throw new ValidacaoException(["Este cadastro não existe mais."]);
        MostrarGravada(dto);
    }

    /// <summary>Troca a ficha pela versão gravada, ficando na aba atual se ela ainda existir.</summary>
    private void MostrarGravada(PessoaDto dto)
    {
        var aba = Aba;
        Formulario = PessoaFormulario.De(dto, _campos, _etiquetas);
        SecaoSelecionada = Secoes.FirstOrDefault(s => s.Secao == aba) ?? Secoes[0];
    }

    partial void OnFormularioChanged(PessoaFormulario? oldValue, PessoaFormulario? newValue)
    {
        if (oldValue is not null) Desligar(oldValue);
        Historico.Clear();
        _historicoDe = null;
        if (newValue is null) return;

        newValue.PodeVerDadosSensiveis = _sessao.Possui(Permissoes.Pessoas.VisualizarDadosSensiveis);
        newValue.ConsultaCep = ConsultarCepAsync;
        newValue.ConsultaCnpj = ConsultarCnpjAsync;
        newValue.FonteMunicipios = uf => _municipios.ListarDaUfAsync(uf);
        newValue.PropertyChanged += Formulario_PropertyChanged;
        foreach (var papel in newValue.Papeis) papel.PropertyChanged += Papel_PropertyChanged;
        AtualizarSecoes(manterAba: false);
    }

    private void Desligar(PessoaFormulario formulario)
    {
        formulario.ConsultaCep = null;
        formulario.ConsultaCnpj = null;
        formulario.FonteMunicipios = null;
        formulario.PropertyChanged -= Formulario_PropertyChanged;
        foreach (var papel in formulario.Papeis) papel.PropertyChanged -= Papel_PropertyChanged;
    }

    private void Formulario_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PessoaFormulario.Natureza)) AtualizarSecoes(manterAba: true);
    }

    private void Papel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PapelOpcao.Ativo)) AtualizarSecoes(manterAba: true);
    }

    /// <summary>Refaz as abas (natureza e papéis mudam quais aparecem), ficando na atual se ela ainda existir.</summary>
    private void AtualizarSecoes(bool manterAba)
    {
        if (Formulario is null) return;
        var atual = Aba;
        Secoes.Clear();
        foreach (var secao in SecaoOpcao.Para(Formulario)) Secoes.Add(secao);
        SecaoSelecionada = (manterAba ? Secoes.FirstOrDefault(s => s.Secao == atual) : null) ?? Secoes[0];
    }

    /// <summary>Histórico já lido para a ficha aberta (lido uma vez, ao abrir a aba).</summary>
    private Guid? _historicoDe;

    [ObservableProperty] private bool _carregandoHistorico;

    partial void OnSecaoSelecionadaChanged(SecaoOpcao? value)
    {
        if (value?.Secao == SecaoPessoa.Historico && Formulario is { Existente: true } f && _historicoDe != f.Id)
            _ = CarregarHistoricoAsync(f.Id);
    }

    /// <summary>Independente do "ocupado" da tela: não apaga mensagens nem espera outra operação.</summary>
    private async Task CarregarHistoricoAsync(Guid pessoaId)
    {
        _historicoDe = pessoaId;
        CarregandoHistorico = true;
        try
        {
            var registros = await _pessoas.ListarHistoricoAsync(pessoaId);
            if (Formulario?.Id != pessoaId) return; // outra ficha foi aberta enquanto lia
            Historico.Clear();
            foreach (var r in registros) Historico.Add(HistoricoItem.De(r));
        }
        catch (SessaoExpiradaException)
        {
            // O aplicativo já volta ao login.
        }
        catch (Exception ex)
        {
            _historicoDe = null; // tenta de novo na próxima vez que a aba abrir
            MostrarErro(ex);
        }
        finally
        {
            CarregandoHistorico = false;
        }
    }

    // ---- Gravação ----

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Formulario is not { } formulario) return;

        if (formulario.ValidarLocalmente() is { Count: > 0 } erros)
        {
            Mostrar(string.Join(Environment.NewLine, erros), TipoMensagem.Erro);
            return;
        }

        var eraEmpresaDoGrupo = formulario.PapelEmpresaDoGrupo.Existia && !formulario.Nova;
        ResultadoSalvarPessoa? resultado = null;
        if (!await ExecutarAsync(async () => resultado = await _pessoas.SalvarAsync(formulario.ParaDto())))
            return;

        MostrarGravada(resultado!.Pessoa);
        MarcarFichaSemAlteracoes();

        Mostrar(resultado.Avisos.Count > 0
                ? "Salvo, com avisos:" + Environment.NewLine + string.Join(Environment.NewLine, resultado.Avisos)
                : "Cadastro salvo.",
            resultado.Avisos.Count > 0 ? TipoMensagem.Aviso : TipoMensagem.Sucesso);

        // Empresas do grupo mudaram: a sessão relê as empresas disponíveis (menu "Trocar empresa").
        if (eraEmpresaDoGrupo || resultado.Pessoa.TemPapel(TipoPapel.EmpresaDoGrupo))
            await AtualizarEmpresasDaSessaoAsync();

        await AtualizarListaAposGravarAsync();
    }

    private async Task AtualizarEmpresasDaSessaoAsync()
    {
        try
        {
            await _autenticacao.AtualizarSessaoAsync();
        }
        catch (SessaoExpiradaException)
        {
            // O aplicativo já volta ao login com a mensagem.
        }
        catch (Exception)
        {
            Mostrar(Mensagem + Environment.NewLine + "A lista de empresas será atualizada no próximo acesso.", TipoMensagem.Aviso);
        }
    }

    // ---- Situação (desativar e reativar) ----

    [RelayCommand]
    private Task DesativarAsync() => AlterarSituacaoAsync(desativar: true);

    [RelayCommand]
    private Task ReativarAsync() => AlterarSituacaoAsync(desativar: false);

    /// <summary>
    /// Pede o motivo (opcional) e chama a ação própria da API. Não mistura com alterações não salvas: o usuário
    /// salva ou descarta antes, para ficar claro o que foi gravado.
    /// </summary>
    private async Task AlterarSituacaoAsync(bool desativar)
    {
        if (Formulario is not { Existente: true } formulario) return;
        if (TemAlteracoes)
        {
            Mostrar("Salve ou descarte as alterações antes de " + (desativar ? "desativar" : "reativar") + " o cadastro.", TipoMensagem.Aviso);
            return;
        }

        var motivo = await PerguntarAsync(
            desativar ? "Desativar cadastro" : "Reativar cadastro",
            desativar
                ? $"{formulario.Nome} deixará de aparecer nas buscas e operações (continua no filtro \"Inativos\", com todo o histórico, e pode ser reativado). Motivo (opcional):"
                : $"{formulario.Nome} volta a aparecer nas buscas e operações. Motivo (opcional):",
            desativar ? "Desativar" : "Reativar",
            "Cancelar",
            desativar ? "Ex.: não compra há 2 anos" : null,
            200);
        if (motivo is null) return; // cancelou

        PessoaDto? gravada = null;
        if (!await ExecutarAsync(async () => gravada = desativar
                ? await _pessoas.DesativarAsync(formulario.Id, formulario.Versao, TextoTela.Nulo(motivo))
                : await _pessoas.ReativarAsync(formulario.Id, formulario.Versao, TextoTela.Nulo(motivo))))
            return;

        MostrarGravada(gravada!);
        MarcarFichaSemAlteracoes();
        Mostrar(desativar ? "Cadastro desativado. Ele continua no filtro \"Inativos\"." : "Cadastro reativado.", TipoMensagem.Sucesso);
        await AtualizarListaAposGravarAsync();
    }

    // ---- Itens das listas da ficha ----

    [RelayCommand]
    private void AdicionarEstabelecimento() => Formulario?.AdicionarEstabelecimento();

    /// <summary>O primeiro item de cada lista já nasce como principal.</summary>
    [RelayCommand]
    private void AdicionarEndereco()
    {
        if (Formulario is { } f) f.AdicionarEndereco(new EnderecoFormulario { Principal = f.Enderecos.Count == 0 });
    }

    [RelayCommand]
    private void AdicionarMeio()
    {
        if (Formulario is { } f) f.AdicionarMeio(new MeioContatoFormulario { Principal = f.MeiosContato.Count == 0 });
    }

    [RelayCommand]
    private void AdicionarContato()
    {
        if (Formulario is { } f) f.AdicionarContato(new ContatoFormulario { Principal = f.Contatos.Count == 0 });
    }

    [RelayCommand]
    private void AdicionarDocumento() => Formulario?.AdicionarDocumento(new DocumentoFormulario());

    /// <summary>
    /// Atalho da ficha: cria a etiqueta no cadastro (fica disponível para todos) e já a marca nesta pessoa.
    /// A pessoa só é gravada ao salvar a ficha.
    /// </summary>
    [RelayCommand]
    private async Task NovaEtiquetaAsync()
    {
        if (Formulario is not { } formulario) return;
        var nome = await PerguntarAsync(
            "Nova etiqueta",
            "Nome da etiqueta. Ela fica disponível para todos os cadastros:",
            "Criar", "Cancelar", "Ex.: Cliente VIP", global::Lone.Domain.Entidades.Etiqueta.TamanhoMaximoNome);
        if (string.IsNullOrWhiteSpace(nome)) return;

        EtiquetaDto? criada = null;
        if (!await ExecutarAsync(async () =>
                criada = await _etiquetasApi.SalvarAsync(new EtiquetaDto { Id = IdSequencial.Novo(), Nome = nome.Trim() })))
            return;

        var etiqueta = criada!;
        _etiquetas.Add(etiqueta);
        FiltrosEtiqueta.Add(new Opcao<Guid?>(etiqueta.Id, etiqueta.Nome));
        formulario.Etiquetas.Incluir(etiqueta, marcar: true);
        Mostrar($"Etiqueta \"{etiqueta.Nome}\" criada e marcada. Salve o cadastro para gravar a marcação.", TipoMensagem.Sucesso);
    }

    // ---- Consultas externas ----

    private async Task ConsultarCnpjAsync(EstabelecimentoFormulario estabelecimento)
    {
        if (!Documento.CnpjValido(estabelecimento.Cnpj))
        {
            Mostrar("Digite um CNPJ válido para consultar.", TipoMensagem.Aviso);
            return;
        }

        DadosCnpj? dados = null;
        if (!await ExecutarAsync(async () => dados = await _consultas.ConsultarCnpjAsync(Documento.Normalizar(estabelecimento.Cnpj))))
            return;

        if (dados is null)
        {
            Mostrar("CNPJ não encontrado na Receita Federal.", TipoMensagem.Aviso);
            return;
        }

        Formulario?.AplicarCnpj(estabelecimento, dados);
        Mostrar($"Dados preenchidos pela consulta ({dados.Fonte}). " +
                (estabelecimento.InscricaoVeioDaConsulta
                    ? "A inscrição estadual também foi encontrada. Confira e salve."
                    : "A inscrição estadual não foi encontrada nas fontes públicas: informe-a (o sistema confere o dígito da UF ao salvar)."),
            TipoMensagem.Informacao);
    }

    private async Task ConsultarCepAsync(EnderecoFormulario endereco)
    {
        if (!Cep.TentarCriar(endereco.Cep, out var cep))
        {
            Mostrar("Digite um CEP válido (8 dígitos) para buscar.", TipoMensagem.Aviso);
            return;
        }

        DadosCep? dados = null;
        if (!await ExecutarAsync(async () => dados = await _consultas.ConsultarCepAsync(cep!.Valor)))
            return;

        if (dados is null)
        {
            Mostrar($"O CEP {cep!.Formatado} não existe na base dos Correios. Confira o número.", TipoMensagem.Aviso);
            return;
        }

        endereco.AplicarCep(dados);
        if (string.IsNullOrWhiteSpace(dados.Logradouro))
            Mostrar($"CEP geral de {dados.Cidade}/{dados.Uf}: esta cidade não tem CEP por rua. Informe o logradouro e o bairro.",
                TipoMensagem.Informacao);
    }
}
