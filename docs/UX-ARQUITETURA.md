# Arquitetura de UX do Lone (redesenho do módulo Pessoas, 26/09/2026)

Decisão: **cada módulo tem as suas telas e as suas configurações**; a página "Configurações do sistema" fica só com o que é
transversal. O menu lateral é organizado por módulo, em **dois níveis** (padrão dos ERPs maduros: Protheus, Dynamics 365,
Bling/Omie): tocar no nome do módulo abre e fecha as telas dele.

```
[ Pesquisar no menu ]
  Início
FAVORITOS  ›            (só aparece com favoritos; ☆/★ em cada tela)
RECENTES   ›            (as 5 últimas telas abertas)
⌄ Pessoas
     Cadastro · Consulta avançada · ⚙ Configurações
› Organização           (Grupos empresariais · ⚙ Configurações)
› Metas                 (Painel · ⚙ Configurações)
  ⚙ Configurações do sistema
(futuro) Vendas / Compras / Estoque / Financeiro / Fiscal — cada um com "⚙ Configurações"
```

## Onde está

| Peça | Arquivo |
|---|---|
| Estrutura do menu (seções, itens, item ativo, permissões, busca, favoritos, recentes) | `Lone.Cliente/ViewModels/MenuLateral.cs` + `MenuViewModel` (`CriarSecoes`, `CriarCatalogo`, `Pesquisar` em `SistemaViewModels.cs`) |
| Favoritos e recentes guardados por usuário | API `api/v1/menu` (`MenuEndpoints`) → `MenuUsuarioAppService` → tabela `PreferenciasMenu`; cliente `MenuUsuarioApi` |
| Desenho do menu (FlyoutContent) e rotas | `Lone.App/AppShell.xaml(.cs)` — os `FlyoutItem` só registram rotas/permissões |
| Configurações por módulo (catálogo, grupos, permissões) | `Lone.Cliente/ViewModels/ConfiguracoesViewModel.cs` (`ModulosConfiguracao`) + `Views/ConfiguracoesPage` |
| Lista de Pessoas (atalhos, filtros, paginação, colunas, ⋯) | `PessoasViewModel` (seção "Lista") + `ListaPessoas.cs` + `Views/PessoasPage.xaml` |
| Design system (tokens e estilos) | `Resources/Styles/Cores.xaml` e `Estilos.xaml` (bloco "DESIGN SYSTEM DO LONE") |

**Para um módulo novo:** uma constante em `ModulosConfiguracao` (título/descrição), os itens no catálogo de
`ConfiguracoesViewModel`, uma seção em `MenuViewModel.CriarSecoes`, e um `FlyoutItem` `configuracoes-<modulo>` no AppShell.

**Menu em dois níveis (decisões do usuário, 26/09/2026):**
- Dentro do módulo, os itens não repetem o nome dele ("Cadastro", "Consulta avançada", "Painel", "⚙ Configurações"). O nome
  completo ("Cadastro de pessoas", "Configurações de Pessoas") fica em `ItemMenu.Descricao`: aparece em Favoritos, Recentes,
  na busca e no leitor de tela; o título da página continua completo.
- Módulos começam fechados; o da tela aberta abre sozinho (e o nome fica destacado se o usuário fechá-lo). Vários podem ficar
  abertos; o app lembra quais enquanto está aberto (nada é gravado; ao entrar de novo, só o da tela atual).
- **Busca no menu:** procura em todas as telas que o perfil pode abrir, inclusive os cadastros das Configurações (ex.:
  "Papéis", em "Pessoas › Configurações"), sem acento/maiúsculas e com todas as palavras; Enter abre a primeira.
- **Favoritos e recentes guardados no servidor, por usuário** (acompanham o usuário em qualquer aparelho). Até 20 favoritos
  (na ordem em que foram marcados); recentes = as 5 últimas telas abertas (Início não conta). Tela sem permissão não aparece,
  mas continua guardada. Tabela `PreferenciasMenu` (uma linha por usuário + rota, nunca apagada; fora da auditoria, como
  `UsuarioAcessos`).
- Setas: o caractere "›" gira 90° quando o grupo está aberto (sem depender de fonte de ícones).

**Aparência das páginas de Configurações (decisão do usuário, 26/09/2026):** cabeçalho novo (título e descrição do módulo),
mas os cadastros continuam em **cartões separados** (estilo `Cartao`, três por linha no computador, um no celular), agrupados
pelo título do grupo — como antes do redesenho. O painel com linhas foi descartado.

## Padrão: cadastros auxiliares oferecem, a ficha mostra o que é usado (decisão do usuário, 26/09/2026)

A ficha de uma entidade mostra **somente as opções efetivamente usadas** nela; as demais ficam num painel "+ Adicionar …"
com pesquisa (o mesmo padrão do painel "Filtros" da lista: abre na própria tela, sem popover novo). Primeira aplicação:
Papéis e Etiquetas na Identificação da pessoa (`PapeisDaFicha`, `EtiquetasFormulario`, `SecaoGeralView`).

- **Papéis** (relacionamento com período): cartão compacto com nome, descrição do cadastro (se houver), estado com texto e
  marca (● Ativo / ○ Inativo), período ("Desde…", "Último período: … → …"), interruptor e "Histórico ›" com os períodos
  gravados. Duas colunas no computador, uma no celular. O interruptor **só muda a ficha**: ao salvar vale a regra de
  sempre (desligar encerra o período, religar abre outro, nada é apagado); quem alterou e o motivo ficam na auditoria (aba
  Histórico) — não há tela de histórico por papel com dados que o período não tem. Aparecem os papéis ativos e os que a
  pessoa já teve (inclusive desativados no cadastro, sem poder religar); nunca atribuídos só no painel.
- **Etiquetas** (classificação livre, sem período): chips com as atribuídas, em ordem alfabética; "×" tira a associação ao
  salvar (a auditoria registra). Sem interruptor. O painel oferece as ativas que faltam e "Nova etiqueta".
- **Permissões na tela:** sem permissão de editar a pessoa, só consulta (sem painel, interruptor travado, sem ×). "Empresa
  do grupo" também exige `PESSOAS.EMPRESAS_DO_GRUPO` na tela.
- **Listas da tela atualizadas no lugar** (`ColecaoSincronizada`): nunca trocar a coleção inteira a cada mudança (causa do
  fechamento da ficha de PJ no Windows).

## Barra de título, conta e empresa (decisão do usuário, 26/09/2026)

- **Windows:** empresa e usuário ficam à direita da barra de título, junto de minimizar/restaurar/fechar
  (`Controles/BarraTituloSistema` como `TrailingContent` do `TitleBar`; instalada por `NavegacaoMaui` só enquanto o
  sistema está aberto). "⇄ Trocar empresa" aparece com mais de uma empresa. Tocar no usuário abre **Trocar de usuário**
  (encerra a sessão e volta ao login) / **Sair do Lone** (encerra a sessão e fecha o app).
- **Celular:** sem barra de título — os mesmos itens ficam no rodapé do menu.
- **Trocar senha:** Configurações do sistema › **Minha conta** (grupo de todos os usuários; por isso Configurações do
  sistema aparece para qualquer um; Usuários e Perfis continuam com as permissões deles). Rota especial
  `ModulosConfiguracao.RotaTrocarSenha` (abre por cima, não é tela do Shell; vale também na busca e nos favoritos).

## Design system

- Espaçamento: 4 · 8 · 12 · 16 · 24 · 32. Raios: 6 (controles, selos) e 8 (superfícies). Bordas `BordaSutil`; sem sombras.
- Tipografia: `TituloPagina` 24 · `SubtituloPagina` 14 · `SecaoTitulo`/`Subtitulo` 16 · texto 14 · `CabecalhoTabela` 12 · `SeloTexto` 12.
- Cores novas: `PrimariaHover`, `Selecao`, `SuperficieRealce`, `BordaSutil`, `MenuFundo/MenuTexto/MenuTextoSecundario/MenuItemAtivo/MenuDestaque`, `SeloNeutroFundo`.
- Componentes (estilos): `PainelConteudo` (superfície), `Separador`, `BotaoTerciario`, `BotaoPerigo`, `BotaoIcone` (⋯ ⚙ ‹ ›),
  `Selo`/`SeloTexto` (situação, tipo, papel), `Chip` (atalho de filtro). Estados nunca só por cor (sempre com texto/traço).
- Ícones: só caracteres de texto (⚙ ⋯ ‹ › ← +) — o app não tem fonte de ícones; trocar por uma fonte (ex.: Fluent/Material)
  é um passo futuro, sem mexer no layout.

## Pessoas

- Lista em tela cheia: cabeçalho ("Pessoas" + "+ Nova pessoa" + ⚙), pesquisa + "Filtros" (painel com papel, etiqueta,
  incluir inativos, município a corrigir, limpar), atalhos (Todos · PF · PJ · Clientes · Fornecedores · Ativos · Inativos),
  tabela com colunas próprias (Nome/Razão social · CPF/CNPJ · Tipo · Papéis · Cidade/UF · Situação · ⋯), paginação de 50
  ("Mostrando 1–50 de 1.248", ‹ 1 … 4 5 6 … 25 ›), estado vazio e carregamento discreto no cabeçalho da tabela.
- Colunas por largura: some Cidade (< 1100), Papéis (< 940), Tipo (< 720) e Documento (< 600, vai para baixo do nome).
- "Todos" = cadastros em uso (ativos e em análise), como sempre: inativos continuam fora da busca padrão (regra 5.4).
- Ficha em tela cheia: "← Voltar para Pessoas", cabeçalho (nome + selo de situação · tipo + papéis em selos · documento,
  código e cidade), "⚙ Configurações do módulo" e "⋯" (histórico, desativar/reativar, fechar). Não há botão "Editar": a
  ficha já abre editável, com Salvar/Descartar na barra fixa (como antes).
- API: `GET pessoas/pagina` (página + total; filtros novos `natureza`, `somenteAtivos`, `somenteInativos`). Só consulta:
  nenhuma regra, tabela ou migration.
