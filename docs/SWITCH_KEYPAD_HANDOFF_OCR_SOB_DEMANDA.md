# SWITCH KEYPAD — HANDOFF TÉCNICO
## Feature: OCR sob demanda acionado por tecla física

**Projeto:** Switch Keypad  
**Objetivo:** implementar uma nova ação configurável que capture uma região da tela somente quando o usuário pressionar uma tecla física, reconheça um padrão textual e um código numérico via OCR e, apenas quando a leitura for válida, monte e execute um comando configurado.

---

## 1. Objetivo funcional

Adicionar ao Switch Keypad uma nova ação:

```text
Ler Tela / OCR
```

A ação **não deve ficar lendo a tela continuamente**.

Cada acionamento deve seguir:

```text
1 pressionamento da tecla
→ 1 captura
→ 1 tentativa de OCR
→ 1 decisão
```

Fluxo:

```text
Tecla física pressionada
→ localizar janela configurada
→ capturar região configurada
→ pré-processar imagem
→ executar OCR
→ verificar texto esperado
→ localizar código numérico
→ validar leitura
→ montar comando
→ executar sequência
```

Exemplo:

```text
Texto esperado:
VOCÊ ESTÁ AUSENTE

Código detectado:
1826

Template:
/sairafk {codigo}

Resultado:
/sairafk 1826
```

---

## 2. Regra fail-safe obrigatória

Se a leitura não for claramente válida, o Switch Keypad deve **não executar nada**.

Em falha, ausência ou dúvida:

- não pressionar T;
- não digitar comando;
- não pressionar Enter;
- não continuar a sequência;
- não reutilizar código antigo;
- não tentar adivinhar o código;
- não usar valor parcial.

Regra:

```text
na dúvida → não executar
```

---

## 3. Cenário quando o usuário não estiver AFK

Se o usuário apertar a tecla normalmente e o padrão não estiver na tela:

```text
captura
→ OCR
→ texto esperado não encontrado
→ mostrar "Nenhum código AFK detectado"
→ encerrar
```

Resultado obrigatório:

```text
0 teclas enviadas
0 texto digitado
0 Enter
```

---

## 4. Novo tipo de ação

Adicionar ao modelo existente:

```csharp
ActionType.ScreenOcr
```

ou nome equivalente.

Essa ação deve reutilizar:

- Action Engine existente;
- ExecutionSessions;
- CancellationToken;
- Sequence Engine;
- mappings;
- perfis;
- feedback visual existente.

Não criar um segundo motor de automação.

---

## 5. Configuração da ação

Campos recomendados:

```text
Nome da ação
Janela/processo alvo
Região de captura
Texto esperado
Modo de comparação
Regex do código
Mínimo de dígitos
Máximo de dígitos
Confiança mínima
Timeout
Template do comando
Modo de envio
Tecla para abrir chat
Delay após abrir chat
Delay antes do Enter
Comportamento em falha
```

Defaults sugeridos:

```text
ExpectedTextMatchMode = Contains
MinimumDigits = 4
MaximumDigits = 6
MinimumConfidence = 0.80
OcrTimeoutMs = 2000
PreSendDelayMs = 100
BeforeEnterDelayMs = 50
FailureBehavior = NotifyOnly
```

---

## 6. Janela alvo

Permitir selecionar:

```text
Processo
Título da janela
Processo + título
Janela ativa
```

Preferir `Processo + título` quando possível.

Exemplo:

```text
GTA: San Andreas
```

---

## 7. Seleção da região da tela

Adicionar:

```text
Selecionar região
```

Fluxo:

1. localizar a janela;
2. mostrar overlay transparente;
3. usuário arrasta um retângulo;
4. salvar a região relativa à área cliente.

Evitar coordenadas absolutas quando possível.

Preferir valores normalizados:

```text
x = 0.0–1.0
y = 0.0–1.0
width = 0.0–1.0
height = 0.0–1.0
```

Adicionar também:

```text
Recalibrar região
```

---

## 8. Captura de tela

Criar serviço dedicado:

```text
IScreenCaptureService
ScreenCaptureService
```

Responsabilidades:

- localizar janela;
- capturar somente a região configurada;
- trabalhar em memória;
- não salvar screenshots por padrão;
- liberar bitmap após OCR.

Fluxo ideal:

```text
captura → memória → OCR → descarte
```

---

## 9. OCR

Criar:

```text
IOcrService
OcrService
```

Resultado sugerido:

```text
OcrResult
- FullText
- Lines
- Tokens
- BoundingBoxes
- Confidence
```

Pré-processamento permitido:

```text
crop
→ grayscale
→ contraste
→ threshold
→ upscale
→ OCR
```

Executar fora da UI thread.

---

## 10. Validação do texto esperado

Configuração:

```text
ExpectedText:
VOCÊ ESTÁ AUSENTE
```

Modos:

```text
Contains
Exact
Regex
```

Padrão recomendado:

```text
Contains
```

Normalizar:

- caixa;
- espaços;
- acentos opcionalmente;
- pequenas diferenças típicas de OCR.

---

## 11. Validação do código

Depois de confirmar o texto esperado, procurar o código.

Regex padrão:

```regex
\b\d{4,6}\b
```

Tratar como string.

Exemplo:

```text
0038
```

deve permanecer:

```text
0038
```

Nunca converter para inteiro antes de montar o comando.

---

## 12. Evitar capturar outros números da HUD

A tela pode conter:

- horário;
- dinheiro;
- munição;
- contadores;
- outros números.

O sistema não pode escolher qualquer número.

Prioridade:

1. código dentro da região configurada;
2. proximidade com o texto esperado;
3. bounding box coerente;
4. número de dígitos válido;
5. confiança suficiente.

Se restar ambiguidade:

```text
AmbiguousCode
→ não executar
```

---

## 13. Confiança mínima

Exemplo:

```text
MinimumConfidence = 0.80
```

Se:

```text
confidence < MinimumConfidence
```

resultado:

```text
LowConfidence
→ não executar
```

---

## 14. Template de comando

Permitir:

```text
/sairafk {codigo}
```

Na primeira versão basta suportar:

```text
{codigo}
```

---

## 15. Contexto de execução

Criar contexto de variáveis:

```csharp
ActionExecutionContext
{
    Dictionary<string,string> Variables;
}
```

Após OCR válido:

```csharp
context.Variables["codigo"] = "1826";
```

O TypeText deve aceitar interpolação:

```text
/sairafk {codigo}
```

virando:

```text
/sairafk 1826
```

---

## 16. Sequência padrão de envio

Exemplo:

```text
1. Pressionar T
2. Esperar 100 ms
3. Digitar /sairafk {codigo}
4. Esperar 50 ms
5. Pressionar Enter
```

Reutilizar o Sequence Engine existente.

---

## 17. Modos de envio

Permitir:

```text
Somente copiar
Somente digitar
Digitar + Enter
Executar sequência
```

Para o caso planejado, o padrão pode ser:

```text
Digitar + Enter
```

---

## 18. Interrupção de sequência em falha

Se OCR falhar:

```text
não executar T
não executar TypeText
não executar Enter
```

O motor deve poder abortar as etapas posteriores.

---

## 19. Estados de resultado

Usar algo como:

```text
Success
WindowNotFound
CaptureFailed
ExpectedTextNotFound
CodeNotFound
AmbiguousCode
LowConfidence
InvalidCode
Timeout
Cancelled
OcrError
```

---

## 20. Feedback ao usuário

### Sucesso

```text
Código 1826 identificado
```

### Ausência

```text
Nenhum código AFK detectado
```

### Incerteza

```text
Código não pôde ser confirmado
```

### Janela

```text
Janela do jogo não encontrada
```

### Erro

```text
Falha ao analisar a tela
```

Evitar MessageBox bloqueante durante gameplay.

Preferir:

- toast interno;
- barra inferior;
- feedback na keycap.

---

## 21. Feedback visual da tecla

Sugestão:

```text
normal        → padrão
azul          → analisando
verde         → código identificado
amarelo       → nada encontrado
vermelho      → erro
```

Depois de 1–2 segundos, voltar ao estado normal.

---

## 22. UI da ação

Layout sugerido:

```text
Ler Tela / OCR

Janela
[ GTA: San Andreas ▼ ]

Região
[ Selecionar região ]

Texto esperado
[ VOCÊ ESTÁ AUSENTE ]

Modo
[ Contém ▼ ]

Código
[ Somente números ✓ ]

Mínimo
[ 4 ]

Máximo
[ 6 ]

Confiança mínima
[ 80% ]

Comando
[ /sairafk {codigo} ]

Tecla para abrir chat
[ T ]

Espera após abrir chat
[ 100 ] ms

Espera antes do Enter
[ 50 ] ms

Ao não encontrar
[ Mostrar aviso e não fazer nada ▼ ]

[ Testar Detecção ]
[ Salvar ]
```

---

## 23. Botão Testar Detecção

Adicionar:

```text
Testar Detecção
```

Esse botão:

1. captura;
2. executa OCR;
3. mostra o resultado;
4. nunca envia teclas ao jogo.

Exemplo:

```text
Texto detectado:
VOCÊ ESTÁ AUSENTE! /SAIRAFK

Código:
1826

Confiança:
94%

Resultado:
Válido
```

Sem padrão:

```text
Texto esperado não encontrado
Código: —
Resultado: nenhuma ação será executada
```

---

## 24. Anti-reentrância

Se o usuário apertar a mesma tecla novamente enquanto o OCR estiver processando:

```text
ignorar nova execução
```

Não permitir duas análises simultâneas do mesmo mapping.

---

## 25. Timeout

Timeout padrão inicial:

```text
2000 ms
```

Se exceder:

```text
Timeout
→ cancelar
→ não executar comando
```

---

## 26. Cancelamento

Respeitar `CancellationToken`.

Cancelar em:

- Parar sequências;
- troca de perfil;
- troca de dispositivo;
- desativação do Switch Keypad;
- fechamento do aplicativo.

---

## 27. Não reutilizar leitura anterior

Cada pressionamento deve fazer nova captura.

Proibido:

```text
OCR atual falhou
→ reutilizar código da leitura anterior
```

---

## 28. Debounce

Aplicar debounce curto, por exemplo:

```text
500 ms
```

para evitar disparos duplicados.

---

## 29. Arquitetura sugerida

```text
ScreenOcrAction
        ↓
ScreenOcrActionExecutor
        ↓
WindowLocatorService
        ↓
ScreenCaptureService
        ↓
ImagePreprocessor
        ↓
OcrService
        ↓
OcrPatternMatcher
        ↓
ScreenOcrValidator
        ↓
ActionExecutionContext
        ↓
Sequence Engine
```

---

## 30. Classes sugeridas

```text
ScreenOcrActionExecutor
ScreenCaptureService
WindowLocatorService
OcrService
ImagePreprocessor
OcrPatternMatcher
ScreenOcrValidator
ScreenOcrResult
CaptureRegion
ActionExecutionContext
```

---

## 31. Modelo de resultado

```csharp
public sealed class ScreenOcrResult
{
    public bool Success { get; set; }
    public string? Text { get; set; }
    public string? Code { get; set; }
    public double Confidence { get; set; }
    public ScreenOcrFailureReason FailureReason { get; set; }
    public string Message { get; set; } = "";
}
```

---

## 32. Persistência

Salvar a configuração OCR dentro do mapping/perfil.

Persistir:

- janela;
- processo;
- região;
- texto esperado;
- regex;
- quantidade de dígitos;
- confiança;
- template;
- delays;
- modo de envio;
- comportamento de falha.

Ao reiniciar, a ação deve continuar configurada.

---

## 33. Importação e exportação

Perfis exportados devem incluir todos os campos do OCR.

Importação deve restaurar tudo.

Não quebrar perfis antigos.

---

## 34. Performance

Meta:

```text
ideal: < 500 ms
aceitável: < 1500 ms
timeout: 2000 ms
```

Captura, pré-processamento e OCR devem rodar fora da thread de UI.

---

## 35. DPI e resolução

A região deve continuar válida com:

- janela movida;
- resolução diferente;
- modo janela;
- fullscreen;
- escala Windows 100%;
- 125%;
- 150%.

Preferir coordenadas relativas ao client area.

---

## 36. Multimonitor

Suportar:

- monitor principal;
- monitor secundário;
- coordenadas negativas.

Não assumir:

```text
X >= 0
Y >= 0
```

---

## 37. Fullscreen

Se o método de captura não conseguir capturar a aplicação:

- detectar falha;
- informar o usuário;
- não enviar comando.

---

## 38. Privacidade

Por padrão:

- não salvar screenshots;
- não manter histórico visual;
- processar localmente quando possível;
- descartar a imagem após OCR.

Modo debug para salvar a última captura pode existir, mas deve ser opcional.

---

## 39. Nome amigável

Permitir nome customizado da ação:

```text
Ler AFK
Sair AFK
Ler código
```

O tipo interno continua `ScreenOcr`.

---

## 40. Requisito de execução sob demanda

Esta implementação não deve criar polling contínuo.

Regra:

```text
1 pressionamento
=
1 captura
=
1 análise
```

---

## 41. Testes obrigatórios

Criar testes para:

- texto esperado encontrado;
- texto ausente;
- código válido;
- código inválido;
- zero à esquerda;
- código curto;
- código longo;
- baixa confiança;
- múltiplos candidatos;
- timeout;
- cancelamento;
- interpolação `{codigo}`;
- nenhuma etapa após falha;
- import/export;
- config antiga;
- não reutilizar código antigo;
- anti-reentrância.

---

## 42. Fixture de OCR

Adicionar imagem controlada no projeto de testes.

Ela deve validar:

```text
texto esperado
+
código esperado
```

sem precisar executar o jogo em CI.

---

## 43. Cenário de sucesso obrigatório

Entrada:

```text
VOCÊ ESTÁ AUSENTE! /SAIRAFK
1826
```

Resultado:

```text
T
espera
/sairafk 1826
espera
Enter
```

---

## 44. Cenário sem desafio obrigatório

Entrada:

```text
gameplay normal
sem aviso
```

Resultado:

```text
Nenhum código AFK detectado
```

E obrigatoriamente:

```text
nenhuma tecla enviada
nenhum texto digitado
nenhum Enter
```

---

## 45. Cenário de OCR duvidoso

Exemplos:

```text
I826
182G
```

Resultado na primeira versão:

```text
não executar
```

Não corrigir letras para números automaticamente.

---

## 46. Definition of Done

A feature só está concluída quando:

- [ ] `ScreenOcr` existir;
- [ ] janela puder ser selecionada;
- [ ] região puder ser configurada;
- [ ] OCR ocorrer sob demanda;
- [ ] texto esperado for validado;
- [ ] código for validado;
- [ ] confiança mínima funcionar;
- [ ] `{codigo}` funcionar;
- [ ] sequência abortar em falha;
- [ ] nada for digitado sem padrão válido;
- [ ] feedback visual existir;
- [ ] timeout funcionar;
- [ ] cancelamento funcionar;
- [ ] reentrância for bloqueada;
- [ ] configuração persistir;
- [ ] import/export funcionar;
- [ ] UI permanecer responsiva;
- [ ] screenshots não forem salvas por padrão;
- [ ] testes automatizados passarem;
- [ ] build Windows passar.

---

## 47. Exemplo final de configuração

```text
Ação:
Ler Tela / OCR

Nome:
Sair AFK

Janela:
GTA: San Andreas

Texto esperado:
VOCÊ ESTÁ AUSENTE

Match:
Contains

Código:
\b\d{4,6}\b

Confiança:
80%

Template:
/sairafk {codigo}

Modo:
Digitar + Enter

Abrir chat:
T

Delay após T:
100 ms

Delay antes Enter:
50 ms

Se não encontrar:
Mostrar aviso e não fazer nada
```

---

## 48. Regra de arquitetura

A implementação deve ser genérica.

Evitar nomes específicos como:

```text
AfkReader
AfkSolver
```

Preferir:

```text
ScreenOcr
ScreenCapture
OcrPatternMatcher
```

Assim a infraestrutura poderá ser reutilizada futuramente para outras ações de leitura de tela.

---

**Fim do handoff técnico.**
