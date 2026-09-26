# Arquitetura de UX do Lone (redesenho do módulo Pessoas, 26/09/2026)

Decisão: **cada módulo tem as suas telas e as suas configurações**; a página "Configurações do sistema" fica só com o que é
transversal. O menu lateral é organizado por módulo.

```
Início
PESSOAS            Pessoas · Consulta avançada · ⚙ Configurações de Pessoas
ORGANIZAÇÃO        Grupos empresariais · ⚙ Configurações de Organização
METAS              Metas · ⚙ Configurações de Metas
⚙ Configurações do sistema
(futuro) VENDAS / COMPRAS / ESTOQUE / FINANCEIRO / FISCAL — cada um com "⚙ Configurações de …"
```

## Onde está

| Peça | Arquivo |
|---|---|
| Estrutura do menu (seções, itens, item ativo, permissões) | `Lone.Cliente/ViewModels/MenuLateral.cs` + `MenuViewModel.CriarSecoes` (`SistemaViewModels.cs`) |
| Desenho do menu (FlyoutContent) e rotas | `Lone.App/AppShell.xaml(.cs)` — os `FlyoutItem` só registram rotas/permissões |
| Configurações por módulo (catálogo, grupos, permissões) | `Lone.Cliente/ViewModels/ConfiguracoesViewModel.cs` (`ModulosConfiguracao`) + `Views/ConfiguracoesPage` |
| Lista de Pessoas (atalhos, filtros, paginação, colunas, ⋯) | `PessoasViewModel` (seção "Lista") + `ListaPessoas.cs` + `Views/PessoasPage.xaml` |
| Design system (tokens e estilos) | `Resources/Styles/Cores.xaml` e `Estilos.xaml` (bloco "DESIGN SYSTEM DO LONE") |

**Para um módulo novo:** uma constante em `ModulosConfiguracao` (título/descrição), os itens no catálogo de
`ConfiguracoesViewModel`, uma seção em `MenuViewModel.CriarSecoes`, e um `FlyoutItem` `configuracoes-<modulo>` no AppShell.

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
