# Fase 2b — Motor de Cobertura e Atribuição Territorial

Revisão arquitetural (28/09/2026). **Nenhum código ou migration antes da aprovação.** Substitui o esboço curto da 2b
em `PLANO-FASE2-EQUIPES-TERRITORIOS.md`. Base: o pedido de revisão do usuário (47 itens) + auditoria do código atual
(commit `c75dce3`).

---

## A. Diagnóstico

### A.1 O que já existe e será reaproveitado (auditado no código)

| Peça existente | Onde | Como a 2b usa |
|---|---|---|
| Catálogo de condições de Pessoas (~70 campos, ids estáveis, "não renomear") | `CatalogoFiltrosPessoas`, `CamposFiltroPessoas` | É a linguagem das regras de cobertura. Nenhum filtro novo. |
| Tradução das condições para o banco (LINQ parametrizado, índices já pensados) | `FiltrosPessoasSql.Aplicar(IQueryable<Pessoa>, condições, Contexto)` | O motor chama o mesmo método por grupo de condições. **Não muda.** |
| Painel de filtros (editor de condições na tela) | `PainelFiltrosPessoas` + `PainelFiltrosView` | Editor de cada grupo da regra (reuso como componente). |
| Vigência início/fim, "nunca apagar, encerrar" | `CarteiraCliente`, `MembroEquipe`, `CoberturaComercial` | Mesmo padrão em atribuições, responsáveis, exceções e posições. |
| Operação com número legível + itens imutáveis + resultado por cliente | `TransferenciaCarteira` / `TransferenciaCarteiraItem` (`TR-2026-0001`, `[NaoAuditar]`) | Modelo da `OperacaoTerritorial` (`TE-2026-0001`). |
| Limite de datas no passado | `ParametrosComerciais.DiasRetroativosMaximo` (30) | Mesmo limite para a data de efeito. |
| Auditoria de negócio (antes/depois, eventos, motivo, operação) | `AgregadoRaiz.RegistrarEvento`, `RegistroAuditoria`, `IMotivoDaOperacao` | Cadastros auditados campo a campo; itens da operação ficam fora (são o próprio registro). |
| Papel comercial (Vendedor, Representante, Supervisor…) com "quem pode ser" | `TipoCarteira` + `TipoCarteiraClassificacao` | É a **função** do responsável do território. Nada de lista nova de funções. |
| Equipes com hierarquia e liderança temporal | `Equipe`, `MembroEquipe` (2a-1) | Responsável do território pode ser equipe; "Meus territórios" (2b-2) sai daqui. |
| Multiempresa: `EmpresaId` nulo = todas as empresas do grupo | carteira, cobertura, exceção comercial, perfis | Mesmo padrão no mapa territorial. |
| Escopo por registro | `EscopoPessoasSql`, `IEscopoPessoas` | Listas de clientes do território respeitam o alcance de quem consulta. |

### A.2 O que seria perigoso duplicar

1. **Um segundo motor de filtros.** Tudo que o motor territorial precisar de condição vem do catálogo; campo novo nasce lá.
2. **Uma segunda "carteira".** Território não é quem atende o cliente; a carteira continua sendo a única fonte de
   "vendedor do cliente" (decisão T4).
3. **Numeração de documentos.** Hoje o `TR-` usa `MAX(Sequencia)+1`, que depende do índice único para não repetir sob
   concorrência. Criar outro gerador igual para `TE-` duplicaria o problema (ver T12).
4. **Uma lista de funções** do responsável: já existe o papel comercial.

### A.3 Achados no código atual que afetam a 2b

| Achado | Impacto | Proposta |
|---|---|---|
| O motor de filtros só faz **E** (cada condição é um `Where` a mais). Não há OU nem grupos. | Regras como "MG **ou** Curvelo e Montes Claros" não cabem. | Grupos em forma normal disjuntiva **acima** do motor (T6), sem mexer nele. |
| Alguns campos do catálogo são **relativos a hoje** (idade, aniversário, documentos vencendo, sem interação, dias relativos) ou dependem de permissão (financeiro, privacidade, colaborador) ou da carteira (vendedor, sem carteira). | Regra com eles muda de resultado sozinha com o tempo, ou dá resultado diferente por quem executa, ou fica circular com a carteira. | Marca `UsavelEmCobertura` no catálogo: só atributos estáveis do cadastro (T6). |
| `FiltrosPessoasSql` recebe condições já validadas por permissão **de quem consulta**. | A regra precisa dar o mesmo resultado para qualquer usuário. | O motor roda sem escopo e sem permissão de campo; a permissão vale para **configurar** (ver o campo para usá-lo). |
| Sequência `TR-` por `MAX+1`. | Colisão sob concorrência (hoje resolvida por erro). | Numerador de documentos (T12). |

### A.4 A proposta anterior da 2b-1, item a item

| Item do plano anterior | Classificação | Por quê |
|---|---|---|
| Tipo de território como cadastro (G1) | **Manter** | Cadastro `TiposTerritorio` com código, ordem, "do sistema". Só classifica. |
| Árvore com território acima (G2: vínculo explícito, de cima soma os de baixo) | **Manter** e reforçar | Cliente vinculado a um nó só; agregação navega a árvore. + histórico da posição na árvore (T7). |
| Regra = condições do catálogo, versão encerra a anterior | **Melhorar** | Grupos OU + grupos de exclusão; versão imutável, com o texto dos critérios congelado. |
| Simular / Aplicar | **Melhorar** | Viram uma **operação planejada** (rascunho → simulada → aplicada) que empacota todas as mudanças; aplicar re-simula e recusa se a base mudou desde a revisão. |
| Manual prevalece (G3) | **Substituir** | Por exceções explícitas e datadas: *Fixar* e *Retirar* (+ ação "Mover", que gera as duas). O motor explica por que a regra perdeu. |
| Reaplicar à mão com aviso de diferença (G4) | **Manter** e generalizar | Vira o painel de **divergências de cobertura** (6 tipos). |
| Data retroativa até o limite (G5) | **Melhorar** | Também não antes da última operação aplicada no mesmo mapa (não reescrever o passado). |
| Responsáveis pessoa/equipe com vigência (G6) | **Manter** | Função = papel comercial. |
| Só clientes (G9) | **Melhorar** | Universo parametrizável por mapa (classificações), padrão Cliente. Prospects/leads usam a mesma chave (Pessoa). |
| "Cliente em vários territórios" (F6 A) | **Melhorar** | Vários territórios **em mapas diferentes**; dentro de um mapa exclusivo, um só (é isso que torna "conflito" um conceito definido). |
| Uma permissão `COMERCIAL.TERRITORIOS` | **Substituir** | Quatro permissões (T10). |
| 2b-2 (Meus territórios, filtro, carteira em data, indicadores) | **Deixar para depois** | Modelo abaixo já tem tudo que elas precisam. |

---

## B. Arquitetura proposta

### B.1 Conceitos

```
MAPA TERRITORIAL  (a "dimensão": Geografia, Segmentos, Contas estratégicas…)
│   empresa (nula = grupo todo) · exclusivo? · universo (classificações: Cliente)
│
├── TERRITÓRIO (nó da árvore)          ── TIPO (classificação livre)
│     ├── posição na árvore (pai) com vigência
│     ├── RESPONSÁVEIS (pessoa ou equipe · função = papel comercial · vigência)
│     └── COBERTURA versionada (v1, v2, v3 …)
│            grupos de inclusão (OU entre grupos, E dentro)
│            grupos de exclusão
│            prioridade (opcional)
│
├── EXCEÇÕES por cliente (Fixar em / Retirar de) com vigência e motivo
│
└── OPERAÇÃO TERRITORIAL  TE-2026-0001  (o "pacote de mudanças")
       rascunho → simulada → aplicada | cancelada
       contém: versões de cobertura novas, territórios novos/movidos/encerrados, exceções
       ao aplicar: publica tudo com a data de efeito e grava as ATRIBUIÇÕES

ATRIBUIÇÃO (resultado persistido)
   cliente × território (× mapa × empresa) · início/fim · origem · versão da regra · exceção · operação
```

**Por que um "mapa"** (T1): o pedido quer ao mesmo tempo (a) cliente em vários territórios (geográfico **e**
segmento **e** estratégico) e (b) conflito detectado quando duas regras disputam o mesmo cliente. As duas coisas só
convivem se existir uma fronteira que diga *quais territórios competem entre si*. O mapa é essa fronteira: dentro de um
mapa exclusivo o cliente tem um território; entre mapas, não há disputa. É o que os CRMs maduros chamam de *territory
model* / dimensão.

**Hierarquia ≠ regra** (item 7 do pedido): a árvore só diz onde o nó está e serve para agregar. Quem entra num território é
só a cobertura dele. A posição na árvore entra **uma única vez** no algoritmo, e de forma explícita: como desempate de
especificidade (passo 5 do algoritmo), nunca como herança de regra.

### B.2 Onde fica cada parte (camadas atuais)

| Camada | Peça |
|---|---|
| `Lone.Domain` | Entidades; `RegrasTerritorio` (árvore sem ciclo, vigências, estados); **`MotorAtribuicao`** puro: recebe candidatos, exceções, árvore e atribuições atuais → devolve resultado por cliente com explicação. 100% testável sem banco. |
| `Lone.Infrastructure` | `CoberturaTerritorialSql`: para cada grupo, `FiltrosPessoasSql.Aplicar(...)` → ids; une/subtrai no banco; devolve `(TerritórioId, PessoaId)` em lote. Repositórios. |
| `Lone.Application` | `TerritorioAppService` (cadastro), `OperacaoTerritorialAppService` (planejar, simular, aplicar, cancelar), `DivergenciasTerritoriaisAppService`, `ExplicacaoTerritorialAppService`. |
| `Lone.Contracts` | DTOs, rotas, permissões. |
| `Lone.Cliente` / `Lone.App` | Telas (seção G.5). |

---

## C. Modelo de dados (migration `Fase2bTerritorios`, só aditiva)

Todas as tabelas: `Id` (Guid sequencial), `CriadoEm`, `AtualizadoEm`; agregados com `Versao` (rowversion). Nada é
apagado: desativa, encerra ou cancela.

| # | Tabela | Finalidade | Campos principais | Chaves / índices / constraints |
|---|---|---|---|---|
| 1 | `TiposTerritorio` | Classificação livre (Geográfico, Segmento, Estratégico…) | Codigo, Nome, Descricao, Ordem, Sistema, Ativo | UQ Codigo; UQ Nome (sem acento/maiúscula, como os outros cadastros) |
| 2 | `MapasTerritoriais` | Dimensão de cobertura | Codigo, Nome, Descricao, EmpresaId?, Exclusivo, Ativo | UQ Codigo; FK EmpresaId → Pessoas |
| 3 | `MapaTerritorialClassificacoes` | Universo do mapa (quem pode ser atribuído) | MapaId, PapelId, Ativo | UQ (MapaId, PapelId); mesmo padrão de `TipoCarteiraClassificacao` |
| 4 | `Territorios` | Nó da árvore | MapaId, Codigo, Nome, TipoId, PaiId? (atual), Descricao, Prioridade?, Situacao (Planejado/Ativo/Encerrado), EncerradoEm?, OperacaoCriacaoId?, OperacaoEncerramentoId? | UQ (MapaId, Codigo); FK PaiId → Territorios (mesmo mapa, conferido no domínio e no serviço); CK Prioridade ≥ 1; IX (MapaId, PaiId) |
| 5 | `TerritorioPosicoes` | Histórico da posição na árvore | TerritorioId, PaiId?, InicioEm, FimEm?, OperacaoId? | CK FimEm ≥ InicioEm; UQ filtrado (TerritorioId) WHERE FimEm IS NULL; IX (PaiId, InicioEm) |
| 6 | `TerritorioResponsaveis` | Quem responde pelo território | TerritorioId, PessoaId?, EquipeId?, TipoCarteiraId (função), InicioEm, FimEm?, Ativo, Observacao | CK exatamente um de PessoaId/EquipeId; CK FimEm ≥ InicioEm; IX (PessoaId, FimEm), (EquipeId, FimEm) |
| 7 | `CoberturasTerritorio` | Versões da regra | TerritorioId, Numero (1,2,3…), Grupos (JSON), Criterios (texto congelado), Prioridade?, InicioEm?, FimEm?, Situacao (Rascunho/Vigente/Encerrada/Descartada), OperacaoId | UQ (TerritorioId, Numero); UQ filtrado (TerritorioId) WHERE Situacao = Vigente AND FimEm IS NULL; CK tamanho do JSON |
| 8 | `ExcecoesTerritorio` | Fixar / Retirar cliente | PessoaId, MapaId, TerritorioId, Tipo (Fixar/Retirar), InicioEm, FimEm?, Motivo, Situacao (Planejada/Vigente/Encerrada/Cancelada), OperacaoId, OperacaoEncerramentoId? | CK FimEm ≥ InicioEm; IX (PessoaId, MapaId, Situacao); regra "um Fixar vigente por cliente e mapa exclusivo" no domínio + UQ filtrado para o aberto |
| 9 | `OperacoesTerritoriais` | O documento `TE-` | Ano, Sequencia, MapaId, EfeitoEm, Motivo, Observacao, Situacao (Rascunho/Simulada/Aplicada/Cancelada), SimuladaEm?, AssinaturaSimulacao?, contagens (Analisados, Entram, Saem, Mudam, Permanecem, Conflitos, SemTerritorio, ExcecoesPreservadas), CriadaPor, AplicadaEm?, AplicadaPor?, CanceladaMotivo? | UQ (Ano, Sequencia); IX (MapaId, Situacao, EfeitoEm) |
| 10 | `OperacaoTerritorialItens` | Resultado aplicado, cliente a cliente (só quem muda + conflitos) | OperacaoId, PessoaId, Resultado (Entra/Sai/Muda/Conflito/SemTerritorio), TerritorioAnteriorId?, TerritorioNovoId?, AtribuicaoEncerradaId?, AtribuicaoNovaId?, Explicacao (JSON: candidatos, exceção, passo que decidiu) | IX (OperacaoId), (PessoaId); `[NaoAuditar]`, gravado uma vez |
| 11 | `AtribuicoesTerritorio` | **A atribuição efetiva** | PessoaId, TerritorioId, MapaId, EmpresaId?, InicioEm, FimEm?, Origem (Regra/Exceção/Importação/Migração), CoberturaId?, ExcecaoId?, OperacaoId, OperacaoEncerramentoId?, Ativo | CK FimEm ≥ InicioEm; **UQ filtrado (PessoaId, MapaId) WHERE FimEm IS NULL AND Ativo = 1** (mapa exclusivo); IX (TerritorioId, FimEm) INCLUDE (PessoaId), IX (PessoaId, InicioEm) |
| 12 | `NumeracoesDocumento` | Numerador único do ERP (T12) | Prefixo, Ano, Ultimo | PK (Prefixo, Ano); incremento com bloqueio de linha na mesma transação |

Observações:
- **Mapa não exclusivo** (ex.: "Campanhas"): o índice único filtrado da tabela 11 não pode valer para ele. Duas opções
  no SQL Server: índice filtrado usando a coluna `Exclusivo` copiada na atribuição (`WHERE FimEm IS NULL AND Ativo = 1 AND
  Exclusivo = 1`). Recomendado: copiar.
- **Ciclo na árvore**: conferido no domínio (percorre os ancestrais do novo pai) e de novo dentro da transação, com a
  árvore do mapa lida com bloqueio. O SQL Server não tem constraint de ciclo; um gatilho seria a alternativa (T7).
- **Grupos (JSON)**: tipo fechado `RegraCobertura { Incluir: List<GrupoCondicoes>, Excluir: List<GrupoCondicoes> }`,
  cada grupo = `List<CondicaoFiltro>`, como os filtros salvos. Nunca SQL nem fórmula. `Criterios` guarda o texto
  legível do momento da publicação ("UF = MG e CNAE = 4722-9/01"), porque nomes de etiquetas e campos podem mudar
  depois e a explicação histórica não pode mudar junto.
- **Tabelas que *não* criei** e por quê: `RegrasTerritorio` separada de `Coberturas` (um território tem uma cobertura
  com versões; uma segunda entidade seria só um nível a mais), `TerritorioVersoes` (a árvore versiona pela tabela de
  posições; o resto do território é cadastro auditado), tabela de simulação (o resultado da simulação é recalculado
  sob demanda; guardar só a assinatura evita gravar e depois ter que apagar rascunhos).

---

## D. Algoritmo de atribuição (por mapa, numa data de efeito)

Entrada: o mapa, a data `D`, a cobertura que valerá em `D` de cada território (vigente ou a do rascunho da operação), as
exceções que valerão em `D` e as atribuições atuais. Tudo em lote; o motor do domínio não acessa o banco.

```
1. UNIVERSO      pessoas com uma das classificações do mapa, ativas (situação Ativo/Em análise)
2. CANDIDATOS    para cada território ATIVO do mapa com cobertura em D:
                   (Incluir₁ ∪ Incluir₂ ∪ …) − (Excluir₁ ∪ Excluir₂ ∪ …)   ∩ UNIVERSO
                 → pares (cliente, território, versão da cobertura)            [banco, em lote]
3. RETIRAR       exceção "Retirar de T" vigente em D → remove T dos candidatos do cliente
                 (registra: "atende à regra de T, mas foi retirado por exceção X")
4. FIXAR         exceção "Fixar em T" vigente em D → T é o resultado, qualquer que seja a regra
                 (registra os candidatos que perderam: "perdeu para a exceção X")
5. ESPECIFICIDADE (só mapa exclusivo, com 2+ candidatos)
                 a) prioridade explícita: menor número vence; sem prioridade = depois de todas
                 b) empate: se um candidato é DESCENDENTE do outro na árvore, o mais profundo vence
                    (MG × Curvelo, Curvelo abaixo de MG → Curvelo)
                 c) ainda empatado → CONFLITO (nenhum vence)
6. CONFLITO      sem vencedor: se a atribuição atual do cliente é um dos empatados, ela é mantida
                 (estabilidade) e o conflito fica aberto; senão o cliente fica sem território neste mapa.
                 Nunca "o primeiro da lista".
7. RESULTADO     por cliente: Entra | Sai | Muda | Permanece | Conflito | SemTerritório,
                 com a explicação (candidatos, passo que decidiu, exceção, versão)
8. GRAVAÇÃO      (só na aplicação) encerra na véspera de D quem sai/muda; abre a nova atribuição com
                 origem, versão e operação; grava os itens da operação
```

Propriedades exigidas no pedido:
- **Determinístico e reproduzível**: a ordem de avaliação não influencia; mesmos dados + mesma data = mesmo resultado
  (o teste roda o motor com a entrada embaralhada).
- **Explicável**: todo resultado carrega o passo que decidiu (3, 4, 5a, 5b, 6) e os candidatos.
- **Auditável**: o que foi aplicado fica nos itens da operação, com a explicação congelada.
- Mapa **não exclusivo**: passos 5 e 6 não existem; o cliente fica em todos os candidatos que sobraram.

---

## E. Máquinas de estado

```
TERRITÓRIO     Planejado ──(operação aplicada)──► Ativo ──(operação que encerra)──► Encerrado
               (criado dentro de uma operação;     (recebe atribuições)            (atribuições encerradas
                não aparece para ninguém fora dela)                                  na mesma operação)

COBERTURA      Rascunho ──(aplicada)──► Vigente ──(nova versão aplicada)──► Encerrada (fim = véspera)
               Rascunho ──(operação cancelada)──► Descartada

OPERAÇÃO       Rascunho ◄──(editar)── Simulada
               Rascunho ──(simular)──► Simulada ──(aplicar: re-simula e confere a assinatura)──► Aplicada
               Rascunho / Simulada ──(cancelar, com motivo)──► Cancelada
               (Aplicada nunca volta: corrige-se com outra operação)

ATRIBUIÇÃO     Vigente ──(operação)──► Encerrada (FimEm)       Futura = InicioEm > hoje (mesma linha)
               Ativo = falso só para lançamento errado de importação/migração (fica no histórico)

EXCEÇÃO        Planejada ──(aplicada)──► Vigente ──(fim ou operação)──► Encerrada
               Planejada ──(operação cancelada)──► Cancelada
```

Estados que **não** criei: "Em revisão" e "Aprovado". Hoje o Lone não tem fluxo de aprovação e a separação real pedida
(configurar ≠ pôr em vigor) já é garantida por Rascunho/Simulada × Aplicada, com permissões diferentes para planejar e
aplicar. A máquina aceita um estado "Aguardando aprovação" entre Simulada e Aplicada no futuro, sem migração de dados
(sugestão S4).

---

## F. Fluxo de simulação

```
[Operação em rascunho]
   │  mudanças planejadas: regra nova de "Açougues MG", exceção "Fixar ABC em Grandes Contas", efeito 01/10
   ▼
Simular ──► motor (seção D) com as coberturas/exceções do RASCUNHO no lugar das vigentes
   │
   ▼
Resumo                                       Lista (paginada, filtrável, cada linha abre a explicação)
 Analisados 1.250 · Entram 87 · Saem 42       ABC Carnes  Açougues MG → Grandes Contas   passo 4 (exceção)
 Mudam 12 · Permanecem 1.109 · Conflitos 5    Açougue S.J.  — conflito: MG Norte × Curvelo  passo 5c
 Sem território 3 · Exceções preservadas 18
   │
   ▼
Grava na operação: Situação = Simulada, SimuladaEm, contagens e a ASSINATURA (hash dos pares cliente→resultado)
Não grava atribuição, não mexe na carteira, não gera nenhum efeito comercial.
```

## G. Fluxo de aplicação

```
Aplicar (permissão APLICAR, motivo já na operação)
   │ 1. confere: operação Simulada · efeito dentro do limite · efeito ≥ última operação aplicada no mapa
   │ 2. re-executa o motor
   │ 3. assinatura diferente da simulada? ──► recusa: "A base mudou desde a simulação (N clientes).
   │                                          Simule de novo e revise."  (operação volta a Rascunho)
   ▼ 4. UMA transação (tudo ou nada):
        · número TE-2026-0001 (numerador)
        · territórios Planejados → Ativos; posições novas; encerrados
        · coberturas: anterior encerrada na véspera, rascunho → Vigente a partir do efeito
        · exceções Planejadas → Vigentes
        · atribuições: encerra (FimEm = véspera) as que saem/mudam; abre as novas
        · itens da operação (só mudanças e conflitos) com a explicação congelada
        · operação → Aplicada (AplicadaEm, AplicadaPor, contagens finais)
   ▼
Resultado na tela = o mesmo resumo, agora com o número do documento.
Falha em qualquer ponto → rollback completo; a operação continua Simulada.
```

Diferença consciente em relação à transferência de carteira (que grava cliente a cliente): um realinhamento
territorial aplicado pela metade deixaria o mapa incoerente (clientes em dois territórios, conflitos falsos). Por isso
aqui é tudo ou nada (T11).

## H. Fluxo de reaplicação (e divergências)

```
Painel "Divergências de cobertura" do mapa  (motor rodando HOJE, sem gravar, contra as atribuições atuais)
   ├─ Fora da regra ............ atribuído, mas a cobertura vigente não o encontra mais
   ├─ Novo que atende .......... sem atribuição neste mapa e com candidato
   ├─ Em conflito .............. empate não resolvido (inclui os mantidos por estabilidade)
   ├─ Sem território ........... no universo e sem candidato
   ├─ Por exceção .............. atribuído por Fixar (informativo; mostra o fim da exceção)
   └─ Regra vencida ............ atribuído por uma versão que já foi encerrada e não por outra
        │
        ▼ ações (todas criam ou completam uma operação em rascunho, nunca gravam direto)
        Simular reaplicação · Manter por exceção (Fixar) · Retirar · Mover para outro território
        │
        ▼
     Simular → revisar → Aplicar (seções F e G)
```

Nada muda sozinho quando o cliente troca de cidade, CNAE, porte ou etiqueta. Na ficha do cliente, aba Comercial, o
aviso aparece para aquele cliente: "Atribuição desatualizada em Geografia: o cliente não atende mais a MG Norte".

---

## I. Histórico: como cada pergunta é respondida

| Pergunta | Resposta (sem inferência) |
|---|---|
| Onde este cliente estava há seis meses? | `AtribuicoesTerritorio` com `InicioEm ≤ d ≤ FimEm` (ou aberta), por mapa. |
| Por que ele está neste território? | A atribuição vigente: origem, versão da cobertura (número + critérios congelados), exceção, operação (número, data de efeito, quem aplicou, motivo). |
| Por que NÃO foi para aquele? | Hoje: o motor explica o cliente ao vivo (candidatos, exceções, passo que decidiu). No passado: a `Explicacao` do item da operação que o colocou onde está. |
| Qual regra valia para o território em 15/08? | A versão de `CoberturasTerritorio` com vigência nessa data. |
| O que era o "Sudeste" em março? | `TerritorioPosicoes` na data → a árvore daquele dia (para indicadores e metas históricas). |
| Quem era responsável pelo território / pelo cliente em uma data? | `TerritorioResponsaveis` na data; o vendedor continua vindo da carteira na data (tela já existente). |
| Quem executou, quando e por quê? | A operação (usuário, data/hora, efeito, motivo) + `RegistroAuditoria` dos cadastros. |

Data de efeito, data solicitada e data da operação: a operação guarda `EfeitoEm` (pedida e efetiva são a mesma: o
sistema não ajusta a data escolhida; se ela não é aceita, recusa) e `AplicadaEm` (quando foi executada).

---

## J. Compatibilidade futura (a pergunta do item 42)

| Módulo futuro | Como usa sem quebrar o modelo |
|---|---|
| **Vendas / pedidos** | O pedido grava o território do cliente **no momento da venda** (cópia do `TerritorioId` por mapa). Relatório de venda por território não depende de recalcular o passado. |
| **Oportunidades** | Mesma cópia na criação; a oportunidade pode herdar o território do cliente ou ser atribuída pelo mesmo motor (a chave é a Pessoa). |
| **Leads / prospects** | Entram mudando o **universo do mapa** (classificação Prospect). Nenhuma tabela nova para eles. |
| **Carteiras** | Continuam independentes; "Sincronizar carteira pelo território" pode virar uma ação que gera uma **transferência de carteira** (mecanismo já existente) a partir dos responsáveis (sugestão S1). |
| **Comissões** | Pagam pelo território gravado na venda + responsáveis na data (vigência) — nada é recalculado. |
| **Metas** | `NivelParticipante` ganha `Territorio`; o realizado soma os clientes atribuídos ao nó e aos descendentes pela árvore da data (`TerritorioPosicoes`). |
| **Indicadores** | Clientes por território/responsável/origem, entradas e saídas, permanência média, conflitos: tudo sai de `AtribuicoesTerritorio` + itens das operações. Nenhum dado reconstruído. |
| **Equipes / "Meus territórios" (2b-2)** | Usuário → Pessoa → (Equipe) → `TerritorioResponsaveis` vigente → territórios → descendentes → atribuições. Entra como mais uma fonte no `RegrasEscopo`, sem mudar tabela. |
| **Multiempresa** | O mapa tem `EmpresaId` (nulo = grupo todo); a atribuição copia a empresa. Duas empresas podem ter mapas próprios ou compartilhar um. A mesma pessoa pode estar no território X da empresa A e no Y da empresa B sem conflito (mapas diferentes). |

---

## K. Regras de negócio (tabela pedida no item 38)

| Regra | Comportamento |
|---|---|
| Cliente atende uma regra | Candidato único → atribuído com origem Regra, versão e operação. |
| Cliente atende várias regras | Mapa exclusivo: prioridade → mais profundo na árvore → senão conflito. Mapas diferentes: fica em todos. |
| Cliente não atende nenhuma | Sem território naquele mapa; aparece em "Sem território". Se tinha atribuição, ela é encerrada **só** quando uma operação for aplicada. |
| Cliente possui exceção (Fixar) | Vence a regra enquanto vigente; os candidatos perdedores ficam na explicação. |
| Inclusão manual | É o "Fixar" (num mapa não exclusivo, soma aos outros territórios). |
| Exclusão manual | É o "Retirar": o território deixa de ser candidato; o cliente cai no próximo candidato ou fica sem. |
| Override (A → B) | Ação "Mover": cria Retirar de A + Fixar em B na mesma operação, com o mesmo motivo. |
| Regra muda | Nova versão em rascunho dentro de uma operação; ao aplicar, a anterior encerra na véspera do efeito. |
| Território muda de pai | Posição nova com vigência a partir do efeito; a anterior encerra. Atribuições não mudam (são do nó). |
| Responsável muda | Encerra o vínculo atual (fim) e inclui o novo (início). Não passa por operação (não muda atribuição). |
| Cliente deixa de atender | Divergência "Fora da regra"; nada muda até uma operação ser aplicada. |
| Regra vencida | Versão encerrada sem substituta → divergência "Regra vencida"; a próxima operação reavalia. |
| Território encerrado | Só por operação: suas atribuições encerram na véspera do efeito e os clientes são reavaliados no mesmo motor (podem ir para outro nó). |
| Reaplicação | Sempre: simular → revisar → aplicar. Nunca automática nesta fase. |
| Simulação | Não grava atribuição, carteira nem efeito comercial; grava só o resumo e a assinatura na operação. |
| Aplicação | Grava tudo da seção G, em uma transação, com número de documento. |
| Data retroativa | Até `DiasRetroativosMaximo` e nunca antes do efeito da última operação aplicada no mesmo mapa. |
| Conflito | Não resolvido nunca escolhe sozinho; mantém a atual se ela é uma das empatadas. Resolve-se com prioridade ou exceção. |
| Multiempresa | Isolado por mapa (empresa do mapa); agregação do grupo = mapas com empresa nula. |
| Cliente novo | Fica sem território até a próxima operação (aparece em "Novo que atende"). |
| Pessoa desativada | Sai do universo; a próxima operação encerra a atribuição (e mostra como "Sai"). |

---

## L. Segurança e permissões

| Permissão | Dá direito a |
|---|---|
| `TERRITORIOS.VISUALIZAR` | Ver mapas, árvore, responsáveis, regras, operações, divergências e o histórico. Lista de clientes filtrada pelo alcance de quem consulta (escopo 2a). |
| `TERRITORIOS.CONFIGURAR` | Tipos, mapas (universo, exclusividade) e responsáveis. |
| `TERRITORIOS.PLANEJAR` | Criar/editar operações em rascunho (territórios novos, mover, encerrar, regras, exceções) e simular. |
| `TERRITORIOS.APLICAR` | Aplicar e cancelar operações. |

Por que quatro e não dez: simular não tem efeito e faz parte de planejar; exceção e encerramento só existem dentro de
uma operação (logo, planejar + aplicar já as controlam); histórico faz parte de ver. Na 2b-1, planejar e aplicar exigem
alcance **Tudo** (a operação mexe no mapa inteiro); restringir ao próprio território fica para a 2b-2.

---

## M. Performance

- O motor é por **conjunto**, nunca por cliente: uma consulta por grupo de condições devolve só ids; união e subtração no
  banco (`UNION`/`EXCEPT` via LINQ) ou em memória (100 mil Guids ≈ 1,6 MB).
- Estimativa: 100 mil clientes × 200 territórios com 2 grupos cada = ~400 consultas indexadas (os campos do catálogo já
  usam índices), e a memória guarda só os pares que batem.
- Gravação: só as mudanças (entram/saem/mudam), em lotes de 1.000 dentro da mesma transação.
- Simulação paginada na tela; contagens vêm do resultado já calculado.
- Se um mapa passar do limite prático (ex.: 300 mil clientes ou 60 s), a aplicação vira tarefa em segundo plano com a
  mesma lógica (sugestão S6). Não é necessário agora.

---

## N. Testes (além dos do pedido, item 39)

- **Domínio (motor puro)**: 1, 2 e 3 candidatos; prioridade; profundidade; empate → conflito; conflito mantendo a atual;
  Fixar × regra; Retirar → cai no próximo; Mover; mapa não exclusivo; entrada embaralhada = mesmo resultado; universo.
- **Árvore**: criar, mover, ciclo direto e indireto (A→B→C→A), mover para outro mapa (recusado), encerrar com
  descendentes ativos (recusado), posição histórica em data.
- **Cobertura**: versão nova encerra a anterior na véspera; vigência em data; grupos OU; grupos de exclusão; campo não
  permitido em cobertura recusado; critérios congelados.
- **Operação**: rascunho → simulada → aplicada; aplicar sem simular (recusado); base mudou → recusado; cancelar;
  retroativo dentro/fora do limite; antes da última operação (recusado); número único sob concorrência; rollback total
  quando um passo falha.
- **Permissões**: só visualizar; planejar sem aplicar; aplicar; alcance restrito recusado para planejar.
- **Integridade** (testes de banco na Infraestrutura): duas atribuições abertas no mesmo mapa exclusivo; responsável
  com pessoa e equipe; período invertido; duas versões vigentes.
- **Arquitetura**: nenhuma consulta de pessoas nova fora do `CoberturaTerritorialSql` sem o comentário "Sem escopo:".

---

## O. Decisões estruturais para aprovação (recomendação técnica em cada uma)

**T1 — O que define quais territórios competem por um cliente**
- A: **Mapa territorial** explícito (dimensão), com empresa, universo e "exclusivo". *(recomendada)*
- B: A raiz de cada árvore é a dimensão (sem entidade nova).
- C: O tipo do território decide (flag "exclusivo" no tipo).
- Consequência: B quebra quando uma árvore precisa mudar de dimensão ou ter universo próprio; C amarra comportamento ao
  tipo, o que o pedido pede para evitar. A custa uma tabela e dá lugar natural para empresa e universo.

**T2 — Exceções**
- A: Duas primitivas (**Fixar em**, **Retirar de**) + a ação "Mover" que cria as duas. *(recomendada)*
- B: Três tipos gravados (Inclusão, Exclusão, Override).
- Consequência: em B, "Override A→B" é exatamente "Retirar A + Fixar B"; guardar três tipos cria dois jeitos de dizer a
  mesma coisa e o motor teria de tratar combinações. A explicação na tela continua dizendo "Movido de A para B".

**T3 — Algoritmo de resolução**: exceção → prioridade explícita → mais profundo na árvore → conflito (mantém a atual se
empatada). *(recomendada)*. Alternativa B: sem profundidade (só prioridade; qualquer empate é conflito) — mais simples,
mas gera conflito em todo caso "MG × Curvelo", que é o caso comum.

**T4 — Território × carteira**
- A: Independentes nesta fase; sincronização futura por transferência de carteira. *(recomendada)*
- B: O responsável do território vira automaticamente o vendedor do cliente.
- Consequência: B cria duas fontes de verdade para "quem atende" e mexe em crédito da venda e metas já em uso.

**T5 — Separar planejar de pôr em vigor**
- A: Operação como pacote (rascunho → simulada → aplicada), com re-simulação e assinatura na aplicação. *(recomendada)*
- B: Editar a regra direto e só a atribuição passar por simular/aplicar.
- Consequência: em B, a regra editada já vale para as divergências e para o painel antes de alguém aplicar — exatamente a
  mistura que o pedido quer evitar.

**T6 — Linguagem das regras**
- A: Grupos em forma normal disjuntiva (OU entre grupos, E dentro) + grupos de exclusão, usando `FiltrosPessoasSql` como
  está, e só campos marcados `UsavelEmCobertura`. *(recomendada)*
- B: Árvore livre de E/OU aninhados.
- Consequência: A cobre todos os exemplos do pedido e reaproveita o painel atual como editor de cada grupo; B exige
  editor novo e é difícil de explicar para quem lê a regra. A pode evoluir para B sem migrar dados (o JSON ganha um nível).

**T7 — Histórico da árvore**
- A: Tabela de posições com vigência (+ pai atual no território). Ciclo conferido no domínio e na transação. *(recomendada)*
- B: Só a auditoria guarda a mudança de pai.
- Consequência: sem A, metas e indicadores "por região em março" teriam de reconstruir a árvore pela auditoria.

**T8 — Data retroativa**: até o limite dos parâmetros **e** não antes da última operação aplicada no mesmo mapa.
*(recomendada)*. B: só o limite (permite aplicar uma operação "no meio" de outra, reescrevendo o que ela fez).

**T9 — Quem pode ser atribuído**: universo por mapa (classificações), padrão Cliente. *(recomendada)*. B: fixo em Cliente.

**T10 — Permissões**: as quatro da seção L; planejar e aplicar exigem alcance Tudo na 2b-1. *(recomendada)*

**T11 — Transação da aplicação**: tudo ou nada. *(recomendada)*. B: por cliente, como a transferência.

**T12 — Numeração de documentos**
- A: Tabela `NumeracoesDocumento` (prefixo + ano), usada pelo `TE-`; o `TR-` passa a usá-la numa entrega separada, sem
  mudar números já emitidos. *(recomendada)*
- B: Copiar o `MAX+1` da transferência.
- Consequência: serve a pedidos, notas e oportunidades depois; B espalha o mesmo risco de concorrência.

**T13 — Tamanho das entregas**: a 2b-1 ficou grande. Proposta:
- **2b-1a — Estrutura**: migration completa; tipos, mapas, territórios, árvore (posições, ciclo), responsáveis; telas de
  cadastro e árvore; numerador. (Territórios criados aqui nascem Ativos sem cobertura — ou só dentro de operação, ver
  T14.)
- **2b-1b — Motor**: coberturas, exceções, operação, simular, aplicar, divergências, explicação do cliente, aba
  Comercial da ficha.
- Cada uma testada por você antes da próxima. *(recomendada)*

**T14 — Criar/mover/encerrar território fora de operação**
- A: Criar e mover livremente enquanto o território **não tem atribuição nem cobertura vigente**; depois disso, só por
  operação. *(recomendada)*
- B: Sempre por operação.
- Consequência: B deixa montar a árvore inicial lento e burocrático; A mantém o controle onde há efeito operacional.

---

## P. Telas (2b-1)

1. **Comercial › Territórios**: seletor de mapa; árvore à esquerda (busca, contagem de clientes por nó e total com os
   descendentes); ficha do território à direita com abas *Dados*, *Responsáveis*, *Regra* (versão vigente + histórico de
   versões), *Clientes* (paginada, respeita o alcance), *Histórico*.
2. **Operações territoriais**: lista (número, mapa, efeito, situação, contagens); a operação em rascunho mostra as
   mudanças planejadas, o botão Simular, o resumo da seção F com filtros por resultado, e Aplicar.
3. **Divergências**: os seis grupos da seção H com as ações.
4. **Explicar cliente** (botão na ficha e em cada linha): o quadro do item 47 — território, origem, regra/versão,
   critérios, operação, quem aplicou; e "por que não foi para X" ao escolher outro território.
5. **Cadastros**: tipos de território; mapas (universo e exclusividade).

---

## Q. Sugestões além do pedido

- **S1 — Sincronizar carteira pelo território** (depois): uma ação que, a partir dos responsáveis vigentes, **monta uma
  transferência de carteira em prévia**. Reusa o mecanismo `TR-` inteiro e mantém a carteira como fonte única.
- **S2 — Território gravado nos documentos** (Vendas/Oportunidades): cópia no momento do documento; comissão e
  indicador nunca dependem do recalculado.
- **S3 — Planejamento anual**: como a operação aceita efeito futuro, o realinhamento de 01/01 pode ser montado,
  simulado e revisado em dezembro e aplicado com antecedência; as atribuições futuras aparecem como "a partir de".
- **S4 — Dupla checagem** (parâmetro do mapa, depois): "quem planeja não aplica", com o estado "Aguardando aprovação".
- **S5 — Capacidade** (F8 já aprovado): aviso na simulação quando um território/responsável passa do número de clientes
  configurado.
- **S6 — Processamento em segundo plano** quando um mapa ficar grande (mesma lógica, sem mudar dados).
- **S7 — Mapa visual** por UF/município (cobertura e clientes sem território) quando entrarem os indicadores.
- **S8 — Atribuição ao salvar o cliente** (parâmetro do mapa, depois): o motor já trabalha por conjunto; para um
  cliente só, é o mesmo cálculo com o universo reduzido a ele.
