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
