# Pendências e correções anotadas

> Pedidos do usuário anotados para tratar numa próxima parte, com o diagnóstico feito na hora. Quando um item for
> resolvido, ele sai daqui e vai para o relatório da fase.

## C1. Cliente não deve ser obrigado a ter vendedor (28/09/2026, 18h10)

**O que aconteceu:** ao salvar um cliente, a ficha recusou com "Carteira 1: escolha quem será Vendedor".

**Diagnóstico:**
- A carteira **não é obrigatória** hoje: cliente sem nenhum vínculo grava normalmente.
- A mensagem vem da conferência do cartão da carteira (`CarteiraFormulario.Validar`, no app, e `RegrasComercial.Validar`,
  na API). Ela aparece quando existe um cartão de papel na carteira sem a pessoa escolhida, por exemplo depois de clicar
  em **Adicionar papel** e não preencher.
- Para quem está cadastrando, isso parece "vendedor obrigatório", e o único jeito de seguir é achar o cartão e clicar
  em Remover.

**Correção proposta:**
1. **Cartão novo e vazio não trava a gravação:** um cartão que ainda não foi gravado e está sem pessoa é descartado ao
   salvar, com o aviso "O papel Vendedor ficou sem pessoa e não foi incluído". Um cartão com a pessoa escolhida continua
   sendo conferido como hoje.
2. **Regra configurável** em Comercial › Configurações › Parâmetros comerciais: "Cliente precisa ter responsável da
   conta", com as opções:
   - **Não** (padrão, o comportamento de hoje);
   - **Sim, ao cadastrar**: cliente novo só grava com o responsável da conta;
   - **Sim, sempre**: também não deixa encerrar o último responsável sem pôr outro.

   Com alcance restrito, o F4 da 2a-2 já põe o próprio usuário como responsável, então a regra não atrapalha o vendedor.
3. **Onde vale:** a regra fica no domínio, com a mesma conferência na API, e o app avisa antes de salvar. Clientes antigos
   sem responsável não são bloqueados: a regra vale ao cadastrar e ao mudar a carteira.

**Decisão pendente:** a opção padrão da regra (sugestão: "Não") e se vale por empresa. Parâmetros comerciais hoje é
um registro único.

## C2. Mensagem de erro que leva ao campo (28/09/2026, 18h21)

**O que aconteceu:** ao salvar, a ficha mostrou no topo "Endereço 1: escolha a UF e o município na lista.", mas a
pessoa estava na aba Identificação e precisou achar sozinha a aba, o endereço e o campo.

**Pedido do usuário:** destacar a aba (o cabeçalho) e o campo que precisa ser corrigido, ou ter um link na mensagem que
leve direto ao campo.

**Como os sistemas maduros fazem:**
- **GOV.UK Design System** (referência em formulários): um "resumo de erros" no topo, em que cada linha é um link que
  leva ao campo, e o campo fica marcado em vermelho, com a mensagem logo abaixo dele.
- **SAP Fiori:** o botão de mensagens mostra a contagem; cada mensagem, ao ser clicada, abre a seção e põe o foco no
  campo.
- **Salesforce e Dynamics 365:** "Revise os erros desta página", com a lista de campos clicáveis, e a aba ou seção com o
  erro fica marcada.

**Proposta (junta as duas ideias do usuário):**
1. **Erro com endereço, não só texto:** cada erro da ficha passa a dizer onde está: a aba, o item (ex.: "Endereço 1") e o
   campo (ex.: Município).
   - Vale para a conferência do app e para os erros que vêm da API (`ValidacaoException` com o caminho do campo, ex.:
     `Enderecos[0].MunicipioId`).
   - Os erros antigos, só com texto, continuam funcionando: aparecem sem link.
2. **Resumo no topo com links:** "2 itens para corrigir antes de salvar".
   - Cada linha é clicável e leva ao campo: troca de aba, rola até o item, abre o cartão se estiver recolhido e põe o
     cursor no campo.
   - O primeiro erro já recebe o foco sozinho ao salvar.
3. **Aba marcada:** a aba com erro ganha um marcador vermelho com a quantidade (ex.: "Endereços ●1"), visível de
   qualquer aba.
4. **Campo marcado:** borda vermelha e a mensagem curta logo abaixo do campo ("Escolha o município na lista"). A marca
   some assim que o campo é corrigido, sem precisar salvar de novo.
5. **Acessibilidade:** o resumo é anunciado pelo leitor de tela, e o link diz o campo ("Ir para Município, no
   Endereço 1").
6. **Ordem de entrega:**
   - primeiro a ficha de Pessoas (a maior e a que mais sofre com isso);
   - depois um componente comum para as outras telas de cadastro;
   - entra como padrão em `docs/UX-ARQUITETURA.md` quando for feito.

## C3. Depois de salvar, a mensagem precisa aparecer onde a pessoa está olhando (28/09/2026, 18h26)

**O que aconteceu:** o botão Salvar fica no fim da ficha, e a mensagem (erro ou aviso) aparece no topo. Quem salvou lá
embaixo não vê nada e pode achar que gravou e mudar de tela.

**Pedido do usuário:** ao clicar em Salvar, a tela rolar para o início, onde está a mensagem.

**Proposta (com C2):**
1. **Deu erro:** a tela rola até o topo, onde está o resumo de erros de C2, e o foco vai para ele. Com C2 pronto, o
   resumo leva direto ao primeiro campo.
2. **Gravou com avisos** (ex.: "possível duplicado", "fora do seu alcance"): também rola até o topo e mostra os avisos.
3. **Gravou sem nada a dizer:** confirmação "Salvo às 18:26" junto do próprio botão Salvar (e uma mensagem rápida no
   rodapé), sem tirar a pessoa do lugar em que ela estava.
4. **Barra de ações fixa:** Salvar, Descartar e Fechar ficam numa barra que não rola com a ficha (no rodapé ou no
   cabeçalho), com um indicador de "alterações não salvas". A mensagem aparece colada nessa barra, então é vista de
   qualquer ponto da ficha. É o padrão de SAP Fiori, Dynamics e Salesforce.
5. **Sair sem salvar:** conferir se trocar de tela ou fechar com alterações pendentes sempre pergunta antes (a ficha já
   pergunta em alguns caminhos, como "Abrir cadastro").
6. **Onde vale:** a ficha de Pessoas primeiro, depois todas as telas de cadastro (componente comum). Vira padrão em
   `docs/UX-ARQUITETURA.md`.

## C4. Permissões do perfil: ordem alfabética e busca (28/09/2026, 23h13)

**O que acontece:** em Perfis de acesso, as permissões aparecem na ordem em que foram criadas no código (grupos e itens
fora de ordem alfabética), e com o sistema crescendo o administrador perde tempo procurando.

**Pedido do usuário:** grupos e permissões em ordem alfabética, e uma caixa de busca (pelo grupo, pelo nome, ou algo mais
inteligente, como fazem os ERPs maduros).

**Como os sistemas maduros fazem:**
- **SAP (PFCG/Fiori) e Dynamics 365:** permissões agrupadas por área, com busca que filtra enquanto digita.
- **Salesforce:** "Localizar configurações" busca por palavras em nome e descrição.
- **Todos:** mostram quantas estão marcadas em cada grupo, têm "Marcar todas do grupo" e o filtro "Só as marcadas", e
  deixam comparar perfis.

**Proposta:**
1. **Ordem:** grupos em ordem alfabética, e permissões em ordem alfabética dentro de cada grupo (pela descrição, que é o
   que aparece). Ignora acentos e maiúsculas ("Ações" junto de "Acesso").
2. **Busca que filtra enquanto digita:**
   - procura no nome do grupo, na descrição e no código (ex.: `PESSOAS.EDITAR`);
   - sem acento e sem diferença de maiúsculas;
   - várias palavras em qualquer ordem ("cliente bloqueio" acha "Alterar limite de crédito e bloqueios de cliente");
   - destaca o trecho encontrado;
   - grupo sem resultado some, e o que sobra fica aberto;
   - "Nenhuma permissão encontrada" quando nada bate.
3. **Sinônimos simples, o "mais inteligente":**
   - uma lista curta de palavras equivalentes (ex.: "excluir/apagar/desativar/inativar", "ver/visualizar/consultar",
     "editar/alterar", "vendedor/carteira/comercial", "senha/usuário/acesso");
   - quem busca "excluir" acha "Desativar e reativar cadastros", já que o Lone nunca apaga.
4. **Atalhos:**
   - cada grupo mostra "3 de 15 marcadas", com "Marcar todas" e "Desmarcar todas" (só nas visíveis, quando há busca);
   - filtro "Só as marcadas", para conferir o que o perfil tem.
5. **Onde:** a regra (ordem, busca e sinônimos) fica num componente do cliente, testável sem MAUI, usado na tela de
   Perfis. Depois, a mesma busca pode servir no menu e nas Configurações de cada módulo.
6. **Futuro (anotado, não entra agora):** comparar dois perfis lado a lado e "quem tem esta permissão" (a lista de usuários
   e perfis que a têm), como no SAP e no Salesforce.

## Outras anotações

- Senhas, crachá e verificação em duas etapas: `docs/PLANO-SENHAS-E-CRACHA.md`.
- Pontos em aberto da Fase 2a-2 (contatos e sócios com alcance restrito e outros): seção 7 de
  `docs/PLANO-FASE2-EQUIPES-TERRITORIOS.md`.
- Padrões de UX pedidos (dias nos períodos, calendário, dia útil): `docs/UX-ARQUITETURA.md`.
