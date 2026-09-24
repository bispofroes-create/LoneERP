using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Core.Entidades;

namespace Lone.ViewModels.Pessoas
{
    /// <summary>Seção "Fornecedor" do cadastro de pessoa (papel de fornecedor).</summary>
    public partial class FornecedorFormulario : ObservableObject
    {
        private bool _existia;

        [ObservableProperty] private bool _habilitado;
        [ObservableProperty] private string _condicaoPagamento = string.Empty;
        [ObservableProperty] private double _prazoEntregaDias = double.NaN;
        [ObservableProperty] private string _observacoes = string.Empty;

        public static FornecedorFormulario De(Fornecedor? f) => f is null
            ? new FornecedorFormulario()
            : new FornecedorFormulario
            {
                _existia = true,
                Habilitado = f.Ativo,
                CondicaoPagamento = f.CondicaoPagamento ?? string.Empty,
                PrazoEntregaDias = f.PrazoEntregaDias ?? double.NaN,
                Observacoes = f.Observacoes ?? string.Empty
            };

        public Fornecedor? ParaEntidade() => !Habilitado && !_existia
            ? null
            : new Fornecedor
            {
                Ativo = Habilitado,
                CondicaoPagamento = CondicaoPagamento,
                PrazoEntregaDias = double.IsNaN(PrazoEntregaDias) ? null : (int)PrazoEntregaDias,
                Observacoes = Observacoes
            };
    }
}
