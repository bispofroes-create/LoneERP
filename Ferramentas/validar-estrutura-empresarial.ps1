# Validação da Estrutura Empresarial (fase de fechamento), no Windows, na raiz do repositório.
# Uso:
#   1. No PMC (projeto padrão Lone.Infrastructure):
#        Add-Migration EstruturaEmpresarial -Project Lone.Infrastructure -StartupProject Lone.Api -OutputDir Persistencia/Migracoes
#   2. No próprio PMC (sem abrir outro powershell, para os acentos saírem certos):
#        & .\Ferramentas\validar-estrutura-empresarial.ps1
#      Opcional: -SqlServer "Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True"
#      (liga os testes de banco: migração real num banco TEMPORÁRIO Lone_Teste_..., nunca no banco do sistema)
# Não aplica a migração em nenhum banco e não faz commit.

param([string]$SqlServer = '')

$ErrorActionPreference = 'Stop'
# dotnet escreve falhas de teste no stderr; com 'Stop' o PowerShell aborta antes do resumo. Quem decide é o código de saída.
$PSNativeCommandUseErrorActionPreference = $false
Set-Location (Split-Path $PSScriptRoot -Parent)

Write-Host "`n== 1. Auditoria da migração (e SQL de dados no fim do Up) ==" -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'auditar-migracao-estrutura-empresarial.ps1')
if ($LASTEXITCODE -ne 0) { Write-Host "Auditoria da migração reprovada: pare aqui." -ForegroundColor Red; exit 1 }

Write-Host "`n== 2. Compilação (API, domínio, cliente e testes) ==" -ForegroundColor Cyan
$ErrorActionPreference = 'Continue'
dotnet build tests/Lone.Tests/Lone.Tests.csproj -c Debug
if ($LASTEXITCODE -ne 0) { Write-Host "Compilação falhou." -ForegroundColor Red; exit 1 }

Write-Host "`n== 3. Todos os testes ==" -ForegroundColor Cyan
if ($SqlServer) { $env:LONE_TESTES_SQLSERVER = $SqlServer } else { Remove-Item Env:LONE_TESTES_SQLSERVER -ErrorAction SilentlyContinue }
$saida = Join-Path $env:TEMP 'lone-testes'
dotnet test tests/Lone.Tests/Lone.Tests.csproj --no-build --logger "trx;LogFileName=estrutura-empresarial.trx" --results-directory $saida
$codigo = $LASTEXITCODE

$trx = Join-Path $saida 'estrutura-empresarial.trx'
if (Test-Path $trx) {
    [xml]$x = Get-Content $trx
    $c = $x.TestRun.ResultSummary.Counters
    Write-Host ("`nTotal: {0}  Passed: {1}  Failed: {2}  Skipped: {3}" -f $c.total, $c.passed, $c.failed, $c.notExecuted)
    $x.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'Failed' } | ForEach-Object {
        Write-Host ("  FALHOU: " + $_.testName) -ForegroundColor Red
    }
    if (-not $SqlServer) { Write-Host "(Testes de SQL Server pulados: rode de novo com -SqlServer para a migração real.)" -ForegroundColor Yellow }
}
if ($codigo -ne 0) { Write-Host "Há testes falhando: a fase não está concluída." -ForegroundColor Red; exit 1 }

Write-Host "`n== 4. Roteiro manual (Windows e Android) ==" -ForegroundColor Cyan
@'
Windows (depois de Update-Database num banco de TESTE, nunca no de produção):
  [ ] Pessoas abre; lista mostra o nome fantasia das PJ (exibição > fantasia > razão social)
  [ ] PF abre: cabeçalho com nome, tipo e papéis, CPF, código e situação
  [ ] PJ abre: cabeçalho com nome fantasia, razão social, CNPJ, grupo empresarial e estabelecimentos
  [ ] Aba Comercial: Cliente e Fornecedor na mesma aba; tirar um papel some só o bloco dele
  [ ] Fornecedor: condição de pagamento do cadastro; texto antigo aparece como "não convertida" quando não ligou
  [ ] Empresa e estabelecimentos: escolher/tirar grupo empresarial (com e sem a permissão de estrutura empresarial)
  [ ] Filial gravada: "Desativar filial" e "Reativar"; salvar e reabrir: continua na lista
  [ ] PJ gravada trocada para Pessoa física: salvar é recusado com a lista do que seria perdido
  [ ] Aba Relacionamentos: buscar pessoa, "Sócio de" e "Tem como sócio", encerrar, desativar, "Mostrar encerrados"
  [ ] Menu Grupos empresariais: criar, renomear, desativar, reativar, ver as empresas do grupo
  [ ] Histórico: grupo, estabelecimentos e relacionamentos aparecem com nomes (não Ids)
Android: abrir o cadastro, navegar nas abas (faixa rolável), listas, formulário, grupo, relacionamentos, estabelecimentos.
'@ | Write-Host
