using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lone.Aplicacao.Integracoes;
using Lone.Aplicacao.Pessoas;
using Lone.Aplicacao.Seguranca;
using Lone.Core.Enums;
using Lone.Core.ObjetosDeValor;
using Microsoft.UI.Xaml.Controls;

namespace Lone.ViewModels.Pessoas
{
    /// <summary>Tela de Pessoas: lista com filtros à esquerda, cadastro com seções à direita.</summary>
    public partial class PessoasViewModel : TelaViewModelBase
    {
        private readonly IPessoaAppService _pessoas;
        private readonly IAutenticacaoService _autenticacao;
        private readonly ICnpjConsulta _cnpj;
        private readonly ICepConsulta _cep;

        private CancellationTokenSource? _buscaCts;
        private bool _ignorarSelecao;

        public PessoasViewModel(IPessoaAppService pessoas, IAutenticacaoService autenticacao, ICnpjConsulta cnpj, ICepConsulta cep)
        {
            _pessoas = pessoas;
            _autenticacao = autenticacao;
            _cnpj = cnpj;
            _cep = cep;
        }

        public ObservableCollection<PessoaResumo> Itens { get; } = new();
        public ObservableCollection<HistoricoItem> Historico { get; } = new();

        // ---- Lista e filtros ----
        [ObservableProperty] private string _busca = string.Empty;
        [ObservableProperty] private TipoPapel? _papel;
        [ObservableProperty] private bool _mostrarInativos;
        [ObservableProperty] private PessoaResumo? _selecionado;
        [ObservableProperty] private bool _listaVazia;

        // ---- Cadastro ----
        [ObservableProperty] private PessoaFormulario _formulario = PessoaFormulario.Nova();

        public async Task CarregarAsync(int? selecionarId = null, CancellationToken ct = default)
        {
            try
            {
                var lista = await _pessoas.ListarAsync(new FiltroPessoas
                {
                    Texto = Busca,
                    Papel = Papel,
                    IncluirInativos = MostrarInativos
                }, ct);
                ct.ThrowIfCancellationRequested();

                Itens.Clear();
                foreach (var p in lista)
                    Itens.Add(p);
                ListaVazia = Itens.Count == 0;

                if (selecionarId is int id)
                    Selecionado = Itens.FirstOrDefault(p => p.Id == id);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Uma busca mais nova substituiu esta.
            }
            catch (Exception ex)
            {
                MostrarErro("carregar a lista", ex);
            }
        }

        partial void OnBuscaChanged(string value) => _ = BuscarComAtrasoAsync();
        partial void OnPapelChanged(TipoPapel? value) => _ = CarregarAsync();
        partial void OnMostrarInativosChanged(bool value) => _ = CarregarAsync();

        private async Task BuscarComAtrasoAsync()
        {
            _buscaCts?.Cancel();
            var cts = _buscaCts = new CancellationTokenSource();
            try
            {
                await Task.Delay(300, cts.Token);
                await CarregarAsync(ct: cts.Token);
            }
            catch (OperationCanceledException) { }
        }

        partial void OnSelecionadoChanged(PessoaResumo? value)
        {
            if (value is null || _ignorarSelecao) return;
            OcultarMensagem();
            _ = AbrirAsync(value.Id);
        }

        private async Task AbrirAsync(int id)
        {
            try
            {
                var pessoa = await _pessoas.ObterAsync(id);
                Formulario = pessoa is null ? PessoaFormulario.Nova() : PessoaFormulario.De(pessoa);
                Historico.Clear();
            }
            catch (Exception ex)
            {
                MostrarErro("abrir o cadastro", ex);
            }
        }

        [RelayCommand]
        private void Novo()
        {
            Selecionado = null;
            Formulario = PessoaFormulario.Nova();
            Historico.Clear();
            OcultarMensagem();
        }

        [RelayCommand]
        private async Task DescartarAsync()
        {
            OcultarMensagem();
            if (Formulario.Id == 0)
                Novo();
            else
                await AbrirAsync(Formulario.Id);
        }

        [RelayCommand]
        private async Task SalvarAsync()
        {
            Ocupado = true;
            OcultarMensagem();
            try
            {
                var eraEmpresaDoGrupo = Formulario.Papeis.Any(p => p.Papel == TipoPapel.EmpresaDoGrupo && p.Existia);
                var resultado = await _pessoas.SalvarAsync(Formulario.ParaEntidade());
                var id = resultado.Pessoa.Id;

                // Empresas do grupo mudaram: a lista de empresas da sessão é recarregada.
                if (eraEmpresaDoGrupo || resultado.Pessoa.TemPapel(TipoPapel.EmpresaDoGrupo))
                    await _autenticacao.AtualizarEmpresasAsync();

                // Recarrega a lista sem disparar a abertura, e depois abre uma vez só.
                _ignorarSelecao = true;
                try { await CarregarAsync(id); }
                finally { _ignorarSelecao = false; }
                await AbrirAsync(id);

                if (resultado.Avisos.Count > 0)
                    Mostrar("Salvo. " + string.Join("\n", resultado.Avisos), InfoBarSeverity.Warning);
                else
                    Mostrar("Cadastro salvo.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                MostrarErro("salvar", ex);
            }
            finally
            {
                Ocupado = false;
            }
        }

        // ---- Consultas externas ----

        [RelayCommand]
        private Task ConsultarCnpjPrincipalAsync() => ConsultarCnpjAsync(Formulario.Principal);

        public async Task ConsultarCnpjAsync(EstabelecimentoFormulario estabelecimento)
        {
            if (!Cnpj.EhValido(estabelecimento.Cnpj))
            {
                Mostrar("Digite um CNPJ válido para consultar.", InfoBarSeverity.Warning);
                return;
            }

            Ocupado = true;
            OcultarMensagem();
            try
            {
                var dados = await _cnpj.ConsultarAsync(estabelecimento.Cnpj);
                if (dados is null)
                {
                    Mostrar("CNPJ não encontrado.", InfoBarSeverity.Warning);
                    return;
                }

                Formulario.AplicarCnpj(estabelecimento, dados);
                Mostrar($"Dados preenchidos pela {dados.Fonte}. Confira e salve.", InfoBarSeverity.Informational);
            }
            catch (Exception ex)
            {
                Mostrar(ex.Message, InfoBarSeverity.Error);
            }
            finally
            {
                Ocupado = false;
            }
        }

        public async Task BuscarCepAsync(EnderecoFormulario endereco)
        {
            Ocupado = true;
            OcultarMensagem();
            try
            {
                var dados = await _cep.ConsultarAsync(endereco.Cep);
                if (dados is null)
                    Mostrar("CEP não encontrado.", InfoBarSeverity.Warning);
                else
                    endereco.AplicarCep(dados);
            }
            catch (Exception ex)
            {
                Mostrar(ex.Message, InfoBarSeverity.Error);
            }
            finally
            {
                Ocupado = false;
            }
        }

        // ---- Listas do cadastro ----

        [RelayCommand]
        private void AdicionarEstabelecimento() => Formulario.AdicionarEstabelecimento();
        public void RemoverEstabelecimento(EstabelecimentoFormulario e) => Formulario.RemoverEstabelecimento(e);
        public void TornarPrincipal(EstabelecimentoFormulario e) => Formulario.TornarPrincipal(e);

        [RelayCommand]
        private void AdicionarEndereco() =>
            Formulario.AdicionarEndereco(new EnderecoFormulario { Principal = Formulario.Enderecos.Count == 0 });
        public void RemoverEndereco(EnderecoFormulario e) => Formulario.RemoverEndereco(e);

        [RelayCommand]
        private void AdicionarMeio() =>
            Formulario.MeiosContato.Add(new MeioContatoFormulario { Principal = Formulario.MeiosContato.Count == 0 });
        public void RemoverMeio(MeioContatoFormulario m) => Formulario.MeiosContato.Remove(m);

        [RelayCommand]
        private void AdicionarContatoPessoa() =>
            Formulario.Contatos.Add(new ContatoPessoaFormulario { Principal = Formulario.Contatos.Count == 0 });
        public void RemoverContatoPessoa(ContatoPessoaFormulario c) => Formulario.Contatos.Remove(c);

        [RelayCommand]
        private void AdicionarDocumento() => Formulario.Documentos.Add(new DocumentoFormulario());
        public void RemoverDocumento(DocumentoFormulario d) => Formulario.Documentos.Remove(d);

        public async Task CarregarHistoricoAsync()
        {
            Historico.Clear();
            if (Formulario.Id == 0) return;

            try
            {
                var registros = await _pessoas.ListarHistoricoAsync(Formulario.Id);
                foreach (var r in registros)
                    Historico.Add(HistoricoItem.De(r));
            }
            catch (Exception ex)
            {
                MostrarErro("carregar o histórico", ex);
            }
        }
    }
}
