using CommunityToolkit.Mvvm.ComponentModel;

namespace Lone.Cliente.Navegacao;

/// <summary>
/// Uma tela que sabe dizer qual registro está aberto nela e abrir outro (ou nenhum) quando a navegação pede. Nos cadastros
/// "lista + ficha" quem implementa é a base (<c>CadastroViewModelBase</c>): todas as telas ganham ao mesmo tempo.
/// </summary>
public interface ITelaNavegavel
{
    /// <summary>Registro aberto agora (nulo = a lista da tela, sem ficha).</summary>
    ReferenciaRegistro? RegistroAberto { get; }

    /// <summary>
    /// Deixa a tela no registro pedido (nulo = fecha a ficha). Pergunta antes, como sempre, se houver alterações não
    /// salvas. Verdadeiro se chegou.
    /// </summary>
    Task<bool> IrParaRegistroAsync(ReferenciaRegistro? registro);
}

/// <summary>O que o aplicativo (MAUI/Shell) oferece à navegação: trocar de tela e dizer qual está aberta.</summary>
public interface IPlataformaNavegacao
{
    /// <summary>Rota da tela aberta (ex.: "pessoas").</summary>
    string? RotaAtual { get; }

    /// <summary>A tela aberta, se ela participa da navegação por registros.</summary>
    ITelaNavegavel? TelaAtual { get; }

    /// <summary>Vai para a tela (o Shell pergunta antes se houver alterações não salvas e pode não ir).</summary>
    Task IrParaRotaAsync(string rota);
}

/// <summary>
/// Motor global de navegação do Lone: o único caminho para "ir a" e "voltar". Separa as perguntas que um ERP maduro separa:
/// <list type="bullet">
/// <item>"Para onde quero ir?" — o menu (navegação global) pede ao motor com <see cref="OrigemNavegacao.Menu"/>.</item>
/// <item>"De onde eu vim?" — o <see cref="HistoricoNavegacao"/> e o <see cref="VoltarAsync"/>.</item>
/// <item>"Qual registro agora?" — trocar de registro pela lista não vira histórico (substitui).</item>
/// <item>"O que eu estava fazendo?" — registros recentes da sessão (<see cref="RegistrosRecentes"/>).</item>
/// </list>
/// O histórico é montado pelo que <i>de fato</i> aconteceu (a tela informa o que abriu), não pelo que foi pedido: fechar a
/// ficha pelo botão Fechar, pelo voltar do celular ou pelo Voltar dá no mesmo. Tudo em memória (nada de banco), limpo a
/// cada troca de contexto (entrada, saída, outra empresa). Não guarda referência a telas: pergunta à plataforma qual está
/// aberta, então tela fechada ou trocada nunca recebe chamada atrasada.
/// </summary>
public sealed partial class GerenciadorNavegacao : ObservableObject
{
    /// <summary>Registros recentes guardados na sessão (futuro: "Recentes" de registros no menu e na busca).</summary>
    public const int MaximoRecentes = 10;

    /// <summary>O motor do aplicativo (o mesmo registrado na injeção de dependência). As telas usam este.</summary>
    public static GerenciadorNavegacao Padrao { get; } = new();

    private readonly HistoricoNavegacao _historico = new();
    private readonly List<ReferenciaRegistro> _recentes = [];
    private readonly TimeProvider _relogio;
    private IPlataformaNavegacao? _plataforma;
    private bool _emCurso;
    private DateTimeOffset _ultimoVoltar = DateTimeOffset.MinValue;

    /// <summary>
    /// Pedidos de Voltar mais próximos que isto contam como um só (duplo clique no ←, tecla repetida): voltar duas vezes
    /// tem de ser intencional.
    /// </summary>
    public static readonly TimeSpan IntervaloMinimoVoltar = TimeSpan.FromMilliseconds(400);

    public GerenciadorNavegacao() : this(TimeProvider.System) { }

    public GerenciadorNavegacao(TimeProvider relogio) => _relogio = relogio;

    /// <summary>A tela ainda pode ser aberta pelo usuário (permissões atuais do menu). Nulo = todas.</summary>
    public Func<string, bool>? RotaPermitida { get; set; }

    /// <summary>Nome da tela para "Voltar para …" (ex.: "Cadastro de pessoas"). Nulo = a própria rota.</summary>
    public Func<string, string?>? TituloDaRota { get; set; }

    public IReadOnlyList<EntradaNavegacao> Historico => _historico.Entradas;
    public EntradaNavegacao? Atual => _historico.Atual;
    public EntradaNavegacao? Anterior => _historico.Anterior;

    /// <summary>Há para onde voltar (o botão Voltar aparece habilitado) — contando só telas que o perfil ainda abre.</summary>
    public bool PodeVoltar => AlvoDoVoltar is not null;

    /// <summary>Texto do Voltar (dica e leitor de tela): diz para onde vai, não só "Voltar".</summary>
    public string DescricaoVoltar => AlvoDoVoltar is { } alvo ? $"Voltar para {alvo.Titulo}" : "Nada para voltar";

    /// <summary>A entrada para onde o Voltar leva: a anterior que ainda pode ser aberta.</summary>
    private EntradaNavegacao? AlvoDoVoltar
    {
        get
        {
            for (var i = _historico.Entradas.Count - 2; i >= 0; i--)
                if (RotaPermitida?.Invoke(_historico.Entradas[i].Local.Rota) != false) return _historico.Entradas[i];
            return null;
        }
    }

    /// <summary>Há um Shell ligado (fora dele — testes de tela, telas de entrada — as telas usam o caminho antigo).</summary>
    public bool Conectado => _plataforma is not null;

    /// <summary>Uma navegação está em andamento (novos pedidos são ignorados: duplo clique não duplica nada).</summary>
    public bool Navegando => _emCurso;

    /// <summary>Registros abertos nesta sessão, o mais recente primeiro (só em memória).</summary>
    public IReadOnlyList<ReferenciaRegistro> RegistrosRecentes => _recentes;

    // ---- Ligação com o aplicativo ----

    /// <summary>O Shell novo assume a navegação (histórico recomeça: outro contexto, outras permissões).</summary>
    public void Conectar(IPlataformaNavegacao plataforma)
    {
        _plataforma = plataforma;
        Limpar();
    }

    /// <summary>O Shell saiu (login, outra empresa): solta a plataforma e esquece o histórico.</summary>
    public void Desconectar(IPlataformaNavegacao plataforma)
    {
        if (!ReferenceEquals(_plataforma, plataforma)) return;
        _plataforma = null;
        Limpar();
    }

    public void Limpar()
    {
        _historico.Limpar();
        _recentes.Clear();
        Avisar();
    }

    // ---- O que aconteceu (a plataforma e as telas informam) ----

    /// <summary>O Shell terminou de trocar de tela (fora de uma navegação do motor, ex.: a primeira tela).</summary>
    public void AoChegarNaTela(OrigemNavegacao origem = OrigemNavegacao.Sistema)
    {
        if (!_emCurso) Reconciliar(origem);
    }

    /// <summary>
    /// A tela abriu, trocou ou fechou o registro. Só vale para a tela aberta (uma tela em segundo plano que termina de
    /// carregar depois que o usuário saiu não mexe no histórico).
    /// </summary>
    public void InformarRegistro(ITelaNavegavel tela, OrigemNavegacao origem = OrigemNavegacao.Lista)
    {
        if (_emCurso || _plataforma is not { } p || !ReferenceEquals(p.TelaAtual, tela)) return;
        Reconciliar(origem);
    }

    // ---- Pedidos ----

    /// <summary>Vai para uma tela como ela está (o menu faz assim: a ficha que estava aberta lá continua aberta).</summary>
    public Task<bool> IrParaTelaAsync(string rota, OrigemNavegacao origem) => IrAsync(new LocalNavegacao(rota), origem, ajustarRegistro: false);

    /// <summary>
    /// Vai a um lugar: a tela e, se informado, o registro (links internos, "Abrir", busca, endereço lone://). Verdadeiro se
    /// chegou. Sem permissão para a tela, não vai.
    /// </summary>
    public Task<bool> AbrirAsync(LocalNavegacao destino, OrigemNavegacao origem) =>
        IrAsync(destino, origem, ajustarRegistro: destino.Registro is not null);

    /// <summary>Abre um endereço interno (<c>lone://pessoas/pessoa/{id}</c>). Falso se o endereço não for do Lone.</summary>
    public Task<bool> AbrirEnderecoAsync(string endereco) =>
        LocalNavegacao.TentarLer(endereco, out var local) ? AbrirAsync(local, OrigemNavegacao.Endereco) : Task.FromResult(false);

    /// <summary>
    /// Volta ao lugar anterior do histórico — o real, não o "pai" lógico. Pula telas que o usuário não pode mais abrir.
    /// Se houver alterações não salvas, a tela pergunta antes (e o usuário pode ficar). Verdadeiro se voltou.
    /// </summary>
    public async Task<bool> VoltarAsync()
    {
        if (_emCurso || _plataforma is not { } p) return false;
        var agora = _relogio.GetUtcNow();
        if (agora - _ultimoVoltar < IntervaloMinimoVoltar) return false; // segundo clique de um duplo clique
        _ultimoVoltar = agora;

        // Tela que deixou de ser permitida (permissões mudaram): sai do caminho de volta.
        while (Anterior is { } candidata && RotaPermitida?.Invoke(candidata.Local.Rota) == false)
            _historico.Remover(_historico.Entradas.Count - 2);
        if (Anterior is not { } alvo)
        {
            Avisar();
            return false;
        }

        var indice = _historico.Entradas.Count - 2;
        var antes = LocalDaTela(p);
        var chegou = false;
        _emCurso = true;
        Avisar();
        try
        {
            chegou = await ChegarAsync(p, alvo.Local, ajustarRegistro: true);
        }
        finally
        {
            _emCurso = false;
            if (ReferenceEquals(p, _plataforma) && LocalDaTela(p) is { } depois)
            {
                // Chegou à tela de volta, mesmo que o registro não abra mais (ex.: excluído): a entrada de volta vira o
                // que de fato abriu, em vez de empilhar um passo novo.
                if (!depois.MesmoQue(antes) && string.Equals(depois.Rota, alvo.Local.Rota, StringComparison.Ordinal))
                {
                    _historico.VoltarPara(indice, depois);
                    LembrarRecente(depois);
                }
                else
                    Registrar(depois, OrigemNavegacao.Voltar);
            }
            Avisar();
        }
        return chegou;
    }

    // ---- Interno ----

    private async Task<bool> IrAsync(LocalNavegacao destino, OrigemNavegacao origem, bool ajustarRegistro)
    {
        if (_emCurso || _plataforma is not { } p) return false;
        if (RotaPermitida?.Invoke(destino.Rota) == false) return false;

        _emCurso = true;
        Avisar();
        try
        {
            return await ChegarAsync(p, destino, ajustarRegistro);
        }
        finally
        {
            _emCurso = false;
            if (ReferenceEquals(p, _plataforma)) Reconciliar(origem, soTela: destino.Registro is null);
            Avisar();
        }
    }

    /// <summary>Troca de tela (se preciso) e ajusta o registro. Para no meio se o Shell foi trocado ou o usuário ficou.</summary>
    private async Task<bool> ChegarAsync(IPlataformaNavegacao p, LocalNavegacao destino, bool ajustarRegistro)
    {
        if (!string.Equals(p.RotaAtual, destino.Rota, StringComparison.Ordinal))
        {
            await p.IrParaRotaAsync(destino.Rota);
            if (!ReferenceEquals(p, _plataforma) || !string.Equals(p.RotaAtual, destino.Rota, StringComparison.Ordinal))
                return false; // outro contexto no meio do caminho, ou o usuário escolheu continuar editando
        }

        if (!ajustarRegistro) return true;
        if (p.TelaAtual is not { } tela) return destino.Registro is null;
        if (MesmoRegistro(tela.RegistroAberto, destino.Registro)) return true;

        await tela.IrParaRegistroAsync(destino.Registro);
        return ReferenceEquals(p, _plataforma) && ReferenceEquals(p.TelaAtual, tela) && MesmoRegistro(tela.RegistroAberto, destino.Registro);
    }

    private static bool MesmoRegistro(ReferenciaRegistro? a, ReferenciaRegistro? b) => a is null ? b is null : a.MesmoQue(b);

    private static LocalNavegacao? LocalDaTela(IPlataformaNavegacao p) =>
        p.RotaAtual is { Length: > 0 } rota ? new LocalNavegacao(rota, p.TelaAtual?.RegistroAberto) : null;

    private void Reconciliar(OrigemNavegacao origem, bool soTela = false)
    {
        if (_plataforma is { } p && LocalDaTela(p) is { } local) Registrar(local, origem, soTela);
    }

    private void Registrar(LocalNavegacao local, OrigemNavegacao origem, bool soTela = false)
    {
        var titulo = TituloDaRota?.Invoke(local.Rota) ?? local.Rota;
        if (_historico.Registrar(new EntradaNavegacao(local, origem, titulo, _relogio.GetUtcNow(), SoTela: soTela))) Avisar();
        LembrarRecente(local);
    }

    private void LembrarRecente(LocalNavegacao local)
    {
        if (local.Registro is not { Novo: false } registro) return;
        _recentes.RemoveAll(r => r.MesmoQue(registro));
        _recentes.Insert(0, registro);
        if (_recentes.Count > MaximoRecentes) _recentes.RemoveAt(_recentes.Count - 1);
    }

    private void Avisar()
    {
        OnPropertyChanged(nameof(PodeVoltar));
        OnPropertyChanged(nameof(DescricaoVoltar));
        OnPropertyChanged(nameof(Navegando));
        OnPropertyChanged(nameof(Atual));
    }
}
