# Navegação do Lone — motor global de navegação contextual

Arquitetura transversal de navegação (fases 1 a 7). Este documento registra a auditoria, as decisões e o andamento.
Situação: **Fase 1 (fundamento) VALIDADA E ENCERRADA em 30/09/2026** (build 0/0; 1761/1761 testes, 21 de navegação;
`validar.ps1` OK; testes manuais no Windows; relatório em `_entrega/navegacao-fase1/VALIDACAO-FASE1-NAVEGACAO.md`).
Sem commit. Fase 2 **não iniciada**.

## 1. Auditoria (30/09/2026)

| Área | O que existe |
|---|---|
| Shell | `AppShell` com ~40 rotas absolutas (`//pessoas`, `//etiquetas`…), `FlyoutItem` ocultos + menu próprio (`FlyoutContent`). Cada tela é uma **área**: não há pilha do Shell por baixo — portanto **não havia nenhum "voltar" entre telas**. **Correção (validação 30/09):** ao trocar de módulo o Shell **recria** a tela (Pessoas volta sem busca/filtro e recarrega); a auditoria inicial supôs cache e estava errada. Dentro da mesma tela (lista ↔ ficha) o estado se mantém. |
| Navegação global | `MenuViewModel.Navegar` → `GoToAsync("//rota")`; Favoritos e Recentes **de telas** (guardados na API por usuário); busca no menu. |
| Entrada | `INavegacao`/`NavegacaoMaui`: login → troca de senha → empresa → sistema (troca a página da janela; cada entrada cria um `AppShell` novo). Único modal: Trocar senha. |
| Links entre telas | `AberturaDePessoa` / `AberturaDeOperacaoTerritorial` (pedido pendente + ir à rota; a tela retira ao aparecer). 7 `Shell.Current.GoToAsync` espalhados em code-behind (`AbrirTela`, `Navegar`, configurações). |
| Lista/ficha | `CadastroViewModelBase` (29 telas): lista + ficha **dentro da mesma página** (`Editando`), `IMestreDetalhe` + `LayoutMestreDetalhe` (lado a lado no computador; um painel por vez abaixo de 760). Pessoas: lista **ou** ficha, abas internas (`SecaoSelecionada`). |
| Voltar do sistema | `OnBackButtonPressed` por página (Android): só fecha a ficha no modo compacto. |
| Alterações não salvas | `PodePerderAlteracoesAsync` (diálogo "Descartar alterações" / "Continuar editando") + guarda do Shell em `OnNavigating`. |
| Diálogos | `IDialogos` (confirmar, perguntar, escolher). |

**Conclusão:** sem bloqueio arquitetural. O ponto decisivo: no Lone a maior parte da navegação (lista → ficha → outra
ficha) acontece **dentro de uma tela**, não entre rotas. Um "back" de páginas (`PopAsync`) não serviria: o histórico
precisa enxergar **tela + registro**. Nada foi criado em paralelo ao que existia: o menu, as aberturas e a proteção de
alterações passaram a conversar com um motor único.

## 2. Arquitetura

```
             LONE
               │
   ┌───────────┼───────────────┐
 MENU        VOLTAR           (Fase 2: BREADCRUMB)
 muda de área  histórico real   hierarquia
   │           │
   └────► GerenciadorNavegacao ◄──── links internos, "Abrir", lone://, (Fase 7: busca)
               │   HistoricoNavegacao (pura, testável)
               │   RegistrosRecentes (sessão)
        ┌──────┴───────┐
 IPlataformaNavegacao   ITelaNavegavel
 (NavegacaoShell)       (CadastroViewModelBase → 29 telas)
```

- **`LocalNavegacao`** = rota + `ReferenciaRegistro` (tipo, Id, nome de exibição, novo). Abas, filtros e rolagem não são
  navegação (estado interno). Endereço interno `lone://pessoas/pessoa/{id}` (nunca leva o nome).
- **`HistoricoNavegacao`** — "de onde eu vim": mesmo lugar não empilha (duplo clique); registro novo gravado continua o
  mesmo passo; outro registro escolhido **na lista** substitui (navegação entre registros ≠ histórico); link que chega à
  tela e abre a ficha é um passo só; chegar ao lugar anterior por qualquer caminho (Fechar, voltar do celular, Voltar)
  desempilha; limite de 50.
- **`GerenciadorNavegacao`** — único caminho para ir e voltar: `IrParaTelaAsync` (menu: a tela como estava),
  `AbrirAsync` (tela + registro: links), `AbrirEnderecoAsync` (lone://), `VoltarAsync`. O histórico é montado pelo que
  **de fato** aconteceu (a tela informa o que abriu), não pelo que foi pedido. Uma navegação por vez (duplo clique não
  duplica). Não guarda telas: consulta a plataforma na hora (tela trocada/destruída nunca recebe chamada atrasada; tela
  em segundo plano não mexe no histórico). Permissões: não abre tela sem permissão; o Voltar pula as que deixaram de ser
  permitidas (usa as permissões do menu — nenhum sistema paralelo). Limpo a cada Shell novo (entrada, saída, empresa).
- **`ITelaNavegavel`** (implementado pela base dos cadastros): `RegistroAberto` + `IrParaRegistroAsync` (abre por Id,
  fecha, novo) — sempre pela proteção de alterações não salvas que já existia.
- **`NavegacaoShell`** (App): adaptador do Shell. **`AtalhosNavegacao`** (Windows): Alt+←, tecla "Voltar" e botão
  lateral do mouse.

## 3. Fase 1 — o que foi feito

- Motor (`Lone.Cliente/Navegacao`: `LocalNavegacao.cs`, `HistoricoNavegacao.cs`, `GerenciadorNavegacao.cs`), registrado
  na injeção de dependência (`GerenciadorNavegacao.Padrao`).
- Base dos cadastros: informa abrir/fechar/gravar/novo; abre registro por Id; `IdDoItem`/`IdDaFicha`/`TituloDaFicha`
  por convenção (Id, Titulo/Nome) com sobrescrita onde precisa (Operações territoriais: `Item.Id`).
- Pessoas (piloto): tipo `pessoa`, abre qualquer pessoa por Id; ação **Abrir** do toast de relacionamento é link pelo
  motor; "Abrir ficha" vindo de outra tela vira um passo só.
- Shell: menu pelo motor (origem Menu); chegada a cada tela entra no histórico; **botão ← na barra de título** (fixo,
  fora da rolagem, habilitado só com para onde voltar, dica "Voltar para João da Silva (Alt+←)"); voltar do celular pelo
  mesmo Voltar (menu aberto fecha primeiro). Os 7 `GoToAsync` espalhados passaram ao motor (origem Link).
- Menu: `PodeAbrir(rota)` e `TituloDaRota(rota)` para o motor.
- Testes: `NavegacaoTests` (19 casos, incluindo integração real com Pessoas e alterações não salvas).

## 4. Limitações conhecidas (Fase 1)
- Sem breadcrumb/cabeçalho contextual (Fase 2). No celular não há barra de título: o Voltar é o do sistema.
- "Salvar e sair" ainda não é oferecido: o diálogo atual tem Descartar/Continuar editando (a decisão passa pelo mesmo
  ponto, `PodePerderAlteracoesAsync`, pronto para a terceira opção na fase de diálogos).
- `AberturaDePessoa`/`AberturaDeOperacaoTerritorial` continuam como estão (compatibilidade); candidatas a virar
  `AbrirAsync` na Fase 2. Operações territoriais abertas pelas divergências ainda empilham "tela + ficha" em dois passos.
- Mudança de permissões durante a sessão só reflete no botão na próxima navegação.
- Estado de lista (filtros, busca, rolagem) é mantido **dentro da tela** (lista ↔ ficha), mas **se perde ao trocar de
  módulo** (o Shell recria a tela). A restauração explícita (`EntradaNavegacao.Estado`) é a Fase 3 — agora com esse
  motivo concreto.
- Voltar para uma ficha de outro módulo: a tela recriada carrega primeiro e depois abre a ficha (espera limitada a 15 s).

## 5. Próximas fases
2. Cabeçalho contextual + breadcrumb (hierarquia ≠ histórico) + título contextual, responsivo.
3. Preservação explícita de contexto (`Estado` por entrada: filtros, busca, ordenação, seleção, rolagem, aba).
4. Lista/detalhe — piloto Pessoas no computador.
5. Anterior/Próximo entre registros (`1 de 184`), começando por Pessoas.
6. Recentes/Favoritos de **registros** (já coletados em memória: `RegistrosRecentes`).
7. Busca global (Ctrl+K) e endereço `lone://` pelo mesmo `AbrirAsync`.

## 6. Encerramento da Fase 1 (30/09/2026)
- Status: **validada e encerrada** (aprovada pelo revisor após a validação no Windows).
- Correções feitas na validação: espera da primeira carga antes de abrir registro pedido pela navegação; fusão de
  entradas iguais seguidas no histórico; Voltar com menos de 400 ms entre pedidos conta como um (duplo clique).
- Pendências registradas para fases futuras (não corrigir antes da fase própria):
  - **Fase 3:** pesquisa, filtro e rolagem se perdem ao trocar de módulo (o Shell recria a tela).
  - **Validação posterior:** Android em aparelho/emulador (voltar do sistema) quando houver ambiente.
  - Botão lateral "voltar" do mouse: validar à mão (a automação não gera esse botão).
  - "Salvar e sair" no diálogo de alterações não salvas (fase de diálogos).
  - `AberturaDePessoa`/`AberturaDeOperacaoTerritorial` → `AbrirAsync` (Fase 2); Operações territoriais abertas pelas
    divergências ainda registram tela e ficha em dois passos.
