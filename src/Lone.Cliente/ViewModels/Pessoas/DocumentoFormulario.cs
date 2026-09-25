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
    private bool _catalogoDefinido;

    public DocumentoFormulario() : this(IdSequencial.Novo(), gravado: false) { }

    private DocumentoFormulario(Guid id, bool gravado)
    {
        Id = id;
        Gravado = gravado;
        _tipos = TiposSemCadastro(Guid.Empty);
        _tipo = _tipos[0];
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
    [NotifyPropertyChangedFor(nameof(AvisoValidade), nameof(TemAvisoValidade), nameof(Vencido), nameof(RotuloValidade))]
    private Opcao<Guid> _tipo;

    /// <summary>Chamado pela ficha ao incluir o item: a lista de tipos do cadastro (vazia = não foi possível ler).</summary>
    public void DefinirCatalogo(IReadOnlyList<TipoDocumentoDto> catalogo)
    {
        var atual = _catalogoDefinido ? Tipo.Valor : _tipoGravado;
        _catalogo = catalogo;
        Tipos = catalogo.Count == 0
            ? TiposSemCadastro(_tipoGravado)
            :
            [
                .. catalogo
                    .Where(t => t.Ativo || t.Id == _tipoGravado)
                    .OrderBy(t => t.Ordem).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
                    .Select(t => new Opcao<Guid>(t.Id, t.Ativo ? t.Nome : t.Nome + " (desativado)"))
            ];
        if (Tipos.Length == 0) Tipos = TiposSemCadastro(_tipoGravado);
        Tipo = Tipos.FirstOrDefault(o => o.Valor == atual) ?? Tipos[0];
        _catalogoDefinido = true;
    }

    /// <summary>Sem o cadastro (falha ao ler): os tipos de sistema, mais o gravado se for outro — ele volta intacto.</summary>
    private static Opcao<Guid>[] TiposSemCadastro(Guid gravado)
    {
        var sistema = TiposDocumentoSistema.Todos.Select(t => new Opcao<Guid>(t.Id, t.Nome)).ToList();
        if (gravado != Guid.Empty && sistema.All(o => o.Valor != gravado))
            sistema.Insert(0, new Opcao<Guid>(gravado, "(tipo gravado)"));
        return [.. sistema];
    }

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
    public IEnumerable<string> Validar()
    {
        var nome = Tipo.Texto + (Numero.Length > 0 ? " " + Numero : string.Empty);
        if (!TextoTela.TentarData(EmitidoEm, out _)) yield return $"{nome}: data de emissão inválida (use dd/mm/aaaa).";
        if (!TextoTela.TentarData(ValidoAte, out var validade)) yield return $"{nome}: validade inválida (use dd/mm/aaaa).";
        else if (Ativo && ExigeValidade && validade is null) yield return $"{nome}: informe a validade.";
        if (!Ativo) yield break;
        foreach (var problema in CamposPersonalizados.Select(c => c.Validar()).OfType<string>())
            yield return problema;
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
