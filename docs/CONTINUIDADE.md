# Lone ERP — documento de continuidade

Situação em 24/09/2026. Serve para quem continuar o trabalho, seja outra conta ou outro assistente.
Todo o código está nesta pasta (`C:\Users\Windows 11\source\repos\Lone`). Este documento explica o que existe, as decisões tomadas e o que falta.

---

## 1. Visão geral

- **Produto:** Lone ERP, em C#. A interface foi migrada de WinUI 3 para **.NET MAUI** (Windows e Android).
- **Plataforma:** .NET 10, Visual Studio 2026, banco **SQL Server Express** (`.\SQLEXPRESS`).
- **Camadas:**
  `MAUI (Lone.App) → Lone.Cliente (ViewModels + cliente HTTP) → ASP.NET Core API (Lone.Api) → Lone.Application → Lone.Domain → Lone.Infrastructure (EF Core 10) → SQL Server`
- **Solução nova:** `Lone.slnx`.
- **Solução antiga (WinUI):** removida na M5 (24/09/2026). Continua recuperável no primeiro commit do git (`Snapshot antes da M5`).
- **Controle de versão:** repositório git local na raiz (branch `main`), criado em 24/09/2026. Um commit por fase.
- **Pacotes:** gestão central em `src/Directory.Packages.props`, com transitive pinning. O `Lone.App` usa `ManagePackageVersionsCentrally=false`, porque a versão do MAUI acompanha a carga de trabalho instalada.
- **Testes:** `tests/Lone.Tests` (xUnit). Não usam banco; o cliente é testado com um servidor HTTP falso que responde em fila (FIFO).

### Projetos
| Projeto | Papel |
|---|---|
| Lone.Domain | Entidades, enums, objetos de valor (Cpf, Cnpj, Cep, Email, Telefone), normalizador e validador de pessoa, validação de IE dos 27 estados, campos personalizados (tipos) |
| Lone.Contracts | DTOs, `Rotas` (todas as URLs da API), `Permissoes` (catálogo), `OpcoesJson` |
| Lone.Application | Casos de uso (AppServices), interfaces de repositório, mapeamentos |
| Lone.Infrastructure | EF Core (`LoneDbContext`, configurações, migrações, repositórios), auditoria, integrações (BrasilAPI, ViaCEP, CNPJ.ws, IBGE), JWT |
| Lone.Api | Minimal APIs, autenticação JWT, usuário da requisição, tratamento de erros, serviço em segundo plano da carga de municípios |
| Lone.Cliente | ViewModels (CommunityToolkit.Mvvm 8.4), `ClienteApi` (renovação automática do token), sessão e navegação. Não depende do MAUI e é testável |
| Lone.App | Telas MAUI (XAML com bindings compilados), controles (`Campo`, `CampoEscolha`, `CaixaMarcar`, `CampoMunicipio`, `BarraMensagem`, `LayoutMestreDetalhe`) e implementações de plataforma |

---

## 2. Decisões do usuário (seguir sempre)

- **Conexão:** o aplicativo fala só com a API, nunca direto com o banco.
- **Chaves:** GUID sequencial (`IdSequencial.Novo()`), gerado no aparelho.
  - **Exceção aprovada:** `Municipios.Id` é o código IBGE (int), por ser tabela oficial e imutável.
- **Nomes dos projetos:** `Lone.*`, em inglês. O código e as mensagens são em português.
- **Estilo:** classes pequenas, cada uma com uma responsabilidade.
  - Nada de lógica em code-behind. Só "cola" de interface, como `Clicked` repassando para um comando do ViewModel.
  - Comentários `///` em português explicando o porquê.
- **Nunca excluir fisicamente** cadastros, campos personalizados nem opções. Sempre desativar.
- **Endereço:** mantém `Cidade`, `Uf` e `CodigoMunicipioIbge` como cópia gravada a partir do município (decisão do usuário). A referência verdadeira é `MunicipioId`.
- **Eventos de negócio:** ficam no `AgregadoRaiz` e são gravados pelo `ColetorAuditoria` na mesma transação (decisão do usuário).
- **Perfil do usuário:** perfeccionista, quer arquitetura profissional.
  - Pede análise antes de mudanças grandes.
  - Pede para apresentar melhorias estruturais antes de mexer em algo que afete outras partes.

---

## 3. Padrões técnicos importantes

- **Agregado e concorrência:** `AgregadoRaiz` tem `Versao` (rowversion). Gravar com versão velha lança `ConflitoDeEdicaoException` (HTTP 409).
- **Filhos da pessoa:** herdam `EntidadePessoaFilha` (PessoaId e `IParteDeAgregado`).
  - `PessoaRepositorio.SalvarAsync` sincroniza os filhos com `SincronizarFilhos`.
  - `ReaproveitarIds` casa os registros pela chave natural: consentimento por canal, etiqueta por texto, valor personalizado por campo.
- **Fluxo de gravação de pessoa (`PessoaAppService.SalvarAsync`):**
  1. Mantém situação e datas de consentimento.
  2. Normaliza.
  3. Confere permissões.
  4. Valida.
  5. Resolve os municípios (`ReferenciasMunicipio.Aplicar`).
  6. Valida os campos personalizados (`ValidadorValoresPersonalizados`).
  7. Confere duplicidade de documento.
  8. Gera avisos (duplicidade e IE).
  9. Grava e relê do banco.
- **Auditoria (`ColetorAuditoria`, dentro de `LoneDbContext.SaveChangesAsync`):** grava a tabela `Auditoria` com uma linha por campo alterado.
  - `[DadoSensivel]` mascara o valor.
  - `[NaoAuditarValor]` registra só que mudou.
  - `[NaoAuditar]` deixa a entidade fora da auditoria.
  - `IValorAuditavel` produz uma linha só com o valor legível (usado pelos campos personalizados).
  - `AcaoAuditoria.Evento` guarda a frase em `Descricao`.
  - `AuditoriaConsultas` troca códigos por nomes: município e nome do campo personalizado.
- **Permissões:**
  - Catálogo em `Lone.Contracts/Seguranca/Permissoes.cs`. Um código gravado nunca muda.
  - Os AppServices usam `IAutorizacao.Exigir`.
  - Perfis são por empresa. Perfil administrador tem tudo, inclusive permissões futuras.
  - A API guarda as permissões em cache por 30 segundos.
- **Telas lista + ficha:** `CadastroViewModelBase<TItem>`, com `LayoutMestreDetalhe` (modo compacto abaixo de 760 de largura).
- **Migrações:** geradas pelo usuário no PMC, com projeto padrão Lone.Infrastructure:
  `Add-Migration Nome -StartupProject Lone.Api -OutputDir Persistencia/Migracoes`.
  A API aplica ao iniciar (`Banco:AplicarMigracoesAoIniciar=true` em desenvolvimento). O EF 9+ lança erro se o modelo mudou sem migração.

---

## 4. Plano de etapas

- **M1, M2, M3:** concluídas e testadas no Windows e no Android.
- **M4 (cadastros), em andamento:**
  - **Parte 1:** usuários e perfis. Concluída.
  - **Parte 2:** pessoas. Concluída.
  - **Parte 3:** cadastro completo. Migração `DadosComplementaresPessoa` gerada.
    - Máscaras.
    - Inscrição estadual via CNPJ.ws, com validação dos 27 estados.
    - CEP pré-validado.
    - Idade calculada e contagem por faixa etária.
    - Sexo, identidade de gênero e cor/raça (só para funcionários, com permissão).
    - Dados civis da PF.
    - Enriquecimento da PJ pelo CNPJ (sócios, CNAEs).
    - Consentimentos LGPD e etiquetas.
  - **Parte 4 (esta sessão):** municípios do IBGE, Descartar, campos personalizados, desativar/reativar, eventos na auditoria. Veja a seção 5.
  - **Critério para fechar a M4:** empresa do grupo, cliente PJ com filial e usuário com perfil por empresa funcionando no Windows e no Android.
- **M5:** remover o projeto WinUI antigo. **Concluída em 24/09/2026**, antes da Fase 2 (decisão D8).
- **M6:** funcionamento offline.
- **Evolução do Cadastro Geral e Motor de Metas (Fases 2 a 13):** diagnóstico, riscos, plano e decisões D1–D8 em
  `docs/DIAGNOSTICO-FASE0.md`. Resumo das decisões: papéis em tabela ligada ao enum; telefones/e-mails evoluindo
  `PessoaMeiosContato`; endereço com finalidades + tipo parametrizável; campos de documentos no motor atual de campos
  personalizados; carteira em tabela própria `CarteiraClientes`; metas com estrutura agora e realizado do cadastro ou
  informado; anexos em pasta no servidor da API.
- **Fase 2a — Etiquetas: concluída em 25/09/2026** (migração aplicada, 834 testes passando):
  - Cadastro `Etiquetas` (nome único sem maiúsculas/acentos via collation `Latin1_General_CI_AI`, descrição, ativa), permissão
    `CADASTROS.ETIQUETAS`, menu "Etiquetas" com mesclagem (move os cadastros e desativa a origem, com histórico em cada pessoa).
  - `PessoaEtiquetas.EtiquetaId` (FK, índices `(PessoaId, EtiquetaId)` único e `(EtiquetaId, PessoaId)`); a coluna `Texto` fica como
    cópia do dado antigo (propriedade de sombra, não usada).
  - Ficha da pessoa: lista de marcar com busca e atalho "Nova etiqueta"; filtro da lista por `EtiquetaId`. O endpoint
    `pessoas/etiquetas` saiu (substituído por `api/v1/etiquetas`).
  - Migração `CadastroEtiquetas`: gerada pelo usuário e **reordenada à mão** (SQL em `SqlMigracaoEtiquetas.cs`). Se for
    gerada de novo, a ordem das operações precisa ser refeita.

O plano completo está no documento Claude Docs "Plano" (id `B8MD9y5X6U5SctK9tUdfZ5`), na conta antiga. Se ele não estiver acessível na conta nova, este arquivo substitui.

---

## 5. O que foi feito na parte 4 (última entrega)

### 5.1 Municípios (IBGE)
- **Tabela `Municipios`:** Id = código IBGE, Nome, NomeBusca (sem acento, maiúsculas), Uf, CodigoUf, Ativo.
- **Carga:** o serviço em segundo plano `Lone.Api/Servicos/CargaMunicipios.cs` baixa a lista de `https://servicodados.ibge.gov.br/api/v1/localidades/municipios` quando a tabela está vazia.
  - A UF sai dos 2 primeiros dígitos do código.
  - Se falhar, tenta de novo a cada 5 minutos.
  - Pode ser desligado com `Municipios:CarregarAoIniciar=false`.
- **Atualização manual:** `POST api/v1/municipios/atualizar`, com permissão `CADASTROS.TABELAS_OFICIAIS`. Ainda não há botão na tela.
  - Nunca apaga: município que sai da lista é desativado.
  - Proteção: recusa listas com menos de 5.000 municípios.
- **Referências:**
  - `Pessoa.NaturalidadeMunicipioId`, que substituiu `NaturalidadeCidade` e `NaturalidadeUf`.
  - `PessoaEndereco.MunicipioId`, obrigatório no Brasil. Endereço no exterior usa `Cidade` digitada.
- **Textos antigos:**
  - A tabela `PendenciasMunicipio` guarda o texto original.
  - `IndiceMunicipios` concilia pelo código IBGE já gravado, depois por nome + UF, e sem UF só se o nome existir num único estado.
  - O que não resolve fica aberto e aparece na ficha ("Informado antes: …") e no filtro da lista "Município a corrigir".
  - A pendência é resolvida sozinha quando o usuário escolhe o município e salva.
- **Cliente:**
  - `SeletorMunicipio` (Lone.Cliente) lê a lista da UF uma vez e filtra no aparelho. Nome exato e único é escolhido sozinho.
  - Controle `CampoMunicipio` (Lone.App).
  - O CEP e o CNPJ já trazem o código IBGE.

### 5.2 Descartar e alterações não salvas (todas as telas)
- **Detecção:** `CadastroViewModelBase` compara o JSON do DTO da ficha com a "foto" tirada ao abrir ou salvar (`DadosDaFicha()`, `MarcarFichaSemAlteracoes()`).
- **Descartar:**
  - Cadastro novo: fecha e volta para a lista.
  - Cadastro existente: relê do servidor.
  - Sem alterações: só volta.
  - Nunca exclui.
- **Pergunta** "Existem alterações não salvas. Deseja realmente descartá-las?", com as opções Descartar alterações / Continuar editando. Aparece em Fechar, Descartar, Novo, trocar de item na lista e trocar de tela pelo menu (`AppShell.OnNavigating`).
- **`IDialogos`** (Lone.Cliente), com implementação em `DialogosMaui`.
- **Telas:** aplicado em Pessoas, Usuários, Perfis e Campos personalizados.

### 5.3 Campos personalizados
- **Tabelas:**
  - `CamposPersonalizados`: Entidade, Nome (único por cadastro), Tipo, Obrigatório, Ativo, Ordem, Dica, Casas decimais, Mínimo/Máximo.
  - `CampoPersonalizadoOpcoes`.
  - `PessoaValoresPersonalizados`: colunas tipadas `ValorTexto`, `ValorNumero`, `ValorData`, `ValorLogico` e `OpcaoId`, com índice por campo e valor.
- **Tipos (12):** Texto, Texto longo, Inteiro, Decimal, Moeda, Sim/Não, Data, Hora (texto "HH:mm"), Data e hora, Lista, E-mail, Telefone.
  - Cada tipo é uma classe `ITipoCampo` registrada em `Lone.Domain/CamposPersonalizados/TiposCampo.cs`.
  - Um tipo novo precisa só de uma classe e uma linha no registro.
- **Regras:**
  - O tipo trava quando o campo já tem valores.
  - Campo desativado mantém o valor gravado.
  - Opção desativada só vale se já era a gravada.
  - Obrigatório só vale para campo ativo.
- **Telas:**
  - Administração: menu "Campos personalizados", tabela com setas de ordem e ficha.
  - Na pessoa: aba "Informações adicionais".
- **Permissão:** `CADASTROS.CAMPOS_PERSONALIZADOS`.

### 5.4 Situação da pessoa
- **Aba "Situação":** mostra a situação atual, motivo e data. O Picker só alterna entre Ativo e Em análise.
- **Ações próprias:** `POST pessoas/{id}/desativar` e `/reativar`, com versão e motivo opcional.
  - Exigem `PESSOAS.INATIVAR`.
  - Registram o evento "Cliente X foi desativado. Motivo: …".
- **Pelo formulário** não é possível pôr um cadastro como inativo.
- **Inativo** some das buscas, mas continua no filtro "Inativos".

### 5.5 Ficha reorganizada
Ordem das abas: Geral · Dados pessoais (PF) ou Empresa e estabelecimentos (PJ) · Contatos · Endereços · Documentos e Dados fiscais (não PJ) · Cliente · Fornecedor · Informações adicionais · Relacionamento e LGPD · Situação · Histórico.

### 5.6 Testes novos
`MunicipiosTests`, `SituacaoEEventosTests`, `CamposPersonalizadosTests`, `SeletorMunicipioTests`, `CamposEAlteracoesTests` (campos e alterações pendentes) e `IbgeMunicipiosOficiaisTests`. Os testes antigos foram ajustados aos construtores novos (`IDialogos`, `MunicipiosApi`, `CamposPersonalizadosApi`) e à fila do servidor falso: a tela de pessoas agora lê etiquetas, depois campos, depois a lista.

---

## 6. Estado atual e próximos passos imediatos

1. **Compilação:** o único erro relatado (`Math.Clamp` ambíguo em `TiposCampo.cs`) foi corrigido. O usuário compilou com sucesso e gerou a migração.
2. **Migração `20260924231439_MunicipiosECamposPersonalizados`:** gerada e **já ajustada à mão**.
   - No começo do `Up`: `migrationBuilder.Sql(SqlMigracaoMunicipios.GuardarNaturalidade);`
   - No fim do `Up`: `migrationBuilder.Sql(SqlMigracaoMunicipios.CriarPendenciasDeNaturalidade);`
   - O SQL está em `Lone.Infrastructure/Persistencia/SqlMigracaoMunicipios.cs`.
   - **Se a migração for gerada de novo, essas duas linhas precisam ser recolocadas.**
3. **A fazer agora:**
   - Recompilar, rodar todos os testes e iniciar a API. Ela deve logar "Tabela de municípios carregada do IBGE…".
   - Testar no app:
     - Naturalidade: UF MG + "Jura".
     - CEP preenchendo o município.
     - Criar um campo personalizado e ver a aba na pessoa.
     - Desativar e reativar um cadastro e conferir o histórico.
     - Descartar e Fechar com alterações.
     - Repetir no Android.
4. **Depois disso:**
   - Atualizar o plano: marcar a M4 parte 4.
   - Validar o critério de fechamento da M4.
   - Seguir para a M5.

### Pontos de atenção
- **Validações que agora bloqueiam a gravação:** endereço no Brasil sem município escolhido impede salvar. Cadastros antigos que não foram conciliados precisarão da escolha manual. É intencional.
- **Versão dos campos:** reordenar muda a versão dos campos. A tela relê a ficha aberta para evitar conflito.
- **Carga do IBGE:** sem internet no servidor, a escolha de município fica indisponível até a carga, e a tela explica isso.
- **Ideias levantadas e ainda não feitas:**
  - Botão na interface para "Atualizar municípios".
  - Filtros e relatórios por valor de campo personalizado (os índices já existem).
  - Campos personalizados para outros cadastros (o enum `EntidadePersonalizavel` já prevê).

---

## 7. Histórico de problemas já resolvidos (para não repetir)

| Problema | Solução |
|---|---|
| Falta de CommunityToolkit.Mvvm no Directory.Packages.props | Pacote adicionado |
| DLLs bloqueadas ao compilar | Fechar o app antes de compilar |
| App não conecta na API | Confiar no certificado HTTPS de desenvolvimento e deixar a API rodando |
| Emulador Android travando (Vulkan da Intel) | `Vulkan=off` no `advancedFeatures.ini` |
| Erro 500 ao salvar perfil (chave de auditoria com mais de 40 caracteres) | `ColetorAuditoria.Chave` tira o id da raiz das chaves compostas |
| API não inicia com "não é possível conectar" | Havia migração pendente: rodar `Add-Migration` |
| Conflitos de nome em C# | Aliases (`CepValor`, `DocumentoFiscal`) e `global::` |
| Picker do MAUI | Precisa de `IList`: as listas de opções são arrays |

## 8. Terceiros
A validação de inscrição estadual foi portada do Caelum Stella (Apache 2.0) e está registrada em `THIRD-PARTY-NOTICES.md`. As listas públicas do IBGE, da BrasilAPI, do ViaCEP e do CNPJ.ws são usadas via HTTP, sem chave.
