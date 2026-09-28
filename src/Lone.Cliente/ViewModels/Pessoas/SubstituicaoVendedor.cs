using Lone.Cliente.ViewModels.Comum;
using Lone.Domain.Comercial;
using Lone.Domain.Entidades;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Um vínculo novo da carteira que entra no lugar de um vigente do mesmo papel de um por vez/exclusivo (decisão D3 da Etapa 4).
/// A regra de conflito é a do domínio (<see cref="RegrasComercial.Conflitam"/>): a ficha só pergunta antes e, com a
/// confirmação, encerra o anterior na véspera do início do novo. Nada é apagado; o servidor confere tudo de novo ao gravar.
/// </summary>
public sealed class SubstituicaoVendedor
{
    private SubstituicaoVendedor(CarteiraFormulario novo, IReadOnlyList<CarteiraFormulario> encerrar,
                                 IReadOnlyList<CarteiraFormulario> impedem, string tipo, bool umPorVez)
    {
        Novo = novo;
        Encerrar = encerrar;
        Impedem = impedem;
        Tipo = tipo;
        UmPorVez = umPorVez;
    }

    public CarteiraFormulario Novo { get; }

    /// <summary>Vigentes que serão encerrados na véspera do início do novo.</summary>
    public IReadOnlyList<CarteiraFormulario> Encerrar { get; }

    /// <summary>Vigentes que começam no mesmo dia ou depois do novo: não dá para substituir sem alterar o histórico.</summary>
    public IReadOnlyList<CarteiraFormulario> Impedem { get; }

    public string Tipo { get; }
    /// <summary>O papel aceita um vínculo por vez (senão, o conflito vem de um vínculo exclusivo).</summary>
    public bool UmPorVez { get; }
    public bool Impedida => Impedem.Count > 0;

    private DateOnly InicioNovo => TextoTela.TentarData(Novo.InicioEm, out var d) && d is { } inicio ? inicio : default;

    /// <summary>
    /// Os vínculos novos desta ficha (ainda não gravados) que conflitam com um vínculo já gravado. Conflito entre dois
    /// vínculos novos não entra: a validação do servidor aponta (não há "anterior" para encerrar).
    /// </summary>
    public static List<SubstituicaoVendedor> Planejar(IReadOnlyList<CarteiraFormulario> carteira, OpcoesComercial? opcoes)
    {
        if (opcoes is null || carteira.Count == 0) return [];
        var tipos = opcoes.Dados.TiposCarteira.ToDictionary(t => t.Id, t => new TipoCarteira
        {
            Id = t.Id, Nome = t.Nome, ResponsavelDaConta = t.ResponsavelDaConta, LimitePorVez = t.LimitePorVez,
            TipoCredito = t.TipoCredito, PercentualPadrao = t.PercentualPadrao
        });
        var porEntidade = carteira.ToDictionary(Entidade);
        var gravados = porEntidade.Where(p => p.Value.Gravada).Select(p => p.Key).ToList();

        var resultado = new List<SubstituicaoVendedor>();
        foreach (var (entidade, item) in porEntidade.Where(p => !p.Value.Gravada && p.Value.Ativo))
        {
            var plano = RegrasComercial.PlanejarSubstituicao(gravados, entidade, tipos);
            if (!plano.TemConflito) continue;
            var tipo = tipos.GetValueOrDefault(entidade.TipoCarteiraId);
            resultado.Add(new SubstituicaoVendedor(item,
                [.. plano.Encerrar.Select(e => porEntidade[e])], [.. plano.Impedem.Select(e => porEntidade[e])],
                tipo?.Nome ?? "carteira", tipo?.UmPorVez == true));
        }
        return resultado;
    }

    /// <summary>
    /// Com a confirmação do usuário: encerra os anteriores na véspera do início do novo. Devolve como desfazer (se a
    /// gravação falhar ou o usuário cancelar outra confirmação, a ficha volta como estava).
    /// </summary>
    public Action Aplicar()
    {
        if (Impedida) throw new InvalidOperationException("Substituição impedida.");
        var antes = Encerrar.Select(a => (Item: a, Fim: a.FimEm)).ToList();
        var fim = TextoTela.Data(InicioNovo.AddDays(-1));
        foreach (var anterior in Encerrar) anterior.FimEm = fim;

        // O novo herda o crédito (%) do anterior, se não tiver o seu: a divisão da venda continua somando 100%.
        var creditoAntes = Novo.PercentualCredito;
        if (string.IsNullOrWhiteSpace(Novo.PercentualCredito) && Encerrar.Count == 1)
            Novo.PercentualCredito = Encerrar[0].PercentualCredito;

        return () =>
        {
            foreach (var (item, fimAntes) in antes) item.FimEm = fimAntes;
            Novo.PercentualCredito = creditoAntes;
        };
    }

    public string Titulo => Impedida ? "Não dá para substituir o vendedor" : "Vendedor já atribuído";

    /// <summary>Texto da confirmação: quem está, quem entra e o que vai acontecer (nada é excluído).</summary>
    public string Mensagem
    {
        get
        {
            var inicio = InicioNovo;
            var novo = Novo.NomePessoa;
            var regra = UmPorVez ? $"um \"{Tipo}\" ativo (um por vez)"
                : Encerrar.Concat(Impedem).Any(e => e.Exclusivo) ? $"um vínculo exclusivo de \"{Tipo}\" ativo"
                : $"um vínculo de \"{Tipo}\" ativo, e o novo é exclusivo";
            if (Impedida)
            {
                var outro = Impedem[0];
                return $"Esta pessoa já tem {regra}: {outro.NomePessoa}, desde {outro.InicioEm}.\n\n" +
                       $"{novo} começaria em {TextoTela.Data(inicio)}, no mesmo dia ou antes dele. Encerrar o vínculo atual " +
                       "antes do início dele apagaria o período em que ele foi o responsável, e o histórico não é alterado.\n\n" +
                       "Ajuste o início do novo vínculo para depois do início do atual, ou corrija o vínculo atual na lista." +
                       DicaVarios;
            }
            var atuais = string.Join(", ", Encerrar.Select(a => $"{a.NomePessoa} (desde {a.InicioEm})"));
            var nomes = string.Join(", ", Encerrar.Select(a => a.NomePessoa));
            return $"Esta pessoa já tem {regra}.\n\n" +
                   $"Vendedor atual: {atuais}\n" +
                   $"Novo vendedor: {novo} (a partir de {TextoTela.Data(inicio)})\n\n" +
                   "O que vai acontecer:\n" +
                   $"• O vínculo de {nomes} será encerrado em {TextoTela.Data(inicio.AddDays(-1))}.\n" +
                   $"• {novo} passa a ser o responsável a partir de {TextoTela.Data(inicio)}.\n" +
                   "• O vínculo anterior continua no histórico.\n" +
                   "• Nenhum cadastro é excluído." +
                   DicaVarios;
        }
    }

    /// <summary>
    /// Papel de um por vez: quem quer dois atendendo juntos (e dividindo a venda) usa outro papel, não um segundo deste.
    /// </summary>
    private string DicaVarios => UmPorVez
        ? $"\n\nPara dois atenderem juntos e dividirem o crédito da venda, use um papel que aceite mais de um (ex.: " +
          "Representante, com crédito \"Receita\"), em Configurações › Papéis comerciais."
        : string.Empty;

    public const string TextoConfirmar = "Encerrar anterior e atribuir novo vendedor";

    private static CarteiraCliente Entidade(CarteiraFormulario item)
    {
        var d = item.ParaDto();
        return new CarteiraCliente
        {
            Id = d.Id, TipoCarteiraId = d.TipoCarteiraId, VendedorId = d.VendedorId, EmpresaId = d.EmpresaId,
            InicioEm = d.InicioEm, FimEm = d.FimEm, Exclusivo = d.Exclusivo, Ativo = d.Ativo
        };
    }
}
