using Lone.Cliente.ViewModels.Comum;
using Lone.Domain.Validacao;

namespace Lone.App.Controles;

/// <summary>Um campo que sabe mostrar o erro dele (borda e "⚠ mensagem" embaixo) e receber o foco.</summary>
public interface ICampoValidavel
{
    /// <summary>Mostra (texto) ou tira (nulo) a marca de erro.</summary>
    void MostrarErro(string? mensagem);

    /// <summary>Põe o foco no campo; falso se não deu (desligado, ainda sem controle nativo).</summary>
    bool Focar();
}

/// <summary>
/// Erro que leva ao campo (Lone Contextual, Fase 1), na tela. Três propriedades anexadas, sem código nas páginas (o mesmo
/// padrão de <see cref="Rolagem"/>):
/// <list type="bullet">
/// <item><c>c:Validacao.Resumo="{Binding Validacao}"</c> na <see cref="ScrollView"/> do formulário: ela passa a atender os
/// pedidos de "ir para o campo" do <see cref="ResumoValidacao"/> (rola até o campo e põe o foco) e a manter a marca dos
/// campos igual aos erros do resumo.</item>
/// <item><c>c:Validacao.Campo="identificacao.documento"</c> no controle (<see cref="ICampoValidavel"/>): o id estável do campo
/// (<c>CamposFichaPessoa</c>; nunca o rótulo).</item>
/// <item><c>c:Validacao.Item="{Binding Id}"</c>, só em cartões de lista: o Id do registro (endereço, documento...), ligado
/// explicitamente no XAML (nada de procurar "Id" por reflexão).</item>
/// </list>
/// O controle se registra no formulário mais próximo ao ser carregado e sai ao ser descarregado (cartões de lista vêm e vão).
/// </summary>
public static class Validacao
{
    public static readonly BindableProperty ResumoProperty = BindableProperty.CreateAttached(
        "Resumo", typeof(ResumoValidacao), typeof(Validacao), null, propertyChanged: ResumoMudou);

    public static readonly BindableProperty CampoProperty = BindableProperty.CreateAttached(
        "Campo", typeof(string), typeof(Validacao), null, propertyChanged: CampoMudou);

    public static readonly BindableProperty ItemProperty = BindableProperty.CreateAttached(
        "Item", typeof(object), typeof(Validacao), null, propertyChanged: (o, _, _) => Ligacao(o)?.Atualizar());

    private static readonly BindableProperty FormularioProperty =
        BindableProperty.CreateAttached("Formulario", typeof(Formulario), typeof(Validacao), null);

    private static readonly BindableProperty LigacaoCampoProperty =
        BindableProperty.CreateAttached("LigacaoCampo", typeof(LigacaoCampo), typeof(Validacao), null);

    public static ResumoValidacao? GetResumo(BindableObject o) => (ResumoValidacao?)o.GetValue(ResumoProperty);
    public static void SetResumo(BindableObject o, ResumoValidacao? valor) => o.SetValue(ResumoProperty, valor);
    public static string? GetCampo(BindableObject o) => (string?)o.GetValue(CampoProperty);
    public static void SetCampo(BindableObject o, string? valor) => o.SetValue(CampoProperty, valor);
    public static object? GetItem(BindableObject o) => o.GetValue(ItemProperty);
    public static void SetItem(BindableObject o, object? valor) => o.SetValue(ItemProperty, valor);

    /// <summary>O Id do registro ligado em <see cref="ItemProperty"/> (Guid ou nada).</summary>
    internal static Guid? ItemDe(BindableObject o) => GetItem(o) is Guid id ? id : null;

    private static LigacaoCampo? Ligacao(BindableObject o) => (LigacaoCampo?)o.GetValue(LigacaoCampoProperty);

    private static void ResumoMudou(BindableObject objeto, object antigo, object novo)
    {
        if (objeto is not ScrollView rolagem) return;
        var formulario = (Formulario?)rolagem.GetValue(FormularioProperty);
        if (formulario is null)
        {
            formulario = new Formulario(rolagem);
            rolagem.SetValue(FormularioProperty, formulario);
        }
        formulario.Trocar(novo as ResumoValidacao);
    }

    private static void CampoMudou(BindableObject objeto, object antigo, object novo)
    {
        if (objeto is not View controle || controle is not ICampoValidavel) return;
        if (Ligacao(controle) is null && novo is string { Length: > 0 })
            controle.SetValue(LigacaoCampoProperty, new LigacaoCampo(controle));
        Ligacao(controle)?.Atualizar();
    }

    /// <summary>Um controle com <c>Campo</c>: entra no formulário ao ser carregado e sai ao ser descarregado.</summary>
    private sealed class LigacaoCampo
    {
        private readonly View _controle;
        private Formulario? _formulario;

        public LigacaoCampo(View controle)
        {
            _controle = controle;
            controle.Loaded += (_, _) => Entrar();
            controle.Unloaded += (_, _) => Sair();
            if (controle.IsLoaded) Entrar();
        }

        public View Controle => _controle;
        public string? Campo => GetCampo(_controle);
        public Guid? Item => ItemDe(_controle);

        private void Entrar()
        {
            Sair();
            Element? pai = _controle.Parent;
            while (pai is not null && pai.GetValue(FormularioProperty) is not Formulario) pai = pai.Parent;
            _formulario = pai?.GetValue(FormularioProperty) as Formulario;
            _formulario?.Registrar(this);
            Atualizar();
        }

        private void Sair()
        {
            _formulario?.Remover(this);
            _formulario = null;
        }

        /// <summary>Marca (ou desmarca) o controle conforme os erros do resumo.</summary>
        public void Atualizar()
        {
            if (_controle is not ICampoValidavel campo) return;
            campo.MostrarErro(_formulario?.Resumo is { } resumo && Campo is { } id ? resumo.ErroDe(id, Item) : null);
        }
    }

    /// <summary>O formulário (a rolagem marcada com <c>Resumo</c>): os campos registrados e o "ir para".</summary>
    private sealed class Formulario
    {
        private const int Tentativas = 30;                    // ~1,5 s: a aba nova termina de aparecer
        private static readonly TimeSpan Intervalo = TimeSpan.FromMilliseconds(50);

        private readonly ScrollView _rolagem;
        private readonly List<LigacaoCampo> _campos = new();
        private int _pedido;

        public Formulario(ScrollView rolagem) => _rolagem = rolagem;

        public ResumoValidacao? Resumo { get; private set; }

        public void Trocar(ResumoValidacao? resumo)
        {
            if (Resumo is not null)
            {
                Resumo.Mudou -= AtualizarTodos;
                Resumo.FocoPedido -= IrPara;
            }
            Resumo = resumo;
            if (resumo is not null)
            {
                resumo.Mudou += AtualizarTodos;
                resumo.FocoPedido += IrPara;
            }
            AtualizarTodos();
        }

        public void Registrar(LigacaoCampo campo)
        {
            if (!_campos.Contains(campo)) _campos.Add(campo);
        }

        public void Remover(LigacaoCampo campo) => _campos.Remove(campo);

        private void AtualizarTodos()
        {
            foreach (var campo in _campos.ToList()) campo.Atualizar();
        }

        /// <summary>
        /// Leva ao campo do erro: espera a aba nova aparecer (o formulário já trocou a aba), rola até o controle e põe o foco.
        /// Sem o controle na tela depois das tentativas (campo que esta pessoa não mostra), não faz nada: o erro continua no
        /// resumo e marcado onde estiver.
        /// </summary>
        private async void IrPara(ErroValidacao erro)
        {
            var pedido = ++_pedido;
            try
            {
                for (var i = 0; i < Tentativas; i++)
                {
                    if (pedido != _pedido) return; // outro pedido no meio: vale o último
                    var alvo = _campos.FirstOrDefault(c => c.Campo == erro.Campo && c.Item == erro.Item && Visivel(c.Controle));
                    if (alvo is not null)
                    {
                        await _rolagem.ScrollToAsync(alvo.Controle, ScrollToPosition.Center, true);
                        if (alvo.Controle is ICampoValidavel campo && !campo.Focar())
                            SemanticScreenReader.Announce(erro.Mensagem);
                        return;
                    }
                    await Task.Delay(Intervalo);
                }
            }
            catch (Exception)
            {
                // Rolar e focar são ajuda: um controle que saiu da tela no meio do caminho não pode derrubar o formulário.
            }
        }

        /// <summary>Na tela de fato: ele e todos os pais visíveis, já medido.</summary>
        private static bool Visivel(View controle)
        {
            if (controle.Width <= 0 || controle.Handler is null) return false;
            for (Element? e = controle; e is not null; e = e.Parent)
                if (e is VisualElement { IsVisible: false }) return false;
            return true;
        }
    }
}
