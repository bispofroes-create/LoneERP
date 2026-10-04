using System.Windows.Input;
using Lone.App.Plataforma;
using Lone.Cliente.ViewModels;
using static Lone.App.Controles.RecursosTela;

namespace Lone.App.Controles;

/// <summary>
/// Menu do usuário (03/10/2026), aberto ao tocar no nome no canto superior direito, como no SAP Fiori, no Dynamics e no
/// Office: cabeçalho (nome, login · empresa), Minha conta (Trocar senha), Administração (Perfis de acesso, Usuários — só
/// com permissão) e Trocar de usuário / Sair do Lone. Substitui a página "Configurações do sistema" do menu lateral.
/// Sem popover na plataforma, abre a lista simples do <see cref="MenuViewModel.OpcoesDoUsuarioCommand"/>.
/// </summary>
internal static class MenuDoUsuario
{
    public static void Abrir(View ancora, MenuViewModel menu)
    {
        if (!Popover.Disponivel)
        {
            menu.OpcoesDoUsuarioCommand.Execute(null);
            return;
        }

        Action? fechar = null;
        var lista = new VerticalStackLayout { Padding = new Thickness(4, 8), WidthRequest = 280, Spacing = 0 };

        // Cabeçalho: quem está logado e em qual empresa.
        var nome = new Label { Text = menu.NomeUsuario, FontSize = 14, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation };
        Cor(nome, Label.TextColorProperty, "Texto");
        var detalhe = new Label { Text = $"{menu.Login} · {menu.EmpresaAtiva}", FontSize = 12, LineBreakMode = LineBreakMode.TailTruncation };
        Cor(detalhe, Label.TextColorProperty, "TextoSecundario");
        lista.Add(new VerticalStackLayout { Padding = new Thickness(12, 4, 12, 8), Spacing = 2, Children = { nome, detalhe } });

        void Item(string icone, string texto, ICommand comando)
        {
            var rotulo = new Label { Text = $"{icone}   {texto}", FontSize = 14, VerticalOptions = LayoutOptions.Center };
            Cor(rotulo, Label.TextColorProperty, "Texto");
            var linha = new Border
            {
                StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                Padding = new Thickness(12, 0), HeightRequest = 36, BackgroundColor = Colors.Transparent, Content = rotulo
            };
            SemanticProperties.SetDescription(linha, texto);
            var hover = new PointerGestureRecognizer();
            hover.PointerEntered += (_, _) => Cor(linha, VisualElement.BackgroundColorProperty, "SuperficieRealce");
            hover.PointerExited += (_, _) => { linha.ClearValue(VisualElement.BackgroundColorProperty); linha.BackgroundColor = Colors.Transparent; };
            linha.GestureRecognizers.Add(hover);
            var toque = new TapGestureRecognizer();
            toque.Tapped += (_, _) =>
            {
                fechar?.Invoke();
                if (comando.CanExecute(null)) comando.Execute(null);
            };
            linha.GestureRecognizers.Add(toque);
            lista.Add(linha);
        }

        void Separador()
        {
            var linha = new BoxView { HeightRequest = 1, Margin = new Thickness(0, 6) };
            Cor(linha, BoxView.ColorProperty, "BordaSutil");
            lista.Add(linha);
        }

        void Secao(string titulo)
        {
            var rotulo = new Label
            {
                Text = titulo, FontSize = 11, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1,
                TextTransform = TextTransform.Uppercase, Padding = new Thickness(12, 2, 12, 4)
            };
            Cor(rotulo, Label.TextColorProperty, "TextoSecundario");
            SemanticProperties.SetHeadingLevel(rotulo, SemanticHeadingLevel.Level2);
            lista.Add(rotulo);
        }

        Separador();
        Secao("Minha conta");
        Item("🔑", MenuViewModel.OpcaoTrocarSenha, menu.TrocarSenhaCommand);
        if (menu.MostrarAdministracao)
        {
            Separador();
            Secao("Administração");
            if (menu.PodeGerenciarPerfis) Item("🛡", MenuViewModel.OpcaoPerfis, menu.AbrirPerfisCommand);
            if (menu.PodeGerenciarUsuarios) Item("👥", MenuViewModel.OpcaoUsuarios, menu.AbrirUsuariosCommand);
        }
        Separador();
        Item("⇄", MenuViewModel.OpcaoTrocarUsuario, menu.TrocarDeUsuarioCommand);
        Item("⏻", MenuViewModel.OpcaoSair, menu.SairDoLoneCommand);

        fechar = Popover.Mostrar(ancora, lista);
    }
}
