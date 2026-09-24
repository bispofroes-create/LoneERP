using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Core.Entidades;
using Lone.Core.Enums;
using Lone.Core.ObjetosDeValor;

namespace Lone.ViewModels.Pessoas
{
    /// <summary>Telefone ou e-mail da própria pessoa.</summary>
    public partial class MeioContatoFormulario : ObservableObject
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private int _tipoIndice = Opcoes.Indice(TipoContato.Celular);
        [ObservableProperty] private string _valor = string.Empty;
        [ObservableProperty] private string _descricao = string.Empty;
        [ObservableProperty] private bool _principal;
        [ObservableProperty] private bool? _permiteComunicacao = true;

        public static MeioContatoFormulario De(MeioContato m) => new()
        {
            Id = m.Id,
            TipoIndice = Opcoes.Indice(m.Tipo),
            Valor = FormatoTela.Contato(m.Tipo, m.Valor),
            Descricao = m.Descricao ?? string.Empty,
            Principal = m.Principal,
            PermiteComunicacao = m.PermiteComunicacao
        };

        public MeioContato ParaEntidade() => new()
        {
            Id = Id,
            Tipo = Opcoes.TipoDeContato(TipoIndice),
            Valor = Valor,
            Descricao = Descricao,
            Principal = Principal,
            PermiteComunicacao = PermiteComunicacao == true
        };
    }

    /// <summary>Formatação para exibir valores gravados só com dígitos.</summary>
    internal static class FormatoTela
    {
        public static string Contato(TipoContato tipo, string? valor) =>
            valor is null ? string.Empty
            : tipo != TipoContato.Email && Telefone.TentarCriar(valor, out var telefone) ? telefone!.Formatado
            : valor;

        public static string FormatarTelefone(string? valor) => Contato(TipoContato.Telefone, valor);
    }
}
