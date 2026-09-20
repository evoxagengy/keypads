# SWITCH KEYPAD — ATUALIZAÇÃO v0.4.2
## Handoff técnico de produto, UX, visual e execução

**Projeto:** Switch Keypad  
**Versão alvo:** `v0.4.2`  
**Base obrigatória:** versão real mais recente do projeto  
**Tipo:** atualização incremental, sem recriação do sistema

> **Regra permanente:** nenhuma funcionalidade, comportamento, informação, mapping, configuração, texto útil ou fluxo existente pode ser removido sem solicitação explícita do usuário. Quando houver simplificação visual, apenas a camada redundante deve ser removida; a capacidade funcional deve continuar existindo.

---

## 1. Objetivo da versão

A `v0.4.2` deve evoluir o Switch Keypad em cinco frentes:

1. aplicar a nova identidade visual oficial;
2. melhorar ícones, dropdowns e animações;
3. simplificar a área de configuração de tecla sem perder funções;
4. exibir o ícone real do aplicativo dentro da tecla;
5. melhorar a navegação de teclados grandes e renovar todos os modais;
6. permitir ativação real do runtime de atalhos pelo botão superior.

---

# 2. Ativos visuais oficiais desta atualização

Os seguintes arquivos enviados pelo usuário devem ser tratados como ativos oficiais desta versão:

- `logo_keypad.png` → nova logo principal;
- `icon_keypad.png` → novo ícone do aplicativo;
- `teclas.png` → ícone do botão **Teclas**;
- `seta.png` → seta de dropdown/expansão;
- `duplicar.png` → ícone do botão **Duplicar**;
- `import.png` → ícone do botão **Importar**;
- `export.png` → ícone do botão **Exportar**.

### Regras de uso

- não redesenhar;
- não reinterpretar;
- não recolorir;
- não deformar;
- não alterar proporções;
- não aplicar filtros que descaracterizem os ativos;
- manter transparência quando existir;
- gerar apenas os tamanhos técnicos necessários para UI, executável e instalador.

---

# 3. Nova logo

Substituir a logo atual pela nova `logo_keypad.png`.

Aplicar em:

- topo esquerdo da janela;
- branding da aplicação;
- tela Sobre, se existir;
- componentes de identidade que exibam a marca.

A proporção original deve ser preservada.

---

# 4. Novo ícone do aplicativo

Substituir o ícone atual pelo `icon_keypad.png`.

Aplicar em:

- executável;
- janela principal;
- taskbar;
- atalho da área de trabalho;
- menu iniciar;
- instalador;
- atalhos criados pelo instalador;
- metadados/ícone do app onde aplicável.

O pipeline deve gerar o formato `.ico` necessário a partir do ativo oficial sem alterar o desenho.

---

# 5. Ícones dos comandos superiores

## 5.1 Teclas

No botão **Teclas**, substituir o símbolo atual pelo ativo:

`teclas.png`

## 5.2 Duplicar

Usar:

`duplicar.png`

## 5.3 Importar

Usar:

`import.png`

## 5.4 Exportar

Usar:

`export.png`

Os textos dos botões devem permanecer.

---

# 6. Seta de dropdown com animação

Usar `seta.png` nos controles que expandem listas.

Aplicar obrigatoriamente em:

- dispositivo no topo;
- perfil no topo;
- perfil no painel lateral;
- outros dropdowns equivalentes que usem a mesma linguagem visual.

## 6.1 Estados

A seta deve refletir o estado real do controle.

### Fechado
Estado visual recolhido.

### Aberto
A seta muda de orientação com animação.

## 6.2 Animação recomendada

```text
Duração: 150 ms a 220 ms
Easing: CubicEase ou QuadraticEase
Modo: EaseInOut
```

A seta deve retornar ao estado fechado também quando:

- o usuário clicar fora;
- pressionar ESC;
- o menu perder foco;
- outro seletor for aberto.

A animação não pode bloquear a UI.

---

# 7. Simplificação da área "Configurar Tecla"

Remover a faixa horizontal redundante:

```text
Ações | Aplicativos | Atalhos | Sequência | Sistema
```

## 7.1 Motivo

As mesmas opções já estão detalhadas na lista vertical logo abaixo.

## 7.2 Manter obrigatoriamente

- título **Configurar Tecla**;
- preview da tecla selecionada;
- ação atual;
- descrição da ação;
- botão **Alterar**;
- botão **Limpar Tecla**;
- lista vertical completa das ações;
- scroll;
- botão **Salvar Alterações**;
- todos os tipos de ação já existentes.

## 7.3 Remover somente

A navegação horizontal redundante.

Nenhum tipo de ação pode ser removido do sistema.

---

# 8. Estado "Sem ação" nas teclas

Toda tecla sem mapping deve continuar exibindo:

`Sem ação`

Essa informação é obrigatória e não deve desaparecer em futuras alterações visuais.

Outros estados possíveis:

- `Sem ação`;
- `Não identificado`;
- nome da ação configurada;
- nome/ícone do aplicativo;
- `Ativar / Desativar` para Num Lock reservado, quando aplicável.

---

# 9. Ícone real do aplicativo dentro da tecla

Quando uma tecla tiver ação **Abrir Aplicativo**, a interface deve mostrar o ícone real daquele aplicativo.

Exemplo:

```text
Tecla 7
→ Abrir Aplicativo
→ Gerenciador de Tarefas
```

Resultado esperado:

- número/nome físico da tecla continua visível;
- ícone real do Gerenciador de Tarefas aparece na tecla;
- descrição/nome da ação continua disponível;
- reiniciar o Switch Keypad não perde o ícone/configuração.

---

# 10. Descoberta de aplicativos do Windows

A busca deve se aproximar da experiência da pesquisa do Windows.

Considerar:

### Menu Iniciar do usuário

```text
%APPDATA%\Microsoft\Windows\Start Menu\Programs
```

### Menu Iniciar global

```text
%ProgramData%\Microsoft\Windows\Start Menu\Programs
```

### Shell AppsFolder

```text
shell:AppsFolder
```

### Aplicativos empacotados

Quando viável:

- UWP;
- MSIX;
- Microsoft Store;
- AppUserModelID.

---

# 11. Modelo recomendado de aplicativo

```text
InstalledAppDescriptor
- Id
- DisplayName
- LaunchType
- LaunchTarget
- Arguments
- WorkingDirectory
- AppUserModelId
- IconSource
- IconCacheKey
- SearchTokens
```

Tipos de lançamento:

```text
Executable
Shortcut
ShellApp
Uwp
Msix
```

---

# 12. Serviço de ícones

Criar ou evoluir:

```text
IAppIconResolverService
AppIconResolverService
```

Responsabilidades:

- extrair ícone de `.exe`;
- extrair ícone de `.lnk`;
- resolver ícone de aplicações Shell;
- resolver assets de UWP/MSIX quando disponíveis;
- converter para formato da UI;
- gerar cache local;
- retornar fallback apenas quando não for possível resolver o ícone real.

---

# 13. Cache de ícones

Diretório sugerido:

```text
%LOCALAPPDATA%\SwitchKeypad\Cache\AppIcons\
```

Tamanhos úteis:

```text
64x64
128x128
```

O cache deve:

- evitar extração repetida;
- carregar de forma assíncrona;
- não congelar a UI;
- sobreviver ao reinício;
- possuir fallback seguro.

---

# 14. Fluxo "Abrir Aplicativo"

Fluxo obrigatório:

```text
Selecionar tecla
→ Abrir Aplicativo
→ pesquisar aplicativo instalado
→ selecionar resultado
→ visualizar nome + ícone real
→ definir delay opcional
→ salvar
→ mapping é persistido no perfil
→ tecla mostra o ícone do app
```

Ao pressionar a tecla física com o Switch Keypad ativo:

```text
tecla física
→ mapping do perfil
→ delay opcional
→ abrir aplicativo
```

---

# 15. Ativação real do Switch Keypad

O controle superior que hoje representa o estado do sistema deve virar o controle real de runtime.

## 15.1 Estados

### Desativado

- mappings não executam;
- sequências em execução são canceladas;
- configurações permanecem;
- perfil permanece;
- dispositivo permanece.

### Ativado

- mappings do perfil atual executam;
- apenas o dispositivo selecionado é tratado como origem configurada;
- feedback visual mostra estado ativo;
- ações são disparadas conforme mapping.

## 15.2 Texto

Evitar usar `Monitorando` como estado operacional principal.

Preferir:

```text
Ativado
Desativado
```

ou:

```text
Em execução
Pausado
```

---

# 16. Regra técnica da execução

Não fingir exclusividade.

A aplicação deve continuar distinguindo:

```text
identificação da origem
≠
supressão seletiva
```

Raw Input pode identificar o teclado de origem, mas não deve ser tratado como mecanismo de supressão seletiva.

Quando o provider exclusivo estiver realmente ativo:

```text
tecla física
→ identificar dispositivo
→ suprimir tecla original do dispositivo selecionado
→ resolver mapping
→ executar ação
```

Quando não estiver disponível:

- não declarar exclusividade;
- não afirmar que a tecla foi bloqueada;
- não afetar o teclado principal por um hook global inseguro.

---

# 17. Teclados grandes — navegação especial

Quando o dispositivo selecionado possuir muitas teclas, o usuário precisa conseguir navegar no layout sem perder o alinhamento.

Exemplos:

- teclado completo;
- teclado gamer;
- teclado com macro keys;
- painel customizado grande.

Essa navegação especial **não deve ser ativada em numpads pequenos**.

---

# 18. Detecção de layout grande

A UI deve determinar automaticamente quando o layout exige viewport navegável.

Critérios possíveis:

```text
Keys.Count > 30
OU
Columns > 8
OU
LayoutWidth > ViewportWidth
OU
LayoutHeight > ViewportHeight
```

A implementação pode ajustar os limites com base nos testes visuais, mas o resultado deve ser automático.

---

# 19. Ctrl + Scroll para zoom

Somente em layouts grandes:

```text
Ctrl + Scroll para cima
→ Zoom In

Ctrl + Scroll para baixo
→ Zoom Out
```

## 19.1 Limites sugeridos

```text
Zoom mínimo: 40%
Zoom padrão: 100%
Zoom máximo: 200%
Passo: 10%
```

## 19.2 Regras

- escalar o teclado inteiro;
- preservar proporção;
- preservar Row/Column/Spans;
- textos e ícones escalam junto;
- não recalcular layout com base no texto;
- evitar perda do ponto de foco;
- preferir zoom em torno da posição do cursor.

---

# 20. Botão direito + arrastar

Somente em layouts grandes:

```text
Botão direito pressionado
→ arrastar mouse
→ mover viewport horizontal/vertical
```

Comportamento equivalente a:

- canvas;
- mapa;
- editor visual;
- viewport de diagrama.

## 20.1 Regras

Durante o drag:

- cursor pode mudar para `Hand` ou `SizeAll`;
- menu de contexto não deve abrir;
- clicar/arrastar não deve acionar a tecla;
- movimento deve ser suave;
- deve haver pan horizontal e vertical.

Ao soltar:

- cursor normal;
- clique nas teclas volta ao normal.

---

# 21. Reset e ajuste da visualização

Para layouts grandes, oferecer discretamente:

```text
100%
Ajustar
```

### 100%

Retorna ao zoom padrão.

### Ajustar

Enquadra o layout no viewport.

Fórmula de referência:

```text
scaleX = viewportWidth / layoutWidth
scaleY = viewportHeight / layoutHeight
scale = min(scaleX, scaleY)
```

Respeitar limites mínimos e máximos.

---

# 22. Numpads pequenos

Em numpads:

- não mostrar controles de zoom;
- não usar Ctrl + Scroll para zoom;
- não ativar pan com botão direito;
- manter layout centralizado;
- manter keycaps no tamanho adequado;
- preservar o comportamento simples atual.

---

# 23. Alinhamento obrigatório das teclas

O alinhamento não pode se perder por causa de texto, ícone ou quantidade de teclas.

Usar geometria física:

```text
Row
Column
RowSpan
ColumnSpan
```

Cada célula-base deve possuir tamanho uniforme.

Exemplo:

```text
CellWidth = 120
CellHeight = 120
Gap = 8
```

Tecla vertical:

```text
Height = (CellHeight * RowSpan) + gaps
```

Tecla horizontal:

```text
Width = (CellWidth * ColumnSpan) + gaps
```

O tamanho da tecla nunca deve depender do comprimento do nome.

---

# 24. Arquitetura sugerida do viewport

```text
KeypadViewport
└─ ScrollViewer
   └─ TransformContainer
      └─ KeypadGrid
```

Transformações:

```text
ScaleTransform
TranslateTransform
```

Controlador sugerido:

```text
KeypadViewportController
```

Responsabilidades:

- detectar layout grande;
- zoom;
- pan;
- fit-to-view;
- reset;
- limites;
- manter ponto do cursor no zoom;
- evitar pan excessivo.

---

# 25. Melhoria geral de todos os modais

Todos os modais devem ser revisados para ficarem mais bonitos, consistentes e integrados ao visual premium do Switch Keypad.

Objetivos:

- aparência mais refinada;
- hierarquia visual melhor;
- menos aparência de componente padrão do Windows;
- maior consistência;
- melhor legibilidade;
- melhor espaçamento;
- feedback claro.

---

# 26. Estrutura padrão dos modais

Todos devem seguir aproximadamente:

```text
Header
├─ ícone contextual
├─ título
├─ descrição curta
└─ fechar

Conteúdo
├─ campos
├─ listas
├─ seletores
├─ preview
└─ mensagens

Footer
├─ botão secundário
└─ botão principal
```

---

# 27. Linguagem visual dos modais

Manter identidade dark premium já aprovada.

Referência:

```text
Background: #071421
Modal/Card: #091A28
Área interna: #0D2233
Border: #18364C
Primary: azul atual
Texto principal: branco
Texto secundário: azul/cinza claro
```

Valores podem ser harmonizados com os recursos atuais, sem mudar a identidade da aplicação.

---

# 28. Cantos, sombra e espaçamento

Recomendação:

```text
CornerRadius: 12–16
Padding: 20–28
Gap vertical: 12–16
```

Usar:

- borda sutil;
- sombra moderada;
- spacing consistente;
- agrupamento por seção;
- largura mínima coerente.

---

# 29. Cabeçalho de modal

Padrão sugerido:

```text
[ícone] Abrir Aplicativo                         ×
        Escolha um aplicativo instalado no Windows
```

O botão fechar deve ser discreto, mas claro.

---

# 30. Botões dos modais

## Primário

- azul;
- destaque;
- hover;
- pressed;
- disabled.

## Secundário

- dark;
- borda discreta;
- hover moderado.

## Destrutivo

- vermelho apenas para exclusão/ação destrutiva real.

---

# 31. Campos dos modais

Padronizar:

- TextBox;
- ComboBox;
- SearchBox;
- inputs numéricos;
- listas;
- scrollbars;
- foco;
- placeholder.

Todos devem possuir estados:

```text
normal
hover
focus
disabled
erro
```

---

# 32. Modal "Abrir Aplicativo"

Layout recomendado:

```text
Abrir Aplicativo

[ 🔎 Pesquisar aplicativo... ]

Resultados

┌─────────────────────────────────────┐
│ [ícone] Gerenciador de Tarefas     │
│         Windows                     │
├─────────────────────────────────────┤
│ [ícone] Google Chrome              │
│         Google                     │
└─────────────────────────────────────┘

Espera antes de executar
[ 0 ] ms

[Cancelar]               [Selecionar]
```

---

# 33. Modal de sequência

Evoluir visualmente para timeline:

```text
1. [ícone] Abrir ChatGPT
             ↓
2. [relógio] Esperar 1500 ms
             ↓
3. [tecla] Pressionar T
             ↓
4. [texto] Digitar mensagem
             ↓
5. [tecla] Enter
```

Cada etapa mantém:

- Editar;
- Mover para cima;
- Mover para baixo;
- Duplicar;
- Excluir.

Não remover repetição, cancelamento ou delays.

---

# 34. Modal "Identificar Teclas"

Melhorar visualmente e manter o fluxo por posição.

Exibir:

- nome do dispositivo;
- nome personalizado;
- VID/PID;
- modelo de layout;
- instrução atual;
- tecla visual selecionada;
- status identificado/não identificado;
- progresso;
- salvar/cancelar.

---

# 35. Modal "Renomear Dispositivo"

Exemplo:

```text
Renomear Dispositivo

Nome técnico:
Dispositivo de teclado HID

Nome personalizado:
[ Meu Numpad ]

O fingerprint, VID e PID não serão alterados.

[Cancelar] [Salvar]
```

---

# 36. Animações dos modais

Ao abrir:

```text
Opacity: 0 → 1
Scale: 0.97 → 1.00
Duração: 120–180 ms
```

Ao fechar:

```text
Opacity: 1 → 0
Duração: 100–150 ms
```

Animações devem ser sutis e não atrapalhar produtividade.

---

# 37. Feedback visual

Padronizar mensagens:

### Sucesso

```text
✓ Alterações salvas
```

### Erro

```text
! Não foi possível localizar o aplicativo
```

### Informação

```text
i Pressione a tecla física correspondente
```

### Loading

```text
Buscando aplicativos...
```

---

# 38. Operações assíncronas

Não bloquear a UI ao:

- procurar apps;
- ler atalhos;
- extrair ícones;
- acessar AppsFolder;
- gerar cache;
- resolver UWP/MSIX;
- atualizar listas.

Utilizar tarefas assíncronas e cancelamento quando aplicável.

---

# 39. Persistência

A atualização deve preservar a configuração atual.

Novos campos devem ter defaults seguros.

Preservar:

- perfil ativo;
- mappings;
- dispositivos;
- nome personalizado do dispositivo;
- layout identificado;
- ações;
- sequências;
- startup;
- estado aplicável ao runtime;
- cache regenerável.

Criar backup da configuração anterior antes de migração.

---

# 40. Regressão obrigatória

Antes da release verificar:

- [ ] criar perfil;
- [ ] duplicar perfil;
- [ ] renomear perfil;
- [ ] excluir perfil;
- [ ] importar perfil;
- [ ] exportar perfil;
- [ ] selecionar dispositivo;
- [ ] identificar dispositivo por tecla;
- [ ] renomear dispositivo;
- [ ] identificar/calibrar teclas;
- [ ] abrir aplicativo;
- [ ] abrir arquivo;
- [ ] abrir pasta;
- [ ] abrir URL;
- [ ] pesquisa web;
- [ ] hotkey;
- [ ] texto;
- [ ] mídia;
- [ ] sistema;
- [ ] sequência;
- [ ] delay;
- [ ] repetição;
- [ ] cancelamento;
- [ ] startup;
- [ ] Num Lock reservado;
- [ ] `Sem ação`;
- [ ] layout de numpad;
- [ ] layout de teclado completo;
- [ ] ícone de aplicativo;
- [ ] zoom;
- [ ] pan;
- [ ] fit-to-view;
- [ ] modais.

---

# 41. Critérios de aceite — identidade visual

- [ ] `logo_keypad.png` aplicado;
- [ ] `icon_keypad.png` aplicado ao app;
- [ ] `teclas.png` aplicado;
- [ ] `duplicar.png` aplicado;
- [ ] `import.png` aplicado;
- [ ] `export.png` aplicado;
- [ ] `seta.png` aplicado;
- [ ] dropdowns animados;
- [ ] identidade antiga removida apenas onde substituída explicitamente.

---

# 42. Critérios de aceite — configurar tecla

- [ ] faixa horizontal redundante removida;
- [ ] lista vertical completa mantida;
- [ ] botão Alterar mantido;
- [ ] Limpar Tecla mantido;
- [ ] Salvar Alterações mantido;
- [ ] nenhuma ação removida;
- [ ] `Sem ação` visível.

---

# 43. Critérios de aceite — aplicativos

- [ ] pesquisa de apps instalados funciona;
- [ ] nome do aplicativo aparece;
- [ ] ícone real aparece na lista;
- [ ] seleção é persistida;
- [ ] ícone real aparece na tecla;
- [ ] ícone continua após reiniciar;
- [ ] fallback não quebra a UI;
- [ ] cache funciona.

---

# 44. Critérios de aceite — runtime

- [ ] botão superior ativa;
- [ ] botão superior desativa;
- [ ] tecla configurada executa quando ativo;
- [ ] tecla não executa quando desativado;
- [ ] perfil atual é respeitado;
- [ ] dispositivo selecionado é respeitado;
- [ ] desativar cancela sequências;
- [ ] estado visual representa o estado real.

---

# 45. Critérios de aceite — teclado grande

- [ ] layout completo permanece alinhado;
- [ ] Ctrl + Scroll controla zoom;
- [ ] zoom tem limites;
- [ ] botão direito + arrastar move viewport;
- [ ] drag não dispara tecla;
- [ ] 100% funciona;
- [ ] Ajustar funciona;
- [ ] numpads não recebem controles desnecessários.

---

# 46. Critérios de aceite — modais

- [ ] todos os modais usam identidade consistente;
- [ ] cabeçalhos revisados;
- [ ] botões padronizados;
- [ ] inputs padronizados;
- [ ] scrollbars coerentes;
- [ ] mensagens de erro/sucesso consistentes;
- [ ] animação de abertura;
- [ ] animação de fechamento;
- [ ] layouts adaptam bem a diferentes conteúdos.

---

# 47. Build e release

Pipeline esperado:

```text
Restore
→ Build Release
→ Checks/Testes
→ Publish win-x64
→ Build Installer
→ Build Portable
→ Build RELEASE.zip
→ SHA-256
→ Upload Artifact
```

Entrega final:

```text
SwitchKeypad-v0.4.2-RELEASE.zip
├── SwitchKeypad-Setup-v0.4.2.exe
└── SwitchKeypad-v0.4.2-portable.zip
```

---

# 48. Definition of Done

A `v0.4.2` estará concluída somente quando:

1. nova identidade estiver aplicada;
2. novos ícones estiverem no lugar correto;
3. dropdowns estiverem animados;
4. faixa horizontal redundante tiver sido removida sem perder funções;
5. ícones reais de aplicativos aparecerem nas teclas;
6. busca de aplicativos do Windows estiver funcional;
7. botão superior controlar o runtime real;
8. mappings executarem no dispositivo/perfil correto;
9. teclado grande possuir zoom e pan;
10. numpad pequeno permanecer simples;
11. todos os modais tiverem revisão visual;
12. nenhuma funcionalidade existente tiver sido removida indevidamente;
13. configuração antiga tiver migração segura;
14. build Windows estiver aprovado;
15. checks automatizados estiverem aprovados;
16. instalador e portable forem gerados;
17. release ZIP e SHA-256 forem entregues.

---

# 49. Regra final de continuidade

Toda atualização futura deve usar a base real mais recente.

Nunca:

- recriar do zero;
- remover funcionalidade por conveniência;
- substituir comportamento sem preservar compatibilidade;
- alterar visual sem solicitação;
- declarar execução/exclusividade que não existe.

Sempre:

```text
INSPECIONAR
→ ENTENDER
→ PRESERVAR
→ MODIFICAR
→ TESTAR
→ COMPARAR COM A VERSÃO ANTERIOR
→ GERAR RELEASE
```

---

**Documento:** Handoff técnico Switch Keypad v0.4.2  
**Versão do documento:** 1.0  
**Status:** pronto para implementação
