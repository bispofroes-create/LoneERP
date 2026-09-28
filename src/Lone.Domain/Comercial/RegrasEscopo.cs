using System.Linq.Expressions;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Domain.Metas;

namespace Lone.Domain.Comercial;

/// <summary>
/// Uma fonte do escopo: os vínculos da carteira de um vendedor. Numa cobertura de ausência o acesso pode valer só para um
/// papel (<paramref name="TipoCarteiraId"/>) ou só para uma empresa (<paramref name="EmpresaId"/>); nulo = todos.
/// </summary>
public sealed record FonteEscopo(Guid VendedorId, Guid? TipoCarteiraId = null, Guid? EmpresaId = null)
{
    /// <summary>Vale para todos os papéis e empresas (a carteira inteira do vendedor).</summary>
    public bool Livre => TipoCarteiraId is null && EmpresaId is null;
}

/// <summary>
/// O que o usuário alcança no cadastro de Pessoas num dia (Motor Comercial, Fase 2a-2): tudo, nada, ou os clientes da
/// carteira das <see cref="Fontes"/>. Calculado uma vez por requisição por <see cref="RegrasEscopo.Resolver"/>.
/// </summary>
public sealed class EscopoResolvido
{
    private EscopoResolvido(AlcanceComercial alcance, IReadOnlyList<FonteEscopo> fontes, Guid? empresaId, DateOnly hoje, bool semPessoa)
    {
        Alcance = alcance;
        Fontes = fontes;
        EmpresaId = empresaId;
        Hoje = hoje;
        SemPessoaLigada = semPessoa;
    }

    /// <summary>O alcance do perfil (o maior entre os perfis do usuário na empresa; decisão E1).</summary>
    public AlcanceComercial Alcance { get; }

    /// <summary>Vendedores cuja carteira está no alcance (vazio com alcance restrito = nenhum cadastro).</summary>
    public IReadOnlyList<FonteEscopo> Fontes { get; }

    /// <summary>Empresa ativa da sessão: só contam os vínculos dela ou sem empresa (decisão E6).</summary>
    public Guid? EmpresaId { get; }

    public DateOnly Hoje { get; }

    /// <summary>Alcance restrito, mas o usuário não está ligado a uma pessoa do cadastro (decisão E2: não vê nada).</summary>
    public bool SemPessoaLigada { get; }

    /// <summary>Sem restrição: o comportamento de antes da Fase 2.</summary>
    public bool Tudo => Alcance == AlcanceComercial.Tudo;

    /// <summary>Nenhum cadastro alcançado (alcance "Nenhum", sem pessoa ligada, ou sem carteira).</summary>
    public bool Vazio => !Tudo && Fontes.Count == 0;

    /// <summary>Alcance restrito (Minha equipe ou Minha carteira): cadastro novo segue F4 e E4.</summary>
    public bool Restrito => !Tudo;

    public static EscopoResolvido Todos(DateOnly hoje, Guid? empresaId = null) =>
        new(AlcanceComercial.Tudo, [], empresaId, hoje, semPessoa: false);

    internal static EscopoResolvido Criar(AlcanceComercial alcance, IReadOnlyList<FonteEscopo> fontes, Guid? empresaId, DateOnly hoje,
                                          bool semPessoa = false) =>
        new(alcance, fontes, empresaId, hoje, semPessoa);
}

/// <summary>
/// Escopo de acesso por registro (Motor Comercial, Fase 2a-2; decisões F2, F5 e E1 a E7). A regra é única: o banco usa a
/// mesma expressão (<see cref="VinculoNoEscopo"/>) que os testes executam em memória. Não acessa banco.
/// <list type="bullet">
/// <item>Minha carteira: os clientes em que a pessoa do usuário tem vínculo, mais os dos titulares que ela cobre hoje
/// numa ausência com "pode acessar" (também quando a cobertura é da equipe dela).</item>
/// <item>Minha equipe: o mesmo para ela e para os membros vigentes das equipes que ela lidera hoje e das equipes abaixo
/// delas.</item>
/// <item>Vínculo que conta: ativo, que não terminou antes de hoje (inclusive o que ainda vai começar; E7), da empresa ativa ou
/// sem empresa (E6).</item>
/// </list>
/// Quem não está em carteira nenhuma (fornecedor, funcionário, não cliente) fica fora de qualquer alcance restrito (F5).
/// </summary>
public static class RegrasEscopo
{
    /// <summary>
    /// O maior alcance entre os perfis (E1: como as permissões, que somam). Sem perfil nenhum: Nenhum (nunca abre por
    /// falta de dado).
    /// </summary>
    public static AlcanceComercial Maior(IEnumerable<AlcanceComercial> alcances)
    {
        var lista = alcances.Where(a => Enum.IsDefined(a)).ToList();
        return lista.Count == 0 ? AlcanceComercial.Nenhum : lista.MinBy(Largura);
    }

    /// <summary>Quanto menor, mais largo (Tudo › Minha equipe › Minha carteira › Nenhum), sem depender do valor gravado.</summary>
    private static int Largura(AlcanceComercial a) => a switch
    {
        AlcanceComercial.Tudo => 0,
        AlcanceComercial.MinhaEquipe => 1,
        AlcanceComercial.MinhaCarteira => 2,
        _ => 3
    };

    /// <summary>
    /// Resolve o escopo do dia. <paramref name="equipes"/>: todas as equipes com os membros (ativas e desativadas; só as
    /// ativas contam). <paramref name="coberturas"/>: as coberturas gravadas (as vigentes hoje, com acesso, contam).
    /// </summary>
    public static EscopoResolvido Resolver(AlcanceComercial alcance, Guid? pessoaDoUsuario, Guid? empresaId,
                                           IReadOnlyCollection<Equipe> equipes, IReadOnlyCollection<CoberturaComercial> coberturas,
                                           DateOnly hoje)
    {
        if (alcance == AlcanceComercial.Tudo) return EscopoResolvido.Todos(hoje, empresaId);
        if (alcance is not (AlcanceComercial.MinhaCarteira or AlcanceComercial.MinhaEquipe))
            return EscopoResolvido.Criar(AlcanceComercial.Nenhum, [], empresaId, hoje);
        if (pessoaDoUsuario is not { } eu)
            return EscopoResolvido.Criar(alcance, [], empresaId, hoje, semPessoa: true);

        var pessoas = new HashSet<Guid> { eu };
        if (alcance == AlcanceComercial.MinhaEquipe)
            foreach (var membro in MembrosAlcancados(equipes, eu, hoje))
                pessoas.Add(membro);

        var fontes = pessoas.Select(p => new FonteEscopo(p)).ToList();
        var equipesDasPessoas = equipes
            .Where(e => e.Ativo && e.Membros.Any(m => pessoas.Contains(m.PessoaId) && m.Vigente(hoje, hoje)))
            .Select(e => e.Id).ToHashSet();
        foreach (var c in coberturas.Where(c => c.PermiteAcesso && c.Vigente(hoje)))
        {
            var cobre = (c.SubstitutoId is { } s && pessoas.Contains(s)) ||
                        (c.EquipeSubstitutaId is { } eq && equipesDasPessoas.Contains(eq));
            if (!cobre || pessoas.Contains(c.TitularId)) continue; // a carteira inteira do titular já está no alcance
            fontes.Add(new FonteEscopo(c.TitularId, c.TipoCarteiraId, c.EmpresaId));
        }
        return EscopoResolvido.Criar(alcance, fontes.Distinct().ToList(), empresaId, hoje);
    }

    /// <summary>
    /// Os membros vigentes hoje das equipes ativas que a pessoa lidera hoje e das equipes ativas abaixo delas (sem ciclo,
    /// pela <see cref="RegrasEquipe.Descendentes"/>).
    /// </summary>
    public static HashSet<Guid> MembrosAlcancados(IReadOnlyCollection<Equipe> equipes, Guid lider, DateOnly hoje)
    {
        var ativas = equipes.Where(e => e.Ativo).ToList();
        var alcancadas = new HashSet<Guid>();
        foreach (var liderada in RegrasEquipe.LideradasPor(ativas, lider, hoje))
        {
            alcancadas.Add(liderada);
            alcancadas.UnionWith(RegrasEquipe.Descendentes(ativas, liderada));
        }
        return ativas.Where(e => alcancadas.Contains(e.Id))
            .SelectMany(e => e.Membros.Where(m => m.Vigente(hoje, hoje)).Select(m => m.PessoaId))
            .ToHashSet();
    }

    /// <summary>
    /// O vínculo da carteira que põe o cliente no alcance. A mesma expressão vai para o banco (EXISTS em CarteiraClientes,
    /// índice (VendedorId, Ativo, FimEm)) e é compilada nos testes. Com escopo Tudo ou vazio não é usada (quem aplica
    /// resolve antes). Só listas e valores capturados: o EF transforma tudo em parâmetros.
    /// </summary>
    public static Expression<Func<CarteiraCliente, bool>> VinculoNoEscopo(EscopoResolvido escopo)
    {
        var hoje = escopo.Hoje;
        var empresa = escopo.EmpresaId;
        Expression<Func<CarteiraCliente, bool>> vale = c => c.Ativo && (c.FimEm == null || c.FimEm >= hoje) &&
                                                            (c.EmpresaId == null || c.EmpresaId == empresa);

        Expression<Func<CarteiraCliente, bool>>? deQuem = null;
        foreach (var grupo in escopo.Fontes.GroupBy(f => (f.TipoCarteiraId, f.EmpresaId)))
        {
            var ids = grupo.Select(f => f.VendedorId).Distinct().ToList();
            var (tipo, empresaDaFonte) = grupo.Key;
            Expression<Func<CarteiraCliente, bool>> parte = tipo is null && empresaDaFonte is null
                ? c => ids.Contains(c.VendedorId)
                : c => ids.Contains(c.VendedorId) &&
                       (tipo == null || c.TipoCarteiraId == tipo) &&
                       (empresaDaFonte == null || c.EmpresaId == null || c.EmpresaId == empresaDaFonte);
            deQuem = deQuem is null ? parte : Combinar(deQuem, parte, Expression.OrElse);
        }
        return deQuem is null
            ? c => false
            : Combinar(vale, deQuem, Expression.AndAlso);
    }

    /// <summary>O cliente está no alcance (a mesma regra do banco, em memória): testes e conferências sem consulta.</summary>
    public static bool Alcanca(EscopoResolvido escopo, IEnumerable<CarteiraCliente> carteiraDoCliente)
    {
        if (escopo.Tudo) return true;
        if (escopo.Vazio) return false;
        var regra = VinculoNoEscopo(escopo).Compile();
        return carteiraDoCliente.Any(regra);
    }

    /// <summary>Junta duas condições sobre o mesmo vínculo num corpo só (sem Invoke, que o EF não traduz).</summary>
    private static Expression<Func<CarteiraCliente, bool>> Combinar(Expression<Func<CarteiraCliente, bool>> a,
                                                                    Expression<Func<CarteiraCliente, bool>> b,
                                                                    Func<Expression, Expression, BinaryExpression> juntar)
    {
        var parametro = a.Parameters[0];
        var corpoB = new TrocaParametro(b.Parameters[0], parametro).Visit(b.Body);
        return Expression.Lambda<Func<CarteiraCliente, bool>>(juntar(a.Body, corpoB), parametro);
    }

    private sealed class TrocaParametro(ParameterExpression de, ParameterExpression para) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == de ? para : base.VisitParameter(node);
    }

    // ---------------------------------------------------------------- Cadastro novo com alcance restrito (F4 e E4)

    /// <summary>Mensagem de E4: com alcance restrito, só clientes podem ser cadastrados (os outros sumiriam da lista).</summary>
    public const string SoClientes = "Seu alcance permite cadastrar só clientes: marque a classificação Cliente.";

    /// <summary>Tem a classificação Cliente ativa (a do sistema, copiada do cadastro de papéis).</summary>
    public static bool EhCliente(Pessoa p) => p.Papeis.Any(x => x.Ativo && x.Papel == TipoPapel.Cliente);

    /// <summary>
    /// F4: cliente novo cadastrado por quem tem alcance restrito, sem responsável da conta, recebe o próprio usuário como
    /// responsável da conta desde o dia do cadastro (sem fim, crédito pelo padrão do papel, todas as empresas; MC-9). Só
    /// num papel de responsável que a pessoa do usuário pode ocupar ("Quem pode ser": uma das
    /// <paramref name="classificacoesDoUsuario"/> aceita pelo papel), senão a gravação recusaria um vínculo que ele nem
    /// viu. Nulo quando não se aplica (não é cliente, já tem responsável, ou nenhum papel de responsável aceita o usuário):
    /// aí o cadastro é gravado e o aviso "fora do seu alcance" explica. A origem "Cadastro" é marcada por quem grava, depois
    /// de <see cref="RegrasComercial.DefinirOrigens"/>.
    /// </summary>
    public static CarteiraCliente? ResponsavelDoCadastro(Pessoa novo, Guid pessoaDoUsuario, IReadOnlySet<Guid> classificacoesDoUsuario,
                                                         IReadOnlyDictionary<Guid, TipoCarteira> tipos, DateOnly hoje)
    {
        if (!EhCliente(novo) || novo.Id == pessoaDoUsuario) return null;
        bool Responsavel(Guid tipoId) => tipos.TryGetValue(tipoId, out var t) && t.ResponsavelDaConta;
        if (novo.Carteira.Any(c => c.Ativo && Responsavel(c.TipoCarteiraId))) return null;
        var papel = tipos.Values
            .Where(t => t.Ativo && t.ResponsavelDaConta && t.ClassificacoesAceitas.Any(classificacoesDoUsuario.Contains))
            .OrderBy(t => t.Ordem).ThenBy(t => t.Nome).FirstOrDefault();
        if (papel is null) return null;
        return new CarteiraCliente
        {
            Id = IdSequencial.Novo(),
            PessoaId = novo.Id,
            TipoCarteiraId = papel.Id,
            VendedorId = pessoaDoUsuario,
            InicioEm = hoje,
            Origem = OrigemVinculoCarteira.Cadastro,
            Observacao = "Incluído no cadastro, por quem cadastrou o cliente."
        };
    }
}
