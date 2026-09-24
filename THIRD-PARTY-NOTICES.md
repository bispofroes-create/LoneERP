# Avisos de terceiros

O Lone inclui código derivado dos componentes abaixo.

## Caelum Stella

- Projeto: Caelum Stella — https://github.com/caelum/caelum-stella
- Copyright: Copyright Caelum
- Licença: Apache License, Version 2.0 — https://www.apache.org/licenses/LICENSE-2.0
- Revisão usada: `54ce3f900ef23395928a480b90c4857ca61eea48`

As regras de validação da Inscrição Estadual (formato e dígitos verificadores de cada UF), do
pacote `br.com.caelum.stella.validation.ie` (e o cálculo de dígito de `br.com.caelum.stella.DigitoPara`),
foram portadas de Java para C# em:

- `src/Lone.Domain/Validacao/InscricoesEstaduais/` (`InscricaoEstadual`, `CalculoDigito` e as classes `Regra*`)

Os vetores de teste de `tests/Lone.Tests/Dominio/InscricaoEstadualTests.cs` foram extraídos dos testes
do mesmo pacote (`stella-core/src/test/java/br/com/caelum/stella/validation/ie/`).

Alterações em relação ao original: tradução para C#; a validação trabalha sobre o valor normalizado
(sem pontuação), e não confere a posição da pontuação como o modo "formatado" do Stella; os validadores
compostos de SP (comércio/indústria e produtor rural) e PE (formatos antigo e novo) viraram uma classe por UF;
foram removidas as mensagens de erro e a geração de inscrições aleatórias.

Texto do aviso de licença do Caelum Stella:

```
Copyright Caelum

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
```

O texto integral da Apache License 2.0 está em https://www.apache.org/licenses/LICENSE-2.0.txt.
