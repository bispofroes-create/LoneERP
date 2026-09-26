# Migração "EstruturaEmpresarial": AUDITA o arquivo gerado pelo EF e coloca o SQL de dados no fim do Up.
# Uso (depois do Add-Migration), no PMC ou num PowerShell na raiz do repositório:
#   & .\Ferramentas\auditar-migracao-estrutura-empresarial.ps1
# 1. Encontra Persistencia/Migracoes/*_EstruturaEmpresarial.cs (exatamente um).
# 2. Confere as operações esperadas no Up (tabela, colunas, CHECK, índices, FKs, tipos novos).
# 3. Procura operações destrutivas no Up (DROP, DELETE, TRUNCATE, renomear, alterar coluna, apagar dados).
# 4. Só se tudo conferir, insere "migrationBuilder.Sql(SqlMigracaoEstruturaEmpresarial.Dados);" no fim do Up
#    (rodar de novo não duplica). Qualquer problema: mostra e NÃO grava nada (código de saída 1).
# Compatível com o Windows PowerShell 5.1 (o do Package Manager Console). Não precisa de Python.

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$pasta = Join-Path $raiz 'src\Lone.Infrastructure\Persistencia\Migracoes'
$linhaSql = 'migrationBuilder.Sql(SqlMigracaoEstruturaEmpresarial.Dados);'
$opcoes = [System.Text.RegularExpressions.RegexOptions]::Singleline

$esperadas = [ordered]@{
    'CreateTable GruposEmpresariais'              = 'CreateTable\(\s*name:\s*"GruposEmpresariais"'
    'Pessoas.GrupoEmpresarialId'                  = 'AddColumn<Guid>\(\s*name:\s*"GrupoEmpresarialId",\s*table:\s*"Pessoas"[^;]*nullable:\s*true'
    'Índice Pessoas.GrupoEmpresarialId'           = 'CreateIndex\(\s*name:\s*"IX_Pessoas_GrupoEmpresarialId"'
    'FK Pessoas -> GruposEmpresariais'            = 'AddForeignKey\(\s*name:\s*"FK_Pessoas_GruposEmpresariais_GrupoEmpresarialId"'
    'CHECK só pessoa jurídica'                    = 'AddCheckConstraint\(\s*name:\s*"CK_Pessoas_GrupoEmpresarialSoPJ",\s*table:\s*"Pessoas",\s*sql:\s*"\[GrupoEmpresarialId\] IS NULL OR \[Natureza\] = 1"'
    'PessoaRelacionamentos.Ativo'                 = 'AddColumn<bool>\(\s*name:\s*"Ativo",\s*table:\s*"PessoaRelacionamentos"'
    'Índice único do vínculo em aberto'           = 'CreateIndex\(\s*name:\s*"IX_PessoaRelacionamentos_Aberto"[^;]*unique:\s*true[^;]*filter:\s*"\[Ativo\] = 1 AND \[FimEm\] IS NULL"'
    'ContasFornecedor.CondicaoPagamentoId'        = 'AddColumn<Guid>\(\s*name:\s*"CondicaoPagamentoId",\s*table:\s*"ContasFornecedor"[^;]*nullable:\s*true'
    'FK ContasFornecedor -> CondicoesPagamento'   = 'AddForeignKey\(\s*name:\s*"FK_ContasFornecedor_CondicoesPagamento_CondicaoPagamentoId"'
    'Tipos novos (Administrador de, Parceiro de)' = 'InsertData\(\s*table:\s*"TiposRelacionamento"'
}
$destrutivas = [ordered]@{
    'DropTable' = '\bDropTable\('; 'DropColumn' = '\bDropColumn\('; 'DropIndex' = '\bDropIndex\('
    'DropForeignKey' = '\bDropForeignKey\('; 'DropCheckConstraint' = '\bDropCheckConstraint\('
    'DropPrimaryKey' = '\bDropPrimaryKey\('; 'RenameTable' = '\bRenameTable\('; 'RenameColumn' = '\bRenameColumn\('
    'RenameIndex' = '\bRenameIndex\('; 'AlterColumn' = '\bAlterColumn'; 'DeleteData' = '\bDeleteData\('
    'UpdateData' = '\bUpdateData\('; 'DropSequence' = '\bDropSequence\('
    'SQL com DELETE/TRUNCATE/DROP' = 'Sql\(\s*@?"[^"]*\b(DELETE|TRUNCATE|DROP)\b'
}

$achados = @(Get-ChildItem -Path $pasta -Filter '*_EstruturaEmpresarial.cs' | Where-Object { $_.Name -notlike '*.Designer.cs' })
if ($achados.Count -ne 1) {
    Write-Host "Esperava exatamente uma migração *_EstruturaEmpresarial.cs em $pasta (achei $($achados.Count)). Gere com Add-Migration." -ForegroundColor Red
    exit 1
}
$caminho = $achados[0].FullName
$bruto = [System.IO.File]::ReadAllBytes($caminho)
$bom = $bruto.Length -ge 3 -and $bruto[0] -eq 0xEF -and $bruto[1] -eq 0xBB -and $bruto[2] -eq 0xBF
$inicio = 0; if ($bom) { $inicio = 3 }
$original = [System.Text.Encoding]::UTF8.GetString($bruto, $inicio, $bruto.Length - $inicio)
$crlf = $original.Contains("`r`n")
$texto = $original.Replace("`r`n", "`n")

$upIni = $texto.IndexOf('protected override void Up(')
$upFim = $texto.IndexOf('protected override void Down(')
if ($upIni -lt 0 -or $upFim -lt $upIni) { Write-Host "Não achei os métodos Up e Down em $caminho." -ForegroundColor Red; exit 1 }
$up = $texto.Substring($upIni, $upFim - $upIni)
$linhasAntes = ($texto.Substring(0, $upIni) -split "`n").Count - 1

Write-Host "Migração: $caminho"
$problemas = New-Object System.Collections.Generic.List[string]
foreach ($nome in $esperadas.Keys) {
    $ok = [regex]::IsMatch($up, $esperadas[$nome], $opcoes)
    if ($ok) { Write-Host "  [ok] $nome" } else { Write-Host "  [FALTA] $nome" -ForegroundColor Red; $problemas.Add("operação esperada não encontrada: $nome") }
}
if (-not $up.Contains('5a0e6f10-0000-0000-0000-000000000007') -or -not $up.Contains('5a0e6f10-0000-0000-0000-000000000008')) {
    $problemas.Add('os Ids fixos de Administrador de (...07) e Parceiro de (...08) não estão no InsertData')
}
foreach ($nome in $destrutivas.Keys) {
    foreach ($m in [regex]::Matches($up, $destrutivas[$nome], $opcoes)) {
        $linha = ($up.Substring(0, $m.Index) -split "`n").Count + $linhasAntes
        $problemas.Add("operação destrutiva no Up: $nome (linha $linha)")
    }
}
# A migração não pode mexer em GrupoEconomico (reservado) nem em endereços (arquitetura validada).
foreach ($protegido in 'GruposEconomicos', 'GrupoEconomicoId', 'PessoaEnderecos', 'PessoaEnderecoFinalidades', 'FinalidadesEndereco') {
    if ($up.Contains($protegido)) { $problemas.Add("o Up menciona ${protegido}: esta migração não deveria tocar nisso") }
}

if ($problemas.Count -gt 0) {
    Write-Host "`nPROBLEMAS (nada foi gravado):" -ForegroundColor Red
    foreach ($p in $problemas) { Write-Host "  - $p" -ForegroundColor Red }
    exit 1
}
if ($up.Contains($linhaSql)) { Write-Host "`nO SQL de dados já está no fim do Up. Nada a fazer." -ForegroundColor Green; exit 0 }

$fecha = $up.TrimEnd().LastIndexOf('}')             # chave que fecha o Up
$fimUp = $up.LastIndexOf("`n", $fecha) + 1          # começo da linha dessa chave
$up = $up.Substring(0, $fimUp) + "`n            " + $linhaSql + "`n" + $up.Substring($fimUp)
$texto = $texto.Substring(0, $upIni) + $up + $texto.Substring($upFim)
if ($crlf) { $texto = $texto.Replace("`n", "`r`n") }
[System.IO.File]::WriteAllText($caminho, $texto, (New-Object System.Text.UTF8Encoding($bom)))
Write-Host "`nTudo conferido. SQL de dados inserido no fim do Up." -ForegroundColor Green
exit 0
