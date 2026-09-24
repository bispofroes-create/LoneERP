# Remove arquivos que ficaram obsoletos nas reorganizações do Lone.
# Uso (na pasta do projeto):  powershell -ExecutionPolicy Bypass -File .\Ferramentas\limpar-obsoletos.ps1
# Pode rodar mais de uma vez: arquivos que já não existem são ignorados.

$raiz = Split-Path -Parent $PSScriptRoot

$obsoletos = @(
    # Primeira versão (Clientes)
    'Views\ClientesPage.xaml',
    'Views\ClientesPage.xaml.cs',
    'ViewModels\ClientesViewModel.cs',
    'ViewModels\ClienteFormulario.cs',
    'Lone.Core\Entidades\TipoPessoa.cs',
    'Lone.Core\Servicos\IClienteService.cs',
    'Lone.Data\Servicos\ClienteService.cs',
    # Etapa 1: movidos para Lone.Aplicacao / renomeados
    'Lone.Core\Servicos\IPessoaService.cs',
    'Lone.Core\Servicos\IAuditoriaService.cs',
    'Lone.Core\Servicos\IUsuarioAtual.cs',
    'Lone.Core\Servicos\ICnpjConsulta.cs',
    'Lone.Core\Servicos\ICepConsulta.cs',
    'Lone.Core\Servicos\IBancoDeDados.cs',
    'Lone.Core\Modelos\FiltroPessoas.cs',
    'Lone.Core\Modelos\PessoaResumo.cs',
    'Lone.Core\Modelos\ResultadoSalvar.cs',
    'Lone.Core\Modelos\DadosCnpj.cs',
    'Lone.Core\Modelos\DadosCep.cs',
    'Lone.Data\Servicos\PessoaService.cs',
    'Lone.Data\Servicos\AuditoriaService.cs',
    'Lone.Data\Servicos\UsuarioWindows.cs',
    # Etapa 2: substituídos pela sessão de usuário
    'Lone.Aplicacao\Seguranca\AutorizacaoProvisoria.cs',
    'Lone.Aplicacao\Seguranca\UsuarioProvisorioWindows.cs',
    # Etapa 3: modelo Pessoa v2 (papéis, estabelecimentos, contatos)
    'Lone.Core\Enums\TipoEndereco.cs',
    'Lone.Core\Enums\PapelFiltro.cs',
    'Lone.Core\Entidades\DadosFiscais.cs',
    'Lone.Core\Entidades\PessoaContato.cs',
    'Lone.Core\Entidades\PapelBase.cs',
    'Lone.Core\Entidades\Cliente.cs',
    'Lone.Core\Entidades\Fornecedor.cs',
    'Lone.Data\Configuracoes\ClienteConfiguration.cs',
    'Lone.Data\Configuracoes\FornecedorConfiguration.cs',
    'Lone.Data\Configuracoes\PessoaContatoConfiguration.cs',
    'ViewModels\Pessoas\ContatoFormulario.cs',
    'ViewModels\Pessoas\ClienteFormulario.cs',
    'ViewModels\Pessoas\FornecedorFormulario.cs',
    'ViewModels\Pessoas\DadosFiscaisFormulario.cs'
)

$removidos = 0
foreach ($relativo in $obsoletos) {
    $caminho = Join-Path $raiz $relativo
    if (Test-Path -LiteralPath $caminho) {
        Remove-Item -LiteralPath $caminho
        Write-Host "Removido: $relativo"
        $removidos++
    }
}

# Pastas que ficaram vazias.
foreach ($pasta in @('Lone.Core\Servicos', 'Lone.Core\Modelos')) {
    $caminho = Join-Path $raiz $pasta
    if ((Test-Path -LiteralPath $caminho) -and -not (Get-ChildItem -LiteralPath $caminho -Force)) {
        Remove-Item -LiteralPath $caminho
        Write-Host "Pasta vazia removida: $pasta"
    }
}

Write-Host "Concluído. $removidos arquivo(s) removido(s)."
