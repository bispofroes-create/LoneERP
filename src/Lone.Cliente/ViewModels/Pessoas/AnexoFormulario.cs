using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Documentos;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// O que a tela de pessoas faz com os anexos (enviar, abrir, remover/reativar). Os anexos são gravados na hora,
/// à parte do "Salvar" da ficha; a tela liga estas ações ao abrir a ficha e desliga ao fechar.
/// </summary>
public sealed class AcoesAnexos
{
    public Func<DocumentoFormulario, Task>? Anexar { get; set; }
    public Func<AnexoFormulario, Task>? Abrir { get; set; }
    public Func<AnexoFormulario, bool, Task>? AlterarAtivo { get; set; }
}

/// <summary>Um arquivo anexado a um documento (só os dados; o conteúdo é baixado ao abrir).</summary>
public sealed partial class AnexoFormulario : ObservableObject
{
    private readonly AcoesAnexos _acoes;

    public AnexoFormulario(AnexoDto dados, AcoesAnexos acoes)
    {
        _acoes = acoes;
        Atualizar(dados);
    }

    public Guid Id { get; private set; }
    public string NomeArquivo { get; private set; } = string.Empty;

    /// <summary>"12/09/2026 · 1,2 MB · maria".</summary>
    public string Detalhe { get; private set; } = string.Empty;

    [ObservableProperty][NotifyPropertyChangedFor(nameof(Visivel), nameof(Inativo))] private bool _ativo = true;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(Visivel))] private bool _mostrarSeInativo;

    public bool Visivel => Ativo || MostrarSeInativo;
    public bool Inativo => !Ativo;

    public void Atualizar(AnexoDto dados)
    {
        Id = dados.Id;
        NomeArquivo = dados.NomeArquivo;
        Detalhe = string.Join(" · ", new[]
        {
            dados.EnviadoEm.ToLocalTime().ToString("dd/MM/yyyy", TextoTela.Brasil),
            Tamanho(dados.Tamanho),
            dados.EnviadoPor
        }.Where(s => s.Length > 0));
        Ativo = dados.Ativo;
        OnPropertyChanged(nameof(NomeArquivo));
        OnPropertyChanged(nameof(Detalhe));
    }

    public static string Tamanho(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => (bytes / 1024d).ToString("0", TextoTela.Brasil) + " KB",
        _ => (bytes / (1024d * 1024d)).ToString("0.0", TextoTela.Brasil) + " MB"
    };

    [RelayCommand]
    private Task AbrirAsync() => _acoes.Abrir?.Invoke(this) ?? Task.CompletedTask;

    [RelayCommand]
    private Task RemoverAsync() => _acoes.AlterarAtivo?.Invoke(this, false) ?? Task.CompletedTask;

    [RelayCommand]
    private Task ReativarAsync() => _acoes.AlterarAtivo?.Invoke(this, true) ?? Task.CompletedTask;
}
