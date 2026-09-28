# Motor Comercial, Fase 1d: transferência de carteira e consulta "em uma data" (plano, 28/09/2026)

> Só análise e plano: nada foi implementado. As decisões marcadas com **[T#]** precisam da resposta do usuário.
> Base: auditoria (`_entrega/AUDITORIA-MOTOR-COMERCIAL.md`, seções 6 e 7), decisões MC-1 a MC-10 e o código da 1a a 1c.

## 1. O que já existe e é reaproveitado

| Peça | Onde | Uso na 1d |
|---|---|---|
| Regra única de conflito e substituição | `RegrasComercial.Conflitam`, `PlanejarSubstituicao`, `Substituir` | Encerrar o vínculo de origem na véspera. O retroativo continua impedido |
| Validações da carteira | `Validar`, `ValidarHistorico`, `ValidarCarteira` (limite por papel, crédito somando 100%), `ValidarReferencias` ("Quem pode ser") | As mesmas regras, cliente a cliente, sem regra paralela |
| Origem do vínculo | `OrigemVinculoCarteira.Transferencia` (já no enum) e `DefinirOrigens(..., origemDosNovos)` | O vínculo novo nasce com origem "Transferência" |
| Gravação protegida | `PessoaRepositorio.SalvarAsync` (dois passos), `rowversion` da pessoa e gatilho `TR_CarteiraClientes_SemSobreposicao` | Cada cliente numa transação própria; concorrência dá 409 só naquele cliente |
| Consulta temporal | `RegrasComercial.CreditosEmData`, `CarteiraCliente.Vigente(data)`, `RegrasCobertura` | A tela "Carteira em uma data" |
| Auditoria e eventos | `ColetorAuditoria`, `RegistrarEvento`, `IMotivoDaOperacao` | Frase no histórico de cada cliente e o motivo da transferência na coluna Motivo |

## 2. Como os sistemas maduros fazem

- **Salesforce ("Mass Transfer Accounts"):** filtro por dono atual, destino único e opções do que levar junto (oportunidades abertas, casos). O resultado é por registro. Sem data de efeito: vale na hora.
- **Dynamics 365 ("Reassign records"):** reatribui tudo o que é de um usuário (desligamento), com destino único. Também sem data de efeito.
- **SAP (funções de parceiro) e Protheus:** alteração em massa do vendedor do cliente, com filtro. O Protheus perde o histórico.
- **O Lone vai além:** tem **data de efeito**, o histórico preservado (encerra e abre), a **prévia antes de gravar**, **vários destinos** e o registro da transferência como documento consultável depois.

## 3. Proposta

### 3.1 O que é uma transferência

"A partir de **01/10/2026**, os clientes de **João** no papel **Vendedor** (empresa X ou todas) passam para **Maria**. Motivo: **desligamento**."

1. **Filtro da origem:**
   - pessoa de origem (obrigatória);
   - papel (um, ou todos os papéis dela);
   - empresa (uma, ou todas);
   - opcionalmente, só alguns clientes marcados na prévia.
2. **Destino:** uma pessoa, ou **vários destinos** (**[T2]**).
3. **Data de efeito** (**[T1]**), **motivo obrigatório** (cadastro ou texto, **[T5]**) e observação.
4. **Por cliente, a execução:**
   - encerra o vínculo de origem em `efeito − 1 dia`;
   - abre o vínculo do destino com o mesmo papel, empresa, exclusivo e **% de crédito** (herda o %: a soma de 100% não quebra);
   - grava `Origem = Transferencia` e `TransferenciaId`;
   - registra a frase no histórico: "Carteira: João (Vendedor) transferido para Maria a partir de 01/10/2026 (TR-2026-0003: desligamento)".

### 3.2 Prévia (nada é gravado)

O sistema monta o plano cliente a cliente, com as **mesmas regras** da ficha, e mostra:

| Grupo | Exemplo |
|---|---|
| **Serão transferidos** | 132 clientes. Por destino, com a contagem, quando houver vários |
| **Não serão processados**, com o motivo | O vínculo começa no dia do efeito ou depois (retroativo); o destino já atende o cliente nesse papel; o destino não pode ocupar o papel ("Quem pode ser"); o papel ficaria acima do limite; o vínculo já termina antes do efeito |
| **Avisos** (não impedem) | Ausências agendadas de João que continuam cadastradas; vínculos futuros de João (começam depois do efeito) que ficam como estão (**[T4]**); clientes com vínculo exclusivo |

O usuário pode desmarcar clientes e confirma com "Transferir 132 clientes".

### 3.3 Execução e resultado

- **Cada cliente vale por si** (mesma decisão D2 da Etapa 4). O cliente é relido, reconferido e gravado na própria transação. Um erro não desfaz os outros.
- **Só as regras comerciais são conferidas**, não a ficha inteira. Um cliente com pendência cadastral (por exemplo, um endereço sem município) não pode travar a transferência. Por isso, a transferência usa um caminho próprio no serviço: carteira, histórico, limite, crédito e referências, mais a gravação protegida. Não passa pelo `PessoaAppService.SalvarAsync`.
- **Resultado:** "130 transferidos, 1 não processado, 1 com erro (conflito de edição: tente de novo só este)". O resultado fica gravado e pode ser reaberto depois.
- **Limite por transferência:** 500 clientes, síncrono. Acima disso, o usuário filtra por papel ou empresa. A fila em segundo plano (MC-10) entra quando for construída (**[T3]**).

### 3.4 Registro da transferência (tabelas novas)

- 🆕 **`TransferenciasCarteira`:**
  - Id e **número legível** (`TR-2026-0001`);
  - `EfeitoEm`, `OrigemId`, `TipoCarteiraId` (nulo = todos), `EmpresaId` (nulo = todas);
  - `Motivo` (obrigatório, 250), `Observacao`;
  - usuário e data (da `EntidadeBase` e da auditoria);
  - contagens: transferidos, não processados e erros.
- 🆕 **`TransferenciaCarteiraItens`:** um por cliente, com `ClienteId`, `VinculoEncerradoId`, `VinculoNovoId`, `DestinoId`, resultado (Transferido, NãoProcessado, Erro) e motivo.
  - É o "resultado por cliente" durável, que responde depois a "quem foi transferido nesse lote e por quê".
  - Os destinos ficam nos itens, sem precisar de uma tabela de destinos.
- 🟢 **`CarteiraClientes.TransferenciaId`:** coluna nula com FK e índice.
- **Nada é apagado nem alterado depois.** Uma transferência errada se desfaz com **outra transferência** (Maria → João), que também fica registrada. Não existe "estornar" que apague.
- Os itens gravam o resultado e o motivo, com o impacto descrito em texto. O "impacto em comissão" do prompt entra na Fase 7, quando houver comissão.

### 3.5 Consulta "Carteira em uma data"

É a tela do princípio 99 do prompt, no módulo Comercial, com dois modos:

- **Por cliente:** escolhe o cliente e a data (padrão: hoje) e vê:
  - quem ocupava cada papel e em qual empresa;
  - o crédito de receita e de sobreposição (`CreditosEmData`);
  - as coberturas vigentes (titular ausente e quem cobria);
  - a origem de cada vínculo (manual, substituição, transferência TR-…).
- **Por pessoa (quem atende):** escolhe a pessoa e a data e vê a lista dos clientes dela naquele dia, por papel. A prévia da transferência usa a mesma consulta.

Equipe e território entram nessa tela na Fase 2, sem mudar a estrutura dela.

### 3.6 Mesma peça para a Etapa 4b (vendedor em lote na lista de Pessoas)

A 4b ("atribuir vendedor às pessoas marcadas") e a 1d mudam a carteira da mesma forma: plano por cliente, prévia,
execução cliente a cliente e resultado. A proposta é **um serviço único** (`MotorDeCarteira`, em `Lone.Application`),
com o plano puro no domínio (`RegrasCarteiraLote`) e duas entradas:
- **1d:** escolha pela origem;
- **4b:** escolha pelos Ids ou critérios da lista.

A 4b fica quase pronta com a 1d.

### 3.7 Permissões, telas e testes

- **Permissão nova `COMERCIAL.TRANSFERIR`:** transferir e ver as transferências. Para consultar a carteira numa data, basta `COMERCIAL.VISUALIZAR`.
- **Telas (menu Comercial):**
  - **"Transferências"**, com a lista e o botão "Nova transferência", que abre o assistente em 3 passos: origem e filtro → destino, data e motivo → prévia → resultado;
  - **"Carteira em uma data"**.
- **API:**
  - `POST comercial/transferencias/previa`;
  - `POST comercial/transferencias`;
  - `GET comercial/transferencias[/{id}]`;
  - `GET comercial/carteira-em-data?cliente=|pessoa=&data=`.
- **Testes:**
  - domínio (plano: normal, retroativo, destino já atende, fora do "Quem pode ser", limite, herança do %, vários destinos, vínculo futuro);
  - serviço (um cliente falha e os outros ficam; motivo obrigatório; permissão);
  - tela (assistente, prévia e resultado);
  - modelo (FK e índice do `TransferenciaId`).

### 3.8 Banco (migration `MotorComercial1d`, precisa de aprovação)

- Duas tabelas novas e uma coluna nula nova em `CarteiraClientes`, com FK e índice.
- **Nenhum SQL de conversão** (os vínculos antigos ficam com `TransferenciaId` nulo) e nenhum DROP.
- O gatilho da carteira **não muda**: a transferência grava pelo mesmo caminho da ficha.

## 4. Decisões

- **[T1] Data de efeito no passado** (ex.: João saiu dia 20 e a transferência é lançada no dia 28):
  - **A (recomendado):** aceitar até N dias para trás (padrão 30, em Parâmetros comerciais), com aviso na prévia ("vendas entre 20/09 e 28/09 mudam de dono no crédito") e o motivo obrigatório. Acima disso, recusar. Hoje a ficha já permite encerrar com data passada.
  - **B:** só hoje ou datas futuras.
  - **C:** qualquer data. Não recomendado: reescreve períodos já fechados, como metas apuradas e, no futuro, comissões pagas.
- **[T2] Vários destinos:**
  - **A (recomendado):** um destino por padrão; "dividir entre vários" reparte pela menor carteira (quem tem menos clientes recebe primeiro), e o usuário pode trocar o destino de cada cliente na prévia.
  - **B:** só um destino (Salesforce e Dynamics). Para dividir, fazem-se duas transferências marcando os clientes.
  - **C:** divisão em partes iguais, na ordem alfabética.
- **[T3] Tamanho:** até 500 clientes por transferência, síncrono, como na D1 da Etapa 4 *(recomendado)*? Ou já construir a fila em segundo plano (MC-10) agora, o que deixa a 1d maior?
- **[T4] Vínculos futuros da origem** (começam depois do efeito):
  - **A (recomendado):** ficam como estão e aparecem como aviso na prévia.
  - **B:** passam também para o destino (corrigir um planejado é permitido).
- **[T5] Motivo:**
  - **A (recomendado):** texto livre obrigatório agora.
  - **B:** cadastro "Motivos de transferência" (Desligamento, Reorganização, Pedido do cliente…), bom para relatórios e com uma tabela a mais.
- **[T6] Pendência da 1c, cobertura começando no passado** (crédito retroativo): aceitar? *(recomendado: sim, até o mesmo limite de dias do T1, com aviso.)*
- **[T7] "Abrir ficha" nos resultados** (transferência, "Carteira vencendo", carteira em uma data): criar agora a navegação direta para a ficha pelo Id, que também resolve a ideia anotada na 1c? *(recomendado: sim, é pequena.)*

### Decisões do usuário (28/09/2026, 13h20)

O usuário aceitou todas as recomendações:
- **T1:** efeito até 30 dias para trás (parâmetro), com aviso e motivo.
- **T2:** um destino, com a opção de dividir pela menor carteira e trocar o destino de cada cliente na prévia.
- **T3:** até 500 clientes, síncrono.
- **T4:** os vínculos futuros ficam como estão (aviso).
- **T5:** motivo em texto obrigatório.
- **T6:** cobertura começando no passado, até o mesmo limite de dias.
- **T7:** "Abrir ficha" pelo Id.

## 5. Ordem de entrega sugerida (cada parte compilada e testada por você)

1. **1d-1:** domínio do plano e serviço, prévia e execução, tabelas e migration, API, e o parâmetro "Datas no passado"
   (T1 e T6).
   - **Entregue em 28/09/2026, sem compilar.** Os detalhes estão na seção 6.
2. **1d-2:** telas "Transferências" (assistente e resultado) e "Carteira em uma data" (a API desta entra aqui, junto com
   a tela), mais "Abrir ficha".
3. **Fechamento da Fase 1:**
   - `docs/FASE-1-RELATORIO.md`;
   - `CONTINUIDADE.md` com o Motor Comercial;
   - MC-4 (migrar `GrupoEconomico` para `GrupoEmpresarial`, com migration de conversão), que é a última pendência da Fase 1 na auditoria.

## 6. Andamento da 1d-1 (28/09/2026)

- **Domínio:**
  - `Lone.Domain/Comercial/RegrasTransferencia.cs` reúne: pedido, candidatos, plano por cliente, aplicação, cópia "como estava", divisão pela menor carteira, frases do histórico e contagens.
  - `Lone.Domain/Entidades/Transferencias.cs` tem `TransferenciaCarteira` e `TransferenciaCarteiraItem` (item fora da auditoria campo a campo).
  - `CarteiraCliente` ganhou `TransferenciaId`. `RegrasComercial.DefinirOrigens` mantém o `TransferenciaId` do gravado, e o vínculo novo da ficha nasce sem ele.
  - `ParametrosComerciais` ganhou `DiasRetroativosMaximo` (padrão 30, de 0 a 365). `RegrasCobertura.Validar` recusa uma cobertura nova, ou com o início mudado, que comece antes desse limite (T6).
- **Serviço:** `TransferenciaCarteiraAppService`, com prévia, gravação, lista e consulta.
  - Por cliente: lê uma vez, planeja, aplica e confere `Validar`, `ValidarHistorico`, `ValidarCarteira` e as referências ("Quem pode ser").
  - Na gravação: atualiza o vendedor padrão, registra a frase e grava a pessoa. Conflito ou recusa daquele cliente viram item "Erro", sem parar os outros.
  - O registro é gravado antes (os vínculos apontam para ele). O resultado é registrado mesmo se a gravação for interrompida (`Concluida` = falso).
  - O número e o motivo vão para a coluna Motivo da auditoria.
- **Infraestrutura:**
  - `TransferenciaCarteiraRepositorio`: clientes alcançados e cargas contados no banco; número por ano com índice único.
  - Configuração EF: tabelas `TransferenciasCarteira` e `TransferenciaCarteiraItens`, e a FK de `CarteiraClientes.TransferenciaId`.
- **API:** `GET/POST api/v1/comercial/transferencias`, `POST .../previa` e `GET .../{id}`, com a permissão `COMERCIAL.TRANSFERIR`.
- **Tela:** só o campo "Datas no passado" em Parâmetros comerciais. As telas da transferência ficam para a 1d-2.
- **Testes:**
  - `Dominio/TransferenciaTests` (17 casos);
  - `Aplicacao/TransferenciaCarteiraAppServiceTests` (4);
  - `ModeloCarteiraTests` (1 novo).
- **Migration `MotorComercial1d`:** gerar no PMC. O que se espera dela:
  - cria as 2 tabelas;
  - adiciona `CarteiraClientes.TransferenciaId` (nula), com FK e índice;
  - adiciona `ParametrosComerciais.DiasRetroativosMaximo` com default 0, seguido de um `UpdateData` do registro único para 30;
  - nenhum DROP e nenhum SQL manual.
