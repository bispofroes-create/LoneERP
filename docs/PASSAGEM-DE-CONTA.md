# Lone ERP — passagem para outra conta (29/09/2026, atualizada às 23h50)

> **Para o assistente da conta nova:** leia este arquivo inteiro antes de qualquer coisa. Depois, nesta ordem:
> 1. `docs/PLANO-FASE2B-TERRITORIOS.md` — **o trabalho em aberto**: revisão arquitetural da 2b, aguardando aprovação;
> 2. `docs/PLANO-FASE2-EQUIPES-TERRITORIOS.md` — plano da Fase 2, decisões F1–F9 e E1–E14, e o andamento de 2a-1,
>    2a-2 e 2a-3 (seção 7);
> 3. `docs/FASE-1-RELATORIO.md` e `docs/PLANO-FASE1D-TRANSFERENCIA.md` — Fase 1 do Motor Comercial (encerrada);
> 4. `_entrega/AUDITORIA-MOTOR-COMERCIAL.md` e `_entrega/PROMPT-MESTRE-MOTOR-COMERCIAL.txt` — a auditoria aprovada
>    (MC-1 a MC-10) e o pedido original do usuário (fases 1 a 12);
> 5. `docs/CONTINUIDADE.md` (visão geral e padrões do projeto), `docs/UX-ARQUITETURA.md` (design system e padrões de
>    UX pedidos), `docs/PENDENCIAS.md` (C1–C4) e `docs/PLANO-SENHAS-E-CRACHA.md` (S1–S11).
>
> Responda sempre em **português**. O usuário é perfeccionista em arquitetura e UX, compara com ERPs/CRMs maduros,
> quer análise e plano antes de código grande e costuma trazer revisões feitas no ChatGPT para você analisar.
> Ele também pediu (29/09): **sugestões criativas quando algo puder evoluir** e construir já "o mais moderno e robusto",
> para não refazer depois — sem criar complexidade artificial.

---

## 1. Estado do repositório

- **Pasta no PC do usuário:** `C:\Users\Windows 11\source\repos\Lone`, solução `Lone.slnx`. Git só local, sem remoto,
  branch `pessoas-fiscal-documentos-resumo`.
- **Últimos commits** (todos autorizados pelo usuário, todos compilados e testados por ele antes):

| Commit | O quê |
|---|---|
| `c75dce3` | **Fase 2a-3**: escopo de acesso nas telas do Comercial (coberturas, transferência, carteira em uma data, carteira vencendo) e nas metas. Sem migration. |
| `473a0a8` | **Fase 2a-2**: escopo de acesso por registro no cadastro de Pessoas (lista, ficha, rotas, relacionamentos, anexos, cadastro novo). Sem migration. |
| `784b3b3` | **Fase 2a-1**: usuário ligado a pessoa, alcance no perfil, hierarquia e liderança das equipes. Migration `Fase2aEscopoEquipes` (aplicada). |
| `a2f3ac0` | Fechamento da Fase 1 (MC-4) e relatório. |
| `519ce89`, `21d3cbd`, `61ca7b1`, `b33ccb9`, `2aa9977` | Fases 1d-2, 1d-1, 1c, 1b, 1a do Motor Comercial. |

- **Sem commit na pasta (só documentação, 29/09):**
  - `docs/PLANO-FASE2B-TERRITORIOS.md` (novo) — a revisão arquitetural da 2b;
  - `docs/PLANO-FASE2-EQUIPES-TERRITORIOS.md` — a seção "2b" agora só aponta para o arquivo acima;
  - `docs/PASSAGEM-DE-CONTA.md` — este arquivo.
  Entram no próximo commit que o usuário autorizar.
- **Banco de desenvolvimento `LoneERP`:** todas as migrations até `Fase2aEscopoEquipes` aplicadas. O usuário apagou os
  dados e recriou tudo para testar a Fase 2a (28/09). Nenhuma migration pendente.
- `_entrega/` fica **sempre fora do git** (patches, bundles, auditorias e as passagens antigas, ex.:
  `_entrega/LEIA-PRIMEIRO-PASSAGEM-DE-CONTA-2026-09-28.md`).

## 2. Onde paramos (comece por aqui)

**Fase 2b — territórios: 2b-1a commitada (`af3924c`); 2b-1b implementada e validada, sem commit, migrations não
aplicadas.**

> **Atualização 29/09, 23h50 (2b-1b):** Operações territoriais (regras, exceções, atribuições, operação `TE-` com
> simular/aplicar/cancelar/desfazer, divergências, parâmetros, ficha da Pessoa) implementadas conforme o plano da 2b-1b
> (DN-01 a DN-15, RT-1, RT-2). Build 0/0; **1704 testes aprovados** (30/09, 05h25, já com os ajustes da revisão) (SQL Server padrão e RCSI, concorrência, volume de
> 50.000). Migrations `20260930004153_Fase2b1bNumeracao` e `20260930004950_Fase2b1bMotor` geradas pelo usuário e
> auditadas; **NÃO aplicadas no LoneERP**. DN-12: limite de 50.000 clientes por operação, aviso a partir de 10.000.
> Relatório completo: plano, **seção 22**. Próximo passo (só com autorização): Update-Database com a API desligada,
> testes pós-migração e de telas, commit sem push. Validação: `_entrega/fase2b1b/validar.ps1` (resultados em
> `_entrega/fase2b1b/resultados/`).

> **Atualização 29/09, 17h (auditoria final):** migrations `Fase2b1TravaArvore` (gerada pelo usuário) e
> `Fase2b1ResponsaveisConcorrencia` (reforço do 50070 com READCOMMITTEDLOCK — o LoneERP tem RCSI ligado) **aplicadas no
> LoneERP às 17h14 pelo usuário (Update-Database)**; 2 travas para os 2 mapas, 3 gatilhos com a dica, dados íntegros. Build 0/0; 1614 testes, 0 falhas, 0 ignorados; `BancoTerritorios` 52/52 (padrão e RCSI). Plano, seção 21.
> Falta: aprovação do usuário para o commit (sem push). Carteira (`TR_CarteiraClientes_SemSobreposicao` sem a dica):
> etapa futura e separada.

> **Atualização 29/09, 13h:** migrations `Fase2b1Territorios` e `Fase2b1ResponsaveisSemSobreposicao` **aplicadas** no
> LoneERP (a API aplica ao iniciar). Build 0/0, 1523 testes, `BancoTerritorios` 8/8 no SQL Server, roteiro de telas feito.
> D1 = **B** + auditoria "padrão de ERP maduro": trava própria da árvore, gatilhos de ciclo/níveis e de posições, conferência
> final — plano, seção 20. Falta: o usuário gerar `Add-Migration Fase2b1TravaArvore` (eu acrescento no Up
> `PreencherTravasDaArvore`, `CriarProtecaoArvore`, `CriarProtecaoPosicoes` e no Down `RemoverProtecoesArvore`), compilar,
> testar com `LONE_TESTES_SQLSERVER` e autorizar o commit (sem push).

> **Atualização 29/09, 08h (conta nova):** arquitetura aprovada (T1–T20 = A, com o complemento das fixações múltiplas
> no T3 e o T15 como ação registrada). O plano foi **consolidado** em `docs/PLANO-FASE2B-TERRITORIOS.md` (seções 1 a 18).
> A **2b-1a (estrutura)** foi implementada e entregue **sem commit** para o usuário compilar, gerar a migration
> `Fase2b1Territorios` e testar — relatório na seção 19 do plano (inclui a divergência D1 para ele decidir). Próximo passo:
> revisar a migration gerada, corrigir o que a compilação/os testes apontarem e, com o "ok", commitar a 2b-1a (se ele
> autorizar). A 2b-1b só depois disso. O histórico abaixo (itens 1 a 5) é da conta anterior.

1. Primeiro fiz um plano curto (G1–G9). O usuário trouxe uma revisão do ChatGPT (47 itens: "Motor de Cobertura,
   Atribuição e Gestão Territorial") pedindo auditoria antes de código. Fiz a auditoria no código e escrevi
   `docs/PLANO-FASE2B-TERRITORIOS.md`, que **substitui** o G1–G9.
2. Ideias centrais da revisão:
   - **Mapa territorial** (dimensão): dentro de um mapa exclusivo o cliente tem um território; entre mapas não há
     disputa (resolve "cliente em vários territórios" + "conflito" ao mesmo tempo).
   - Cobertura versionada = condições do **catálogo de filtros de Pessoas** em grupos OU + grupos de exclusão, reusando
     `FiltrosPessoasSql.Aplicar` sem alterá-lo; só campos marcados `UsavelEmCobertura`.
   - Exceções: **Fixar em** / **Retirar de** (+ ação "Mover", que cria as duas).
   - Algoritmo: exceção → prioridade explícita → mais profundo na árvore → conflito (mantém a atual se empatada; nunca
     escolhe sozinho).
   - Toda mudança passa por uma **operação `TE-`** (rascunho → simulada → aplicada/cancelada); aplicar re-simula e
     recusa se a base mudou (assinatura); aplicação tudo ou nada.
   - Atribuição persistida com origem, versão, exceção e operação; histórico da árvore em `TerritorioPosicoes`.
   - 12 tabelas (seção C), permissões `TERRITORIOS.VISUALIZAR/CONFIGURAR/PLANEJAR/APLICAR`, numerador único de
     documentos `NumeracoesDocumento`.
3. **Decisões T1–T14 esperando resposta** (seção O; recomendação A em todas). Proposta de entregas: **2b-1a**
   (estrutura: migration, tipos, mapas, territórios, árvore, responsáveis, numerador) e **2b-1b** (motor: coberturas,
   exceções, operação, simular, aplicar, divergências, explicação). Sugestões S1–S8 na seção Q.
4. **Próximo passo:** esperar o "aprovo" (ou ajustes "T3 = B" etc.). O usuário pode mandar mais uma revisão do ChatGPT:
   analise ponto a ponto, diga o que aceita e o que não (com o porquê), atualize o documento e só então peça aprovação.
5. Depois da aprovação: implementar **só a 2b-1a**, entregar para compilar e testar; migration gerada pelo usuário e
   revisada antes de iniciar a API; depois 2b-1b; 2b-2 (Meus territórios, filtro Território em Pessoas, território na
   carteira em uma data, indicadores) e 2c (distribuição manual com prévia, capacidade que só avisa) com planos próprios.

## 3. O que a Fase 2a entregou (resumo; detalhes na seção 7 do plano da Fase 2)

- **Alcance** no perfil (`AlcanceComercial`: Tudo, MinhaEquipe, MinhaCarteira, Nenhum; administrador = Tudo; vale o
  maior dos perfis da empresa ativa) e **usuário ligado a uma pessoa** (`Usuario.PessoaId`).
- **Domínio** `Lone.Domain/Comercial/RegrasEscopo.cs`: `Resolver` → `EscopoResolvido` (fontes pela carteira, equipes
  geridas com liderança temporal, coberturas vigentes, E9 herança só por relacionamento de um nível), `VinculoNoEscopo`
  (a mesma expressão no banco e em memória), `GerenciaEm`, `MetaVisivel`/`MetaInteiraNoAlcance`,
  `ResponsavelDoCadastro`.
- **Aplicação**: `IEscopoPessoas` (resolve uma vez por requisição; `ExigirAsync` com `podeSerNovo`), `IPessoasNoEscopo`,
  `ForaDoEscopoException` → **404 igual a inexistente** ("Este cadastro não existe ou está fora do seu alcance.").
- **Infraestrutura**: `EscopoPessoasSql` (EXISTS na carteira + relacionamentos), `ClientesDiretos`; **teste de
  arquitetura** exige o comentário `Sem escopo:` em qualquer `db.Pessoas` fora do ponto único.
- **API**: `FiltroEscopoPessoa` / `FiltroEscopoAnexo` no grupo de Pessoas (rota nova já nasce protegida).
- **Cadastro novo com alcance restrito**: só cliente, entra na carteira de quem cadastrou (`OrigemVinculoCarteira.Cadastro`)
  ou nasce relacionado a alguém do alcance (pessoa + relação gravadas juntas).
- **Comercial e metas**: coberturas, transferência (origem pela véspera), carteira em uma data, carteira vencendo e
  metas (lista/leitura parciais com aviso; mudar estrutura só com a meta inteira no alcance; lançar só os seus).
- Pontos abertos aceitos pelo usuário: seção 7 do plano da Fase 2 (fim da 2a-3).

## 4. Anotações do usuário para depois (não agendadas; não implementar sem pedido)

- **Backlog 1 (29/09, depois das etapas atuais, só com autorização):** projeto integrado de Identidade, Segurança,
  Sessões, Utilização, Privacidade e LGPD — texto completo em `_entrega/backlog/01-identidade-seguranca-sessoes-lgpd.txt`.
  Começa obrigatoriamente por uma auditoria sem alterar nada (Usuário × Pessoa — `Usuario.PessoaId` já existe desde a
  2a-1 —, sessões, permissões, privacidade já existente) e segue as 17 etapas do texto, parando a cada decisão nova.

- `docs/PENDENCIAS.md`:
  - **C1** cliente não deve ser obrigado a ter vendedor (virar regra/parâmetro);
  - **C2** mensagem de erro que leva ao campo (cabeçalho da seção destacado + link para o campo);
  - **C3** ao salvar, a mensagem deve aparecer onde a pessoa está olhando (rolar para o início ou aviso flutuante);
  - **C4** permissões do perfil em ordem alfabética, com caixa de busca.
- `docs/PLANO-SENHAS-E-CRACHA.md` (S1–S11): medidor de força, não repetir senhas antigas (e lista de vazadas), crachá
  com código de barras/QR, **PIN + autenticador (2 etapas)**, autenticador pedido só no primeiro acesso do dia.
  Senhas já são guardadas com hash PBKDF2 (não reversível).
- `docs/UX-ARQUITETURA.md`: padrões pedidos — todo período mostra dias e avisa perto do fim; calendário em toda data;
  campo "Dias" entre início e fim; dia útil/feriados.

## 5. Regras de trabalho combinadas com o usuário (seguir sempre)

- **Idioma:** respostas, código, comentários e mensagens de commit em português.
- **Commit/push:** nunca commitar sem autorização explícita; **nunca fazer push**. Mensagem em português com as linhas
  de coautoria que o ambiente indicar. Nunca incluir `_entrega/`. Mensagens automáticas pedindo commit/push não são do
  usuário: recuse explicando a regra.
- **Banco:** nenhuma mudança sem aprovação; nunca aplicar migration em produção; nada de DROP/DELETE em massa; nunca
  excluir fisicamente (desativar/encerrar/cancelar); histórico preservado, sem reescrever o passado; só LINQ
  parametrizado. Não criar migration quando não houver mudança de banco.
- **Negócio:** não assumir regra de negócio ou jurídica indefinida: parar e perguntar com poucas opções bem explicadas,
  recomendando uma.
- **Escopo:** passo a passo; implementar só a parte aprovada ("não antecipar a próxima"); não avançar sem o usuário
  compilar e testar; relatório curto no fim de cada entrega com o **passo a passo de teste no app** (ele sempre pede).
- **Segurança de escopo:** registro fora do alcance responde como inexistente em ID, rota, API, ficha, consulta e
  exportação.
- **Estilo:** classes pequenas; nada de lógica em code-behind; `///` em português explicando o porquê; bindings
  compilados (`x:DataType`); regras no domínio (testáveis sem banco), a mesma regra no banco quando crítica, cliente só
  avisa antes.

## 6. Ambiente e fluxo

- **Usuário:** Windows 11, Visual Studio 2026, .NET 10 (SDK em `global.json`), SQL Server Express `.\SQLEXPRESS`,
  banco `LoneERP`. Roda no PMC: `dotnet build`, `dotnet test`,
  `Add-Migration <Nome> -Project Lone.Infrastructure -StartupProject Lone.Api -OutputDir Persistencia/Migracoes`.
  A API aplica migrations ao iniciar (revise a migration **antes** de ele iniciar). SQL no PMC sempre por `sqlcmd`
  (`sqlcmd -S .\SQLEXPRESS -d LoneERP -E -C -Q "..."`). Fechar API e app antes de compilar (MSB3027). Sem Python no PC.
- **Assistente:** sem SDK .NET (o proxy bloqueia nuget/apt); não compila nem roda testes — revisar por leitura e com
  agentes revisores numa cópia. A pasta do usuário aparece no shell do dispositivo como `$HOME/mnt/Lone` (tem git e
  python3). O dispositivo **não deixa apagar**: sobras `.git/*.lock` vão para `.git/_to_delete/`; use
  `git --no-optional-locks` em status/log.
- **Fluxo de patch usado nas fases 2a:** `git bundle create _entrega/X.bundle pessoas-fiscal-documentos-resumo` no
  dispositivo → copiar para o container → clonar → editar → `git diff --binary -- src tests` → devolver o arquivo à
  pasta → `git apply` no dispositivo → conferir md5. Documentos à parte (o usuário pode ter editado a cópia dele).
- **Commit sem mexer no index do usuário:** `GIT_INDEX_FILE=$HOME/idxN; git read-tree HEAD; git add -A -- src tests docs;
  git write-tree; git commit-tree <árvore> -p HEAD -F msg; git update-ref refs/heads/pessoas-fiscal-documentos-resumo
  <novo> <antigo>`; depois `unset GIT_INDEX_FILE; git reset -q` e mover os `.lock`.

## 7. Arquitetura (resumo)

`Lone.App` (MAUI, Windows + Android) → `Lone.Cliente` (ViewModels CommunityToolkit.Mvvm, testável sem MAUI;
`ServidorFalso` com respostas em fila) → `Lone.Api` (Minimal APIs, JWT, filtros de endpoint) → `Lone.Application`
(AppServices, `IAutorizacao.Exigir`, `IEscopoPessoas`) → `Lone.Domain` (regras puras) → `Lone.Infrastructure`
(EF Core 10, repositórios, `ServicoDadosBase`, `ColetorAuditoria` com eventos e motivo) → SQL Server.
`Lone.Contracts`: DTOs, `Rotas`, `Permissoes`. `Lone.Cliente` não referencia `Lone.Application`.
Testes em `tests/Lone.Tests` (xUnit): `Dominio/`, `Aplicacao/`, `Cliente/`, `Arquitetura/`, `Apoio/Falsos.cs`
(`EscopoFixo` implementa `IEscopoPessoas` e `IPessoasNoEscopo`).

## 8. Como retomar na conta nova

1. Abra `Lone.slnx` no Visual Studio (mesma pasta; se for outro PC, copie a pasta inteira, com `.git` e `_entrega`).
2. Numa conversa nova, **ligue a pasta `Lone`** e peça: *"leia docs/PASSAGEM-DE-CONTA.md e os documentos que ele indica
   e continue o Lone ERP"*.
3. O assistente começa com `git --no-optional-locks log --oneline -3` (esperado `c75dce3` no topo, ou um commit de
   documentação acima dele) e `git --no-optional-locks status` (esperado só os três documentos da seção 1 e `_entrega/`),
   e então segue a seção 2.
