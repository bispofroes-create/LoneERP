# Fase 2b — Motor de Cobertura e Atribuição Territorial (plano consolidado)

> **Situação (30/09/2026, 05h25):** 2b-1a commitada (`af3924c`). **2b-1b implementada e validada, sem commit**:
> build 0/0, 1704 testes aprovados (SQL Server padrão e RCSI, concorrência, volume de 50.000 clientes), migrations
> `Fase2b1bNumeracao` e `Fase2b1bMotor` geradas e auditadas, **não aplicadas no LoneERP**. Relatório na seção 22;
> aguardando a revisão do usuário e a autorização para Update-Database (com a API desligada), testes pós-migração e commit.
>
> **Situação anterior (29/09/2026, 07h19):** arquitetura aprovada. Decisões **T1 a T20 aprovadas** (opção A), com o complemento
> obrigatório das **fixações múltiplas** no T3 e a definição de **desfazer operação futura** (T15) como ação de negócio
> registrada. Autorizada a **2b-1a** (estrutura). A 2b-1b (motor) só começa depois da 2b-1a compilada e testada pelo
> usuário. Este documento substitui a revisão de 28/09 e a seção R de 29/09; o histórico da discussão está no git
> (commit `ceffbbb` e seguinte).

---

## 1. Decisões aprovadas

| # | Decisão | O que ficou valendo |
|---|---|---|
| T1 | O que define quem compete por um cliente | **Mapa territorial** = dimensão independente de atribuição (seção 2) |
| T2 | Exceções | Só **Fixar em** e **Retirar de** (+ ação "Mover" = as duas), com vigência, motivo, usuário, data, origem e histórico |
| T3 | Algoritmo | Seção 4, com o complemento das fixações múltiplas e das fixações inválidas |
| T4 | Território × carteira | Independentes; transferência de carteira a partir do território fica para depois (S1) |
| T5 | Planejar × pôr em vigor | Operação `TE-`: rascunho → simulada (revisão) → aplicada; aplicar re-simula e confere a assinatura; tudo ou nada |
| T6 | Linguagem das regras | Grupos de inclusão (OU entre grupos, E dentro) + grupos de exclusão, sobre o catálogo de filtros, só campos `UsavelEmCobertura` |
| T7 | Histórico da árvore | `TerritorioPosicoes` com vigência |
| T8 | Data retroativa | Até `DiasRetroativosMaximo` e nunca antes do efeito da última operação aplicada no mapa; data da operação, data de efeito, usuário e motivo gravados separadamente |
| T9 | Universo | Classificações por mapa (padrão Cliente) |
| T10 | Permissões | `TERRITORIOS.VISUALIZAR`, `.CONFIGURAR`, `.PLANEJAR`, `.APLICAR`; planejar e aplicar exigem alcance Tudo |
| T11 | Transação | Aplicação tudo ou nada |
| T12 | Numeração | `NumeracoesDocumento`, segura para concorrência (seção 9) |
| T13 | Entregas | 2b-1a (estrutura) e 2b-1b (motor), separadas |
| T14 | Território sem uso | Cria, renomeia, move e reorganiza livremente; com uso, nada é apagado nem reescrito (encerra/versiona) |
| T15 | Desfazer operação futura | Permitido só antes do efeito; é uma ação registrada (seção 8), nunca apaga a operação |
| T16 | Endereço de referência | Finalidade parametrizável no mapa (padrão Comercial); nunca "Comercial" fixo no motor |
| T17 | Planejado × publicado | Planejado em `OperacaoTerritorialMudancas`; tabelas de fatos só recebem o que foi aplicado; várias operações em rascunho por mapa |
| T18 | Mover com uso | Exige operação se o nó **ou qualquer descendente** tem regra ou atribuição |
| T19 | Migrations | Uma por entrega; a 2b-1a cria só as tabelas da estrutura |
| T20 | Nome | **Regra do território** (nunca "cobertura", que no Lone são as ausências) em entidades, banco, serviços, telas, documentação e mensagens |

Princípio aprovado para os módulos futuros: **P-T1 a P-T3** (seção 13).

---

## 2. Conceitos

| Conceito | O que é | O que **não** é |
|---|---|---|
| **Mapa territorial** | Uma **dimensão independente de atribuição**: define quais territórios disputam um cliente. Dentro de um mapa exclusivo, no máximo um território por cliente; entre mapas diferentes nunca há disputa nem conflito. Tem empresa, universo, exclusividade e finalidade do endereço de referência. | Não é pasta. No domínio é a fronteira do algoritmo: o motor sempre roda **um mapa por vez**. |
| **Território** | Nó da árvore do mapa: posição (pai), tipo, responsáveis. | Não diz quem entra: quem diz é a regra. A árvore agrega e entra só no desempate por especificidade (passo 5b). |
| **Regra do território** | O que torna um cliente **candidato**: grupos de inclusão menos grupos de exclusão, mais a prioridade. Versionada (v1, v2…), imutável depois de publicada, critérios congelados em texto. | Não é a atribuição: regra nova não muda cliente nenhum até uma operação ser aplicada. |
| **Exceção** | Decisão humana datada sobre um cliente: Fixar em T / Retirar de T. | Não altera a regra; prevalece enquanto vigente. |
| **Atribuição** | Resultado gravado: cliente × território, com período, origem, versão da regra, exceção e operação. | Não é recalculada ao consultar. |
| **Operação TE-** | Pacote que planeja, simula e aplica o que muda atribuições. | Não cobre cadastro, responsáveis e árvore sem uso (T14/T18). |

Exemplo: o mesmo cliente está em *Geografia → Curvelo*, *Segmentos → Grandes Açougues* e *Estratégico → Key Account*,
sem conflito nenhum: são três mapas.

---

## 3. Modelo de dados (13 tabelas)

Todas: `Id` (Guid sequencial), `CriadoEm`, `AtualizadoEm`; agregados com `Versao` (rowversion). Nada é apagado.

| # | Tabela | Entrega | Campos principais | Integridade |
|---|---|---|---|---|
| 1 | `TiposTerritorio` | **2b-1a** | Codigo, Nome, Descricao, Ordem, Ativo | UQ Codigo; UQ Nome (sem acento/maiúscula). Três de sistema com Id fixo: Geográfico, Segmento, Estratégico |
| 2 | `MapasTerritoriais` | **2b-1a** | Codigo, Nome, Descricao, EmpresaId?, Exclusivo, FinalidadeEnderecoReferenciaId, Ativo | UQ Codigo; UQ Nome; FK Empresa → Pessoas; FK Finalidade → FinalidadesEndereco. Na 2b-1b: UQ (Id, Exclusivo), UltimaOperacaoId, UltimoEfeitoEm |
| 3 | `MapaTerritorialClassificacoes` | **2b-1a** | MapaId, PapelId, Ativo | UQ (MapaId, PapelId); FK → Papeis |
| 4 | `Territorios` | **2b-1a** | MapaId, Codigo, Nome, TipoId, PaiId? (atual), Descricao, Situacao (Ativo/Encerrado), FimEm? | **AK (MapaId, Id)**; **FK composta (MapaId, PaiId) → (MapaId, Id)**; UQ (MapaId, Codigo); UQ filtrado (MapaId, PaiId, Nome) WHERE Situacao = Ativo; CK Situacao/FimEm coerentes |
| 5 | `TerritorioPosicoes` | **2b-1a** | MapaId, TerritorioId, PaiId?, InicioEm, FimEm?, Ativo | FK composta do território e do pai; CK FimEm ≥ InicioEm; UQ filtrado (TerritorioId) WHERE FimEm IS NULL AND Ativo = 1. Na 2b-1b: OperacaoId, OperacaoEncerramentoId |
| 6 | `TerritorioResponsaveis` | **2b-1a** | TerritorioId, PessoaId?, EquipeId?, TipoCarteiraId (função), InicioEm, FimEm?, Observacao, Ativo | CK exatamente um de Pessoa/Equipe; CK FimEm ≥ InicioEm; FKs sem cascata; **gatilho `TR_TerritorioResponsaveis_SemSobreposicao`** (mesmo território + função + pessoa ou equipe, ativos, sem períodos cruzados; aprovado em 29/09) |
| 7 | `RegrasTerritorio` | 2b-1b | TerritorioId, Numero, Grupos (JSON), Criterios (texto congelado), Prioridade?, InicioEm, FimEm?, Ativo, OperacaoId, OperacaoEncerramentoId? | UQ (TerritorioId, Numero); UQ filtrado da versão aberta; CK tamanho do JSON; CK Prioridade ≥ 1 |
| 8 | `ExcecoesTerritorio` | 2b-1b | PessoaId, MapaId, TerritorioId, Exclusivo (cópia), Tipo, InicioEm, FimEm?, Motivo, Origem, Situacao (Vigente/Encerrada/Anulada), OperacaoId, OperacaoEncerramentoId? | FK composta do território; FK (MapaId, Exclusivo) → Mapas(Id, Exclusivo); UQ filtrado do Fixar aberto por cliente em mapa exclusivo |
| 9 | `OperacoesTerritoriais` | 2b-1b | Ano, Sequencia, MapaId, EfeitoEm, Motivo, Observacao, Situacao (Rascunho/Simulada/Aplicada/Cancelada/Desfeita), SimuladaEm, AssinaturaSimulacao, AtributosAvaliadosEm, contagens, CriadaPor/Em, AplicadaPor/Em, CanceladaPor/Em/Motivo, DesfeitaPor/Em/Motivo/SolicitadaEm | UQ (Ano, Sequencia); IX (MapaId, Situacao, EfeitoEm) |
| 10 | `OperacaoTerritorialMudancas` | 2b-1b | OperacaoId, Ordem, Tipo, TerritorioId?, PessoaId?, ExcecaoId?, Dados (JSON de tipo fechado) | FKs reais; IX (OperacaoId) |
| 11 | `OperacaoTerritorialItens` | 2b-1b | OperacaoId, PessoaId, Resultado, TerritorioAnteriorId?, TerritorioNovoId?, AtribuicaoEncerradaId?, AtribuicaoNovaId?, Explicacao (JSON) | `[NaoAuditar]`, gravado uma vez |
| 12 | `AtribuicoesTerritorio` | 2b-1b | PessoaId, TerritorioId, MapaId, Exclusivo (cópia), InicioEm, FimEm?, Origem, RegraId?, ExcecaoId?, OperacaoId, OperacaoEncerramentoId?, Ativo | FK composta do território; FK (MapaId, Exclusivo) → Mapas; UQ filtrado (PessoaId, MapaId) WHERE FimEm IS NULL AND Ativo = 1 AND Exclusivo = 1; sem EmpresaId (vem do mapa) |
| 13 | `NumeracoesDocumento` | 2b-1b | Prefixo, Ano, Ultimo | PK (Prefixo, Ano) |

Não existem: `TerritorioVersoes` (a árvore versiona por posições), tabela de simulação (recalculada; só a assinatura é
guardada), tabela de "mover" (é uma mudança da operação), estados de rascunho nas tabelas de fatos (T17).

---

## 4. Algoritmo de resolução (T3)

Entrada, lida em lote (o motor do domínio `MotorAtribuicao` não acessa banco): mapa `M`, data de efeito `D`, árvore de
`M` em `D`, regra de cada território ativo em `D` (a publicada ou a planejada na operação) com o conjunto de clientes
que a satisfaz, exceções em `D`, atribuições da véspera.

```
0. UNIVERSO     cliente fora do universo de M em D → resultado vazio ("fora do universo")

1. CANDIDATOS   C = { T ativo em D | T tem regra em D e o cliente satisfaz a regra de T }
                (satisfaz = atende a pelo menos um grupo de inclusão e a nenhum grupo de exclusão)

2. RETIRAR      C = C − { T | "Retirar de T" vigente em D }

3. FIXAR        F = fixações vigentes em D para o cliente em M
                ├─ mapa exclusivo e |F| ≥ 2          → INCONSISTÊNCIA "Conflito de fixação"      FIM
                ├─ alguma f ∈ F inválida (território  → INCONSISTÊNCIA "Fixação inválida"         FIM
                │  encerrado ou inexistente em D, de outro mapa, cliente fora do universo,
                │  ou Fixar e Retirar do mesmo território no mesmo período)
                ├─ mapa exclusivo e |F| = 1          → vencedor = f; os de C ≠ f: "perdeu para a exceção"  FIM
                └─ mapa não exclusivo                → resultado = C ∪ F                          FIM

4. MAPA NÃO EXCLUSIVO → resultado = C   FIM        (daqui em diante, só exclusivo)
   |C| = 0 → sem território   FIM        |C| = 1 → vencedor   FIM

5. DESEMPATE (sobre conjuntos)
   5a PRIORIDADE      p(T) = prioridade da regra (1 = mais alta; sem = ∞); fica C = { T | p(T) = mín }
   5b ESPECIFICIDADE  fica C = { T ∈ C | nenhum outro de C está abaixo de T na árvore de D }
   |C| = 1 → vencedor   FIM

6. CONFLITO (|C| ≥ 2): se a atribuição atual está em C, é mantida ("Permanece em conflito");
   senão, sem território ("Conflito"). Nunca escolhe entre os empatados.
```

**Fixações múltiplas e inválidas (complemento aprovado).** Duas ou mais fixações vigentes do mesmo cliente num mapa
exclusivo produzem **Conflito de fixação**; uma fixação que aponte para território encerrado, inexistente, de outro
mapa ou incompatível produz **Fixação inválida**. Em nenhum dos dois casos o sistema escolhe outro território, e **não
usa** prioridade, especificidade, ordem de cadastro, Id, data de criação ou ordem de consulta. As inconsistências:
- impedem o planejamento de criá-las (validação ao incluir a mudança na operação);
- se existirem mesmo assim (dado importado, mudança concorrente), aparecem na simulação e nas divergências com o
  motivo, as exceções envolvidas e quem as criou;
- **bloqueiam a aplicação** de qualquer operação do mapa enquanto existirem ("A operação produziria resultado inválido
  para N clientes: resolva as inconsistências (encerrar ou corrigir as fixações) na própria operação e simule de
  novo"). A atribuição atual do cliente não é mexida;
- ficam registradas: a tentativa de aplicação recusada vai para a auditoria da operação, com a lista.

**Por que cada critério vem antes do seguinte**

| Ordem | Critério | Por quê |
|---|---|---|
| 0 | Universo | Fronteira do mapa definida por quem o configurou; nem exceção a atravessa. |
| 1 | Regra | Única fonte de candidatos automáticos; o resto só remove candidatos ou impõe decisão humana. |
| 2 | Retirar | Afirmação humana negativa: território retirado não concorre a desempate nenhum. |
| 3 | Fixar | Decisão sobre o indivíduo vale mais que regra sobre o conjunto (mesmo princípio da exceção comercial). |
| 5a | Prioridade | Declaração **explícita** vem antes de inferência (permite "Key Account ganha de Curvelo"). |
| 5b | Especificidade | **Inferida da árvore**: filho descreve um recorte do pai; resolve MG × Curvelo sem configurar prioridade. |
| 6 | Conflito | Empate real: sem informação para decidir. Manter a atual não é escolher: ela já era válida. |

**Recusados como desempate:** quantidade de condições (propriedade da escrita, não do significado), contenção de
conjuntos calculada pelos dados (um cliente novo mudaria o resultado de outros), nível numérico da árvore (só faz
sentido no mesmo ramo), ordem de cadastro, Id, código, nome, data de criação e ordem de consulta.

**Explicação** (sai do mesmo cálculo). Cada território do mapa recebe um estado: `Vencedor` (com o passo),
`RetiradoPorExcecao`, `PerdeuParaExcecao`, `PerdeuPorPrioridade`, `PerdeuPorEspecificidade`, `Empatado`, `NaoAtende`
(por grupo e, para um cliente, **por condição**: "UF do endereço de referência = SP; a regra pede MG"), `SemRegra`,
`Inativo`; e o cliente pode ter `ConflitoDeFixacao` ou `FixacaoInvalida`. A tela responde "por que este?" e "por que não
aquele?". Na aplicação, a explicação fica congelada no item da operação.

**Propriedades e testes:** determinístico (só conjuntos; entrada embaralhada 50 vezes = mesmo resultado e explicação),
reproduzível (mesma configuração em D + cadastro + atribuições = mesma assinatura), explicável (um teste por estado),
testável sem banco. Casos: 0/1/2/3 candidatos; prioridade com e sem valor; ancestral × descendente; irmãos; prioridade
que anula especificidade; Fixar × regra; Retirar que faz cair no próximo; **duas fixações**; **fixação em território
encerrado**; conflito mantendo e sem a atual; mapa não exclusivo; fora do universo.

**Aviso de configuração:** regra com prioridade num território com descendentes com regra → "MG tem prioridade 2:
Curvelo e Montes Claros nunca vencerão MG para clientes que atendem às duas regras".

---

## 5. Endereço de referência (T16)

As condições de endereço do catálogo usam qualquer endereço ativo (certo para a lista de Pessoas, errado para
território). O mapa guarda a **finalidade do endereço de referência** (FK para o cadastro de finalidades; padrão
Comercial ao criar, escolhida pelo usuário). Nas regras territoriais, as condições de endereço valem só para o endereço
**marcado como principal dessa finalidade** (`PessoaEnderecoFinalidades.Principal`). Implementação na 2b-1b: parâmetro
opcional `EnderecoReferencia` no `FiltrosPessoasSql.Contexto` (nulo = comportamento atual; a lista de Pessoas não
muda). Sem endereço principal na finalidade → não atende às condições de endereço; explicação e divergência dizem por
quê; não há endereço reserva. O motor avalia o cadastro no momento da simulação/aplicação (o cadastro de Pessoas não é
versionado); a operação grava `AtributosAvaliadosEm`.

---

## 6. Exceções (T2)

Campos: cliente, mapa, território, tipo, início, fim?, motivo (obrigatório), origem (*Manual*, *Divergência*,
*Importação*), operação que criou, operação que encerrou, quem e quando (da operação), auditoria.
Regras: nascem e terminam antes do fim só por operação; num mapa exclusivo, no máximo um Fixar vigente por cliente
(domínio + índice); Fixar e Retirar do mesmo território sobrepostos: recusado; Fixar só para cliente do universo e
território ativo; nunca altera a regra; quando o fim chega nada muda sozinho (divergência "Exceção vencida", com dias
restantes e aviso antes do fim, padrão de UX de períodos); "Mover de A para B" = Retirar A + Fixar B.

---

## 7. Máquinas de estado

```
TERRITÓRIO   Ativo ──(encerrar: sem uso, direto; com uso, por operação)──► Encerrado
             Encerrado ──(reativar: só sem uso)──► Ativo

REGRA        (publicada pela operação) Vigente ──(nova versão ou encerrar regra)──► Encerrada (fim = véspera)
             mesma data de efeito substituída por outra operação → Anulada (Ativo = 0)

OPERAÇÃO     Rascunho ◄──(editar)── Simulada
             Rascunho ──(simular)──► Simulada ──(aplicar: re-simula, confere assinatura e inconsistências)──► Aplicada
             Rascunho / Simulada ──(cancelar, com motivo)──► Cancelada
             Aplicada ──(desfazer, só antes do efeito, com motivo)──► Desfeita        (T15)

ATRIBUIÇÃO   Vigente ──(operação)──► Encerrada (FimEm)   ·  futura = InicioEm > hoje  ·  Anulada (Ativo = 0)

EXCEÇÃO      Vigente ──(fim ou operação)──► Encerrada   ·   Anulada (operação desfeita ou mesma data)
```

---

## 8. Vigência, histórico e T15

- `DateOnly`, período fechado: vigente em D ⇔ `InicioEm ≤ D ≤ (FimEm ?? ∞)`; encerrar = `FimEm = D − 1`.
- Operação com efeito futuro grava linhas "a partir de".
- Duas operações no mesmo dia de efeito: a linha aberta pela primeira com início em D é **anulada** (`Ativo = 0`,
  apontando para a operação que anulou) e fica no histórico.
- **Desfazer operação futura (T15)** é uma ação de negócio, não uma exclusão: exige permissão APLICAR e motivo; só é
  aceita enquanto `EfeitoEm > hoje` (depois do efeito, corrige-se com nova operação). Grava na operação: `DesfeitaPor`,
  `DesfeitaEm` (data e hora), `DesfeitaMotivo`, `DesfazerSolicitadoEm` (data da solicitação), situação anterior
  (Aplicada) e posterior (Desfeita), mais o evento na auditoria. Efeito: anula as linhas que ela abriu e reabre as que
  ela fechou com fim futuro. A operação, os itens e a explicação continuam intactos.
- Árvore: uma linha por período em `TerritorioPosicoes`; "a árvore em D" sai direto dela. Sem uso, mover só corrige a
  posição aberta (nada dependia dela; a auditoria registra).

---

## 9. Concorrência

1. **Número TE-** (T12): `NumeracoesDocumento`, incremento atômico (`UPDATE … SET Ultimo = Ultimo + 1 OUTPUT
   inserted.Ultimo`); primeiro do ano por inserção protegida pela PK, repetida uma vez. Número dado **ao criar o
   rascunho**, em transação curta (operação cancelada guarda o número; buraco aceitável em documento interno).
2. **Aplicações simultâneas no mesmo mapa**: o primeiro comando da aplicação atualiza o mapa com a versão lida
   (rowversion); a segunda recebe conflito ("simule de novo").
3. **Mudanças estruturais da árvore** (criar, mover, mudar "existe desde", encerrar, reativar território) exigem a
   **versão da árvore que a tela mostrava** e a trocam na mesma transação, na trava própria `MapaTerritorialArvores`
   (D1 = B, 29/09). Duas mudanças simultâneas que juntas formariam ciclo (A→B e B→A) não passam: a segunda falha no banco
   e nada dela é gravado; quem decidiu olhando uma árvore velha é avisado. A ficha do mapa tem a versão dela, separada:
   mexer na árvore não a invalida (desativar/reativar o mapa, sim, troca a versão da árvore). Proteção tripla: regra no
   domínio + serialização pela trava + gatilho de ciclo/níveis no banco.
4. **Assinatura da simulação**: hash de (efeito; por cliente: resultado, território, versão da regra, exceção; versões
   das regras; versão do mapa).
5. Cliente editado durante a aplicação: vale o que foi lido no início; a mudança aparece na próxima divergência.

---

## 10. Integridade da hierarquia

- Pai no mesmo mapa **garantido pelo banco**: AK `(MapaId, Id)` + FK composta `(MapaId, PaiId)`; o mesmo nas
  posições, atribuições e exceções (`(MapaId, TerritorioId)`).
- `MapaId` do território é imutável. Ciclo: domínio (sobe os ancestrais do novo pai) + serialização (seção 9) + gatilho
  `TR_Territorios_Arvore` (erro 50071), que vale também para gravações feitas direto no banco.
- Profundidade máxima: 12 níveis (proteção), no domínio e no mesmo gatilho.
- Posições do mesmo território não se cruzam: índice único (uma aberta) + gatilho `TR_TerritorioPosicoes_SemSobreposicao`
  (erro 50072). Pai do território = pai da posição aberta: conferido no fim da gravação, na mesma transação (são duas
  tabelas gravadas em comandos separados; um gatilho veria o passo intermediário).
- Nome único entre irmãos ativos (domínio + índice filtrado); código único no mapa.
- Encerrar com descendentes ativos: recusado. Pai encerrado não recebe filhos.
- Mover/encerrar/reativar com uso na subárvore: só por operação (T18); sem uso: livre (T14).

---

## 11. Comportamentos

| Situação | Comportamento |
|---|---|
| Território sem regra | Nó agregador; recebe só por Fixar; soma os descendentes. |
| Cliente sem território | Aparece em "Sem território" com o motivo; atribuição anterior só encerra por operação. |
| Cliente em conflito | Mantém a atual se empatada; senão sem território; resolve-se por prioridade, árvore ou exceção, via operação. |
| Conflito de fixação / fixação inválida | Inconsistência: nada é escolhido, a atual não muda, a aplicação é bloqueada até resolver. |
| Regra alterada | Nova versão na operação; anterior termina em D − 1; critérios congelados. |
| Regra encerrada | Clientes só dela saem na mesma operação (ou ficam por Fixar). |
| Território encerrado | Por operação e sem descendentes ativos: regra, atribuições, Fixar e responsáveis terminam; clientes reavaliados. |
| Exceção vencida | Divergência "Exceção vencida"; nada muda sozinho. |
| Cliente mudou de endereço/CNAE/etiqueta | Divergência + aviso na ficha; nada muda sozinho. |
| Pessoa desativada | Sai do universo; a próxima operação encerra a atribuição. |

---

## 12. Multiempresa

`MapasTerritoriais.EmpresaId` (nulo = grupo todo), travado depois do uso. Universo de mapa de empresa: classificação
**e** conta de cliente válida para a empresa (própria ou padrão). A atribuição não copia a empresa (vem do mapa). A
cópia de `Exclusivo` é amarrada por FK composta a `Mapas(Id, Exclusivo)`, o que também impede mudar a exclusividade de
um mapa em uso. A mesma pessoa pode estar em mapas de empresas diferentes sem conflito.

---

## 13. Princípio para Vendas, Oportunidades, Comissões e Metas (aprovado)

- **P-T1** Todo documento comercial grava, no momento do fato, para cada mapa marcado "registrar nos documentos":
  `AtribuicaoTerritorioId`, `TerritorioId`, código e nome congelados, e o vendedor da carteira na data.
- **P-T2** Relatório histórico, comissão, meta, indicador, ranking e fechamento leem o documento; nunca recalculam o
  território pelo cadastro atual (venda de 2026 em MG Norte com João continua assim depois da reorganização de 2027).
- **P-T3** Período fechado congela o apurado; operação retroativa não altera período fechado (com fechamento, a T8 ganha
  a trava "não antes do último período fechado").

---

## 14. Permissões

| Permissão | Dá direito a | Entrega |
|---|---|---|
| `TERRITORIOS.VISUALIZAR` | Ver tipos, mapas, árvore, responsáveis, histórico (e na 2b-1b: regras, operações, divergências; clientes filtrados pelo alcance) | 2b-1a |
| `TERRITORIOS.CONFIGURAR` | Tipos, mapas, territórios sem uso (criar, mover, encerrar, reativar) e responsáveis | 2b-1a |
| `TERRITORIOS.PLANEJAR` | Operações em rascunho e simulação (alcance Tudo) | 2b-1b |
| `TERRITORIOS.APLICAR` | Aplicar, cancelar e desfazer (alcance Tudo) | 2b-1b |

---

## 15. Performance

Motor por conjunto: uma consulta por grupo de condições devolve ids; união/subtração no banco ou em memória. Gravação
só das mudanças, em lotes de 1.000 na mesma transação. Se um mapa passar do limite prático (ex.: 300 mil clientes ou
60 s), aplicação em segundo plano com a mesma lógica (S6).

---

## 16. Entregas

### 2b-1a — Estrutura (autorizada)

- **Migration `Fase2b1Territorios`** (gerada pelo usuário, revisada antes de iniciar a API), só aditiva: tabelas 1 a 6 e
  os três tipos de sistema.
- **Domínio:** entidades; `RegrasArvoreTerritorial` (código, nome entre irmãos, pai no mesmo mapa, ciclo, profundidade,
  pai ativo, uso na subárvore, encerrar/reativar); `RegrasMapaTerritorial` (código, nome, universo, finalidade,
  empresa; campos travados com uso); `RegrasTipoTerritorio`; `RegrasResponsavelTerritorio` (pessoa **ou** equipe,
  função = papel comercial, quem pode ser, sobreposição, histórico não reescrito, dentro da vida do território).
- **Uso operacional:** a interface `IUsoTerritorial` responde "quais territórios têm regra publicada ou atribuição" e "o
  mapa está em uso". Na 2b-1a as tabelas do motor não existem, então a resposta é sempre "nenhum". A 2b-1b só troca a
  implementação: nenhuma regra nem tela muda.
- **Serialização:** mudança estrutural grava o território e atualiza a versão do mapa na mesma gravação, com a versão
  lida antes da validação (seção 9.3).
- **Permissões** VISUALIZAR e CONFIGURAR; **API**; **telas**: Tipos de território e Mapas territoriais (Comercial ›
  Configurações › Territórios) e **Territórios** (Comercial › Territórios: mapa, árvore com busca, ficha com Dados,
  Responsáveis e Histórico da posição).
- **Testes:** domínio, aplicação, cliente, modelo EF e banco real (pulados sem `LONE_TESTES_SQLSERVER`), incluindo A→B
  e B→A simultâneos.

### 2b-1b — Motor (implementada em 29/09; relatório na seção 22)

Tabelas 7 a 13 (migration própria); `UsavelEmCobertura` no catálogo; endereço de referência no `FiltrosPessoasSql`;
`MotorAtribuicao`; regras e exceções; operação (rascunho, mudanças, simular, aplicar, cancelar, desfazer);
inconsistências; divergências; explicação; aba Comercial da ficha; permissões PLANEJAR e APLICAR; `IUsoTerritorial`
real; TR- passando a usar o numerador (entrega própria, sem mudar números emitidos).

### Depois

2b-2 (Meus territórios no escopo, filtro Território em Pessoas, território na carteira em uma data, indicadores) e 2c
(distribuição e capacidade), com planos próprios.

---

## 17. Sugestões além do pedido

S1 sincronizar carteira pelo território (gera transferência `TR-` em prévia) · S2 território gravado nos documentos
(virou P-T1) · S3 planejamento anual com efeito futuro · S4 dupla checagem "quem planeja não aplica" · S5 capacidade
avisando na simulação · S6 processamento em segundo plano · S7 mapa visual por UF/município · S8 atribuição ao salvar o
cliente (parâmetro do mapa) · S9 operação agendada para o fim das exceções.

---

## 18. Andamento

- 28/09 — revisão arquitetural (T1–T14).
- 29/09 05h39 — aprovação T1–T14 com ajustes; pedido de fechar o T3.
- 29/09 — revisão final (T3 exato, T15–T20).
- 29/09 07h19 — aprovação T3 (com fixações múltiplas) e T15–T20; início da 2b-1a.
- 29/09 — 2b-1a entregue para compilar e testar (seção 19). Sem commit.
- 29/09 12h24 — build 0/0; 1523 aprovados; `BancoTerritorios` 8/8 no SQL Server. Teste de telas feito (roteiro completo).
- 29/09 13h15 — D1 = **B** e auditoria "padrão de ERP maduro" (seção 20). Sem commit.
- 29/09 17h — auditoria final da 2b-1a (seção 21); commit `af3924c`.
- 29/09 — plano da 2b-1b consolidado (DN-01 a DN-15, RT-1, RT-2) e autorizado; implementação.
- 29/09 23h45 — 2b-1b: build 0/0; 1700 aprovados; limite DN-12 = 50.000 (aviso 10.000) escolhido pelo usuário e provado
  no teste de volume. Sem Update-Database, sem commit (seção 22).

## 19. Relatório da 2b-1a (entregue em 29/09/2026, sem commit, aguardando compilação e testes do usuário)

**Banco — migrations `Fase2b1Territorios` (aplicada) e `Fase2b1ResponsaveisSemSobreposicao` (o gatilho).** Só aditivas:
seis tabelas novas e um gatilho; nenhuma coluna de tabela existente muda.

| Tabela | Chaves, índices e restrições |
|---|---|
| `TiposTerritorio` | PK; UQ `Codigo`; UQ `Nome` (Latin1_General_CI_AI); 3 linhas iniciais (Geográfico, Segmento, Estratégico) |
| `MapasTerritoriais` | PK; UQ `Codigo`; UQ `Nome`; FK `EmpresaId` → Pessoas; FK `FinalidadeEnderecoReferenciaId` → FinalidadesEndereco (obrigatória) |
| `MapaTerritorialClassificacoes` | PK; UQ (`MapaId`, `PapelId`); FK → Papeis |
| `Territorios` | PK; **AK (`MapaId`, `Id`)**; **FK composta (`MapaId`, `PaiId`) → (`MapaId`, `Id`)**; FK Mapa, Tipo; UQ (`MapaId`, `Codigo`); UQ filtrado (`MapaId`, `PaiId`, `Nome`) WHERE `Situacao` = 0; CK `Situacao` × `FimEm` |
| `TerritorioPosicoes` | PK; FK composta (`MapaId`, `TerritorioId`) e (`MapaId`, `PaiId`) → AK; UQ filtrado (`TerritorioId`) WHERE `FimEm` IS NULL AND `Ativo` = 1; CK `FimEm` ≥ `InicioEm`; IX (`MapaId`, `PaiId`, `InicioEm`) |
| `TerritorioResponsaveis` | PK; FK Pessoa, Equipe, TiposCarteira (função); CK pessoa **ou** equipe; CK período; IX (`PessoaId`, `FimEm`), (`EquipeId`, `FimEm`); gatilho de sobreposição (erro 50070), criado pela migration com `SqlMigracaoTerritorios.CriarProtecao` |

Nada apaga em cascata (Restrict/NoAction em todas as FKs).

**Entidades e regras.** `TipoTerritorio`, `MapaTerritorial` (+ `MapaTerritorialClassificacao`), `Territorio`
(+ `TerritorioPosicao`, `TerritorioResponsavel`), `SituacaoTerritorio`. Regras no domínio: `RegrasCadastroTerritorial`
(código estável, textos, tipo), `RegrasMapaTerritorial` (universo, endereço de referência, travas com uso, desativação),
`RegrasArvoreTerritorial` (pai no mesmo mapa, ciclo direto e indireto, 12 níveis, nome entre irmãos ativos, "existe desde"
coerente com pai e filhos, uso na subárvore, posições, encerrar e reativar), `RegrasResponsavelTerritorio` (pessoa ou
equipe, "Quem pode ser" da função, sobreposição, histórico não reescrito, dentro da vida do território).

**Concorrência.** Mudança de estrutura (criar, mover, mudar "existe desde", encerrar, reativar): o primeiro comando da
transação é `UPDATE MapasTerritoriais SET AtualizadoEm = … WHERE Id = @mapa AND Versao = @lida` (LINQ `ExecuteUpdateAsync`,
parametrizado), com a versão lida **antes** da conferência. Zero linhas = outra mudança de estrutura gravou no meio →
nada é gravado ("Outra mudança na árvore deste mapa foi gravada ao mesmo tempo…"). Vir primeiro também evita impasse.
Renomear, mudar tipo/descrição e responsáveis não serializam pelo mapa.

**Responsáveis (aprovado em 29/09, 08h56).** Três camadas: (1) a regra no domínio (`RegrasResponsavelTerritorio`); (2) o
**gatilho** `TR_TerritorioResponsaveis_SemSobreposicao`, no padrão do gatilho da carteira, que barra também gravações feitas
direto no banco — chave: mesmo território + mesma função + mesma pessoa **ou** mesma equipe, só linhas ativas, fim nulo =
aberto, dia do fim conta; (3) a **trava de versão do território**: toda alteração de um território existente começa, dentro
da transação, por `UPDATE Territorios … WHERE Id = @id AND Versao = @aberta` (depois da trava do mapa, quando houver,
sempre nessa ordem). Dois usuários incluindo responsáveis conflitantes na mesma versão: o segundo espera nessa primeira
linha, é recusado pela versão e não grava nada. Responsáveis alterados passam antes pela interseção do período antes ×
depois (dois passos, como a carteira), para que o gatilho nunca veja um estado intermediário falsamente sobreposto.

**Telas.** Comercial › **Territórios** (mapa, árvore com recuo, caminho, busca, "Mostrar encerrados"; ficha com Dados,
Responsáveis e Histórico da posição; "+ Território abaixo deste", Encerrar, Reativar); Comercial › Configurações ›
Territórios › **Mapas territoriais** e **Tipos de território**. Permissões `TERRITORIOS.VISUALIZAR` e
`TERRITORIOS.CONFIGURAR` (quem configura também enxerga; quem só visualiza vê a ficha travada).

**Testes novos (52):** domínio 23 (`Dominio/TerritoriosTests`), aplicação 8 (`Aplicacao/TerritorioAppServiceTests`, inclui
A→B × B→A intercalados), cliente 6 (`Cliente/TerritoriosTelasTests`), modelo EF 7 (`Infraestrutura/ModeloTerritoriosTests`)
e banco real 8 (`Infraestrutura/BancoTerritoriosTests`: FK composta, posição aberta dupla, CKs, nome entre irmãos,
A→B × B→A **simultâneos**, gatilho de sobreposição direto no banco sem falso positivo, dois usuários incluindo responsáveis
conflitantes ao mesmo tempo, rollback completo quando o gatilho barra, e reorganização de períodos sem falso positivo —
pulados sem `LONE_TESTES_SQLSERVER`). Resultado do usuário antes do gatilho: 1523 aprovados, 0 falhas, 26 ignorados. Ajustados:
`MenuLateralTests`, `ConfiguracoesViewModelTests` (item de menu e grupo de configuração novos). Revisão independente por
agente, sem compilar: 2 falhas de teste encontradas e corrigidas; nenhum erro de compilação encontrado.

**Plano × implementado**

| Plano | Implementado | Observação |
|---|---|---|
| Tabelas 1 a 6 | Iguais à seção 3 | — |
| Território `FimEm` | `FimEm` = último dia; encerrar sem uso: hoje é o último dia | responsáveis vigentes terminam hoje; os que nem começaram são anulados |
| "Existe desde" | Sem coluna nova: é o início da primeira posição | editável só sem uso; não pode ser futuro, nem antes do pai, nem depois dos filhos |
| Uso operacional | `IUsoTerritorial`, sempre vazio na 2b-1a | a 2b-1b só troca a implementação |
| Serialização (9.3) | `ExecuteUpdateAsync` como primeiro comando da transação | ver divergência D1 |
| Permissões | VISUALIZAR e CONFIGURAR | CONFIGURAR também dá leitura (decisão de implementação, sem efeito no banco) |
| Mapa desativado | Árvore só para consulta; mapa em uso não desativa (regra pronta para a 2b-1b) | — |
| Largura da lista | `LayoutMestreDetalhe` ganhou largura opcional (a árvore usa 420) | compatível com todas as telas |

**Divergências e pontos para decidir**

- **D1 — resolvida em 29/09 (13h15): opção B**, implementada como descrito na seção 20.
- Com uso na subárvore, um "existe desde" diferente enviado direto pela API é recusado (não descartado em silêncio).
- Verificação de 29/09 (08h53), confirmada pelo usuário: `PapelId` do universo = a classificação da pessoa (cadastro de
  Papéis), como na T9 e no "Quem pode ser" do papel comercial; "Existe desde" = menor início das posições, corrigível só sem
  uso (T14, auditado), sem histórico artificial.
- Acrescentado a pedido (29/09, 08h56): gatilho de sobreposição dos responsáveis + trava de versão do território na
  gravação + testes de banco real. Como a `Fase2b1Territorios` já tinha sido aplicada no banco de desenvolvimento (a API
  aplica ao iniciar; 0 territórios e 0 responsáveis gravados), ela **não foi reescrita**: o gatilho entrou numa segunda
  migration, `Fase2b1ResponsaveisSemSobreposicao` (sem operação de esquema; Up = `SqlMigracaoTerritorios.CriarProtecao`,
  Down = `RemoverProtecao`), no mesmo formato da `CarteiraSemSobreposicao`. Divergência consciente da T19 (uma migration
  por entrega): a 2b-1a ficou com duas.

## 20. Trava da árvore (D1 = B) e proteções no banco (29/09/2026, sem commit)

Pedido do usuário: "o sistema o mais seguro", no padrão de ERPs maduros — falhar cedo na aplicação, proteger
definitivamente no banco, conflito sempre explícito, histórico preservado.

**Auditoria (o que já estava no padrão e não mudou):** gatilho dos responsáveis (conjunto, várias linhas, só ativos, fim
nulo = aberto, dia do fim conta, pessoa × equipe separadas, rollback total, Down só remove o gatilho); reorganização de
períodos em dois passos; trava de versão do território; dois usuários com responsáveis conflitantes; histórico nunca
apagado nem reescrito; auditoria existente (usuário, data, motivo, antes/depois) reaproveitada.

**Mudanças:**
- `MapaTerritorialArvores` (PK `MapaId` → `MapasTerritoriais`, `Versao` rowversion, `AtualizadoEm`; fora da auditoria:
  controle técnico). Mapa novo nasce com a sua; a migration cria uma para cada mapa existente.
- API: `GET territorios?mapaId=` devolve `ArvoreTerritorialDto` (versão lida **antes** dos territórios + lista);
  `TerritorioDto.VersaoArvore` e `AlterarSituacaoTerritorioRequisicao.VersaoArvore` exigidas em mudança de estrutura.
  Sem a versão: validação; versão velha: conflito, recusado **antes** da conferência e de novo na gravação.
- Mensagens: árvore velha — "A árvore deste mapa foi alterada por outro usuário (ou em outra janela) enquanto esta tela
  estava aberta. Nada foi salvo…"; ficha do mapa velha — "O mapa territorial foi alterado por outro usuário enquanto esta
  tela estava aberta. Nada foi salvo: recarregue os dados antes de salvar."
- Gatilhos novos (READCOMMITTEDLOCK nas leituras): `TR_Territorios_Arvore` (ciclo e 12 níveis, 50071) e
  `TR_TerritorioPosicoes_SemSobreposicao` (50072). Recusa do banco vira mensagem própria, nunca erro SQL cru.
- Conferência final da posição aberta no repositório (erro interno se não conferir; nada é gravado).
- Reativar recusa território sem posição válida (antes seria erro interno).

**Migration `Fase2b1TravaArvore`** (terceira, porque a segunda já estava aplicada): CreateTable da trava (EF) + no fim do
Up `PreencherTravasDaArvore`, `CriarProtecaoArvore`, `CriarProtecaoPosicoes`; no começo do Down
`RemoverProtecoesArvore` (depois o EF retira a tabela). As duas anteriores não mudam.

**Limite consciente:** o gatilho dos responsáveis (migration 2, já aplicada) não tem READCOMMITTEDLOCK. Só importaria com
READ_COMMITTED_SNAPSHOT ligado **e** duas gravações simultâneas feitas por fora da aplicação; pela aplicação a trava do
território já serializa. Se um dia o banco ligar RCSI, recriar o gatilho com a dica.

**Testes acrescentados:** domínio 10 casos de sobreposição (Theory de 9 + territórios diferentes); aplicação 2
(árvore velha recusada antes de conferir, inclusive encerrar; sem versão); modelo 1 (trava e gatilhos); banco real 7
(várias linhas no mesmo INSERT e UPDATE; ciclo e 13 níveis direto no banco; posições que se cruzam; árvore velha sem gravar
nada; cadastro do mapa × árvore sem conflito falso e desativação invalidando a árvore vista; mapa novo com trava;
conferência final; histórico de responsáveis/posições/auditoria depois de trocar e encerrar).

## 21. Auditoria final da 2b-1a (29/09/2026, 17h, sem commit)

- **Achado 1 (bloqueante, corrigido):** a migration `Fase2b1TravaArvore` não existia (modelo com a trava e os gatilhos,
  snapshot sem). Gerada pelo usuário (`20260929195958`); no Up, depois do CreateTable, `PreencherTravasDaArvore`,
  `CriarProtecaoArvore`, `CriarProtecaoPosicoes`; no Down, `RemoverProtecoesArvore` antes do DropTable.
- **Achado 2 (real, corrigido):** o gatilho 50070 sem READCOMMITTEDLOCK deixava duas gravações simultâneas feitas por fora
  confirmarem sobreposição com READ_COMMITTED_SNAPSHOT ou SNAPSHOT — e o **LoneERP está com RCSI ligado**. Correção na
  migration `Fase2b1ResponsaveisConcorrencia` (`20260929200500`, sem esquema): `ReforcarProtecaoConcorrencia` (CREATE OR
  ALTER com a dica); Down = `DesfazerReforcoProtecaoConcorrencia` (texto exato da migration 2). O "limite consciente" da
  seção 20 deixou de existir.
- Testes de banco agora rodam no padrão do SQL Server e com RCSI/SNAPSHOT (`BancoTerritoriosTests` e
  `BancoTerritoriosRcsiTests`), incluindo duas transações simultâneas de verdade; `MigracaoTerritoriosTests` aplica as
  migrations reais num banco temporário (Up, trava do mapa antigo, gatilhos, Down, Up).
- Resultado: build 0/0; `dotnet test` 1614/0/0; `BancoTerritorios` 52/0/0; migrations 3/0/0.
- Pendente de decisão: gatilho da carteira (`TR_CarteiraClientes_SemSobreposicao`) tem a mesma forma de ler (sem a dica).

---

## 22. Relatório da 2b-1b — Operações territoriais (29/09/2026, 23h50, sem commit, migrations não aplicadas)

Implementada conforme o plano consolidado da 2b-1b (artefato "Plano 2b-1b") e as decisões DN-01 a DN-15, RT-1 e RT-2.

### 22.1 Validação

| Etapa | Resultado |
|---|---|
| Build da solução | 0 avisos, 0 erros |
| `dotnet test` (tudo, com `LONE_TESTES_SQLSERVER`) | **1704 aprovados, 0 falhas, 0 ignorados** (30/09, 05h25, depois da revisão de 29/09 23h50; eram 1614 na 2b-1a) |
| Banco real | `BancoOperacoesTerritoriais*` no padrão do SQL Server **e** com RCSI/SNAPSHOT; aplicações simultâneas de verdade |
| Volume (DN-12) | 50.000 clientes + 5 mudanças de estrutura numa transação: simular 5,8 s, aplicar 12,9 s (SQL Express do usuário); medição anterior com 10.000: 1,5 s / 2,8 s |
| Erro no último lote | desfaz os lotes anteriores; nada fica gravado; a operação continua Simulada |
| Migrations | `MigracaoMotorTerritorialTests`: a partir da 2b-1a com um mapa antigo, Up → modelo sem mudanças pendentes → Down até a Numeracao → Down até a 2b-1a (gatilhos da 2b-1a intactos) → Up sem duplicar |

### 22.2 Migrations (separação aprovada)

- **`20260930004153_Fase2b1bNumeracao`** — só `NumeracoesDocumento` (PK Prefixo+Ano, `CK Ultimo >= 0`). Down: DropTable.
- **`20260930004950_Fase2b1bMotor`** — 11 tabelas (`ParametrosTerritoriais` com a linha única de 30 dias,
  `MapaTerritorialMotor`, `RegrasTerritorio`, `ExcecoesTerritorio`, `AtribuicoesTerritorio`, `OperacoesTerritoriais`,
  `OperacaoTerritorialMudancas`, `OperacaoTerritorialSimulacoes`, `OperacaoTerritorialSimulacaoItens`,
  `OperacaoTerritorialItens`, `OperacaoTerritorialFechamentos`); colunas novas, todas anuláveis ou com padrão, em tabelas
  da 2b-1a (`TerritorioPosicoes.OperacaoId/OperacaoMudancaId/OperacaoEncerramentoId/OperacaoAnulacaoId`,
  `TerritorioResponsaveis.OperacaoEncerramentoId`, `MapasTerritoriais.RegistrarNosDocumentos` = falso — DN-15); índice
  único `UX_MapasTerritoriais_Id_Exclusivo`; todas as FKs `Restrict`. No fim do Up, por SQL: `PreencherMotores` (uma
  linha de motor por mapa existente, idempotente), `CriarChavesExclusivo`, gatilhos 50073 (regras: sobreposição e
  imutabilidade por EXCEPT), 50074 (exceções), 50075 (atribuições) e 50076 (itens imutáveis) — os três primeiros leem com
  `READCOMMITTEDLOCK`. No começo do Down, `RemoverProtecoesMotor`.
- Nenhuma migration da 2b-1a foi alterada; nenhuma coluna existente muda de tipo; nenhum dado existente é reescrito
  (só a linha do motor é acrescentada para cada mapa).

### 22.3 Decisões de implementação (dentro do aprovado; listadas para revisão)

1. **FK de Exclusivo criada por SQL**, não pelo EF: uma chave alternativa `(Id, Exclusivo)` no EF tornaria Exclusivo
   imutável também em mapas sem uso, o que a 2b-1a permite trocar. Com a FK `(MapaId, Exclusivo)` → índice único, o banco
   recusa trocar a exclusividade de mapa que já tem exceção ou atribuição.
2. **12 tabelas físicas** (1 na Numeracao + 11 no Motor). O plano da 2b-1b dizia "onze tabelas novas" porque contava
   `OperacaoTerritorialSimulacoes` + `OperacaoTerritorialSimulacaoItens` numa linha só; nada além do plano foi criado.
   A tabela de 13 da seção 3 é a da revisão de 28/09 (antes de DN-02, DN-03 e dos fechamentos) e fica como histórico.
3. **Versão do motor muda também quando muda campo travado do mapa** (empresa, exclusividade, finalidade do
   endereço, classificações/universo) e na (des)ativação — não só com a árvore (DN-02 estendida ao que muda o resultado do motor). Nome e
   descrição não mudam a versão.
4. **Mudanças de rascunho removidas são apagadas** (não anuladas): rascunho não é fato; a história da operação guarda o
   evento.
5. **Operação sem mudanças (revisão de 29/09, 23h50: mantida como no plano da 2b-1b, telas: "criar operação com estas").** Serve para uma coisa só: corrigir
   divergências que vieram do cadastro (endereço, etiqueta, CNAE mudaram sem operação), que não têm mudança planejada a
   fazer. Nasce pelo botão **"Criar operação com estas"** da tela Divergências territoriais (rascunho no mapa, efeito na
   data conferida, motivo pedido na hora; abre na tela de operações). A simulação mostra só divergências e a aplicação as
   corrige (DN-14). Sem mudanças e sem divergências, não há nada a gravar: a aplicação é recusada com "Nada a aplicar".
   Numa operação com mudanças, as divergências existentes no mapa são corrigidas junto (a aplicação grava o estado coerente
   do mapa inteiro na data), separadas na simulação como "divergência" e não como efeito da operação. *O botão faltava na
   primeira entrega (a tela só orientava em texto); foi feito nesta revisão.*
6. **Qualquer usuário com PLANEJAR edita qualquer rascunho** (DN-06; a rowversion impede edição às cegas); cancelar segue
   RT-2 (PLANEJAR só o próprio, APLICAR qualquer um). **Reforço de auditoria (revisão de 29/09):** cada edição (data,
   motivo, observação, mudança incluída ou retirada, com o texto da mudança) vira evento; quando quem edita não é quem
   criou — comparado pelo Id do usuário —, o evento diz "João alterou a operação TE-…, criada por Maria: …". A história da
   operação mostra os eventos e o antes → depois de data, motivo e observação; o cabeçalho destaca "criada por Maria e
   editada também por João (última edição: João, em …)". Limitação conhecida: a auditoria do Lone guarda o nome de quem
   gravou, não o Id; o destaque do cabeçalho compara nomes (os eventos comparam Ids). Identidade estável na auditoria está
   no backlog 1 (projeto de identidade).
7. **Ficha da Pessoa** mostra só os territórios de hoje (sem "por quê?" nem histórico, que ficam na consulta do território
   e na operação).
8. **"Sem endereço" fica fora dos campos de regra**; UF e Município com "nenhum destes" exigem o endereço de referência
   existente (senão cliente sem endereço entraria por exclusão).
9. **Assinatura na aplicação**: recalculada com a versão do motor que a simulação assinou — a trava do motor
   (`UPDATE … WHERE Versao = vista`) prova que a versão é a mesma, mas o próprio UPDATE gera rowversion nova.
10. **DN-12 (escolha do usuário em 29/09 23h40)**: limite de **50.000 clientes gravados por operação**, aviso a partir de
    **10.000**, constantes no domínio (`RegrasOperacaoTerritorial.LimiteClientesPorOperacao/AvisoClientesPorOperacao`).
    Conta entram + saem + mudam + origem atualizada (bloqueados não contam, já impedem). Acima do limite: aviso na tela,
    Aplicar desligado e recusa antes de travar o mapa, registrada na história, com a mensagem "Esta operação afetará N
    clientes, ultrapassando o limite de 50.000 clientes afetados por operação. Reduza o escopo da operação ou divida o
    planejamento em operações menores." O motor não prescreve como dividir (região, segmento, território, classificação,
    reformular a regra): é decisão de quem planeja (ajuste da revisão de 29/09, 23h50).

### 22.3b Roteiro de telas no LoneERP real (30/09, depois do Update-Database) e ajustes

Conferência pós-migração (só leitura): tudo conforme (seção 22.1). Roteiro feito no app com o mapa "Teste 2b-1b":
TE-2026-0001 (regra por etiqueta, aplicada), TE-2026-0002 ("Criar operação com estas", sem mudanças, corrigiu a
divergência), TE-2026-0003 ("Nada a aplicar", cancelada), TE-2026-0004 (história com motivo antes → depois, cancelada),
TE-2026-0005 (criada pela Edna, editada pelo Rafael: destaque e "Rafael Froés alterou a operação …, criada por Edna",
cancelada), TE-2026-0006 (tela de filtros, cancelada). Ajustes feitos antes do commit, a pedido do usuário:
- tela de operações relê a árvore do mapa a cada operação aberta (território criado depois aparece nas listas);
- listas de escolha (Picker) no Windows abrem com um toque em qualquer ponto do campo, não só na setinha
  (`Plataforma/AjusteListaEscolha`, para todas as telas);
- a ficha da operação volta ao topo quando aparece uma mensagem ("Mudança incluída", erros), para a mensagem ficar à
  vista (`ViewModelBase.MensagemMostrada`; C3 aplicado só nesta tela — o restante do C3 continua anotado).

### 22.4 Fora do escopo (confirmado, não feito)

Correção do gatilho da carteira (READCOMMITTEDLOCK), numeração TR-, empresa em equipes/responsáveis, território novo
com início futuro, processamento em segundo plano, campos personalizados em regra, dupla aprovação.

### 22.5 Observações

- Pasta vazia `src/Lone.Infrastructure/Persistencia/Migracoclses` (de 28/09, fora do git por estar vazia): pode ser
  apagada à mão.
- `_entrega/` não está no `.gitignore` (aparece como não rastreada): o commit deve listar os arquivos explicitamente.

### 22.6 Próximos passos (só com autorização)

1. Revisão deste relatório pelo usuário.
2. `Update-Database` com a API desligada (aplica `Fase2b1bNumeracao` e `Fase2b1bMotor`); conferência do banco (motor por
   mapa, parâmetros, gatilhos, FKs).
3. Testes pós-migração e roteiro de telas (operações, simulação, aplicação, desfazer, divergências, parâmetros, ficha).
4. Commit (sem push).

---

## Apêndice — diagnóstico do código (28/09, commit `c75dce3`)

- Reaproveitado: catálogo e `FiltrosPessoasSql` (linguagem das regras, sem alteração), painel de filtros (editor de cada
  grupo), vigência "nunca apagar, encerrar", operação com número e itens imutáveis (modelo da `TR-`),
  `DiasRetroativosMaximo`, auditoria com eventos e motivo, papel comercial como função do responsável, equipes com
  hierarquia e liderança temporal, `EmpresaId` nulo = grupo, escopo por registro.
- Não duplicar: segundo motor de filtros, segunda carteira, gerador `MAX+1`, lista de funções.
- Achados: o motor de filtros só faz E (grupos OU ficam acima dele); campos relativos a hoje, dependentes de permissão
  ou da carteira não servem para regra (`UsavelEmCobertura`); `FiltrosPessoasSql` recebe condições já filtradas por
  permissão (o motor roda sem escopo; a permissão vale para configurar); `TR-` por `MAX+1`; condições de endereço sobre
  qualquer endereço (T16); "cobertura" já é o nome das ausências (T20).
