using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Cadastros;
using Lone.Contracts.CamposPersonalizados;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Documentos;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Documentos;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;

using Lone.Domain.Pessoas;
using Lone.Domain.Validacao;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// RG, CNH, alvará... O tipo vem do cadastro de tipos de documento, que também diz se a validade é obrigatória e
/// com quantos dias avisar o vencimento. As datas são digitadas como dd/mm/aaaa. Remover um documento já gravado
/// só o desativa (volta em "Mostrar inativos"); um ainda não gravado sai da lista.
/// </summary>
public sealed partial class DocumentoFormulario : ItemDeLista
{
    private IReadOnlyList<TipoDocumentoDto> _catalogo = [];
    private Guid _tipoGravado;
    private NaturezaPessoa _natureza = NaturezaPessoa.Fisica;

    /// <summary>Documento novo começa sem tipo: o usuário escolhe (nada de "RG" pré-escolhido numa empresa).</summary>
    public static readonly Opcao<Guid> SemTipo = new(Guid.Empty, "Escolha o tipo");

    /// <summary>Órgão emissor ou UF gravados: continuam à vista mesmo num tipo que não os usa.</summary>
    private bool _orgaoOuUfGravados;

    public DocumentoFormulario() : this(IdSequencial.Novo(), gravado: false) { }

    private DocumentoFormulario(Guid id, bool gravado)
    {
        Id = id;
        Gravado = gravado;
        _tipos = [SemTipo, .. TiposSemCadastro(Guid.Empty)];
        _tipo = SemTipo;
    }

    public Guid Id { get; }

    /// <summary>Já existe no banco: remover desativa em vez de tirar da lista.</summary>
    public bool Gravado { get; }

    /// <summary>Data de referência do aviso de vencimento (a ficha usa a data do aparelho; os testes fixam uma).</summary>
    public DateOnly Hoje { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    // ---- Tipo (cadastro de tipos de documento) ----

    /// <summary>Tipos ativos do cadastro (mais o gravado, se desativado). Array: o Picker precisa de IList.</summary>
    [ObservableProperty] private Opcao<Guid>[] _tipos;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvisoValidade), nameof(TemAvisoValidade), nameof(Vencido), nameof(RotuloValidade),
                              nameof(MostrarOrgaoEmissor), nameof(MostrarUf), nameof(MostrarEmissao),
                              nameof(RotuloOrgaoEmissor), nameof(RotuloUf), nameof(RotuloEmissao))]
    private Opcao<Guid> _tipo;

    /// <summary>Chamado pela ficha ao incluir o item: a lista de tipos do cadastro (vazia = não foi possível ler).</summary>
    public void DefinirCatalogo(IReadOnlyList<TipoDocumentoDto> catalogo)
    {
        _catalogo = catalogo;
        MontarTipos();
    }

    /// <summary>Chamado pela ficha ao incluir o item e quando a natureza muda: só os tipos que se aplicam a ela.</summary>
    public void DefinirNatureza(NaturezaPessoa natureza)
    {
        _natureza = natureza;
        MontarTipos();
    }

    /// <summary>
    /// Tipos ativos que se aplicam à natureza da pessoa, mais o gravado (mesmo desativado ou de outra natureza: volta intacto).
    /// Sem tipo escolhido (ou com um que deixou de se aplicar, num documento novo), a lista começa em "Escolha o tipo".
    /// </summary>
    private void MontarTipos()
    {
        var atual = Tipo.Valor;
        var lista = _catalogo.Count == 0
            ? TiposSemCadastro(_tipoGravado)
                .Where(o => o.Valor == _tipoGravado || TiposDocumentoSistema.AplicaA(TipoSistemaDe(o.Valor), _natureza)).ToList()
            : _catalogo
                .Where(t => t.Id == _tipoGravado || (t.Ativo && AplicaA(t, _natureza)))
                .OrderBy(t => t.Ordem).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Select(t => new Opcao<Guid>(t.Id, t.Ativo ? t.Nome : t.Nome + " (desativado)"))
                .ToList();
        var escolhido = lista.FirstOrDefault(o => o.Valor == atual);
        if (escolhido is null) lista.Insert(0, SemTipo);
        Tipos = [.. lista];
        Tipo = escolhido ?? SemTipo;
    }

    /// <summary>Sem o cadastro (falha ao ler): os tipos de sistema, mais o gravado se for outro — ele volta intacto.</summary>
    private static Opcao<Guid>[] TiposSemCadastro(Guid gravado)
    {
        var sistema = TiposDocumentoSistema.Todos.Select(t => new Opcao<Guid>(t.Id, t.Nome)).ToList();
        if (gravado != Guid.Empty && sistema.All(o => o.Valor != gravado))
            sistema.Insert(0, new Opcao<Guid>(gravado, "(tipo gravado)"));
        return [.. sistema];
    }

    /// <summary>O enum de sistema do tipo (nulo = tipo criado pelo usuário).</summary>
    private TipoDocumento? TipoSistemaDe(Guid tipoId) =>
        _catalogo.FirstOrDefault(t => t.Id == tipoId) is { } doCadastro
            ? doCadastro.TipoSistema
            : TiposDocumentoSistema.Todos.Where(t => t.Id == tipoId).Select(t => (TipoDocumento?)t.Tipo).FirstOrDefault();

    /// <summary>
    /// P1-8B: a quem o tipo se aplica vem do cadastro (os de sistema nascem com a regra de antes). As regras fixas do enum
    /// ficam só para quando o cadastro não pôde ser lido.
    /// </summary>
    private static bool AplicaA(TipoDocumentoDto t, NaturezaPessoa natureza) => natureza switch
    {
        NaturezaPessoa.Fisica => t.AplicaPessoaFisica,
        NaturezaPessoa.Juridica => t.AplicaPessoaJuridica,
        _ => t.AplicaEstrangeiro
    };

    /// <summary>Uso do órgão emissor no tipo escolhido (do cadastro; sem ele, a regra fixa de antes).</summary>
    private UsoCampoDocumento UsoOrgao => TipoDoCadastro?.UsoOrgaoEmissor ??
        (TiposDocumentoSistema.TemOrgaoEmissor(TipoSistemaDe(Tipo.Valor)) ? UsoCampoDocumento.Opcional : UsoCampoDocumento.Oculto);

    private UsoCampoDocumento UsoUfDoTipo => TipoDoCadastro?.UsoUf ??
        (TiposDocumentoSistema.TemUf(TipoSistemaDe(Tipo.Valor)) ? UsoCampoDocumento.Opcional : UsoCampoDocumento.Oculto);

    private UsoCampoDocumento UsoEmissaoDoTipo => TipoDoCadastro?.UsoEmissao ?? UsoCampoDocumento.Opcional;

    /// <summary>Órgão emissor: conforme o tipo ("Não usar" esconde) ou já preenchido (continua à vista e intacto).</summary>
    public bool MostrarOrgaoEmissor => _orgaoOuUfGravados || UsoOrgao != UsoCampoDocumento.Oculto;

    /// <summary>UF: conforme o tipo ou já preenchida.</summary>
    public bool MostrarUf => _orgaoOuUfGravados || UsoUfDoTipo != UsoCampoDocumento.Oculto;

    /// <summary>Data de emissão: conforme o tipo ou já preenchida.</summary>
    public bool MostrarEmissao => _emissaoGravada || UsoEmissaoDoTipo != UsoCampoDocumento.Oculto;

    public string RotuloOrgaoEmissor => UsoOrgao == UsoCampoDocumento.Obrigatorio ? "Órgão emissor (obrigatório)" : "Órgão emissor";
    public string RotuloUf => UsoUfDoTipo == UsoCampoDocumento.Obrigatorio ? "UF (obrigatória)" : "UF";
    public string RotuloEmissao => UsoEmissaoDoTipo == UsoCampoDocumento.Obrigatorio ? "Emitido em (obrigatório)" : "Emitido em";

    /// <summary>Data de emissão gravada: continua à vista mesmo num tipo que não a usa.</summary>
    private bool _emissaoGravada;

    // ---- Campos personalizados do tipo do documento (D4) ----

    private IReadOnlyList<CampoPersonalizadoDto> _campos = [];
    private IReadOnlyList<ValorPersonalizadoDto> _valoresGravados = [];

    /// <summary>Campos do tipo escolhido (mudam quando o tipo muda; o que já foi digitado num campo é mantido).</summary>
    public ObservableCollection<CampoPersonalizadoFormulario> CamposPersonalizados { get; } = new();

    public bool TemCamposPersonalizados => CamposPersonalizados.Count > 0;

    /// <summary>Chamado pela ficha ao incluir o documento: os campos personalizados de documentos (ativos).</summary>
    public void DefinirCampos(IReadOnlyList<CampoPersonalizadoDto> campos)
    {
        _campos = campos;
        MontarCampos();
    }

    partial void OnTipoChanged(Opcao<Guid> value) => MontarCampos();

    private void MontarCampos()
    {
        var digitados = CamposPersonalizados.Select(c => c.ParaDto()).OfType<ValorPersonalizadoDto>().ToList();
        var valores = digitados.Concat(_valoresGravados.Where(g => digitados.All(d => d.CampoId != g.CampoId))).ToList();
        CamposPersonalizados.Clear();
        foreach (var campo in _campos.Where(c => c.Ativo && c.Visivel && c.TipoDocumentoId == Tipo.Valor).OrderBy(c => c.Ordem))
        {
            var formulario = CampoPersonalizadoFormulario.Criar(campo, valores.FirstOrDefault(v => v.CampoId == campo.Id));
            formulario.Onde = "documento " + Tipo.Texto;
            CamposPersonalizados.Add(formulario);
        }
        OnPropertyChanged(nameof(TemCamposPersonalizados));
    }

    private TipoDocumentoDto? TipoDoCadastro => _catalogo.FirstOrDefault(t => t.Id == Tipo.Valor);

    public bool ExigeValidade => TipoDoCadastro?.ExigeValidade
        ?? TiposDocumentoSistema.Todos.Any(t => t.Id == Tipo.Valor && t.ExigeValidade);

    private int DiasAviso => TipoDoCadastro?.DiasAvisoVencimento ?? TipoDocumentoCadastro.DiasAvisoPadrao;

    public string RotuloValidade => ExigeValidade ? "Válido até (obrigatório)" : "Válido até";

    // ---- Dados ----

    [ObservableProperty] private string _numero = string.Empty;
    [ObservableProperty] private string _orgaoEmissor = string.Empty;
    [ObservableProperty] private string _uf = string.Empty;
    [ObservableProperty] private string _emitidoEm = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvisoValidade), nameof(TemAvisoValidade), nameof(Vencido))]
    private string _validoAte = string.Empty;

    [ObservableProperty] private string _observacoes = string.Empty;

    // ---- Vencimento ----

    private SituacaoValidade Situacao =>
        TextoTela.TentarData(ValidoAte, out var validade) ? RegrasDocumento.Situacao(validade, DiasAviso, Hoje) : SituacaoValidade.SemValidade;

    /// <summary>"Vence em 12 dias", "Vencido há 3 dias" (vazio = sem aviso). Só para documentos ativos.</summary>
    public string AvisoValidade =>
        Ativo && TextoTela.TentarData(ValidoAte, out var validade) ? RegrasDocumento.TextoSituacao(validade, DiasAviso, Hoje) : string.Empty;

    public bool TemAvisoValidade => AvisoValidade.Length > 0;
    public bool Vencido => Ativo && Situacao == SituacaoValidade.Vencido;
    public bool VenceEmBreve => Ativo && Situacao == SituacaoValidade.VenceEmBreve;

    // ---- Ativo / inativo ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visivel), nameof(Inativo), nameof(AvisoValidade), nameof(TemAvisoValidade), nameof(Vencido))]
    private bool _ativo = true;

    /// <summary>Ligado pela ficha em "Mostrar inativos".</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Visivel))] private bool _mostrarSeInativo;

    public bool Visivel => Ativo || MostrarSeInativo;
    public bool Inativo => !Ativo;

    [RelayCommand]
    private void Reativar() => Ativo = true;

    partial void OnAtivoChanged(bool value) => OnPropertyChanged(nameof(PodeAnexar));

    partial void OnMostrarSeInativoChanged(bool value)
    {
        foreach (var a in Anexos) a.MostrarSeInativo = value;
    }

    // ---- Anexos (gravados na hora, à parte do "Salvar" da ficha) ----

    private AcoesAnexos _acoes = new();

    /// <summary>Definido pela ficha ao incluir o documento: as ações de anexos ligadas pela tela.</summary>
    public AcoesAnexos Acoes
    {
        get => _acoes;
        set
        {
            _acoes = value;
            var dados = _anexosGravados;
            Anexos.Clear();
            foreach (var a in dados) Anexos.Add(new AnexoFormulario(a, value) { MostrarSeInativo = MostrarSeInativo });
        }
    }

    private IReadOnlyList<AnexoDto> _anexosGravados = [];

    public ObservableCollection<AnexoFormulario> Anexos { get; } = new();

    /// <summary>Só documento já gravado e ativo recebe anexos (o arquivo precisa de um documento no banco).</summary>
    public bool PodeAnexar => Gravado && Ativo;

    public string DicaAnexos => Gravado ? string.Empty : "Salve o cadastro para anexar arquivos a este documento.";

    [RelayCommand]
    private Task AnexarAsync() => Acoes.Anexar?.Invoke(this) ?? Task.CompletedTask;

    /// <summary>Chamado pela tela depois do envio.</summary>
    public void IncluirAnexo(AnexoDto dados) =>
        Anexos.Insert(0, new AnexoFormulario(dados, Acoes) { MostrarSeInativo = MostrarSeInativo });

    public static DocumentoFormulario De(DocumentoDto d)
    {
        var tipo = d.TipoDocumentoId != Guid.Empty ? d.TipoDocumentoId : TiposDocumentoSistema.Id(d.Tipo);
        var f = new DocumentoFormulario(d.Id, gravado: true)
        {
            _tipoGravado = tipo,
            _orgaoOuUfGravados = d.OrgaoEmissor is not null || d.Uf is not null,
            _emissaoGravada = d.EmitidoEm is not null,
            _anexosGravados = d.Anexos,
            _valoresGravados = d.ValoresPersonalizados,
            Numero = d.Numero,
            OrgaoEmissor = d.OrgaoEmissor ?? string.Empty,
            Uf = d.Uf ?? string.Empty,
            EmitidoEm = TextoTela.Data(d.EmitidoEm),
            ValidoAte = TextoTela.Data(d.ValidoAte),
            Observacoes = d.Observacoes ?? string.Empty,
            Ativo = d.Ativo
        };
        f.Tipos = TiposSemCadastro(tipo);
        f.Tipo = f.Tipos.First(o => o.Valor == tipo);
        return f;
    }

    /// <summary>Datas que não dá para entender e validade obrigatória (o resto a API valida).</summary>
    public IEnumerable<string> Validar() => ValidarComCampos().Select(e => e.Mensagem);

    /// <summary>Os mesmos erros, com o campo do documento (e o Id dele) para a tela levar até lá.</summary>
    public IEnumerable<ErroValidacao> ValidarComCampos()
    {
        if (Tipo.Valor == Guid.Empty)
        {
            yield return new("Documento" + (Numero.Length > 0 ? " " + Numero : string.Empty) + ": escolha o tipo.", CamposFichaPessoa.DocumentoTipo, Id);
            yield break;
        }
        var nome = Tipo.Texto + (Numero.Length > 0 ? " " + Numero : string.Empty);
        if (!TextoTela.TentarData(EmitidoEm, out _)) yield return new($"{nome}: data de emissão inválida (use dd/mm/aaaa).", CamposFichaPessoa.DocumentoEmitidoEm, Id);
        if (!TextoTela.TentarData(ValidoAte, out var validade)) yield return new($"{nome}: validade inválida (use dd/mm/aaaa).", CamposFichaPessoa.DocumentoValidoAte, Id);
        else if (Ativo && ExigeValidade && validade is null) yield return new($"{nome}: informe a validade.", CamposFichaPessoa.DocumentoValidoAte, Id);
        if (!Ativo) yield break;
        // Campos do tipo de documento: erro geral nesta fase.
        foreach (var problema in CamposPersonalizados.Select(c => c.Validar()).OfType<string>())
            yield return new(problema);
    }

    public DocumentoDto ParaDto()
    {
        TextoTela.TentarData(EmitidoEm, out var emitido);
        TextoTela.TentarData(ValidoAte, out var valido);
        return new DocumentoDto
        {
            Id = Id,
            TipoDocumentoId = Tipo.Valor,
            // Só por compatibilidade: a API copia o enum do tipo escolhido.
            Tipo = TipoDoCadastro is { } doCadastro
                ? doCadastro.TipoSistema ?? TipoDocumento.Outro
                : TiposDocumentoSistema.Todos.Where(t => t.Id == Tipo.Valor).Select(t => t.Tipo).DefaultIfEmpty(TipoDocumento.Outro).First(),
            Ativo = Ativo,
            Numero = Numero,
            OrgaoEmissor = TextoTela.Nulo(OrgaoEmissor),
            Uf = TextoTela.Nulo(Uf),
            EmitidoEm = emitido,
            ValidoAte = valido,
            Observacoes = TextoTela.Nulo(Observacoes),
            // Sem a lista de campos (falha ao ler), os valores gravados voltam intactos.
            ValoresPersonalizados = _campos.Count == 0
                ? [.. _valoresGravados]
                : CamposPersonalizados.Select(c => c.ParaDto()).OfType<ValorPersonalizadoDto>().ToList()
        };
    }
}
