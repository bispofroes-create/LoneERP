using Lone.Cliente.Mensagens;
using Lone.Cliente.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace Lone.App.Controles;

/// <summary>
/// Camada global de mensagens (Fase 1: toasts de sucesso). Está no modelo de todas as páginas (<c>PaginaComMensagens</c>,
/// Estilos.xaml), por cima do conteúdo e fora de qualquer rolagem: a mensagem aparece onde a pessoa está, sem voltar ao topo.
/// <para>
/// Computador: canto inferior direito, acima da barra de ações das fichas (Salvar/Descartar/Fechar e o motivo da alteração
/// ficam livres). Janela estreita: a largura da janela menos as margens. Celular: embaixo, na largura toda (margem de 16).
/// Só ocupa o espaço dos toasts — sem toast, some — e não desloca nada: fora deles, a página continua recebendo toque,
/// clique e teclado.
/// </para>
/// Só desenha o que o <see cref="ServicoMensagens"/> manda. Inscreve-se ao entrar na tela e sai ao deixar a tela (sem
/// referência presa a páginas fechadas). O anúncio ao leitor de tela é feito uma vez só, pelo App (várias páginas podem ter a
/// camada ao mesmo tempo: a do Shell e a de uma janela modal).
/// </summary>
public sealed class CamadaMensagens : VerticalStackLayout
{
    private readonly ServicoMensagens _servico;
    private readonly bool _celular = DeviceInfo.Current.Idiom == DeviceIdiom.Phone;
    private readonly Dictionary<Guid, Toast> _toasts = [];
    private VisualElement? _area;

    public CamadaMensagens() : this(ServicoMensagens.Padrao) { }

    public CamadaMensagens(ServicoMensagens servico)
    {
        _servico = servico;
        Spacing = 8;
        IsVisible = false;
        VerticalOptions = LayoutOptions.End;
        Posicionar();

        Loaded += (_, _) =>
        {
            _servico.Mudou += ServicoMudou;
            Sincronizar();
        };
        Unloaded += (_, _) => _servico.Mudou -= ServicoMudou;
        ParentChanged += (_, _) =>
        {
            if (_area is not null) _area.SizeChanged -= AreaMudou;
            _area = Parent as VisualElement;
            if (_area is not null) _area.SizeChanged += AreaMudou;
            Posicionar();
        };
    }

    private void AreaMudou(object? sender, EventArgs e) => Posicionar();

    /// <summary>Aplica a regra de <see cref="PosicaoToast"/> à largura atual da página.</summary>
    private void Posicionar()
    {
        var largura = _area is { Width: > 0 } area ? area.Width : PosicaoToast.LarguraComputador * 2; // antes da 1ª medida
        var posicao = PosicaoToast.Calcular(largura, _celular);
        HorizontalOptions = posicao.AlinhadaADireita ? LayoutOptions.End : LayoutOptions.Fill;
        WidthRequest = posicao.Largura ?? -1;
        Margin = new Thickness(posicao.MargemEsquerda, 0, posicao.MargemDireita, posicao.MargemInferior);
    }

    private void ServicoMudou(object? sender, EventArgs e)
    {
        // O fim do tempo chega pela thread do temporizador.
        if (Dispatcher.IsDispatchRequired) Dispatcher.Dispatch(Sincronizar);
        else Sincronizar();
    }

    /// <summary>Deixa a pilha igual ao serviço: tira os que saíram, atualiza os que ficaram e acrescenta os novos embaixo.</summary>
    private void Sincronizar()
    {
        var visiveis = _servico.Visiveis;
        var ids = visiveis.Select(m => m.Id).ToHashSet();

        foreach (var (id, toast) in _toasts.Where(t => !ids.Contains(t.Key)).ToList())
        {
            Children.Remove(toast);
            _toasts.Remove(id);
        }

        foreach (var mensagem in visiveis)
        {
            if (_toasts.TryGetValue(mensagem.Id, out var existente))
            {
                existente.Atualizar();
                continue;
            }
            var toast = new Toast(mensagem, _servico);
            _toasts[mensagem.Id] = toast;
            Children.Add(toast); // o mais novo fica embaixo, mais perto do canto (sem animação: nada atrapalha a leitura)
        }

        IsVisible = _toasts.Count > 0;
    }

    /// <summary>Um toast: ícone + texto (nunca só a cor), contagem de repetidas, a ação (se houver) e fechar.</summary>
    private sealed class Toast : Border
    {
        private readonly MensagemUsuario _mensagem;
        private readonly Label _contagem;

        public Toast(MensagemUsuario mensagem, ServicoMensagens servico)
        {
            _mensagem = mensagem;
            var (icone, nomeTipo) = Descrever(mensagem.Tipo);

            StrokeShape = new RoundRectangle { CornerRadius = 10 };
            Padding = new Thickness(16, 8, 6, 8);
            this.SetAppThemeColor(BackgroundColorProperty, Cor("ToastFundo"), Cor("ToastFundoEscuro"));
            this.SetAppThemeColor(StrokeProperty, Cor("ToastFundo"), Cor("ToastBordaEscuro"));
            StrokeThickness = 1;
            Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.25f, Radius = 16, Offset = new Point(0, 6) };
            SemanticProperties.SetDescription(this, $"{nomeTipo}: {mensagem.Texto}");

            var grade = new Grid
            {
                ColumnSpacing = 10,
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto), // ícone
                    new ColumnDefinition(GridLength.Star), // texto
                    new ColumnDefinition(GridLength.Auto), // contagem
                    new ColumnDefinition(GridLength.Auto), // ação
                    new ColumnDefinition(GridLength.Auto)  // fechar
                }
            };

            var rotuloIcone = new Label
            {
                Text = icone, FontSize = 16, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center,
                TextColor = Cor(mensagem.Tipo == TipoMensagem.Sucesso ? "ToastIconeSucesso" : "ToastTexto")
            };

            var texto = new Label
            {
                Text = mensagem.Texto, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap, VerticalOptions = LayoutOptions.Center
            };
            texto.SetAppThemeColor(Label.TextColorProperty, Cor("ToastTexto"), Cor("ToastTextoEscuro"));

            _contagem = new Label
            {
                FontSize = 12, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center,
                TextColor = Cor("ToastSecundario")
            };

            grade.Add(rotuloIcone, 0);
            grade.Add(texto, 1);
            grade.Add(_contagem, 2);

            if (mensagem.Acao is { } acao)
            {
                var botaoAcao = new Button { Text = acao.Texto, Style = Estilo("BotaoToast") };
                SemanticProperties.SetDescription(botaoAcao, acao.Descricao ?? acao.Texto);
                botaoAcao.Clicked += (_, _) => _ = servico.ExecutarAcaoAsync(mensagem.Id);
                SegurarTempo(botaoAcao, servico);
                grade.Add(botaoAcao, 3);
            }

            var fechar = new Button { Text = "✕", Style = Estilo("BotaoToastFechar") };
            ToolTipProperties.SetText(fechar, "Fechar o aviso");
            SemanticProperties.SetDescription(fechar, "Fechar o aviso");
            fechar.Clicked += (_, _) => servico.Dispensar(mensagem.Id);
            SegurarTempo(fechar, servico);
            grade.Add(fechar, 4);

            // Ponteiro sobre o toast: o tempo para (dá para ler até o fim ou alcançar o botão).
            var ponteiro = new PointerGestureRecognizer();
            ponteiro.PointerEntered += (_, _) => servico.Pausar(mensagem.Id);
            ponteiro.PointerExited += (_, _) => servico.Retomar(mensagem.Id);
            GestureRecognizers.Add(ponteiro);

            Content = grade;
            Atualizar();
        }

        /// <summary>Repetidas somam aqui ("×2"), em vez de empilhar outro toast igual.</summary>
        public void Atualizar()
        {
            _contagem.Text = _mensagem.Contagem > 1 ? $"×{_mensagem.Contagem}" : string.Empty;
            _contagem.IsVisible = _mensagem.Contagem > 1;
            SemanticProperties.SetDescription(_contagem, _mensagem.Contagem > 1 ? $"{_mensagem.Contagem} vezes" : string.Empty);
        }

        /// <summary>Foco do teclado num botão do toast também segura o tempo.</summary>
        private void SegurarTempo(Button botao, ServicoMensagens servico)
        {
            botao.Focused += (_, _) => servico.Pausar(_mensagem.Id);
            botao.Unfocused += (_, _) => servico.Retomar(_mensagem.Id);
        }

        private static (string Icone, string Nome) Descrever(TipoMensagem tipo) => tipo switch
        {
            TipoMensagem.Sucesso => ("✓", "Sucesso"),
            TipoMensagem.Aviso => ("!", "Aviso"),
            TipoMensagem.Erro => ("✕", "Erro"),
            _ => ("i", "Informação")
        };

        private static Color Cor(string chave) =>
            Application.Current?.Resources.TryGetValue(chave, out var valor) == true && valor is Color cor ? cor : Colors.Gray;

        private static Style? Estilo(string chave) =>
            Application.Current?.Resources.TryGetValue(chave, out var valor) == true ? valor as Style : null;
    }
}
