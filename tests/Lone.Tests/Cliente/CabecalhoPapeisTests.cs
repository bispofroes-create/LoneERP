using System.Collections.Specialized;
using Lone.Cliente.ViewModels.Pessoas;
using Lone.Contracts.GruposEmpresariais;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enums;

namespace Lone.Tests.Cliente;

/// <summary>
/// Selos de papéis do cabeçalho da ficha. Defeito corrigido (26/09/2026): ao abrir uma pessoa jurídica, a leitura dos grupos
/// empresariais atualizava o cabeçalho e os selos eram refeitos do zero enquanto o Windows montava a tela — o aplicativo
/// fechava (COMException no Measure). Agora a coleção é a mesma e só muda quando um papel é marcado ou desmarcado.
/// </summary>
public class CabecalhoPapeisTests
{
    private static PessoaDto Empresa() => new()
    {
        Id = Guid.NewGuid(),
        Codigo = 7,
        Natureza = NaturezaPessoa.Juridica,
        Nome = "ABC Comércio Ltda",
        Estabelecimentos = [new EstabelecimentoDto { Id = Guid.NewGuid(), Cnpj = "11222333000181", Principal = true, NomeFantasia = "ABC" }],
        Papeis =
        [
            new PapelDto { Papel = TipoPapel.Cliente, Ativo = true, InicioEm = new DateOnly(2025, 1, 1) },
            new PapelDto { Papel = TipoPapel.Fornecedor, Ativo = true, InicioEm = new DateOnly(2025, 1, 1) }
        ]
    };

    [Fact]
    public void Abrir_empresa_e_ler_os_grupos_nao_refaz_os_selos()
    {
        var f = PessoaFormulario.De(Empresa());
        var selos = f.PapeisAtivos;
        Assert.Equal(new TipoPapel?[] { TipoPapel.Cliente, TipoPapel.Fornecedor }, selos.Select(p => p.Papel).ToArray());
        var mudancasNosSelos = 0;
        var trocouAColecao = false;
        selos.CollectionChanged += (_, _) => mudancasNosSelos++;
        f.PropertyChanged += (_, e) => trocouAColecao |= e.PropertyName == nameof(PessoaFormulario.PapeisAtivos);

        // O que acontece ao abrir a PJ: grupos lidos da API, e o cabeçalho é atualizado por outras mudanças.
        f.DefinirGruposEmpresariais([new GrupoEmpresarialDto { Id = Guid.NewGuid(), Nome = "Grupo X", Ativo = true }]);
        f.Principal.NomeFantasia = "ABC Matriz";
        f.NomeExibicao = "ABC";

        Assert.Same(selos, f.PapeisAtivos);
        Assert.Equal(0, mudancasNosSelos);
        Assert.False(trocouAColecao);
    }

    [Fact]
    public void Marcar_e_desmarcar_papel_muda_so_o_selo_dele_na_ordem_dos_papeis()
    {
        var f = PessoaFormulario.De(Empresa());
        var eventos = new List<NotifyCollectionChangedAction>();
        f.PapeisAtivos.CollectionChanged += (_, e) => eventos.Add(e.Action);

        f.PapelCliente.Ativo = false;
        Assert.Equal(new TipoPapel?[] { TipoPapel.Fornecedor }, f.PapeisAtivos.Select(p => p.Papel).ToArray());

        f.PapelCliente.Ativo = true;
        Assert.Equal(new TipoPapel?[] { TipoPapel.Cliente, TipoPapel.Fornecedor }, f.PapeisAtivos.Select(p => p.Papel).ToArray());

        Assert.Equal(new[] { NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add }, eventos.ToArray());
    }
}
