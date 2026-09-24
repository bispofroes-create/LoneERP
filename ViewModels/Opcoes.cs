using System;
using Lone.Core.Enums;

namespace Lone.ViewModels
{
    /// <summary>
    /// Textos das caixas de seleção e a conversão entre a posição escolhida e o enum.
    /// A ordem de cada lista precisa bater com a ordem do array de valores correspondente.
    /// </summary>
    public static class Opcoes
    {
        public static string[] Naturezas { get; } = { "Pessoa física", "Pessoa jurídica", "Estrangeiro" };
        private static readonly NaturezaPessoa[] ValoresNatureza =
            { NaturezaPessoa.Fisica, NaturezaPessoa.Juridica, NaturezaPessoa.Estrangeiro };

        public static string[] Situacoes { get; } = { "Ativo", "Em análise", "Inativo", "Arquivado" };
        private static readonly SituacaoPessoa[] ValoresSituacao =
            { SituacaoPessoa.Ativo, SituacaoPessoa.EmAnalise, SituacaoPessoa.Inativo, SituacaoPessoa.Arquivado };

        public static string[] IndicadoresIE { get; } =
            { "Não informado", "Contribuinte do ICMS", "Contribuinte isento", "Não contribuinte" };
        private static readonly IndicadorIE[] ValoresIndicadorIE =
            { IndicadorIE.NaoInformado, IndicadorIE.Contribuinte, IndicadorIE.Isento, IndicadorIE.NaoContribuinte };

        public static string[] Regimes { get; } = { "Não informado", "Simples Nacional", "MEI", "Regime normal" };
        private static readonly RegimeTributario[] ValoresRegime =
            { RegimeTributario.NaoInformado, RegimeTributario.SimplesNacional, RegimeTributario.Mei, RegimeTributario.RegimeNormal };

        public static string[] TiposContato { get; } = { "Telefone", "Celular", "WhatsApp", "E-mail", "Outro" };
        private static readonly TipoContato[] ValoresTipoContato =
            { TipoContato.Telefone, TipoContato.Celular, TipoContato.WhatsApp, TipoContato.Email, TipoContato.Outro };

        public static string[] TiposDocumento { get; } = { "RG", "CNH", "Passaporte", "Documento estrangeiro", "Outro" };
        private static readonly TipoDocumento[] ValoresTipoDocumento =
            { TipoDocumento.Rg, TipoDocumento.Cnh, TipoDocumento.Passaporte, TipoDocumento.DocumentoEstrangeiro, TipoDocumento.Outro };

        /// <summary>Filtros da lista de pessoas (nulo = todas).</summary>
        public static TipoPapel?[] FiltrosPapel { get; } =
            { null, TipoPapel.Cliente, TipoPapel.Fornecedor, TipoPapel.EmpresaDoGrupo };

        /// <summary>Ordem em que os papéis aparecem no cadastro.</summary>
        public static TipoPapel[] PapeisNaTela { get; } =
        {
            TipoPapel.Cliente, TipoPapel.Fornecedor, TipoPapel.Vendedor, TipoPapel.Transportadora,
            TipoPapel.Representante, TipoPapel.PrestadorServico, TipoPapel.Funcionario, TipoPapel.EmpresaDoGrupo
        };

        public static NaturezaPessoa Natureza(int indice) => Valor(ValoresNatureza, indice);
        public static int Indice(NaturezaPessoa v) => Array.IndexOf(ValoresNatureza, v);

        public static SituacaoPessoa Situacao(int indice) => Valor(ValoresSituacao, indice);
        public static int Indice(SituacaoPessoa v) => Array.IndexOf(ValoresSituacao, v);

        public static IndicadorIE Indicador(int indice) => Valor(ValoresIndicadorIE, indice);
        public static int Indice(IndicadorIE v) => Array.IndexOf(ValoresIndicadorIE, v);

        public static RegimeTributario Regime(int indice) => Valor(ValoresRegime, indice);
        public static int Indice(RegimeTributario v) => Array.IndexOf(ValoresRegime, v);

        public static TipoContato TipoDeContato(int indice) => Valor(ValoresTipoContato, indice);
        public static int Indice(TipoContato v) => Array.IndexOf(ValoresTipoContato, v);

        public static TipoDocumento TipoDeDocumento(int indice) => Valor(ValoresTipoDocumento, indice);
        public static int Indice(TipoDocumento v) => Array.IndexOf(ValoresTipoDocumento, v);

        private static T Valor<T>(T[] valores, int indice) => valores[indice >= 0 && indice < valores.Length ? indice : 0];
    }
}
