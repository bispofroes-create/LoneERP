using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Lone.Views
{
    public sealed partial class EmConstrucaoPage : Page
    {
        public EmConstrucaoPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Titulo.Text = e.Parameter as string ?? "Em construção";
        }
    }
}
