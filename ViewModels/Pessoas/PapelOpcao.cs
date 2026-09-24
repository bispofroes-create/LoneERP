using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Aplicacao.Pessoas;
using Lone.Core.Entidades;
using Lone.Core.Enums;

namespace Lone.ViewModels.Pessoas
{
    /// <summary>Um papel na tela (ligado/desligado). Guarda o registro existente para não perder o histórico.</summary>
    public partial class PapelOpcao : ObservableObject
    {
        private int _id;
        private DateOnly _inicioEm;
        private string? _observacoes;

        public PapelOpcao(TipoPapel papel)
        {
            Papel = papel;
        }

        public TipoPapel Papel { get; }
        public string Texto => PessoaResumo.NomePapel(Papel);

        /// <summary>A pessoa já teve este papel (desligar só inativa).</summary>
        public bool Existia => _id > 0;

        [ObservableProperty] private bool _ativo;

        public static PapelOpcao De(TipoPapel papel, PessoaPapel? existente) => existente is null
            ? new PapelOpcao(papel)
            : new PapelOpcao(papel)
            {
                _id = existente.Id,
                _inicioEm = existente.InicioEm,
                _observacoes = existente.Observacoes,
                Ativo = existente.Ativo
            };

        /// <summary>Nulo quando o papel nunca existiu e continua desligado.</summary>
        public PessoaPapel? ParaEntidade() => !Ativo && !Existia
            ? null
            : new PessoaPapel { Id = _id, Papel = Papel, Ativo = Ativo, InicioEm = _inicioEm, Observacoes = _observacoes };
    }
}
