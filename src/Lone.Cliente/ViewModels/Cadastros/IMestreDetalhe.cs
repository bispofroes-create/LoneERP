using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lone.Cliente.ViewModels.Cadastros;

/// <summary>O que a tela precisa saber para arrumar lista e ficha conforme a largura disponível.</summary>
public interface IMestreDetalhe : INotifyPropertyChanged
{
    bool ModoCompacto { get; set; }
    bool Editando { get; }
    bool MostrarLista { get; }
    bool MostrarFicha { get; }
    IAsyncRelayCommand FecharFichaCommand { get; }

    /// <summary>Há alterações não salvas na ficha aberta.</summary>
    bool TemAlteracoes { get; }

    /// <summary>Pergunta, se preciso, se pode sair da tela perdendo as alterações.</summary>
    Task<bool> PodeSairAsync();
}
