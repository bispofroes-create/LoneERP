using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Core.Entidades;

namespace Lone.ViewModels.Pessoas
{
    /// <summary>Conta padrão de cliente (vale para todas as empresas sem conta própria).</summary>
    public partial class ContaClienteFormulario : ObservableObject
    {
        private int? _vendedorPadraoId;

        [ObservableProperty] private int _id;
        [ObservableProperty] private double _limiteCredito = double.NaN; // NaN = vazio no NumberBox
        [ObservableProperty] private double _diasMaximoAtraso = double.NaN;
        [ObservableProperty] private double _descontoMaximo = double.NaN;
        [ObservableProperty] private string _condicaoPagamento = string.Empty;
        [ObservableProperty] private bool _exigeAprovacaoAcimaLimite = true;
        [ObservableProperty] private string _observacoes = string.Empty;

        public static ContaClienteFormulario De(ContaCliente? c) => c is null
            ? new ContaClienteFormulario()
            : new ContaClienteFormulario
            {
                _vendedorPadraoId = c.VendedorPadraoId,
                Id = c.Id,
                LimiteCredito = c.LimiteCredito is { } limite ? (double)limite : double.NaN,
                DiasMaximoAtraso = c.DiasMaximoAtraso ?? double.NaN,
                DescontoMaximo = c.DescontoMaximo is { } desconto ? (double)desconto : double.NaN,
                CondicaoPagamento = c.CondicaoPagamento ?? string.Empty,
                ExigeAprovacaoAcimaLimite = c.ExigeAprovacaoAcimaLimite,
                Observacoes = c.Observacoes ?? string.Empty
            };

        public ContaCliente ParaEntidade() => new()
        {
            Id = Id,
            EmpresaId = null,
            LimiteCredito = double.IsNaN(LimiteCredito) ? null : (decimal)LimiteCredito,
            DiasMaximoAtraso = double.IsNaN(DiasMaximoAtraso) ? null : (int)DiasMaximoAtraso,
            DescontoMaximo = double.IsNaN(DescontoMaximo) ? null : (decimal)DescontoMaximo,
            CondicaoPagamento = CondicaoPagamento,
            ExigeAprovacaoAcimaLimite = ExigeAprovacaoAcimaLimite,
            VendedorPadraoId = _vendedorPadraoId,
            Observacoes = Observacoes
        };
    }

    /// <summary>Conta padrão de fornecedor.</summary>
    public partial class ContaFornecedorFormulario : ObservableObject
    {
        private int? _transportadoraPadraoId;

        [ObservableProperty] private int _id;
        [ObservableProperty] private string _condicaoPagamento = string.Empty;
        [ObservableProperty] private double _prazoMedioDias = double.NaN;
        [ObservableProperty] private double _leadTimeDias = double.NaN;
        [ObservableProperty] private double _avaliacao = double.NaN;
        [ObservableProperty] private string _observacoes = string.Empty;

        public static ContaFornecedorFormulario De(ContaFornecedor? f) => f is null
            ? new ContaFornecedorFormulario()
            : new ContaFornecedorFormulario
            {
                _transportadoraPadraoId = f.TransportadoraPadraoId,
                Id = f.Id,
                CondicaoPagamento = f.CondicaoPagamento ?? string.Empty,
                PrazoMedioDias = f.PrazoMedioDias ?? double.NaN,
                LeadTimeDias = f.LeadTimeDias ?? double.NaN,
                Avaliacao = f.Avaliacao ?? double.NaN,
                Observacoes = f.Observacoes ?? string.Empty
            };

        public ContaFornecedor ParaEntidade() => new()
        {
            Id = Id,
            EmpresaId = null,
            CondicaoPagamento = CondicaoPagamento,
            PrazoMedioDias = double.IsNaN(PrazoMedioDias) ? null : (int)PrazoMedioDias,
            LeadTimeDias = double.IsNaN(LeadTimeDias) ? null : (int)LeadTimeDias,
            Avaliacao = double.IsNaN(Avaliacao) ? null : (byte)Avaliacao,
            TransportadoraPadraoId = _transportadoraPadraoId,
            Observacoes = Observacoes
        };
    }
}
