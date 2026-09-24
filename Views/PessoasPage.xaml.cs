using System;
using System.ComponentModel;
using Lone.Aplicacao.Pessoas;
using Lone.ViewModels;
using Lone.ViewModels.Pessoas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Lone.Views
{
    /// <summary>
    /// Tela da Central de Pessoas. O code-behind só cuida do que é visual (qual seção aparece)
    /// e repassa ao ViewModel os cliques feitos dentro dos cartões das listas.
    /// </summary>
    public sealed partial class PessoasPage : Page
    {
        private const string SecaoInicial = "geral";

        public PessoasViewModel ViewModel { get; }

        public PessoasPage()
        {
            ViewModel = App.Services.GetRequiredService<PessoasViewModel>();
            InitializeComponent();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            Unloaded += (_, _) => ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CarregarAsync();
        }

        // Ao abrir outra pessoa (ou "Nova pessoa"), volta para a seção Geral.
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PessoasViewModel.Formulario))
                return;

            if (Secoes.SelectedItem != ItemGeral)
                Secoes.SelectedItem = ItemGeral;
            else
                MostrarSecao(SecaoInicial);
        }

        // ---- Lista e filtros ----

        private void Lista_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.Selecionado = Lista.SelectedItem as PessoaResumo;
        }

        private void FiltroPapel_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            var indice = sender.Items.IndexOf(sender.SelectedItem);
            ViewModel.Papel = indice >= 0 && indice < Opcoes.FiltrosPapel.Length ? Opcoes.FiltrosPapel[indice] : null;
        }

        // ---- Seções do cadastro ----

        private void Secoes_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            var secao = sender.SelectedItem?.Tag as string ?? SecaoInicial;
            MostrarSecao(secao);

            if (secao == "historico")
                _ = ViewModel.CarregarHistoricoAsync();
        }

        private void MostrarSecao(string secao)
        {
            // Durante a criação da página alguns painéis ainda não existem.
            if (SecaoHistorico is null) return;

            SecaoGeral.Visibility = Visivel(secao == "geral");
            SecaoEstabelecimentos.Visibility = Visivel(secao == "estabelecimentos");
            SecaoEnderecos.Visibility = Visivel(secao == "enderecos");
            SecaoContatos.Visibility = Visivel(secao == "contatos");
            SecaoDocumentos.Visibility = Visivel(secao == "documentos");
            SecaoCliente.Visibility = Visivel(secao == "cliente");
            SecaoFornecedor.Visibility = Visivel(secao == "fornecedor");
            SecaoHistorico.Visibility = Visivel(secao == "historico");
        }

        private static Visibility Visivel(bool sim) => sim ? Visibility.Visible : Visibility.Collapsed;

        // ---- Estabelecimentos ----

        private void TornarPrincipal_Click(object sender, RoutedEventArgs e)
        {
            if (Item<EstabelecimentoFormulario>(sender) is { } estabelecimento)
                ViewModel.TornarPrincipal(estabelecimento);
        }

        private void RemoverEstabelecimento_Click(object sender, RoutedEventArgs e)
        {
            if (Item<EstabelecimentoFormulario>(sender) is { } estabelecimento)
                ViewModel.RemoverEstabelecimento(estabelecimento);
        }

        private async void ConsultarCnpj_Click(object sender, RoutedEventArgs e)
        {
            if (Item<EstabelecimentoFormulario>(sender) is { } estabelecimento)
                await ViewModel.ConsultarCnpjAsync(estabelecimento);
        }

        /// <summary>
        /// Escolha do endereço fiscal. Seleção vazia é ignorada (acontece quando o cartão é recriado
        /// ou a lista muda); para voltar ao padrão existe o botão "Usar padrão".
        /// </summary>
        private void EnderecoFiscal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox { DataContext: EstabelecimentoFormulario estabelecimento, SelectedItem: EnderecoFormulario endereco }
                && estabelecimento.EnderecoFiscal != endereco)
            {
                estabelecimento.EnderecoFiscal = endereco;
            }
        }

        private void UsarEnderecoPadrao_Click(object sender, RoutedEventArgs e)
        {
            if (Item<EstabelecimentoFormulario>(sender) is { } estabelecimento)
                estabelecimento.EnderecoFiscal = null;
        }

        // ---- Endereços ----

        private async void BuscarCep_Click(object sender, RoutedEventArgs e)
        {
            if (Item<EnderecoFormulario>(sender) is { } endereco)
                await ViewModel.BuscarCepAsync(endereco);
        }

        private void RemoverEndereco_Click(object sender, RoutedEventArgs e)
        {
            if (Item<EnderecoFormulario>(sender) is { } endereco)
                ViewModel.RemoverEndereco(endereco);
        }

        // ---- Contatos e documentos ----

        private void RemoverMeio_Click(object sender, RoutedEventArgs e)
        {
            if (Item<MeioContatoFormulario>(sender) is { } meio)
                ViewModel.RemoverMeio(meio);
        }

        private void RemoverContatoPessoa_Click(object sender, RoutedEventArgs e)
        {
            if (Item<ContatoPessoaFormulario>(sender) is { } contato)
                ViewModel.RemoverContatoPessoa(contato);
        }

        private void RemoverDocumento_Click(object sender, RoutedEventArgs e)
        {
            if (Item<DocumentoFormulario>(sender) is { } documento)
                ViewModel.RemoverDocumento(documento);
        }

        /// <summary>O item do cartão em que o botão foi clicado (o DataContext do modelo).</summary>
        private static T? Item<T>(object sender) where T : class =>
            (sender as FrameworkElement)?.DataContext as T;
    }
}
