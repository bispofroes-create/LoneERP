# Lone ERP — passagem para outra conta (26/09/2026, noite)

> **Para o assistente da conta nova:** leia este arquivo inteiro antes de qualquer coisa. Depois, leia na ordem:
> `docs/CONTINUIDADE.md` (visão geral, decisões e padrões), `docs/UX-ARQUITETURA.md` (redesenho e design system),
> `docs/FASE3-PRIVACIDADE.md` (LGPD) e `docs/REVISAO-PESSOAS.md` (plano de revisão da ficha). Responda sempre em **português**.

---

## 1. Estado do repositório

- Pasta no PC do usuário: `C:\Users\Windows 11\source\repos\Lone` (solução `Lone.slnx`, branch `main`, git só local, sem remoto).
- **Último commit: `49b9f96`** — Redesign do módulo Pessoas. Working tree limpo, exceto:
  - `_entrega/` (não versionada: pacotes .zip, auditorias e planos antigos);
  - este arquivo e a atualização de status em `docs/CONTINUIDADE.md` (feitos depois do commit, **ainda sem commit**).
- **Compilação:** `dotnet build` → 0 avisos, 0 erros (SDK 10.0.401, fixado em `global.json`).
- **Testes:** `dotnet test` → **1181 aprovados, 0 falhas, 22 ignorados** (os 22 são `[FatoSqlServer]`, que só rodam com a
  variável `LONE_TESTES_SQLSERVER`, ex.: `Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True`).
- **Não testado ainda:** o redesenho rodando no app (Windows e Android). Primeira coisa a pedir ao usuário: abrir o app e
  conferir menu, lista, filtros, paginação, ficha e Configurações de cada módulo (roteiro na seção 5).

### Histórico recente (mais novo primeiro)
| Commit | O quê |
|---|---|
| `49b9f96` | Redesign do módulo Pessoas (menu por módulo, lista paginada, ficha em tela cheia, configurações por módulo, design system) |
| `097ed98` | Fase 3 — Privacidade e LGPD (consentimento por finalidade em períodos, regra central de comunicação) |
| `872dda0` | Consolidação de Pessoas, fase 5: página Configurações e menu só com a operação |
| `303264b` | Consolidação, fase 2: aba Fiscal; condição antiga do cliente só leitura |
| `f54bebb` | Consolidação, fase 1: Identificação, Documentos para PJ, contatos em duas listas |
| `0bc3b0d` | Estrutura empresarial e relacionamentos |
| `0976726` … `163cb43` | Endereço × finalidade (implementação, auditorias, migration, `global.json`) |

(Fase 4 da consolidação — Situação, Informações adicionais, Histórico — foi conferida e não precisou de mudança.)

### Migrations (todas aplicadas no banco de desenvolvimento `LoneERP`)
`Inicial` → `DadosComplementaresPessoa` → `MunicipiosECamposPersonalizados` → `CadastroEtiquetas` → `CadastroProfissoes` →
`CadastroPapeis` → `MeiosContatoETipos` → `CadastroGeral` → `FinalidadesEndereco` → `EstruturaEmpresarial` →
`20260926230535_PrivacidadeConsentimentos` (última). O redesenho **não** tem migration.

---

## 2. Regras de trabalho combinadas com o usuário (seguir sempre)

- **Idioma:** respostas, código, comentários e mensagens em português (nomes de projetos `Lone.*` em inglês).
- **Commit/push:** **nunca** fazer commit sem autorização explícita do usuário ("pode fazer o commit"). Nunca fazer push.
  Mensagens de commit em português.
- **Banco:** nunca aplicar migration em produção; nada de `DROP`/`DELETE` em massa; manter colunas antigas e migrar dados com
  conferência (THROW se não bater); **nunca excluir fisicamente** (desativar/encerrar). Sem SQL dinâmico inseguro.
- **Regra de negócio:** não assumir regra jurídica ou de negócio que não esteja definida. Havendo dúvida de negócio,
  **parar antes de alterar o código** e perguntar. Não mexer em modelo de dados sem relatar antes.
- **Escopo:** passo a passo; não avançar de etapa sem o usuário testar a atual; não mexer fora do escopo sem avisar; não
  corrigir com base em hipótese (reproduzir primeiro). Apresentar análise/alternativas antes de mudanças grandes.
- **Estilo:** classes pequenas, uma responsabilidade; nada de lógica em code-behind (só cola de interface); `///` em
  português explicando o porquê; bindings compilados (`x:DataType`) no XAML.
- **Entrega:** ao final, relatório curto das alterações e dos testes (o usuário compila e testa).

## 3. Ambiente e fluxo do usuário

- Windows 11, Visual Studio 2026, .NET 10 (SDK 10.0.401), SQL Server Express `.\SQLEXPRESS`, banco `LoneERP`.
- **O PC do usuário não tem Python.** Ferramentas `.py` em `Ferramentas/` precisam ser rodadas do lado do assistente
  (ou substituídas por PowerShell).
- O usuário roda tudo no **Console do Gerenciador de Pacotes (PMC)**: `dotnet build`, `dotnet test`, `Add-Migration`,
  `Update-Database`. Nunca pedir para colar SQL no PMC (usar SSMS ou `sqlcmd -S .\SQLEXPRESS -E -C -i arquivo.sql`).
- **Migrations:** o usuário gera com
  `Add-Migration <Nome> -Project Lone.Infrastructure -StartupProject Lone.Api -OutputDir Persistencia/Migracoes`.
  Quando houver SQL de dados (`SqlMigracao*.cs`), a linha `migrationBuilder.Sql(...)` precisa ser inserida no ponto certo
  **antes** do `Update-Database` — peça ao usuário para avisar logo depois do `Add-Migration`.
  A API aplica migrations pendentes ao iniciar (`Banco:AplicarMigracoesAoIniciar=true` em desenvolvimento).
- **DLL bloqueada ao compilar (MSB3027/MSB3021 "bloqueado por Lone.Api"):** a API ou o app estão rodando — parar a
  depuração (Shift+F5) ou fechar o console da API e compilar de novo.
- Ambiente do assistente: não há SDK .NET; não compila nem roda testes. Se a conversa estiver ligada ao PC do usuário, a pasta
  aparece como `$HOME/mnt/Lone` no shell do dispositivo (tem `git`, `python3`, `zip`). Para apagar arquivos (ex.:
  `.git/index.lock`) é preciso pedir permissão de exclusão.

## 4. Arquitetura (resumo)

`Lone.App` (MAUI, Windows + Android) → `Lone.Cliente` (ViewModels CommunityToolkit.Mvvm + `ClienteApi`, testável sem MAUI) →
`Lone.Api` (Minimal APIs, JWT) → `Lone.Application` (AppServices, `IAutorizacao.Exigir`) → `Lone.Domain` →
`Lone.Infrastructure` (EF Core 10, repositórios, `ColetorAuditoria`) → SQL Server. `Lone.Contracts`: DTOs, `Rotas`, `Permissoes`.

Testes (`tests/Lone.Tests`, xUnit): o cliente é testado com `ServidorFalso` (respostas em fila FIFO; `Recebidas` guarda método +
caminho, sem query), `AmbienteCliente`, `DialogosFalsos` (`RespostaEscolha`, `OpcoesOferecidas` para o novo `EscolherAsync`).

## 5. O que o redesenho entregou (commit `49b9f96`) e o que conferir no app

Detalhes em `docs/UX-ARQUITETURA.md`. Resumo:
- **Menu lateral próprio** (`AppShell.xaml` FlyoutContent + `MenuLateral.cs` + `MenuViewModel.CriarSecoes`): seções Início,
  PESSOAS (Pessoas, Consulta avançada, ⚙ Configurações), ORGANIZAÇÃO (Grupos empresariais, ⚙), METAS (Metas, ⚙),
  ⚙ Configurações do sistema; item ativo destacado; rodapé com usuário, Trocar empresa, Trocar senha, Sair.
  Os `FlyoutItem` continuam registrando as rotas/permissões, mas ocultos.
- **Configurações por módulo:** rotas `configuracoes-pessoas`, `-organizacao`, `-metas`, `-sistema` (mesma `ConfiguracoesPage`;
  catálogo em `ModulosConfiguracao`/`ConfiguracoesViewModel`, 19 cadastros, cada um com a permissão de antes).
- **Lista de Pessoas:** filtros rápidos (Todos, PF, PJ, Clientes, Fornecedores, Ativos, Inativos), painel de filtros avançados,
  tabela com selos, colunas que somem por largura (1100/940/720/600), menu ⋯ por linha, estado vazio,
  **paginação no servidor** `GET api/v1/pessoas/pagina` (50 por página, máx. 200; `PaginaListaPessoas`).
- **Ficha em tela cheia:** "← Voltar para Pessoas", cabeçalho (nome, situação, tipo, papéis, documento · código · cidade),
  abas com indicador, aba Geral em blocos, barra fixa Salvar/Descartar/Fechar.
- **Design system:** tokens em `Cores.xaml`, estilos em `Estilos.xaml` (bloco "DESIGN SYSTEM DO LONE").
- **Cuidado com nomes parecidos:** `PaginaPessoas` (Consulta avançada) ≠ `PaginaListaPessoas` (lista nova);
  `PessoaResumo.SituacaoTexto` (vazio quando Ativo, usado no detalhe) ≠ `PessoaResumo.SituacaoSelo` (sempre texto, usado no selo).

**Roteiro de conferência no app (Windows, depois Android):**
1. Menu: seções, item ativo ao navegar, rodapé (Trocar empresa/senha, Sair); no celular o menu fecha ao escolher.
2. Pessoas: busca, cada filtro rápido, painel Filtros + Limpar, paginação (mais de 50 registros), estreitar a janela e ver as
   colunas sumirem, ⋯ da linha (Abrir, Histórico, Desativar/Reativar com e sem permissão).
3. Ficha: abrir, trocar de aba, salvar, descartar, voltar para a lista; conferir que nenhuma aba antiga sumiu.
4. ⚙ de cada módulo: grupos e itens conforme o perfil; abrir um cadastro auxiliar e voltar.

## 6. Pontos em aberto e próximos passos (só quando o usuário pedir)

- **Conferência visual do redesenho** (seção 5) e ajustes que o usuário apontar.
- Aplicar o novo cabeçalho/estilo às outras páginas (cadastros auxiliares, Metas, Grupos empresariais, Consulta avançada).
- Link "voltar para Configurações do módulo" nas páginas de cadastros auxiliares.
- Fonte de ícones (Fluent/Material) no lugar dos caracteres ⚙ ⋯ ← +.
- Fase 3: a ordem da regra central seguiu a **matriz** (consentimento antes de "Aceita comunicações"); o usuário foi avisado —
  confirmar se mantém. Fora do escopo da Fase 3: consentimento de pessoas de contato, filtro por consentimento na consulta
  avançada, marketing em telefone, bases legais além de Consentimento, direitos do titular.
- Itens antigos do plano de revisão (`docs/REVISAO-PESSOAS.md`): validação de campo sem ícone verde, Nacional/Estrangeira,
  Segmentos (um principal por pessoa), multiseleção, aba Comercial contextual ao papel Cliente, melhorias da CBO, unicidade
  de documento por tipo + número + país. Módulos futuros no menu: Vendas, Compras, Estoque, Financeiro, Fiscal.

## 7. Como retomar na conta nova

1. Abra `Lone.slnx` no Visual Studio (a pasta é a mesma; se for outro PC, descompacte o pacote
   `_entrega/Lone-completo-49b9f96.zip`, que tem o histórico git, sem `bin/`, `obj/`, `.vs/`).
2. Numa sessão nova do Claude, conecte a pasta `Lone` e peça: *"leia docs/PASSAGEM-DE-CONTA.md e os documentos que ele indica
   e continue o Lone ERP"*.
3. Antes de mudar código, o assistente deve rodar `git log --oneline -5` e `git status` para confirmar que está em `49b9f96`.
