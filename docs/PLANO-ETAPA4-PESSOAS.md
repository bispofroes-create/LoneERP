# Etapa 4 da tela de Pessoas — seleção múltipla e ações em lote (plano, 27/09/2026)

> Só análise e plano: nada foi implementado. As decisões marcadas com **[D#]** precisam da resposta do usuário.

## 1. O que já existe e pode ser reaproveitado

| Peça | Onde | Serve para |
|---|---|---|
| Desativar e reativar com permissão, motivo e evento na auditoria | `PessoaAppService.AlterarSituacaoAsync` (`PESSOAS.INATIVAR`, `IMotivoDaOperacao`) | Inativar em lote, pessoa a pessoa, com a mesma regra |
| Gravação com auditoria por pessoa | `PessoaRepositorio.SalvarAsync` + `ColetorAuditoria` | Cada pessoa alterada no lote fica no histórico dela |
| Etiquetas da pessoa | `Pessoa.Etiquetas` (`PessoaEtiqueta`) | Etiquetar e tirar etiqueta em lote |
| Carteira de clientes | `CarteiraCliente` (tipo, empresa, vendedor, início/fim, exclusivo) + `RegrasComercial` (tipo principal: um só por vez; exclusivo: sem sobreposição) | Atribuir vendedor em lote. As regras já barram o que conflita |
| Exportar CSV com permissão própria e registro na auditoria | `ConsultaPessoasAppService.ExportarAsync` (`PESSOAS.EXPORTAR`) | Exportar só as selecionadas |
| Critérios da tela (busca, aba, painel) | `CriteriosPessoas` + motor de filtros | Escolher "todas as N que atendem ao filtro" sem mandar os Ids |

Nenhuma dessas ações precisa de tabela nova. Todas mudam dados que já têm lugar e auditoria.

## 2. Como os ERPs maduros fazem

- **Salesforce e HubSpot:**
  - Caixa de marcar por linha e "marcar a página" no cabeçalho.
  - Ao marcar a página, uma faixa oferece "Selecionar todos os 1.248".
  - A barra de ações em lote aparece no lugar da barra de busca enquanto há seleção.
  - O resultado é parcial: "1.240 atualizados, 8 com erro (ver)". Cada registro vale por si; um erro não desfaz os outros.
- **Dynamics 365:** o mesmo padrão. Ações longas vão para um processo em segundo plano com aviso ao terminar.
- **Protheus, Bling e Omie:** a alteração em lote existe em rotinas separadas ("alteração em massa"), com filtro e
  prévia do que vai mudar. Não fica na lista.

## 3. Proposta

**Seleção (só no computador; no celular fica para depois):**
- Caixa de marcar à esquerda do avatar. Aparece ao passar o mouse e fica fixa quando há algo marcado.
- No cabeçalho, "marcar a página". Com a página toda marcada, a faixa oferece "Selecionar todas as 1.248 que
  atendem ao filtro" (**[D1]**).
- A seleção fica ao trocar de página. Mudar a busca, a aba ou os filtros limpa a seleção, avisando.
- Enquanto há seleção, a barra de ações substitui a barra de busca: "12 selecionadas · Etiquetar · Atribuir vendedor ·
  Exportar · Inativar · ✕".

**Ações (cada uma com a permissão da ação única equivalente, conferida de novo no servidor):**

| Ação | Permissão | Como |
|---|---|---|
| Etiquetar / tirar etiqueta | `PESSOAS.EDITAR` | Escolher uma ou mais etiquetas ativas e se põe ou tira. Quem já tem fica igual |
| Atribuir vendedor | `PESSOAS.EDITAR` | Tipo de carteira, vendedor, empresa (ou todas), início. Conflito com a carteira atual: ver **[D3]** |
| Exportar selecionadas | `PESSOAS.EXPORTAR` | O mesmo CSV de hoje, só com as marcadas. Registro na auditoria como hoje |
| Inativar (e reativar) | `PESSOAS.INATIVAR` | Motivo (**[D4]**). Quem já está inativo é pulado e aparece no resultado |

**Execução:**
- **[D2]** Cada pessoa é gravada separadamente, com o próprio evento no histórico.
- Um motivo comum ("Ação em lote: etiqueta VIP") vai para a coluna Motivo da auditoria.
- Antes de executar, a tela mostra uma confirmação com o que vai acontecer ("Pôr a etiqueta VIP em 12 pessoas").
- Depois, mostra o resultado: "10 alteradas, 1 já tinha, 1 com erro". Tocar nos erros abre a lista com o motivo de
  cada um (por exemplo, conflito de edição ou carteira sobreposta).
- Limite por ação: 500 pessoas por vez (**[D1]**). Acima disso, a tela pede para refinar o filtro. Um processo em
  segundo plano fica para outra etapa, se for preciso.
- **API:** um endpoint por ação (`POST pessoas/lote/etiquetas`, `.../carteira`, `.../inativar`, `.../reativar`),
  que recebe os Ids ou os critérios da tela e devolve o resultado por pessoa. A exportação reaproveita a rota atual,
  com os Ids nos critérios.

**Banco:** nada muda. Não há migration nem índice novo.

## 4. Decisões

- **[D1] Alcance da seleção:**
  - **A (recomendado):** a página, mais a opção "todas as N que atendem ao filtro", com limite de 500 por ação.
  - **B:** só o que foi marcado à mão, até 500.
  - **C:** sem limite, em segundo plano. É a mais cara, porque precisa de fila e de aviso ao terminar.
- **[D2] Se uma pessoa falhar:**
  - **A (recomendado):** cada uma vale por si, e o resultado lista as que falharam.
  - **B:** tudo ou nada.
- **[D3] Atribuir vendedor quando a pessoa já tem vendedor ativo do mesmo tipo principal ou exclusivo** (é regra de
  negócio):
  - **A:** encerrar o anterior no dia anterior ao início do novo, com evento no histórico.
  - **B:** pular essa pessoa e listá-la no resultado.
  - **C:** perguntar na confirmação, com as duas opções.
- **[D4] Motivo para inativar em lote:**
  - **A (recomendado):** obrigatório no lote, embora seja opcional na ação única.
  - **B:** opcional, como na ação única.
- **[D5] Ordem de entrega:**
  - **A (recomendado):** 4a com seleção, etiquetar, exportar e inativar/reativar; 4b com atribuir vendedor, que tem
    mais regra.
  - **B:** tudo junto.

---

## 5. Decisões do usuário (28/09/2026, manhã)

- **D1:** a página, mais "todas as N que atendem ao filtro", com limite de 500 por ação.
- **D2:** cada pessoa vale por si, e o resultado lista as que falharam.
- **D3:** substituir o vendedor, com confirmação explícita, sem apagar nada e com proteção contra concorrência. O usuário
  mandou um prompt detalhado, cujo resumo está na seção 6. O texto chegou cortado no item 13 ("Se a arquitetu…").
- **D4:** motivo obrigatório para inativar em lote.
- **D5:** a regra do vendedor vem primeiro, na ficha. Depois vem a 4a (seleção, etiquetar, exportar e inativar) e por
  último a 4b (vendedor em lote).
- **Banco:** gatilho `TR_CarteiraClientes_SemSobreposicao` aprovado.
- **Retroativo:** um novo vínculo que começa no mesmo dia ou antes do vigente não substitui ninguém. A pessoa fica
  listada, com o motivo.

## 6. Regra de substituição de vendedor — auditoria do código (28/09/2026)

### 6.1 Como funciona hoje

- **Dados:**
  - `CarteiraCliente` (tabela `CarteiraClientes`) é filha do agregado `Pessoa`, com os campos: tipo (`TipoCarteiraId`),
    vendedor, empresa (nula = todas), `InicioEm`, `FimEm` (nulo = em aberto), `Exclusivo`, `Observacao` e `Ativo`
    (falso = lançado por engano).
  - "Principal" é do tipo, não do vínculo: `TipoCarteira.Principal`. No início, só o tipo "Vendedor" é principal.
  - O vendedor principal vigente vira o `VendedorPadraoId` da conta do cliente, por `RegrasComercial.AtualizarVendedorPadrao`.
- **Regra (domínio):** `RegrasComercial.Validar(Pessoa, tipos)`. Não pode haver dois vínculos ativos do mesmo tipo e da
  mesma empresa com períodos sobrepostos quando o tipo é principal ou o vínculo é exclusivo. Tipos diferentes ou
  empresas diferentes não conflitam. A mensagem hoje é "só um \"Vendedor\" (principal) por vez…".
- **Onde roda:** no servidor, em `PessoaAppService`, na gravação da ficha: `RegrasComercial.Validar` +
  `_comercial.ValidarAsync` (vendedor com papel Vendedor/Representante), antes de `AtualizarVendedorPadrao`.
- **Nunca apaga:** `PessoaRepositorio` sincroniza a carteira com `apagarAusentes: false`. Encerrar é preencher `FimEm`.
- **Concorrência:**
  - `Pessoa` tem `rowversion` (`AgregadoRaiz.Versao`), e o repositório sempre marca a pessoa como alterada
    ("para a versão ser conferida e trocada"), mesmo que só a carteira tenha mudado.
  - Se dois usuários gravam a mesma pessoa, o segundo recebe `ConflitoDeEdicaoException` (409) e nada dele é gravado.
  - Como a regra roda de novo no servidor sobre a pessoa inteira, uma chamada direta à API também não passa.
- **Histórico:** a auditoria (`ColetorAuditoria`) grava cada campo alterado de cada vínculo, com usuário, data/hora e o
  motivo da operação (`IMotivoDaOperacao`). Os eventos de negócio (`RegistrarEvento`) aparecem no histórico da pessoa.
  Com `InicioEm`/`FimEm` dá para responder "quem era o vendedor em tal data".
- **Na ficha, hoje:** para trocar o vendedor, o usuário precisa preencher à mão o fim do vínculo antigo antes de
  incluir o novo. Se não fizer, a gravação é recusada com a mensagem de sobreposição. Não há substituição assistida.
- **Banco:** não há restrição para a carteira, só os índices `(VendedorId, Ativo, FimEm)` e `(PessoaId, InicioEm)`. A
  proteção é o agregado com `rowversion` mais a regra no servidor. O projeto já usa gatilhos para regras que um índice
  não expressa: `TR_PessoaEnderecoFinalidades_…`, em `SqlMigracaoFinalidadesEndereco`.

### 6.2 Proposta

1. **Domínio (`RegrasComercial`):**
   - `ConflitosDeCarteira(pessoa, novo vínculo, tipos)` devolve os vínculos ativos que conflitam, pelo mesmo critério da
     regra atual: mesmo tipo, mesma empresa, principal ou exclusivo, período sobreposto. Não há regra paralela: a
     validação atual passa a usar essa função.
   - `Substituir(pessoa, novo vínculo, nomes)` encerra cada conflitante com `FimEm = início do novo − 1 dia` e registra
     o evento "Carteira: João da Silva (Vendedor) encerrado em 14/03/2026 e substituído por Maria Oliveira a partir de
     15/03/2026". O fim é o dia anterior porque `Vigente` inclui o dia do fim; assim não há sobreposição nem buraco.
   - **Não mexe no passado:** se o novo começa no mesmo dia ou antes do início do vigente, não substitui, porque
     encerrar o anterior antes do início dele apagaria o período em que ele foi responsável. Essa pessoa vai para a
     lista "não processadas", com o motivo. Um vínculo futuro que conflita também não é mexido.
2. **Ficha (uma pessoa):**
   - Ao incluir ou editar um vínculo que conflita, a tela mostra a confirmação da D3: vendedor atual, novo vendedor e o
     que vai acontecer.
   - Os botões são "Cancelar" e "Encerrar anterior e atribuir novo vendedor". A substituição fica na ficha e vai para o
     servidor na mesma gravação da pessoa.
   - A mesma transação e a mesma `rowversion` valem: se outra pessoa gravou antes, a gravação inteira é recusada
     (nada fica meio feito) e a tela pede para reabrir.
3. **Servidor:** na gravação, se um vínculo foi encerrado junto com a inclusão de outro do mesmo tipo e empresa
   começando no dia seguinte, registra o evento de substituição. Vale para a ficha e para o lote.
4. **Lote (4b):**
   - `POST pessoas/lote/carteira` em dois passos.
   - **Prévia (sem gravar):** quantas recebem o vendedor sem conflito, quantas vão substituir alguém (com os nomes) e
     quantas não podem ser processadas (com o motivo).
   - **Execução:** depois da confirmação, pessoa a pessoa. Cada uma é relida, conferida de novo e gravada na própria
     transação, com a sua `rowversion`.
   - O resultado separa atribuídas, substituídas, não processadas e com erro.
5. **Proteção no banco (precisa da sua aprovação: é migration):**
   - Gatilho `TR_CarteiraClientes_SemSobreposicao`, que recusa dois vínculos ativos do mesmo tipo e empresa com
     períodos sobrepostos quando o tipo é principal ou o vínculo é exclusivo. É a mesma regra do domínio, agora
     também no banco, no padrão dos gatilhos de endereço.
   - Protege até contra SQL feito à mão ou uma rotina futura que grave a carteira sem passar pelo agregado.
   - Sem ele, a proteção continua sendo a `rowversion` mais a regra no servidor. Isso já cobre a API e a concorrência
     entre usuários.

### 6.2.1 Andamento

A regra na ficha (itens 1–3) e a proteção no banco (item 5) foram entregues em 28/09/2026. Detalhes em `CONTINUIDADE.md`.
Falta gerar a migration. O lote (item 4) fica para a 4b.

### 6.3 Arquivos que mudam

- **Domínio:** `Lone.Domain/Comercial/RegrasComercial.cs`.
- **Aplicação:** `Lone.Application/Pessoas/PessoaAppService.cs` (evento de substituição) e um serviço novo de lote
  (`LotePessoasAppService`, na 4a/4b).
- **Contratos e API:** DTOs do lote em `Lone.Contracts/Pessoas`, as rotas em `Rotas` e os endpoints em `PessoasEndpoints`.
- **Cliente:** `ComercialFormulario.cs` (`CarteiraFormulario`), `PessoaFormulario.cs` (confirmação ao gravar ou incluir)
  e `PessoasViewModel`.
- **Infraestrutura (só com aprovação):** a migration do gatilho, no padrão de `SqlMigracaoFinalidadesEndereco`.
- **Testes:**
  - regra: conflito, tipos diferentes, empresas diferentes, retroativo e futuro;
  - evento de substituição;
  - confirmação na ficha;
  - prévia e resultado do lote;
  - concorrência (409).

