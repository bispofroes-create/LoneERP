# Senhas e crachá de acesso (proposta, 28/09/2026)

> Pedido do usuário (28/09, 17h50), só anotado: nada foi implementado. Entra depois da Fase 2a (ou quando o usuário
> decidir), com plano aprovado antes do código. As decisões **[S#]** precisam de resposta.

## 1. O que já existe

- **Senha guardada com hash, não com criptografia reversível:** PBKDF2-HMAC-SHA256, sal de 16 bytes e 600.000 iterações
  (`HasherSenhaPbkdf2`, recomendação OWASP). Nem quem tem acesso ao banco consegue ler a senha: só conferir.
- **Política atual** (`PoliticaSenha`): mínimo de 8 caracteres, com letras e números, e diferente do login.
- **Bloqueio por tentativas** (`Usuario.MaximoTentativas`, `TempoBloqueio`), limite de 10 logins por minuto por IP e
  resposta com o mesmo tempo quando o login não existe.
- **Senha definida pelo administrador** obriga o usuário a criar a dele no próximo acesso, e as sessões abertas caem.

## 2. Como os sistemas maduros fazem hoje

- **NIST SP 800-63B (revisão 4) e OWASP:**
  - comprimento conta mais que "misturar tipos de caractere": mínimo 8 com um segundo fator; 15 quando a senha é o único
    fator (NIST rev. 4);
  - aceitar senhas longas (64 ou mais), espaços e acentos (frases-senha);
  - **não** exigir composição ("maiúscula, número e símbolo") nem troca periódica: só trocar quando houver suspeita;
  - **recusar senhas conhecidas:** lista de senhas vazadas e comuns, e palavras do contexto (nome, login, nome da empresa);
  - medidor de força que estima o tempo para descobrir, e não só conta tipos de caractere (zxcvbn, usado por Dropbox e
    outros).
- **Microsoft Entra, Google Workspace e ERPs (SAP, TOTVS):** lista de senhas proibidas, histórico para não repetir
  (Active Directory guarda até 24), bloqueio por tentativas, segundo fator (autenticador) e, no chão de loja ou fábrica,
  **crachá com PIN** em vez de senha digitada.

## 3. Proposta: senha

1. **Medidor de força** na tela de troca de senha e no cadastro de usuário, enquanto a pessoa digita:
   - quatro níveis: **Fraca / Média / Forte / Muito forte**, com barra e cor, e o texto dizendo o motivo ("sequência de
     teclado", "é o seu nome", "senha muito comum", "fica forte com mais 4 caracteres");
   - estimativa no estilo zxcvbn (dicionário, sequências, repetições, datas, teclado), em português;
   - a regra fica no domínio (`Lone.Domain`), então o app mede na hora e a API confere de novo a mesma regra;
   - a API recusa "Fraca". Aceitar "Média" é a decisão **[S2]**.
2. **Sugestão de senha:** botão "Sugerir senha" que gera uma **frase-senha** (4 palavras em português + número, ex.:
   "cadeira-limão-trovão-47"), mais fácil de lembrar e mais forte que "Ab#12345". Uma segunda opção gera senha
   aleatória de 16 caracteres, para quem usa gerenciador de senhas. Gerada no aparelho, com gerador criptográfico.
3. **Não repetir senhas antigas:** guardar os **hashes** das últimas N senhas (nunca a senha) e recusar a nova que bater
   com alguma. N é a decisão **[S3]**.
4. **Lista de senhas proibidas:**
   - lista embutida das senhas mais comuns e vazadas (ex.: 100 mil), com variações em português ("123mudar", "senha123");
   - palavras do contexto: nome, login, e-mail, nome da empresa e "lone";
   - opcional: consulta ao Have I Been Pwned por k-anonimato (só 5 caracteres do hash saem do servidor), se a API tiver
     internet (**[S4]**).
5. **Comprimento:** mínimo 10 (ou 12) e máximo 128, com espaços e acentos aceitos, sem exigir composição (**[S1]**).
6. **Sem troca periódica obrigatória.** Troca forçada só quando o administrador redefine a senha ou quando há suspeita de
   vazamento. Opcional por perfil: validade em dias, para quem exigir por auditoria (**[S5]**).

## 4. Proposta: crachá (código de barras ou QR Code)

**Resposta direta: dá para fazer, mas o crachá não deve levar a senha, nem criptografada.** Se a senha estiver no
código, criptografada ou não, a chave para abrir precisa estar no aplicativo, e quem copiar o app descobre as senhas.
Além disso, trocar a senha invalidaria o crachá. Os sistemas maduros resolvem assim:

1. **Credencial própria do crachá:** o servidor gera um **código aleatório de 128 bits ou mais**, sem relação com a
   senha nem com o login:
   - ex.: `LONE1-7KQ2M-9XWPD-R4TZH-B8NCY-...`, que no leitor aparece só como texto sem sentido;
   - o banco guarda só o **hash** desse código (como faz com a senha): nem quem lê o banco consegue imprimir um crachá igual;
   - um crachá ativo por usuário, com número de via (reimprimir invalida o anterior), validade opcional, e revogação na
     hora (perdeu, foi desligado).
2. **Crachá + PIN (recomendado):** o crachá é "algo que você tem", e qualquer um que fotografar o QR poderia entrar. Os
   sistemas de loja e fábrica pedem junto um **PIN de 4 a 6 dígitos** (curto, porque o crachá já é o primeiro fator),
   com bloqueio depois de poucas tentativas (**[S6]**).
3. **Onde vale:**
   - login por crachá só em aparelhos autorizados (balcão, estação da fábrica, tablet do depósito), ligado ou desligado
     por usuário ou perfil;
   - opcional: um perfil próprio para quem entra por crachá (ex.: só vendas no balcão, sem configurações) (**[S7]**).
4. **Leitura:**
   - leitores USB e Bluetooth funcionam como teclado: a tela de login ganha "Passe o crachá", que reconhece a leitura
     rápida terminada em Enter e não confunde com digitação;
   - no Android, dá para ler também pela câmera.
   - **QR Code** é o recomendado (mais dados, correção de erro, lê torto ou com risco). Código de barras (Code 128) também
     serve, com um código mais curto (cerca de 100 bits).
5. **Cartão para imprimir:**
   - PDF no tamanho de cartão de crédito (CR80, 85,6 × 54 mm), com nome, empresa, foto opcional (a da ficha da pessoa
     ligada ao usuário, Fase 2a) e o QR;
   - uma folha A4 com vários cartões para imprimir de uma vez;
   - bibliotecas: QRCoder (MIT) para o QR e QuestPDF ou PdfSharp para o PDF. Usar biblioteca externa aqui é a decisão
     **[S8]** (MC-10 só tratou da fila).
6. **Auditoria:** "entrou pelo crachá (via 3) no aparelho X", crachá emitido, reimpresso ou revogado, e cada PIN errado.

## 5. Verificação em duas etapas (código do autenticador, como o do Google)

Pedido do usuário (28/09, 17h54): PIN ou senha + o código de 6 dígitos que muda a cada 30 segundos.

1. **Padrão TOTP (RFC 6238):** funciona com Google Authenticator, Microsoft Authenticator, Authy, 1Password e outros.
   Não precisa de internet no celular nem de SMS (SMS é o método mais fraco, e o NIST desaconselha). O cálculo é
   HMAC-SHA1 sobre o relógio: dá para fazer com código próprio, sem biblioteca.
2. **Ativação pelo usuário:**
   - em "Minha conta › Verificação em duas etapas", o app mostra um QR Code (e o código para digitar);
   - a pessoa lê no autenticador, digita um código para confirmar, e só então a verificação liga;
   - recebe **10 códigos de recuperação** de uso único (para quando perder o celular), mostrados uma vez só e guardados
     como hash.
3. **No login:**
   - senha (ou PIN, ou crachá + PIN), depois o código de 6 dígitos;
   - tolerância de 30 segundos para mais ou para menos (relógio do celular adiantado ou atrasado);
   - o mesmo código não vale duas vezes (anti-reuso);
   - bloqueio por tentativas, como na senha;
   - opção "Confiar neste aparelho por 30 dias", para não pedir o código todo dia no computador de sempre (**[S11]**).
4. **Segredo do autenticador:**
   - diferente da senha, precisa ser lido de volta para conferir o código, então não pode ser hash;
   - é gravado **criptografado** com a proteção de dados do ASP.NET (chave fora do banco);
   - quem copiar só o banco não consegue gerar os códigos.
5. **Administração:**
   - o administrador pode exigir a verificação por perfil (ex.: Administrador e Financeiro) (**[S9]**);
   - pode desligar a de um usuário que perdeu o celular e os códigos de recuperação, com motivo, e isso fica na auditoria;
   - nunca vê o segredo.
6. **PIN + código em vez de senha** (**[S10]**):
   - é aceitável, porque o PIN é "algo que você sabe" e o código é "algo que você tem" (o celular);
   - o PIN precisa de bloqueio curto (ex.: 5 erros) e só vale com a verificação ligada;
   - bancos fazem assim no aplicativo;
   - para quem usa o sistema o dia todo no escritório, senha + código com "confiar neste aparelho" costuma ser mais
     prático.

## 6. Outras melhorias de segurança que valem junto (opcionais)

- **Aviso de login novo:** "entrou num aparelho novo", na tela e no e-mail.
- **Sessões abertas:** o usuário vê e encerra as sessões dele. Os tokens de renovação por aparelho já existem.

## 7. Decisões (a responder quando for planejar)

- **[S1] Comprimento mínimo:** A (recomendado) 10 caracteres, sem exigir composição; B 12; C manter 8 com letras e
  números.
- **[S2] "Média" é aceita?** A (recomendado) sim, e só "Fraca" é recusada; B exigir "Forte".
- **[S3] Histórico:** A (recomendado) não repetir as últimas 5; B as últimas 10; C nenhuma (o NIST não exige).
- **[S4] Consulta a senhas vazadas pela internet (HIBP):** A (recomendado) só a lista embutida (funciona sem internet);
  B lista embutida + HIBP quando houver internet.
- **[S5] Validade da senha:** A (recomendado) sem validade, trocando só quando há suspeita; B validade por perfil (ex.:
  90 dias).
- **[S6] Crachá:** A (recomendado) crachá + PIN; B só o crachá, e só em aparelhos autorizados.
- **[S7] Onde o crachá vale:** A (recomendado) aparelhos autorizados, ligado por usuário; B em qualquer aparelho.
- **[S8] Bibliotecas:** A (recomendado) QRCoder + QuestPDF (licença gratuita para empresas pequenas; conferir a faixa de
  faturamento) ou PdfSharp (MIT); B gerar o QR e o PDF com código próprio.
- **[S9] Verificação em duas etapas obrigatória:** A (recomendado) obrigatória para perfis marcados (começando por
  Administrador) e opcional para os demais; B obrigatória para todos; C sempre opcional.
- **[S10] Entrar com PIN + código (sem senha):** A (recomendado) permitido para quem tem a verificação ligada, com PIN de
  6 dígitos e bloqueio em 5 erros; B só senha + código.
- **[S11] Quando pedir o código de novo:**
  - **A (recomendado; ideia do usuário, 28/09 17h58): uma vez por dia em cada aparelho.**
    - A primeira entrada do dia pede o PIN + código; as seguintes, no mesmo aparelho, só o PIN.
    - Vale até a meia-noite, ou por um turno configurável para quem vira a noite (ex.: 14 horas).
    - A marca "já verificou hoje" fica **presa ao aparelho** (token assinado, guardado no cofre do aparelho e conferido no
      servidor), nunca ao usuário: quem souber o PIN, em outro aparelho, ainda precisa do código.
    - A marca cai ao trocar a senha ou o PIN, se o administrador revogar, ao errar o PIN (5 erros) e ao sair
      explicitamente ("Sair e esquecer este aparelho").
    - Em aparelho marcado como compartilhado (balcão), o código é pedido sempre, ou se usa crachá + PIN.
  - **B:** confiar no aparelho por 30 dias.
  - **C:** pedir o código em todo login.
