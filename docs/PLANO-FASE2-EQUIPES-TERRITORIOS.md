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

### 2a-1 — banco e cadastros (commit `784b3b3`, 28/09/2026; migration aplicada e telas testadas)

- Usuário ligado a uma pessoa (F1): `Usuarios.PessoaId` opcional, índice único filtrado (uma pessoa por usuário), FK sem cascata. Tela de Usuários: busca da pessoa, "Ligar à pessoa escolhida" (escolher na lista não liga sozinho) e "Tirar". A conferência (pessoa ativa e livre) só roda quando a pessoa muda.
- Alcance no perfil (F2): `Perfis.AlcanceComercial` (0 = Tudo, padrão dos perfis existentes). Administrador fica sempre em Tudo. Ainda **não** filtra nada: o filtro é a 2a-2.
- Equipes (F3): `Equipes.EquipePaiId` (equipe acima, sem ciclo) e `MembrosEquipe.Papel` (Membro/Líder, um líder por vez, com vigência). `Equipes.LiderId` vira cópia do líder de hoje, calculada pelo servidor. Na tela: "Equipe acima", "Líder hoje" (só leitura) e o papel de cada membro (só muda antes da entrada).
- Regras no domínio: `Lone.Domain/Metas/RegrasEquipe.cs`.
- Migração: gerar `Fase2aEscopoEquipes`; no Up, logo depois do AddColumn de `MembrosEquipe.Papel`, inserir `migrationBuilder.Sql(SqlMigracaoEquipes.LiderComoMembro);` (o líder gravado vira membro com papel Líder; não apaga nada).
- Testes novos: `Dominio/EquipesTests`, `Aplicacao/UsuarioPessoaTests`, `Infraestrutura/ModeloEquipesTests`, `Cliente/EquipesEUsuarioPessoaTests`.

### 2a-2 — escopo nas consultas (plano aprovado em 28/09/2026, 16h57: E1 a E8 na opção A)

**Tamanho:** a 2a-2 cobre Pessoas (lista, ficha e tudo o que pende da ficha), F4 e F5. As telas do Comercial (carteira
vencendo, carteira em uma data, coberturas, transferências) e as metas ficam na **2a-3**, para a entrega caber num
build e num teste seus (**[E8]**). Não há migration prevista.

**1. A regra, no domínio** (`Lone.Domain/Comercial/RegrasEscopo.cs`, testável sem banco):
- entrada: o alcance, a pessoa do usuário, as equipes (com pai e membros) e as coberturas vigentes, e o dia;
- saída (`EscopoResolvido`): **Tudo**, **Nenhum** ou uma lista de **fontes**. Cada fonte é um vendedor, com papel e empresa
  opcionais (a cobertura pode valer só para um papel ou uma empresa);
- **Minha carteira** = a própria pessoa + os titulares que ela cobre hoje com "pode acessar" (também quando a cobertura é
  da equipe dela);
- **Minha equipe** = o de cima + os membros vigentes das equipes que ela lidera hoje e das equipes abaixo delas (sem
  ciclo, pela `RegrasEquipe`) + as coberturas desses membros;
- o vínculo conta se estiver **ativo e vigente hoje ou começar depois** (**[E7]**: quem recebe uma transferência para a
  semana que vem já enxerga o cliente; quem entrega deixa de ver no dia seguinte ao fim);
- a empresa do vínculo: conta o vínculo **da empresa ativa ou sem empresa** (**[E6]**).

**2. O alcance do usuário** (Aplicação):
- `AcessoEfetivo` ganha o `Alcance`: com vários perfis na empresa, **vale o maior** (**[E1]**), como as permissões, que
  somam. Administrador = Tudo;
- `IEscopoPessoas` (um por requisição): resolve o escopo uma vez e guarda; `ContemAsync(pessoaId)` e `ExigirAsync(pessoaId)`;
- usuário **sem pessoa ligada** e alcance restrito: não vê nenhum cadastro, e a lista mostra "Seu usuário não está ligado
  a uma pessoa do cadastro. Peça ao administrador." (**[E2]**).

**3. No banco** (Infraestrutura, só LINQ parametrizado):
- `EscopoPessoasSql.Aplicar(consulta, escopo, db, hoje)`: `EXISTS` em `CarteiraClientes` pelas fontes, usando o índice
  `(VendedorId, Ativo, FimEm)`;
- `PessoasNoEscopo(db)` substitui `db.Pessoas` em `PessoaRepositorio.Filtrar` (lista, contagens das abas, indicadores,
  prévia), em `ConsultaPessoas` (consulta avançada, exportação, filtros salvos e suas contagens), nas faixas etárias,
  nos clientes ativos e nos endereços duplicados;
- **F5:** com alcance restrito, só aparecem clientes da carteira; quem não é cliente não aparece.

**4. A ficha e tudo o que pende dela** (API):
- um **filtro de endpoint** no grupo de Pessoas confere o escopo de toda rota com o id da pessoa: abrir, gravar,
  desativar, reativar, consolidar endereços, histórico, privacidade, consentimentos, bloqueios, interações,
  relacionamentos e anexos (o anexo resolve a pessoa dele);
- fora do escopo, a resposta é **a mesma de um cadastro que não existe** ("Você não tem acesso a este cadastro ou ele não
  existe mais."), para não revelar nada;
- **pessoas relacionadas a um cliente do alcance** (sócios, contatos, filiais): aparecem pelo nome na ficha do cliente,
  mas abrir a ficha delas segue o alcance (**[E3]**).

**5. Cadastro novo com alcance restrito** (F4):
- cliente novo sem responsável da conta: o usuário entra como responsável da conta desde o dia do cadastro (100% de
  crédito, padrão MC-9), com a origem nova **"Cadastro"** (valor 5 no enum, sem migration);
- cliente novo que já vem com outro responsável: o usuário **também** entra? Não: vale o que ele escolheu, e o aviso
  "Este cliente não ficará no seu alcance" aparece antes de salvar;
- cadastro novo que **não é cliente** (fornecedor, funcionário): **recusado**, com "Seu alcance permite cadastrar só
  clientes." (**[E4]**), porque senão ele some da lista na hora de salvar.

**6. Duplicados:** "documento em uso" continua olhando a base inteira (sem isso, nasce cadastro em dobro), mas, fora do
alcance, só diz "Este documento já está cadastrado. Peça acesso ao responsável.", sem nome nem código (**[E5]**). As
"possíveis duplicadas" mostram só as do alcance.

**7. No app:** alcance **Nenhum** esconde Pessoas do menu. Alcance restrito mostra uma faixa discreta na lista:
"Mostrando a sua carteira" ou "Mostrando a carteira da sua equipe".

**8. Testes:**
- domínio: `EscopoTests` (níveis, hierarquia de 3 níveis, líder que saiu, cobertura com e sem acesso, por papel e por
  empresa, vínculo futuro e encerrado, vários perfis);
- aplicação: F4, F5, cadastro de não-cliente recusado, documento em uso fora do alcance;
- **arquitetura:** (a) na Infraestrutura, `db.Pessoas` só aparece nos arquivos da lista de exceções, cada uma com o
  porquê; (b) toda rota do grupo de Pessoas com id de pessoa tem o filtro de escopo.

**Decisões novas (recomendado: A em todas):**
- **[E1]** Vários perfis com alcances diferentes: **A** vale o maior; B vale o menor.
- **[E2]** Usuário sem pessoa ligada e alcance restrito: **A** não vê nada, com o aviso; B vê tudo até ser ligado.
- **[E3]** Pessoas relacionadas a um cliente: **A** o nome aparece na ficha do cliente, e a ficha delas segue o alcance;
  B abrem também.
- **[E4]** Cadastro de quem não é cliente, com alcance restrito: **A** recusado; B permitido (e some da lista).
- **[E5]** Documento já cadastrado fora do alcance: **A** avisa sem mostrar nome nem código; B mostra o nome.
- **[E6]** Empresa do vínculo: **A** conta o da empresa ativa ou sem empresa; B conta qualquer empresa.
- **[E7]** Vínculo que começa no futuro: **A** já dá acesso; B só a partir do início.
- **[E8]** Dividir em 2a-2 (Pessoas) e 2a-3 (telas do Comercial e metas): **A** sim; B tudo junto.

**Andamento da 2a-2 (28/09/2026): build, testes e telas aprovados pelo usuário (18h31); commitada. Sem migration.**

- **Domínio** (`Lone.Domain/Comercial/RegrasEscopo.cs`): `Resolver` (níveis, hierarquia, coberturas), `VinculoNoEscopo` (a
  expressão única, que vai para o banco e roda em memória nos testes), `Maior` (E1) e `ResponsavelDoCadastro` (F4, só num
  papel de responsável que o usuário pode ocupar pelo "Quem pode ser"). Origem nova do vínculo: `Cadastro` (5).
- **Aplicação:** `AcessoEfetivo.Alcance` (o maior dos perfis da empresa; administrador = Tudo), `AcessoDoUsuario.PessoaId`,
  `IAlcanceDoUsuario`, `IEscopoPessoas`/`EscopoPessoas` (resolve uma vez por requisição; Tudo não faz consulta nenhuma),
  `ForaDoEscopoException`. `PessoaAppService`: E4 (só clientes; sem pessoa ligada ou com alcance Nenhum, recusa), F4, E5
  (documento em uso fora do alcance, sem nome nem código, na checagem da ficha e na gravação) e o aviso "Gravado. Este
  cadastro ficou fora do seu alcance..." depois de salvar.
- **Infraestrutura:** `EscopoPessoasSql` (EXISTS em CarteiraClientes) na lista, abas, indicadores, prévia, consulta
  avançada, exportação, filtros salvos, faixas etárias, clientes ativos, possíveis duplicadas, endereços duplicados e nas
  opções de porte e origem. Os outros `db.Pessoas` levam o comentário "Sem escopo: motivo" (os do Comercial e das metas
  ficam marcados para a 2a-3).
- **API:** `FiltroEscopoPessoa` no grupo de Pessoas inteiro e `FiltroEscopoAnexo` nos anexos. Com alcance restrito, o id
  inexistente e o fora do alcance recebem a mesma resposta (404, "Este cadastro não existe ou está fora do seu alcance."),
  menos no PUT da ficha, que cria o cadastro novo.
- **Aplicativo:** faixa acima da lista ("Mostrando a sua carteira..."), Pessoas fora do menu com alcance Nenhum (a sessão
  traz `Alcance` e `PessoaId`), documento em uso fora do alcance sem "Abrir cadastro".
- **Testes:** `Dominio/EscopoTests`, `Aplicacao/EscopoPessoasTests`, `Cliente/AlcanceClienteTests` e
  `Arquitetura/EscopoArquiteturaTests` (lê o código-fonte: `db.Pessoas` sem "Sem escopo:" na Infraestrutura, filtro nos
  grupos de rotas, nome do id da pessoa nas rotas).

**Pontos em aberto da 2a-2 (para o usuário decidir ou para a próxima parte):**
- **Contatos e sócios com alcance restrito:** E4 recusa cadastrar quem não é cliente, e a busca de pessoa para um
  relacionamento só acha o que está no alcance. Então o vendedor com "Minha carteira" não consegue ligar um contato ou
  sócio (pessoa física) ao cliente dele. Caminho possível: aceitar pessoa sem carteira quando ela nasce já relacionada a um
  cliente do alcance (e ela entra no alcance por esse relacionamento). Decidir antes da 2a-3.
- **F4 sem papel possível:** se nenhum papel de responsável aceita a classificação da pessoa do usuário, o cliente é gravado
  sem ele e o aviso "fora do seu alcance" aparece. Configurar o "Quem pode ser" do papel responsável resolve.
- **Faixa da lista:** é lida ao abrir a tela; depois de trocar de empresa com alcance diferente, só atualiza ao reabrir
  Pessoas (o mesmo vale hoje para os botões que dependem de permissão).
- **PUT da ficha:** com alcance restrito, um PUT num id conhecido ainda distingue "não existe" (segue para a gravação) de
  "fora do alcance" (404). Os ids têm 8 bytes aleatórios, então só vale para ids já vistos. Aceito.
- **Configurações** (grupos empresariais, profissões) e o histórico mostram nomes de cadastros fora do alcance; são telas
  com permissão própria. As telas do Comercial e as metas recebem o escopo na 2a-3.

### 2a-3 — escopo nas telas do Comercial e nas metas (plano aprovado em 28/09/2026, 18h37, com ajustes)

**Duas listas, a partir do escopo que a 2a-2 já resolve (`EscopoResolvido`), sem migration:**
- **clientes no alcance:** a mesma regra da lista de Pessoas;
- **pessoas no alcance:** o próprio usuário e, em "Minha equipe", os membros das equipes que ele lidera e das de baixo.
  Quem ele só cobre numa ausência dá acesso aos clientes, mas não entra aqui, porque não é gente que ele gerencia.

Com alcance Tudo, nada muda.

| Tela | O que muda com alcance restrito |
|---|---|
| **Carteira vencendo** | Só os vínculos de clientes no alcance. |
| **Carteira em uma data** | Por cliente: só cliente no alcance. Por pessoa: só pessoa no alcance. A data pode ser passada, e vale o alcance de hoje (**[E10]**). |
| **Ausências e coberturas** | A lista mostra as coberturas em que o titular ou quem cobre está no alcance. Cadastrar, encerrar e cancelar (com a permissão de hoje) só para titular no alcance: o líder lança as férias da equipe dele (**[E11]**). |
| **Transferências** | A origem e cada destino precisam estar no alcance. A lista mostra as transferências que envolvem alguém do alcance (**[E12]**). |
| **Metas** | Aparecem as metas com algum participante no alcance, e dentro delas só esses participantes, com alvos, realizado e apuração. Participante Empresa, Filial ou Departamento só aparece com alcance Tudo. Criar e editar metas continua exigindo alcance Tudo (**[E13]**). |
| **Opções de pessoas** (quem pode ser, atendentes) | Continuam com todos que podem ocupar o papel: são colaboradores, não clientes. O vendedor pode pôr um representante de fora da equipe no cliente dele (**[E14]**). |

Todo acesso por id (abrir cobertura, transferência ou meta) confere o alcance no serviço e, fora dele, responde como
inexistente, igual à 2a-2. Os comentários "Sem escopo (fica para a 2a-3...)" da Infraestrutura saem ou viram o escopo.

**Contatos e sócios com alcance restrito** (o ponto aberto da 2a-2) (**[E9]**): hoje o vendedor com "Minha carteira" não
consegue ligar um contato ou sócio (pessoa física) ao cliente dele. Proposta:
- aceitar cadastrar quem não é cliente quando a pessoa nasce já relacionada a um cliente do alcance;
- ela passa a estar no alcance por esse relacionamento, enquanto ele estiver vigente;
- a busca de pessoa para relacionar também acha quem já está relacionado a clientes do alcance.

**Testes:** as regras no domínio (pessoas no alcance, metas filtradas, relacionamento que dá alcance) e testes de
aplicação de cada tela. O teste de arquitetura passa a cobrir também as consultas do Comercial e das metas.

**Decisões novas (recomendado: A em todas):**
- **[E9] Contatos e sócios:** A: pessoa relacionada a um cliente do alcance entra no alcance, e pode ser cadastrada assim;
  B: continua como está (só o alcance Tudo cadastra contatos).
- **[E10] Consulta do passado:** A: vale o alcance de hoje (o líder novo vê o histórico da equipe); B: vale quem estava no
  alcance naquela data.
- **[E11] Coberturas:** A: o líder cadastra as ausências da equipe; B: só alcance Tudo cadastra.
- **[E12] Transferências:** A: dentro do alcance (o gerente transfere entre vendedores da equipe dele); B: só alcance Tudo
  transfere.
- **[E13] Metas:** A: vê as metas com participantes no alcance, só as linhas deles, e só o alcance Tudo cria e edita;
  B: vê a meta inteira se tiver algum participante no alcance.
- **[E14] Opções de pessoas:** A: todos que podem ocupar o papel; B: só as pessoas no alcance.

**Decisões do usuário (28/09/2026, 18h37):**
- **E9 aprovada com ajuste:** o contato ou sócio entra no alcance **pela relação com aquele cliente**, e isso não é
  global. João vê Maria porque ela é sócia do Cliente A, que está no alcance dele. Isso não mostra a João os outros
  relacionamentos de Maria (com clientes fora do alcance), e o alcance não se propaga em cadeia.
- **E10 aprovada:** as consultas do passado usam o alcance de hoje.
- **E11 aprovada com ajuste:** o líder cadastra e administra as ausências da equipe, respeitando a **liderança
  temporal**. Vale quem era líder da equipe (e membro dela) no período da ausência, não só quem lidera hoje.
- **E12 aprovada:** o gerente transfere entre vendedores do alcance dele. A transferência continua sendo a operação
  própria, que preserva o histórico. Destino fora do alcance é bloqueado.
- **E13 alterada:** a **permissão** decide se o usuário cria e edita metas; o **alcance** decide quais metas e
  participantes ele vê e opera. Não exigir alcance Tudo.
- **E14 aprovada:** a escolha de quem atende considera todos os colaboradores elegíveis ao papel, independentemente do
  alcance de clientes.
- **Regras gerais:**
  - registro fora do alcance se comporta como inexistente em todo acesso (id, rota, API, ficha, consulta e exportação);
  - sem migration se não houver mudança no banco;
  - relatório ao final;
  - sem push;
  - a 2b só com plano novo e aprovação.

**Decisões do usuário (28/09/2026, 22h09):**
- **E9 — aprovada com ajuste:** o contato ou sócio entra no alcance **pela relação com aquele cliente**, e só um nível: isso
  não abre os outros relacionamentos da pessoa (na ficha dela, só aparecem os relacionamentos com clientes do alcance).
- **E10 — aprovada:** consultas do passado usam o alcance de hoje.
- **E11 — aprovada:** o líder cadastra e administra as ausências e coberturas da equipe, com **liderança temporal**: vale
  quem liderava a equipe do titular na data da ausência.
- **E12 — aprovada:** transferência entre pessoas no alcance, sempre pela operação de transferência (histórico
  preservado, nunca edição direta do vínculo); destino fora do alcance bloqueia a operação.
- **E13 — alterada:** **permissão ≠ alcance.** A permissão (METAS.*) diz se o usuário cria, edita ou lança; o alcance diz
  quais metas e participantes ele vê e pode operar. Não exigir alcance Tudo.
- **E14 — aprovada:** as opções de quem atende são todos os colaboradores elegíveis ao papel, independentes do alcance de
  clientes; valem os critérios próprios de elegibilidade.
- Fora do alcance = inexistente também por id, rota, API, ficha, consulta e exportação. Sem migration, se não houver
  mudança de banco. Não antecipar a 2b.

**Andamento da 2a-3 (28/09/2026): aprovada pelo usuário (23h14) e commitada. Sem migration.** Depois dos testes nas telas, o aviso do "Novo relacionamento" passou a sumir quando o tipo ou a pessoa é escolhido.

- **Domínio** (`RegrasEscopo`):
  - o escopo passa a trazer `Pessoas` (o usuário e a equipe), `EquipesGeridas` e `EquipesDasPessoas`;
  - `GerenciaEm` (liderança na data: E11 e E12);
  - `AlcancaPorRelacao` e `RelacionamentoVisivel` (E9, um nível);
  - `ParticipanteNoAlcance`, `MetaVisivel` e `MetaInteiraNoAlcance` (E13).
- **E9:**
  - o escopo de Pessoas no banco inclui quem tem relacionamento vigente com um cliente da carteira alcançada;
  - na ficha dessa pessoa, só aparecem os relacionamentos com esses clientes;
  - relacionar alguém de fora a um cliente do alcance é recusado (fora do alcance = inexistente);
  - cadastro novo pode nascer relacionado (`PessoaDto.RelacionarAoCriar`, botão "Cadastrar nova pessoa assim" na aba
    Relacionamentos), e com alcance restrito isso libera cadastrar quem não é cliente, desde que o outro lado seja um
    cliente da carteira.
- **Coberturas (E11):**
  - a lista e a leitura mostram só o que envolve o alcance (titular, quem cobre ou a equipe);
  - cadastrar, alterar e cancelar exigem liderar o titular no início da ausência;
  - o aviso na ficha fica só com as ausências de quem atende clientes do alcance.
- **Carteira vencendo:** só clientes do alcance.
- **Carteira em uma data:** cliente ou pessoa no alcance de hoje (E10).
- **Transferências (E12):**
  - origem e destinos precisam estar no alcance pela liderança na data de efeito, e destino fora bloqueia tudo;
  - a lista e a leitura mostram só o que envolve alguém do alcance.
- **Metas (E13):**
  - lista, leitura e apuração mostram só os participantes do alcance (`MetaDto.ParcialPorAlcance`, com aviso na tela);
  - estrutura, situação e cancelamento exigem a permissão e a meta inteira no alcance;
  - lançar e importar realizado vale só para os participantes do alcance;
  - as opções de participantes são as do alcance.
- **Opções de quem atende (E14):** sem mudança; as leituras de `db.Pessoas` do Comercial e das metas ganharam o motivo.
- **Testes:**
  - domínio: pessoas e equipes no alcance, liderança temporal, E9 e metas;
  - aplicação: relacionamentos com alcance restrito, transferência dentro e fora da equipe, lista de transferências;
  - os testes que constroem os serviços usam o `EscopoFixo` (alcance Tudo por padrão).
- **Depois da revisão independente:**
  - a origem da transferência vale também pela véspera do efeito (no desligamento, o vendedor sai da equipe no dia
    anterior), e a transferência gravada é relida sem o filtro de hoje;
  - a pessoa nova e o relacionamento com que ela nasce são gravados na mesma transação
    (`IPessoaRepositorio.SalvarNovaComRelacionamentoAsync`);
  - com alcance restrito, esse relacionamento precisa valer hoje;
  - meta sem participantes só aparece para o alcance Tudo, e o gerente cria a meta já com participantes do alcance;
  - com alcance restrito, "não existe" e "fora do alcance" respondem igual também em coberturas e metas;
  - a carteira vencendo usa só os clientes diretos (contatos e sócios não têm carteira);
  - a permissão é conferida antes da regra da meta inteira.
- **Aceitos, anotados para decidir depois:**
  - os filtros "Pessoa relacionada" e "Tipo de relacionamento" da lista consideram todos os relacionamentos da pessoa;
    mostram só pessoas do alcance, mas poderiam indicar um relacionamento que a ficha esconde;
  - o aviso de ausência na ficha mostra só as ausências de quem atende pelo alcance;
  - o líder pode indicar como substituto alguém de fora da equipe (E14: colaboradores elegíveis), e a cobertura com
    "pode acessar" dá a essa pessoa acesso aos clientes do titular durante a ausência.
- **Testes de aplicação novos:** coberturas do líder (lista, leitura, cancelar e cadastrar fora da equipe), carteira em uma
  data com alcance restrito e metas (`Aplicacao/MetasEscopoTests`: lista, leitura parcial, apuração, situação bloqueada,
  lançamento só dos seus, criação só com participantes do alcance).
