using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Core.Entidades;

namespace Lone.ViewModels.Pessoas
{
    /// <summary>Pessoa de contato (ex.: "Maria — Financeiro").</summary>
    public partial class ContatoPessoaFormulario : ObservableObject
    {
        private int? _pessoaVinculadaId;

        [ObservableProperty] private int _id;
        [ObservableProperty] private string _nome = string.Empty;
        [ObservableProperty] private string _cargo = string.Empty;
        [ObservableProperty] private string _departamento = string.Empty;
        [ObservableProperty] private string _telefone = string.Empty;
        [ObservableProperty] private string _celular = string.Empty;
        [ObservableProperty] private bool? _celularWhatsApp = false;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _observacoes = string.Empty;
        [ObservableProperty] private bool? _principal = false;

        public static ContatoPessoaFormulario De(Contato c) => new()
        {
            _pessoaVinculadaId = c.PessoaVinculadaId,
            Id = c.Id,
            Nome = c.Nome,
            Cargo = c.Cargo ?? string.Empty,
            Departamento = c.Departamento ?? string.Empty,
            Telefone = FormatoTela.FormatarTelefone(c.Telefone),
            Celular = FormatoTela.FormatarTelefone(c.Celular),
            CelularWhatsApp = c.CelularWhatsApp,
            Email = c.Email ?? string.Empty,
            Observacoes = c.Observacoes ?? string.Empty,
            Principal = c.Principal
        };

        public Contato ParaEntidade() => new()
        {
            Id = Id,
            Nome = Nome,
            Cargo = Cargo,
            Departamento = Departamento,
            Telefone = Telefone,
            Celular = Celular,
            CelularWhatsApp = CelularWhatsApp == true,
            Email = Email,
            Observacoes = Observacoes,
            Principal = Principal == true,
            PessoaVinculadaId = _pessoaVinculadaId
        };
    }
}
