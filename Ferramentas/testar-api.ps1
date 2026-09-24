# Testa a Lone.Api de ponta a ponta (o mesmo roteiro do Lone.Api.http), sem precisar do editor HTTP.
# Uso (com a API rodando no Visual Studio):
#   powershell -ExecutionPolicy Bypass -File .\Ferramentas\testar-api.ps1
# Parâmetros opcionais: -Base http://localhost:5080 -Login admin -Senha Lone2026erp

param(
    [string]$Base = 'http://localhost:5080',
    [string]$Login = 'admin',
    [string]$Senha = 'Lone2026erp'
)

$ErrorActionPreference = 'Stop'
$v1 = "$Base/api/v1"
$script:ok = 0
$script:falhas = 0

# Chama a API e devolve Status (número) e Corpo (objeto), inclusive nas respostas de erro.
function Chamar {
    param([string]$Metodo, [string]$Url, $Corpo = $null, [string]$Token = $null)

    $parametros = @{ Method = $Metodo; Uri = $Url; UseBasicParsing = $true; Headers = @{} }
    if ($Token) { $parametros.Headers['Authorization'] = "Bearer $Token" }
    if ($null -ne $Corpo) {
        $json = $Corpo | ConvertTo-Json -Depth 10
        $parametros.Body = [System.Text.Encoding]::UTF8.GetBytes($json)
        $parametros.ContentType = 'application/json; charset=utf-8'
    }

    try {
        $resposta = Invoke-WebRequest @parametros
        $objeto = $null
        if ($resposta.Content) { $objeto = $resposta.Content | ConvertFrom-Json }
        return [pscustomobject]@{ Status = [int]$resposta.StatusCode; Corpo = $objeto }
    }
    catch {
        $http = $_.Exception.Response
        if ($null -eq $http) { throw "Sem resposta de $Url. A API está rodando? ($($_.Exception.Message))" }
        $texto = $_.ErrorDetails.Message
        if (-not $texto) {
            $leitor = New-Object System.IO.StreamReader($http.GetResponseStream())
            $texto = $leitor.ReadToEnd()
        }
        $objeto = $null
        if ($texto) { try { $objeto = $texto | ConvertFrom-Json } catch { $objeto = $texto } }
        return [pscustomobject]@{ Status = [int]$http.StatusCode; Corpo = $objeto }
    }
}

function Conferir {
    param([string]$Passo, [bool]$Certo, [string]$Detalhe)
    if ($Certo) {
        $script:ok++
        Write-Host ("[OK]      {0}  {1}" -f $Passo, $Detalhe) -ForegroundColor Green
    } else {
        $script:falhas++
        Write-Host ("[FALHOU]  {0}  {1}" -f $Passo, $Detalhe) -ForegroundColor Red
    }
}

function Mensagem($resposta) {
    if ($resposta.Corpo -and $resposta.Corpo.detail) { return "$($resposta.Status) - $($resposta.Corpo.detail)" }
    return "$($resposta.Status)"
}

function Entrar {
    $r = Chamar POST "$v1/autenticacao/entrar" @{ login = $Login; senha = $Senha; dispositivo = 'Script de teste' }
    if ($r.Status -ne 200) { throw "Não consegui entrar com '$Login': $(Mensagem $r)" }
    return $r.Corpo
}

Write-Host "Testando $Base" -ForegroundColor Cyan

# 1. Saúde
$r = Chamar GET "$Base/saude"
Conferir '1. Saúde da API e do banco' ($r.Status -eq 200) (Mensagem $r)

# 2 e 3. Primeiro acesso (só na primeira vez) ou login
$r = Chamar GET "$v1/autenticacao/situacao"
Conferir '2. Situação do sistema' ($r.Status -eq 200) ("existeUsuario = {0}" -f $r.Corpo.existeUsuario)
if (-not $r.Corpo.existeUsuario) {
    $r = Chamar POST "$v1/autenticacao/primeiro-acesso" @{ nome = 'Administrador'; login = $Login; senha = $Senha; dispositivo = 'Script de teste' }
    Conferir '3. Primeiro acesso cria o administrador' ($r.Status -eq 200 -and $r.Corpo.administrador) (Mensagem $r)
}

# 4. Senha errada
$r = Chamar POST "$v1/autenticacao/entrar" @{ login = $Login; senha = 'errada123' }
Conferir '4. Senha errada é recusada' ($r.Status -eq 401 -and $r.Corpo.codigo -eq 'login_recusado') (Mensagem $r)

# 5. Login correto
$sessao = Entrar
Conferir '5. Login correto' ([bool]$sessao.tokenAcesso) ("usuário {0}, administrador = {1}" -f $sessao.nome, $sessao.administrador)

# 6. Sem token
$r = Chamar GET "$v1/pessoas"
Conferir '6. Sem token não entra' ($r.Status -eq 401 -and $r.Corpo.codigo -eq 'nao_autenticado') (Mensagem $r)

# 7. Empresa do grupo (cadastra na primeira vez; nas próximas, só confere)
$empresaId = '0f7a4c1e-0000-0000-0000-0197a1b2c3d4'
$r = Chamar GET "$v1/pessoas/$empresaId" -Token $sessao.tokenAcesso
if ($r.Status -eq 404) {
    $empresa = @{
        natureza = 'Juridica'
        nome = 'Minha Empresa Ltda'
        nomeExibicao = 'Minha Empresa'
        estabelecimentos = @(@{ cnpj = '11.222.333/0001-81'; principal = $true; indicadorIE = 'NaoContribuinte' })
        enderecos = @(@{ finalidades = 'Principal, Fiscal'; cep = '01310-100'; logradouro = 'Avenida Paulista'; numero = '1000'; bairro = 'Bela Vista'; cidade = 'São Paulo'; uf = 'SP' })
        papeis = @(@{ papel = 'EmpresaDoGrupo' })
    }
    $r = Chamar PUT "$v1/pessoas/$empresaId" $empresa $sessao.tokenAcesso
    Conferir '7. Cadastro da empresa do grupo' ($r.Status -eq 200) ("{0} - código {1}" -f (Mensagem $r), $r.Corpo.pessoa.codigo)
} else {
    Conferir '7. Empresa do grupo já cadastrada' ($r.Status -eq 200) ("código {0}" -f $r.Corpo.codigo)
}

# 8. Renovar: a empresa passa a ser a ativa
$r = Chamar POST "$v1/autenticacao/renovar" @{ tokenRenovacao = $sessao.tokenRenovacao }
Conferir '8. Renovação da sessão' ($r.Status -eq 200 -and $null -ne $r.Corpo.empresaAtiva) ("empresa ativa: {0}" -f $r.Corpo.empresaAtiva.nome)
$renovada = $r.Corpo

# 9. Reusar o token antigo: tudo é revogado
$r = Chamar POST "$v1/autenticacao/renovar" @{ tokenRenovacao = $sessao.tokenRenovacao }
Conferir '9. Token antigo reusado é recusado' ($r.Status -eq 401) (Mensagem $r)
$r = Chamar POST "$v1/autenticacao/renovar" @{ tokenRenovacao = $renovada.tokenRenovacao }
Conferir '9. ...e a sessão nova também caiu (proteção contra roubo)' ($r.Status -eq 401) (Mensagem $r)

# 10. Novo login e consultas
$sessao = Entrar
$token = $sessao.tokenAcesso
$r = Chamar GET "$v1/pessoas?texto=empresa" -Token $token
Conferir '10. Lista de pessoas' ($r.Status -eq 200 -and @($r.Corpo).Count -ge 1) ("{0} encontrada(s)" -f @($r.Corpo).Count)
$r = Chamar GET "$v1/pessoas/$empresaId" -Token $token
Conferir '11. Abrir o cadastro' ($r.Status -eq 200) ("{0}, {1} estabelecimento(s)" -f $r.Corpo.nome, @($r.Corpo.estabelecimentos).Count)
$r = Chamar GET "$v1/pessoas/$empresaId/historico" -Token $token
Conferir '12. Histórico (auditoria)' ($r.Status -eq 200 -and @($r.Corpo).Count -ge 1) ("{0} registro(s)" -f @($r.Corpo).Count)

# 13. Regra de negócio
$r = Chamar PUT "$v1/pessoas/$([guid]::NewGuid())" @{ natureza = 'Fisica'; nome = 'Fulano'; documentoPrincipal = '111.111.111-12' } $token
Conferir '13. CPF inválido é recusado com a lista de erros' ($r.Status -eq 400 -and @($r.Corpo.erros).Count -ge 1) ("{0}: {1}" -f $r.Status, ($r.Corpo.erros -join ' | '))

# 14. Consultas externas (precisam de internet)
$r = Chamar GET "$v1/consultas/cep/01310100" -Token $token
Conferir '14. Consulta de CEP' ($r.Status -in 200, 502) ("{0} {1}" -f (Mensagem $r), $r.Corpo.logradouro)
$r = Chamar GET "$v1/consultas/cnpj/11222333000181" -Token $token
Conferir '14. Consulta de CNPJ' ($r.Status -in 200, 404, 502) (Mensagem $r)

# 15. Segurança
foreach ($caminho in 'usuarios', 'usuarios/perfis-disponiveis', 'perfis', 'perfis/permissoes', 'empresas') {
    $r = Chamar GET "$v1/$caminho" -Token $token
    Conferir "15. GET $caminho" ($r.Status -eq 200) ("{0} item(ns)" -f @($r.Corpo).Count)
}

# 16. Sair
$r = Chamar POST "$v1/autenticacao/sair" @{ tokenRenovacao = $sessao.tokenRenovacao }
Conferir '16. Sair' ($r.Status -eq 204) (Mensagem $r)
$r = Chamar POST "$v1/autenticacao/renovar" @{ tokenRenovacao = $sessao.tokenRenovacao }
Conferir '16. ...depois de sair, o token não renova mais' ($r.Status -eq 401) (Mensagem $r)

Write-Host ''
$cor = if ($script:falhas -eq 0) { 'Green' } else { 'Red' }
Write-Host ("Resultado: {0} ok, {1} com falha." -f $script:ok, $script:falhas) -ForegroundColor $cor
