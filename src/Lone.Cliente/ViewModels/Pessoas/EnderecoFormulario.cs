using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Enderecos;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using CepValor = Lone.Domain.ObjetosDeValor.Cep;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Um endereço da pessoa, com as finalidades marcáveis (principal, fiscal, cobrança...) e o tipo do cadastro
/// (Sede, Depósito...). Remover um endereço já gravado só o desativa (histórico de entregas, notas antigas);
/// ele volta em "Mostrar inativos". Um endereço ainda não gravado sai da lista.
/// </summary>
public sealed partial class EnderecoFormulario : ItemDeLista
{
    private IReadOnlyList<TipoEnderecoDto> _catalogo = [];
    private Guid? _tipoGravado;
    private bool _catalogoDefinido;

    public EnderecoFormulario() : this(IdSequencial.Novo(), gravado: false) { }

    private EnderecoFormulario(Guid id, bool gravado)
    {
        Id = id;
        Gravado = gravado;
        Municipio.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SeletorMunicipio.Selecionado) or nameof(SeletorMunicipio.Uf))
                OnPropertyChanged(nameof(Resumo));
        };
    }

    /// <summary>UF + município da tabela do IBGE (endereço no Brasil).</summary>
    public SeletorMunicipio Municipio { get; } = new();

    /// <summary>Texto antigo do município que ainda precisa ser escolhido na lista (vazio = nada a corrigir).</summary>
    public string MunicipioACorrigir { get; private set; } = string.Empty;
    public bool TemMunicipioACorrigir => MunicipioACorrigir.Length > 0;

    /// <summary>Gerado no aparelho: um estabelecimento pode apontar para este endereço antes de gravar.</summary>
    public Guid Id { get; }

    /// <summary>Já existe no banco: remover desativa em vez de tirar da lista.</summary>
    public bool Gravado { get; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Visivel), nameof(Inativo), nameof(Resumo))]
    private bool _ativo = true;

    /// <summary>Ligado pela ficha em "Mostrar inativos".</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Visivel))]
    private bool _mostrarSeInativo;

    public bool Visivel => Ativo || MostrarSeInativo;
    public bool Inativo => !Ativo;

    [RelayCommand]
    private void Reativar() => Ativo = true;

    [ObservableProperty] private string _observacoes = string.Empty;

    // ---- Tipo (cadastro de tipos de endereço: Sede, Depósito...) ----

    public static readonly Opcao<Guid?> SemTipo = new(null, "—");

    /// <summary>"—" e os tipos ativos (mais o gravado, se desativado). Array: o Picker precisa de IList.</summary>
    [ObservableProperty] private Opcao<Guid?>[] _tipos = [SemTipo];

    [ObservableProperty] private Opcao<Guid?> _tipo = SemTipo;

    /// <summary>Chamado pela ficha ao incluir o item: a lista de tipos do cadastro.</summary>
    public void DefinirCatalogo(IReadOnlyList<TipoEnderecoDto> catalogo)
    {
        _catalogo = catalogo;
        var atual = _catalogoDefinido ? Tipo?.Valor : _tipoGravado;
        Tipos =
        [
            SemTipo,
            .. catalogo
                .Where(t => t.Ativo || t.Id == _tipoGravado)
                .OrderBy(t => t.Ordem).ThenBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Select(t => new Opcao<Guid?>(t.Id, t.Ativo ? t.Nome : t.Nome + " (desativado)"))
        ];
        Tipo = Tipos.FirstOrDefault(o => o.Valor == atual) ?? SemTipo;
        _catalogoDefinido = true;
    }

    /// <summary>Definido pela ficha: consulta o CEP digitado e preenche o endereço.</summary>
    public Func<EnderecoFormulario, Task>? AoBuscarCep { get; set; }

    /// <summary>Último CEP já consultado ou vindo do cadastro: não consulta de novo o mesmo número.</summary>
    private string _cepConhecido = string.Empty;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Resumo))] private string _descricao = string.Empty;
    [ObservableProperty] private bool _principal;
    [ObservableProperty] private bool _fiscal;
    [ObservableProperty] private bool _cobranca;
    [ObservableProperty] private bool _entrega;
    [ObservableProperty] private bool _correspondencia;
    [ObservableProperty] private bool _residencial;
    [ObservableProperty] private bool _comercial = true;

    [ObservableProperty] private string _cep = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Resumo))] private string _logradouro = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Resumo))] private string _numero = string.Empty;
    [ObservableProperty] private string _complemento = string.Empty;
    [ObservableProperty] private string _bairro = string.Empty;

    /// <summary>Cidade digitada: só para endereço no exterior (no Brasil vale o município da lista).</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Resumo))] private string _cidade = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoBrasil), nameof(Resumo))]
    private bool _noExterior;

    [ObservableProperty] private string _codigoPais = PessoaEndereco.CodigoPaisBrasil;
    [ObservableProperty] private string _pais = "Brasil";

    public bool NoBrasil => !NoExterior;

    partial void OnNoExteriorChanged(bool value)
    {
        if (value)
        {
            if (CodigoPais == PessoaEndereco.CodigoPaisBrasil) CodigoPais = string.Empty;
            if (Pais == "Brasil") Pais = string.Empty;
        }
        else
        {
            CodigoPais = PessoaEndereco.CodigoPaisBrasil;
            Pais = "Brasil";
        }
    }

    /// <summary>No Brasil, o município precisa ser escolhido da lista.</summary>
    public string? ValidarMunicipio(string rotulo)
    {
        if (NoExterior) return null;
        if (Municipio.Validar($"{rotulo} (município)") is { } pendente) return pendente;
        return Municipio.Escolhido ? null : $"{rotulo}: escolha a UF e o município na lista.";
    }

    /// <summary>Texto curto (ex.: escolha do endereço fiscal de uma filial).</summary>
    public string Resumo
    {
        get
        {
            var linha = string.Join(", ", new[] { Logradouro, Numero }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var local = NoExterior
                ? string.Join(" - ", new[] { Cidade, Pais }.Where(s => !string.IsNullOrWhiteSpace(s)))
                : Municipio.Selecionado is { } m ? $"{m.Nome}/{m.Uf}" : Municipio.Uf ?? string.Empty;
            var texto = string.Join(" - ", new[] { linha, local }.Where(s => s.Length > 0));
            if (texto.Length == 0) texto = "(endereço sem logradouro)";
            if (!string.IsNullOrWhiteSpace(Descricao)) texto = $"{Descricao}: {texto}";
            return Ativo ? texto : texto + " (inativo)";
        }
    }

    public override string ToString() => Resumo;

    /// <summary>Botão "Buscar CEP": consulta mesmo que o número já tenha sido consultado.</summary>
    [RelayCommand]
    private Task BuscarCepAsync()
    {
        _cepConhecido = Digitos(Cep);
        return AoBuscarCep?.Invoke(this) ?? Task.CompletedTask;
    }

    /// <summary>Ao completar os 8 dígitos, já confere o CEP (avisa na hora se não existir).</summary>
    partial void OnCepChanged(string value)
    {
        var digitos = Digitos(value);
        if (digitos.Length != 8 || digitos == _cepConhecido || AoBuscarCep is null) return;
        _cepConhecido = digitos;
        _ = AoBuscarCep(this);
    }

    private static string Digitos(string? texto) => new((texto ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

    public static EnderecoFormulario De(EnderecoDto e, string? municipioACorrigir = null)
    {
        var f = Criar(e);
        f.MunicipioACorrigir = municipioACorrigir ?? string.Empty;
        if (!f.NoExterior)
            f.Municipio.Definir(e.MunicipioId, e.MunicipioId is null ? null : e.Cidade, e.Uf is { Length: 2 } uf && uf != "EX" ? uf : null);
        return f;
    }

    private static EnderecoFormulario Criar(EnderecoDto e) => new(e.Id, gravado: true)
    {
        _cepConhecido = Digitos(e.Cep),
        _tipoGravado = e.TipoEnderecoId,
        Descricao = e.Descricao ?? string.Empty,
        Observacoes = e.Observacoes ?? string.Empty,
        Ativo = e.Ativo,
        Principal = e.Finalidades.HasFlag(FinalidadeEndereco.Principal),
        Fiscal = e.Finalidades.HasFlag(FinalidadeEndereco.Fiscal),
        Cobranca = e.Finalidades.HasFlag(FinalidadeEndereco.Cobranca),
        Entrega = e.Finalidades.HasFlag(FinalidadeEndereco.Entrega),
        Correspondencia = e.Finalidades.HasFlag(FinalidadeEndereco.Correspondencia),
        Residencial = e.Finalidades.HasFlag(FinalidadeEndereco.Residencial),
        Comercial = e.Finalidades.HasFlag(FinalidadeEndereco.Comercial),
        Cep = CepValor.TentarCriar(e.Cep, out var cep) ? cep!.Formatado : e.Cep ?? string.Empty,
        Logradouro = e.Logradouro,
        Numero = e.Numero ?? string.Empty,
        Complemento = e.Complemento ?? string.Empty,
        Bairro = e.Bairro ?? string.Empty,
        NoExterior = e.CodigoPais != PessoaEndereco.CodigoPaisBrasil,
        Cidade = e.CodigoPais != PessoaEndereco.CodigoPaisBrasil ? e.Cidade : string.Empty,
        CodigoPais = e.CodigoPais,
        Pais = e.Pais
    };

    public EnderecoDto ParaDto(int ordem) => new()
    {
        Id = Id,
        Descricao = TextoTela.Nulo(Descricao),
        // Sem a lista de tipos (falha ao ler), o tipo gravado volta intacto.
        TipoEnderecoId = _catalogo.Count > 0 ? Tipo?.Valor : _tipoGravado,
        Observacoes = TextoTela.Nulo(Observacoes),
        Ativo = Ativo,
        // Inativo não é principal (o servidor também garante).
        Finalidades = Ativo ? Finalidades() : Finalidades() & ~FinalidadeEndereco.Principal,
        Ordem = ordem,
        Cep = TextoTela.Nulo(Cep),
        Logradouro = Logradouro,
        Numero = TextoTela.Nulo(Numero),
        Complemento = TextoTela.Nulo(Complemento),
        Bairro = TextoTela.Nulo(Bairro),
        // No Brasil vale o Id do município; nome, UF e código IBGE são copiados do IBGE pela API.
        MunicipioId = NoExterior ? null : Municipio.MunicipioId,
        Cidade = NoExterior ? Cidade : Municipio.Selecionado?.Nome ?? Municipio.Texto,
        Uf = NoExterior ? null : Municipio.Uf,
        CodigoMunicipioIbge = NoExterior ? null : Municipio.MunicipioId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        CodigoPais = NoExterior ? CodigoPais : PessoaEndereco.CodigoPaisBrasil,
        Pais = NoExterior ? Pais : "Brasil"
    };

    private FinalidadeEndereco Finalidades()
    {
        var total = FinalidadeEndereco.Nenhuma;
        if (Principal) total |= FinalidadeEndereco.Principal;
        if (Fiscal) total |= FinalidadeEndereco.Fiscal;
        if (Cobranca) total |= FinalidadeEndereco.Cobranca;
        if (Entrega) total |= FinalidadeEndereco.Entrega;
        if (Correspondencia) total |= FinalidadeEndereco.Correspondencia;
        if (Residencial) total |= FinalidadeEndereco.Residencial;
        if (Comercial) total |= FinalidadeEndereco.Comercial;
        return total;
    }

    /// <summary>Preenche com a consulta de CEP, sem apagar o que a consulta não trouxe.</summary>
    public void AplicarCep(DadosCep d)
    {
        _cepConhecido = Digitos(d.Cep);
        Cep = CepValor.TentarCriar(d.Cep, out var cep) ? cep!.Formatado : d.Cep;
        if (d.Logradouro is not null) Logradouro = d.Logradouro;
        if (d.Bairro is not null) Bairro = d.Bairro;
        if (string.IsNullOrWhiteSpace(Complemento) && d.Complemento is not null) Complemento = d.Complemento;
        NoExterior = false;
        DefinirMunicipio(d.CodigoMunicipioIbge, d.Cidade, d.Uf);
    }

    public void AplicarCnpj(DadosCnpj d)
    {
        if (d.Cep is not null) _cepConhecido = Digitos(d.Cep);
        if (d.Cep is not null) Cep = CepValor.TentarCriar(d.Cep, out var cep) ? cep!.Formatado : d.Cep;
        if (d.Logradouro is not null) Logradouro = d.Logradouro;
        if (d.Numero is not null) Numero = d.Numero;
        if (d.Complemento is not null) Complemento = d.Complemento;
        if (d.Bairro is not null) Bairro = d.Bairro;
        NoExterior = false;
        DefinirMunicipio(d.CodigoMunicipioIbge, d.Cidade, d.Uf);
    }

    /// <summary>
    /// CEP e CNPJ trazem o código IBGE: o município já vem escolhido. Sem código, só a UF é preenchida e o
    /// nome fica digitado para o usuário escolher na lista (nunca vira município "de texto").
    /// </summary>
    private void DefinirMunicipio(string? codigoIbge, string? nome, string? uf)
    {
        if (int.TryParse(codigoIbge, out var codigo) && !string.IsNullOrWhiteSpace(nome))
        {
            Municipio.Definir(codigo, nome, uf);
            return;
        }
        if (!string.IsNullOrWhiteSpace(uf)) Municipio.Uf = uf.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(nome)) Municipio.Texto = nome;
    }
}
