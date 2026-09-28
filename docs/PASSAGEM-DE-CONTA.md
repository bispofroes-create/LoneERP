# Lone ERP — passagem para outra conta (27/09/2026, noite)

> **Para o assistente da conta nova:** leia este arquivo inteiro antes de qualquer coisa. Depois, leia na ordem:
> `docs/CONTINUIDADE.md` (histórico completo, decisões e padrões — as entradas de 27/09 são as desta passagem),
> `docs/UX-ARQUITETURA.md` (design system), `docs/FASE3-PRIVACIDADE.md` (LGPD) e `docs/REVISAO-PESSOAS.md`.
> Responda sempre em **português**. O usuário é perfeccionista com arquitetura e UX, compara com ERPs maduros e quer
> análise + plano antes de código.

---

> **Atualização (27/09/2026, 23h, conta nova):** a conta nova retomou por este arquivo, fez a Etapa 3 (prévia e
> indicadores) e o usuário compilou e pediu os commits. O estado atual está na seção 1; o que ela fez está nas últimas
> entradas de 27/09 do `CONTINUIDADE.md` e em `docs/PLANO-ETAPA3-PESSOAS.md`. As seções 5 e 6 abaixo são da passagem
> original (o "sem commit" delas já não vale).

## 1. Estado do repositório (27/09/2026, 23h)

- Pasta no PC do usuário: `C:\Users\Windows 11\source\repos\Lone` (solução `Lone.slnx`, git só local, sem remoto).
- Branch `pessoas-fiscal-documentos-resumo`. Commits novos sobre `ef9d577`:
  - `7155284`: filtros por catálogo, colunas escolhidas, Etapas 1–2 e abas configuráveis (inclui a migration `PreferenciasTela`).
  - `8651c2f`: Etapa 3a, a prévia ao lado da lista.
  - `8db9988`: Etapa 3b, a faixa de indicadores, com dois testes antigos atualizados.
  - Depois deles, o commit só de documentação desta atualização.
- **Working tree limpo**; só `_entrega/` fica fora do git. Regra mantida: nunca commitar sem o usuário pedir e nunca fazer push.
- **Compilação e testes:** build ok; `dotnet test` com 1.328 aprovados, 0 falhas e 22 ignorados. Os ignorados são
  testes de banco (`Infraestrutura.Migracao*`, `BancoEnderecoFinalidadeTests`), que só rodam com SQL Server de teste
  configurado. Antes de rodar, feche a API e o app: senão as DLLs ficam travadas (MSB3027).
- **Migration nova já gerada e aplicada pelo usuário:** `20260927231413_PreferenciasTela` (só cria a tabela
  `PreferenciasTela`). Nenhuma outra migration pendente.
- Testes: não rodados desde as últimas mudanças (o assistente não tem SDK). Pedir ao usuário `dotnet test`.

## 2. Regras de trabalho combinadas com o usuário (seguir sempre)

- **Idioma:** respostas, código, comentários e mensagens em português (nomes de projetos `Lone.*` em inglês).
- **Commit/push:** nunca commitar sem autorização explícita; nunca fazer push. Mensagens de commit em português.
- **Banco:** nenhuma mudança de banco sem aprovação; nunca aplicar migration em produção; nada de `DROP`/`DELETE` em massa;
  nunca excluir fisicamente (desativar/encerrar); histórico preservado; só LINQ parametrizado (nunca SQL montado com texto).
- **Negócio:** não assumir regra jurídica/de negócio indefinida — parar e perguntar. Apresentar análise, comparação com ERPs
  maduros e alternativas antes de mudanças grandes; mudanças incrementais.
- **Estilo:** classes pequenas; nada de lógica em code-behind (só cola de interface); `///` em português explicando o porquê;
  bindings compilados (`x:DataType`) no XAML.
- **Entrega:** relatório curto no fim (o que mudou, testes, o que o usuário precisa fazer). O usuário compila no Visual Studio
  e manda prints dos erros.

## 3. Ambiente e fluxo do usuário

- Windows 11, Visual Studio 2026, .NET 10 (SDK 10.0.401 em `global.json`), SQL Server Express `.\SQLEXPRESS`, banco `LoneERP`.
- O usuário roda no **Console do Gerenciador de Pacotes (PMC)**: `dotnet build`, `dotnet test`, `Add-Migration`.
  Migration: `Add-Migration <Nome> -Project Lone.Infrastructure -StartupProject Lone.Api -OutputDir Persistencia/Migracoes`.
  A API aplica migrations pendentes ao subir — **sem a migration gerada a API cai ao iniciar** (erro de "servidor web não
  está mais em execução" no VS). O PC do usuário não tem Python.
- Ambiente do assistente: sem SDK .NET (não compila, não roda testes). Com a conversa ligada ao PC, a pasta aparece como
  `$HOME/mnt/Lone` no shell do dispositivo (tem git, python3). Revisões por agente em cópia no container funcionaram bem
  para achar erros de compilação sem SDK.
- Mockup aprovado da tela de Pessoas: artifact "Lone — Tela de Pessoas (proposta)" na conta antiga (privado; na conta nova
  não abre — a descrição abaixo basta).

## 4. Arquitetura (resumo)

`Lone.App` (MAUI, Windows + Android) → `Lone.Cliente` (ViewModels CommunityToolkit.Mvvm 8.4 + `ClienteApi`, testável sem
MAUI) → `Lone.Api` (Minimal APIs, JWT) → `Lone.Application` (AppServices, `IAutorizacao.Exigir`) → `Lone.Domain` →
`Lone.Infrastructure` (EF Core 10) → SQL Server. `Lone.Contracts`: DTOs, `Rotas`, `Permissoes`.
Testes (`tests/Lone.Tests`, xUnit): cliente testado com `ServidorFalso` (**respostas em fila FIFO** — toda requisição nova
na abertura da tela exige resposta a mais nos testes), `AmbienteCliente`, `DialogosFalsos`.
**Lone.Cliente não referencia Lone.Application** (constantes compartilhadas ficam em Contracts).

## 5. O que foi feito nesta conta (27/09) — tudo em `docs/CONTINUIDADE.md`, entradas de 27/09

1. **Commit `ef9d577`**: aba Fiscal da PF; documentos por natureza; natureza jurídica e "Regime normal" do CNPJ;
   Identificação com CPF/CNPJ logo após a natureza, aviso de CPF/CNPJ duplicado ("Abrir cadastro"); ficha larga (1600)
   em 3 colunas com painel "Resumo da pessoa"; confirmação ao trocar PJ→PF com dados da empresa.
2. **Filtro de Pessoas, fases 1–4** (sem commit): catálogo de campos + motor único (`CatalogoFiltrosPessoas`,
   `FiltrosPessoasSql`, Ids em `CamposFiltroPessoas`), painel de filtros à direita com chips, ~72 campos, telefone/DDD/
   e-mail; visões salvas (tabela `FiltroSalvo` reaproveitada), exportar CSV, "Procurar endereços duplicados" no ⋯.
   **Consulta avançada removida** do menu/app (favoritos e recentes antigos redirecionam para `pessoas`).
3. **Etapa 1 da tela de Pessoas** (sem commit): colunas escolhidas pelo usuário (seletor com busca, marcar todas,
   restaurar padrão, ↑↓), nome preso à esquerda + rolagem lateral, ordenação em 3 cliques (crescente → decrescente →
   padrão) no servidor, linha de filtro por coluna liga/desliga (vira condição/chip), tabela `PreferenciasTela`
   (preferência por usuário + tela; `GET/PUT api/menu/telas/{tela}`), visões guardam o layout.
4. **Etapa 2** (sem commit, compilada pelo usuário): menu lateral sem Configurações de Pessoas (continuam na busca e nos
   favoritos); botão "⚙ Configurações" na tela **abre direto a página Configurações de Pessoas em cartões** (o usuário
   não gostou da lista); barra única (busca, Visões, Filtros, ⋯); abas com contador (Ativos/Inativos saíram das abas);
   chips com "Limpar"; sucesso em aviso flutuante; linha com avatar, selos de papéis (`SeloTom`), "Sem CPF/CNPJ", ações
   rápidas ligar/WhatsApp/e-mail ao passar o mouse; densidade confortável/compacta; celular em cartões + "Ordenar".
5. **Abas escolhidas pelo usuário** (sem commit, **não compilado**): "Todos" + até 10 abas de natureza, papel
   (inclusive Transportadora e papéis criados) ou visão salva (★), editor "＋", contador em todas (visões por
   `POST pessoas/consulta/filtros/contagens`), guardadas em `LayoutListaPessoas.Abas`.
6. **Última revisão por agente (aplicada, não compilada):** `EditorAbas.Abrir` público; `Assert.Same`→`Equal` no teste;
   "Mostrar como aba" remonta o catálogo antes; ids guardados preservados; sair/remover aba de visão limpa os filtros dela;
   aba marcada que some volta para "Todos" relendo; sem releitura dupla ao trocar a busca por código
   (`CancelarBuscaAtrasada` no `CadastroViewModelBase`); plural dos papéis só em casos seguros; catálogo que falhou não
   grava preferência por cima; `OpenAsync` checa o retorno; contagem por papel sem `Distinct`.

## 6. Pendências e próximos passos

**Imediato (atualizado às 23h de 27/09):**
1. Conferir na tela:
   - abas: ＋, Transportadora, visão como aba com contador, tirar a aba marcada;
   - prévia: clique, duplo clique, ‹ › e o botão "Clique: …";
   - indicadores: tocar põe e tira o filtro; com "Com pendência cadastral", o resumo das pessoas mostra a pendência;
   - rolagem lateral com muitas colunas, velocidade da busca e celular.

**Próximas etapas combinadas (fazer só quando o usuário pedir):**
- Largura da coluna por arrasto e reordenar colunas arrastando (hoje ↑↓ no seletor) — testar arrasto no Windows.
- ~~Etapa 3~~: feita (commits `8651c2f` e `8db9988`). Ficou de fora o teclado na prévia (↑↓, Enter, Esc), que precisa de
  código por plataforma no MAUI.
- **Etapa 4:** seleção múltipla com ações em lote (etiquetar, atribuir vendedor, exportar selecionados, inativar) —
  endpoints próprios com permissão e auditoria por pessoa.
- Abas: "padrão da empresa" (definido por quem configura Pessoas) — precisa decidir onde guardar (aprovação de banco).
- Configurações fora do menu também para Organização e Metas (as telas deles ainda não têm o botão).
- Pendências antigas ainda sem aprovação: migration dos documentos ("Aplica-se a", campos padrão por tipo, documento ↔
  estabelecimento); fase futura produtor rural e retenções (desenho em `CONTINUIDADE.md`).
- Desempenho: sem índices novos; se filtros de texto ou contagens ficarem lentos em base grande, medir e propor
  migration só de índices (com aprovação).

## 7. Como retomar na conta nova

1. Abra `Lone.slnx` no Visual Studio (mesma pasta). Se for outro PC, copie a pasta inteira (inclui `.git`).
2. Numa conversa nova do Claude, **conecte a pasta `Lone`** e peça: *"leia docs/PASSAGEM-DE-CONTA.md e os documentos que
   ele indica e continue o Lone ERP"*.
3. O assistente deve começar com `git --no-optional-locks log --oneline -5` (esperado o commit de documentação no topo,
   depois `8db9988`) e `git --no-optional-locks status` (esperado limpo, fora `_entrega/`), e só então seguir a seção 6.
   Use `--no-optional-locks`: pelo shell do dispositivo, um `git status` comum deixa um `.git/index.lock` que só sai
   com permissão de apagar, e esse arquivo trava o git no Visual Studio.
