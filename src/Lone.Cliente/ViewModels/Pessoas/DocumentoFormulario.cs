using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Enums;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>RG, CNH, passaporte... As datas são digitadas como dd/mm/aaaa.</summary>
public sealed partial class DocumentoFormulario : ItemDeLista
{
    public DocumentoFormulario() : this(IdSequencial.Novo()) { }
    private DocumentoFormulario(Guid id) => Id = id;

    public Guid Id { get; }
    public IReadOnlyList<Opcao<TipoDocumento>> Tipos => OpcoesPessoa.TiposDocumento;

    [ObservableProperty] private Opcao<TipoDocumento> _tipo = OpcoesPessoa.TiposDocumento[0];
    [ObservableProperty] private string _numero = string.Empty;
    [ObservableProperty] private string _orgaoEmissor = string.Empty;
    [ObservableProperty] private string _uf = string.Empty;
    [ObservableProperty] private string _emitidoEm = string.Empty;
    [ObservableProperty] private string _validoAte = string.Empty;
    [ObservableProperty] private string _observacoes = string.Empty;

    public static DocumentoFormulario De(DocumentoDto d) => new(d.Id)
    {
        Tipo = Opcao.De(OpcoesPessoa.TiposDocumento, d.Tipo),
        Numero = d.Numero,
        OrgaoEmissor = d.OrgaoEmissor ?? string.Empty,
        Uf = d.Uf ?? string.Empty,
        EmitidoEm = TextoTela.Data(d.EmitidoEm),
        ValidoAte = TextoTela.Data(d.ValidoAte),
        Observacoes = d.Observacoes ?? string.Empty
    };

    /// <summary>Datas que não dá para entender (o resto a API valida).</summary>
    public IEnumerable<string> Validar()
    {
        var nome = Tipo.Texto + (Numero.Length > 0 ? " " + Numero : string.Empty);
        if (!TextoTela.TentarData(EmitidoEm, out _)) yield return $"{nome}: data de emissão inválida (use dd/mm/aaaa).";
        if (!TextoTela.TentarData(ValidoAte, out _)) yield return $"{nome}: validade inválida (use dd/mm/aaaa).";
    }

    public DocumentoDto ParaDto()
    {
        TextoTela.TentarData(EmitidoEm, out var emitido);
        TextoTela.TentarData(ValidoAte, out var valido);
        return new DocumentoDto
        {
            Id = Id,
            Tipo = Tipo.Valor,
            Numero = Numero,
            OrgaoEmissor = TextoTela.Nulo(OrgaoEmissor),
            Uf = TextoTela.Nulo(Uf),
            EmitidoEm = emitido,
            ValidoAte = valido,
            Observacoes = TextoTela.Nulo(Observacoes)
        };
    }
}
