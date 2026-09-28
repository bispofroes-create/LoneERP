# Carteira de clientes, equipes, comissão e cobertura: desenho (28/09/2026)

> Proposta para empresa com equipes grandes e muitos vendedores. É **só desenho**, sem código. As decisões marcadas
> com **[C#]** são do usuário. O objetivo é deixar a estrutura configurável, para não precisar mexer no sistema depois.

## 1. Como os sistemas maduros fazem

| Sistema | Vários vendedores por cliente | Divisão de crédito/comissão | Ausência e cobertura |
|---|---|---|---|
| **Salesforce** | Dono da conta, mais a *equipe da conta* com papéis configuráveis, mais territórios (uma conta pode estar em vários) | *Splits* na oportunidade: o tipo "receita" precisa somar 100%; o tipo *overlay* (especialista, gerente) é crédito extra que não precisa somar 100%; o administrador cria outros tipos | Territórios com vários usuários e papéis; trocar o dono mantém o histórico da equipe |
| **Dynamics 365** | Dono, mais a equipe de vendas (*access team* por modelo), mais territórios | Por extensão ou pelo módulo de comissões | Reatribuição e equipe da conta |
| **SAP SD** | "Funções de parceiro" no cliente (vendedor, representante, gerente…), cada uma com regra de quantos pode haver | A comissão sai das condições do pedido; cada função de parceiro pode ter a sua | O pedido copia os parceiros do cliente e pode ser ajustado |
| **Protheus** | Um vendedor no cliente (`A1_VEND`) e até 5 no pedido (`C5_VEND1..5`, `C5_COMIS1..5`) | Percentual por vendedor no pedido. Prioridade da busca: item → produto → pedido → cliente → vendedor | Sem conceito próprio: troca-se o vendedor do cliente, e o histórico se perde |
| **Oracle Incentive Compensation** | Crédito atribuído por regras (território, produto, cliente) | Crédito de receita e crédito sem receita (*overlay*), com repasse para a hierarquia (gerente) | Regras com vigência |

**O que é comum aos modernos:**
1. Um **responsável principal**, que é o dono da conta e o padrão do pedido.
2. Uma **equipe da conta** com **papéis**.
3. O **crédito** da venda é dividido em percentuais, separado de "quem atende".
4. O pedido **congela** quem recebe o quê no momento da venda. A comissão é calculada sobre o pedido, não sobre a
   carteira de hoje.
5. **Vigência e histórico**: nada é apagado.

## 2. O que o Lone já tem

- `TipoCarteira` (Vendedor, Representante, Televendas, Supervisor e os criados pelo usuário), com um único tipo
  "principal".
- `CarteiraCliente`: tipo, vendedor, empresa, início e fim, exclusivo e ativo. É histórico e nunca é apagada.
- A regra da substituição de vendedor (feita hoje, ainda sem commit): um principal ou exclusivo por vez, com
  confirmação, encerramento na véspera, gatilho no banco e frase no histórico.
- `Equipes`, com membros que têm entrada e saída, e o motor de **metas**, que já lê a carteira dos vendedores.
- Colaborador com vínculos e lotação (empresa, departamento, cargo).
- Ainda não existem pedido, faturamento, comissão nem ausência.

## 3. Proposta: cinco camadas, cada uma configurável

### 3.1 Política por tipo de carteira (configuração, sem código novo depois)

Cada tipo de carteira ganha a sua política, editável em Configurações de Pessoas:

| Campo | Opções | Exemplo |
|---|---|---|
| Quantos ao mesmo tempo | 1 / até N / sem limite | Vendedor: 1 · Representante: até 3 · Especialista: sem limite |
| Crédito | **Receita** (entra na divisão que soma 100%) / **Sobreposição** (crédito extra) / **Nenhum** | Vendedor e Representante: receita · Supervisor: sobreposição · Televendas: nenhum |
| Percentual padrão | 0–100 | Representante: 30% |
| Conta para metas | sim / não | |
| Responsável principal | um tipo só (como hoje) | Vendedor |

- **Quantos ao mesmo tempo** generaliza a regra atual: "1" é o comportamento de hoje (substituição com confirmação),
  "até N" recusa o N+1º e "sem limite" não confere. O campo "Exclusivo" do vínculo continua valendo.
- O **gatilho do banco** passa a ler o limite do tipo, em vez de só "principal".

### 3.2 Divisão do crédito no cliente

- Cada vínculo ativo de tipo **receita** tem um **percentual de crédito**. Por empresa e na mesma data, a soma dos
  vínculos de receita vigentes precisa ser **100%**. Com um só, ele recebe 100% sozinho.
- **Sobreposição** tem o seu percentual e não entra na soma, como o *overlay* do Salesforce (supervisor, especialista
  de produto).
- Ao trocar um vendedor, o novo **herda o percentual** do anterior, o que evita somas quebradas.
- Exemplo com vendedor 70%, representante 30% e supervisor com 5% de sobreposição: a venda gera 100% de crédito de
  receita dividido entre os dois, mais 5% de sobreposição para o supervisor.

### 3.3 Ausências e cobertura (férias, folga, licença)

Tabela nova, **Ausências de vendedor**, com vendedor, tipo (férias, folga, licença, afastamento, treinamento),
início, fim, **substituto** (uma pessoa ou uma equipe), **regra do crédito** e observação.

- **A carteira não muda**, e o histórico continua limpo: a ausência é temporária.
- **Durante a ausência:**
  - o substituto vê e atende os clientes do ausente;
  - o pedido novo sugere o substituto como quem atendeu;
  - a tela avisa "João está de férias até 15/10; atendimento por Maria".
- **Regra do crédito**, configurável por empresa com o padrão da empresa, e ajustável em cada ausência:
  - **Titular:** o crédito continua do ausente, como a comissão da carteira dele. É o comum em folga e férias curtas.
  - **Substituto:** o crédito vai para quem cobre. É o comum em licença longa.
  - **Dividido:** X% para o titular e o resto para o substituto.
- **Sem substituto definido:** a cobertura vai para a **equipe** do vendedor (fila) ou para o **supervisor** da
  carteira, conforme a política.
- **Desligamento não é ausência.** É transferência definitiva da carteira, pela substituição (na ficha ou em lote),
  com a nova data de início.

### 3.4 Regras de comissão (percentual), com vigência e prioridade

Uma tabela única de regras, no lugar de campos espalhados pelos cadastros. Cada regra tem: quem (vendedor, equipe ou
todos), o quê (produto, grupo de produto ou todos), para quem (cliente, grupo de clientes, região ou todos), condição
de pagamento, faixa de desconto, **% de comissão**, **base** (faturado ou recebido), vigência e prioridade.

- **Escolha da regra:** vale a mais específica, como a prioridade do Protheus (item → produto → cliente → vendedor),
  com desempate pela prioridade.
- **Desconto reduz comissão**, em faixas (ex.: até 5% de desconto paga 3%; acima de 10% paga 1%).
- **Devolução e cancelamento** estornam a comissão, e o **recebido** paga só quando o cliente paga.
- O **prêmio das metas** (já existe) pode virar comissão variável depois, sem mudar a estrutura.

### 3.5 O pedido congela a divisão (quando existir o módulo de vendas)

- Ao criar o pedido, o sistema copia a carteira vigente do cliente: vendedores, percentuais de crédito e cobertura,
  se houver ausência.
- Com permissão, o usuário ajusta a divisão **naquele pedido**, e o ajuste fica auditado.
- A comissão é calculada sobre esse retrato. Trocar a carteira amanhã não muda a comissão de ontem.
- Sem limite fixo de vendedores por pedido, ao contrário dos 5 do Protheus.

## 4. Ordem de entrega sugerida

| Fase | O que entra | Banco |
|---|---|---|
| **C1 — agora** | Política por tipo (quantos, crédito, % padrão, metas), % de crédito no vínculo com soma 100%, gatilho lendo o limite, e a tela da carteira nova (vigentes × histórico, "Trocar vendedor", "Adicionar outro tipo", gravado só com fim, observação e ativo editáveis) | Colunas novas em `TiposCarteira` e `CarteiraClientes` (migration: **precisa de aprovação**) |
| **C2** | Ausências e cobertura (tabela, tela na ficha do colaborador, aviso na ficha do cliente, fila da equipe) | Tabela nova (aprovação) |
| **C3** | Regras de comissão (tabela e simulador "quanto João ganharia nesta venda") | Tabela nova (aprovação) |
| **C4** | Retrato no pedido, cálculo, apuração, estorno e pagamento | Junto com o módulo de vendas e o financeiro |

- A C1 muda só o cadastro e deixa tudo pronto para as outras fases, que dependem de vendas.
- A regra da substituição já feita continua valendo para os tipos com limite 1.

## 5. Decisões

- **[C1] Responsável principal:** manter um responsável principal (dono da conta, padrão do pedido), com o crédito
  dividido por percentuais na equipe? (recomendado: modelo Salesforce/SAP) Ou permitir dois principais "iguais"?
- **[C2] Soma 100%:** obrigatória por empresa sempre que houver vínculos de receita? (recomendado) Ou permitir menos
  de 100%, com o resto sem dono?
- **[C3] Crédito durante a ausência, padrão da empresa:** titular (recomendado para férias e folga), substituto ou
  dividido?
- **[C4] Sem substituto:** fila da equipe ou supervisor da carteira?
- **[C5] Base da comissão, padrão:** faturado ou recebido? (fica configurável por regra)
- **[C6] Visibilidade:** vendedor vê só os clientes da carteira dele? (comum em equipes grandes; é permissão nova e vale
  para a lista, a busca e os relatórios)
- **[C7] Começar pela C1 agora**, com migration aprovada?
