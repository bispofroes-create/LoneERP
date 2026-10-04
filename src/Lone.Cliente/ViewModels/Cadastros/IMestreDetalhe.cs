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

    /// <summary>Pode criar registro nesta tela (o "+ Novo" e o Ctrl+N; 03/10/2026).</summary>
    bool PodeCriar { get; }

    /// <summary>Nada em andamento (o Ctrl+N espera a tela ficar livre).</summary>
    bool Livre { get; }

    /// <summary>Novo registro (pergunta antes se houver alterações não salvas).</summary>
    IAsyncRelayCommand NovoCommand { get; }

    /// <summary>Há alterações não salvas na ficha aberta.</summary>
    bool TemAlteracoes { get; }

    /// <summary>Pergunta, se preciso, se pode sair da tela perdendo as alterações.</summary>
    Task<bool> PodeSairAsync();
}
