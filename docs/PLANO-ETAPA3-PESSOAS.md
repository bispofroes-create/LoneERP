# Etapa 3 da tela de Pessoas — prévia lateral e indicadores (plano, 27/09/2026)

> Só análise e plano. Nada foi implementado. Decisões marcadas com **[D#]** precisam da resposta do usuário.

**Decisões do usuário (27/09/2026, noite):**
- **D1:** cada usuário escolhe, em Configurações da tela, o que o clique faz. O padrão é a prévia.
- **D2:** a prévia fica aberta de uma pessoa para outra e é lembrada.
- **D3:** "pendência cadastral" = sem CPF/CNPJ, sem endereço ativo ou com município a corrigir, e contribuinte sem IE.
  O CPF/CNPJ duplicado fica fora.
- **D4:** "vencendo" segue a antecedência de cada tipo de documento.
- **D5:** os números valem para a base toda.

**Andamento:** 3a (prévia) entregue em 27/09 à noite, sem compilar. Detalhes e o que ficou de fora (teclado) estão em
`CONTINUIDADE.md`. Falta a 3b (indicadores).

## 1. O que já existe e pode ser reaproveitado

| Peça | Onde | Serve para |
|---|---|---|
| Resumo da pessoa (blocos Situação, Documentos, Cadastro; níveis Alerta/Atenção/Informação/Ok; tocar leva à aba) | `Lone.Cliente/ViewModels/Pessoas/ResumoPessoa.cs` (`IFonteResumoPessoa`) + `Views/Pessoas/ResumoPessoaView.xaml` | Corpo da prévia, sem mudar nada nas fontes |
| Leitura da ficha | `PessoasApi.ObterAsync(id)` + `PessoaFormulario.De(...)` (os cadastros auxiliares já estão carregados na tela) | Prévia sem endpoint novo |
| Motor de filtros com contagem | `IConsultaPessoas.ContarAsync(List<CriteriosPessoas>)` (o mesmo das abas de visão) | Números dos indicadores |
| Campos do catálogo | `situacao.bloqueado`, `documentos.vencidos`, `documentos.vencendo` (N dias), `enderecos.semEndereco`, `enderecos.municipioACorrigir` | Clique no indicador vira chip no painel de filtros |

Hoje tocar na linha abre a ficha (`AbrirLinhaCommand`). As condições do filtro são sempre combinadas com **E**, não há **OU**.

## 2. Como os ERPs maduros fazem

- **Prévia:** Dynamics 365 (painel de visualização rápida), HubSpot e Pipedrive (painel lateral ao clicar no registro) e
  Salesforce (cartão ao passar o mouse). O padrão comum é o clique simples mostrar a prévia, e abrir o registro ficar
  num botão da prévia ou no duplo clique. Protheus, Bling e Omie não têm prévia: o clique abre o cadastro.
- **Indicadores:** Dynamics e HubSpot mostram uma faixa de números acima da lista. Tocar no número aplica o filtro
  correspondente. Os números valem para a base toda (KPI), não para a busca digitada.

## 3. Prévia lateral

**Proposta:**
- Em tela larga (≥ 1280), o painel fica à direita da lista, com cerca de 380 de largura. Ele empurra a lista, e a rolagem
  lateral das colunas continua funcionando.
- Conteúdo do painel:
  - Cabeçalho: avatar, nome, código, CPF/CNPJ e selos de papéis.
  - Ações: "Abrir ficha" (principal), ligar, WhatsApp e e-mail.
  - Corpo: o próprio Resumo da pessoa, reaproveitado sem mudar as fontes. Tocar num item abre a ficha já na aba do item.
- Teclado: ↑↓ trocam a pessoa da prévia, Enter abre a ficha e Esc fecha a prévia.
- No celular não há prévia: tocar continua abrindo a ficha, como hoje.
- Leitura: um `ObterAsync` por pessoa, esperando 150 ms e cancelando a anterior se o usuário andar rápido. A prévia
  guarda as últimas 5 lidas. Se a pessoa foi alterada, relê ao abrir a ficha, que já faz isso.
- Banco: nada muda. API: nada muda. As permissões são as mesmas da ficha, porque o `ObterAsync` já aplica as dela.

**[D1] O que o clique na linha faz em tela larga:**
- **A (recomendada):** o clique mostra a prévia; duplo clique, Enter ou "Abrir ficha" abrem a ficha. É o padrão do
  Dynamics e do HubSpot.
- **B:** o clique continua abrindo a ficha e um ícone 👁 na linha (ou a tecla Espaço) mostra a prévia. Mantém o hábito,
  mas a prévia fica escondida.
- **C:** fica a critério de cada usuário, em "Configurações da tela" (padrão A). Custa uma opção a mais na preferência,
  sem mudança de banco (vai no JSON do layout).

**[D2]** A prévia fica aberta de uma pessoa para a outra (só troca o conteúdo) e é lembrada na preferência da tela?
Recomendado: sim para as duas.

## 4. Faixa de indicadores

**Proposta:** uma faixa fina entre a barra (busca, Visões, Filtros, ⋯) e as abas, com até 4 números:

| Indicador | Condição (catálogo) | Cor |
|---|---|---|
| Com bloqueio ativo | `situacao.bloqueado = Sim` | alerta |
| Documentos vencidos | `documentos.vencidos = Sim` | alerta |
| Documentos vencendo | ver **[D4]** | atenção |
| Com pendência cadastral | campo novo, ver **[D3]** | atenção |

- Tocar num indicador põe a condição como chip no painel. Tocar de novo tira. A aba e a busca atuais continuam valendo.
- O número vale para a base toda (cadastros ativos e em análise), como um KPI. É contado no máximo 1 vez por minuto e
  depois de salvar ou desativar. Assim não pesa em cada busca digitada, que já faz 2 consultas a mais pelas abas.
- Indicador com 0 fica apagado mas visível, para mostrar que está tudo em dia.
- Campo sem permissão para o usuário faz o indicador sumir.
- Um "⋯" na faixa permite escolher quais indicadores mostrar ou esconder a faixa. Isso fica na preferência da tela.
- A faixa usa um endpoint novo, `GET pessoas/consulta/indicadores`, que monta os critérios fixos e chama o mesmo
  `ContarAsync`. Tudo em LINQ parametrizado.
- **Sem mudança de banco e sem índices novos:** medir antes em base grande.

**[D3] O que é "pendência cadastral"** (regra de negócio: preciso da definição). O motor não tem OU, então a proposta é
um campo novo `cadastro.comPendencia` (Sim/Não), cuja consulta junta com OU uma lista fixa de pendências. A lista fica
num lugar só em `Contracts`, usada pelo Resumo (cliente) e pela consulta (servidor), com um teste garantindo que os dois
contam o mesmo. Candidatos, tirados do bloco "Cadastro" do Resumo:

| Pendência | Nível hoje no Resumo | Entra? (sugestão) |
|---|---|---|
| PF sem CPF / PJ sem CNPJ | Atenção | sim |
| Nenhum endereço ativo | Atenção | sim |
| Endereço com município a corrigir | Atenção | sim |
| Contribuinte do ICMS sem IE | Alerta | sim |
| CPF/CNPJ usado em outro cadastro | Alerta | sim, mas é mais caro (autojunção); medir |
| Nenhum telefone ou e-mail | Informação | não |
| PJ com regime tributário não informado | Informação | não |

**[D4] "Documentos vencendo":**
- **A (recomendada):** pela antecedência de cada tipo de documento, igual ao Resumo. É coerente com a ficha, mas pede um
  operador novo no catálogo ("vencendo pela antecedência do tipo").
- **B:** fixo em 30 dias, usando o campo que já existe (`documentos.vencendo` até 30). É mais simples, mas pode
  divergir do Resumo.

**[D5]** O número segue a aba e a busca atuais, em vez de valer para a base toda? Não recomendado: são mais 4 contagens
por busca. Se o usuário quiser, dá para contar só ao parar de digitar.

## 5. Ordem de entrega (incremental)

1. **3a — Prévia** (só cliente): `PreviaPessoa` no `PessoasViewModel`, painel no `PessoasPage.xaml` e teclado.
   Testes: cliques e troca de pessoa, cancelamento da leitura anterior e fila do `ServidorFalso` (cada prévia = 1 GET).
2. **3b — Indicadores:**
   - Servidor: campo `cadastro.comPendencia` e, se [D4] = A, o operador novo.
   - Endpoint `GET pessoas/consulta/indicadores`.
   - Tela: faixa, chip e preferência.
   - Testes: catálogo, SQL (os mesmos casos do Resumo), contagem, permissões e fila do cliente.
3. Nenhuma migration. Se as contagens ficarem lentas, medir e propor migration só de índices, com aprovação.

## 6. Riscos

- **Mudar o clique ([D1] A)** muda um hábito. Mitigação: o duplo clique e o Enter abrem a ficha, e o botão "Abrir ficha"
  fica em destaque na prévia.
- **Regra de pendência duplicada** (cliente e servidor). Mitigação: a lista única em `Contracts` e o teste de coerência.
- **Desempenho da autojunção de CPF/CNPJ duplicado.** Mitigação: deixar fora da primeira entrega se a medição for ruim.
