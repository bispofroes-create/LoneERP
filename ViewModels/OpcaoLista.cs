namespace Lone.ViewModels
{
    /// <summary>Opção de ComboBox com um Id (nulo = "todos"/"nenhum") e o texto mostrado.</summary>
    public sealed record OpcaoLista(int? Id, string Texto)
    {
        public override string ToString() => Texto;
    }
}
