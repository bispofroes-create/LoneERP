# Arquitetura de UX do Lone (redesenho do módulo Pessoas, 26/09/2026)

Decisão: **cada módulo tem as suas telas e as suas configurações**; o que é transversal (Minha conta, Usuários, Perfis
de acesso) fica no **menu do usuário**, no canto superior direito (03/10/2026; antes era a página "Configurações do
sistema" no menu lateral). O menu lateral é organizado por módulo, em **dois níveis** (padrão dos ERPs maduros: Protheus, Dynamics 365,
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

**Decisões do usuário (03/10/2026):**
- Aprovado o plano inteiro, na ordem: (1) `CampoData` com o calendário — feito no próprio `Campo` com `Mascara="Data"`,
  então vale para todos os campos de data das fichas de uma vez; (2) o campo **Prazo** entre Início e Fim; (3) o cadastro
  **Prazos de período**.
- **Prazo:** escolher um prazo cadastrado (30, 60, 180 dias…) ou digitar os dias à mão; contagem inclusiva.
- **Cadastro "Prazos de período":** tabela nova (migration autorizada; o Update-Database fica com o usuário), uma lista
  só para o sistema inteiro, pré-carregada com 7, 15, 30, 60, 90, 180 dias e 1 ano.
- **Fim não cai em fim de semana (pedido novo):** junto do período, a opção "Não terminar em fim de semana". Ligada, se o
  Fim calculado cair num sábado ou domingo, vai para a segunda-feira seguinte e a tela informa quantos dias foram somados
  ("+2 dias: o fim cairia no sábado 31/10, foi para segunda 02/11"). Feriados entram depois, com o calendário de
  feriados (item 3 abaixo).

**Feito (03/10/2026):**
- Parte 1: calendário no `Campo` com `Mascara="Data"` (`Calendario`, `Popover`), em todas as fichas.
- Parte 2: `c:CampoPrazo` entre Início e Fim (conta em `CalculoPrazo`, testada): digitar os dias ou escolher no ▾
  (7, 15, 30, 60, 90, 180 dias, 1 ano pelo calendário); "Não terminar em fim de semana" leva o Fim para a segunda e
  avisa "+2 dias: o fim cairia no sábado 31/10 e foi para segunda 02/11."; mudar o Fim recalcula os dias; o Início move
  o Fim só depois de o prazo ser escolhido no campo (abrir ou trocar de registro não muda nada — `Registro`). A opção
  do fim de semana não é gravada. Tela-piloto: Ausências e coberturas.
- A lista do ▾ vem de `CalculoPrazo.Padrao` até a parte 3 (cadastro "Prazos de período", com tabela).

**Decisões que ainda estavam em aberto:**
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
  sistema está aberto). "⇄ Trocar empresa" aparece com mais de uma empresa.
- **Menu do usuário (03/10/2026; referência: menu do usuário do SAP Fiori, Dynamics e Office):** tocar no usuário abre
  um popover (`Controles/MenuDoUsuario`): cabeçalho (nome, login · empresa) · **Minha conta** (🔑 Trocar senha) ·
  **Administração** (🛡 Perfis de acesso, 👥 Usuários, 📅 Prazos de período — a seção só aparece com alguma das
  permissões) · ⇄ Trocar de
  usuário (encerra a sessão e volta ao login) · ⏻ Sair do Lone (encerra a sessão e fecha o app).
  "Configurações do sistema" saiu do menu lateral e a página deixou de existir. Parâmetros gerais do sistema, quando
  existirem, entram em Administração.
- **Celular:** sem barra de título nem popover — tocar no usuário, no rodapé do menu, mostra as mesmas opções numa lista.
- **Trocar senha:** rota especial `ModulosConfiguracao.RotaTrocarSenha` (abre por cima, não é tela do Shell). Trocar
  senha, Usuários e Perfis continuam na busca do menu e nos favoritos (caminho "Menu do usuário").

## Design system

- Espaçamento: 4 · 8 · 12 · 16 · 24 · 32. Raios: 6 (controles, selos) e 8 (superfícies). Bordas `BordaSutil`; sem sombras.
- Tipografia: `TituloPagina` 24 · `SubtituloPagina` 14 · `SecaoTitulo`/`Subtitulo` 16 · texto 14 · `CabecalhoTabela` 12 · `SeloTexto` 12.
- Cores novas: `PrimariaHover`, `Selecao`, `SuperficieRealce`, `BordaSutil`, `MenuFundo/MenuTexto/MenuTextoSecundario/MenuItemAtivo/MenuDestaque`, `SeloNeutroFundo`.
- Componentes (estilos): `PainelConteudo` (superfície), `Separador`, `BotaoTerciario`, `BotaoPerigo`, `BotaoIcone` (⋯ ⚙ ‹ ›),
  `Selo`/`SeloTexto` (situação, tipo, papel), `Chip` (atalho de filtro). Estados nunca só por cor (sempre com texto/traço).
- Ícones: só caracteres de texto (⚙ ⋯ ‹ › ← +) — o app não tem fonte de ícones; trocar por uma fonte (ex.: Fluent/Material)
  é um passo futuro, sem mexer no layout.

## Padrão: cartões lado a lado começam na mesma altura (pedido do usuário, 03/10/2026)

- Quando um cartão lateral (prévia, resumo, painel) fica ao lado do cartão principal da tela, **o topo dos dois fica
  alinhado**. O que fica acima do cartão principal e é só dele (abas, cartão recolhido, avisos, barra da tabela) **não**
  empurra o lateral para cima: o lateral começa junto com o cartão principal.
- Como fazer: uma `Grid` com `ColumnDefinitions="*,Auto"` e `RowDefinitions="Auto,Auto"`. Na linha 0, coluna 0, fica o que
  vem acima do cartão principal. Na linha 1 ficam o cartão principal (coluna 0) e o lateral (coluna 1,
  `VerticalOptions="Start"`). Não compensar com margem fixa: a altura do que fica em cima muda (avisos, abas, densidade).
- Exemplos: lista de Pessoas (tabela + prévia) e ficha de Pessoas (conteúdo da aba + resumo da pessoa).
- Vale para **todas as telas novas** com cartão lateral.

## Padrão: largura das colunas nas listagens (decisão do usuário, 03/10/2026)

- **A coluna principal** (a que identifica o registro: nome / razão social, descrição) é a que **mais cresce** com o
  espaço livre (peso 3) e a primeira a encolher quando entram mais colunas, até a mínima dela (Pessoas: 260). Passou da
  mínima, a tabela rola para o lado.
- **Texto livre** (cidade, papéis, e-mail, bairro, nome fantasia, observação) cresce com peso 1, a partir da mínima.
- **Dado curto** (código, CPF/CNPJ, tipo, situação, UF, datas, números, valores, telefone, CEP, opções) tem **largura fixa** e
  nunca cresce: esticar só cria espaço vazio dentro da célula.
- A sobra nunca vira área vazia: com poucas colunas, a coluna principal fica larga (decisão P1, opção A).
- Onde fica: a regra de cada tela em código (Pessoas: `GradePessoas.Definicao`); o cálculo é da `CalculadoraLarguras`.

## Padrão: rodapé das listagens e posição do registro (decisão do usuário, 03/10/2026)

- O rodapé **diz o que está contando** e separa o total da página: "51 pessoas · página 1 de 2". Com uma página só,
  apenas "51 pessoas" (e os botões de página somem). Um registro: "1 pessoa". Com filtro, conta o que o filtro mostra.
  Nunca "Mostrando 1–50 de 51" (parecia "51 páginas").
- A **posição de um registro** aparece onde ele está aberto (prévia, ficha): "8 de 51", na lista toda, e não dentro da
  página.
- Onde fica: `Paginacao.Resumo` (com a palavra da tela: "pessoa"/"pessoas") e `PreviaPessoa.Posicao`.

## Padrão: campo dentro de borda do Lone tem uma moldura só (pedido do usuário, 03/10/2026)

- Campo (texto, escolha ou pesquisa) desenhado dentro de uma borda do Lone mostra **só a borda do Lone**: o controle do
  Windows fica sem moldura, sem fundo e sem a linha de foco própria. Antes aparecia uma segunda moldura por dentro
  (parecia defeito).
- Com o campo em uso, a borda do Lone fica na **cor primária**. Valor inválido continua em vermelho.
- Como fazer: `plat:CampoSemMoldura.Ligado="True"` no campo (XAML) ou `CampoSemMoldura.Aplicar(campo)` (código), em
  `Lone.App/Plataforma`. Campo sem borda do Lone (formulários) continua com o visual do Windows.
- Aplicado em: linha de filtro da grade, pesquisa de Pessoas, busca de colunas, busca de campos do painel de filtros,
  pesquisa do menu.

## Padrão: barra de ações da ficha (decisão do usuário, 03/10/2026; referência: rodapé do SAP Fiori)

- **Estado à esquerda, só quando há o que avisar:** "● Alterações não salvas" (ponto na cor de aviso) ou "Novo cadastro,
  ainda não salvo". Sem alterações, não aparece texto ("Sem alterações" seria ruído).
- **Ações à direita, sempre no mesmo lugar:** **Salvar** (destaque) e **Descartar**, nessa ordem (padrão do SAP e do
  Windows). Sem alteração ficam desligados, não somem: o usuário sabe onde estão e a tela não pula.
- **Fechar não fica na barra:** sai pelo "Voltar" do topo. Sem alterações fecha direto, sem aviso; com alterações
  pergunta antes.
- **Motivo da alteração (opcional):** só com alteração, primeiro como o botão "+ Adicionar motivo"; ao clicar, o campo
  abre ao lado (moldura única, cursor no campo). Salvou ou descartou, volta a ficar fechado. Se um dia algum campo
  **exigir** motivo, o padrão é perguntar numa janela na hora de salvar, só nesse caso.
- **Depois de salvar:** a confirmação aparece no aviso que some sozinho ("Pessoa salva: Nome").
- Aplicado em: ficha de Pessoas. As demais fichas passam a seguir quando forem revistas.

## Padrão: blocos de campos e altura dos campos (decisão do usuário, 03/10/2026)

- Campos das fichas ficam em `c:BlocoCampos` (Lone.App/Controles), não em `FlexLayout`: o FlexLayout do MAUI repartia a
  altura do bloco igualmente entre as linhas e cortava os campos mais altos. Cada linha tem a altura do seu campo mais
  alto. **Grade do Lone** (decisão do usuário, 03/10/2026, referência SAP Fiori): 1 coluna até 600 de largura, 2 colunas
  até 1300, 3 colunas daí em diante. O campo ocupa **1 coluna** (`FlexLayout.Basis` 50%), **2 colunas**
  (`c:BlocoCampos.Colunas="2"`) ou a **linha inteira** (`FlexLayout.Basis` 100%); sem base = largura natural (só para
  itens que não são campos). `AlignItems` alinha os campos de uma linha. Listas de selos/etiquetas e barras de botões
  continuam em `FlexLayout`.
- Regras da grade a aplicar tela a tela (aprovadas em 03/10/2026; análise em `_entrega/p2-b2/ANALISE-PROPORCAO-FORMULARIOS.md`):
  texto livre longo ocupa a linha inteira; campo + botão (CEP + Buscar) ocupa 1 coluna; botões de ação de uma seção numa
  linha própria ou no cabeçalho da seção, nunca no lugar de um campo; largura máxima do conteúdo só em três medidas
  (`LarguraFichaCompleta` 1600, `LarguraFichaSimples` 1000, `LarguraPainel` 360, em `Estilos.xaml`); 12 entre colunas,
  8 entre linhas de campos, 24 entre seções.
- Caixa de texto e lista de escolha têm a mesma altura (44) e letra (15): lado a lado ficam iguais.
- **Altura única de 44 em tudo o que se preenche ou aciona** (decisão de 03/10/2026; referência: densidade "cozy" do SAP
  Fiori, 44 px, que nunca mistura densidades na mesma página): caixa de texto, lista de escolha, pesquisa (da lista e
  `c:CampoPesquisa`) e botões. Na linha de pesquisa e filtros, tudo alinha pela base (os campos com rótulo em cima ficam
  com a caixa na mesma linha da pesquisa) e a caixa de marcar fica centrada nos 44.
- Pesquisa dentro de formulário ou de filtros (ex.: Cliente + Buscar) é um `c:CampoPesquisa` com rótulo, numa coluna
  da grade de campos — nunca uma barra solta com a largura da página.
- **Botão ao lado de campo fica alinhado à caixa** (03/10/2026): os campos trazem do estilo o espaço da grade (12 à
  direita, 8 embaixo); ao lado de um botão ou da pesquisa, esse espaço sai do campo (`Padding="0"`) e fica no contêiner
  (campo + botão = um `Grid *,Auto` com o espaço da grade, como CNPJ + Consultar e Mapa + Conferir). Na linha de
  pesquisa da lista, a `ListaCadastro` faz isso sozinha com os filtros.
- Campo sozinho também fica num `c:BlocoCampos` (meia linha, alinhado à esquerda): com `HorizontalOptions="Start"` ele
  encolhe ao tamanho do texto e com `Fill` + largura máxima o MAUI o centraliza.

## Padrão: tela de cadastro (decisão do usuário, 03/10/2026, opção A; referência: List Report + Object Page do SAP Fiori)

- Substitui o layout antigo "lista estreita + ficha ao lado". Tela-piloto: Ausências e coberturas; o usuário aprovou e
  pediu o padrão em todas as telas (03/10/2026): as 27 telas que usavam `LayoutMestreDetalhe` foram migradas e o controle
  foi removido.
- **Peças comuns** (`Lone.App/Controles`): `c:ListaCadastro` (título, subtítulo, "+ Novo ...", pesquisa, `Filtros` ao lado
  da pesquisa, `Aviso` abaixo dela, mensagem, `GradeLista` e estado vazio) e `c:FichaCadastro` (Voltar, título, situação,
  mensagem, o formulário como conteúdo, e a `BarraFicha`). Ficha com ações próprias troca Salvar/Descartar por
  `AcoesBarra` (Metas, Operações territoriais, Transferências). Lista em árvore (Territórios): `GradeCadastro.Recuo` e
  `Marcador`. Situação de cadastro auxiliar: `ColunaCadastro.Situacao` (selo Ativo/Inativo).
- **Páginas de consulta e de configuração** (Carteira vencendo, Carteira em uma data, Divergências, Configurações,
  Parâmetros): sem largura máxima centralizada; ocupam a página com a mesma margem dos dois lados.
- **Lista (página inteira):** título e subtítulo da página; ação principal ("+ Nova ...") à direita do título; pesquisa
  com moldura única e filtros simples na linha de baixo; a lista numa `GradeLista` dentro de um painel, com a parte fixa
  (título e subtítulo do registro) e colunas pela regra de largura das listagens; estado vazio com explicação e ação.
- **Ficha (página própria):** "← Voltar para ..." no topo, título e situação, o formulário num painel com a grade de
  campos ocupando a largura da página, com a mesma margem dos dois lados, como Pessoas (a grade chega a 3 colunas e não sobra área vazia; 03/10/2026) e a barra de ações da ficha (`c:BarraFicha`) embaixo.
- Código: o ViewModel da tela informa as colunas em `CriarGradeDaLista()` (`GradeCadastro<T>`, `ColunaCadastro<T>`); a base
  (`CadastroViewModelBase`) monta o conteúdo da lista ao filtrar, marca o registro aberto e abre a ficha pelo toque
  (`AbrirRegistroDaLinhaCommand`).

## Padrão: ordenar a lista pelo título da coluna (pedido do usuário, 03/10/2026)

- Em todas as telas de cadastro (`ListaCadastro` + `GradeCadastro`), como em Pessoas: clicar no título da coluna
  ordena **crescente (▲)**, de novo **decrescente (▼)**, e de novo volta à **ordem padrão** (a do servidor). A dica do
  título diz o que o próximo clique faz.
- Textos em ordem natural ("TE-9" antes de "TE-10"), sem diferenciar maiúsculas nem acentos; datas e números pelo valor
  (a coluna informa `ordem:`; ex.: Efeito ordena pela data, Mudanças pelo total); vazios sempre por último. Empates
  mantêm a ordem padrão. A busca e a ficha aberta continuam valendo depois de ordenar.
- Listas em árvore (Territórios) não ordenam: a hierarquia é a ordem.

## Cadastro "Prazos de período" (decisão do usuário, 03/10/2026)

- Os prazos prontos do campo Prazo (▾) vêm do cadastro **Prazos de período** (menu do usuário › Administração;
  permissão "Alterar parâmetros do cadastro"). Cada prazo: quantidade + unidade (**dias**, contando o início; **meses**
  e **anos**, pelo calendário). Nada é excluído: desativar tira da lista. A migração cria os de antes (7, 15, 30, 60,
  90, 180 dias e 1 ano).
- O campo Prazo lê a lista na primeira vez que o ▾ abre e guarda por 5 minutos (gravar no cadastro relê). Sem resposta
  da API, valem os de antes.
- Sem coluna Unidade ("7 dias" já diz a unidade); a pesquisa por "meses" continua achando. Campo Prazo em: Coberturas, Metas, Territórios (responsáveis), vínculos de carteira e
  lotações do colaborador.

## Padrão: lista de cadastro auxiliar (decisão do usuário, 04/10/2026; análise em _entrega/p2-b2/ANALISE-PROMPT-CADASTRO-AUXILIAR.md)

Vale para todas as telas com `ListaCadastro` (peça comum; nenhuma tela precisa mudar):
- **Filtro Situação** (Ativos / Inativos / Todos) ao lado da pesquisa em toda lista com a coluna Situação (menos as
  em árvore). Começa em **Ativos**; a **última escolha fica guardada para o usuário** (preferência da tela
  "lista-⟨tela⟩" na API; vale em qualquer aparelho).
- **Lista vazia em três casos:** nada cadastrado (texto da tela); nada para a pesquisa ("Nenhum resultado para "x"",
  dizendo se há registros com o texto em outra situação, e **Limpar pesquisa**); nada na situação ("Nenhum cadastro
  em "Inativos"" e **Mostrar todos**). Nunca repete o "+ Novo" (um só, no alto).
- **Pesquisa** sem diferenciar maiúsculas nem acentos ("periodo" acha "período").
- **Linha** de 44 px sem subtítulo; 56 com subtítulo.
- **Ordenação:** ▲/▼ só na coluna ordenada; nas outras, o "↕" aparece só com o mouse em cima.
- **Colunas:** não repetir em coluna o que a coluna principal já diz.
- **Teclado (04/10/2026), em toda GradeLista:** ↑↓, Home/End e Page Up/Down andam pelas linhas (do próprio Windows);
  **Enter** na linha faz o mesmo que o clique; a linha com o foco ganha o destaque do mouse; os **títulos ordenáveis**
  recebem o foco com o Tab e ordenam com **Enter ou Espaço** (o "↕" aparece com o foco).
- **Futuro:** menu "…" por linha quando houver ação sem abrir a ficha; contador.

## Padrão: texto que não cabe (pedido do usuário, 03/10/2026; referência: SAP Fiori)

- **Listas, tabelas e colunas de nomes** (uma linha por item): o texto termina em **"…"** e aparece **inteiro ao passar o
  mouse** (dica). Nunca cortar no meio da letra. A `GradeLista` já faz isso em todas as células.
- **Texto com partes** (nome em negrito + qualificação): rótulos separados, cada um com "…" — no Windows, o texto com
  partes (`FormattedText`) corta sem as reticências.
- **Formulários:** o rótulo do campo e os textos de ajuda quebram linha (não cortam); o valor digitado rola dentro do
  campo. A dica do campo (placeholder) é curta e cabe no campo — não leva informação essencial.

## Padrão: botão Novo e "Adicionar" (decisão do usuário, 03/10/2026; análise em _entrega/p2-b2/ANALISE-BOTAO-NOVO.md)

- **Novo da página:** canto superior direito, "+ Novo ⟨objeto⟩" / "+ Nova ⟨objeto⟩", principal (como Salesforce e
  Oracle; no SAP fica na barra da tabela, que aqui é o mesmo lugar). **Um só por tela:** a lista vazia não repete o
  botão; mostra o título e a orientação "Use "+ Nova transferência", no alto da página, para incluir a primeira."
  (decisão de 03/10/2026).
- **Permissão:** sem permissão de criar, o "+ Novo" some (`PodeCriar` da `CadastroViewModelBase`; Pessoas = Criar,
  Metas = Gerenciar, Coberturas = Comercial.Coberturas, Territórios = Configurar, Operações = Planejar). A API continua
  conferindo.
- **Ctrl+N** = Novo na tela de cadastro aberta (dica no botão: "Novo cargo (Ctrl+N)"); pergunta antes se houver
  alterações não salvas.
- **Dentro da ficha:** "+ Adicionar ⟨item⟩", botão secundário no cabeçalho da seção, à direita. "+ Novo/Nova" só quando
  cria um cadastro que existe sozinho (ex.: "+ Nova etiqueta", "+ Novo território abaixo deste").
- **"Salvar e novo"** na barra da ficha dos cadastros auxiliares (`MostrarSalvarENovo`): salva e, se salvou, abre uma
  ficha nova.

## Padrão: filtros de consulta e Imprimir (decisão do usuário, 03/10/2026; referência: barra de filtros do SAP Fiori)

- Filtros numa linha da grade de campos; no fim da linha, alinhados às caixas: **Limpar** (secundário, só aparece com
  filtro) e **Filtrar** (principal na página; secundário dentro de uma ficha, onde o principal é Salvar). Nunca
  "Limpar filtro" solto entre botões.
- Linha de filtros cheia e equilibrada: cada filtro tem uma largura mínima (`FlexLayout.Basis` fixa) e um peso
  (`c:BlocoCampos.Peso`); fechada a linha, a sobra é dividida pelos pesos (os campos de texto e de escolha esticam; o
  número e os botões não).
- Número com setas: `c:CampoNumero` (▲▼ dentro do campo e teclas ↑ ↓; digitar continua valendo; mínimo, máximo, passo).
- **Imprimir / PDF** fica na barra da lista (à direita, acima dela), com o resumo à esquerda: é ação sobre a lista,
  não sobre o filtro.
- Aplicado em: Carteira vencendo (filtros por prazo, papel, quem atende, empresa e cliente; prazo em selo: até 7 dias
  vermelho, até 30 laranja) e Histórico de Pessoas.

## Padrão: tela de consulta do Lone (decisão do usuário, 03/10/2026; referência: List Report do SAP Fiori)

1. **Cabeçalho:** título e descrição curta.
2. **Filtros:** numa linha cheia (largura mínima + peso), Limpar (só com filtro) e Filtrar no fim. Período com
   "Aviso padrão (N dias)", prazos prontos e "Personalizado…" (aparece o número com ▲▼).
3. **Barra da lista:** título com o contador ("Vínculos vencendo (12)"), atalhos com contagem por faixa
   (`c:AtalhoContagem`: "Todos 12 · Hoje 1 · Até 7 dias 3"; tocar filtra) e, à direita, **Exportar ▾** (`c:BotaoMenu`:
   Imprimir / PDF, Excel (CSV); novas saídas entram no menu, sem mais botões).
4. **Lista em colunas** ordenável (clicar no título; de novo inverte); tocar na linha abre o registro.
5. **Lista vazia num lugar só, numa frase e uma ação** ("Nenhum vínculo termina nos próximos 30 dias" · "03/10/2026 a
   02/11/2026" · [Ampliar para 60 dias], ou "Limpar filtro" com filtro). O período é escolhido **só no campo Período**.
   Sem resultado, os atalhos com 0 e o Exportar somem (fica o título com o contador); atalho que repete o período
   ("Até 30 dias" com período de 30) também some. Nada de mensagem repetida fora da lista.
6. Sem cartões de indicador por enquanto (os atalhos com contagem fazem esse papel). Depois: escolher colunas,
   agrupar, salvar visualização.
- Aplicado em: Carteira vencendo (a API devolve também os dias e o período consultado: `CarteiraVencendoDto`).

## Pessoas

- Lista em tela cheia: cabeçalho ("Pessoas" + "+ Nova pessoa" + ⚙), pesquisa + "Filtros" (painel com papel, etiqueta,
  incluir inativos, município a corrigir, limpar), atalhos (Todos · PF · PJ · Clientes · Fornecedores · Ativos · Inativos),
  tabela com colunas próprias (Nome/Razão social · CPF/CNPJ · Tipo · Papéis · Cidade · UF · Situação · ⋯), paginação de 50
  ("Mostrando 1–50 de 1.248", ‹ 1 … 4 5 6 … 25 ›), estado vazio e carregamento discreto no cabeçalho da tabela.
- Colunas por largura: some Cidade (< 1100), Papéis (< 940), Tipo (< 720) e Documento (< 600, vai para baixo do nome).
- "Todos" = cadastros em uso (ativos e em análise), como sempre: inativos continuam fora da busca padrão (regra 5.4).
- Ficha em tela cheia: "← Voltar para Pessoas", cabeçalho (nome + selo de situação · tipo + papéis em selos · documento,
  código e cidade), "⚙ Configurações do módulo" e "⋯" (histórico, desativar/reativar, fechar). Não há botão "Editar": a
  ficha já abre editável, com Salvar/Descartar na barra fixa (como antes).
- API: `GET pessoas/pagina` (página + total; filtros novos `natureza`, `somenteAtivos`, `somenteInativos`). Só consulta:
  nenhuma regra, tabela ou migration.
