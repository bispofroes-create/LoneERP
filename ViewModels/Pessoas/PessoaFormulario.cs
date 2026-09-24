using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Lone.Aplicacao.Integracoes;
using Lone.Core.Entidades;
using Lone.Core.Enums;
using Lone.Core.Validacao;

namespace Lone.ViewModels.Pessoas
{
    /// <summary>
    /// Cópia editável de uma pessoa. Cada seção da tela tem sua classe (estabelecimento, endereço,
    /// contato, documento, papel, contas); esta classe só compõe e converte o conjunto.
    /// </summary>
    public partial class PessoaFormulario : ObservableObject
    {
        // Campos que esta tela ainda não edita, mas precisam voltar intactos ao salvar.
        private int? _grupoEconomicoId;
        private int? _mescladaEmId;
        private List<ContaCliente> _outrasContasCliente = new();
        private List<ContaFornecedor> _outrasContasFornecedor = new();

        public PessoaFormulario()
        {
            foreach (var papel in Opcoes.PapeisNaTela)
                Papeis.Add(new PapelOpcao(papel));
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CodigoTexto))]
        private int _id;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CodigoTexto))]
        private int _codigo;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EhFisica), nameof(EhJuridica), nameof(EhEstrangeiro),
                                  nameof(RotuloNome), nameof(RotuloDocumento))]
        private int _naturezaIndice;

        [ObservableProperty] private int _situacaoIndice;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Titulo))]
        private string _nome = string.Empty;

        [ObservableProperty] private string _nomeSocial = string.Empty;
        [ObservableProperty] private string _nomeExibicao = string.Empty;
        [ObservableProperty] private string _apelido = string.Empty;

        /// <summary>CPF (PF) ou identificação do estrangeiro. Na PJ, o CNPJ fica nos estabelecimentos.</summary>
        [ObservableProperty] private string _documento = string.Empty;

        [ObservableProperty] private DateTimeOffset? _dataNascimento;
        [ObservableProperty] private string _observacoes = string.Empty;

        public byte[]? Versao { get; private set; }

        public ObservableCollection<EstabelecimentoFormulario> Estabelecimentos { get; } = new();
        public ObservableCollection<EnderecoFormulario> Enderecos { get; } = new();
        public ObservableCollection<MeioContatoFormulario> MeiosContato { get; } = new();
        public ObservableCollection<ContatoPessoaFormulario> Contatos { get; } = new();
        public ObservableCollection<DocumentoFormulario> Documentos { get; } = new();
        public ObservableCollection<PapelOpcao> Papeis { get; } = new();

        public ContaClienteFormulario ContaCliente { get; private set; } = new();
        public ContaFornecedorFormulario ContaFornecedor { get; private set; } = new();

        /// <summary>Bloqueios ativos (só leitura aqui; bloquear/liberar vem na etapa 7).</summary>
        public string TextoBloqueios { get; private set; } = string.Empty;

        // ---- Calculados ----
        public EstabelecimentoFormulario Principal => Estabelecimentos[0];
        public PapelOpcao PapelCliente => Papeis.First(p => p.Papel == TipoPapel.Cliente);
        public PapelOpcao PapelFornecedor => Papeis.First(p => p.Papel == TipoPapel.Fornecedor);

        public bool EhFisica => Opcoes.Natureza(NaturezaIndice) == NaturezaPessoa.Fisica;
        public bool EhJuridica => Opcoes.Natureza(NaturezaIndice) == NaturezaPessoa.Juridica;
        public bool EhEstrangeiro => Opcoes.Natureza(NaturezaIndice) == NaturezaPessoa.Estrangeiro;

        public string RotuloNome => EhJuridica ? "Razão social *" : "Nome completo *";
        public string RotuloDocumento => EhFisica ? "CPF" : "Identificação estrangeira";
        public string Titulo => string.IsNullOrWhiteSpace(Nome) ? "Nova pessoa" : Nome;
        public string CodigoTexto => Id == 0 ? "Novo cadastro" : $"Código {Codigo:000000}";

        partial void OnNaturezaIndiceChanged(int value)
        {
            foreach (var e in Estabelecimentos)
                e.EhJuridica = EhJuridica;
        }

        // ---- Criação e conversão ----

        public static PessoaFormulario Nova()
        {
            var f = new PessoaFormulario();
            f.PapelCliente.Ativo = true;
            f.AdicionarEndereco(new EnderecoFormulario { Principal = true });
            f.AdicionarEstabelecimento();
            return f;
        }

        public static PessoaFormulario De(Pessoa p)
        {
            var f = new PessoaFormulario
            {
                _grupoEconomicoId = p.GrupoEconomicoId,
                _mescladaEmId = p.MescladaEmId,
                Id = p.Id,
                Codigo = p.Codigo,
                Versao = p.Versao,
                NaturezaIndice = Opcoes.Indice(p.Natureza),
                SituacaoIndice = Opcoes.Indice(p.Situacao),
                Nome = p.Nome,
                NomeSocial = p.NomeSocial ?? string.Empty,
                NomeExibicao = p.NomeExibicao ?? string.Empty,
                Apelido = p.Apelido ?? string.Empty,
                Documento = p.Natureza == NaturezaPessoa.Juridica ? string.Empty
                    : p.Natureza == NaturezaPessoa.Fisica ? Core.Validacao.Documento.Formatar(p.DocumentoPrincipal)
                    : p.DocumentoPrincipal ?? string.Empty,
                DataNascimento = Datas.ParaTela(p.DataNascimento),
                Observacoes = p.Observacoes ?? string.Empty,
                ContaCliente = ContaClienteFormulario.De(p.ContasCliente.FirstOrDefault(c => c.EmpresaId is null)),
                ContaFornecedor = ContaFornecedorFormulario.De(p.ContasFornecedor.FirstOrDefault(c => c.EmpresaId is null)),
                _outrasContasCliente = p.ContasCliente.Where(c => c.EmpresaId is not null).ToList(),
                _outrasContasFornecedor = p.ContasFornecedor.Where(c => c.EmpresaId is not null).ToList(),
                TextoBloqueios = string.Join("\n", p.Bloqueios.Where(b => b.Ativo)
                    .Select(b => $"{b.Escopo} desde {b.InicioEm:dd/MM/yyyy} por {b.InicioPor}: {b.Motivo}"))
            };

            foreach (var e in p.Enderecos.OrderBy(e => e.Ordem))
                f.AdicionarEndereco(EnderecoFormulario.De(e));

            foreach (var e in p.Estabelecimentos.OrderByDescending(e => e.Principal).ThenBy(e => e.Cnpj))
            {
                var estabelecimento = EstabelecimentoFormulario.De(e, f.Enderecos);
                estabelecimento.EhJuridica = f.EhJuridica;
                f.Estabelecimentos.Add(estabelecimento);
            }
            if (f.Estabelecimentos.Count == 0)
                f.AdicionarEstabelecimento();
            f.MarcarPrincipal();

            foreach (var m in p.MeiosContato.OrderBy(m => m.Tipo).ThenByDescending(m => m.Principal))
                f.MeiosContato.Add(MeioContatoFormulario.De(m));
            foreach (var c in p.Contatos.OrderByDescending(c => c.Principal).ThenBy(c => c.Nome))
                f.Contatos.Add(ContatoPessoaFormulario.De(c));
            foreach (var d in p.Documentos)
                f.Documentos.Add(DocumentoFormulario.De(d));

            f.Papeis.Clear();
            foreach (var papel in Opcoes.PapeisNaTela)
                f.Papeis.Add(PapelOpcao.De(papel, p.Papeis.FirstOrDefault(x => x.Papel == papel)));

            return f;
        }

        public Pessoa ParaEntidade()
        {
            // Endereços primeiro: os estabelecimentos referenciam estas mesmas instâncias.
            var enderecos = Enderecos.ToDictionary(e => e, e => e.ParaEntidade());

            var natureza = Opcoes.Natureza(NaturezaIndice);
            var estabelecimentos = (natureza == NaturezaPessoa.Juridica ? Estabelecimentos.ToList() : new() { Principal })
                .Select(e => e.ParaEntidade(enderecos))
                .ToList();

            var pessoa = new Pessoa
            {
                Id = Id,
                Codigo = Codigo,
                Versao = Versao,
                Natureza = natureza,
                Situacao = Opcoes.Situacao(SituacaoIndice),
                Nome = Nome,
                NomeSocial = NomeSocial,
                NomeExibicao = NomeExibicao,
                Apelido = Apelido,
                DocumentoPrincipal = natureza == NaturezaPessoa.Juridica ? null : Documento,
                DataNascimento = Datas.ParaEntidade(DataNascimento),
                GrupoEconomicoId = _grupoEconomicoId,
                MescladaEmId = _mescladaEmId,
                Observacoes = Observacoes,
                Estabelecimentos = estabelecimentos,
                Enderecos = Enderecos.Select(e => enderecos[e]).ToList(),
                MeiosContato = MeiosContato.Select(m => m.ParaEntidade()).ToList(),
                Contatos = Contatos.Select(c => c.ParaEntidade()).ToList(),
                Documentos = Documentos.Select(d => d.ParaEntidade()).ToList(),
                Papeis = Papeis.Select(p => p.ParaEntidade()).OfType<PessoaPapel>().ToList()
            };

            // Contas: a padrão entra se o papel existe (ativo ou não); as de outras empresas voltam intactas.
            if (PapelCliente.Ativo || PapelCliente.Existia || ContaCliente.Id > 0)
                pessoa.ContasCliente.Add(ContaCliente.ParaEntidade());
            pessoa.ContasCliente.AddRange(_outrasContasCliente);

            if (PapelFornecedor.Ativo || PapelFornecedor.Existia || ContaFornecedor.Id > 0)
                pessoa.ContasFornecedor.Add(ContaFornecedor.ParaEntidade());
            pessoa.ContasFornecedor.AddRange(_outrasContasFornecedor);

            return pessoa;
        }

        // ---- Listas ----

        public void AdicionarEndereco(EnderecoFormulario endereco)
        {
            endereco.PropertyChanged += Endereco_PropertyChanged;
            Enderecos.Add(endereco);
        }

        public void RemoverEndereco(EnderecoFormulario endereco)
        {
            endereco.PropertyChanged -= Endereco_PropertyChanged;
            Enderecos.Remove(endereco);
            foreach (var e in Estabelecimentos.Where(e => e.EnderecoFiscal == endereco))
                e.EnderecoFiscal = null;
        }

        /// <summary>Só um endereço principal: marcar um desmarca os outros.</summary>
        private void Endereco_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(EnderecoFormulario.Principal) || sender is not EnderecoFormulario marcado || marcado.Principal != true)
                return;
            foreach (var outro in Enderecos.Where(x => !ReferenceEquals(x, marcado)))
                outro.Principal = false;
        }

        public EstabelecimentoFormulario AdicionarEstabelecimento()
        {
            var estabelecimento = new EstabelecimentoFormulario(Enderecos) { EhJuridica = EhJuridica };
            Estabelecimentos.Add(estabelecimento);
            MarcarPrincipal();
            return estabelecimento;
        }

        public void RemoverEstabelecimento(EstabelecimentoFormulario estabelecimento)
        {
            if (Estabelecimentos.Count <= 1 || ReferenceEquals(estabelecimento, Principal)) return;
            Estabelecimentos.Remove(estabelecimento);
            MarcarPrincipal();
        }

        /// <summary>O principal é sempre o primeiro da lista.</summary>
        public void TornarPrincipal(EstabelecimentoFormulario estabelecimento)
        {
            var indice = Estabelecimentos.IndexOf(estabelecimento);
            if (indice <= 0) return;
            Estabelecimentos.Move(indice, 0);
            MarcarPrincipal();
            OnPropertyChanged(nameof(Principal));
        }

        private void MarcarPrincipal()
        {
            for (var i = 0; i < Estabelecimentos.Count; i++)
                Estabelecimentos[i].EhPrincipal = i == 0;
        }

        // ---- Consulta de CNPJ ----

        /// <summary>Preenche o estabelecimento (e, no principal, a razão social) com a consulta. O usuário confere e salva.</summary>
        public void AplicarCnpj(EstabelecimentoFormulario estabelecimento, DadosCnpj d)
        {
            estabelecimento.AplicarCnpj(d);

            EnderecoFormulario? endereco;
            if (ReferenceEquals(estabelecimento, Principal))
            {
                if (d.RazaoSocial.Length > 0) Nome = d.RazaoSocial;
                endereco = Enderecos.FirstOrDefault(e => e.Principal == true) ?? Enderecos.FirstOrDefault();
                if (endereco is null)
                {
                    endereco = new EnderecoFormulario { Principal = true, Fiscal = true };
                    AdicionarEndereco(endereco);
                }
            }
            else
            {
                // Filial: endereço próprio, usado como endereço fiscal dela.
                endereco = estabelecimento.EnderecoFiscal;
                if (endereco is null)
                {
                    endereco = new EnderecoFormulario { Descricao = "Filial " + Core.Validacao.Documento.Formatar(d.Cnpj), Fiscal = true };
                    AdicionarEndereco(endereco);
                    estabelecimento.EnderecoFiscal = endereco;
                }
            }
            endereco.AplicarCnpj(d);

            AdicionarMeioSeNovo(TipoContato.Telefone, d.Telefone);
            AdicionarMeioSeNovo(TipoContato.Email, d.Email);
        }

        private void AdicionarMeioSeNovo(TipoContato tipo, string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return;

            var jaExiste = MeiosContato.Any(m =>
                string.Equals(m.Valor.Trim(), valor.Trim(), StringComparison.OrdinalIgnoreCase) ||
                (tipo != TipoContato.Email &&
                 Core.Validacao.Documento.SomenteDigitos(m.Valor) == Core.Validacao.Documento.SomenteDigitos(valor)));

            if (!jaExiste)
                MeiosContato.Add(new MeioContatoFormulario
                {
                    TipoIndice = Opcoes.Indice(tipo),
                    Valor = valor,
                    Descricao = "Receita Federal"
                });
        }
    }
}
