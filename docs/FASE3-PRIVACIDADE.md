# Fase 3 — Privacidade e LGPD (documentação técnica)

Implementada em 26/09/2026 sobre o commit `872dda0`, a partir de `docs/FASE3-PRIVACIDADE-AUDITORIA.md` e das decisões
fechadas pelo usuário. **Sem commit** (alterações no working tree para revisão). Compilação e testes **não executados**
neste ambiente (sem SDK .NET).

## Conceitos (independentes; nenhum altera o outro)

| Conceito | Onde fica | Papel |
|---|---|---|
| Consentimento | `PessoaConsentimento` (períodos) + `FinalidadeTratamento` | **Autoriza** uma finalidade (canal opcional) |
| Aceita comunicações | `MeioContato.PermiteComunicacao` (inalterado) | **Restringe** o canal, qualquer que seja a base legal |
| Uso para marketing | bit `FinalidadeEmail.Marketing` do e-mail (inalterado; só e-mail) | **Classifica** o canal; não autoriza |

## Regra central — `Lone.Domain/Privacidade/RegrasComunicacao.PodeComunicar(pessoa, meioContatoId, canal, finalidade)`

Retorna `DecisaoComunicacao(Resultado, Motivo)`. Ordem determinística (primeiro bloqueio vence):

1. Pessoa inexistente, inativa ou arquivada → `BloqueadoPorPessoaInativa` (Em análise não bloqueia; bloqueios comerciais/faturamento/financeiros não entram).
2. Meio não pertence à pessoa, canal inválido ou incompatível com o meio (e-mail↔E-mail; telefone↔Ligação; WhatsApp/SMS só com a marca no telefone; Correspondência não é telefone/e-mail) → `BloqueadoPeloCanal`; meio inativo → `BloqueadoPorCanalInativo`.
3. Finalidade inexistente, desativada ou somente histórico ("Registro anterior") → `BloqueadoPorFinalidade`.
4. Base legal sem regra no Lone (hoje só Consentimento tem) → `SemBaseLegalAplicavel`.
5. Base legal Consentimento: consentimento em vigor para pessoa + finalidade + canal (canal específico prevalece sobre o geral) → senão `BloqueadoPorConsentimento`.
6. `PermiteComunicacao = false` → `BloqueadoPeloCanal`.
7. Classificação exigida pela finalidade (Marketing: e-mail com "Uso para marketing") → senão `BloqueadoPorFinalidade`.
8. `Permitido`.

**Observação de ordem:** o prompt da Fase 3 (seção 17) lista "Aceita comunicações" logo depois do canal, mas a matriz
obrigatória (seção 33, linha 1: consentimento Não + canal Não → `BloqueadoPorConsentimento`) exige avaliar o consentimento
antes. Foi seguida a matriz. Para a outra ordem basta mover o passo 6 para depois do passo 2 (a linha 1 da matriz passaria a
`BloqueadoPeloCanal`).

## Consentimento em períodos (`RegrasConsentimento`)

- `Conceder`: finalidade existente, ativa, não somente histórico e com base legal Consentimento; motivo obrigatório; versão do
  termo e origem opcionais; data do servidor e usuário; recusa segundo período em vigor para a mesma finalidade + canal.
- `Revogar`: motivo obrigatório; encerra o período (Concedido = falso, RevogadoEm/Por, MotivoRevogacao). Nunca apaga.
  "Registro anterior" não se revoga.
- `Situacao(registros, finalidade, canal)`: Concedido / Revogado / Não informado (sem registro) / Não aplicável (base legal ≠
  consentimento). Com canal: registros do canal decidem; sem eles, decide o geral (canal nulo).

## Banco (migration `20260926230535_PrivacidadeConsentimentos`)

Gerada pelo usuário no PMC em 26/09/2026. O PC do usuário não tem Python: a linha
`migrationBuilder.Sql(SqlMigracaoPrivacidade.ConsentimentosAnteriores);` foi inserida depois (antes da FK) pela ferramenta,
rodada do lado do assistente. O `Update-Database` foi executado ANTES dessa inserção no banco de desenvolvimento; como a FK
foi criada sem erro, a tabela `PessoaConsentimentos` não tinha registros antigos (nada a migrar).

- **Tabela nova `FinalidadesTratamento`**: Id, Codigo (30, único, imutável), Nome (60, único CI_AI), Descricao (250),
  BaseLegal (tinyint), ClassificacaoExigida (tinyint), SomenteHistorico, Ordem, DoSistema, Ativo, CriadoEm, AtualizadoEm, Versao;
  CHECK `CK_FinalidadesTratamento_SistemaAtiva`. Dados de sistema (HasData, Ids fixos `7a9e1c08-…-01` Marketing e `…-99`
  Registro anterior).
- **`PessoaConsentimentos`**: + FinalidadeId (FK Restrict), ConcedidoPor (100), Motivo (250), VersaoTermo (80),
  RevogadoPor (100), MotivoRevogacao (250); `Canal` passa a aceitar nulo; sai o índice único `(PessoaId, Canal)`; entra o
  índice único filtrado `IX_PessoaConsentimentos_EmVigor (PessoaId, FinalidadeId, Canal) WHERE Concedido = 1` e o CHECK
  `CK_PessoaConsentimentos_RevogadoForaDeVigor`.
- **Dados:** `SqlMigracaoPrivacidade.ConsentimentosAnteriores` liga todos os registros antigos a "Registro anterior" (antes da
  FK); canal, datas, origem e situação preservados; quem/motivo/versão ficam NULL. Nada é criado; Marketing e "Aceita
  comunicações" não viram consentimento. Confere no fim (THROW 50050 se sobrar registro sem finalidade).
- **Down:** desfaz estrutura; falha se já houver consentimento com canal nulo (a coluna volta a ser obrigatória) — esperado,
  não é caminho de produção.

## API e permissões

- `GET pessoas/{id}/privacidade`, `POST pessoas/{id}/consentimentos`, `POST pessoas/{id}/consentimentos/{cid}/revogar` —
  permissão nova **`PESSOAS.PRIVACIDADE`** (consultar, conceder, revogar, histórico). Motivo vai também para a coluna Motivo
  da auditoria (`IMotivoDaOperacao`); o `ColetorAuditoria` registra os campos (finalidade traduzida para o nome no histórico).
- `api/v1/finalidades-tratamento` (CRUD sem exclusão) sob **`CADASTROS.TIPOS`** (cadastros auxiliares de Pessoas); leitura
  também com `PESSOAS.PRIVACIDADE`.
- O `PUT pessoas/{id}` **não** recebe nem grava consentimentos (`PessoaDto.Consentimentos` saiu).

## UI

- Ficha: aba "Interações e LGPD" → **Interações** (segmentação e interações) + **Privacidade** (cadastro gravado e permissão):
  consentimentos por finalidade (Finalidade · Situação · Data), "Conceder consentimento" (finalidade, canal opcional, motivo,
  versão do termo, origem), períodos com Revogar e "Mostrar histórico", e os canais com as marcas e a decisão da regra
  central por finalidade.
- Contatos: "Uso para marketing" (antes "Marketing") e os textos de ajuda sobre "Aceita comunicações" e "Uso para marketing".
- Configurações › Pessoas › **Finalidades de tratamento**.

## Testes criados

`Dominio/PrivacidadeTests` (matriz, todos os resultados, períodos, estados, geral × específico, Registro anterior, cadastro),
`Aplicacao/PrivacidadeAppServiceTests`, `Cliente/PrivacidadeFormularioTests`, `Infraestrutura/MigracaoPrivacidadeTests`
(SQL Server, banco temporário). Substituídos (regra retirada de propósito): os testes antigos de consentimento pelo Salvar em
`Dominio/DadosComplementaresTests` e `Cliente/PessoaDadosComplementaresTests`. Ajustado: `ConfiguracoesViewModelTests`.

## Fora do escopo (documentado)

Pessoas de contato (`Contato`) sem consentimento/"aceita comunicações"; consulta avançada sem filtro por consentimento;
classificação de marketing em telefone; bases legais além de Consentimento; direitos do titular.
