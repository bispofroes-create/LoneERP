# Fase 3 — Privacidade e LGPD: auditoria e proposta (base: commit 872dda0)

Só análise. Nenhum código, banco ou migration foi alterado. Implementação só depois da aprovação e das decisões da seção D.

---

## A. Estruturas existentes

| Conceito | Onde está | Detalhes |
|---|---|---|
| Pessoa | `Pessoa` (AgregadoRaiz, tabela `Pessoas`) | Raiz; filhos herdam `EntidadePessoaFilha` e são gravados no `PUT pessoas/{id}` (sincronizados por `SincronizarFilhos`). |
| Telefone / e-mail | `MeioContato` (tabela `PessoaMeiosContato`) | `Tipo` (enum `TipoContato`), `Valor`, `TipoMeioContatoId` (cadastro de tipos), `Ramal`, `WhatsApp`, `Sms`, `Finalidades` (flags `FinalidadeEmail`), `Principal`, **`PermiteComunicacao`**, `Ativo`. Nunca apagado (desativa). |
| "Aceita comunicações" | `MeioContato.PermiteComunicacao` (bool, padrão **true**) | Já é propriedade do canal. **Nenhuma regra usa** esse campo hoje. |
| Marketing | `MeioContato.Finalidades` com o bit `FinalidadeEmail.Marketing` (8) | Só existe para **e-mail** (junto de Financeiro, Cobrança, NF-e). Telefone não tem classificação de uso. Nenhuma regra usa. |
| Consentimento | `PessoaConsentimento` (tabela `PessoaConsentimentos`, migração `DadosComplementaresPessoa`) | **Um registro por canal** (`Canal`: E-mail, WhatsApp, SMS, Telefone, Correspondência — enum `CanalComunicacao`), `Concedido`, `ConcedidoEm`, `RevogadoEm`, `Origem` (80). Índice único `(PessoaId, Canal)`. **Sem finalidade.** Gravado junto com a ficha. |
| Regras de consentimento | `PessoaNormalizador.NormalizarConsentimentos` (datas do servidor), `PessoaAppService.ManterDatasDosConsentimentos` (datas gravadas valem; o aparelho não define datas), `PessoaValidador` (canal válido, origem ≤ 80) | Não há regra de decisão ("pode comunicar?") em lugar nenhum. |
| Pessoas de contato | `Contato` (sem cadastro próprio: nome, cargo, telefone, celular, e-mail) | Sem "aceita comunicações" nem consentimento. Fora do escopo (ver E). |
| Auditoria | `ColetorAuditoria` dentro de `LoneDbContext.SaveChangesAsync`, tabela `Auditoria` | Uma linha por campo alterado de toda entidade com `[DisplayName]`, com usuário, data/hora e **Motivo** (fase 6). `PessoaConsentimento` e `MeioContato` já são auditados. Eventos de negócio (frase) via `AgregadoRaiz`. |
| Histórico temporal (padrão já usado) | `PessoaPapel` (períodos, nunca apagados), `Bloqueio` (início/fim, quem, motivo), `VinculosColaborador`, `HistoricoFiscal` | Modelo de "período com início e fim + quem/por quê", sem sobrescrever. |
| Ações fora do Salvar (padrão já usado) | Bloqueios (`POST pessoas/{id}/bloqueios`, `.../liberar`), desativar/reativar, relacionamentos | Motivo obrigatório vai para a coluna de auditoria. |
| Cadastros auxiliares (padrão já usado) | `FinalidadeEnderecoCadastro`, `TipoDocumentoCadastro`, `Papel` | Código imutável + nome + ordem + `DoSistema` + ativo; os de sistema com Ids fixos (HasData); nunca excluídos. |
| Permissões | `Lone.Contracts/Seguranca/Permissoes.cs`; `IAutorizacao.Exigir` nos AppServices | Nenhuma permissão específica de privacidade hoje. |
| Enums rígidos relacionados | `CanalComunicacao`, `FinalidadeEmail` (flags), `TipoContato` | Técnicos e estáveis; o que falta é a **finalidade do consentimento**, que hoje não existe. |
| UI | Aba "Interações e LGPD" (`SecaoRelacionamentoView`): segmentação, **checkbox por canal** + "Como foi autorizado", interações. Aba Contatos: checkboxes "Aceita comunicações" e "Marketing" por item. | Três marcações sem explicação da diferença — exatamente o que a especificação pede para evitar. |

## B. Conflitos

1. **Consentimento sem finalidade** — é por canal. Não responde "autorizou *para quê*?".
2. **Fontes de verdade concorrentes por canal**: consentimento "WhatsApp"/"SMS" × marcações `MeioContato.WhatsApp`/`Sms` × `PermiteComunicacao`. O consentimento por canal mistura "autorização" com "meio".
3. **Histórico sobrescrito na linha**: o índice único `(PessoaId, Canal)` obriga uma linha só; reautorizar apaga `RevogadoEm` e troca `ConcedidoEm`. O histórico existe só na auditoria por campo, não como períodos consultáveis.
4. **"Não informado" × "Revogado"**: a linha distingue (RevogadoEm preenchido), mas a tela é um checkbox Sim/Não.
5. **Consentimento gravado pelo Salvar da ficha**: uma edição qualquer da ficha pode mexer no consentimento; não há ação própria com motivo, como os bloqueios.
6. **Marketing só existe no e-mail**: não há como classificar um telefone/WhatsApp para marketing.
7. **Nenhuma regra central**: nada decide hoje se um canal pode ser usado; não há regra espalhada a remover (bom ponto de partida).
8. `PermiteComunicacao` nasce **true** por padrão — correto como restrição (não autoriza nada), desde que a regra central exija o consentimento quando a base legal for consentimento.

## C. Proposta

### Mantido
- `MeioContato.PermiteComunicacao` (mesma coluna) = "Aceita comunicações": restrição do canal.
- `FinalidadeEmail.Marketing` no e-mail = "Uso para marketing": classificação.
- `ColetorAuditoria`, coluna Motivo, eventos de negócio; padrões de cadastro auxiliar, de períodos e de ações próprias.
- Todo o resto da ficha (fases 1, 2, 5), endereços, documentos, relacionamentos.

### Criado
1. **Cadastro `FinalidadesTratamento`** (padrão `FinalidadeEnderecoCadastro`): `Codigo` imutável, `Nome`, `Descricao`, **`BaseLegal`**, **`ClassificacaoExigida`** (qual uso do canal a finalidade exige — ex.: Marketing exige e-mail com "Uso para marketing"), `Ordem`, `DoSistema`, `Ativo`. Nunca excluída. Tela em Configurações › Pessoas.
2. **Enum `BaseLegal`**: Consentimento, Execução de contrato, Obrigação legal/regulatória, Legítimo interesse, Exercício regular de direitos (art. 7º da LGPD — lista da própria lei, por isso enum; a finalidade é que é cadastro).
3. **Consentimento como períodos** (evolução de `PessoaConsentimento`, sem tabela paralela): cada concessão é uma linha nova; revogar encerra a linha. Campos: `FinalidadeId`, `Canal` (opcional — ver D3), `ConcedidoEm`/`ConcedidoPor`, `RevogadoEm`/`RevogadoPor`, `Origem`, `VersaoTermo` (opcional, texto), `Observacao`. Nada é apagado. Situação calculada: **Concedido / Revogado / Não informado** (sem linha) / **Não aplicável** (finalidade cuja base legal não é consentimento).
4. **Ações próprias** fora do Salvar: `POST pessoas/{id}/consentimentos` (conceder) e `.../consentimentos/{id}/revogar` (motivo → coluna de auditoria), `GET .../consentimentos/historico`. O Salvar da ficha deixa de mexer em consentimento.
5. **Regra central** `Lone.Domain/Privacidade/RegrasComunicacao.PodeComunicar(pessoa, meio, finalidade, momento)` → `DecisaoComunicacao { Resultado, Motivo }`, na ordem: finalidade ativa → base legal → consentimento vigente (só se a base for consentimento) → canal ativo → canal aceita comunicações → classificação exigida → demais restrições. Resultados: `Permitido`, `BloqueadoPorConsentimento`, `BloqueadoPeloCanal`, `BloqueadoPorFinalidade` (canal não classificado / finalidade inativa), `BloqueadoPorCanalInativo`, `SemBaseLegalAplicavel`. Nenhum dos três campos altera os outros.
6. Permissão nova para conceder/revogar (ver D7).

### Alterado
- `PessoaConsentimento`: + `FinalidadeId`, `Canal` passa a opcional, + quem/versão/observação; o índice único `(PessoaId, Canal)` sai e entra um índice único **filtrado** "um consentimento vigente por pessoa + finalidade (+ canal)".
- DTO/mapeamento/repositório: consentimentos saem do `PUT` da ficha (a ficha só lê).
- UI: aba "Interações e LGPD" vira **"Interações"** (origem, primeiro contato, interações) + **"Privacidade"** (consentimentos por finalidade com Conceder/Revogar/Histórico; quadro dos canais com "Aceita comunicações", "Uso para marketing" e o resultado da regra para cada finalidade). Na aba Contatos os rótulos passam a "Aceita comunicações" e "Uso para marketing" com os textos de ajuda da especificação.

### Removido
- Nada é apagado do banco. O checkbox por canal sai da tela; os registros antigos continuam (ver D1).

### Migration (uma, gerada por você no PMC, como sempre)
- Tabela `FinalidadesTratamento` + dados de sistema (HasData, Ids fixos).
- Colunas novas em `PessoaConsentimentos`; troca de índices; SQL de dados só para o que for decidido em D1 (sem inventar consentimento).
- Não aplicada automaticamente em produção.

### Impacto
- **Dados:** depende de D1. `Marketing` e `PermiteComunicacao` **não** viram consentimento em hipótese alguma.
- **UI:** abas Interações e Privacidade; Contatos com textos novos; Configurações ganha "Finalidades de tratamento".
- **Testes:** novos (domínio: regra central e matriz da seção 18; aplicação: conceder/revogar/histórico/finalidade inexistente; cliente: aba Privacidade). Ajustes nos testes que montam consentimento pelo `PessoaDto` e na fila do servidor falso da tela de Pessoas.

## D. Decisões de negócio necessárias (antes de codificar)

1. **Consentimentos antigos (por canal, sem finalidade).** Opções: (a) manter como histórico só leitura, ligados a uma finalidade de sistema "Registro anterior (sem finalidade)" que **não autoriza nenhuma finalidade**; (b) converter para uma finalidade que você indicar (ex.: "Comunicações comerciais") — isso seria afirmar uma finalidade que o registro não tem. Recomendação: (a).
2. **Finalidades iniciais e base legal de cada uma.** A especificação proíbe assumir regra jurídica. Recomendação: criar só **Marketing** (base Consentimento, exige "Uso para marketing") e deixar as demais para você cadastrar, ou você lista quais criar e com qual base.
3. **Consentimento por finalidade ou por finalidade + canal?** (ex.: "Marketing por e-mail" concedido, "Marketing por WhatsApp" não). Recomendação: finalidade com canal **opcional** (vazio = qualquer canal).
4. **Marketing por telefone/WhatsApp/SMS:** hoje só o e-mail tem "Uso para marketing". Opções: (a) criar a mesma classificação para telefones (usa a coluna `Finalidades` que já existe, sem coluna nova); (b) marketing só por e-mail (telefone sempre "não classificado").
5. **Demais restrições:** pessoa **inativa** bloqueia comunicação? Bloqueios existentes (vender/faturar/financeiro) **não** são de comunicação — recomendação: não usá-los. Pessoa inativa: bloquear (`BloqueadoPorSituacao`)?
6. **Finalidade com base diferente de consentimento** (contrato, obrigação legal...): dispensa só o consentimento e continua exigindo canal ativo + aceita comunicações + classificação? Ou ignora "Aceita comunicações" (ex.: aviso de cobrança por obrigação contratual)? Recomendação: **continua respeitando o canal** (a especificação diz "canal restringe").
7. **Permissões:** nova `PESSOAS.PRIVACIDADE` para conceder/revogar; cadastro de finalidades sob `CADASTROS.TIPOS` ou nova `CADASTROS.FINALIDADES_TRATAMENTO`?
8. **Versão do termo:** o Lone não tem cadastro de termos. Recomendação: campo de texto opcional (ex.: "Política v2 — 01/2026"), sem cadastro.

## E. Fora do escopo (documentado, não feito)
- Pessoas de contato (`Contato`) têm telefone/e-mail sem "aceita comunicações" nem consentimento.
- Consulta avançada não filtra por consentimento/decisão (pode entrar depois usando a regra central).
- Direitos do titular (acesso, portabilidade, eliminação/anonimização) — outra fase.

## F. Conferência de dados que só você consegue fazer (SQL Server)
```sql
SELECT Canal, Concedido, COUNT(*) Qtde, MIN(ConcedidoEm) Primeiro, MAX(ConcedidoEm) Ultimo
FROM PessoaConsentimentos GROUP BY Canal, Concedido ORDER BY Canal, Concedido;

SELECT Tipo, PermiteComunicacao, CASE WHEN Finalidades & 8 = 8 THEN 1 ELSE 0 END Marketing, COUNT(*) Qtde
FROM PessoaMeiosContato GROUP BY Tipo, PermiteComunicacao, CASE WHEN Finalidades & 8 = 8 THEN 1 ELSE 0 END;
```
