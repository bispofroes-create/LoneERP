using Lone.Cliente.ViewModels.Territorios;
using Lone.Contracts.Comercial;
using Lone.Contracts.Empresas;
using Lone.Contracts.Enderecos;
using Lone.Contracts.Territorios;
using Lone.Domain.Enderecos;
using Lone.Domain.Enums;
using Lone.Domain.Papeis;

namespace Lone.Tests.Cliente;

/// <summary>
/// Telas dos territórios (Fase 2b-1a): árvore achatada em ordem com recuo e caminho, opções de "território acima" sem ciclo,
/// responsáveis filtrados pela função, ficha que manda o DTO certo (inclusive o anulado) e mapa novo com o padrão da empresa
/// pequena (exclusivo, universo Cliente, endereço Comercial) e travas quando em uso.
/// </summary>
public class TerritoriosTelasTests
{
    private static readonly Guid MapaId = Guid.NewGuid();
    private static readonly Guid Geografico = Guid.NewGuid();

    private static TerritorioResumoDto T(string nome, TerritorioResumoDto? pai = null, bool encerrado = false, bool comUso = false) => new()
    {
        Id = Guid.NewGuid(), MapaId = MapaId, Codigo = nome.ToUpperInvariant(), Nome = nome, TipoId = Geografico, Tipo = "Geográfico", PaiId = pai?.Id,
        Situacao = encerrado ? SituacaoTerritorio.Encerrado : SituacaoTerritorio.Ativo, FimEm = encerrado ? new DateOnly(2026, 9, 1) : null, ComUso = comUso
    };

    [Fact]
    public void Arvore_vem_em_ordem_com_nivel_caminho_e_sem_os_encerrados_por_padrao()
    {
        var brasil = T("Brasil");
        var sudeste = T("Sudeste", brasil);
        var mg = T("MG", sudeste);
        var curvelo = T("Curvelo", mg);
        var bh = T("Belo Horizonte", mg);
        var norte = T("Norte", brasil, encerrado: true);
        var todos = new[] { curvelo, norte, mg, brasil, bh, sudeste };

        var linhas = ArvoreTerritorios.Linhas(todos, incluirEncerrados: false);
        Assert.Equal(new[] { "Brasil", "Sudeste", "MG", "Belo Horizonte", "Curvelo" }, linhas.Select(l => l.Nome).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4, 4 }, linhas.Select(l => l.Nivel).ToArray());
        Assert.Equal("Brasil › Sudeste › MG", linhas[4].Caminho);
        Assert.Equal(3 * LinhaTerritorio.RecuoPorNivel, linhas[4].Recuo);
        Assert.Equal("▾", linhas[2].Marcador); // MG tem filhos
        Assert.Equal("•", linhas[4].Marcador);

        var comEncerrados = ArvoreTerritorios.Linhas(todos, incluirEncerrados: true);
        var linhaNorte = comEncerrados.Single(l => l.Nome == "Norte");
        Assert.True(linhaNorte.Encerrado);
        Assert.Contains("encerrado em 01/09/2026", linhaNorte.Detalhe);
        Assert.Equal("Norte", comEncerrados.Last().Nome); // encerrados depois dos ativos do mesmo nível
    }

    [Fact]
    public void Territorio_acima_tira_o_proprio_os_de_baixo_e_os_encerrados_mas_mantem_o_atual()
    {
        var brasil = T("Brasil");
        var sudeste = T("Sudeste", brasil);
        var mg = T("MG", sudeste);
        var velho = T("Velho", encerrado: true);
        var todos = new[] { brasil, sudeste, mg, velho };

        var opcoes = ArvoreTerritorios.PaisPossiveis(sudeste.Id, todos, brasil.Id);
        Assert.Equal(new Guid?[] { null, brasil.Id }, opcoes.Select(o => o.Valor).ToArray()); // sem Sudeste e sem MG (abaixo)

        var comAtualEncerrado = ArvoreTerritorios.PaisPossiveis(mg.Id, todos, velho.Id);
        Assert.Equal("Velho (encerrado)", comAtualEncerrado.Single(o => o.Valor == velho.Id).Texto);
        Assert.Equal("Brasil › Sudeste", comAtualEncerrado.Single(o => o.Valor == sudeste.Id).Texto);
    }

    private static TerritoriosOpcoesDto Opcoes()
    {
        var vendedor = Guid.NewGuid();
        var funcionario = Guid.NewGuid();
        return new TerritoriosOpcoesDto
        {
            Tipos = [new TipoTerritorioDto { Id = Geografico, Codigo = "GEOGRAFICO", Nome = "Geográfico" }],
            Funcoes =
            [
                new TipoCarteiraDto { Id = Guid.NewGuid(), Nome = "Vendedor", Classificacoes = [vendedor] },
                new TipoCarteiraDto { Id = Guid.NewGuid(), Nome = "Supervisor", Classificacoes = [funcionario] }
            ],
            Pessoas = [new AtendenteOpcaoDto(Guid.NewGuid(), "João", [vendedor]), new AtendenteOpcaoDto(Guid.NewGuid(), "Maria", [funcionario])],
            Equipes = [new Lone.Contracts.Colaboradores.PessoaOpcaoDto(Guid.NewGuid(), "Equipe Sul")],
            Empresas = [new EmpresaResumo(Guid.NewGuid(), "Loja Centro", true)],
            Classificacoes =
            [
                new ClassificacaoOpcaoDto(PapeisSistema.Id(TipoPapel.Cliente), "Cliente", true),
                new ClassificacaoOpcaoDto(Guid.NewGuid(), "Prospect", true)
            ],
            Finalidades =
            [
                new FinalidadeEnderecoDto { Id = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Comercial), Nome = "Comercial", Ativo = true },
                new FinalidadeEnderecoDto { Id = FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Entrega), Nome = "Entrega", Ativo = true }
            ]
        };
    }

    [Fact]
    public void Responsavel_so_oferece_quem_pode_ocupar_a_funcao_e_manda_pessoa_ou_equipe()
    {
        var opcoes = Opcoes();
        var r = ResponsavelTerritorioFormulario.Novo(opcoes);

        r.Funcao = r.Funcoes.Single(f => f.Texto == "Vendedor");
        Assert.Equal(new[] { "—", "João" }, r.Pessoas.Select(p => p.Texto).ToArray());
        r.Funcao = r.Funcoes.Single(f => f.Texto == "Supervisor");
        Assert.Equal(new[] { "—", "Maria" }, r.Pessoas.Select(p => p.Texto).ToArray());

        r.Pessoa = r.Pessoas.Single(p => p.Texto == "Maria");
        var dto = r.ParaDto();
        Assert.Equal(opcoes.Pessoas[1].Id, dto.PessoaId);
        Assert.Null(dto.EquipeId);

        r.TipoQuem = ResponsavelTerritorioFormulario.TiposQuem[1];
        r.Equipe = r.Equipes.Single(e => e.Texto == "Equipe Sul");
        dto = r.ParaDto();
        Assert.Null(dto.PessoaId);
        Assert.Equal(opcoes.Equipes[0].Id, dto.EquipeId);
    }

    [Fact]
    public void Ficha_nova_abaixo_de_outro_herda_o_tipo_e_manda_o_pai_e_o_inicio()
    {
        var opcoes = Opcoes();
        var mg = T("MG");
        var f = TerritorioEdicao.Criar(MapaId, opcoes, [mg], mg.Id);
        f.Codigo = "CURVELO";
        f.Nome = "Curvelo";

        Assert.Empty(f.ValidarLocalmente());
        var dto = f.ParaDto();
        Assert.Equal(mg.Id, dto.PaiId);
        Assert.Equal(Geografico, dto.TipoId);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), dto.InicioEm);
        Assert.True(f.PodeMover);
    }

    [Fact]
    public void Remover_responsavel_gravado_que_nao_comecou_vai_como_anulado_e_com_uso_nao_move()
    {
        var opcoes = Opcoes();
        var curvelo = T("Curvelo", comUso: true);
        var norte = T("Norte");
        curvelo.PaiId = norte.Id;
        var futuro = new TerritorioResponsavelDto
        {
            Id = Guid.NewGuid(), PessoaId = opcoes.Pessoas[0].Id, Nome = "João", TipoCarteiraId = opcoes.Funcoes[0].Id,
            InicioEm = DateOnly.FromDateTime(DateTime.Today).AddDays(10)
        };
        var dto = new TerritorioDto
        {
            Id = norte.Id, MapaId = MapaId, Codigo = "NORTE", Nome = "Norte", TipoId = Geografico, InicioEm = new DateOnly(2026, 1, 1),
            Situacao = SituacaoTerritorio.Ativo, Responsaveis = [futuro]
        };
        var f = TerritorioEdicao.De(dto, opcoes, [norte, curvelo]);

        Assert.False(f.PodeMover); // Curvelo, abaixo, tem uso
        Assert.True(f.InicioSomenteLeitura);
        var r = Assert.Single(f.Responsaveis);
        Assert.True(r.PodeRemover);
        r.RemoverCommand.Execute(null);

        Assert.Empty(f.Responsaveis);
        var enviado = Assert.Single(f.ParaDto().Responsaveis);
        Assert.Equal(futuro.Id, enviado.Id);
        Assert.False(enviado.Ativo);
    }

    [Fact]
    public void Mapa_novo_nasce_exclusivo_com_cliente_e_endereco_comercial_e_em_uso_trava()
    {
        var opcoes = Opcoes();
        var novo = MapaTerritorialEdicao.Criar(opcoes);
        novo.Codigo = "GEOGRAFIA";
        novo.Nome = "Geografia";

        var dto = novo.ParaDto();
        Assert.True(dto.Exclusivo);
        Assert.Null(dto.EmpresaId);
        Assert.Equal(FinalidadesEnderecoIniciais.Id(FinalidadesEnderecoIniciais.Comercial), dto.FinalidadeEnderecoReferenciaId);
        Assert.Equal(new[] { PapeisSistema.Id(TipoPapel.Cliente) }, dto.Classificacoes.ToArray());
        Assert.True(novo.PodeMudarEstrutura);
        Assert.False(novo.CodigoSomenteLeitura);

        var emUso = MapaTerritorialEdicao.De(new MapaTerritorialDto
        {
            Id = Guid.NewGuid(), Codigo = "GEOGRAFIA", Nome = "Geografia", EmUso = true, Classificacoes = dto.Classificacoes,
            FinalidadeEnderecoReferenciaId = dto.FinalidadeEnderecoReferenciaId
        }, opcoes);
        Assert.False(emUso.PodeMudarEstrutura);
        Assert.True(emUso.CodigoSomenteLeitura);
        Assert.Contains("em uso", emUso.SituacaoTexto);

        novo.Classificacoes.ToList().ForEach(c => c.Marcado = false);
        Assert.Contains(novo.ValidarLocalmente(), e => e.Contains("universo"));
    }
}
