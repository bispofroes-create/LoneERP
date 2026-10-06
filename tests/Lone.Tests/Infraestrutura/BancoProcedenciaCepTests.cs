using Lone.Application.Integracoes.ConferenciaCep;
using Lone.Application.Pessoas;
using Lone.Contracts.Integracoes;
using Lone.Contracts.Pessoas;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Lone.Infrastructure.Persistencia;
using Lone.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// F2, DM3, no SQL Server com o gatilho da Auditoria: o CEP aplicado a partir da sugestão é gravado pelo usuário
/// (Origem = Usuario, sem motivo do sistema) e a procedência vira uma linha Evento na mesma operação. Nenhuma linha da
/// Auditoria é alterada ou apagada. Pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public class BancoProcedenciaCepTests : IClassFixture<BancoComAuditoriaProtegida>
{
    private static readonly DateOnly Hoje = new(2026, 10, 5);
    private readonly BancoComAuditoriaProtegida _fixture;

    public BancoProcedenciaCepTests(BancoComAuditoriaProtegida fixture) => _fixture = fixture;

    private BancoDeTeste Banco => _fixture.Banco!;
    private LoneDbContext Db() => Banco.Contexto();

    private PessoaRepositorio Repositorio() =>
        new(new MotorTerritorialTeste.Fabrica(Banco), new MotorTerritorialTeste.UsuarioTeste(), new MotorTerritorialTeste.EscopoTudo(Hoje));

    [FatoSqlServer]
    public async Task Cep_aplicado_da_sugestao_grava_como_usuario_com_a_procedencia_em_evento()
    {
        var id = Guid.NewGuid();
        var enderecoId = Guid.NewGuid();
        var p = new Pessoa { Id = id, Nome = "F2 " + id.ToString("N")[..6], Natureza = NaturezaPessoa.Juridica };
        p.Enderecos.Add(new PessoaEndereco
        {
            Id = enderecoId, PessoaId = id, Cep = "35790000", Logradouro = "Rua Barão", Numero = "150", Bairro = "Centro", Cidade = "Curvelo", Uf = "MG"
        });
        var repo = Repositorio();
        await repo.SalvarAsync(p, nova: true, OrigemAlteracao.Usuario, CancellationToken.None);
        int linhasAntes;
        await using (var db = Db()) linhasAntes = await db.Auditoria.CountAsync();

        // A ficha aplicou a sugestão (alteração não salva) e o usuário salvou.
        var relogio = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var emitidas = new SugestoesCepEmitidas(relogio);
        emitidas.Registrar("35790000", "35790001", CepFonte.ViaCep);
        var anterior = (await repo.ObterAsync(id, CancellationToken.None))!;
        var dados = (await repo.ObterAsync(id, CancellationToken.None))!;
        dados.Enderecos.Single().Cep = "35790001";
        var enviado = new EnderecoDto
        {
            Id = enderecoId, SugestaoCepAplicada = new SugestaoCepAplicadaDto { CepConferido = "35790000", CepSugerido = "35790001", Fonte = CepFonte.ViaCep }
        };
        foreach (var frase in ProcedenciaSugestaoCep.Frases([enviado], dados.Enderecos, anterior.Enderecos, emitidas.FoiEmitida))
            dados.RegistrarEvento(frase);
        await repo.SalvarAsync(dados, nova: false, OrigemAlteracao.Usuario, CancellationToken.None);

        await using var leitura = Db();
        var novas = await leitura.Auditoria.Where(a => a.RaizId == id).OrderBy(a => a.Id).ToListAsync();
        var cep = Assert.Single(novas, a => a.Acao == AcaoAuditoria.Alteracao && a.Campo == "Cep");
        var evento = Assert.Single(novas, a => a.Acao == AcaoAuditoria.Evento && a.Descricao!.Contains("sugestão da conferência"));

        Assert.Equal(("35790000", "35790001"), (cep.ValorAnterior, cep.ValorNovo));
        Assert.Equal(OrigemAlteracao.Usuario, cep.Origem);                 // origem da alteração = usuário
        Assert.Equal(OrigemAlteracao.Usuario, evento.Origem);
        Assert.Equal(cep.OperacaoId, evento.OperacaoId);                   // mesma gravação
        Assert.Null(cep.Motivo);                                           // o sistema não escreve no Motivo
        Assert.Contains("fonte: ViaCEP", evento.Descricao);                // fonte da sugestão ≠ origem
        Assert.Contains("35790-000 → 35790-001", evento.Descricao);
        Assert.DoesNotContain(novas, a => a.Origem == OrigemAlteracao.ConsultaExterna);
        Assert.True(await leitura.Auditoria.CountAsync() > linhasAntes);   // só inclusão
        Assert.Equal("35790001", (await leitura.PessoaEnderecos.SingleAsync(e => e.Id == enderecoId)).Cep);
    }
}
