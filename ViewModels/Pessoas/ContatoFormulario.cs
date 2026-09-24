using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Core.Entidades;
using Lone.Core.Enums;
using Lone.Core.ObjetosDeValor;

namespace Lone.ViewModels.Pessoas
{
    public partial class ContatoFormulario : ObservableObject
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private int _tipoIndice = Opcoes.Indice(TipoContato.Celular);
        [ObservableProperty] private string _valor = string.Empty;
        [ObservableProperty] private string _descricao = string.Empty;
        [ObservableProperty] private bool _principal;

        public static ContatoFormulario De(PessoaContato c) => new()
        {
            Id = c.Id,
            TipoIndice = Opcoes.Indice(c.Tipo),
            Valor = Exibir(c),
            Descricao = c.Descricao ?? string.Empty,
            Principal = c.Principal
        };

        /// <summary>Telefones ficam gravados só com dígitos; na tela aparecem formatados.</summary>
        private static string Exibir(PessoaContato c) =>
            c.Tipo != TipoContato.Email && Telefone.TentarCriar(c.Valor, out var telefone)
                ? telefone!.Formatado
                : c.Valor;

        public PessoaContato ParaEntidade() => new()
        {
            Id = Id,
            Tipo = Opcoes.TipoDeContato(TipoIndice),
            Valor = Valor,
            Descricao = Descricao,
            Principal = Principal
        };
    }
}
