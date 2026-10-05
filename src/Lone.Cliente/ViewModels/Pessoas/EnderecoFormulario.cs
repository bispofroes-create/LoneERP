using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Enderecos;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Pessoas;
using CepValor = Lone.Domain.ObjetosDeValor.Cep;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Um endereço físico da pessoa, com as finalidades (usos) do cadastro de finalidades — cada uma podendo ser a
/// principal da pessoa para aquele uso — e o tipo do cadastro (Sede, Depósito...). O mesmo endereço não se cadastra
/// de novo para outro uso: acrescenta-se a finalidade. Remover um endereço já gravado só o desativa (histórico); ele
/// volta em "Mostrar inativos", sem principal (a principalidade antiga não volta sozinha). Um novo sai da lista.
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

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Visivel), nameof(Inativo), nameof(Resumo), nameof(PodeEditarFinalidades), nameof(PodeReativar), nameof(TemRevisao))]
    private bool _ativo = true;

    /// <summary>Endereço desativado perde todo principal; reativado, volta sem principal (o usuário define de novo).</summary>
    partial void OnAtivoChanged(bool value)
    {
        if (value) return;
        foreach (var f in Finalidades) f.Principal = false;
    }

    /// <summary>
    /// Somente leitura: duplicado já consolidado em outro endereço (fica inativo, no histórico, e não pode ser reativado:
    /// usa-se o endereço mantido). Só a consolidação no servidor preenche.
    /// </summary>
    public Guid? MescladoEmId { get; private set; }

    public bool Consolidado => MescladoEmId is not null;

    /// <summary>Inativo que pode voltar (o consolidado em outro não volta).</summary>
    public bool PodeReativar => !Ativo && MescladoEmId is null;

    // ---- Revisão deixada pela migração do cadastro antigo ----

    /// <summary>Motivos gravados pela migração (a ficha só pode apagar: "marcar como revisado").</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemRevisao))]
    private MotivoRevisaoEndereco _revisaoMigracao = MotivoRevisaoEndereco.Nenhum;
    public bool TemRevisao => Ativo && RevisaoMigracao != MotivoRevisaoEndereco.Nenhum;

    /// <summary>O usuário conferiu o endereço (decisão explícita): os motivos saem ao salvar.</summary>
    [RelayCommand]
    private void MarcarRevisado() => RevisaoMigracao = MotivoRevisaoEndereco.Nenhum;

    /// <summary>Ligado pela ficha em "Mostrar inativos".</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Visivel))]
    private bool _mostrarSeInativo;

    public bool Visivel => Ativo || MostrarSeInativo;
    public bool Inativo => !Ativo;

    [RelayCommand]
    private void Reativar()
    {
        if (PodeReativar) Ativo = true; // volta sem principal (o usuário define de novo)
    }

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

    /// <summary>O CEP (dígitos) que a última consulta conferiu; mudou o CEP, o resultado não vale mais.</summary>
    private string _cepVerificado = string.Empty;

    /// <summary>Município (IBGE) do CEP conferido e o texto dele ("Curvelo/MG"), para conferir com o escolhido.</summary>
    private int? _municipioDoCep;
    private string _localDoCep = string.Empty;

    /// <summary>"⚠ CEP não encontrado..." embaixo do CEP, logo depois da consulta (a gravação também recusa).</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemAvisoCep))] private string _avisoCep = string.Empty;
    public bool TemAvisoCep => AvisoCep.Length > 0;

    /// <summary>Como o endereço está gravado (nulo se é novo): só endereço novo ou alterado precisa estar completo.</summary>
    public PessoaEndereco? ComoGravado { get; private set; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Resumo))] private string _descricao = string.Empty;

    // ---- Finalidades (endereço × uso, com o principal de cada uso) ----

    private IReadOnlyList<FinalidadeEnderecoDto> _catalogoFinalidades = [];
    public static readonly Opcao<Guid?> SemFinalidade = new(null, "Escolha a finalidade");

    /// <summary>Todas as relações (inclusive as retiradas, que ficam como histórico e podem ser reativadas).</summary>
    public ObservableCollection<FinalidadeNoEndereco> Finalidades { get; } = new();

    /// <summary>Finalidades ativas do cadastro que ainda não estão ativas neste endereço (array: o Picker precisa de IList).</summary>
    [ObservableProperty] private Opcao<Guid?>[] _opcoesFinalidade = [SemFinalidade];
    [ObservableProperty] private Opcao<Guid?> _finalidadeEscolhida = SemFinalidade;

    /// <summary>Mensagem junto das finalidades (ex.: "Este endereço já possui esta finalidade.").</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemAvisoFinalidade))] private string _avisoFinalidade = string.Empty;
    public bool TemAvisoFinalidade => AvisoFinalidade.Length > 0;

    public bool PodeEditarFinalidades => Ativo;
    public bool SemFinalidades => Finalidades.All(f => !f.Ativo);

    /// <summary>Definido pela ficha: marcar/desmarcar principal (confere os outros endereços e pergunta antes de trocar).</summary>
    public Func<EnderecoFormulario, FinalidadeNoEndereco, Task>? AoPedirPrincipal { get; set; }

    /// <summary>Nome da finalidade pelo cadastro (desativada aparece com a marca).</summary>
    public string NomeFinalidade(Guid id) =>
        _catalogoFinalidades.FirstOrDefault(f => f.Id == id) is { } f ? (f.Ativo ? f.Nome : f.Nome + " (desativada)") : "(finalidade)";

    /// <summary>Chamado pela ficha ao incluir o endereço: o cadastro de finalidades (nomes e opções).</summary>
    public void DefinirFinalidades(IReadOnlyList<FinalidadeEnderecoDto> catalogo)
    {
        _catalogoFinalidades = catalogo;
        foreach (var f in Finalidades) f.Nome = NomeFinalidade(f.FinalidadeId);
        AtualizarOpcoesFinalidade();
    }

    private void AtualizarOpcoesFinalidade()
    {
        OpcoesFinalidade =
        [
            SemFinalidade,
            .. _catalogoFinalidades.Where(c => c.Ativo && !Finalidades.Any(f => f.Ativo && f.FinalidadeId == c.Id))
                .OrderBy(c => c.Ordem).Select(c => new Opcao<Guid?>(c.Id, c.Nome))
        ];
        FinalidadeEscolhida = SemFinalidade;
        OnPropertyChanged(nameof(SemFinalidades));
    }

    /// <summary>
    /// Acrescenta a finalidade escolhida. Já ativa neste endereço = aviso, nada muda. Retirada antes = a mesma relação
    /// é reativada (sem principal), sem criar outra linha histórica.
    /// </summary>
    [RelayCommand]
    private void AdicionarFinalidade() => AdicionarFinalidade(FinalidadeEscolhida.Valor);

    public FinalidadeNoEndereco? AdicionarFinalidade(Guid? finalidadeId)
    {
        AvisoFinalidade = string.Empty;
        if (finalidadeId is not { } id)
        {
            AvisoFinalidade = "Escolha a finalidade.";
            return null;
        }
        if (Finalidades.FirstOrDefault(f => f.FinalidadeId == id) is { } existente)
        {
            if (existente.Ativo)
            {
                AvisoFinalidade = "Este endereço já possui esta finalidade.";
                return existente;
            }
            existente.Ativo = true;
            existente.Principal = false;
            AtualizarOpcoesFinalidade();
            return existente;
        }
        var nova = new FinalidadeNoEndereco(IdSequencial.Novo(), id, gravada: false) { Nome = NomeFinalidade(id) };
        Incluir(nova);
        AtualizarOpcoesFinalidade();
        return nova;
    }

    private void Incluir(FinalidadeNoEndereco f)
    {
        f.AoRemover = () => RetirarFinalidade(f);
        f.AoAlternarPrincipal = () => AoPedirPrincipal?.Invoke(this, f) ?? Task.CompletedTask;
        Finalidades.Add(f);
    }

    /// <summary>Gravada: fica inativa (histórico) e perde o principal. Nova: sai da lista.</summary>
    public void RetirarFinalidade(FinalidadeNoEndereco f)
    {
        AvisoFinalidade = string.Empty;
        f.Principal = false;
        if (f.Gravada) f.Ativo = false;
        else Finalidades.Remove(f);
        AtualizarOpcoesFinalidade();
    }

    /// <summary>Resumo das finalidades ativas (ex.: "Entrega (principal) · Cobrança").</summary>
    public string TextoFinalidades =>
        string.Join(" · ", Finalidades.Where(f => f.Ativo).Select(f => f.Principal ? f.Nome + " (principal)" : f.Nome)) is { Length: > 0 } t
            ? t : "sem finalidade";

    // ---- Duplicidade (preenchido pela ficha) ----

    /// <summary>Endereço já cadastrado que parece ser este (nulo = nenhum). Pode ser um inativo (histórico) igual.</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(TemDuplicidade), nameof(DuplicidadeIncerta), nameof(IgualAInativo), nameof(TextoBotaoUsarExistente))]
    private EnderecoFormulario? _igualA;

    /// <summary>O igual é um endereço inativo: a ação é reativá-lo (não se cria outra linha física).</summary>
    public bool IgualAInativo => IgualA is { Ativo: false };
    public string TextoBotaoUsarExistente => IgualAInativo ? "Reativar endereço existente" : "Usar endereço existente";

    /// <summary>Verdadeiro = faltam dados para ter certeza (sem CEP ou bairro de um lado): o usuário confirma.</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(DuplicidadeIncerta))] private bool _duplicidadePossivel;

    /// <summary>O usuário disse que é outro endereço (só vale para a duplicidade incerta).</summary>
    public bool OutroEnderecoConfirmado { get; set; }

    public bool TemDuplicidade => IgualA is not null;
    public bool DuplicidadeIncerta => IgualA is not null && DuplicidadePossivel;

    public string TextoDuplicidade => IgualA is not { } e ? string.Empty
        : !e.Ativo
            ? $"Já existe um endereço igual cadastrado para esta pessoa, porém ele está inativo: {e.Resumo}."
            : (DuplicidadePossivel
                  ? $"Parece o mesmo endereço já cadastrado (falta CEP ou bairro para ter certeza): {e.Resumo}."
                  : $"Este endereço já está cadastrado para esta pessoa: {e.Resumo}.")
              + $" Finalidades atuais: {e.TextoFinalidades}.";

    partial void OnIgualAChanged(EnderecoFormulario? value) => OnPropertyChanged(nameof(TextoDuplicidade));
    partial void OnDuplicidadePossivelChanged(bool value) => OnPropertyChanged(nameof(TextoDuplicidade));

    /// <summary>Definidos pela ficha.</summary>
    public Action<EnderecoFormulario>? AoUsarExistente { get; set; }
    public Action<EnderecoFormulario>? AoConfirmarOutro { get; set; }
    public Action<EnderecoFormulario>? AoCancelarNovo { get; set; }

    /// <summary>"Cancelar" diante de um igual inativo: o endereço novo sai da ficha (nada é criado).</summary>
    [RelayCommand]
    private void CancelarNovo() => AoCancelarNovo?.Invoke(this);

    [RelayCommand]
    private void UsarExistente() => AoUsarExistente?.Invoke(this);

    [RelayCommand]
    private void ContinuarComOutro() => AoConfirmarOutro?.Invoke(this);

    /// <summary>Os dados físicos como a entidade do domínio (para a comparação normalizada de duplicidade).</summary>
    public PessoaEndereco ParaComparacao() => new()
    {
        Id = Id,
        Ativo = Ativo,
        Cep = Cep,
        Logradouro = Logradouro,
        Numero = Numero,
        Complemento = Complemento,
        Bairro = Bairro,
        MunicipioId = NoExterior ? null : Municipio.MunicipioId,
        Cidade = NoExterior ? Cidade : Municipio.Selecionado?.Nome ?? Municipio.Texto ?? string.Empty,
        Uf = NoExterior ? null : Municipio.Uf,
        CodigoPais = NoExterior ? CodigoPais : PessoaEndereco.CodigoPaisBrasil
    };

    [ObservableProperty] private string _cep = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Resumo))] private string _logradouro = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Resumo), nameof(SugerirSemNumero))] private string _numero = string.Empty;

    /// <summary>
    /// Caixa "Sem número": marcada, o número fica "S/N" (travado); desmarcada, volta vazio para digitar. Digitar "SN", "s/n"...
    /// também marca (o texto fica padronizado).
    /// </summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(SugerirSemNumero))] private bool _semNumero;

    /// <summary>
    /// Dica embaixo do número ("Este endereço não tem número? Marcar 'Sem número'") quando o texto não parece um número
    /// ("casa", "x", "0"). Não é erro: "KM 23" ou "100A" não mostram nada.
    /// </summary>
    public bool SugerirSemNumero => !SemNumero && NoBrasil && Lone.Domain.Enderecos.RegrasEndereco.PareceSemNumero(Numero);

    /// <summary>O link da dica: marca "Sem número" (o número vira "S/N").</summary>
    [RelayCommand]
    private void MarcarSemNumero() => SemNumero = true;

    /// <summary>Tocar no texto "Sem número" também marca/desmarca a caixa.</summary>
    [RelayCommand]
    private void AlternarSemNumero() => SemNumero = !SemNumero;

    partial void OnSemNumeroChanged(bool value)
    {
        if (value) Numero = Lone.Domain.Enderecos.RegrasEndereco.SemNumero;
        else if (Lone.Domain.Enderecos.RegrasEndereco.EhSemNumero(Numero)) Numero = string.Empty;
    }

    partial void OnNumeroChanged(string value)
    {
        var semNumero = Lone.Domain.Enderecos.RegrasEndereco.EhSemNumero(value);
        if (semNumero != SemNumero) SemNumero = semNumero;
        else if (semNumero && value != Lone.Domain.Enderecos.RegrasEndereco.SemNumero) Numero = Lone.Domain.Enderecos.RegrasEndereco.SemNumero;
    }
    [ObservableProperty] private string _complemento = string.Empty;
    [ObservableProperty] private string _bairro = string.Empty;

    /// <summary>Cidade digitada: só para endereço no exterior (no Brasil vale o município da lista).</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Resumo))] private string _cidade = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoBrasil), nameof(Resumo), nameof(SugerirSemNumero))]
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
        if (digitos != _cepVerificado) LimparConferenciaCep();
        if (digitos.Length != 8 || digitos == _cepConhecido || AoBuscarCep is null) return;
        _cepConhecido = digitos;
        _ = AoBuscarCep(this);
    }

    private void LimparConferenciaCep()
    {
        _cepVerificado = string.Empty;
        _municipioDoCep = null;
        _localDoCep = string.Empty;
        AvisoCep = string.Empty;
    }

    /// <summary>A consulta respondeu que o CEP não existe (sem internet não se chama isto: a gravação segue).</summary>
    public void MarcarCepInexistente()
    {
        var digitos = Digitos(Cep);
        LimparConferenciaCep();
        _cepVerificado = digitos;
        AvisoCep = $"⚠ CEP {Cep} não encontrado nos Correios. Confira o número.";
    }

    /// <summary>
    /// O CEP conferido pela consulta: inexistente, ou de outro município que não o escolhido, impede a gravação. Sem
    /// consulta (CEP gravado antes, ou sem internet), não há o que conferir.
    /// </summary>
    public string? ValidarCep(string rotulo)
    {
        if (!Ativo || NoExterior || _cepVerificado.Length == 0 || _cepVerificado != Digitos(Cep)) return null;
        if (TemAvisoCep) return $"{rotulo}: o CEP {Cep} não existe nos Correios. Confira o número.";
        if (_municipioDoCep is { } doCep && Municipio.MunicipioId is { } escolhido && escolhido != doCep)
            return $"{rotulo}: o CEP {Cep} é de {_localDoCep}, mas o município escolhido é outro. Confira o CEP ou o município.";
        return null;
    }

    private static string Digitos(string? texto) => new((texto ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

    public static EnderecoFormulario De(EnderecoDto e, string? municipioACorrigir = null)
    {
        var f = Criar(e);
        f.MunicipioACorrigir = municipioACorrigir ?? string.Empty;
        foreach (var u in e.Usos)
            f.Incluir(new FinalidadeNoEndereco(u.Id, u.FinalidadeId, gravada: true) { Principal = u.Principal, Ativo = u.Ativo });
        if (!f.NoExterior)
            f.Municipio.Definir(e.MunicipioId, e.MunicipioId is null ? null : e.Cidade, e.Uf is { Length: 2 } uf && uf != "EX" ? uf : null);
        f.ComoGravado = f.ParaComparacao();
        return f;
    }

    private static EnderecoFormulario Criar(EnderecoDto e) => new(e.Id, gravado: true)
    {
        _cepConhecido = Digitos(e.Cep),
        _tipoGravado = e.TipoEnderecoId,
        Descricao = e.Descricao ?? string.Empty,
        Observacoes = e.Observacoes ?? string.Empty,
        Ativo = e.Ativo,
        MescladoEmId = e.MescladoEmId,
        RevisaoMigracao = e.RevisaoMigracao,
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
        MescladoEmId = MescladoEmId, // a API mantém o gravado (consolidar é operação própria)
        RevisaoMigracao = RevisaoMigracao,
        // A fonte são os Usos (com o principal de cada finalidade); os bits legados a API é que calcula.
        Finalidades = FinalidadeEndereco.Nenhuma,
        Usos = Finalidades.Select(f => new FinalidadeDoEnderecoDto
        {
            Id = f.Id, FinalidadeId = f.FinalidadeId, Ativo = f.Ativo, Principal = Ativo && f.Ativo && f.Principal
        }).ToList(),
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

    /// <summary>Preenche com a consulta de CEP, sem apagar o que a consulta não trouxe.</summary>
    public void AplicarCep(DadosCep d)
    {
        _cepConhecido = Digitos(d.Cep);
        _cepVerificado = _cepConhecido;
        AvisoCep = string.Empty;
        Cep = CepValor.TentarCriar(d.Cep, out var cep) ? cep!.Formatado : d.Cep;
        if (d.Logradouro is not null) Logradouro = d.Logradouro;
        if (d.Bairro is not null) Bairro = d.Bairro;
        if (string.IsNullOrWhiteSpace(Complemento) && d.Complemento is not null) Complemento = d.Complemento;
        NoExterior = false;
        DefinirMunicipio(d.CodigoMunicipioIbge, d.Cidade, d.Uf);
        _municipioDoCep = int.TryParse(d.CodigoMunicipioIbge, out var codigo) ? codigo : null;
        _localDoCep = string.IsNullOrWhiteSpace(d.Uf) ? d.Cidade ?? string.Empty : $"{d.Cidade}/{d.Uf}";
    }

    public void AplicarCnpj(DadosCnpj d) => AplicarCnpj(d, new AplicacaoReceita(conferir: false));

    /// <summary>
    /// O endereço do CNPJ, campo a campo por <paramref name="r"/>: vazio é preenchido; com outro valor num cadastro gravado,
    /// fica para o usuário escolher na conferência "atual × Receita".
    /// </summary>
    public void AplicarCnpj(DadosCnpj d, AplicacaoReceita r)
    {
        var cepReceita = d.Cep is null ? null : CepValor.TentarCriar(d.Cep, out var cep) ? cep!.Formatado : d.Cep;
        r.Valor(new(CamposFichaPessoa.Cep, Id), "Endereço · CEP", Cep, cepReceita, v =>
        {
            _cepConhecido = Digitos(v);
            Cep = v;
        });
        r.Valor(new(CamposFichaPessoa.Logradouro, Id), "Endereço · Logradouro", Logradouro, d.Logradouro, v => Logradouro = v);
        r.Valor(new(CamposFichaPessoa.Numero, Id), "Endereço · Número", Numero, Lone.Domain.Enderecos.RegrasEndereco.NormalizarNumero(d.Numero), v => Numero = v);
        r.Valor(new(CamposFichaPessoa.Complemento, Id), "Endereço · Complemento", Complemento, d.Complemento, v => Complemento = v);
        r.Valor(new(CamposFichaPessoa.Bairro, Id), "Endereço · Bairro", Bairro, d.Bairro, v => Bairro = v);
        var municipioAtual = NoExterior ? Cidade : Municipio.Selecionado is { } m ? $"{m.Nome}/{m.Uf}" : string.Empty;
        var municipioReceita = string.IsNullOrWhiteSpace(d.Cidade) ? null : string.IsNullOrWhiteSpace(d.Uf) ? d.Cidade : $"{d.Cidade}/{d.Uf}";
        r.Valor(new(CamposFichaPessoa.Municipio, Id), "Endereço · Município", municipioAtual, municipioReceita, _ =>
        {
            NoExterior = false;
            DefinirMunicipio(d.CodigoMunicipioIbge, d.Cidade, d.Uf);
        });
    }

    // ---- Troca de empresa numa ficha nova (consulta de CNPJ refeita com outro CNPJ) ----

    /// <summary>
    /// O lugar do endereço como texto (CEP, logradouro, número, complemento, bairro, município/cidade, exterior): guardado
    /// depois da consulta de CNPJ para saber, na troca de empresa, se o usuário mexeu nele. Finalidades, principal e
    /// descrição não contam (não misturam um lugar com outro).
    /// </summary>
    public IReadOnlyList<string> Localizacao() =>
    [
        Cep, Logradouro, Numero, Complemento, Bairro,
        NoExterior ? Cidade : Municipio.Selecionado is { } m ? $"{m.Nome}/{m.Uf}" : $"{Municipio.Texto}/{Municipio.Uf}",
        NoExterior ? "exterior" : "Brasil"
    ];

    /// <summary>Mesmo lugar que <paramref name="guardada"/> (sem contar maiúsculas, espaços e pontuação de números).</summary>
    public bool MesmaLocalizacao(IReadOnlyList<string> guardada)
    {
        var atual = Localizacao();
        return atual.Count == guardada.Count && atual.Zip(guardada).All(p => AplicacaoReceita.Igual(p.First, p.Second));
    }

    /// <summary>Esvazia o lugar inteiro (o endereço da consulta anterior, sem alteração do usuário, recebe o da nova).</summary>
    public void LimparLocalizacao()
    {
        foreach (var campo in CamposDaLocalizacao) LimparCampoDaConsulta(campo);
    }

    /// <summary>Campos do endereço que a consulta de CNPJ preenche (chaves de <see cref="CamposFichaPessoa"/>).</summary>
    public static readonly IReadOnlyList<string> CamposDaLocalizacao =
    [
        CamposFichaPessoa.Cep, CamposFichaPessoa.Logradouro, CamposFichaPessoa.Numero, CamposFichaPessoa.Complemento,
        CamposFichaPessoa.Bairro, CamposFichaPessoa.Municipio
    ];

    /// <summary>Esvazia um campo que a consulta anterior preencheu (troca de empresa). Outros campos: nada.</summary>
    public void LimparCampoDaConsulta(string campo)
    {
        switch (campo)
        {
            case CamposFichaPessoa.Cep:
                _cepConhecido = string.Empty;
                Cep = string.Empty;
                LimparConferenciaCep();
                break;
            case CamposFichaPessoa.Logradouro: Logradouro = string.Empty; break;
            case CamposFichaPessoa.Numero: Numero = string.Empty; break; // desmarca "Sem número" (OnNumeroChanged)
            case CamposFichaPessoa.Complemento: Complemento = string.Empty; break;
            case CamposFichaPessoa.Bairro: Bairro = string.Empty; break;
            case CamposFichaPessoa.Municipio:
                NoExterior = false;
                Cidade = string.Empty;
                Municipio.Definir(null, null, null);
                break;
        }
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

/// <summary>
/// Uma finalidade de um endereço (ex.: Entrega), com a marca de principal da pessoa para esse uso. Retirar uma já
/// gravada a desativa (histórico); adicioná-la de novo reativa a mesma relação.
/// </summary>
public sealed partial class FinalidadeNoEndereco : ItemDeLista
{
    public FinalidadeNoEndereco(Guid id, Guid finalidadeId, bool gravada)
    {
        Id = id;
        FinalidadeId = finalidadeId;
        Gravada = gravada;
    }

    public Guid Id { get; }
    public Guid FinalidadeId { get; }

    /// <summary>Já existe no banco: retirar desativa em vez de tirar da lista.</summary>
    public bool Gravada { get; }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Texto))] private string _nome = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Texto), nameof(TextoBotaoPrincipal))] private bool _principal;
    [ObservableProperty] private bool _ativo = true;

    public string Texto => Principal ? $"{Nome} · principal" : Nome;
    public string TextoBotaoPrincipal => Principal ? "Deixar de ser principal" : "Tornar principal";

    /// <summary>Definido pelo endereço (a ficha confere os outros endereços antes de trocar o principal).</summary>
    public Func<Task>? AoAlternarPrincipal { get; set; }

    [RelayCommand]
    private Task AlternarPrincipalAsync() => AoAlternarPrincipal?.Invoke() ?? Task.CompletedTask;
}
