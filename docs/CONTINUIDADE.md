# Lone ERP — documento de continuidade

Situação em 26/09/2026 (noite; passagem de conta em `docs/PASSAGEM-DE-CONTA.md`). Serve para quem continuar o trabalho, seja outra conta ou outro assistente.
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
- **Fase 2b — Profissões e CBO: concluída em 25/09/2026** (migração aplicada, testes passando, testado no app):
  - Cadastro `Profissoes` (nome único CI_AI, descrição, ocupação CBO opcional, ativa), permissão `CADASTROS.PROFISSOES`,
    menu "Profissões" com mesclagem (move as pessoas e desativa a origem; histórico em cada pessoa).
  - Tabela oficial `OcupacoesCbo` (Id = código CBO de 6 dígitos, mesma regra dos municípios). Sem API pública estável:
    importa o arquivo "CBO2002 - Ocupacao.csv" do site da CBO pelo botão "Importar CBO" (permissão `CADASTROS.TABELAS_OFICIAIS`;
    recusa arquivo com menos de 2.000 ocupações; nunca apaga, desativa).
  - `Pessoas.ProfissaoId` (FK); a coluna `Profissao` (texto) fica como cópia do dado antigo (propriedade de sombra).
  - Ficha da pessoa: autocompletar (`SeletorDeLista` + controle `CampoLista`, reutilizáveis) e atalho "Nova profissão".
  - Decisão do usuário: cadastros auxiliares **um por um** (sem base comum); o `SeletorDeLista` é só um componente de tela.
  - Migração `CadastroProfissoes`: no fim do `Up`, `migrationBuilder.Sql(SqlMigracaoProfissoes.CriarProfissoesELigarPessoas);`.
- **Fase 2c — Papéis: concluída em 25/09/2026** (migração aplicada, testes passando, testado no app):
  - Cadastro `Papeis` (código imutável, nome único CI_AI, descrição, ordem, ativo, `PapelSistema` ligado ao enum `TipoPapel`).
    Os 8 de sistema nascem pela migração com Ids fixos (`PapeisSistema`); Cliente, Fornecedor, Empresa do grupo e Funcionário
    não podem ser desativados (regras no código). Permissão `CADASTROS.PAPEIS`, menu "Papéis".
  - `PessoaPapeis.PapelId` (FK); `Papel` (enum) virou cópia do papel de sistema, sobrescrita pelo cadastro ao gravar (o aplicativo
    não consegue se promover a empresa do grupo). Índice único só entre os ativos: **vários períodos do mesmo papel**.
  - Períodos de papel **nunca são apagados** (`SincronizarFilhos(..., apagarAusentes: false)`): desmarcar encerra, marcar de novo
    começa outro período. Início/encerramento viram frase no histórico da pessoa.
  - Filtro da lista por `papelId`; a lista mostra os nomes do cadastro.
  - Migração `CadastroPapeis`: gerada pelo usuário e **reordenada à mão** (SQL em `SqlMigracaoPapeis.cs`).
- **Fase 3a — Telefones e e-mails (código entregue em 25/09/2026, aguardando migração e testes):**
  - Decisões: DDD dentro do número (coluna calculada `Ddd`, persistida e indexada, só para filtro); WhatsApp virou
    marcação (tipo WhatsApp antigo → Celular + WhatsApp); remover desativa e esconde ("Mostrar inativos" + "Reativar").
  - `PessoaMeiosContato` ganhou `TipoMeioContatoId` (classificação), `Ramal`, `WhatsApp`, `Sms`, `Finalidades`
    (financeiro, cobrança, NF-e, marketing) e `Ativo`. Nunca apagados (`apagarAusentes: false`).
  - Cadastro `TiposMeioContato` (categoria telefone/e-mail, nome único por categoria, ordem, ativo; 5 iniciais com Ids fixos).
    Permissão `CADASTROS.TIPOS` (servirá também para tipos de endereço e de documento). Menu "Tipos de telefone/e-mail".
  - Migração `MeiosContatoETipos`: no fim do `Up`, `migrationBuilder.Sql(SqlMigracaoMeiosContato.AtivarEConverterWhatsApp);`.
- **Fase 3b — Endereços (código entregue em 25/09/2026; sem teste por fase — tudo será testado no fim):**
  - D3: finalidades mantidas; `PessoaEnderecos` ganhou `TipoEnderecoId` (FK, cadastro parametrizável), `Observacoes` (250) e `Ativo`.
  - Cadastro `TiposEndereco` (nome único CI_AI, ordem, ativo; Sede, Filial, Depósito, Residência com Ids fixos). Permissão
    `CADASTROS.TIPOS`, menu "Tipos de endereço".
  - Endereços nunca apagados (`apagarAusentes: false`): remover um gravado desativa (perde "principal", fica fora da lista;
    "Mostrar inativos" + "Reativar"). Inativo não é conferido (município antigo não trava) e não pode ser o endereço fiscal
    de uma filial. Pendência de município de endereço inativo é considerada resolvida.
  - **Migração única final** (gerada pelo usuário depois de todas as fases): logo depois da criação da coluna
    `PessoaEnderecos.Ativo`, inserir `migrationBuilder.Sql(SqlMigracaoCadastroGeral.AtivarEnderecos);`.
  - Tradução dos Ids novos no histórico (tipo de telefone, tipo de endereço) fica para a fase 6.
- **Fase 4a — Documentos parametrizáveis (código entregue em 25/09/2026; sem teste por fase):**
  - Cadastro `TiposDocumento` (entidade `TipoDocumentoCadastro`, porque `TipoDocumento` é o enum): nome único CI_AI, ordem,
    ativo, `TipoSistema` (ligado ao enum; os 5 de sistema com Ids fixos `7a9e1c03-...-01..04, 09`), `ExigeValidade` e
    `DiasAvisoVencimento` (padrão 30). Permissão `CADASTROS.TIPOS`, menu "Tipos de documento".
  - `PessoaDocumentos` ganhou `TipoDocumentoId` (FK) e `Ativo`; o enum `Tipo` continua (cópia feita pela API a partir do tipo;
    chamada antiga só com o enum é ligada ao tipo de sistema). Vários documentos por pessoa; nunca apagados (desativa).
  - Situação da validade calculada (`RegrasDocumento.Situacao`: sem validade / válido / vence em breve / vencido); a ficha
    mostra o aviso por documento e o resumo na aba. Índice `(TipoDocumentoId, ValidoAte)` filtrado por ativos para as
    consultas de vencimento (filtro da lista entra na fase 12).
  - **Migração única final:** `migrationBuilder.Sql(SqlMigracaoCadastroGeral.LigarDocumentosAosTipos);` depois de criar
    `TiposDocumento` + dados de sistema e as colunas novas de `PessoaDocumentos`, e **antes** da FK
    `FK_PessoaDocumentos_TiposDocumento_TipoDocumentoId` (a coluna nasce com Guid vazio).
- **Fase 4b — Anexos de documentos (D7; código entregue em 25/09/2026; sem teste por fase):**
  - Tabela `AnexosDocumento` (PessoaId, PessoaDocumentoId, nome, tipo, tamanho, SHA-256, caminho, enviado por, ativo);
    faz parte do histórico da pessoa. Conteúdo numa pasta do servidor da API: `{Anexos:Pasta}/{ano}/{mês}/{id}.bin`
    (padrão `C:\ProgramData\Lone\Anexos`); caminho gerado só com o Id, nunca com o nome enviado.
  - Aceita PDF, JPG e PNG conferidos pelo conteúdo (assinatura), extensão coerente, até `Anexos:TamanhoMaximoMb` (10).
    Integridade conferida pelo hash ao baixar. Nada é apagado: remover desativa; o arquivo fica.
  - Rotas: `POST pessoas/{id}/documentos/{docId}/anexos` (JSON base64), `GET anexos/{id}/conteudo`,
    `POST anexos/{id}/desativar|reativar`. Enviar/remover = `PESSOAS.EDITAR`; baixar = `PESSOAS.VISUALIZAR`.
  - Envio é imediato (não depende do Salvar); só documento já gravado e ativo recebe anexo. A ficha traz os anexos em
    `DocumentoDto.Anexos` (somente leitura). O aplicativo abre o arquivo com o programa padrão do aparelho.
  - **Backup:** a pasta de anexos precisa entrar no backup junto com o banco.
- **Fase 5 — Campos personalizados de documentos (D4; código entregue em 25/09/2026; sem teste por fase):**
  - Mesmo motor (ITipoCampo, colunas tipadas): base `ValorPersonalizado` com `PessoaValorPersonalizado` (tabela antiga) e
    `DocumentoValorPersonalizado` (tabela nova `PessoaDocumentoValoresPersonalizados`, com `PessoaDocumentoId`).
  - `CamposPersonalizados` ganhou `TipoDocumentoId` (escopo `Documento`), `Visivel` e `Pesquisavel`; nome único por
    (cadastro, tipo de documento). Tipos novos CPF e CNPJ (guardados sem máscara, dígitos conferidos; CNPJ alfanumérico).
  - Oculto: não aparece na ficha e mantém o gravado; não pode ser obrigatório. Pesquisável (só tipos de texto): entra na
    busca da lista pelo começo do texto, com índice `(CampoId, ValorTextoBusca)` — coluna calculada persistida com os
    200 primeiros caracteres (o texto inteiro passa do limite de chave de índice). A busca também acha número de documento.
  - Documento que muda de tipo descarta os valores dos campos do tipo antigo; documento inativo mantém os gravados.
  - Tela "Campos personalizados" com escolha "Pessoas / Documentos" e o tipo de documento na ficha do campo.
  - **Migração única final:** `migrationBuilder.Sql(SqlMigracaoCadastroGeral.CamposVisiveis);` depois de criar
    `CamposPersonalizados.Visivel`.
- **Fase 6 — Auditoria (código entregue em 25/09/2026; sem teste por fase):**
  - Coluna `Auditoria.Motivo` (250): o serviço define `IMotivoDaOperacao.Motivo` (na API, o próprio usuário da requisição)
    e a gravação copia para todas as linhas da operação. Ficha de pessoa: campo "Motivo da alteração (opcional)" ao lado
    do Salvar (`PessoaDto.MotivoAlteracao`, só no envio); desativar/reativar também gravam o motivo na coluna.
  - Histórico paginado por chave (`?antes={último Id}&limite=`, página de 100, máximo 500) com "Carregar mais antigos";
    `RegistroHistorico` traz `Id` e `Motivo`.
  - Tradução de Ids no histórico: tipo de telefone/e-mail, tipo de endereço, tipo de documento (documento e campo),
    papel e documento do anexo/valor de campo ("CNH 123").
- **Fase 7 — Colaborador (código entregue em 25/09/2026; sem teste por fase; decisões no DIAGNÓSTICO, fim do arquivo):**
  - Cadastros: `Cargos` (com CBO opcional), `Departamentos`, `Setores` (dentro do departamento), `CentrosCusto` (árvore
    pai/filho, código único, analítico/sintético; só analítico recebe colaborador). Permissão
    `CADASTROS.ESTRUTURA_ORGANIZACIONAL`; menus "Cargos", "Departamentos", "Setores", "Centros de custo".
  - Ficha: `VinculosColaborador` (empresa do grupo, matrícula única por empresa, tipo, admissão, desligamento, motivo,
    jornada) e `LotacoesColaborador` (início/fim, cargo, departamento, setor, centro de custo, gestor). Nunca apagados.
    "Nova lotação" encerra a atual na véspera; desligamento encerra a lotação aberta. Admissão/desligamento viram frase.
  - Permissão `PESSOAS.COLABORADOR` para ver/alterar (sem ela: aba some, API mantém os dados). Opções da aba numa
    chamada só (`GET colaboradores/opcoes`), lida quando a aba abre.
  - Migração final: só tabelas novas (nenhum SQL de dados).
- **Fase 8 — Comercial (código entregue em 25/09/2026; sem teste por fase; decisões no DIAGNÓSTICO):**
  - Cadastros: `CondicoesPagamento` (parcelas em dias "0/30/60", acréscimo/desconto %, prazo médio), `PerfisComerciais`
    (limite, desconto, dias de atraso, condição, aprovação), `TiposCarteira` (um principal; Vendedor, Representante,
    Televendas, Supervisor com Ids fixos). Permissão `CADASTROS.COMERCIAL`; menus correspondentes.
  - Conta do cliente: `PerfilComercialId` e `CondicaoPagamentoId` (o texto antigo da condição fica guardado).
    `ExcecoesComerciais` (por campo, com vigência, sem sobreposição por empresa) e `CarteiraClientes` (D5: tipo,
    vendedor, empresa, início/fim, exclusivo, observação, ativo). Nada é apagado.
  - Vale: exceção vigente → perfil → conta (`RegrasComercial.Efetivos`; a aba Cliente mostra o resumo "em vigor hoje").
  - D5: ao gravar, o vendedor principal vigente vira `ContaCliente.VendedorPadraoId` (sem principal vigente, fica como
    estava). Mudar perfil/exceções exige `PESSOAS.ALTERAR_CREDITO`. Vendedor precisa do papel Vendedor/Representante.
  - **Migração única final:** `migrationBuilder.Sql(SqlMigracaoCadastroGeral.CarteiraDosVendedoresPadrao);` depois de
    criar `CarteiraClientes` e inserir `TiposCarteira` (cria o vínculo principal para cada vendedor padrão existente).
  - Pendente para depois: rotina diária que atualiza o vendedor padrão quando um período vence sem a ficha ser salva.
- **Fase 9 — Fiscal (código entregue em 25/09/2026; sem teste por fase):**
  - `HistoricoFiscal` por estabelecimento (regime, contribuinte do ICMS, IE, situação na Receita, produtor rural) com
    vigência, mantido pela API ao gravar (mudança encerra o período na véspera; duas no mesmo dia corrigem o de hoje).
    A ficha mostra o histórico por estabelecimento. `RegrasFiscal.Vigente` responde a situação numa data.
  - `Estabelecimentos.ProdutorRural`. Tabela `Cnaes` (CNAE 2.3 do IBGE, carregada em segundo plano como os municípios;
    `Cnaes:CarregarAoIniciar`; `POST cnaes/atualizar` com `CADASTROS.TABELAS_OFICIAIS`; busca `GET cnaes?texto=`).
  - `EstabelecimentoCnaes`: cópia em tabela dos campos de texto (principal + secundários), para filtros com índice.
  - **Migração única final:** `migrationBuilder.Sql(SqlMigracaoCadastroGeral.HistoricoFiscalECnaes);` depois de criar
    `HistoricoFiscal`, `EstabelecimentoCnaes` e a coluna `Estabelecimentos.ProdutorRural`.
- **Fase 10 — Situações (código entregue em 25/09/2026; sem teste por fase):**
  - Bloqueios como ação própria: `POST pessoas/{id}/bloqueios` e `.../bloqueios/{id}/liberar` (motivo obrigatório,
    quem/quando; motivo também na coluna de auditoria). Escopo novo `Faturamento`. Permissões `PESSOAS.BLOQUEAR` e
    `PESSOAS.DESBLOQUEAR`. Aba Situação lista bloqueios (ativos primeiro) com Bloquear/Liberar.
  - Relacionamento: tabela `Interacoes` (só inclusão; ligação, visita, e-mail, WhatsApp, reunião, outro;
    `PESSOAS.INTERACOES`), situação calculada (sem interação / ativo / em risco / inativo) por
    `ParametrosRelacionamento` (linha única, padrão 90 e 180 dias; `GET/PUT pessoas/parametros-relacionamento` com
    `CADASTROS.PARAMETROS`). A ficha traz `Relacionamento` (última interação, situação, 50 mais recentes).
  - Pendente: tela para editar os parâmetros de inatividade (hoje só pela API); "última compra" quando houver vendas.
  - Migração final: tabelas novas + dado inicial de `ParametrosRelacionamento` (HasData); nenhum SQL de dados.
- **Fase 11 — Motor de metas (código entregue em 25/09/2026; sem teste por fase; decisões no DIAGNÓSTICO):**
  - Cadastros: `Equipes` (+ `MembrosEquipe` com entrada/saída, nunca apagados) e `Indicadores` (código e fonte
    imutáveis; 4 de sistema com Ids fixos 7a9e1c06-…-01..04 via HasData: novos clientes, clientes ativos, reativados,
    interações; os criados pelo usuário são "Informado"). Permissões `METAS.VISUALIZAR`, `METAS.GERENCIAR`,
    `METAS.LANCAR_REALIZADO`, `METAS.FECHAR`; menus "Metas", "Indicadores", "Equipes".
  - `Metas` + `MetaItens` (peso, soma 100), `MetaFaixas` (a partir de %, nome, % de prêmio), `MetaParticipantes`
    (Empresa/Filial/Departamento/Equipe/Colaborador; sem FK na referência) e `MetaAlvos` (alvo, realizado, origem).
  - Fluxo: rascunho (estrutura editável) → publicada (lança realizado informado; volta a rascunho só sem realizado)
    → em apuração → fechada (aprovação `METAS.FECHAR`: congela realizado calculado, nota, faixa e prêmio). Reabrir
    exige `METAS.FECHAR` + motivo (vai para a coluna de auditoria) e limpa só o calculado congelado. Rascunho pode
    ser cancelado (desativado). Nota = Σ peso × atingimento (teto por item, padrão 150%; "menor melhor" = alvo/realizado).
  - Realizado: manual na ficha ou CSV `participante;valor` (conferir → gravar; nome sem acento/maiúsculas).
  - Realizado calculado (`FonteIndicadoresCadastro`, contado no banco): participante → vendedores (colaborador; membros
    da equipe; lotações do departamento; vínculos da empresa) no período → clientes da carteira deles → períodos do
    papel Cliente / interações. **Filial usa a empresa da filial** até a lotação ter estabelecimento.
  - Migração final: tabelas novas + HasData dos indicadores de sistema; nenhum SQL de dados.
  - Pendente: fontes de vendas/faturamento quando o módulo de vendas existir; ligação do prêmio com comissões.
- **Fase 12 — Consulta avançada de pessoas (código entregue em 25/09/2026; sem teste por fase):**
  - Tela própria "Consulta avançada" (menu, `PESSOAS.VISUALIZAR`). Critérios tipados (`CriteriosPessoas`): texto,
    natureza, situação, papéis, etiquetas, UF/município, CNAE (código ou começo, só principal), regime, produtor rural,
    carteira de hoje (vendedor ou sem carteira), relacionamento (mesma regra dos parâmetros), sem interação há N dias,
    bloqueio ativo, documentos vencidos / vencendo em N dias, campo personalizado pesquisável, período de cadastro.
    Cada critério vira um Where do LINQ (sem SQL dinâmico). `POST pessoas/consulta` (critérios no corpo, não na URL).
  - Paginação por chave (Nome + Id, "Carregar mais"); total contado só na primeira página. A lista de pessoas
    (`PessoaRepositorio.Resumir`) e a busca por texto (`AplicarBusca`) são as mesmas da tela de cadastro.
  - Filtros salvos (`FiltrosSalvos`, critérios em JSON de tipo fechado): do usuário, opção "compartilhar com todos",
    só o autor altera/remove (desativa).
  - Exportar CSV (`;`, UTF-8 com BOM, fórmulas neutralizadas) com permissão `PESSOAS.EXPORTAR`, até 50.000 linhas;
    cada exportação vira evento na auditoria (Entidade `ConsultaPessoas`, critérios na descrição).
  - Índices novos: `PessoaDocumentos (ValidoAte, PessoaId)` filtrado, `PessoaEnderecos (Uf, MunicipioId)`, `Pessoas (CriadoEm)`.
  - Migração final: tabela `FiltrosSalvos` + índices; nenhum SQL de dados.
  - Pendente: abrir a ficha a partir do resultado (hoje a consulta só lista); várias escolhas por lista na tela (a API já aceita).
    A ordem é por `Nome` (índice); a linha mostra o nome de exibição — quem tem nome de exibição/social pode parecer fora de ordem.
- **Fase 13 — Revisão (25/09/2026, por leitura; sem compilar):** corrigidos: propriedade duplicada `Situacoes` na ficha
  (a lista de situação de uso virou `SituacoesDeUso`), `using` faltando no `SituacaoRepositorio`, validação da primeira
  lotação de vínculo novo (vazio = começa na admissão), FK de `HistoricoFiscal` para estabelecimento removida (remover
  estabelecimento da ficha quebrava no banco; histórico fica), índice único dos campos personalizados sem filtro,
  filiais nas metas (filtro antes da projeção), contagens das metas pelos períodos (encerrado tem Ativo = falso),
  limites de texto das metas validados, permissão conferida antes na mudança de situação da meta.
- **Revisão de Pessoas — endereço × finalidade (26/09/2026):** ver `docs/REVISAO-PESSOAS.md` §11. **Migração nova** (depois
  de `CadastroGeral`): `Add-Migration FinalidadesEndereco -Project Lone.Infrastructure -StartupProject Lone.Api`, depois
  `python Ferramentas/inserir-sql-migracao.py` (coloca `MigrarFinalidades`, `CriarProtecoes` no fim do Up e
  `RemoverProtecoes` no começo do Down). Correções pós-auditoria: `docs/REVISAO-PESSOAS.md` §11.1.
- **Estrutura empresarial (26/09/2026; código entregue, sem compilar — ambiente sem SDK .NET):**
  - Decisões do usuário: **Grupo empresarial** é entidade nova (`GruposEmpresariais` + `Pessoas.GrupoEmpresarialId`), só para
    pessoa jurídica (regra no validador + CHECK `CK_Pessoas_GrupoEmpresarialSoPJ`); **GrupoEconomico fica intacto** (reservado,
    sem migração nem ligação). Pessoa física participa pelos relacionamentos. `PessoaPapel` continua global (sem EmpresaId);
    contextos por empresa já existem em `ContasCliente`/`ContasFornecedor.EmpresaId` e `VinculosColaborador`.
  - **Nome para exibir** (só cabeçalho e lista de Pessoas): nome de exibição → (PJ) nome fantasia do principal → nome social →
    nome. Regra única em `Lone.Domain/Pessoas/NomePessoa`; a lista usa `PessoaRepositorio.NomeParaExibirNoBanco` (mesma
    ordem, conferida por teste). Consulta avançada, carteira, metas e colaboradores continuam como antes.
  - **Relacionamentos entre pessoas** (`PessoaRelacionamento` reaproveitado): coluna `Ativo`, índice único do vínculo em aberto,
    tipos novos de sistema **Administrador de** e **Parceiro de** (HasData). Operações próprias (fora do Salvar):
    `GET/POST pessoas/{id}/relacionamentos`, `.../{relId}/encerrar`, `.../{relId}/desativar`, `GET pessoas/estrutura/opcoes`.
    Sócio/administrador exigem `PESSOAS.ESTRUTURA_EMPRESARIAL` e destino que não seja pessoa física. Aba "Relacionamentos".
  - **Estabelecimento nunca é apagado:** a gravação recebe todos os gravados (faltou = 400); "Remover" filial gravada desativa;
    Frases no histórico: incluído, desativado, reativado, troca do principal.
  - **PJ gravada não vira PF/estrangeiro** (`Lone.Domain/Pessoas/RegrasNaturezaPessoa`, na API e na ficha): a recusa lista o que
    seria perdido (CNPJs, fantasia, grupo, vínculos societários, sócios da Receita). Cadastro novo e as demais trocas seguem livres.
  - **Estabelecimento inativo:** continua validado quanto a formato e integridade (CNPJ e raiz, IE, SUFRAMA, CNAE, endereço fiscal
    da própria pessoa); só deixam de valer para ele "contribuinte precisa de IE" e "endereço fiscal inativo".
  - Limitações conhecidas: histórico do relacionamento só na auditoria da pessoa de origem; relacionamento desativado não se
    reativa; ordenação da lista pelo nome de exibição usa subconsulta sem índice; filiais apagadas antes desta versão não voltam.
  - **Condição de pagamento do fornecedor:** `ContasFornecedor.CondicaoPagamentoId` (FK); o texto antigo fica guardado e a
    ficha mostra "não convertida" quando não ligou.
  - Ficha: cabeçalho (nome, razão social, tipo e papéis, documento, código, situação, grupo e estabelecimentos, etiquetas);
    abas **Comercial** (Cliente + Fornecedor) e **Relacionamentos**; "Relacionamento e LGPD" virou **"Interações e LGPD"**.
    Menu "Grupos empresariais" (`CADASTROS.GRUPOS_EMPRESARIAIS`).
    Aba Geral da PJ: Razão social, **Nome fantasia (opcional)** (o mesmo do estabelecimento principal) e **Nome de exibição
    (opcional)** com ajuda fixa conforme a natureza (`PessoaFormulario.AjudaNomeExibicao`). Só tela; banco inalterado.
  - **Migração (gerar no PMC):** `Add-Migration EstruturaEmpresarial -Project Lone.Infrastructure -StartupProject Lone.Api
    -OutputDir Persistencia/Migracoes` e, **no fim do Up**, `migrationBuilder.Sql(SqlMigracaoEstruturaEmpresarial.Dados);`
    (ativa os relacionamentos existentes e liga condições em texto de mesmo nome; THROW 50040–50041 se algo não conferir).
    Validada em 26/09/2026: migração gerada e auditada (sem operação destrutiva no Up), compilação sem avisos, 1141 testes
    aprovados (0 falhas, 0 pulados, com SQL Server).
    Depois: teste instável de endereços corrigido (`FinalidadesEnderecoTests`, empate de ordem decidido por Guid aleatório;
    só o teste mudou) e teste novo da aba Geral (1142 no total). Windows e Android validados manualmente.
    Depois, no próprio PMC: `& .\Ferramentas\validar-estrutura-empresarial.ps1` (roda `auditar-migracao-estrutura-empresarial.ps1`,
    que confere as operações esperadas, recusa qualquer Drop/Rename/AlterColumn/Delete e insere a linha do Sql; compila; roda
    todos os testes com Total/Passed/Failed/Skipped; imprime o roteiro manual de Windows e Android).
- **Consolidação da ficha de Pessoas — Fase 1 (26/09/2026; código entregue, sem compilar — ambiente sem SDK .NET):**
  plano completo em `_entrega/PLANO-CONSOLIDACAO-PESSOAS.md` (5 fases, nenhuma com migration). Só App + Cliente + testes.
  - Aba **Geral → "Identificação"**; recebe as **Etiquetas** (saíram de "Interações e LGPD") e, na PJ, o bloco
    **Dados da empresa** (abertura, porte, capital, natureza jurídica do principal) e o **Grupo empresarial** (saíram da aba
    da empresa). As opções de grupo agora são lidas quando a Identificação abre numa PJ (ou quando a ficha vira PJ nela);
    Identificação e Relacionamentos compartilham a mesma leitura em andamento (`CarregarOpcoesEstruturaAsync`).
  - Aba da PJ renomeada para **"Estabelecimentos"** (sócios da Receita + matriz e filiais). CNPJ, nome fantasia e natureza
    jurídica do principal são editados **só na Identificação**; no cartão do principal aparecem só leitura
    (`EstabelecimentoFormulario.PrincipalDaPJ`). Filiais continuam editando os seus.
  - **Documentos** aparece para todas as naturezas, com o resumo do documento principal ("altere na Identificação").
    Tipos como RG aparecem também para PJ (filtro por natureza exigiria coluna nova — não feito).
  - **Contatos**: listas **Telefones** e **E-mails** (mesma coleção `MeiosContato`; `NaListaTelefones`/`NaListaEmails`,
    comandos `AdicionarTelefone`/`AdicionarEmail`, primeiro de cada tipo vira principal); pessoas de contato com a
    explicação de quando usar Relacionamentos. Nada muda no que é gravado.
  - PF: "Dados pessoais" continua aba própria (recomendação do plano). "Aceita comunicações"/"Marketing" continuam onde
    estavam até a Fase 3 (Privacidade), que **depende de decisão do usuário** sobre a precedência (plano, seção 9).
  - Testes: `PessoasViewModelTests` (abas ajustadas; grupos lidos uma vez na Identificação; duas listas de contatos).
  - A conferir pelo usuário: compilar, rodar os testes, e no Windows/Android os critérios da seção 10 do plano.
- **Consolidação de Pessoas — Fase 2 (26/09/2026; código entregue, sem compilar):**
  - Aba fiscal única, **depois de Documentos** para todas as naturezas: **"Fiscal e estabelecimentos"** (PJ: matriz, filiais e
    dados fiscais de cada CNPJ) e **"Fiscal"** (PF/estrangeiro). A PJ não tem mais aba de empresa logo após a Identificação.
  - **Sócios da Receita** foram para a Identificação (bloco Dados da empresa, só leitura).
  - Cliente: o texto antigo da condição de pagamento deixou de ser campo editável; aparece só leitura com
    "não convertida" / "guardada", igual ao fornecedor (`ContaClienteFormulario.TextoCondicaoAnterior`). O texto continua
    sendo gravado como veio.
  - Colaborador: revisão de textos (dica do desligamento).
  - Testes: `EstruturaEmpresarialFormularioTests` (condição do cliente, ordem/nome da aba fiscal), `PessoasViewModelTests` ajustado.
- **Consolidação de Pessoas — Fase 5 (26/09/2026; código entregue, sem compilar):** menu lateral só com a operação
  (Início, Pessoas, Consulta avançada, Grupos empresariais, Metas, **Configurações**). Página **Configurações**
  (`ConfiguracoesViewModel` + `ConfiguracoesPage`) com os cadastros de apoio em grupos (Pessoas, Comercial, Organização,
  Metas, Sistema), em ordem alfabética, cada um com a mesma permissão de antes. As telas continuam registradas no
  `AppShell` com as mesmas rotas, mas com `Shell.FlyoutItemIsVisible="False"`; a página abre com `GoToAsync("//rota")`
  (a pergunta de alterações não salvas continua valendo). Para voltar, usa-se "Configurações" no menu.
  Testes: `ConfiguracoesViewModelTests`. Fase 4 (Situação, Informações adicionais, Histórico): conferidas, sem mudança.
  **Fase 3 (Privacidade) pendente de decisão do usuário** sobre a precedência entre consentimento LGPD, "Aceita
  comunicações" do meio e "Marketing" do e-mail.
- **Fase 3 — Privacidade e LGPD (26/09/2026; commit `097ed98`, compilada e testada, migration aplicada no desenvolvimento):** ver `docs/FASE3-PRIVACIDADE.md`
  (e a auditoria em `docs/FASE3-PRIVACIDADE-AUDITORIA.md`). Consentimento por finalidade em períodos (canal opcional),
  cadastro `FinalidadesTratamento` (Marketing; "Registro anterior" só histórico), regra central
  `RegrasComunicacao.PodeComunicar`, ações próprias de conceder/revogar (`PESSOAS.PRIVACIDADE`), abas Interações + Privacidade.
  **Migração (gerar no PMC):** `Add-Migration PrivacidadeConsentimentos -Project Lone.Infrastructure -StartupProject Lone.Api
  -OutputDir Persistencia/Migracoes`, depois `python Ferramentas/inserir-sql-privacidade.py` (SQL dos consentimentos antigos
  antes da FK). Não aplicar em produção antes de revisar.
- **Redesenho do módulo Pessoas (26/09/2026; commit `49b9f96`; build 0 avisos/0 erros; 1181 testes aprovados, 0 falhas, 22 ignorados sem SQL Server; falta conferir no app):** ver `docs/UX-ARQUITETURA.md`. Menu por módulo
  (FlyoutContent), configurações por módulo (Pessoas, Organização, Metas, Sistema), lista de Pessoas em tabela paginada
  (`GET pessoas/pagina`), ficha em tela cheia com cabeçalho novo e abas com indicador, design system em Cores/Estilos.
  Sem mudança de regra, banco ou migration.
- **Ajustes pós-redesenho (26/09/2026, noite; código entregue, sem compilar — ambiente sem SDK .NET; sem commit):**
  - Configurações de cada módulo voltaram aos **cartões separados** agrupados por título (cabeçalho novo mantido).
  - **Menu em dois níveis** (módulos abrem e fecham), **busca no menu**, **favoritos** (☆/★) e **recentes**, guardados no
    servidor por usuário. Detalhes em `docs/UX-ARQUITETURA.md`.
  - **Migration nova (gerar no PMC):** `Add-Migration MenuFavoritosRecentes -Project Lone.Infrastructure -StartupProject Lone.Api
    -OutputDir Persistencia/Migracoes`. Só cria a tabela `PreferenciasMenu` (FK para `Usuarios`, índice único
    `IX_PreferenciasMenu_UsuarioId_Rota`); nenhum SQL de dados, nada é alterado nas tabelas existentes.
  - **Defeito corrigido:** abrir a ficha de qualquer PJ fechava o app no Windows (COMException 0x80004005 no Measure do
    cabeçalho). Elemento confirmado por diagnóstico (desligá-lo resolveu): os selos de papéis — uma lista (`FlexLayout` +
    `BindableLayout`) dentro de outra `FlexLayout` que quebra linha. Tornar a coleção fixa (sincronizada no lugar) NÃO
    bastou; a linha virou texto simples (`TipoEPapeisCabecalho`, "Pessoa jurídica ·
    Cliente · Fornecedor"). Evitar lista com `BindableLayout` dentro de `FlexLayout` no cabeçalho da ficha.
  - **Papéis e Etiquetas na Identificação** (só interface e ViewModel; sem banco, sem migração, sem mudança de regra): a ficha
    mostra só o que a pessoa tem; "+ Adicionar" abre painel com pesquisa; papéis em cartões com interruptor (vale ao salvar)
    e histórico dos períodos; etiquetas em chips. Padrão registrado em `docs/UX-ARQUITETURA.md`.
  - API: `GET api/v1/menu/preferencias`, `PUT api/v1/menu/favoritos`, `POST api/v1/menu/acessos` (só login; cada usuário
    mexe nos próprios dados).
- **Caixas de marcar e barra de título (26/09/2026, noite; sem commit até o usuário testar):** caixas de marcar compactas em
  todo o app (`Plataforma/AjusteCaixaMarcar`, espaço `CaixaMarcar.EspacoTexto`; Usuários e Perfis passaram a usar
  `CaixaMarcar`). Empresa e usuário na barra de título do Windows; Trocar senha em Configurações do sistema › Minha conta;
  menu do usuário com Trocar de usuário / Sair do Lone. Ver `docs/UX-ARQUITETURA.md`. Sem banco.
- **Testes no SQL Server (opcionais):** defina `LONE_TESTES_SQLSERVER` (ex.: `Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True`);
  sem ela os testes de banco aparecem como pulados.
- **Passo final (do usuário):** gerar **uma** migração depois de `MeiosContatoETipos` e inserir, nos pontos indicados
  acima, os SQL de `SqlMigracaoCadastroGeral`: `AtivarEnderecos`, `LigarDocumentosAosTipos` (antes da FK dos
  documentos para `TiposDocumento`), `CamposVisiveis`, `CarteiraDosVendedoresPadrao` (depois do insert de
  `TiposCarteira`), `HistoricoFiscalECnaes`. Depois: compilar, rodar todos os testes e testar o app.

- **Aba Fiscal da pessoa física e do estrangeiro (27/09/2026; código entregue, sem compilar; sem mudança no banco):**
  - PF: "Produtor rural" no topo. Sem produtor, a aba mostra só o resumo "Não contribuinte do ICMS · Consumidor final
    (padrão nas vendas)"; com produtor, Indicador de IE (sem "Não informado"; marcar sugere "Contribuinte") e IE.
    Inscrição municipal por "Adicionar inscrição municipal". Regime, CNAE e SUFRAMA não aparecem (são de empresa).
    Estrangeiro: só o resumo. O título "Estabelecimento principal" aparece só na PJ.
  - Dados antigos: PF/estrangeiro com regime, CNAE ou SUFRAMA gravados vê o bloco "Dados que não se aplicam..." (leitura)
    com "Remover estes dados" (vale ao salvar). IE gravada sem produtor rural continua à vista. Carregar não altera nada.
  - Servidor: `PessoaNormalizador` grava "Não contribuinte" na PF sem produtor rural que estava "Não informado";
    `RegrasFiscal.ValidarCamposDeEmpresa` (chamado no `PessoaAppService`) impede incluir/alterar regime, CNAE e SUFRAMA em
    PF/estrangeiro, mas aceita manter o gravado ou apagar; `RegrasFiscal.AtualizarHistorico`: quando a mudança só preenche
    dado vazio, corrige o período aberto em vez de abrir outro (decisão A, estendida a todas as naturezas; ver abaixo).
  - Testes: `Dominio/FiscalTests` (normalização, histórico, campos de empresa), `Cliente/FiscalPessoaFisicaFormularioTests`.
- **Documentos por natureza e ajustes do Fiscal da PJ (27/09/2026; código entregue, sem compilar; sem mudança no banco):**
  - Documentos e Fiscal continuam em abas separadas (decisão do usuário, como nos ERPs maduros); a ligação documento ↔
    estabelecimento fica para a migration abaixo.
  - Documento novo começa em "Escolha o tipo" (não grava sem tipo). `TiposDocumentoSistema.AplicaA`: RG e CNH só PF;
    passaporte PF e estrangeiro; documento estrangeiro só estrangeiro; "Outro" e tipos do usuário para todos. A lista do
    documento acompanha a natureza (trocar a natureza tira de um documento novo o tipo que deixou de valer); o tipo
    gravado continua como está. A API recusa tipo que não se aplica em documento novo ou troca de tipo
    (`RegrasDocumento.Aplicar`, parâmetro `natureza`). Órgão emissor só nos documentos pessoais de sistema; UF só RG e CNH
    (ou quando já preenchidos). O quadro do documento principal virou uma linha (`DocumentoPrincipalResumo`).
  - Fiscal: natureza jurídica mostrada com a descrição (`NaturezasJuridicas`, tabela CONCLA, dígito calculado);
    consulta de CNPJ preenche "Regime normal" quando a Receita diz que não é optante do Simples (ou, sem registro no
    Simples, só se o regime estava "Não informado"); histórico fiscal: preencher dado vazio (regime/indicador "não
    informado", IE ou situação sem valor) corrige o período aberto; alterar um dado preenchido ou produtor rural abre
    período novo.
  - Testes: `Cliente/DocumentosPorNaturezaTests`, `Dominio/FiscalTests` (histórico e natureza jurídica).
- **Identificação: documento primeiro, natureza jurídica da tabela, consulta automática e duplicidade (27/09/2026; código
  entregue, sem compilar; sem mudança no banco):**
  - Ordem: Natureza → CPF / CNPJ do principal + "Consultar CNPJ" / identificação estrangeira → nome... (como Omie, Bling,
    Conta Azul: o documento preenche o resto).
  - Natureza jurídica (Identificação e filial) é um `CampoLista` com a tabela `NaturezasJuridicas` (busca por código ou
    nome); grava o código de 4 dígitos, como antes. Código fora da tabela aparece como está; texto sem escolher não grava.
  - `PessoaFormulario.AoCompletarDocumento`: CPF ou CNPJ do principal completo, válido e diferente do último conferido
    (o gravado não é conferido ao abrir). A tela pergunta `POST pessoas/documento-em-uso` (mesma regra da gravação:
    CPF; raiz do CNPJ) e mostra o aviso com "Abrir cadastro"; sem duplicidade e em PJ nova, consulta a Receita sozinha
    (cadastro gravado: só pelo botão). Falha na pergunta não impede nada: a gravação continua recusando o duplicado.
  - Testes: `Cliente/IdentificacaoDocumentoTests`. Atenção ao rodar: testes de tela que digitam CPF/CNPJ válido agora
    geram a chamada `documento-em-uso` (e, na PJ nova, a consulta de CNPJ) — se algum teste conta chamadas ao servidor
    falso, ajustar.
- **Ficha mais larga, três colunas, resumo da pessoa e confirmação ao sair de PJ (27/09/2026; código entregue, sem
  compilar; sem mudança no banco):**
  - Ficha de Pessoas até 1600 de largura (antes 1200). `Controles/ColunasAdaptaveis` (ligado a todo FlexLayout pelo
    estilo implícito): com o FlexLayout a partir de 1000 de largura, campos de meia linha (base 50%) viram um terço;
    100%, automático e celular não mudam.
  - "Resumo da pessoa" (`ResumoPessoa`, `Views/Pessoas/ResumoPessoaView`): ficha a partir de 1280 → painel de 300 à
    direita (pode ser recolhido; volta pelo cartão); mais estreita → cartão acima das abas, fechado por padrão. Blocos
    por fontes (`IFonteResumoPessoa`): Situação (situação, bloqueios ativos, relacionamento), Documentos (vencidos e
    vencendo), Cadastro (documento em uso/faltando, endereço, município a corrigir, telefone/e-mail, contribuinte sem
    IE, regime). Tocar num item leva à aba. Refeito ao abrir, ao trocar de aba e depois de salvar. Módulos futuros
    (comercial, financeiro) acrescentam uma fonte, sem mudar a tela.
  - Natureza pela tela (`NaturezaNaTela`): sair de PJ com dados da empresa pergunta; confirmado, limpa CNPJ, dados da
    Receita, fiscais, sócios, grupo e a razão social vinda da consulta (endereços e telefones ficam); senão volta a PJ.
  - Testes: `Cliente/ResumoETrocaNaturezaTests`.
- **Filtro de Pessoas — Fase 1: catálogo de campos e motor único (27/09/2026; código entregue, sem compilar; sem mudança
  no banco):** plano aprovado em 4 fases (1 motor · 2 tela de Pessoas com painel e chips · 3 novos campos da 1ª versão ·
  4 visões salvas, exportar, "endereços duplicados" no "⋯" e saída da Consulta avançada do menu). Mapa de campos na
  planilha "Sugestão de filtro avançado.xlsx", aba "Mapa consolidado" (123 campos; 67 na 1ª versão).
  - Contratos (`Contracts/Pessoas/FiltrosPessoas.cs`): `CondicaoFiltro` (campo + `OperadorFiltro` + valores em texto),
    `CampoFiltroDto`, `CatalogoFiltrosPessoasDto`. `CriteriosPessoas.Condicoes` soma-se aos critérios antigos.
  - `Application/Consultas/CatalogoFiltrosPessoas.cs`: Ids estáveis (`CamposFiltroPessoas`, gravados nos filtros salvos),
    definições (grupo, tipo, operadores, vale para, permissão, opções) e validação das condições.
    `ConsultaPessoasAppService.CondicoesDe` traduz os critérios antigos; `CatalogoAsync` monta o catálogo pelas
    permissões (campos personalizados pesquisáveis entram como "campo:{Id}"). `GET pessoas/consulta/catalogo`.
  - `Infrastructure/Persistencia/Consultas/FiltrosPessoasSql.cs`: um Where por campo (mesmos índices de antes);
    `ConsultaPessoas.FiltrarAsync` passou a usar só ele. Campo novo = definição no catálogo + condição aqui (teste
    `CatalogoFiltrosPessoasTests` confere os dois lados).
  - A tela da Consulta avançada não mudou (continua mandando os critérios antigos). Testar nela: cada critério deve dar
    o mesmo resultado de antes.
- **Filtro de Pessoas — Fase 2: painel na tela de Pessoas (27/09/2026; código entregue, sem compilar; sem mudança no
  banco):**
  - `PainelFiltrosPessoas` (Cliente) monta os grupos a partir de `GET pessoas/consulta/catalogo`; cada campo tem caixa de
    marcar e, marcado, o editor do tipo (lista com caixas, texto, dias, período dd/mm/aaaa, sim/não; município por UF +
    nome). Campo incompleto não filtra. Chips acima da lista (tocar abre o grupo no painel; ✕ tira o filtro); botão
    "Filtros (n)"; "Buscar campo…"; "Limpar tudo". A lista relê 400 ms depois da última mudança, só quando as condições
    mudam de fato. Atalho PF/PJ desabilita os campos da outra natureza.
  - `Views/Pessoas/PainelFiltrosView`: painel à direita (340) na tela de Pessoas; no celular, sobre a lista. As colunas
    da tabela descontam o painel aberto.
  - Os filtros fixos antigos (Papel, Etiqueta, Incluir inativos, Município a corrigir) saíram: são campos do catálogo.
    Com "Situação do cadastro" no painel, a lista deixa de esconder os inativos (a condição decide).
  - API: `POST pessoas/pagina` (`ListaPessoasRequisicao`: filtro da lista + condições, no corpo); sem condições o cliente
    continua no `GET pessoas/pagina`. `PessoaRepositorio.ListarPaginaAsync(..., condicoes, hoje, ...)` usa o mesmo
    `FiltrosPessoasSql`. `CamposFiltroPessoas` foi para Contracts (o cliente usa os Ids).
  - Testes: `Cliente/PainelFiltrosTests`; `PessoasViewModelTests`/`AnexosTests` respondem o catálogo na abertura.
- **Filtro de Pessoas — Fase 3: campos da 1ª versão (27/09/2026; código entregue, sem compilar; sem mudança no banco):**
  46 campos novos no catálogo (65 no total), com a condição em `FiltrosPessoasSql`: nome fantasia, nascimento,
  aniversário no mês, idade, abertura, porte, natureza jurídica, grupo empresarial; sexo, estado civil, profissão;
  tipo de contato, WhatsApp, finalidade (NF-e, cobrança...), aceita comunicações, sem contato; cargo do contato;
  finalidade do endereço, bairro, CEP, sem endereço; tipo de documento, validade; indicador de IE, IE, situação na
  Receita; limite de crédito (VISUALIZAR_FINANCEIRO), perfil e condição do cliente; condição e avaliação do
  fornecedor; colaborador (empresa, tipo, admissão, ativo, cargo/departamento/setor vigentes — COLABORADOR);
  tipo de relacionamento e "relacionado a (nome)"; origem; consentimentos em vigor/revogados e canal (PRIVACIDADE);
  tipo do bloqueio; alterado em. Opções do banco: `IConsultaPessoas.OpcoesFiltroAsync` (cadastros ativos e valores já
  usados: porte, origem, situação na Receita, naturezas jurídicas). `DefinicaoCampoFiltro.SomenteDigitos` (CNAE, CEP,
  IE). Painel: número com "Entre" tem início e fim.
  - Índices: nenhum criado. Se algum filtro de texto (bairro, CEP, nome fantasia) ficar lento em base grande, medir e
    propor migration só de índices (aprovação do usuário).
- **Filtro de Pessoas — Fase 4: visões salvas, exportar e fim da Consulta avançada (27/09/2026; código entregue, sem
  compilar; sem mudança no banco):**
  - Botão "Visões" ao lado de "Filtros": lista as visões (lidas na hora, com as compartilhadas), aplica, salva os
    filtros atuais (nome; só para mim ou compartilhada; mesmo nome atualiza a própria) e remove a própria. O botão mostra
    "Visão: X" e "(alterada)" quando painel ou atalho mudam depois. Aplicar volta o atalho para "Todos" e relê na hora;
    campo sem permissão ou opção que não existe mais é ignorado com aviso. Tabela `FiltroSalvo` reaproveitada.
  - Critérios salvos/exportados = condições do painel + busca + atalho convertido em condição
    (`PessoasViewModel.CriteriosDaTela`). Filtros salvos no formato antigo (Consulta avançada) chegam convertidos para
    condições do catálogo (`ConsultaPessoasAppService.NoFormatoDoCatalogo`).
  - Botão "⋯" no cabeçalho: "Exportar o resultado (CSV)" (PESSOAS.EXPORTAR; confirmação; auditoria como antes) e
    "Procurar endereços duplicados" (lista com "Mostrar mais…"; tocar abre a ficha para consolidar).
  - Consulta avançada removida: rota `consulta-pessoas` do Shell, item do menu, `ConsultaPessoasPage`,
    `ConsultaPessoasViewModel` e `ConsultaPessoasApi` (as chamadas foram para `PessoasApi`). Favoritos e recentes
    gravados com `consulta-pessoas` passam a abrir `pessoas` (`MenuViewModel.RotasAntigas`). Endpoints da API mantidos.
  - Testes: `MenuLateralTests` (menu sem o item; alias da rota antiga), `PessoasListaTests` (atalho vira condição;
    aplicar visão).
- **Tela de Pessoas — Etapa 1: colunas escolhidas, ordenação e filtro nas colunas (27/09/2026; código entregue, sem
  compilar; migration nova aprovada pelo usuário):** mockup aprovado no artifact "Lone — Tela de Pessoas (proposta)".
  - **Migration (gerar no PMC):** `Add-Migration PreferenciasTela -Project Lone.Infrastructure -StartupProject Lone.Api
    -OutputDir Persistencia/Migracoes`. Só cria a tabela `PreferenciasTela` (Id, UsuarioId, Tela varchar(60), Conteudo
    nvarchar(4000), AlteradaEm + colunas da EntidadeBase; FK para `Usuarios`; índice único
    `IX_PreferenciasTela_UsuarioId_Tela`). Nenhum SQL de dados; nada muda nas tabelas existentes.
  - Preferência genérica por usuário + tela (`PreferenciaTela`, JSON que só a tela entende; fora da auditoria, como
    `PreferenciaMenu`): `GET/PUT api/menu/telas/{tela}` (`MenuUsuarioAppService.ObterTelaAsync/DefinirTelaAsync`: só
    objeto JSON até 4.000 caracteres). A lista de pessoas usa a tela `pessoas-lista` e recebe o layout já no catálogo
    (`CatalogoFiltrosPessoasDto.Colunas` + `Layout`): nenhuma requisição nova ao abrir a tela.
  - Colunas: `Application/Consultas/ColunasListaPessoas.cs` (Id = o do campo de filtro; só campos com um valor por
    pessoa: telefone/e-mail principal, bairro/CEP do endereço de referência, fiscal do estabelecimento principal, vendedor
    da carteira vigente, limite da conta geral) + `Infrastructure/.../ColunasPessoasSql.cs` (ordenação no ORDER BY com
    desempate nome → Id; valores das colunas extras lidos só para as linhas da página, uma consulta por coluna). Teste
    confere os dois lados. Limite de crédito exige VISUALIZAR_FINANCEIRO para mostrar e para ordenar.
  - `ListaPessoasRequisicao.Colunas/Ordenacao`; `PessoaResumo.Valores` (texto invariável). Sem condições, colunas extras
    nem ordenação, o cliente continua no GET simples.
  - Campos novos no catálogo de filtros (também no painel): Nome, Código, CPF/CNPJ (número ou parte), Cidade (nome).
  - Cliente: `GradePessoas` (colunas visíveis, seletor com "na lista" ↑↓✕ e todas por grupo com busca, marcar todas,
    restaurar padrão, ordenação em 3 cliques, linha de filtro liga/desliga), `FiltroColuna` (escreve no campo do painel
    com o mesmo Id: vira condição/chip/visão/exportação; número "10..50", "1000..", "..500", ">=", "<="; data
    "15/09/2026", "09/2026", "2026", faixas), `LinhaPessoa`/`CelulaGrade` (células já formatadas). Visões salvas guardam
    também o layout (`CriteriosPessoas.Layout`). Preferência gravada 0,8 s depois da última mudança, só se mudou.
  - Tela: nome preso à esquerda (300), colunas rolando para o lado (cabeçalho acompanha pela rolagem), linhas de altura
    fixa (56) para as duas partes ficarem alinhadas; `CollectionView` trocada por `BindableLayout` (página ≤ 50 linhas).
    Abaixo de 600 de largura: só o nome com o documento embaixo. Arrastar para reordenar e largura por arrasto: Etapa 2.
  - Testes: `ColunasListaPessoasTests`, `GradePessoasTests`, `MenuUsuarioAppServiceTests` (preferência da tela),
    `PessoasListaTests` (tela estreita; ordenar relê com POST e grava a preferência).
  - **A validar no Windows:** fluidez com muitas colunas (50 linhas × todas as colunas) e rolagem lateral do cabeçalho.
  - **Revisão (27/09/2026, sem compilar):** celular com o nome na largura toda (a 2ª coluna vai a 0); rolagem lateral
    sincronizada nos dois sentidos (cabeçalho ↔ linhas, com trava contra ping-pong); tirar a coluna que ordena volta à
    ordem padrão e relê; linha de filtro some no celular (`GradePessoas.MostrarLinhaFiltro`); colunas guardadas sem
    permissão continuam na preferência; natureza, situação, sexo, estado civil e regime ordenam pelo texto (CASE no
    banco), não pelo código do enum; preferência vazia = volta ao padrão ("{}"); coluna nula na requisição vira erro de
    validação; layout lido aceita a direção em número ou texto. Testes novos em `GradePessoasTests` e
    `MenuUsuarioAppServiceTests`; `AlteracoesPendentesTests` passou a responder o catálogo.
- **Tela de Pessoas — Etapa 2: visual aprovado no mockup (27/09/2026; código entregue, sem compilar; sem mudança no
  banco — a migration `PreferenciasTela` da Etapa 1 já foi gerada pelo usuário):**
  - Menu lateral: Pessoas mostra só "Cadastro" (com as rotas das configurações de Pessoas como relacionadas: continua
    destacado nelas). A página "Configurações de Pessoas" e cada cadastro continuam na busca do menu e nos favoritos
    (`MenuViewModel.CriarCatalogo`). Quem só configura, sem ver pessoas, continua com o item no menu. Organização e Metas
    não mudaram (as telas delas ainda não têm o botão).
  - Cabeçalho: "⚙ Configurações" (abre direto a página "Configurações de Pessoas", em cartões, como era pelo menu —
    decisão do usuário, que não gostou da lista; `PessoasViewModel.ConfiguracoesCommand`, a tela navega por `AbrirTela`)
    e "+ Nova pessoa".
  - Barra única: pesquisa, Visões, Filtros (destacado com filtro valendo) e "⋯" (agora com "Visões salvas…", que no
    celular é o único acesso).
  - Abas no lugar dos atalhos, com contador: Todos, PF, PJ, Clientes, Fornecedores (Ativos/Inativos saíram: conflitavam
    com o filtro "Situação do cadastro"). Contagens vêm na página 1 (`PaginaListaPessoas.Atalhos`, GET e POST): mesma
    busca e condições, sem o atalho; 3 consultas (natureza agrupada, clientes, fornecedores).
  - Chips com "Limpar" na mesma linha. Sucesso vira aviso flutuante embaixo (some em 4 s; `MostrarAvisoFlutuante`);
    erro e aviso continuam na barra (`MostrarBarraDaLista`).
  - Linha: avatar com iniciais (PF redondo, PJ quadrado), nome + "Cód."; papéis em selos coloridos (`SeloPapel` +
    controle `SeloTom`; cores novas `Grupo*` em Cores.xaml); "Sem CPF"/"Sem CNPJ" e "(a corrigir)" como selo de aviso;
    ligar, WhatsApp e e-mail ao passar o mouse (telefone/e-mail principais vêm sempre na página em `PessoaResumo.Valores`;
    `EnderecoDoTelefone` põe o 55 em números de 10/11 dígitos; a tela abre com `Launcher`). Nome preso com 340.
  - Densidade confortável (56) / compacta (44), guardada com as colunas (`LayoutListaPessoas.Compacta`).
  - Celular: linha vira cartão (documento · cidade, selos de papéis, ligar e WhatsApp sempre à vista, altura 92) e botão
    "↕ Ordenar" (coluna + direção; `GradePessoas.OrdenarPor`).
  - Tirar uma coluna só solta a ordenação se for a coluna que ordenava (antes: qualquer ordenação por coluna fora da
    lista, o que desfazia a escolhida pelo "Ordenar" do celular).
  - Revisão independente (sem compilar): telefone com "+" não ganha 55 e "0" de longa distância sai; ações rápidas com
    `Launcher.OpenAsync` (TryOpenAsync falha no Android 11+ sem `<queries>`); rolagem lateral sincronizada ignorando só o
    eco da posição pedida; mensagens de desativar apontam para o filtro "Situação do cadastro"; preferência "{}" = padrão;
    aviso repetido recomeça a contagem. **Atenção a desempenho:** as contagens das abas são 3 consultas a mais na
    página 1 (cada busca digitada); se ficar lento em base grande, medir e considerar cache ou contagem sob demanda.
  - **Ficou para a próxima rodada:** largura da coluna por arrasto e reordenar arrastando (hoje ↑↓ no seletor);
    prévia lateral e indicadores (Etapa 3); seleção múltipla (Etapa 4).
  - Testes: `MenuLateralTests` (menu sem configurações de Pessoas; catálogo com a página), `PessoasListaTests` (abas e
    contagens; ações rápidas; aviso flutuante; configurações da tela), `GradePessoasTests` (selos, iniciais, "Sem CPF",
    densidade, cartão do celular, ações rápidas).
- **Tela de Pessoas — abas escolhidas pelo usuário (27/09/2026; código entregue, sem compilar; sem mudança no banco):**
  pedido do usuário ("poder pôr Transportadora"); decisões: por usuário (padrão da empresa fica para depois), visões
  entram como abas e **todas as abas têm contador**.
  - Abas = "Todos" (fixa) + até 10 escolhidas: natureza (`natureza:Fisica`), papel do cadastro de Papéis (`papel:{Id}`,
    inclusive os criados pelo usuário; nome no plural por `CatalogoAbas.Plural`) ou visão salva (`visao:{Id}`, com ★).
    Ids em `AbasPessoas` (Contracts). Guardadas em `LayoutListaPessoas.Abas` (preferência da tela; nulo = padrão PF, PJ,
    Clientes, Fornecedores). As visões salvas não levam nem mudam as abas. Papel inativo/sem permissão: a aba some e o
    Id fica guardado; visão apagada: sai.
  - Editor: "＋" no fim das abas (e "Escolher as abas…" no ⋯): "Nas abas (nesta ordem)" com ↑↓✕, todas por grupo
    (Tipo de pessoa, Papéis, Visões salvas), limite com aviso, "Restaurar padrão" (`EditorAbas`, `ItemAba`, `GrupoAbas`).
    Menu "Visões" ganhou "Mostrar \"X\" como aba".
  - Contadores: natureza e papel vêm na página 1 (`ContagensAtalhosPessoas.Naturezas`/`Papeis`: 2 consultas, todos os
    papéis de uma vez, pessoas distintas com o papel ativo; papel sem ninguém = 0). Visões: `POST
    pessoas/consulta/filtros/contagens` (`ConsultaPessoasAppService.ContarFiltrosAsync`: só visões visíveis ao usuário;
    campo sem permissão fica de fora, como na tela; visão com condição inválida fica sem número), refeito no máximo a cada
    minuto (não depende da busca da tela) e ao salvar/fixar.
  - Tocar numa aba de visão aplica a visão (a aba fica marcada); sair dela para outra aba limpa painel e busca (eram da
    visão). Tirar a aba marcada volta para "Todos" e relê.
  - Abas que não cabem: rolam para o lado (barra de rolagem no computador), não "Mais ▾".
  - Testes (`PessoasListaTests`): aba Transportadoras com contador e preferência; máximo e restaurar padrão; aba de visão
    com contador, aplicar e sair; plural dos papéis. Contagens das abas no formato novo.
  - Revisão de compilação por agente (conta nova, 27/09 à noite, sem compilar): nenhum erro de compilação nem fila de
    teste errada encontrados. Três correções aplicadas: (1) numa aba de visão, as abas de natureza/papel/Todos ficam sem
    número (a contagem vinha com os filtros da visão, que tocar nelas limpa) — teste na "Aba_de_visao…"; (2) "Mostrar
    como aba" com a visão já aplicada só marca a aba (antes reaplicava a gravada e perdia o "(alterada)"); (3)
    `ColunasListaPessoas.Limpar` guarda até 2×Maximo abas (as guardadas de papéis desativados vinham depois das 10 e eram
    cortadas). Ficou anotada, sem mexer: corrida antiga em que uma resposta atrasada deixa total/contadores velhos na tela.
- **Etapa 3 (prévia lateral e indicadores):** plano e decisões do usuário (D1–D5, 27/09 à noite) em
  `docs/PLANO-ETAPA3-PESSOAS.md`.
  - **3a — Prévia (código entregue, sem compilar; sem mudança no banco nem na API):**
    - `PreviaPessoa` (Lone.Cliente) lê a pessoa como a ficha (`ObterAsync` + `PessoaFormulario.De`, esperando 150 ms e
      cancelando a anterior) e mostra o cabeçalho da linha, "Abrir ficha", ligar/WhatsApp/e-mail e o mesmo
      `ResumoPessoa` da ficha. Tocar num item do resumo abre a ficha na aba do item. ‹ › andam pela página ("3 de 50").
    - Tela: coluna `ColunaPrevia` (380 + 16) entre a tabela e o painel de filtros, ao lado só da tabela e da paginação
      (barra, abas e chips passam por cima). Só quando cabe: a lista precisa ficar com ≥ 700 (`Previa.Cabe`). Sem
      espaço (celular, janela estreita), o clique abre a ficha. A linha mostrada fica marcada (`LinhaPessoa.NaPrevia`).
    - Clique: prévia (padrão) ou abre a ficha, escolha de cada usuário no botão "Clique: …" da barra da tabela
      (`LayoutListaPessoas.CliqueAbreFicha`, na preferência da tela; as visões não mexem nisso). Duplo clique sempre abre
      a ficha. O "⋯" da linha ganhou "Mostrar prévia" quando cabe.
    - A prévia fica aberta de uma pessoa para outra, acompanha a lista relida sem ler de novo e relê ao voltar da ficha
      (ou quando reaparece, se estava escondida).
    - **Não feito:** teclado (↑↓, Enter, Esc) — o MAUI não tem atalho de teclado sem código por plataforma; ficou ‹ › na
      prévia. Cache das últimas pessoas lidas (não pareceu necessário).
    - Testes (`PessoasListaTests`): clique mostra a prévia com o resumo; mesma pessoa não relê; próxima; fechar; lista
      relida mantém a pessoa; cadastro que não existe mais; escolha "abre a ficha" guardada; sem espaço abre a ficha;
      janela média.
    - Revisão por agente (sem compilar): nenhum erro de compilação ou de fila; corrigidos 4 detalhes (linha marcada com a
      prévia fechada ou escondida, leitura à toa no duplo clique e com a prévia escondida).
  - **3b — Indicadores (código entregue, sem compilar; sem migration, sem índice novo):**
    - Faixa abaixo da barra de busca: "Com bloqueio", "Documentos vencidos", "Documentos vencendo" e "Com pendência
      cadastral", contados na base toda (ativos e em análise; decisão D5). Tocar põe a condição no painel de filtros
      (vira chip e o indicador fica destacado); tocar de novo tira. Zero fica apagado, mas à vista. "⋯" da faixa: tirar
      ou pôr cada indicador, ou esconder a faixa (volta pelo "⋯" da lista). Guardado em
      `LayoutListaPessoas.SemIndicadores`/`IndicadoresOcultos`.
    - Servidor: campos novos no catálogo `documentos.vencendoPeloAviso` (antecedência do próprio tipo, como o resumo;
      tipo fora do cadastro usa 30 dias; D4) e `cadastro.comPendencia` (enum `PendenciaCadastral` em Contracts: sem
      CPF/CNPJ, sem endereço ativo preenchido, município a corrigir de endereço ativo, contribuinte sem IE; D3).
      `IndicadoresListaPessoas` (Application) define os 4. `ConsultaPessoasAppService.IndicadoresAsync` + GET
      `pessoas/consulta/indicadores`.
    - Os números vêm dentro do catálogo (sem ida a mais ao abrir a tela; se a contagem falhar, o catálogo vem sem eles e
      a tela abre normal). Depois, a lista reconta no máximo 1×/minuto na página 1 (`IntervaloIndicadores`) e depois de
      voltar da ficha. Faixa escondida: o servidor nem conta.
    - Testes: `IndicadoresListaTests` (faixa do catálogo, tocar põe/tira, campo sem permissão avisa, intervalo,
      preferência, e a coerência: cada `PendenciaCadastral` aparece no resumo da pessoa) e
      `CatalogoFiltrosPessoasTests` (cada indicador é uma condição válida do catálogo).
    - Revisão por agente (sem compilar): nada de compilação, tradução do EF segura. Corrigidos: faixa sem caminho de
      volta ao tirar todos os indicadores; nova tentativa quando o catálogo vem sem números; município a corrigir de
      endereço sem logradouro (o resumo não mostra); espaço duplo entre os chips.
    - **Atenção:** o SQL das pendências não tem teste (não há teste de banco no projeto). Conferir na tela: tocar em "Com
      pendência cadastral" e abrir algumas pessoas da lista (o resumo de cada uma deve mostrar a pendência).
      `IndicadoresOuNadaAsync` engole erro de contagem sem log: se a faixa não aparecer, testar a rota direto.
- **Compilação e commits (27/09/2026, 23h):**
  - O usuário compilou tudo, e o build passou.
  - O `dotnet test` teve 2 falhas em testes antigos, desatualizados, e os dois foram corrigidos:
    - `Condicoes_invalidas_sao_recusadas`: o CNAE com 8 dígitos agora responde "no máximo 7 dígitos".
    - `Datas_e_numeros_invalidos…`: desde "documentos por natureza", um documento novo precisa de tipo antes de a
      validade ser conferida.
  - Commits no branch `pessoas-fiscal-documentos-resumo`: `7155284` (filtros, Etapas 1–2, abas configuráveis),
    `8651c2f` (prévia) e `8db9988` (indicadores e testes). Não houve como separar filtros, Etapas 1–2 e abas, porque
    mexem nos mesmos arquivos.
  - Falta confirmar a rodada de testes depois da correção.
- **Migration pendente de aprovação (documentos):** no tipo de documento, "Aplica-se a" (PF/PJ/estrangeiro) e quais campos
  padrão usa (órgão emissor, UF, emissão); no documento, estabelecimento opcional (alvará/licença da filial), com resumo
  dos documentos da filial no cartão do estabelecimento (Fiscal) e link para a aba Documentos.
- **Fase futura: produtor rural e retenções (desenho registrado em 27/09/2026; nada implementado):**
  - Motivo: comprar de produtor rural PF envolve a contribuição previdenciária sobre a produção rural (Funrural), com
    retenção/sub-rogação pelo adquirente em muitos casos, e a declaração da aquisição (EFD-Reinf); contratar autônomo PF
    envolve retenções (INSS, IRRF, ISS conforme o município).
  - Dados previstos: opção do produtor pela contribuição sobre a folha (anual), indicadores de sub-rogação/retenção;
    no autônomo, NIT/PIS, contribuição já recolhida em outra fonte e inscrição municipal.
  - Onde moram: dados que mudam com o tempo (opção anual do produtor) no histórico fiscal por período (`HistoricoFiscal`),
    ao lado de produtor rural e IE; os demais no estabelecimento. Avaliar junto um "perfil tributário do participante"
    (como o grupo tributário dos ERPs maduros), usado pelo motor fiscal para CFOP/ICMS/retenções.
  - Depende do módulo de compras/entrada de notas e da apuração: só implementar quando houver quem consuma os dados.
    Exige migração (aprovação do usuário). Validar as regras com um contador na época (mudaram várias vezes e a reforma
    tributária — IBS/CBS — também trata do produtor rural).
  - Lembrete do módulo fiscal: cada nota deve gravar a cópia dos dados do destinatário na emissão; o histórico do cadastro
    não substitui essa cópia.

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
