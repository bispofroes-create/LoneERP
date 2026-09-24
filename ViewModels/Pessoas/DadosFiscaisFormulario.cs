using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Core.Entidades;

namespace Lone.ViewModels.Pessoas
{
    public partial class DadosFiscaisFormulario : ObservableObject
    {
        [ObservableProperty] private int _indicadorIndice;
        [ObservableProperty] private string _inscricaoEstadual = string.Empty;
        [ObservableProperty] private string _inscricaoMunicipal = string.Empty;
        [ObservableProperty] private string _inscricaoSuframa = string.Empty;

        public static DadosFiscaisFormulario De(DadosFiscais f) => new()
        {
            IndicadorIndice = Opcoes.Indice(f.IndicadorIE),
            InscricaoEstadual = f.InscricaoEstadual ?? string.Empty,
            InscricaoMunicipal = f.InscricaoMunicipal ?? string.Empty,
            InscricaoSuframa = f.InscricaoSuframa ?? string.Empty
        };

        public DadosFiscais ParaEntidade() => new()
        {
            IndicadorIE = Opcoes.Indicador(IndicadorIndice),
            InscricaoEstadual = InscricaoEstadual,
            InscricaoMunicipal = InscricaoMunicipal,
            InscricaoSuframa = InscricaoSuframa
        };
    }
}
