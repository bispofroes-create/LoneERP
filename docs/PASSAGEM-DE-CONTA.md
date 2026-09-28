# Lone ERP — passagem para outra conta (27/09/2026, noite)

> **Para o assistente da conta nova:** leia este arquivo inteiro antes de qualquer coisa. Depois, leia na ordem:
> `docs/CONTINUIDADE.md` (histórico completo, decisões e padrões — as entradas de 27/09 são as desta passagem),
> `docs/UX-ARQUITETURA.md` (design system), `docs/FASE3-PRIVACIDADE.md` (LGPD) e `docs/REVISAO-PESSOAS.md`.
> Responda sempre em **português**. O usuário é perfeccionista com arquitetura e UX, compara com ERPs maduros e quer
> análise + plano antes de código.

---

## 1. Estado do repositório (fim de 27/09/2026)

- Pasta no PC do usuário: `C:\Users\Windows 11\source\repos\Lone` (solução `Lone.slnx`, git só local, sem remoto).
- **Último commit: `ef9d577`** (branch `pessoas-fiscal-documentos-resumo`) — Pessoas: fiscal por natureza, documentos por
  natureza, identificação com documento primeiro e resumo da pessoa.
- **Muito trabalho SEM COMMIT** no working tree (~37 arquivos alterados + ~20 novos). É tudo o que está na seção 5. O
  usuário ainda não pediu o commit: **não commitar sem ele pedir** ("pode fazer o commit").
- **Compilação:** o usuário compilou e rodou a Etapa 2 no Windows (as abas com contador aparecem na tela). As mudanças
  posteriores (abas configuráveis + correções da última revisão, seção 5.6) **ainda não foram compiladas**.
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

**Imediato (quando o usuário voltar):**
1. Compilar; tratar os erros que o usuário mandar (prints).
2. `dotnet test` — corrigir falhas (atenção a filas do `ServidorFalso`).
3. Conferir na tela: abas (＋, Transportadora, visão como aba com contador, tirar a aba marcada), rolagem lateral com
   muitas colunas, velocidade da busca (contagens = 2 consultas a mais na página 1), celular.
4. Com tudo certo e o usuário pedindo: **commit** (há muito trabalho sem commit — sugerir dividir em 2–3 commits: filtros
   fases 1–4; Etapas 1–2; abas configuráveis).

**Próximas etapas combinadas (fazer só quando o usuário pedir):**
- Largura da coluna por arrasto e reordenar colunas arrastando (hoje ↑↓ no seletor) — testar arrasto no Windows.
- **Etapa 3:** prévia lateral ao clicar no nome (reaproveitar o "Resumo da pessoa") e faixa de indicadores clicáveis
  (cadastros ativos, com pendência cadastral, documentos vencendo, com bloqueio).
- **Etapa 4:** seleção múltipla com ações em lote (etiquetar, atribuir vendedor, exportar selecionados, inativar) —
  endpoints próprios com permissão e auditoria por pessoa.
- Abas: "padrão da empresa" (definido por quem configura Pessoas) — precisa decidir onde guardar (aprovação de banco).
- Configurações fora do menu também para Organização e Metas (as telas deles ainda não têm o botão).
- Pendências antigas ainda sem aprovação: migration dos documentos ("Aplica-se a", campos padrão por tipo, documento ↔
  estabelecimento); fase futura produtor rural e retenções (desenho em `CONTINUIDADE.md`).
- Desempenho: sem índices novos; se filtros de texto ou contagens ficarem lentos em base grande, medir e propor
  migration só de índices (com aprovação).

## 7. Como retomar na conta nova

1. Abra `Lone.slnx` no Visual Studio (mesma pasta). Se for outro PC, copie a pasta inteira (inclui `.git` e o trabalho
   sem commit).
2. Numa conversa nova do Claude, **conecte a pasta `Lone`** e peça: *"leia docs/PASSAGEM-DE-CONTA.md e os documentos que
   ele indica e continue o Lone ERP"*.
3. O assistente deve começar com `git log --oneline -5` (esperado `ef9d577` no topo) e `git status` (esperado o trabalho
   sem commit da seção 5), e só então seguir a seção 6.
