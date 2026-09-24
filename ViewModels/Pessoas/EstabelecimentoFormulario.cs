using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Aplicacao.Integracoes;
using Lone.Core.Entidades;
using Lone.Core.Validacao;

namespace Lone.ViewModels.Pessoas
{
    /// <summary>Um estabelecimento (CNPJ) da pessoa jurídica, ou o bloco fiscal da PF/estrangeiro.</summary>
    public partial class EstabelecimentoFormulario : ObservableObject
    {
        public EstabelecimentoFormulario(ObservableCollection<EnderecoFormulario> enderecosDaPessoa)
        {
            EnderecosDisponiveis = enderecosDaPessoa;
        }

        [ObservableProperty] private int _id;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Titulo))]
        private string _cnpj = string.Empty;

        [ObservableProperty] private string _nomeFantasia = string.Empty;
        [ObservableProperty] private bool _ativo = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TextoReceita))]
        private string _situacaoReceita = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TextoReceita))]
        private DateTime? _consultadoReceitaEm;

        [ObservableProperty] private int _indicadorIndice;
        [ObservableProperty] private string _inscricaoEstadual = string.Empty;
        [ObservableProperty] private string _inscricaoMunicipal = string.Empty;
        [ObservableProperty] private string _inscricaoSuframa = string.Empty;
        [ObservableProperty] private int _regimeIndice;
        [ObservableProperty] private string _cnaePrincipal = string.Empty;
        [ObservableProperty] private string _naturezaJuridica = string.Empty;

        /// <summary>Nulo = usa o endereço fiscal/principal da pessoa.</summary>
        [ObservableProperty] private EnderecoFormulario? _enderecoFiscal;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Titulo), nameof(PodeRemover))]
        private bool _ehPrincipal;

        /// <summary>Definido pela pessoa: mostra CNPJ e ações de filial só na pessoa jurídica.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PodeRemover))]
        private bool _ehJuridica;

        /// <summary>A mesma lista de endereços da pessoa (para escolher o endereço fiscal).</summary>
        public ObservableCollection<EnderecoFormulario> EnderecosDisponiveis { get; }

        public bool PodeRemover => EhJuridica && !EhPrincipal;

        public string Titulo
        {
            get
            {
                var cnpj = Documento.Formatar(Cnpj);
                var tipo = EhPrincipal ? "Estabelecimento principal" : "Filial";
                return cnpj.Length == 0 ? tipo : $"{tipo} · {cnpj}";
            }
        }

        public string TextoReceita => SituacaoReceita.Length == 0
            ? string.Empty
            : $"Situação na Receita Federal: {SituacaoReceita}" +
              (ConsultadoReceitaEm is { } em ? $" (consultado em {em:dd/MM/yyyy HH:mm})" : string.Empty);

        public static EstabelecimentoFormulario De(Estabelecimento e, ObservableCollection<EnderecoFormulario> enderecos) =>
            new(enderecos)
            {
                Id = e.Id,
                Cnpj = Documento.Formatar(e.Cnpj),
                NomeFantasia = e.NomeFantasia ?? string.Empty,
                Ativo = e.Ativo,
                SituacaoReceita = e.SituacaoReceita ?? string.Empty,
                ConsultadoReceitaEm = e.ConsultadoReceitaEm,
                IndicadorIndice = Opcoes.Indice(e.IndicadorIE),
                InscricaoEstadual = e.InscricaoEstadual ?? string.Empty,
                InscricaoMunicipal = e.InscricaoMunicipal ?? string.Empty,
                InscricaoSuframa = e.InscricaoSuframa ?? string.Empty,
                RegimeIndice = Opcoes.Indice(e.RegimeTributario),
                CnaePrincipal = e.CnaePrincipal ?? string.Empty,
                NaturezaJuridica = e.NaturezaJuridica ?? string.Empty,
                EhPrincipal = e.Principal,
                EnderecoFiscal = e.EnderecoFiscalId is int id ? BuscarPorId(enderecos, id) : null
            };

        /// <param name="enderecos">Endereços do formulário já convertidos (mesmas instâncias da pessoa).</param>
        public Estabelecimento ParaEntidade(IReadOnlyDictionary<EnderecoFormulario, PessoaEndereco> enderecos)
        {
            var entidade = new Estabelecimento
            {
                Id = Id,
                Cnpj = Cnpj,
                Principal = EhPrincipal,
                NomeFantasia = NomeFantasia,
                Ativo = Ativo,
                SituacaoReceita = SituacaoReceita,
                ConsultadoReceitaEm = ConsultadoReceitaEm,
                IndicadorIE = Opcoes.Indicador(IndicadorIndice),
                InscricaoEstadual = InscricaoEstadual,
                InscricaoMunicipal = InscricaoMunicipal,
                InscricaoSuframa = InscricaoSuframa,
                RegimeTributario = Opcoes.Regime(RegimeIndice),
                CnaePrincipal = CnaePrincipal,
                NaturezaJuridica = NaturezaJuridica
            };

            // Endereço já gravado: basta o Id. Endereço novo: referência ao objeto (o banco liga ao gravar).
            if (EnderecoFiscal is not null && enderecos.TryGetValue(EnderecoFiscal, out var endereco))
            {
                if (endereco.Id > 0) entidade.EnderecoFiscalId = endereco.Id;
                else entidade.EnderecoFiscal = endereco;
            }

            return entidade;
        }

        public void AplicarCnpj(DadosCnpj d)
        {
            Cnpj = Documento.Formatar(d.Cnpj);
            if (d.NomeFantasia is not null) NomeFantasia = d.NomeFantasia;
            SituacaoReceita = d.SituacaoCadastral ?? string.Empty;
            ConsultadoReceitaEm = DateTime.Now;
        }

        private static EnderecoFormulario? BuscarPorId(IEnumerable<EnderecoFormulario> enderecos, int id)
        {
            foreach (var e in enderecos)
                if (e.Id == id) return e;
            return null;
        }
    }
}
