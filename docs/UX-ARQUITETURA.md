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

## Padrão: todo período com início e fim mostra os dias e avisa perto do fim (pedido do usuário, 28/09/2026; a implementar)

**Regra para o sistema inteiro:** onde houver um período com início e fim, a tela mostra **quantos dias** ele tem e
**quanto falta**. Perto do fim, o período ganha **destaque de aviso**. É o mesmo comportamento já feito no cartão do
vínculo da carteira (Fase 1c), agora como padrão para todos.

- **Texto:**
  - duração: "33 dias";
  - situação: "começa em 5 dias", "faltam 12 dias", "termina hoje", "encerrado há 3 dias";
  - sem fim: "em aberto" (não gera aviso).
- **Aviso:** a faixa "Termina em N dias", a partir da antecedência configurada. Não impede nada, só avisa.
- **Antecedência:**
  - quando o próprio cadastro tem a sua, usa a dele (ex.: `DiasAvisoVencimento` do tipo de documento, `DiasAvisoFimVinculo` dos Parâmetros comerciais);
  - senão, usa um padrão geral de 30 dias.
  - **A decidir na implementação:** onde fica o padrão geral (parâmetros do sistema ou de cada módulo).
- **Uma peça só:**
  - o cálculo sai de `CarteiraFormulario.Prazo/PertoDoFim/AvisoFim` e vai para uma regra comum, testável sem tela (ex.: `Lone.Domain/Comum/Periodo`: duração, dias restantes, situação e "perto do fim");
  - um componente de tela reutilizável (ex.: controle `PrazoPeriodo`) fica ao lado dos campos Início/Fim;
  - a mesma regra alimenta os indicadores e listas do tipo "vencendo".
- **Onde aplicar** (levantamento de 28/09/2026):
  - carteira do cliente (feito);
  - ausências e coberturas (formulário "Ausência": Início/Fim);
  - exceções comerciais;
  - períodos de papel da pessoa;
  - relacionamentos (sócio, administrador…);
  - lotação do colaborador (e o vínculo, com o desligamento);
  - membros de equipe;
  - período das metas;
  - histórico fiscal (só a duração: não é "vencimento");
  - consentimentos com prazo;
  - documentos com validade (já avisam: alinhar ao componente comum);
  - e todo período novo dos próximos módulos: territórios, transferências com efeito, regras e planos de comissão com vigência, períodos de comissão.
- **Atenção:** não mostrar "faltam" nem avisar em período **encerrado de propósito** (histórico). Também não avisar
  em vínculo **já trocado**, quando outro do mesmo papel começa no dia seguinte (pendência já anotada na 1c).
- **Entrega:** sem banco. Pode vir junto com a Fase 1d ou logo depois dela, como etapa própria de interface.

## Padrão: campo de data com calendário, período por quantidade de dias e dia útil (pedido do usuário, 28/09/2026; a implementar)

Complementa o padrão acima (dias e aviso perto do fim). Juntos, formam um único componente de período, usado no
sistema inteiro.

**1. Calendário em toda data.**
- Ícone 📅 **dentro do campo, à direita**, como em SAP Fiori, Dynamics 365, Salesforce e Omie.
- Clicar abre um calendário pequeno, com:
  - hoje destacado;
  - navegação por mês e ano;
  - o botão "Hoje";
  - "Limpar" quando a data é opcional.
- **Digitar continua valendo** e segue sendo o caminho mais rápido.
- **Atalhos ao digitar**, inspirados no Dynamics 365 F&O ("t", "t+30"):
  - "h" = hoje;
  - "+30" / "-1" = hoje mais ou menos N dias (no campo Fim, a partir do Início);
  - "30/10" completa o ano;
  - "301026" vira 30/10/2026.
- **Hoje:** os campos de data são texto com máscara (`Campo`) e `TextoTela.TentarData`.
- **A fazer:** um controle `CampoData` (texto + botão + calendário). Atenção: o `DatePicker` do MAUI não aceita data vazia, então provavelmente será preciso um calendário próprio num popup, o mesmo no Windows e no Android. Trocar os campos de data de todas as telas por ele.

**2. Período por quantidade de dias.**
- Entre Início e Fim, um campo **"Dias"**, ligado aos dois:
  - mudar Dias recalcula o Fim;
  - mudar o Fim recalcula Dias;
  - mudar o Início mantém Dias e move o Fim.
- **Contagem inclusiva**, a mesma do Lone: o Fim é o último dia de vigência. Exemplo: 30 dias a partir de 28/09/2026 terminam em 27/10/2026, como 30 dias de férias. A tela mostra "30 dias (28/09 a 27/10)" para não haver dúvida. É a mesma conta de `CarteiraFormulario.Prazo`.
- **Atalhos opcionais** ao lado: +7, +15, +30, +90 e 1 ano. O "1 ano" é pelo calendário: 28/09/2026 a 27/09/2027.
- Período sem fim ("em aberto"): Dias vazio.

**3. Dia útil (fim de semana e feriado).**
- **Como os ERPs maduros fazem:**
  - usam um **calendário de dias úteis** (SAP: *factory calendar*; Dynamics: calendários de trabalho; Protheus: `DataValida` e a tabela de feriados);
  - o ajuste é uma **regra do contexto**, não uma escolha a cada digitação: "vencimento que cai em dia não útil vai para o próximo dia útil" é regra da condição de pagamento e do banco;
  - a vigência de um contrato ou das férias é contada em dias corridos.
- **Proposta:**
  - um cadastro **Calendário de feriados** (nacionais pré-carregados; estaduais e municipais por empresa ou estabelecimento), usado por todos;
  - cada contexto define a sua regra: **Não ajustar** (padrão nas vigências), **Próximo dia útil** ou **Dia útil anterior** (vencimentos, entregas, pagamento de comissão), e opcionalmente se a contagem é em **dias corridos ou úteis**;
  - onde a regra é "Não ajustar", o campo **avisa sem mudar nada** ("30/10/2026 cai num sábado") e oferece um toque: "usar sexta 29/10" ou "usar segunda 01/11". O usuário decide, e a data não muda escondida;
  - onde a regra é automática, a tela mostra a data ajustada e o porquê ("vence 02/11 → 03/11, feriado de Finados").
- **Banco:** o calendário de feriados e a regra por contexto **precisam de migration** (aprovação do usuário). O calendário e os dias corridos, sem ajuste, não precisam.

**Decisões a confirmar na implementação:**
- contagem inclusiva, "30 dias = até 27/10" *(recomendado, igual ao Lone hoje)*;
- os atalhos de digitação;
- se o calendário de feriados entra agora ou junto com o financeiro, que é onde o ajuste é obrigatório;
- a regra de cada contexto.

**Ordem sugerida:**
1. `CampoData` com o calendário e os atalhos;
2. o componente de período, com Dias, a duração e o aviso do padrão anterior;
3. o calendário de feriados e as regras por contexto.

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
