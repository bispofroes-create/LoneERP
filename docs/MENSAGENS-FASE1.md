# Mensagens da interface — Fase 1 (camada global e toast de sucesso)

Registro da implementação da Fase 1 da arquitetura global de feedback. A auditoria anterior (≈210 chamadas de mensagem) não
é substituída por este documento; ele registra só o que a Fase 1 fez. Situação (30/09/2026): **build sem erros e sem avisos; 1731/1731 testes aprovados; testes manuais A, C, D e F
feitos (resultado no fim); sem commit.**

## Arquitetura

```
Tela (ViewModel) ── Mostrar(texto, tipo[, ação, contexto])      ← o ponto de entrada de sempre (ViewModelBase)
        │
        ▼
ServicoMensagens.Classificar(tipo)                               ← quem decide a apresentação é o serviço, não a tela
        ├─ Sucesso → ApresentacaoMensagem.Toast → ServicoMensagens.Publicar → MensagemUsuario
        │                                                           │  (duração, repetição, fila, prioridade, ação)
        │                                                           ▼
        │                               CamadaMensagens (no modelo de TODA página, fora de qualquer rolagem)
        └─ Erro / Aviso / Informação → barra da tela, exatamente como antes (inclui MensagemMostrada)
```

| Peça | Arquivo | Papel |
|---|---|---|
| `MensagemUsuario`, `PrioridadeMensagem`, `ApresentacaoMensagem`, `AcaoMensagem` | `src/Lone.Cliente/Mensagens/MensagemUsuario.cs` | Modelo só de memória: texto, tipo (= severidade, o `TipoMensagem` que já existia), apresentação, prioridade, duração, ação opcional (uma só), contexto opcional, contagem de repetidas. |
| `ServicoMensagens` | `src/Lone.Cliente/Mensagens/ServicoMensagens.cs` | Serviço único do aplicativo (singleton + `ServicoMensagens.Padrao`). Classifica, calcula a duração, deduplica, limita a 3 visíveis + fila de 10 com prioridade, pausa/retoma o tempo, executa a ação, limpa na troca de contexto. `TimeProvider` injetável (testes com relógio controlado). |
| `PosicaoToast` | `src/Lone.Cliente/Mensagens/PosicaoToast.cs` | Regra pura de posição (testável sem MAUI). |
| `CamadaMensagens` | `src/Lone.App/Controles/CamadaMensagens.cs` | Desenha o que o serviço manda; assina ao carregar e cancela ao descarregar (sem referência presa a páginas fechadas). |
| `PaginaComMensagens` | `src/Lone.App/Resources/Styles/Estilos.xaml` | `ControlTemplate` aplicado pelo estilo implícito de `ContentPage`: `Grid { ContentPresenter; CamadaMensagens }`. |
| `ViewModelBase.Mostrar` | `src/Lone.Cliente/ViewModels/ViewModelBase.cs` | Continua sendo o ponto de entrada; sucesso passa ao serviço, o resto segue para a barra. |

### Como o toast é hospedado globalmente
O estilo implícito de `ContentPage` (que já existia) ganhou `ControlTemplate = PaginaComMensagens`. O conteúdo de cada página
continua sendo o mesmo (vai para o `ContentPresenter`), então largura, altura, `ScrollView`, grades e o `LayoutMestreDetalhe`
(que mede pela largura da **página**) não mudam. A camada fica por cima, alinhada embaixo, e só ocupa o espaço dos toasts
(sem toast, fica invisível) — fora dela a página continua recebendo toque, clique e teclado. Não há página a alterar uma a uma.

Login, Primeiro acesso, Escolher empresa, Trocar senha e Carregando também são `ContentPage` e recebem a camada pelo mesmo
estilo; nenhum mecanismo especial foi necessário. Na troca de contexto (entrada, saída, outra empresa — `NavegacaoMaui`) o
serviço é esvaziado: toasts e ações do contexto anterior não seguem.

### Posição
- Computador: canto inferior direito, 400 de largura, margem 24 à direita e 80 embaixo (área mais estreita que 448: margens
  de 16 e a largura que couber, para o texto não quebrar palavras no meio — ajuste após o teste manual F) — acima
  da barra fixa de ações das fichas (Salvar/Descartar/Fechar e o motivo da alteração continuam livres).
- Celular: embaixo, largura toda, margem 16 dos lados, 80 embaixo.

### Regras do serviço
- **Duração** (só no serviço): 2 s + 70 ms por caractere, entre 4 s e 12 s; com ação, no mínimo 8 s. Ponteiro sobre o toast
  ou foco do teclado num botão dele seguram o tempo; ao sair, pelo menos mais 2 s.
- **Repetição**: mesmo tipo + texto + contexto enquanto visível ou na fila não empilha — soma (“×3”) e recomeça o tempo.
  Depois de sumir, é mensagem nova. O leitor de tela anuncia de novo (a ação aconteceu de novo).
- **Limite**: 3 visíveis; as demais esperam (fila de até 10, a de maior prioridade primeiro). Prioridade maior que a menor
  visível toma o lugar dela (que volta a esperar). Fila cheia: sai a de menor prioridade mais antiga.
- **Ação**: no máximo uma, só em toast (`Mostrar` com ação em erro/aviso lança exceção). Ao executar, o toast sai; falha na
  ação não derruba o aplicativo.
- **Acessibilidade**: ícone + texto (nunca só cor), descrição semântica “Sucesso: …”, anúncio via `SemanticScreenReader`
  (uma vez, no App), botões com descrição e dica, contraste ≥ 4,5:1 nos dois temas, sem animação.
- Não grava nada: não é notificação persistente, não é central de notificações, não vai para a auditoria.

## Como as mensagens existentes chegaram ao novo mecanismo
Nenhuma chamada foi reescrita para migrar: as **84 chamadas de sucesso** (29 arquivos) passam por `ViewModelBase.Mostrar`, que
agora as encaminha ao serviço. As de erro (45 + `MostrarErro`), aviso (68) e informação (8) seguem na barra, sem mudança —
inclusive os avisos “salve ou descarte” (continuam aviso na barra; o diálogo de decisão é de fase própria) e o paliativo
`MensagemMostrada` de Operações territoriais (agora disparado só por erro/aviso/informação, que é o que ainda fica na barra).

Mudanças pontuais do piloto (Pessoas):
- Gravação da ficha: “Cadastro salvo.” → “Pessoa criada: Bruno” (nova) / “Pessoa salva: Bruno” (existente), com o nome do
  cabeçalho da ficha (abreviado com “…” acima de 40 caracteres) e contexto = a pessoa. Com avisos de duplicidade continua
  **aviso na barra**.
- Padrão de texto (`TextosMensagem.ComNome`), decidido com base no SAP Fiori (“[objeto] [ação]”, com o nome quando ele
  identifica o registro), Salesforce e Morningstar: “[objeto] [ação]: [nome]” — evita a concordância de gênero do nome,
  sem “com sucesso”. **Campos alterados não entram no toast**: quem mostra o que mudou é o Histórico; só consequências que
  o usuário não fez diretamente (ex.: vínculo anterior encerrado) justificam texto a mais. Demais telas adotam o padrão
  quando forem revisadas.
- Relacionamento registrado: toast com a ação **Abrir** (abre a ficha da outra pessoa; pergunta antes se houver alterações
  não salvas; se o usuário saiu de Pessoas, volta para a tela). Contexto = ficha de origem.
- O aviso flutuante próprio da lista de Pessoas (sucesso embaixo, no meio) foi retirado: era uma versão local do toast; agora
  é o global.
- Trocar senha pelo menu: “Senha alterada…” aparece no toast; a tela fica concluída (só “Fechar”).

## Testes automatizados
- `tests/Lone.Tests/Cliente/MensagensTests.cs` (novo): sucesso vira toast sem barra e sem rolagem (27.1); erro/aviso/informação
  continuam na barra e disparam `MensagemMostrada` (paliativo); ação só em toast; duração (curta, longa, teto, com ação);
  expiração; pausa/retomada; repetidas (visível e na fila), contexto (27.3); fila de 3, ordem, prioridade, limite da fila
  (27.4); ação executa uma vez, falha não derruba, sem ação não faz nada (27.5); toast sobrevive à troca de tela e é limpo na
  troca de contexto (27.2); serviço único por padrão; posição computador/janela estreita/celular (27.6/27.7).
- `tests/Lone.Tests/Cliente/PessoasMensagensTests.cs` (novo): gravar pessoa nova no fim da ficha → toast “Pessoa criada”, 4 s,
  sem barra e sem voltar ao topo (27.8).
- Ajustados (o sucesso saiu da barra): `CamposEAlteracoesTests`, `PessoasListaTests`, `TrocarSenhaViewModelTests`,
  `UsuariosViewModelTests`, `ViewModelBaseTests`.
- Validação: `_entrega/mensagens-fase1/validar.ps1` (build + todos os testes; resultados em `resultados/`).

## Testes manuais (obrigatórios antes de expandir)
| Caso | Passos | Esperado |
|---|---|---|
| A — Pessoa | Abrir uma pessoa, rolar até o fim, alterar, Salvar | Toast “Pessoa salva” no canto inferior direito, sem rolar; barra de ações livre |
| B — Criação | Novo cadastro em Pessoas e em um cadastro auxiliar (ex.: Etiquetas) | Toast “Pessoa criada” / mensagem do cadastro |
| C — Ação | Registrar um relacionamento; clicar **Abrir**; repetir saindo para outra tela antes de clicar | Abre a ficha da outra pessoa (volta para Pessoas se preciso) |
| D — Mestre-detalhe | Salvar em Etiquetas/Profissões/Operações territoriais em janela larga e estreita | Layout íntegro; erro de Operações ainda leva a vista à mensagem |
| E — Shell/login | Login com senha errada (erro na tela, sem toast); trocar senha pelo menu; trocar de empresa com toast visível | Toast da senha aparece; troca de empresa limpa os toasts |
| F — Janela/celular | Estreitar a janela ao mínimo; (se possível) Android | Toast cabe na largura; no celular, embaixo na largura toda |
| G — Teclado/leitor | Tab até “Abrir”/“✕”; Narrador ligado | Tempo para enquanto focado; Narrador lê “Sucesso: …” |

## Limitações conhecidas
- Mensagens de sucesso longas (várias linhas, ex.: resumo de transferência sem erros) também viram toast; o teto de 12 s e a
  pausa ao passar o ponteiro compensam. Se alguma precisar ficar na tela, é candidata à futura faixa/resultado parcial.
- Operações territoriais: sucesso da execução com “A data mudou: simule de novo.” agora é toast; revisar no teste manual D.
- Não há teste automatizado da camada MAUI em si (o projeto de testes não referencia o app); a posição é testada pela regra
  pura `PosicaoToast` e o resto pelos testes manuais.

## Não feito nesta fase (de propósito)
Erro, aviso, validação e informação em toast; erro por campo; diálogo “salve ou descarte” (71 casos); remoção do paliativo
`MensagemMostrada`; progresso; notificações persistentes/central; mudança de textos fora do piloto; qualquer mudança de
banco, migration, API ou das fases territoriais.

## Próximo passo recomendado
Depois da validação e dos testes manuais: Fase 2 — faixa de erro/aviso fora da rolagem (na mesma camada), o que permite
aposentar `MensagemMostrada`; em seguida, o diálogo de decisão “salve ou descarte”.

## Resultado dos testes manuais (30/09/2026, Windows, feitos pelo Claude na máquina do desenvolvedor)
| Caso | Resultado |
|---|---|
| A — Pessoa (Bruno, fim da ficha, Observações, Salvar) | ✅ "Pessoa salva" no canto inferior direito, acima da barra Salvar/Descartar; a ficha ficou no fim, sem subir; sumiu sozinho em ~4 s. Observação desfeita depois (novo toast "Pessoa salva"). |
| C — Ação (relacionamento Bruno "Parceiro de" Diego) | ✅ Toast com **Abrir**; o clique abriu a ficha do Diego. Relacionamento de teste desativado em seguida (toast "Relacionamento desativado (continua no histórico).") |
| D — Mestre-detalhe (Etiquetas: descrição de "Banco") | ✅ "Alterações salvas." no toast; lista e detalhe intactos. Descrição revertida. Operações territoriais abre com o layout íntegro. |
| F — Janela no mínimo (Etiquetas, modo compacto) | ✅ com ressalva: o toast cabe na área da página, mas em largura mínima a palavra quebra no meio ("Alteraç/ões"). Ajuste menor proposto: margens de 16 quando a área for estreita. |
| B, E, G, erro em Operações territoriais | Não executados nesta rodada (B coberto pelo teste automático de pessoa nova; E/G pedem login/troca de senha/Narrador). |

### Segunda rodada (após “Pessoa salva: Bruno” e margens estreitas)
| Caso | Resultado |
|---|---|
| A — Pessoa com nome | ✅ “Pessoa salva: Bruno” no canto inferior direito, ficha parada no fim. Observação de teste desfeita. |
| F — Janela no mínimo | ✅ O texto quebra por palavra (“Pessoa / salva: / Bruno”), sem cortar palavras. |

Observação para fase futura (não é da Fase 1): clicar em Salvar sem nenhuma alteração ainda grava e confirma
“Pessoa salva”. Pela regra “não gerar feedback redundante”, o ideal é o Salvar ficar desabilitado sem alterações (ou não
gravar e não confirmar). Na largura mínima da janela a ficha de Pessoas fica muito espremida porque o menu lateral não
recolhe — comportamento anterior, fora do escopo das mensagens.

## Complemento (30/09/2026): Salvar só com alterações e menu lateral recolhível
Decisões aprovadas depois dos testes manuais (regra “não gerar feedback redundante”):

- **Salvar só com o que gravar** (base de todos os cadastros, `CadastroViewModelBase`): item existente sem alteração →
  Salvar e Descartar desabilitados; a barra de ações mostra o estado em texto (“Sem alterações” / “Alterações não
  salvas” / “Novo cadastro, ainda não salvo”) — padrão de desktop (botão “Aplicar” do Windows) somado ao indicador de
  estado do Dynamics 365; o texto compensa a ressalva de acessibilidade de botões desabilitados (GitHub Primer). Item
  novo: Salvar sempre disponível (a gravação mostra o que falta). Fechar sem alterações fecha em silêncio (nenhum ERP
  maduro anuncia “fechado sem alterações”); com alterações, pergunta, como antes.
- Como funciona: `ObservadorFicha` acompanha a ficha em qualquer nível (campos, itens de listas, itens novos) sem
  executar cálculos (percorre campos, não propriedades) e sem entrar em ViewModels de tela; a decisão continua sendo a
  comparação com a versão gravada — mudar e voltar ao valor original não conta. Solta tudo ao fechar a ficha. Cada
  cadastro informa a ficha em `FichaObservada` (28 cadastros; Operações territoriais usa as propriedades da própria tela).
  26 telas ligadas (`IsEnabled="{Binding PodeSalvarAgora}"` + `EstadoFicha`); Parâmetros comerciais, Parâmetros
  territoriais e Trocar senha não são cadastros lista+ficha e ficam como estavam.
- **Menu lateral** (Windows): fixo com a janela a partir de 1008 de largura (limite do NavigationView do Fluent);
  abaixo, recolhe e aparece o botão ☰ na barra de título (abre por cima e fecha ao escolher a tela). Regra em
  `MenuLateral.Recolhido`.
- Testes: `SalvarSoComAlteracoesTests` (nova, gravada sem alteração, alterar/voltar, nível interno e item novo em lista,
  ficha fechada solta a inscrição, limites do menu).
- Achados no teste na tela e corrigidos: (1) no Windows o `SizeChanged` do próprio Shell não chega — o menu passou a
  medir pela janela (`Window.SizeChanged`, ligado ao abrir e solto ao sair do sistema); (2) o painel nativo do WinUI
  (NavigationView) mostrava o seu próprio botão de menu no canto esquerdo, sobreposto à barra de título — escondido em
  `Plataforma/AjusteMenuLateral` (fica só o ☰ do Lone).
- Resultado (1740/1740 testes antes dos dois ajustes acima, que são só do app Windows; build do Visual Studio ok):
  | Caso | Resultado |
  |---|---|
  | Etiquetas “Banco” aberta sem mexer | ✅ Salvar e Descartar desabilitados, “Sem alterações” |
  | Digitar 1 letra / apagar | ✅ acende na hora (“Alterações não salvas”) / apaga de novo (“Sem alterações”) |
  | Pessoas (Bruno) aberto | ✅ Salvar desabilitado, “Sem alterações” |
  | Janela larga → estreita → larga | ✅ menu fixo → recolhido com ☰ (abre por cima, fecha ao escolher Etiquetas) → fixo de novo, sem ☰ |
