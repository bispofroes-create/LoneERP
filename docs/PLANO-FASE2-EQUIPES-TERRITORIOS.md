# Motor Comercial, Fase 2: equipes, territórios, distribuição e escopo de acesso (plano, 28/09/2026)

> Só análise e plano: nada foi implementado. As decisões marcadas com **[F#]** precisam da resposta do usuário.
> Base: auditoria (`_entrega/AUDITORIA-MOTOR-COMERCIAL.md`, seções 3, 5.3 e 6, "Fase 2") e MC-6 (o escopo de acesso sobe
> para esta fase). Segue os princípios de sempre: tudo com vigência, nada apagado, padrões prontos para a empresa pequena
> e complexidade progressiva.

## 1. O que já existe

| Peça | Onde | Situação |
|---|---|---|
| `Equipe` + `MembroEquipe` | `Lone.Domain/Entidades/Metas.cs` | Nome, departamento, **um líder** (`LiderId`, sem histórico) e membros com entrada e saída. Usada nas metas. **Sem hierarquia** |
| `LotacaoColaborador.GestorId` | `Colaborador.cs` | O gestor do colaborador, com vigência (lotação). Não é usado no Comercial |
| Catálogo de filtros de Pessoas | `CatalogoFiltrosPessoas` + `FiltrosPessoasSql.Aplicar` | Cerca de 70 campos (UF, município, CEP, CNAE, porte, grupo, etiqueta, campo personalizado…) viram LINQ parametrizado, com testes. **É a base dos territórios** |
| Perfis e permissões | `Perfil`, `UsuarioPerfil` (por empresa), `RegrasDeAcesso` | Permissões por função. **Não existe escopo por registro:** quem vê Pessoas vê a base inteira |
| `Usuario` | `Usuario.cs` | Login, nome, e-mail e perfis. **Não está ligado a uma Pessoa:** o sistema não sabe que o usuário "rafael" é o vendedor Rafael da carteira |
| Consultas de Pessoas | `PessoaRepositorio.Filtrar` e cerca de 35 consultas a `Pessoas` | A lista, as contagens das abas, os indicadores, a exportação e as visões passam por `Filtrar`/`FiltrosPessoasSql`. É **um ponto central** para aplicar o escopo |

**A lacuna que decide a fase:** sem a ligação usuário ↔ pessoa, não há como aplicar "o vendedor vê só a própria carteira".

## 2. Como os sistemas maduros fazem

- **Salesforce:**
  - o acesso a registros é separado da permissão de função, com três camadas:
    - padrão da organização (privado ou público);
    - hierarquia de papéis: o gerente vê o que os subordinados veem;
    - compartilhamento por regra, equipe ou território.
  - **Enterprise Territory Management:** territórios em hierarquia, regras de atribuição por campos da conta, simulação ("rodar as regras") antes de aplicar, e a conta pode estar em vários territórios.
- **Dynamics 365:**
  - "unidades de negócio" com níveis de acesso por perfil: Usuário, Unidade, Unidade e filhas, Organização;
  - hierarquia de gerentes;
  - territórios com gerente e membros.
- **SAP:** áreas de vendas e estrutura organizacional; acesso por objeto de autorização (valores permitidos).
- **O que é comum:**
  1. o usuário é uma pessoa da estrutura;
  2. o **nível de alcance** faz parte do perfil;
  3. a **hierarquia** abre o que os de baixo veem;
  4. os territórios se definem **por regras** e são **simulados** antes de aplicar.

## 3. Proposta, em três partes (cada uma compilada e testada por você)

### 2a: quem é quem e o que cada um vê (escopo de acesso)

1. **Usuário ligado a uma pessoa** (**[F1]**): o campo "Pessoa no cadastro" na tela de Usuários. É opcional, e uma pessoa tem no máximo um usuário.
2. **Alcance no perfil** (**[F2]**): no cadastro de Perfis, "Alcance em Pessoas e no Comercial", com quatro níveis:
   - **Tudo** (padrão: ninguém perde acesso na atualização);
   - **Minha equipe e as de baixo**;
   - **Minha carteira**;
   - **Nenhum**.

   O administrador vê tudo. Perfil por empresa, como hoje.
3. **Hierarquia das equipes** (**[F3]**):
   - `Equipes.EquipePaiId` (ex.: Comercial › Regional Sul › Televendas Sul);
   - **liderança com vigência**: o papel do membro passa a ser Membro ou Líder, com entrada e saída. `LiderId` vira cópia do líder vigente, como o `VendedorPadraoId`.

   Assim dá para responder "quem liderava a equipe em 15/04", como pede o princípio 99.
4. **O que é "minha carteira" num dia** (regra única no domínio, `RegrasEscopo`):
   - os clientes em que a pessoa do usuário tem vínculo vigente (qualquer papel);
   - os clientes das coberturas em que ela cobre alguém com "pode acessar" (Fase 1c);
   - em "minha equipe": os mesmos critérios para todos os membros vigentes das equipes que ela lidera e das equipes abaixo delas;
   - cliente novo cadastrado por um vendedor de "minha carteira": ver **[F4]**.
5. **Onde o escopo vale** (**[F5]**), aplicado **no servidor**, num ponto só (`EscopoPessoas`, em `Filtrar` e na leitura da ficha):
   - lista de Pessoas, com as contagens das abas, os indicadores, as visões e a exportação;
   - abrir, gravar e prévia da ficha (fora do escopo: "Você não tem acesso a este cadastro", sem dizer se existe);
   - Carteira vencendo, Carteira em uma data, coberturas e transferências (só o que está no alcance);
   - metas (o painel mostra só o que está no alcance).
6. **Banco (migration, precisa de aprovação):**
   - `Usuarios.PessoaId` (nulo, índice único filtrado);
   - `Perfis.AlcanceComercial` (padrão Tudo);
   - `Equipes.EquipePaiId`;
   - `MembrosEquipe.Papel`, com conversão: o `LiderId` atual vira membro Líder desde a criação da equipe.

   Nada é apagado.

### 2b: territórios

1. **Cadastro de territórios** em árvore (Brasil › Sudeste › MG), com tipo livre (geográfico, segmento, porte, estratégico…), responsável (pessoa ou equipe, com vigência) e ativo.
2. **Regra do território = condições do catálogo de filtros** (o mesmo painel da tela de Pessoas), com vigência e versão. Uma regra nova não reescreve a anterior: encerra uma e abre a outra. Vale qualquer campo (UF, CEP, CNAE, porte, etiqueta, campo personalizado…) sem código novo.
3. **"Simular"** mostra quantos clientes a regra alcança, quais entram e quais saem em relação a hoje, nada é gravado. **"Aplicar"** grava o vínculo cliente × território com vigência e origem (Regra ou Manual), no padrão da prévia e resultado da transferência.
4. **Um cliente pode estar em mais de um território** (**[F6]**).
5. **No escopo:** um nível a mais, "Meus territórios", para o diretor ou gerente regional.
6. **Na "Carteira em uma data":** o território vigente de cada cliente.
7. **Banco:**
   - `Territorios`;
   - `RegrasTerritorio` (condições em JSON do catálogo, vigência, versão);
   - `TerritorioClientes` (vigência, origem, regra aplicada);
   - `TerritorioResponsaveis`.

### 2c: distribuição automática e capacidade

1. **Regra de distribuição:** território (ou condições) → destino (equipe ou lista de pessoas) + **estratégia**:
   - menor carteira (a mesma da transferência);
   - rodízio;
   - por especialidade (condição);
   - prioridade.
2. **Quando roda** (**[F7]**):
   - no cadastro de um cliente novo sem responsável;
   - ou por "Distribuir clientes sem responsável" (prévia + resultado).

   Grava o vínculo com `Origem = Distribuição`.
3. **Capacidade** (**[F8]**):
   - limite por pessoa e papel, contado em clientes ou em pontos (peso por porte ou segmento: pequeno 1, médio 3, grande 10);
   - a distribuição e a transferência avisam, ou recusam, quando o limite seria passado.
4. **Banco:**
   - `RegrasDistribuicao`;
   - `CapacidadesComerciais`;
   - `PesosCliente`.

## 4. Riscos

- **Desempenho do escopo:** filtrar por carteira e hierarquia em cada consulta. Mitigação:
  - resolver primeiro o conjunto de vendedores alcançados, que é pequeno;
  - usar `EXISTS` em `CarteiraClientes`, com o índice `(VendedorId, Ativo, FimEm)` que já existe;
  - medir numa base grande.
- **Consultas esquecidas:** alguma das cerca de 35 consultas a `Pessoas` pode ficar sem escopo. Mitigação:
  - o escopo entra em `Filtrar` e na leitura da ficha;
  - um teste de arquitetura lista os pontos de entrada e confere cada um.
- **Mudança de comportamento:** o padrão "Tudo" mantém como está hoje. Só quem receber outro alcance passa a ver menos.

## 5. Decisões

- **[F1] Ligação usuário ↔ pessoa:**
  - **A (recomendado):** campo opcional na tela de Usuários, com uma pessoa por usuário.
  - **B:** por e-mail (o usuário com o mesmo e-mail da pessoa). É frágil.
- **[F2] Onde fica o alcance:**
  - **A (recomendado):** um campo no perfil, com os níveis Tudo, Equipe e abaixo, Minha carteira e Nenhum. É uma escolha só e clara, como no Dynamics.
  - **B:** permissões separadas (`COMERCIAL.VER_PROPRIA_CARTEIRA`, `...EQUIPE`, `...TUDO`), como na auditoria. Podem conflitar entre si.
- **[F3] Liderança da equipe:**
  - **A (recomendado):** membro com papel Líder e vigência (histórico), com o `LiderId` como cópia.
  - **B:** manter só o `LiderId` (sem histórico de quem liderou).
- **[F4] Cliente novo cadastrado por quem tem alcance "Minha carteira":**
  - **A (recomendado):** ele entra automaticamente como responsável da conta desde o dia do cadastro (vendedor padrão), senão o cliente some da lista dele depois de salvar.
  - **B:** fica sem carteira, e só quem tem alcance maior vê.
- **[F5] Pessoas que não são clientes** (fornecedores, funcionários) para quem tem "Minha carteira" ou "Minha equipe":
  - **A (recomendado):** não aparecem (o alcance vale para o cadastro de Pessoas inteiro).
  - **B:** o alcance restringe só os clientes; o resto continua visível.
- **[F6] Cliente em vários territórios:**
  - **A (recomendado):** pode, como no Salesforce, com a prioridade da regra desempatando a distribuição.
  - **B:** um território por cliente.
- **[F7] Distribuição:**
  - **A (recomendado):** manual, com prévia ("Distribuir clientes sem responsável"), mais a opção de a regra valer no cadastro de cliente novo, desligada por padrão.
  - **B:** sempre automática no cadastro.
- **[F8] Capacidade:**
  - **A (recomendado):** avisa e deixa seguir com motivo; o padrão é sem limite.
  - **B:** recusa quando passa do limite.
- **[F9] Ordem:** 2a (escopo), depois 2b (territórios), depois 2c (distribuição e capacidade)? *(recomendado; cada parte com a sua migration)*

## 6. Decisões do usuário (28/09/2026, 15h32)

- O usuário aprovou todas as recomendações de F1 a F9 (opção A em cada uma e a ordem 2a → 2b → 2c).
- A 2a foi dividida em duas entregas:
  - **2a-1:** banco e cadastros (pessoa do usuário, alcance do perfil, hierarquia e liderança das equipes);
  - **2a-2:** o escopo aplicado nas consultas.

## 7. Andamento

### 2a-1 — banco e cadastros (28/09/2026, código aplicado; aguardando build, test e migration)

- Usuário ligado a uma pessoa (F1): `Usuarios.PessoaId` opcional, índice único filtrado (uma pessoa por usuário), FK sem cascata. Tela de Usuários: busca da pessoa, "Ligar à pessoa escolhida" (escolher na lista não liga sozinho) e "Tirar". A conferência (pessoa ativa e livre) só roda quando a pessoa muda.
- Alcance no perfil (F2): `Perfis.AlcanceComercial` (0 = Tudo, padrão dos perfis existentes). Administrador fica sempre em Tudo. Ainda **não** filtra nada: o filtro é a 2a-2.
- Equipes (F3): `Equipes.EquipePaiId` (equipe acima, sem ciclo) e `MembrosEquipe.Papel` (Membro/Líder, um líder por vez, com vigência). `Equipes.LiderId` vira cópia do líder de hoje, calculada pelo servidor. Na tela: "Equipe acima", "Líder hoje" (só leitura) e o papel de cada membro (só muda antes da entrada).
- Regras no domínio: `Lone.Domain/Metas/RegrasEquipe.cs`.
- Migração: gerar `Fase2aEscopoEquipes`; no Up, logo depois do AddColumn de `MembrosEquipe.Papel`, inserir `migrationBuilder.Sql(SqlMigracaoEquipes.LiderComoMembro);` (o líder gravado vira membro com papel Líder; não apaga nada).
- Testes novos: `Dominio/EquipesTests`, `Aplicacao/UsuarioPessoaTests`, `Infraestrutura/ModeloEquipesTests`, `Cliente/EquipesEUsuarioPessoaTests`.

### Próximo: 2a-2 — escopo nas consultas (Pessoas, ficha, Comercial, metas; F4 e F5).
