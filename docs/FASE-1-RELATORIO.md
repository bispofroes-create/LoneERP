# Motor Comercial, Fase 1: carteira central (relatório, 28/09/2026)

> Pedido pelo prompt mestre (item 92) ao fim de cada fase. A base está em `_entrega/AUDITORIA-MOTOR-COMERCIAL.md` (Fase 0),
> com as decisões MC-1 a MC-10 aprovadas. O plano da última parte está em `docs/PLANO-FASE1D-TRANSFERENCIA.md` (T1 a T7).

## 1. O que foi implementado

| Parte | Commit | Entrega |
|---|---|---|
| Base (Etapa 4, D3) | `184e8f6` | Substituição de vendedor principal/exclusivo (encerra na véspera, confirmação na ficha) e gatilho `TR_CarteiraClientes_SemSobreposicao` |
| 1a | `2aa9977` | Tipo de carteira vira **papel comercial** com política: quantos ao mesmo tempo, crédito (Nenhum, Receita, Sobreposição), % padrão, conta para metas e responsável da conta. Vínculo com % de crédito (a receita soma 100%) e origem. O gatilho passa a seguir o limite do papel |
| 1b | `b33ccb9` | "Quem pode ser" (classificações aceitas por papel). Vínculo que já começou fica travado (só fim, observação e ativo mudam). "Trocar" e "Adicionar papel". Carteira separada em vigentes e histórico |
| 1c | `61ca7b1` | Módulo **Comercial** no menu. Prazo e aviso de fim no cartão do vínculo. **Ausências e coberturas** (a carteira não muda; acabam sozinhas). **Carteira vencendo**. Parâmetros comerciais. Filtros de Pessoas e Metas seguindo o "Quem pode ser" |
| 1d-1 | `21d3cbd` | **Transferência de carteira**: data de efeito (até N dias para trás), prévia com as mesmas regras da ficha, gravação cliente a cliente, número `TR-AAAA-NNNN`, resultado por cliente registrado, divisão pela menor carteira. Parâmetro "Datas no passado", que também vale para o início das coberturas |
| 1d-2 | `519ce89` | Telas: **Transferências** (assistente em 3 passos) e **Carteira em uma data** (por cliente ou por quem atende). **"Abrir ficha"** pelo Id |

## 2. Tabelas

- **Alteradas:**
  - `TiposCarteira`: política do papel, `ResponsavelDaConta` e o check da política.
  - `CarteiraClientes`: `PercentualCredito`, `Origem` e `TransferenciaId`; gatilho de sobreposição e limite.
  - `ParametrosComerciais`: `DiasRetroativosMaximo`.
- **Novas:**
  - `TiposCarteiraClassificacoes` ("Quem pode ser");
  - `TiposAusencia`;
  - `ParametrosComerciais`;
  - `CoberturasComerciais`;
  - `TransferenciasCarteira`;
  - `TransferenciaCarteiraItens`.

## 3. Migrations (todas aplicadas no banco de desenvolvimento)

- **`CarteiraSemSobreposicao`:** gatilho, com SQL manual (`SqlMigracaoCarteira.CriarProtecao`).
- **`PapeisComerciais`:** conversão `Principal` → `ResponsavelDaConta` e troca do gatilho.
- **`QuemPodeSerPapel`:** classificações iniciais Vendedor/Representante.
- **`MotorComercial1c`:** só tabelas e dados iniciais (`HasData`).
- **`MotorComercial1d`:** tabelas, uma coluna nula e um `UpdateData`.

Nenhuma delas tem DROP no Up nem exclusão de dados.

## 4. Regras principais (onde estão)

- **`RegrasComercial` (domínio):**
  - conflito único;
  - substituição;
  - validação da carteira: limite por papel e crédito somando 100% por empresa e data, conferidos só no que a gravação acrescenta;
  - histórico que não é reescrito;
  - origem e transferência do vínculo, definidas pelo servidor;
  - `CreditosEmData`.
- **`RegrasCobertura`:** sobreposição no mesmo escopo, o que muda depois de começar, cancelamento e limite de dias no passado.
- **`RegrasTransferencia`:** pedido, candidatos, plano por cliente, divisão, frases e contagens.
- **No banco:**
  - gatilho da carteira (um por vez, exclusivo e limite);
  - checks de papel, cobertura e parâmetros;
  - índice único do número da transferência.

## 5. Testes

1.411 aprovados, 0 falhas e 22 ignorados (os de banco, que precisam de SQL Server de teste). Antes da Fase 1 eram 1.338.
Build com 0 avisos e 0 erros. O usuário testou no app cada parte, inclusive a transferência de ponta a ponta.

## 6. Limitações conhecidas

- **Coberturas:** a regra de crédito e o acesso gravados ainda não são usados em cálculo. O crédito entra na Fase 4 e o acesso (escopo) na Fase 2.
- **Aviso na ficha:** o cartão da ficha ainda destaca "perto do fim" em vínculo já trocado. A "Carteira vencendo" já desconta esse caso.
- **Vendedor padrão:** falta uma rotina diária que atualize o vendedor padrão quando um vínculo começa ou vence sem a ficha ser salva. Transferência com efeito futuro só vira vendedor padrão quando o cliente é gravado de novo.
- **Consultas:** `CoberturaConsultas.ContarClientesAsync` faz uma consulta por cobertura.
- **Datas:** o cliente usa `DateTime.Today` e o servidor `GetLocalNow()`; perto da meia-noite podem divergir.
- **Tamanho da transferência:** até 500 clientes, síncrona. Fila em segundo plano (MC-10) quando for preciso.
- **Sem teste de banco** do gatilho e da transferência (precisam de SQL Server de teste).

## 7. Pendências da Fase 1 e próximos passos

- **MC-4 (feita em 28/09/2026, sem migration):** o grupo de empresas é só o `GrupoEmpresarial`.
  - O banco foi conferido pelo usuário: `GruposEconomicos` com 0 registros e `Pessoas.GrupoEconomicoId` com 0 pessoas.
  - Por isso nada foi convertido. O campo saiu de `Pessoa`, do `PessoaDto`, do mapeamento e da ficha.
  - A coluna e a tabela ficam no banco como estavam (propriedade de sombra e classe `GrupoEconomico` marcada como legado), sem DROP.
- **Pedidos de interface anotados em `docs/UX-ARQUITETURA.md`:** dias e aviso em todo período, campo de data com calendário, campo "Dias" e dia útil.
- **Fase 2 (auditoria, seção 9):** equipes com hierarquia, territórios (regras pelo catálogo de filtros), distribuição, capacidade e **escopo de acesso**.
