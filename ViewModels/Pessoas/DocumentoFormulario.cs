using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Core.Entidades;

namespace Lone.ViewModels.Pessoas
{
    public partial class DocumentoFormulario : ObservableObject
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private int _tipoIndice;
        [ObservableProperty] private string _numero = string.Empty;
        [ObservableProperty] private string _orgaoEmissor = string.Empty;
        [ObservableProperty] private string _uf = string.Empty;
        [ObservableProperty] private DateTimeOffset? _emitidoEm;
        [ObservableProperty] private DateTimeOffset? _validoAte;
        [ObservableProperty] private string _observacoes = string.Empty;

        public static DocumentoFormulario De(PessoaDocumento d) => new()
        {
            Id = d.Id,
            TipoIndice = Opcoes.Indice(d.Tipo),
            Numero = d.Numero,
            OrgaoEmissor = d.OrgaoEmissor ?? string.Empty,
            Uf = d.Uf ?? string.Empty,
            EmitidoEm = Datas.ParaTela(d.EmitidoEm),
            ValidoAte = Datas.ParaTela(d.ValidoAte),
            Observacoes = d.Observacoes ?? string.Empty
        };

        public PessoaDocumento ParaEntidade() => new()
        {
            Id = Id,
            Tipo = Opcoes.TipoDeDocumento(TipoIndice),
            Numero = Numero,
            OrgaoEmissor = OrgaoEmissor,
            Uf = Uf,
            EmitidoEm = Datas.ParaEntidade(EmitidoEm),
            ValidoAte = Datas.ParaEntidade(ValidoAte),
            Observacoes = Observacoes
        };
    }

    /// <summary>Conversão entre DateOnly (banco) e DateTimeOffset (CalendarDatePicker).</summary>
    internal static class Datas
    {
        public static DateTimeOffset? ParaTela(DateOnly? d) =>
            d is { } data ? new DateTimeOffset(data.ToDateTime(TimeOnly.MinValue)) : null;

        public static DateOnly? ParaEntidade(DateTimeOffset? d) =>
            d is { } data ? DateOnly.FromDateTime(data.Date) : null;
    }
}
