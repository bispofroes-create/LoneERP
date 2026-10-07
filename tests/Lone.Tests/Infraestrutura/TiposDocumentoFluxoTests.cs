using Lone.Application.Documentos;
using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Documentos;
using Lone.Contracts.Seguranca;
using Lone.Domain.Comum;
using Lone.Domain.Documentos;
using Lone.Domain.Enums;
using Lone.Domain.Validacao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lone.Tests.Infraestrutura;

/// <summary>
/// P1-8A: o cadastro de tipos de documento no caminho real (TipoDocumentoAppService → repositório → SQL Server), que o B0
/// apontou sem teste. Registra o comportamento de hoje (inclusive renomear tipo de sistema, que o P1-8B vai bloquear — D1)
/// e as regras novas gravadas como dados. Pulado sem LONE_TESTES_SQLSERVER.
/// </summary>
public class TiposDocumentoFluxoTests : IClassFixture<AmbienteCadastroPessoas>
{
    private static readonly Guid Rg = TiposDocumentoSistema.Id(TipoDocumento.Rg);
    private static readonly Guid Cnh = TiposDocumentoSistema.Id(TipoDocumento.Cnh);

    private readonly AmbienteCadastroPessoas _ambiente;

    public TiposDocumentoFluxoTests(AmbienteCadastroPessoas ambiente) => _ambiente = ambiente;

    private async Task<T> ComServicoAsync<T>(Func<ITipoDocumentoAppService, Task<T>> acao, params string[] negadas)
    {
        await using var requisicao = _ambiente.Requisicao();
        requisicao.ServiceProvider.GetRequiredService<UsuarioDoTeste>().Negadas.UnionWith(negadas);
        return await acao(requisicao.ServiceProvider.GetRequiredService<ITipoDocumentoAppService>());
    }

    private Task<TipoDocumentoDto> SalvarAsync(TipoDocumentoDto dto, params string[] negadas) => ComServicoAsync(s => s.SalvarAsync(dto), negadas);
    private async Task<TipoDocumentoDto> ObterAsync(Guid id) => (await ComServicoAsync(s => s.ObterAsync(id)))!;

    private static string NomeUnico(string prefixo) => $"{prefixo} {Random.Shared.Next(100000, 999999)}";

    // ---------------------------------------------------------------- comportamento que já existia

    [FatoSqlServer]
    public async Task Tipo_novo_do_usuario_nasce_sem_tipo_de_sistema_e_com_o_comportamento_de_sempre()
    {
        var salvo = await SalvarAsync(new TipoDocumentoDto { Nome = NomeUnico("Alvará"), TipoSistema = TipoDocumento.Cnh, ExigeValidade = true });

        Assert.Null(salvo.TipoSistema); // o enum não é escolhido pelo aplicativo
        Assert.True(salvo.Ordem > 0);
        Assert.Equal((true, true, true, UsoCampoDocumento.Oculto, UsoCampoDocumento.Oculto, UsoCampoDocumento.Opcional,
                      FormatoNumeroDocumento.Livre, UnicidadeDocumento.Nenhuma),
            (salvo.AplicaPessoaFisica, salvo.AplicaPessoaJuridica, salvo.AplicaEstrangeiro, salvo.UsoOrgaoEmissor, salvo.UsoUf,
             salvo.UsoEmissao, salvo.FormatoNumero, salvo.Unicidade));

        await using var db = _ambiente.Banco!.Contexto();
        Assert.True(await db.Auditoria.AnyAsync(a => a.Descricao != null && a.Descricao.Contains(salvo.Nome) && a.Descricao.Contains("criado")));
    }

    [FatoSqlServer]
    public async Task Nome_repetido_sem_contar_maiusculas_e_acentos_e_recusado()
    {
        var nome = NomeUnico("Licença");
        await SalvarAsync(new TipoDocumentoDto { Nome = nome });

        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => SalvarAsync(new TipoDocumentoDto { Nome = nome.ToUpperInvariant().Replace("Ç", "C") }));
        Assert.Contains(erro.Erros, e => e.StartsWith("Já existe o tipo de documento"));
        await Assert.ThrowsAsync<ValidacaoException>(() => SalvarAsync(new TipoDocumentoDto { Nome = "rg" }));
    }

    [FatoSqlServer]
    public async Task Tipo_de_sistema_nao_perde_o_enum_ao_ser_salvo()
    {
        var cnh = await ObterAsync(Cnh);
        cnh.TipoSistema = null;
        cnh.DiasAvisoVencimento = 45;

        var salva = await SalvarAsync(cnh);

        Assert.Equal((TipoDocumento?)TipoDocumento.Cnh, salva.TipoSistema);
        Assert.Equal(45, salva.DiasAvisoVencimento);
    }

    [FatoSqlServer]
    public async Task Tipo_de_sistema_nao_pode_ser_renomeado_D1()
    {
        // P1-8B (D1): substitui o teste que registrava o comportamento antigo ("renomear hoje é aceito") — mudança de
        // requisito autorizada, não relaxamento. O nome volta como estava e nada é gravado.
        var rg = await ObterAsync(Rg);
        rg.Nome = "Registro Geral (teste)";
        var erro = await Assert.ThrowsAsync<ValidacaoException>(() => SalvarAsync(rg));
        Assert.Contains(erro.Erros, e => e.StartsWith("O nome do tipo de sistema \"RG\" não pode ser alterado"));
        Assert.Equal(("RG", (TipoDocumento?)TipoDocumento.Rg), ((await ObterAsync(Rg)).Nome, (await ObterAsync(Rg)).TipoSistema));
    }

    [FatoSqlServer]
    public async Task Desativar_e_reativar_tipo_de_sistema_e_permitido()
    {
        var cnh = await ObterAsync(Cnh);
        var desativada = await ComServicoAsync(s => s.DesativarAsync(Cnh, new AlterarSituacaoRequisicao { Versao = cnh.Versao }));
        Assert.False(desativada.Ativo);
        var reativada = await ComServicoAsync(s => s.ReativarAsync(Cnh, new AlterarSituacaoRequisicao { Versao = desativada.Versao }));
        Assert.True(reativada.Ativo);
        Assert.Equal((TipoDocumento?)TipoDocumento.Cnh, reativada.TipoSistema);
    }

    [FatoSqlServer]
    public async Task Versao_antiga_do_tipo_da_conflito_de_edicao()
    {
        var aberto = await SalvarAsync(new TipoDocumentoDto { Nome = NomeUnico("Certidão") });
        var outraSessao = await ObterAsync(aberto.Id);
        outraSessao.DiasAvisoVencimento = 10;
        await SalvarAsync(outraSessao);

        aberto.DiasAvisoVencimento = 20;
        await Assert.ThrowsAnyAsync<ConflitoDeEdicaoException>(() => SalvarAsync(aberto));
    }

    [FatoSqlServer]
    public async Task Permissoes_do_cadastro_de_tipos()
    {
        await Assert.ThrowsAsync<AcessoNegadoException>(() => SalvarAsync(new TipoDocumentoDto { Nome = NomeUnico("Sem permissão") }, Permissoes.Cadastros.Tipos));

        // A ficha de pessoas lista os tipos só com a permissão de ver pessoas (sem a contagem de uso).
        var lista = await ComServicoAsync(s => s.ListarAsync(false), Permissoes.Cadastros.Tipos);
        Assert.Contains(lista, t => t.Id == Rg);
        Assert.All(lista, t => Assert.Equal(0, t.QuantidadeUsos));

        await Assert.ThrowsAsync<AcessoNegadoException>(() => ComServicoAsync(s => s.ListarAsync(false), Permissoes.Cadastros.Tipos, Permissoes.Pessoas.Visualizar));
    }

    // ---------------------------------------------------------------- P1-8A

    [FatoSqlServer]
    public async Task Regras_novas_do_tipo_sao_gravadas_e_devolvidas()
    {
        var salvo = await SalvarAsync(new TipoDocumentoDto
        {
            Nome = NomeUnico("Registro profissional"),
            AplicaPessoaFisica = true, AplicaPessoaJuridica = false, AplicaEstrangeiro = false,
            UsoOrgaoEmissor = UsoCampoDocumento.Obrigatorio, UsoUf = UsoCampoDocumento.Obrigatorio, UsoEmissao = UsoCampoDocumento.Oculto,
            FormatoNumero = FormatoNumeroDocumento.SomenteDigitos, TamanhoMinimoNumero = 4, TamanhoMaximoNumero = 12,
            Unicidade = UnicidadeDocumento.Aviso
        });

        var relido = await ObterAsync(salvo.Id);
        Assert.Equal((true, false, false, UsoCampoDocumento.Obrigatorio, UsoCampoDocumento.Obrigatorio, UsoCampoDocumento.Oculto,
                      FormatoNumeroDocumento.SomenteDigitos, (int?)4, (int?)12, UnicidadeDocumento.Aviso),
            (relido.AplicaPessoaFisica, relido.AplicaPessoaJuridica, relido.AplicaEstrangeiro, relido.UsoOrgaoEmissor, relido.UsoUf,
             relido.UsoEmissao, relido.FormatoNumero, relido.TamanhoMinimoNumero, relido.TamanhoMaximoNumero, relido.Unicidade));
    }

    [FatoSqlServer]
    public async Task Configuracao_invalida_e_recusada_e_bloqueio_sem_documentos_e_aceito()
    {
        // P1-8B: o bloqueio foi liberado (com diagnóstico na ativação); a recusa provisória do P1-8A deixou de existir.
        var semNinguem = new TipoDocumentoDto { Nome = NomeUnico("Ninguém"), AplicaPessoaFisica = false, AplicaPessoaJuridica = false, AplicaEstrangeiro = false };
        await Assert.ThrowsAsync<ValidacaoException>(() => SalvarAsync(semNinguem));
        var porUfSemUf = new TipoDocumentoDto { Nome = NomeUnico("Por UF"), Unicidade = UnicidadeDocumento.PorTipoEUf };
        await Assert.ThrowsAsync<ValidacaoException>(() => SalvarAsync(porUfSemUf)); // a UF precisa ser obrigatória

        var porTipo = await SalvarAsync(new TipoDocumentoDto { Nome = NomeUnico("Bloqueio"), Unicidade = UnicidadeDocumento.PorTipo });
        Assert.Equal(UnicidadeDocumento.PorTipo, porTipo.Unicidade);
        var porUf = await SalvarAsync(new TipoDocumentoDto { Nome = NomeUnico("Bloqueio UF"), Unicidade = UnicidadeDocumento.PorTipoEUf, UsoUf = UsoCampoDocumento.Obrigatorio });
        Assert.Equal(UnicidadeDocumento.PorTipoEUf, porUf.Unicidade);
    }
}
