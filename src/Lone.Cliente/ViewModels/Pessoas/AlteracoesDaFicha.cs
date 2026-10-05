using Lone.Cliente.ViewModels.Comum;
using Lone.Contracts.Pessoas;
using Lone.Domain.Pessoas;
using CepValor = Lone.Domain.ObjetosDeValor.Cep;
using DocumentoFiscal = Lone.Domain.Validacao.Documento;

namespace Lone.Cliente.ViewModels.Pessoas;

/// <summary>
/// Os campos da ficha que o destaque acompanha, como texto de tela, a partir do DTO (o mesmo que vai para a API e que a
/// detecção de "Alterações não salvas" compara). Cobre o que a consulta de CNPJ preenche e os dados principais:
/// identificação, fiscal de cada estabelecimento, endereços e telefones/e-mails. A chave é a mesma dos erros
/// (<see cref="CamposFichaPessoa"/> + Id do registro), para o destaque cair no controle certo.
/// </summary>
public static class AlteracoesDaFicha
{
    /// <summary>Campo → valor como se lê na tela ("" = vazio).</summary>
    public static Dictionary<ChaveCampo, string> Valores(PessoaDto d)
    {
        var v = new Dictionary<ChaveCampo, string>();
        void Por(string campo, Guid? item, string? valor) => v[new ChaveCampo(campo, item)] = (valor ?? string.Empty).Trim();

        Por(CamposFichaPessoa.Nome, null, d.Nome);
        if (d.DocumentoPrincipal is not null) Por(CamposFichaPessoa.Documento, null, d.DocumentoPrincipal);
        Por(CamposFichaPessoa.DataNascimento, null, TextoTela.Data(d.DataNascimento));
        Por(CamposFichaPessoa.DataAbertura, null, TextoTela.Data(d.DataAbertura));
        Por(CamposFichaPessoa.Porte, null, d.Porte);
        Por(CamposFichaPessoa.CapitalSocial, null, TextoTela.Decimal(d.CapitalSocial));
        Por(CamposFichaPessoa.NomeMae, null, d.NomeMae);
        Por(CamposFichaPessoa.NomePai, null, d.NomePai);

        foreach (var e in d.Estabelecimentos)
        {
            // O principal mostra CNPJ, nome fantasia e natureza jurídica na Identificação (sem item); a filial, no cartão dela.
            Guid? doCartao = e.Principal ? null : e.Id;
            Por(e.Principal ? CamposFichaPessoa.Documento : CamposFichaPessoa.Cnpj, doCartao, e.Cnpj is null ? null : DocumentoFiscal.Formatar(e.Cnpj));
            Por(CamposFichaPessoa.NomeFantasia, doCartao, e.NomeFantasia);
            Por(CamposFichaPessoa.NaturezaJuridica, doCartao, e.NaturezaJuridica);
            Por(CamposFichaPessoa.Cnae, e.Id, e.CnaePrincipal);
            Por(CamposFichaPessoa.CnaesSecundarios, e.Id, e.CnaesSecundarios);
            Por(CamposFichaPessoa.InscricaoEstadual, e.Id, e.InscricaoEstadual);
            Por(CamposFichaPessoa.IndicadorIE, e.Id, Opcao.De(OpcoesPessoa.IndicadoresIE, e.IndicadorIE).Texto);
            Por(CamposFichaPessoa.Regime, e.Id, Opcao.De(OpcoesPessoa.Regimes, e.RegimeTributario).Texto);
        }

        foreach (var e in d.Enderecos.Where(e => e.Ativo))
        {
            Por(CamposFichaPessoa.Cep, e.Id, CepValor.TentarCriar(e.Cep, out var cep) ? cep!.Formatado : e.Cep);
            Por(CamposFichaPessoa.Logradouro, e.Id, e.Logradouro);
            Por(CamposFichaPessoa.Numero, e.Id, e.Numero);
            Por(CamposFichaPessoa.Complemento, e.Id, e.Complemento);
            Por(CamposFichaPessoa.Bairro, e.Id, e.Bairro);
            Por(CamposFichaPessoa.Municipio, e.Id, e.MunicipioId is null ? null : string.IsNullOrWhiteSpace(e.Uf) ? e.Cidade : $"{e.Cidade}/{e.Uf}");
        }

        foreach (var m in d.MeiosContato.Where(m => m.Ativo))
            Por(CamposFichaPessoa.MeioContatoValor, m.Id, m.Valor);

        return v;
    }

    /// <summary>
    /// Os campos diferentes entre o gravado e o atual, com o valor de antes (registro novo: antes vazio). Campos vazios dos
    /// dois lados e diferenças só de maiúsculas/espaços/pontuação de número não contam.
    /// </summary>
    public static IReadOnlyList<(ChaveCampo Chave, string Antes, string Agora)> Comparar(PessoaDto gravado, PessoaDto atual)
    {
        var antes = Valores(gravado);
        var agora = Valores(atual);
        var diferentes = new List<(ChaveCampo, string, string)>();
        foreach (var chave in agora.Keys.Union(antes.Keys))
        {
            var a = antes.GetValueOrDefault(chave, string.Empty);
            var b = agora.GetValueOrDefault(chave, string.Empty);
            if (!AplicacaoReceita.Igual(a, b)) diferentes.Add((chave, a, b));
        }
        return diferentes;
    }
}
