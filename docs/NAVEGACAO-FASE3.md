# Lone — Navegação, Fase 3: preservação de contexto
## Auditoria, plano, implementação e validação — status: **implementada, aguardando revisão** (sem commit)

Pré-requisito: Fase 1 encerrada (commit `a354b2e`). Cópia de segurança do repositório gerada (`.bundle`).

### Objetivo
"Eu não perdi o que estava fazendo": ao voltar a uma tela (pelo ← ou pelo menu), ela reaparece como o usuário a deixou —
pesquisa, filtros, ordenação, página, seleção, ficha aberta e aba, e a posição da lista.

## 1. Auditoria (o que acontece hoje)
- **Causa:** todas as páginas e ViewModels são `Transient` (42 páginas, ~40 ViewModels) e o Shell **recria** a página ao
  trocar de módulo (achado da validação da Fase 1). Tudo nasce zerado.
- **Já sobrevive** (gravado na API por usuário): colunas e densidade da grade, visões salvas, favoritos/recentes do menu.
- **Se perde hoje** (Pessoas, piloto): pesquisa (`Busca`), aba de visão/filtro rápido (`Abas`), painel de filtros
  (`Filtros`), ordenação (`Grade.Ordenacao`), página (`_pagina`), prévia/seleção (`Previa`, `Selecionado`), ficha aberta e
  aba da ficha (`Formulario`, `SecaoSelecionada`), rolagem (`RolagemLinhas` vertical, `RolagemColunas` horizontal, rolagem
  da ficha).
- **Nas demais 28 telas de cadastro:** pesquisa, item selecionado/ficha aberta, rolagem da lista.
- **Dentro da mesma tela** (lista ↔ ficha) nada se perde: é o mesmo ViewModel.

## 2. Duas formas de resolver

| | A. Manter as telas vivas | B. Guardar o estado de navegação (recomendada) |
|---|---|---|
| Como | ViewModel de cada tela passa a viver enquanto o Shell existir; a página nova reusa o mesmo ViewModel | Ao sair, a tela entrega um "retrato" do estado de navegação; ao voltar (tela nova), o retrato é reaplicado |
| Preserva | Tudo, "de graça" (inclusive rolagem só se a página também for mantida) | O que cada tela declara (base genérica + extras de Pessoas) |
| Dados | Lista antiga na volta (precisa recarregar à mão) | Lista **relida do servidor** com os mesmos filtros: dados frescos |
| Alterações não salvas | Continuariam vivas depois de o usuário aceitar descartá-las (precisa tratar) | Não são guardadas: a ficha reabre como está no banco (coerente com a guarda atual) |
| Riscos | 6 páginas assinam eventos do ViewModel no construtor: com ViewModel de vida longa, **vazamento** e handlers duplicados; ~40 telas a revisar | Tempo da restauração (a tela precisa carregar antes) — já resolvido na Fase 1 (espera da primeira carga) |
| Especificação | Mistura estado de navegação com dados de negócio | Exatamente a separação pedida ("Navigation State ≠ Business Data", só em memória) |

**Recomendação: B.** É menor, controlada, testável e segue a separação pedida; A troca um problema visível por riscos
invisíveis (vazamento de memória, dados velhos, edições descartadas que "voltam").

## 3. Desenho proposto (B)
- **`IEstadoNavegavel`** (em `Lone.Cliente/Navegacao`), implementado pela tela:
  `object? CapturarEstado()` e `Task RestaurarEstadoAsync(object estado)`. O estado é um `record` imutável, **só em
  memória**, por tela.
- **Base dos cadastros (29 telas de uma vez):** estado genérico = pesquisa + registro aberto (reaberto pelo motor, como na
  Fase 1). Pessoas acrescenta: aba de visão/filtro rápido, filtros do painel, ordenação, página, prévia e aba da ficha.
- **Motor (`GerenciadorNavegacao`):** guarda o último estado de cada tela ao sair dela (antes de trocar de rota, pelo
  menu, link ou ←) e o reaplica quando a tela nova aparece. Também grava o estado na entrada do histórico
  (`EntradaNavegacao.Estado`, previsto na Fase 1), para o ← restaurar o momento exato. Limpo a cada Shell novo
  (saída, outra empresa), como o histórico.
- **Rolagem sem gambiarra por página:** um comportamento central opt-in (`Rolagem.Preservar="lista"` no XAML) em
  `ScrollView`/`CollectionView`: registra a posição no estado da tela e a reaplica depois que os dados carregam (com
  limite de tentativas). Só listas e fichas longas marcadas — não em toda rolagem.
- **Ordem da restauração:** tela nova → primeira carga → aplica pesquisa/filtros/ordenação/página → relê a lista →
  reabre a ficha (se houver) → aplica a rolagem. Registro que sumiu ou ficou sem permissão: fica na lista, sem erro.
- **LGPD:** o estado pode conter o que o usuário digitou na pesquisa (ex.: um CPF): fica só em memória, nunca em disco,
  endereço `lone://`, dica ou log; some ao sair do sistema.

## 4. Testes previstos
- Captura/restauração genérica (pesquisa + ficha) na base; Pessoas: pesquisa, visão, filtros, ordenação, página, prévia,
  aba da ficha.
- Motor: sair e voltar pelo menu e pelo ←; estado por tela; limpeza ao trocar de Shell; registro excluído; sem permissão.
- Alterações não salvas **não** voltam depois de descartadas.
- Manual (roteiro da especificação, item 42): Pessoas → pesquisa → filtro → descer a lista → abrir → aba → outro módulo
  → voltar (← e menu) → tudo como estava, inclusive a posição da lista.

## 5. Fora do escopo
Breadcrumb (Fase 2), Anterior/Próximo (Fase 5), recentes/favoritos de registros (Fase 6), busca global (Fase 7),
persistência do estado entre sessões (só se houver decisão), banco/migration/API.

## 6. Decisões pedidas
1. Aprovar a forma **B** (estado de navegação) em vez de A.
2. Restaurar **também ao voltar pelo menu** (não só pelo ←)? Recomendo sim: é o mesmo "não perdi o lugar".
3. Piloto completo em **Pessoas** + estado genérico (pesquisa e ficha) nas outras 28 telas na mesma fase?

---

## 7. Decisões aprovadas (30/09/2026) — incorporadas antes do código
1. **Forma B** aprovada: estado de navegação separado do estado de negócio, só em memória (sem tabela, migration, API,
   persistência ou alteração de dado de negócio). `IEstadoNavegavel` com captura/restauração por tela.
2. **Restaurar também pelo menu**: ← e Menu → Pessoas significam "voltar ao meu trabalho naquela tela".
3. **Pessoas = piloto completo** (pesquisa, abas de visão/filtro rápido, filtros, ordenação, página, seleção/prévia,
   ficha, aba da ficha, rolagem da lista e da ficha). Demais cadastros: estado genérico (pesquisa + ficha) e o mecanismo
   central. A infraestrutura é reutilizável — nada exclusivo de Pessoas no motor.

### 7.1 Estado atual da tela ≠ estado da entrada do histórico
- **Estado atual da tela** ("como estava quando o usuário saiu dela"): um por tela (rota), guardado pelo motor e
  sobrescrito a cada saída. Usado quando se volta à tela **pelo menu** ou por navegação posterior.
- **Estado da entrada do histórico** ("como estava aquele ponto exato"): um retrato **por entrada**
  (`EntradaNavegacao.Estado`), gravado quando o usuário sai daquela entrada. Usado pelo **←**.
- Mesma infraestrutura, dois guardados. Uma navegação posterior atualiza o estado atual da tela, mas **nunca** o retrato
  de uma entrada anterior do histórico.
- Só se restaura numa **tela recriada**: se a instância que está aparecendo é a mesma que foi deixada, ela já está como
  estava (restaurar seria redundante).

### 7.2 Estado inválido ou obsoleto — política de fallback
A restauração é tolerante; nunca é erro de navegação porque o mundo mudou:
| Situação | Comportamento |
|---|---|
| Página que deixou de existir | última página válida (regra já existente na listagem) |
| Registro excluído / sem permissão / não abre naquele contexto | não abre a ficha; fica na lista, **sem mensagem de erro** |
| Filtro inválido (campo sem permissão, opção que não existe) | ignora só aquele elemento; o resto é aplicado |
| Aba de visão/filtro rápido que não existe mais | "Todos" |
| Aba da ficha que não existe mais | aba padrão da ficha |
| Visão salva removida | sem visão marcada; filtros restaurados continuam |
| Prévia de pessoa fora da página atual | sem prévia |
| Estado parcialmente incompatível | restaura tudo o que ainda for válido |
| Estado completamente inválido | tela no estado padrão |

### 7.3 Alterações não salvas e descarte (obrigatória)
O estado guarda apenas **qual** registro estava aberto, nunca o conteúdo editado: a ficha sempre reabre com o que está
gravado. Além disso, ao **aceitar descartar** para sair da tela (guarda do Shell), a ficha é de fato descartada
(relida do servidor ou fechada) — o descarte nunca "volta" depois, nem se a tela não for recriada.

### 7.4 Restauração idempotente
Aplicar o mesmo estado duas vezes dá o mesmo resultado: só se muda o que difere (pesquisa, aba, filtros, ordenação,
página); no máximo **uma** releitura da lista; ficha só é aberta se for outra; rolagem vai para a mesma posição. A
restauração acontece dentro de uma navegação do motor: **não cria passos no histórico** nem dispara eventos duplicados.

### 7.5 Rolagem
Comportamento central **opt-in** (`Rolagem.Preservar="lista"` / `"ficha"`), só em componentes marcados. Registra a
posição enquanto o usuário rola e a reaplica quando o conteúdo já tem altura para recebê-la (tentativas limitadas).
Preparado para listas, fichas longas, grades e mestre-detalhe.

### 7.6 Ordem da restauração
1. tela criada → 2. primeira carga → 3. pesquisa/aba/filtros/ordenação/página → 4. uma releitura da lista →
5. ficha → 6. aba da ficha → 7. seleção/prévia → 8. rolagem. Princípio: nenhuma posição visual antes de a lista/ficha
estar pronta para recebê-la.

### 7.7 LGPD
Estado só em memória; o texto da pesquisa (que pode conter dado pessoal) nunca vai para banco, arquivo, endereço
`lone://`, log, dica ou auditoria. Tudo some a cada Shell novo (saída, outra empresa), como o histórico.

### 7.8 Testes obrigatórios (além dos do item 4)
Estado da tela ≠ estado da entrada; retorno pelo ←; retorno pelo menu; estado por tela; estado por entrada; estado
inválido; registro excluído; registro sem permissão; página que deixou de existir; filtro inválido; alteração
descartada não reaparece; restauração idempotente; restauração não cria entrada no histórico; restauração de rolagem;
limpeza ao criar novo Shell; Pessoas com o conjunto completo; demais cadastros com o estado genérico. Teste manual
completo (roteiro do item 4).

### 7.9 Regras de implementação
Sem banco, migration, API ou mudança de regra de negócio; reutilizar o `GerenciadorNavegacao` e a proteção existente de
alterações não salvas; mecanismo central, sem soluções por tela; qualquer conflito arquitetural → parar e perguntar.
**Sem commit/push** até a revisão da validação final.

## 8. Implementação (30/09/2026)
- `Lone.Cliente/Navegacao/EstadoNavegacao.cs`: `EstadoTela` (pesquisa, registro aberto, rolagens), `IEstadoNavegavel`
  (capturar/restaurar) e `MemoriaRolagem` (posições por chave; posição restaurada fica pendente até a rolagem conseguir
  recebê-la).
- `GerenciadorNavegacao`: ao sair de uma tela guarda o **estado atual da tela** (por rota, com a instância deixada) e o
  **retrato da entrada atual** do histórico (`HistoricoNavegacao.DefinirEstadoDaAtual` — só a entrada atual, nunca uma
  anterior). Ao chegar numa tela **recriada**: ← usa o retrato da entrada (sem retrato, o último da tela); menu e links usam
  o último estado da tela (o registro pedido prevalece). Restauração dentro da operação do motor (nenhum passo novo no
  histórico); tela que falha ao se descrever não impede a navegação. `Limpar` (Shell novo / desconectar) apaga tudo.
- `CadastroViewModelBase`: implementa `IEstadoNavegavel` para todos os cadastros (pesquisa + ficha + rolagens), na ordem
  aprovada; registro que não abre mais fica de fora sem mensagem de erro e não é tentado de novo na mesma tela; sair com
  alterações e escolher "Descartar" relê a ficha do servidor antes de deixar a tela (`PodeSairAsync`).
- Pessoas (`EstadoPessoas` + overrides): aba de filtro rápido/visão, condições do painel, visão marcada, ordenação,
  página, prévia e aba da ficha. Uma releitura só; filtros e grade não disparam releituras nem gravam preferência durante
  a restauração; comparação pelo resultado (idempotente mesmo com estado velho).
- `Lone.App/Controles/Rolagem.cs`: `c:Rolagem.Preservar="chave"` (opt-in) — em Pessoas, `lista` e `ficha`.
- Nada em banco, arquivo, endereço, log ou dica; sem migration, sem API, sem mudança de regra de negócio.

## 9. Validação (30/09/2026) — ver também a seção 10
- `validar.ps1`: build 0 erros/0 avisos; **1792/1792 testes** (Fase 1: 1761; +19 `NavegacaoEstadoTests`,
  +12 `EstadoNavegacaoCadastrosTests`).
- Manual no Windows: ver `_entrega/navegacao-fase3/VALIDACAO-FASE3-NAVEGACAO.md`.
- Ponto levado ao revisor: durante a restauração (primeira carga + releitura + ficha) o menu e o ← ficam ocupados.

## 10. Correção autorizada — opção B: pedido novo interrompe a restauração (30/09/2026)
- O motor fica ocupado só durante a **troca de tela**; enquanto a tela nova restaura o contexto (primeira carga,
  releitura, ficha), `Navegando` é falso: o menu e o ← atendem.
- Pedido novo durante a restauração (`InterromperRestauracao`): cancela a restauração (token passado a
  `IEstadoNavegavel.RestaurarEstadoAsync`); registra a chegada àquela tela como a operação faria ao terminar (é uma
  navegação real do usuário — sem a ficha que ainda não tinha aberto); mantém como estado da tela e retrato da entrada o
  que **ia** ser restaurado (nunca a tela pela metade; sem instância guardada, voltar a ela restaura de novo, por inteiro);
  libera o motor para o pedido novo. A operação interrompida, quando acorda, não registra nem muda nada (número da
  operação).
- Menu para a própria tela que está restaurando não interrompe (é ela chegando). Shell novo cancela sem registrar nada.
- Enquanto restaura, `PodeVoltar`/`DescricaoVoltar` já preveem a chegada (numa cópia do histórico): "Voltar para" a tela
  de onde o usuário veio.
- Base dos cadastros: cancelada, nenhum passo seguinte começa (primeira carga, releitura, ficha, detalhes, rolagem) e a
  lista que ainda estiver sendo lida é descartada ao chegar (`_versaoLista`). Uma leitura HTTP já em andamento não é
  abortada (as APIs deste caminho não recebem token), mas o resultado fica só na instância abandonada: fora da tela, sem
  efeito no histórico (a tela não é mais a atual) nem no estado guardado (não é recapturada).
- Menu lateral: o comando `IrCommand` (Fase 1) não aceitava um segundo toque enquanto a navegação anterior não
  terminasse (padrão do `AsyncRelayCommand`) — com a restauração dentro da navegação, travava o menu. Agora
  `AllowConcurrentExecutions = true`: quem decide é o motor (ignora durante a troca de tela; interrompe durante a
  restauração).
- Medição sem depurador (diagnóstico temporário, já removido): a primeira carga de Pessoas **ocupa a thread de
  interface ~4,6 s** em `AntesDeListarAsync` (também sem restauração nenhuma — primeira entrada na tela). Nesse intervalo o
  Windows só entrega o clique quando a thread libera; entregue, o motor interrompe em ~1 ms e a outra tela abre em
  ~0,4 s.
- Fora do escopo (registrado): otimizar a primeira carga de Pessoas — leituras em sequência **e** trabalho síncrono na
  thread de interface (~4,6 s) em `AntesDeListarAsync`.
