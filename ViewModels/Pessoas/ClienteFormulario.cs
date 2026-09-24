using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Core.Entidades;

namespace Lone.ViewModels.Pessoas
{
    /// <summary>Seção "Cliente" do cadastro de pessoa (papel de cliente).</summary>
    public partial class ClienteFormulario : ObservableObject
    {
        /// <summary>A pessoa já teve este papel alguma vez (então desmarcar só inativa).</summary>
        private bool _existia;

        [ObservableProperty] private bool _habilitado;
        [ObservableProperty] private double _limiteCredito = double.NaN; // NaN = vazio no NumberBox
        [ObservableProperty] private string _condicaoPagamento = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TextoBloqueio))]
        private bool _bloqueado;

        [ObservableProperty] private string _motivoBloqueio = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TextoBloqueio))]
        private DateTime? _bloqueadoEm;

        [ObservableProperty] private string _observacoes = string.Empty;

        public string TextoBloqueio => Bloqueado && BloqueadoEm is { } em ? $"Bloqueado desde {em:dd/MM/yyyy HH:mm}" : string.Empty;

        public static ClienteFormulario De(Cliente? c) => c is null
            ? new ClienteFormulario()
            : new ClienteFormulario
            {
                _existia = true,
                Habilitado = c.Ativo,
                LimiteCredito = c.LimiteCredito is { } limite ? (double)limite : double.NaN,
                CondicaoPagamento = c.CondicaoPagamento ?? string.Empty,
                Bloqueado = c.Bloqueado,
                MotivoBloqueio = c.MotivoBloqueio ?? string.Empty,
                BloqueadoEm = c.BloqueadoEm,
                Observacoes = c.Observacoes ?? string.Empty
            };

        /// <summary>Nulo quando o papel nunca foi usado e continua desmarcado.</summary>
        public Cliente? ParaEntidade() => !Habilitado && !_existia
            ? null
            : new Cliente
            {
                Ativo = Habilitado,
                LimiteCredito = double.IsNaN(LimiteCredito) ? null : (decimal)LimiteCredito,
                CondicaoPagamento = CondicaoPagamento,
                Bloqueado = Bloqueado,
                MotivoBloqueio = MotivoBloqueio,
                Observacoes = Observacoes
            };
    }
}
