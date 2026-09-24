using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Aplicacao.Integracoes;
using Lone.Core.Entidades;
using Lone.Core.Enums;

namespace Lone.ViewModels.Pessoas
{
    /// <summary>Um endereço da pessoa, com as finalidades marcáveis (principal, fiscal, cobrança...).</summary>
    public partial class EnderecoFormulario : ObservableObject
    {
        [ObservableProperty] private int _id;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Resumo))]
        private string _descricao = string.Empty;

        // bool? porque é o tipo do CheckBox.IsChecked.
        [ObservableProperty] private bool? _principal = false;
        [ObservableProperty] private bool? _fiscal = false;
        [ObservableProperty] private bool? _cobranca = false;
        [ObservableProperty] private bool? _entrega = false;
        [ObservableProperty] private bool? _correspondencia = false;
        [ObservableProperty] private bool? _residencial = false;
        [ObservableProperty] private bool? _comercial = true;

        [ObservableProperty] private string _cep = string.Empty;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(Resumo))] private string _logradouro = string.Empty;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(Resumo))] private string _numero = string.Empty;
        [ObservableProperty] private string _complemento = string.Empty;
        [ObservableProperty] private string _bairro = string.Empty;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(Resumo))] private string _cidade = string.Empty;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(Resumo))] private string _uf = string.Empty;
        [ObservableProperty] private string _codigoMunicipioIbge = string.Empty;
        [ObservableProperty] private string _codigoPais = PessoaEndereco.CodigoPaisBrasil;
        [ObservableProperty] private string _pais = "Brasil";

        /// <summary>Texto curto para listas (ex.: escolha do endereço fiscal de um estabelecimento).</summary>
        public string Resumo
        {
            get
            {
                var linha = string.Join(", ", new[] { Logradouro, Numero }.Where(s => !string.IsNullOrWhiteSpace(s)));
                var local = string.Join("/", new[] { Cidade, Uf }.Where(s => !string.IsNullOrWhiteSpace(s)));
                var texto = string.Join(" - ", new[] { linha, local }.Where(s => s.Length > 0));
                if (texto.Length == 0) texto = "(endereço sem logradouro)";
                return string.IsNullOrWhiteSpace(Descricao) ? texto : $"{Descricao}: {texto}";
            }
        }

        public static EnderecoFormulario De(PessoaEndereco e) => new()
        {
            Id = e.Id,
            Descricao = e.Descricao ?? string.Empty,
            Principal = e.Tem(FinalidadeEndereco.Principal),
            Fiscal = e.Tem(FinalidadeEndereco.Fiscal),
            Cobranca = e.Tem(FinalidadeEndereco.Cobranca),
            Entrega = e.Tem(FinalidadeEndereco.Entrega),
            Correspondencia = e.Tem(FinalidadeEndereco.Correspondencia),
            Residencial = e.Tem(FinalidadeEndereco.Residencial),
            Comercial = e.Tem(FinalidadeEndereco.Comercial),
            Cep = e.Cep ?? string.Empty,
            Logradouro = e.Logradouro,
            Numero = e.Numero ?? string.Empty,
            Complemento = e.Complemento ?? string.Empty,
            Bairro = e.Bairro ?? string.Empty,
            Cidade = e.Cidade,
            Uf = e.Uf ?? string.Empty,
            CodigoMunicipioIbge = e.CodigoMunicipioIbge ?? string.Empty,
            CodigoPais = e.CodigoPais,
            Pais = e.Pais
        };

        public PessoaEndereco ParaEntidade() => new()
        {
            Id = Id,
            Descricao = Descricao,
            Finalidades = Finalidades(),
            Cep = Cep,
            Logradouro = Logradouro,
            Numero = Numero,
            Complemento = Complemento,
            Bairro = Bairro,
            Cidade = Cidade,
            Uf = Uf,
            CodigoMunicipioIbge = CodigoMunicipioIbge,
            CodigoPais = CodigoPais,
            Pais = Pais
        };

        private FinalidadeEndereco Finalidades()
        {
            var marcadas = new List<(bool? Marcado, FinalidadeEndereco Valor)>
            {
                (Principal, FinalidadeEndereco.Principal),
                (Fiscal, FinalidadeEndereco.Fiscal),
                (Cobranca, FinalidadeEndereco.Cobranca),
                (Entrega, FinalidadeEndereco.Entrega),
                (Correspondencia, FinalidadeEndereco.Correspondencia),
                (Residencial, FinalidadeEndereco.Residencial),
                (Comercial, FinalidadeEndereco.Comercial)
            };
            return marcadas.Where(m => m.Marcado == true)
                .Aggregate(FinalidadeEndereco.Nenhuma, (total, m) => total | m.Valor);
        }

        /// <summary>Preenche com o resultado da consulta de CEP, sem apagar o que a consulta não trouxe.</summary>
        public void AplicarCep(DadosCep d)
        {
            Cep = d.Cep;
            if (d.Logradouro is not null) Logradouro = d.Logradouro;
            if (d.Bairro is not null) Bairro = d.Bairro;
            if (d.Cidade is not null) Cidade = d.Cidade;
            if (d.Uf is not null) Uf = d.Uf;
            if (d.CodigoMunicipioIbge is not null) CodigoMunicipioIbge = d.CodigoMunicipioIbge;
            if (string.IsNullOrWhiteSpace(Complemento) && d.Complemento is not null) Complemento = d.Complemento;
            CodigoPais = PessoaEndereco.CodigoPaisBrasil;
            Pais = "Brasil";
        }

        public void AplicarCnpj(DadosCnpj d)
        {
            if (d.Cep is not null) Cep = d.Cep;
            if (d.Logradouro is not null) Logradouro = d.Logradouro;
            if (d.Numero is not null) Numero = d.Numero;
            if (d.Complemento is not null) Complemento = d.Complemento;
            if (d.Bairro is not null) Bairro = d.Bairro;
            if (d.Cidade is not null) Cidade = d.Cidade;
            if (d.Uf is not null) Uf = d.Uf;
            if (d.CodigoMunicipioIbge is not null) CodigoMunicipioIbge = d.CodigoMunicipioIbge;
            CodigoPais = PessoaEndereco.CodigoPaisBrasil;
            Pais = "Brasil";
        }
    }
}
