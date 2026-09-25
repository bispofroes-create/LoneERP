# Revisão estrutural do módulo Pessoas — análise (25/09/2026)

Documento de trabalho da revisão pedida (itens 1 a 28). Primeiro o que existe, depois o que muda. As decisões
tomadas com o usuário ficam no fim.

## 1. Estrutura atual encontrada

**Camadas.** `Lone.Domain` (entidades, regras, validadores) → `Lone.Application` (AppServices, permissões) →
`Lone.Infrastructure` (EF Core/SQL Server, auditoria) → `Lone.Api` (Minimal API) → `Lone.Cliente` (ViewModels MVVM,
testáveis) → `Lone.App` (MAUI: telas XAML e controles).

**Ficha da pessoa.** `PessoasPage.xaml` (lista + ficha) com uma faixa de abas (`SecaoOpcao.Para`) e 13 views
(`Views/Pessoas/Secao*View.xaml`). O estado fica em `PessoaFormulario` (928 linhas) e os itens de lista em
formulários próprios (`EnderecoFormulario`, `MeioContatoFormulario`, `DocumentoFormulario`, `EstabelecimentoFormulario`,
`EtiquetasFormulario`, `ConsentimentoFormulario`, `SituacoesFormulario`, `ColaboradorFormulario`, `ComercialFormulario`).

Abas hoje: Geral · Dados pessoais (PF) / Empresa e estabelecimentos (PJ) · Contatos · Endereços · Documentos + Dados
fiscais (não PJ) · Cliente (papel Cliente) · Fornecedor · Colaborador · Informações adicionais · Relacionamento e LGPD ·
Situação · Histórico.

**Controles reutilizáveis (Lone.App/Controles).** `Campo` (rótulo + Entry com máscara), `CampoEscolha` (rótulo +
Picker), `CampoLista` (autocompletar), `CampoMunicipio` (UF + município IBGE), `CaixaMarcar`, `BarraMensagem`,
`LayoutMestreDetalhe`. Estilos em `Resources/Styles` (Titulo, Subtitulo, Rotulo, Secundario, Cartao, CartaoItem,
BotaoSecundario, BotaoLink, cores Aviso/Sucesso/Borda).

**Tabelas envolvidas.** `Pessoas`, `Estabelecimentos` (+ `HistoricoFiscal`, `EstabelecimentoCnaes`),
`PessoaEnderecos` (finalidades em bits, `TipoEnderecoId`), `MeiosContato` (enum `TipoContato` Telefone/Celular/
WhatsApp/Email/Outro + `TipoMeioContatoId` do cadastro com `Categoria` telefone/e-mail), `Contatos` (pessoas de
contato), `PessoaDocumentos` (+ `TiposDocumento`, `AnexosDocumento`, valores de campos), `PessoaPapeis` (períodos) +
`Papeis`, `PessoaEtiquetas` + `Etiquetas`, `PessoaConsentimentos` (canal, concedido, datas, origem),
`PessoaValoresPersonalizados` + `CamposPersonalizados`, `ContasCliente`/`ContasFornecedor`, `ExcecoesComerciais`,
`CarteiraClientes`, `VinculosColaborador`/`LotacoesColaborador`, `PessoaBloqueios`, `Interacoes`,
`PessoaRelacionamentos`/`GruposEconomicos` (sem tela), `Profissoes` + `OcupacoesCbo`, `Municipios`.

**Como as opções chegam na ficha.** Ao abrir a tela de Pessoas, em sequência: Etiquetas, Campos, Profissões, Papéis,
Tipos de telefone/e-mail, Tipos de endereço, Tipos de documento, Campos de documento (tudo do banco, nada fixo). Abas
pesadas carregam sob demanda (Colaborador, Cliente). Município: tabela do IBGE com busca.

**Validações.** Backend: `PessoaValidador` (CPF/CNPJ com dígitos, obrigatórios, datas, duplicidade),
validadores de endereço, documento, campos personalizados, comercial. Frontend: só datas/números mal digitados
(`ValidarLocalmente`) e mensagens todas no topo da ficha — CPF/CNPJ só são conferidos ao salvar.

**Navegação.** `AppShell.xaml`: 19 itens soltos no menu lateral (um por cadastro), cada um com rota própria.

## 2. Problemas identificados (com a causa)

| # | Problema | Causa encontrada |
|---|---|---|
| P1 | Idade errada/antiga na tela | O campo "Idade" é um `Campo` ligado em OneWay; ao receber o valor, o controle reescreve `Texto` e o MAUI **remove a ligação OneWay** quando a propriedade é definida localmente. Na primeira atualização (ex.: "13/05/19" vira "7 anos" enquanto se digita) a ligação morre e a idade congela. Remover a data também não limpa. O cálculo (`Idade.Em`) está certo. |
| P2 | CPF/CNPJ só avisados no fim | Não há validação no cliente ao sair do campo; o `Campo` não tem estado de erro. |
| P3 | Tela fecha com 2+ endereços | Forte suspeita: o seletor "Endereço fiscal" do estabelecimento recebe a **ObservableCollection viva de EnderecoFormulario** como itens do Picker (WinUI); incluir endereços enquanto o Picker a observa derruba o app. Todos os outros seletores usam arrays estáveis. |
| P4 | "UF" repetido na naturalidade | `CampoMunicipio` usa rótulo "UF" **e** dica "UF" no Picker. |
| P5 | Menu lateral poluído | Cada cadastro auxiliar é um item de primeiro nível. |
| P6 | Papéis, finalidades e etiquetas em caixas espalhadas | Não existe componente de seleção múltipla. |
| P7 | Telefones e e-mails misturados | Uma lista só de `MeiosContato`, com o tipo escolhido dentro do card. |
| P8 | "Cliente" não descreve o conteúdo; Etiquetas dentro de "Relacionamento e LGPD"; LGPD misturado | Organização de abas. |
| P9 | Campos de PJ/PF misturados | "Geral" junta natureza, CPF, nascimento, nome social e CNPJ; "Dados fiscais" aparece para PF com campos de PJ. |
| P10 | Importação de ocupações | O backend importa e grava (inclui/atualiza/desativa, nunca apaga). Faltam pré-visualização, histórico e relatório; se o botão "não faz nada" na máquina do usuário, é preciso a mensagem de erro (ver decisões). |

## 3. Estrutura proposta

**Menu.** Grupo PESSOAS (Pessoas, Consulta avançada) · CONFIGURAÇÕES DE PESSOAS · CONFIGURAÇÕES COMERCIAIS. As
telas continuam as mesmas e com as mesmas rotas (`Routing.RegisterRoute`), só saem do primeiro nível.

**Ficha** (a primeira coisa de um cadastro novo é o tipo de pessoa; as abas se adaptam):

1. **Identificação** — Tipo de pessoa · CPF (PF) ou CNPJ + consulta (PJ) com validação ao sair do campo · Nome
   completo / Razão social · Nome de exibição (logo abaixo, com a ajuda "Se não informado, será utilizado o nome
   completo") · Nome fantasia (PJ) · Nascimento + idade calculada (PF) · Papéis (seleção múltipla) · Observações.
2. **Dados pessoais** (PF) — sexo, identidade de gênero, estado civil, escolaridade, nacionalidade, mãe, pai,
   profissão, naturalidade (UF → município), nome social, apelido. / **Empresa e estabelecimentos** (PJ).
3. **Contatos** — Telefones (+ Adicionar telefone), E-mails (+ Adicionar e-mail), Pessoas de contato.
4. **Endereços** — um card por endereço, finalidades em seleção múltipla.
5. **Documentos** — identificação (CPF/CNPJ, só leitura, vem da aba 1) + documentos parametrizados.
6. **Fiscal e tributário** — PJ: IE e indicador, IM, SUFRAMA, regime, CNAE, produtor rural, histórico. PF: só
   produtor rural e, quando marcado, a IE de produtor (é o único caso fiscal de PF).
7. **Comercial** (papel Cliente) — Condições comerciais · Exceções · Carteira.
8. Fornecedor · Colaborador (como hoje, por papel).
9. **Informações adicionais** — campos personalizados (dinâmicos).
10. **Relacionamento** — origem ("como conheceu"), primeiro contato, interações, situação de relacionamento.
11. **Etiquetas** — seleção múltipla com busca.
12. **Privacidade e comunicações** — um cartão por canal (e-mail, WhatsApp, SMS, ligações, correspondência) com
    autorizado/não, datas, origem e o histórico (auditoria).
13. Situação · Histórico.

## 4. Componentes

- **Novo `CampoMultiplo`** (Lone.App) + **`SelecaoMultipla`** (Lone.Cliente, testável): chips removíveis, botão que
  abre a lista com busca e caixas, fecha sozinho; opções sempre vindas do ViewModel (nada fixo no controle). Usado em
  Papéis, Etiquetas e Finalidades; pronto para Equipes e outras classificações.
- **`Campo` ganha estado de validação**: `Erro` (texto vermelho logo abaixo + borda de erro), `Valido` (✓),
  evento/comando ao sair do campo (`AoSair`). Reaproveitado por todos os campos da ficha.
- Reutilizados: `CampoEscolha`, `CampoMunicipio` (corrigido), `CampoLista`, `BarraMensagem`, cartões e estilos.
- Seletor de endereço fiscal passa a usar opções em array (padrão dos outros seletores).

## 5. Entidades/tabelas

Nenhuma coluna existente muda. A reorganização é de apresentação e validação; os dados já estão modelados.
Idade **não é armazenada** (sempre calculada da data). Possíveis acréscimos dependem das decisões (segmentação,
histórico de importações da CBO).

## 6. Migrations

Só se as decisões pedirem tabelas novas (`ImportacoesCbo` para o histórico; `Segmentos` para segmentação). Sempre
aditivas, sem alterar dados existentes.

## 7. Regras de negócio mantidas/explicitadas

- PF: CPF obrigatório só se a política já exige (hoje opcional); se informado, dígitos conferidos no cliente e na API.
- PJ: CNPJ do estabelecimento principal conferido no cliente e na API (aceita CNPJ alfanumérico).
- Um endereço principal; finalidades com significado de sistema (Fiscal vai para NF-e, Cobrança para boletos).
- Papéis encerram período ao desmarcar (histórico), inclusive pelo novo componente.
- Aba Comercial só com papel Cliente; Fiscal PJ só para PJ.

## 8. Validação

Cliente: ao sair do campo (CPF, CNPJ, data de nascimento, e-mail, telefone) com mensagem junto do campo; ao salvar,
a lista de erros continua no topo **e** cada campo mostra o seu. API: continua a autoridade (nenhuma regra só visual).

## 9. Testes

Testes de ViewModel (xUnit, sem interface): idade (aniversário já ocorrido/não ocorrido, recém-nascido, data
inválida, vazia, mudança, remoção), CPF/CNPJ válido/inválido no campo, 1/2/3 endereços, editar/remover o 2º, trocar
o principal, finalidades diferentes, vários telefones/e-mails, papéis e etiquetas pelo componente múltiplo, abas
PF×PJ, Comercial com/sem cliente, campos personalizados; importação da CBO (pré-visualização e gravação) no domínio.

## 10. Ajustes do usuário à análise (aprovada como base)

1. **Unicidade de documentos:** por tipo de documento + número (+ país emissor quando aplicável). CPF e CNPJ mantêm as
   regras próprias; passaporte, documento estrangeiro e identificação fiscal estrangeira também terão regra de
   unicidade. Estrangeiro não libera duplicidade.
2. **País:** antes de criar colunas de país, verificar se já existe cadastro de países e reutilizar a referência
   (sem código e nome soltos que possam divergir).
3. **Segmentação:** `Segmentos` + `PessoaSegmentos`; um principal por pessoa hoje, estrutura pronta para vários;
   nunca dois principais — regra no negócio e índice único filtrado no banco.
4. **R1 / endereços:** não corrigir por hipótese. Reproduzir (1, 2, 3 endereços; editar e remover o 2º; finalidades
   diferentes; trocar o principal), capturar exceção e pilha, identificar a causa e só então corrigir.
5. **R2 / validação:** erro junto do campo, estado visual de erro, mensagem clara, validação ao digitar quando fizer
   sentido, carregando em consultas externas; **sem** ícone verde em todo campo válido.
6. **R7 / Comercial:** contextual ao papel Cliente; tirar o papel **não apaga** os dados comerciais (preservados).
7. **Histórico:** o que deixa de valer é desativado/encerrado, nunca apagado.
8. **Execução:** uma etapa por vez, começando pelo R1; nada fora do escopo sem avisar antes.

## 11. Endereço × finalidade (aprovado em 26/09/2026)

**Conceito.** Endereço = localização física; finalidade = uso. "Principal" não é finalidade: é a marca da relação
Pessoa + Endereço + Finalidade. O mesmo endereço não é cadastrado de novo para outro uso.

**Tabelas.**
- `FinalidadesEndereco` (cadastro): Id (PK; iniciais estáveis 7a9e1c07-…-01..06), Codigo (único, imutável:
  COMERCIAL, RESIDENCIAL, FISCAL, ENTREGA, COBRANCA, CORRESPONDENCIA), Nome (único), Ordem, DoSistema, Ativo.
  O sistema trabalha com o Id; regra que precisa de uma finalidade usa o Código. O enum antigo fica só para a coluna
  legada e a migração.
- `PessoaEnderecos`: chave alternativa (Id, PessoaId); `MescladoEmId` (FK para ela mesma); `Finalidades` (bits) passa a
  ser **legada/derivada** — regravada pela API na mesma transação (bits das finalidades ativas + bit 1 no endereço de
  referência da listagem); nada lê dela.
- `Pessoas.RevisarFinalidadesEndereco`: marca geral da migração; a ficha mostra o motivo por finalidade; a API só
  desliga (quando não restar pendência).
- `PessoaEnderecoFinalidades`: Id, PessoaId, PessoaEnderecoId, FinalidadeId, Principal, Ativo.
  - FK composta (PessoaEnderecoId, PessoaId) → PessoaEnderecos(Id, PessoaId): relação e endereço da mesma pessoa.
  - Único filtrado (PessoaEnderecoId, FinalidadeId) WHERE Ativo = 1 + reativação da mesma linha ao adicionar de novo.
  - Único filtrado (PessoaId, FinalidadeId) WHERE Principal = 1.
  - CHECK (Principal = 0 OR Ativo = 1).

**Regras.** Principal explícito (nunca pela ordem); ficha pergunta antes de substituir; endereço/relação inativos
sem principal; endereço reativado volta sem principal; finalidade desativada não entra em associação nova.
Endereço de referência da listagem (só exibição): principal da finalidade ativa de menor Ordem, senão o 1º ativo.
Conferência de IE: endereço fiscal da filial → principal FISCAL → endereço de referência (nunca vira principal).

**Duplicidade.** Pelo endereço físico normalizado (CEP, UF/município, bairro, logradouro com abreviações seguras no
início, número com S/N, complemento): Igual / Possível / Diferente. Novo igual = bloqueado com "Usar endereço
existente"; possível = o usuário confirma. Duplicados antigos: apontados na ficha e pela rotina
`GET pessoas/enderecos-duplicados` (Consulta avançada); consolidação assistida (escolhe o que fica, mostra o
resultado e as diferenças, une finalidades preservando o principal, duplicado inativo + `MescladoEmId`, filiais
redirecionadas, evento na auditoria). Nunca DELETE.

**Migração** (`SqlMigracaoFinalidadesEndereco.MigrarFinalidades`, inserida pela ferramenta): uma relação por bit
(sem o bit 1); principal: (1) antigo principal com a finalidade; (2) único endereço ativo com ela; (3) 2+ sem antigo
principal = sem principal + revisão; (4) antigo principal sem finalidade = nada inventado + revisão; (5) dois antigos
principais = revisão. Endereço inativo = histórico sem principal. Conferências com THROW (desfaz tudo).
