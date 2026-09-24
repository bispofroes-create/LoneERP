# Lone ERP — Fase 0: diagnóstico e plano de evolução

Situação em 24/09/2026. Nenhum código foi alterado para produzir este documento.
Base: leitura do código em `src/` e `tests/` (Lone.slnx), mais a solução antiga na raiz (Lone.sln, WinUI).

> **Observação sobre os requisitos.** Os documentos "Evolução Completa do Cadastro Geral do ERP" e
> "Motor de Metas — Estrutura Profissional e Parametrizável" **não estão na pasta do projeto**.
> Este diagnóstico usa os requisitos como descritos na mensagem de missão. Se os documentos tiverem
> mais detalhes, eles devem ser colocados em `docs/` antes da Fase 2.

---

## 1. DIAGNÓSTICO

### 1.1 Stack

| Item | Encontrado |
|---|---|
| Linguagem | C# (.NET 10, `net10.0`) |
| Frontend | .NET MAUI (Windows + Android), XAML com bindings compilados, CommunityToolkit.Mvvm 8.4 |
| Backend | ASP.NET Core 10, Minimal APIs (`Lone.Api/Endpoints`) |
| Banco | SQL Server Express (`.\SQLEXPRESS`, banco `LoneERP`) |
| ORM | EF Core 10.0.0 (SqlServer), `IDbContextFactory<LoneDbContext>` |
| Arquitetura | Camadas: App → Cliente (ViewModels + HTTP) → Api → Application → Domain ← Infrastructure |
| Autenticação | JWT (15 min) + token de renovação (14 dias, tabela `TokensRenovacao`), PBKDF2, bloqueio após 5 tentativas, rate limit no login |
| Autorização | Catálogo de permissões por código (`Permissoes.cs`), perfis por empresa, `IAutorizacao.Exigir/Possui` nos AppServices, cache de 30 s na API (`CacheAcesso`) |
| Migrações | EF Migrations em `Lone.Infrastructure/Persistencia/Migracoes`, geradas no PMC pelo usuário; a API aplica ao iniciar em desenvolvimento |
| Testes | xUnit 2.9.3, **178 testes** (`[Fact]`/`[Theory]`), sem banco; cliente testado com servidor HTTP falso (fila FIFO) |
| Integrações | ViaCEP, BrasilAPI (CNPJ), CNPJ.ws (IE), IBGE (municípios) |

**Restrição de ambiente:** nem a nuvem desta sessão nem a máquina virtual local têm o SDK do .NET, e a rede
bloqueia a instalação. **Compilação, testes e `Add-Migration` precisam ser feitos por você no Visual Studio**
ao fim de cada fase. Eu entrego o código e a lista de verificação.

### 1.2 Estrutura

| Projeto | Conteúdo relevante |
|---|---|
| Lone.Domain | 24 entidades, 13 enums, objetos de valor (Cpf, Cnpj, Cep, Email, Telefone), `PessoaNormalizador`, `PessoaValidador`, IE dos 27 estados, motor de tipos de campo personalizado (`ITipoCampo`, `TiposCampo`) |
| Lone.Contracts | `PessoaDto`, `FiltroPessoas`, `PessoaResumo`, `Rotas`, `Permissoes`, DTOs de segurança, campos, municípios |
| Lone.Application | `PessoaAppService`, `CampoPersonalizadoAppService`, `MunicipioAppService`, `UsuarioAppService`, `PerfilAppService`, `AutenticacaoService`, `AcessoService`, `ConsultasAppService`; interfaces de repositório |
| Lone.Infrastructure | `LoneDbContext`, 14 arquivos de configuração, 6 repositórios, `ColetorAuditoria`, `AuditoriaConsultas`, `EmpresaConsultas`, integrações, JWT |
| Lone.Api | Endpoints: Autenticação, Pessoas, Cadastros (municípios, campos), Consultas (CEP/CNPJ), Segurança (usuários, perfis, empresas); `CargaMunicipios` em segundo plano |
| Lone.Cliente | `ClienteApi` e APIs tipadas, `CadastroViewModelBase<T>`, `PessoasViewModel`, `PessoaFormulario` (588 linhas) e formulários filhos, `SeletorMunicipio`, `IDialogos` |
| Lone.App | Telas: Login, Primeiro acesso, Trocar senha, Escolher empresa, Início, Pessoas (12 seções), Campos personalizados, Usuários, Perfis, Em construção |

**Migrações existentes:** `Inicial` (24/09), `DadosComplementaresPessoa` (24/09), `MunicipiosECamposPersonalizados` (24/09, com SQL manual).

**Solução antiga (WinUI):** `Lone.Core`, `Lone.Data`, `Lone.Aplicacao`, `Lone.Integracoes`, `Views`, `ViewModels` na raiz.
Aponta para o **mesmo banco `LoneERP`**, mas não tem migrações próprias. Será removida na M5.

### 1.3 Cadastro de pessoas

**Entidade raiz:** `Pessoa` (tabela `Pessoas`), `AgregadoRaiz` com `Versao` (rowversion) e eventos de negócio.
Código sequencial (`SEQUENCE`), natureza (Física, Jurídica, Estrangeiro), `DocumentoPrincipal` (CPF ou raiz do CNPJ, índice único filtrado).

| Requisito | O que existe | Tabela | Observação |
|---|---|---|---|
| Papéis | `PessoaPapel` com enum `TipoPapel` (Cliente, Fornecedor, EmpresaDoGrupo, Vendedor, Funcionario, Transportadora, Representante, PrestadorServico), `Ativo`, `InicioEm`, `FimEm`, `Observacoes` | `PessoaPapeis` | **Enum fixo**, não parametrizável. Índice único `(PessoaId, Papel)` impede mais de um período do mesmo papel. O enum é usado em regras: empresas do grupo, permissões, descrição dos eventos |
| Telefones e e-mails | `MeioContato` (tipo enum `TipoContato`: Telefone, Celular, WhatsApp, Email, Outro; `Valor`, `Descricao`, `Principal`, `PermiteComunicacao`) | `PessoaMeiosContato` | Telefone e e-mail na **mesma tabela**. Sem DDD separado, ramal, SMS, finalidades de e-mail (financeiro, cobrança, NF-e, marketing), status |
| Pessoas de contato | `Contato` (nome, cargo, departamento, telefone, celular, WhatsApp, e-mail, `PessoaVinculadaId`) | `PessoaContatos` | Telefone e e-mail do contato são colunas de texto soltas |
| Endereços | `PessoaEndereco`: descrição (identificação), `Finalidades` (flags: Principal, Fiscal, Cobrança, Entrega, Correspondência, Residencial, Comercial), ordem, CEP, logradouro, número, complemento, bairro, `MunicipioId` (IBGE), cópia de cidade/UF/código, país | `PessoaEnderecos` | Já permite vários, inclusive do mesmo tipo. Falta tipo parametrizável, observações e status |
| Documentos | `PessoaDocumento` (enum `TipoDocumento`: RG, CNH, Passaporte, Estrangeiro, Outro; número, órgão, UF, emissão, validade, observações) | `PessoaDocumentos` | **Enum fixo**. CPF e CNPJ **não** ficam aqui: ficam em `Pessoas.DocumentoPrincipal` e `Estabelecimentos.Cnpj`, com índices únicos. Sem anexos, sem campos por tipo, sem alerta de vencimento |
| Profissão | `Pessoa.Profissao` texto livre (80) | `Pessoas` | Sem cadastro, sem CBO. Gera duplicidade semântica |
| Etiquetas | `PessoaEtiqueta.Texto` (40), índice `(PessoaId, Texto)` único e índice por `Texto` | `PessoaEtiquetas` | Texto livre por pessoa (digitado separado por vírgula). **Não há cadastro reutilizável** |
| Campos personalizados | `CampoPersonalizado` + `CampoPersonalizadoOpcao` + `PessoaValorPersonalizado` (colunas tipadas: texto, número, data, lógico, opção). 12 tipos. Índices `(CampoId, ValorNumero)`, `(CampoId, ValorData)`, `(CampoId, OpcaoId)` | 3 tabelas | Já é o modelo "EAV tipado". `EntidadePersonalizavel` só tem `Pessoa`. Falta `Visivel`, `Pesquisavel`, tipos CPF e CNPJ, índice por `ValorTexto` |
| Dados fiscais PJ | `Estabelecimento`: CNPJ, principal, nome fantasia, ativo, situação na Receita, `IndicadorIE`, IE, IM, SUFRAMA, `RegimeTributario` (enum: Simples, MEI, Normal), CNAE principal, natureza jurídica, **CNAEs secundários em texto separado por vírgula (1000)**, endereço fiscal | `Estabelecimentos` | Um CNPJ = uma linha (matriz e filiais). **Sem histórico** (só auditoria). CNAE secundário não é pesquisável |
| Sócios | `PessoaSocio` (nome, qualificação, documento, entrada) | `PessoaSocios` | Vindos da Receita |
| Colaboradores | Só os papéis `Funcionario` e `Vendedor`. Sexo, gênero e cor/raça restritos | — | **Não existe** matrícula, cargo, departamento, admissão, gestor etc. |
| Vendedores / representantes | Papéis `Vendedor` e `Representante`; `ContaCliente.VendedorPadraoId` | `ContasCliente` | Um vendedor padrão por cliente e empresa, sem vigência |
| Relacionamentos | `PessoaRelacionamento` (pessoa destino, tipo, início, fim, observações) + `TipoRelacionamento` parametrizável (6 do sistema: sócio de, responsável por, representante de, contato de, dependente de, funcionário de) | `PessoaRelacionamentos`, `TiposRelacionamento` | **Existe no banco, mas não tem API, tela nem leitura** (fica fora de `ObterAsync`) |
| Grupo econômico | `GrupoEconomico` + `Pessoa.GrupoEconomicoId` | `GruposEconomicos` | Sem tela nem API |
| Mesclagem | `Pessoa.MescladaEmId` | `Pessoas` | Sem operação |
| LGPD | `PessoaConsentimento` (canal, concedido, datas, origem), um por canal | `PessoaConsentimentos` | Funcionando |
| Situação | `SituacaoPessoa` (Ativo, EmAnalise, Inativo, Arquivado), motivo, data; ações desativar/reativar com evento | `Pessoas` | Funcionando |
| Bloqueios | `Bloqueio` (empresa, escopo Comercial/Financeiro/Cadastral, origem manual/automático, motivo, início/fim, quem) | `PessoaBloqueios` | **Modelo com histórico pronto, mas sem operação, API nem tela.** Só é lido no DTO |

### 1.4 Comercial

| Requisito | Situação |
|---|---|
| Clientes | Papel `Cliente` + `ContaCliente` por empresa do grupo (limite de crédito, dias de atraso, desconto máximo %, condição de pagamento **texto**, exige aprovação, vendedor padrão) |
| Fornecedores | `ContaFornecedor` por empresa (condição **texto**, prazo médio, lead time, transportadora padrão, avaliação) |
| Vendedores / carteira | Só `VendedorPadraoId` (um por cliente e empresa, sem vigência) |
| Tabelas de preço | **Não existe** |
| Descontos / margem / alçadas | Só `DescontoMaximo` na conta do cliente. **Sem motor de preços, sem margem, sem alçada** |
| Condições de pagamento | **Não existe cadastro**: texto livre |
| Comissões | **Não existe** |
| Vendas, pedidos, orçamentos, faturamento, devoluções, recebimentos | **Não existem** (nenhuma entidade, tabela ou endpoint; o menu só tem Início, Pessoas, Campos, Usuários, Perfis) |
| Produtos | **Não existe** |
| Permissões comerciais | Só `PESSOAS.ALTERAR_CREDITO` e `PESSOAS.VISUALIZAR_FINANCEIRO` |

### 1.5 Metas

Busca por metas, indicadores, comissões, bônus, vendas, desempenho, dashboards e relatórios em `src/`, `tests/` e na solução antiga:
**nada encontrado**. Os únicos indicadores são `clientes-ativos` e `faixas-etarias` (tela Início).
Também **não há sistema de notificações/alertas** (só `BarraMensagem` e `IDialogos` na interface).

### 1.6 Banco

28 tabelas no modelo novo: `Pessoas`, `PessoaPapeis`, `PessoaMeiosContato`, `PessoaContatos`, `PessoaDocumentos`,
`PessoaEnderecos`, `Estabelecimentos`, `PessoaSocios`, `PessoaConsentimentos`, `PessoaEtiquetas`, `PessoaBloqueios`,
`PessoaRelacionamentos`, `TiposRelacionamento`, `GruposEconomicos`, `ContasCliente`, `ContasFornecedor`,
`CamposPersonalizados`, `CampoPersonalizadoOpcoes`, `PessoaValoresPersonalizados`, `Municipios`, `PendenciasMunicipio`,
`Usuarios`, `UsuarioAcessos`, `UsuarioPerfis`, `Perfis`, `PerfilPermissoes`, `TokensRenovacao`, `Auditoria`.

- **Chaves:** GUID sequencial gerado no aparelho; exceção `Municipios.Id` (código IBGE). `Auditoria.Id` é `long`.
- **Índices relevantes:** `Pessoas(Codigo)` único, `Pessoas(Natureza, DocumentoPrincipal)` único filtrado, `Pessoas(Nome)`,
  `Pessoas(Situacao)`, `Pessoas(DataNascimento)`, `PessoaPapeis(Papel, Ativo)`, `PessoaMeiosContato(Valor)`,
  `Estabelecimentos(Cnpj)` único filtrado, `PessoaEtiquetas(Texto)`, `Auditoria(RaizEntidade, RaizId, DataHora)`.
- **Chaves estrangeiras:** filhos da pessoa com `CASCADE` (nunca disparado, porque pessoa não é excluída);
  referências cruzadas (`EmpresaId`, `VendedorPadraoId`, `PessoaDestinoId`, `MunicipioId`) com `NO ACTION`/`RESTRICT`.
- **Possíveis duplicidades conceituais:**
  1. Telefone/e-mail em dois lugares: `PessoaMeiosContato` e colunas de `PessoaContatos`.
  2. Papel `Vendedor` + `ContaCliente.VendedorPadraoId` + tipo de relacionamento `RepresentanteDe`: três formas de ligar vendedor e cliente.
  3. `EscopoBloqueio.Cadastral` e `SituacaoPessoa.Inativo`: sobreposição de "situação cadastral".
  4. Papel `Funcionario` e tipo de relacionamento `FuncionarioDe`.

### 1.7 Dependências do cadastro de pessoas

| Quem depende | Como |
|---|---|
| Empresas do grupo / login | `EmpresaConsultas` lê `Pessoas` com papel `EmpresaDoGrupo` e seus `Estabelecimentos`; `UsuarioPerfil.EmpresaId` e `AcessoService` usam isso para escolher a empresa e calcular permissões |
| Contas de cliente e fornecedor | `EmpresaId`, `VendedorPadraoId`, `TransportadoraPadraoId` apontam para `Pessoas` |
| Auditoria / histórico | `ColetorAuditoria` agrupa filhos pela raiz `Pessoa`; `AuditoriaConsultas` traduz município e campo personalizado |
| Municípios | `NaturalidadeMunicipioId`, `PessoaEndereco.MunicipioId`, `PendenciasMunicipio` |
| Campos personalizados | `PessoaValoresPersonalizados` |
| Indicadores da tela Início | `ContarClientesAtivosAsync`, `ListarNascimentosAsync` |
| Cliente/App | `PessoaDto` inteiro trafega na ficha; `PessoaFormulario` e 12 seções XAML; testes de formulário e de ViewModel dependem da ordem das chamadas no servidor falso |
| Enum `TipoPapel` | `Pessoa.Descricao()`, `NomesPessoa.Papel`, `FiltroPessoas.Papel`, `PapelOpcao` (cliente), regras de permissão e de dados sensíveis |

### 1.8 Permissões

Um único sistema, bem estruturado: catálogo imutável por código, perfis por empresa, administrador com tudo.
Qualquer permissão nova (papéis, profissões, documentos, colaborador, metas etc.) entra **no mesmo catálogo** e aparece
sozinha na tela de Perfis. **Não há alçadas** (limite por valor). Quando houver desconto/margem, alçada será um
conceito novo (valor-limite por perfil), que deve **estender** `Perfil`, não criar outro sistema.

### 1.9 Auditoria

`ColetorAuditoria` dentro de `SaveChangesAsync`: uma linha por campo alterado, usuário, data/hora, operação, origem,
entidade, raiz, valor anterior e novo, eventos de negócio com descrição. Atributos `[DadoSensivel]`, `[NaoAuditarValor]`,
`[NaoAuditar]`. A aba **Histórico** da pessoa já é uma linha do tempo do cadastro (até 500 linhas, sem paginação).
Lacunas: não há coluna **Motivo** (o motivo só aparece dentro do texto do evento); valores limitados a 500 caracteres;
filho removido some da tabela e fica só o registro de exclusão.

### 1.10 Testes

178 testes em Domínio, Aplicação, API (contrato JSON, problemas, segurança), Cliente (ViewModels, formulários, máscaras,
sessão) e Integrações. **Não há testes de repositório nem de migração contra banco.** As consultas LINQ do
`PessoaRepositorio` só são validadas rodando o app.

---

## 2. ACHADOS IMPORTANTES (afetam o plano)

1. **Exclusão física de filhos.** `PessoaRepositorio.SincronizarFilhos` apaga do banco qualquer endereço, telefone,
   documento, **papel**, conta ou sócio que o usuário remover da ficha. Isso contraria a regra de histórico (nº 4) e a sua
   decisão "nunca excluir fisicamente". Ex.: remover o papel Cliente apaga `InicioEm/FimEm`. A auditoria registra a exclusão,
   mas o registro sai da tabela. **Correção proposta na Fase 3:** papéis, endereços, meios de contato e documentos passam a
   ser **desativados** (ativo + data de fim), e a remoção física fica só para linha criada e removida na mesma edição.
2. **Não existe módulo de vendas, pedidos, faturamento, produtos, preços, condições de pagamento nem comissões.**
   A regra "nunca invente regras comerciais" impede calcular o "realizado" de metas financeiras hoje. Veja a seção 8.
3. **Busca não escala.** `ListarAsync` usa `Contains` (`LIKE '%x%'`) em 7 tabelas, ordena por
   `COALESCE(NomeExibicao, NomeSocial, Nome)` (sem índice) e devolve até 500 linhas **sem paginação**.
   Com 100 mil pessoas, isso é varredura completa a cada tecla. O próprio código já prevê "tabela de termos + paginação por chave".
4. **Modelos prontos e sem uso:** `Bloqueio`, `PessoaRelacionamento`/`TipoRelacionamento`, `GrupoEconomico`, `MescladaEmId`.
   São a base natural para situação comercial, carteira de clientes e grupos econômicos: **evoluir, não recriar**.
5. **Enums fixos** onde o requisito pede cadastro parametrizável: `TipoPapel`, `TipoDocumento`, `TipoContato`, `FinalidadeEndereco`,
   `RegimeTributario`. Vários têm **significado de regra** no código (ex.: `EmpresaDoGrupo`, endereço `Fiscal`), então não
   podem simplesmente virar texto configurável.
6. **CNAEs secundários em texto** separado por vírgula: impossível filtrar por CNAE com índice.
7. **Solução antiga aponta para o mesmo banco.** Rodá-la por engano não altera o esquema (não tem migrações), mas pode gravar
   dados com outro modelo. Reforça a importância da M5.
8. **Erro de compilação/teste pendente**, relatado antes desta missão e ainda não informado. Precisa ser resolvido antes da Fase 2.

---

## 3. O QUE JÁ EXISTE (será reutilizado)

| Estrutura | Onde | Uso na evolução |
|---|---|---|
| `Pessoa` como agregado central, sem duplicar pessoa por papel | Domain/Entidades | Base de tudo |
| `EntidadePessoaFilha`, `SincronizarFilhos`, `ReaproveitarIds` | Domain, Infrastructure | Padrão para todo filho novo (com a correção de desativação) |
| `PessoaAppService.SalvarAsync` (fluxo de 9 passos) | Application | Novos passos entram nele, não num serviço paralelo |
| `PessoaNormalizador` / `PessoaValidador` | Domain/Validacao | Regras novas de normalização e validação |
| Catálogo `Permissoes` + perfis por empresa + `IAutorizacao` | Contracts, Application | Todas as permissões novas |
| `ColetorAuditoria` + eventos do `AgregadoRaiz` + aba Histórico | Infrastructure, App | Auditoria e linha do tempo |
| Motor de campos personalizados (`ITipoCampo`, `TiposCampo`, `ValidadorValoresPersonalizados`, colunas tipadas) | Domain | Campos de documentos (novo escopo), tipos CPF/CNPJ |
| `PendenciasMunicipio` + `IndiceMunicipios` (conciliação de texto antigo) | Infrastructure, Application | **Mesmo padrão** para migrar profissão e etiquetas em texto |
| Carga de tabela oficial em segundo plano (`CargaMunicipios`) | Api/Servicos | Mesmo padrão para CNAE (IBGE) e CBO, se aprovado |
| `Bloqueio` (escopo, origem, motivo, início/fim, quem) | Domain | Situação comercial e cadastral com histórico |
| `PessoaRelacionamento` + `TipoRelacionamento` | Domain | Carteira de clientes (vínculos com tipo e vigência) |
| `Estabelecimento` (matriz/filial por CNPJ) + empresas do grupo | Domain, `EmpresaConsultas` | Dados fiscais PJ, filiais, hierarquia de metas (Empresa → Filial) |
| `GrupoEconomico` | Domain | Grupos econômicos |
| `CadastroViewModelBase<T>`, `LayoutMestreDetalhe`, controles `Campo`/`CampoEscolha`/`CaixaMarcar` | Cliente, App | Telas de cadastros auxiliares (papéis, profissões, etiquetas, tipos) |
| `IdSequencial`, `rowversion`, `ConflitoDeEdicaoException` | Domain | Todas as tabelas novas |

## 4. O QUE ESTÁ FALTANDO

- Cadastros parametrizáveis: papéis, profissões, etiquetas, tipos de telefone/e-mail, tipos de endereço, tipos de documento.
- Telefone: DDD, ramal, SMS, status. E-mail: finalidades (financeiro, cobrança, NF-e, marketing). Endereço: observações, status.
- Campos personalizados por tipo de documento; flags `Visivel` e `Pesquisavel`; tipos CPF e CNPJ; alerta de vencimento; anexos (não há armazenamento de arquivos).
- Coluna `Motivo` na auditoria; paginação do histórico.
- Operações de bloqueio/desbloqueio (API, tela, permissão); escopo "faturamento".
- Situação de relacionamento e datas de última compra/interação.
- Dados do colaborador: vínculo, matrícula, cargo, departamento, setor, gestor, centro de custo, admissão/desligamento, jornada — e os cadastros de cargo, departamento, setor e centro de custo.
- Perfil comercial (padrões + exceções), carteira com vários vínculos e vigência.
- Histórico fiscal com vigência (regime, IE, situação); CNAEs em tabela; produtor rural.
- Filtro avançado combinável com paginação por chave.
- Todo o Motor de Metas.
- Módulos transacionais (vendas, faturamento, preços, comissões): **fora do escopo desta missão**, mas pré-requisito do "realizado" automático.

## 5. O QUE PRECISA SER ALTERADO (previsão; detalhado fase a fase)

| Camada | Itens |
|---|---|
| Domain | `Pessoa` (+`ProfissaoId`), `PessoaPapel` (+`PapelId`), `PessoaEtiqueta` (+`EtiquetaId`), `MeioContato` (+tipo, DDD, ramal, flags, `Ativo`), `PessoaEndereco` (+tipo, observações, `Ativo`), `PessoaDocumento` (+`TipoDocumentoId`, `Ativo`), `CampoPersonalizado` (+escopo, `Visivel`, `Pesquisavel`), `EntidadePersonalizavel` (+`Documento`), `TiposCampo` (+CPF, CNPJ), `EscopoBloqueio` (+faturamento), entidades novas dos cadastros auxiliares, colaborador e metas |
| Infrastructure | Configurações novas, `LoneDbContext`, `PessoaRepositorio` (`ObterAsync`, `SincronizarFilhos`, `ListarAsync`), `AuditoriaConsultas` (traduzir Ids de papel/profissão/etiqueta/tipos), `RegistroAuditoria` (+`Motivo`), repositórios dos cadastros novos |
| Application | `PessoaAppService.SalvarAsync` (resolver referências), `PessoaMapeamento`, AppServices dos cadastros auxiliares, bloqueios, colaborador, metas |
| Contracts | `PessoaDto` e filhos, `FiltroPessoas` (critérios + página), `Rotas`, `Permissoes` (novos códigos) |
| Api | Endpoints dos cadastros auxiliares, bloqueio/desbloqueio, metas |
| Cliente/App | Seções da ficha (Papéis, Etiquetas, Contatos, Endereços, Documentos, Colaborador), telas de cadastros auxiliares, filtro avançado, metas |
| Migrações | Uma por fase, sempre aditiva, com SQL de migração de dados e contagens antes/depois |

## 6. O QUE NÃO DEVE SER ALTERADO

- Login, primeiro acesso, troca de senha, renovação de token, escolha de empresa.
- Catálogo de permissões existente (códigos gravados nunca mudam).
- Regras de CPF/CNPJ, índice único de documento, IE dos 27 estados, enriquecimento pelo CNPJ.
- Municípios IBGE, pendências de município, SQL manual da migração `MunicipiosECamposPersonalizados`.
- Fluxo Descartar/alterações não salvas, `CadastroViewModelBase`.
- Consentimentos LGPD, situação da pessoa (desativar/reativar com evento).
- Formato da tabela `Auditoria` (só **acréscimo** de coluna).
- Chaves GUID sequenciais; valores numéricos dos enums já gravados.

---

## 7. RISCOS

| # | Risco | Nível | Motivo | Mitigação |
|---|---|---|---|---|
| R1 | Trocar enums (`TipoPapel`, `TipoDocumento`, `TipoContato`) por tabelas | **Crítico** | Enums têm significado de regra (empresas do grupo → login e permissões). Erro aqui derruba o acesso ao sistema | Tabela com "papel de sistema" ligado ao enum; coluna antiga mantida; regras continuam usando o código do sistema |
| R2 | Migração de dados de texto (profissão, etiquetas) | Alto | Duplicidades semânticas; perda silenciosa | Padrão de pendência (como municípios), contagem antes/depois, texto original preservado |
| R3 | Mudar `SincronizarFilhos` para desativar em vez de excluir | Alto | Afeta todas as seções da ficha e índices únicos (ex.: `(PessoaId, Papel)`) | Filtrar ativos na leitura, ajustar índices únicos para filtrados, testes de formulário |
| R4 | Metas sem módulo de vendas | Alto | "Realizado" financeiro não tem fonte; risco de inventar regra | Motor com fontes plugáveis; só fontes reais; apuração manual/importada até existir vendas |
| R5 | Performance da busca com 100 mil pessoas | Alto | `LIKE '%x%'` em várias tabelas, sem paginação | Fase 12: termos normalizados + paginação por chave; índices por critério |
| R6 | Compilar/testar só na sua máquina | Médio | Não consigo rodar o build; ciclo mais lento | Fases pequenas; lista de verificação por fase; testes de unidade para cada regra |
| R7 | Crescimento da `Auditoria` | Médio | Uma linha por campo; histórico sem paginação | Paginação por `DataHora`; índice já existe |
| R8 | Ordem de chamadas do servidor falso nos testes do cliente | Médio | Cada leitura nova na tela de pessoas quebra testes existentes | Ajustar a fila nos testes em cada fase (já aconteceu na parte 4) |
| R9 | Solução antiga no mesmo banco | Baixo | Sem migrações próprias; uso acidental | Fazer a M5 cedo (ver plano) |
| R10 | Anexos de documentos | Médio | Não existe armazenamento de arquivos; escolha errada pesa no banco Express (limite de 10 GB) | Decisão arquitetural separada antes da Fase 4b |

---

## 8. ALTERNATIVAS QUE PRECISAM DA SUA DECISÃO

### D1. Papéis parametrizáveis
- **Especificado:** cadastro de papéis com código, nome, descrição, ativo, ordem, vigência.
- **No projeto:** enum `TipoPapel` gravado em `PessoaPapeis.Papel`, com regras presas a ele.
- **Proposta:** tabela `Papeis` (Guid, código imutável, nome, descrição, ordem, ativo, `PapelSistema` byte nulo e único que liga ao enum).
  Os 8 papéis atuais nascem como papéis de sistema (não podem ser desativados se tiverem regra). `PessoaPapeis` ganha `PapelId`;
  a coluna `Papel` fica preenchida para os papéis de sistema e o código continua usando `TemPapel(TipoPapel.X)`.
  Índice único passa a `(PessoaId, PapelId)` **filtrado por ativo**, permitindo vários períodos do mesmo papel.
- **Impacto:** médio. **Risco:** crítico se feito sem o vínculo com o enum; baixo com ele.

### D2. Telefones e e-mails
- **Especificado:** telefones e e-mails com campos próprios e tipos parametrizáveis.
- **No projeto:** uma tabela `PessoaMeiosContato` para os dois.
- **Opção A (recomendada):** evoluir `PessoaMeiosContato`: tabela `TiposMeioContato` (categoria Telefone/E-mail, nome, ativo, ordem);
  colunas novas `TipoMeioContatoId`, `Ddd`, `Ramal`, `WhatsApp`, `Sms`, `Finalidades` (flags de e-mail: financeiro, cobrança, NF-e, marketing),
  `Observacao`, `Ativo`. A busca por telefone/e-mail continua numa tabela e num índice só.
- **Opção B:** criar `PessoaTelefones` e `PessoaEmails` e migrar. Mais "limpo" no papel, mas duplica toda a lógica de sincronização, auditoria,
  busca e duplicidade, e quebra o que já funciona.
- **Recomendação:** A.

### D3. Tipo de endereço
- **No projeto:** `Finalidades` (flags) com significado de regra (Principal, Fiscal, Cobrança, Entrega).
- **Proposta:** manter as finalidades (regra) e acrescentar `TipoEnderecoId` parametrizável (classificação livre: "Depósito", "Filial Centro"...),
  mais `Observacoes` e `Ativo`.

### D4. Armazenamento de campos personalizados de documentos
- **Opções avaliadas:** (1) coluna física por campo — descartada; (2) JSON em `nvarchar(max)` — SQL Server não indexa JSON diretamente
  (só por coluna computada, uma por campo), validação e ordenação ruins; (3) EAV puro (valor em texto) — perde tipo e índice;
  (4) **EAV tipado, já usado em `PessoaValoresPersonalizados`**.
- **Recomendação:** reutilizar o motor atual (opção 4), com o escopo `Documento` + `TipoDocumentoId` no campo e uma tabela
  `PessoaDocumentoValoresPersonalizados` com as mesmas colunas tipadas e índices. Acrescentar índice `(CampoId, ValorTexto)` para os
  campos marcados como pesquisáveis.

### D5. Carteira de clientes
- **No projeto:** `ContaCliente.VendedorPadraoId` (um, sem vigência) e `PessoaRelacionamento` (tipado, com início/fim, sem uso).
- **Proposta:** usar `PessoaRelacionamento` com tipos novos de sistema (vendedor principal, secundário, responsável comercial, pós-venda,
  consultor, técnico, gerente da conta), acrescentando `EmpresaId`, `Exclusivo` e índice `(PessoaDestinoId, TipoRelacionamentoId, InicioEm)`.
  `VendedorPadraoId` passa a ser preenchido a partir do vínculo "vendedor principal" vigente (compatibilidade) e é migrado para vínculos.
- **Alternativa:** tabela `CarteiraClientes` própria. Só vale se você preferir separar vínculos comerciais de vínculos pessoais.

### D6. Motor de Metas sem módulo de vendas
- **Fato:** não existem vendas, pedidos, faturamento, devoluções, produtos nem recebimentos.
- **Opção A (recomendada):** construir agora só a **estrutura** (indicadores, metas, participantes, períodos, vigência, pesos, faixas,
  apuração com status) e uma interface `IFonteIndicador`. Fontes disponíveis hoje: indicadores do cadastro (novos clientes, clientes ativos,
  clientes reativados) e **realizado informado/importado** com auditoria. Fontes de venda/faturamento entram quando esses módulos existirem.
- **Opção B:** adiar o Motor de Metas para depois do módulo de vendas.
- **Opção C:** criar vendas agora — expansão de escopo grande, fora desta missão.

### D7. Anexos de documentos
- Não existe armazenamento de arquivos. Opções: `varbinary(max)` no banco (simples, mas pesa no limite de 10 GB do Express),
  FILESTREAM (complexo), pasta no servidor da API com metadados no banco (recomendado), armazenamento em nuvem (depende de contrato).
  Proposta: tratar em fase própria (4b), depois dos tipos de documento.

### D8. Ordem da M5
- Remover a solução antiga (M5) **antes** da Fase 2 elimina o risco R9 e simplifica o repositório. É uma etapa curta.

---

## 9. CLASSIFICAÇÃO DAS SUGESTÕES DO GEMINI

| Sugestão | Classificação | Justificativa |
|---|---|---|
| Grupos econômicos | 1. Já existente (sem tela) | `GrupoEconomico` + `Pessoa.GrupoEconomicoId`. Falta API/tela: melhoria pequena |
| Holding/matriz/filiais/coligadas | 1. Parcialmente existente | Matriz/filial = `Estabelecimento`; holding/coligada = `PessoaRelacionamento` + grupo econômico. Não criar estrutura nova |
| Limite de crédito | 1. Já existente | `ContaCliente.LimiteCredito` por empresa, com permissão própria |
| Risco financeiro | 5. Expansão de escopo | Depende de títulos/recebimentos, que não existem |
| Integração com bureaus | 5. Expansão de escopo | Custo, contrato e LGPD; sem módulo financeiro |
| LGPD/consentimento | 1. Já existente | `PessoaConsentimento` por canal com datas e origem |
| Portal de autoatendimento | 5. Expansão de escopo | Novo canal, nova autenticação |
| Omnichannel | 5. Expansão de escopo | Sem módulo de atendimento |
| WhatsApp Business API | 4. Melhoria futura | O cadastro já marca WhatsApp e consentimento; a integração é outro módulo |
| Timeline baseada em eventos | 1/2. Já existente e requisito | Auditoria + eventos do agregado já são a linha do tempo. Outros módulos devem publicar eventos na mesma tabela ou em referências. **Event Sourcing: over-engineering** |
| JSON/JSONB | 6. Over-engineering aqui | SQL Server não tem JSONB; perde índice e validação. Ver D4 |
| EAV | 1. Já existente (tipado) | Manter o EAV tipado; EAV puro seria regressão |

---

## 10. PLANO DE IMPLEMENTAÇÃO

Cada fase segue o ciclo: investigar → propor → implementar a menor mudança segura → você compila, roda os testes e gera a migração →
revisar → relatório da fase. Nenhuma fase começa com a anterior quebrada.

| Fase | Conteúdo | Depende de | Migração |
|---|---|---|---|
| **0.5** | Resolver o erro pendente; fechar a M4 parte 4; (opcional D8) M5 — remover a solução antiga | — | — |
| **1** | Este diagnóstico + suas decisões D1–D8 | — | — |
| **2a** | Etiquetas: cadastro `Etiquetas`; `PessoaEtiquetas.EtiquetaId`; migração dos textos (dedupe sem acento e sem caixa); tela de etiquetas; seleção na ficha | 1 | `CadastroEtiquetas` |
| **2b** | Profissões: cadastro com CBO; `Pessoa.ProfissaoId`; conciliação do texto antigo com pendência | 1 | `CadastroProfissoes` |
| **2c** | Papéis parametrizáveis (D1) | 1 | `CadastroPapeis` |
| **2d** | Tipos de meio de contato, de endereço e de documento (cadastros com itens de sistema) | 1 | `TiposAuxiliares` |
| **3** | Telefones/e-mails (D2), endereços (D3); **desativar em vez de excluir** filhos (achado 1) | 2d | `ContatosEEnderecos` |
| **4a** | Documentos parametrizáveis: `TipoDocumentoId`, múltiplos, validade, status de vencimento (válido / próximo / vencido) com antecedência configurável no tipo | 2d | `DocumentosParametrizaveis` |
| **4b** | Anexos (D7) | 4a | `AnexosDocumentos` |
| **5** | Campos personalizados de documentos (D4), `Visivel`, `Pesquisavel`, tipos CPF/CNPJ | 4a | `CamposDeDocumentos` |
| **6** | Auditoria: coluna `Motivo`, tradução de Ids novos, paginação do histórico, filtros na linha do tempo | 2–5 | `MotivoAuditoria` |
| **7** | Colaborador: cadastros de cargo, departamento, setor, centro de custo; vínculos com admissão/desligamento; histórico de cargo/departamento com vigência; gestor; aba contextual | 2c, 6 | `DadosColaborador` |
| **8** | Comercial: perfil comercial (padrões) + exceção individual com vigência; carteira (D5); condição de pagamento como cadastro (hoje texto) | 7 | `DadosComerciais` |
| **9** | Fiscal PJ: histórico com vigência de regime, IE e situação por estabelecimento; CNAEs em tabela (catálogo IBGE); produtor rural | 6 | `HistoricoFiscal` |
| **10** | Situações separadas: cadastral (existente), comercial (bloqueios: API, tela, permissões `PESSOAS.BLOQUEAR`/`DESBLOQUEAR`, escopo faturamento), relacionamento (última interação; última compra quando houver vendas); regras configuráveis de inatividade | 6, 8 | `SituacoesEAtividade` |
| **11** | Motor de Metas conforme D6: indicadores, metas compostas com pesos, faixas, períodos, vigência, participantes (hierarquia Empresa → Filial → Departamento → Equipe → Colaborador), apuração e status; contrato para comissões | 7, 8 | `MotorMetas` |
| **12** | Filtro avançado: critérios tipados (sem SQL dinâmico), composição no `IQueryable`, paginação por chave, índices por critério, termos de busca normalizados | 2–10 | `IndicesBusca` |
| **13** | Testes de regra, revisão de consultas (planos de execução), carga de 100 mil pessoas de teste, revisão final | todas | — |

## 11. DECISÕES DO USUÁRIO (24/09/2026)

O erro de compilação pendente foi resolvido pelo usuário.

| Decisão | Escolha |
|---|---|
| D1 Papéis | Tabela `Papeis` ligada ao enum `TipoPapel` (papéis de sistema); `PessoaPapeis.PapelId`; vários períodos do mesmo papel |
| D2 Telefones e e-mails | Evoluir `PessoaMeiosContato` (uma tabela, tipos parametrizáveis por categoria) |
| D3 Endereços | Manter `Finalidades` (regra) + `TipoEnderecoId` parametrizável, observações e ativo |
| D4 Campos de documentos | Reutilizar o motor de campos personalizados (colunas tipadas), escopo por tipo de documento |
| D5 Carteira de clientes | **Tabela própria `CarteiraClientes`** (não usar `PessoaRelacionamento`). `ContaCliente.VendedorPadraoId` continua existindo por compatibilidade e passa a refletir o vendedor principal vigente |
| D6 Metas | Construir a estrutura agora; realizado vindo do cadastro ou informado/importado com auditoria; vendas entram depois como nova fonte |
| D7 Anexos | Arquivos em pasta no servidor da API, metadados no banco |
| D8 M5 | Remover a solução WinUI antiga antes da Fase 2 |
