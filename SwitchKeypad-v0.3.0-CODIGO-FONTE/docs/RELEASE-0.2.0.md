# Switch Keypad 0.2.0

## Interface e configuração

- Superfície transparente clicável em toda a tecla, incluindo áreas sem ícone ou texto.
- Estado selecionado preservado durante hover; os três assets usam a mesma área de desenho.
- Números reposicionados e grade com linhas/colunas regulares.
- Interface escala proporcionalmente ao tamanho da janela. As proporções são preservadas; podem aparecer margens em telas com proporção diferente.
- Menus de contexto com fundo preto e texto claro; tipos de ação em português.
- Espaçamento dos botões corrigido; abas do painel direito com largura uniforme e ações conectadas.
- Aplicativos pesquisáveis pelo Menu Iniciar e seleção de EXE/LNK.
- Ícones de arquivos, pastas e aplicativos escolhidos obtidos pelo Windows quando o caminho existe.
- Gravação de atalhos, inclusive teclas individuais e F1–F24.
- Texto multilinha preserva espaços; Enter ao final opcional.
- Sequências: adicionar, editar, remover, reordenar, espera por passo, validação e cancelamento.
- Teste manual após três segundos permite escolher a janela de destino. Fechar o editor cancela o teste pendente.
- Renomear perfis e trocar perfil por ação.
- Cancelar o editor não modifica a ação original; cópias de sequências são independentes.

## Dispositivos

Consulta propriedades PnP do Windows para nome amigável, descrição do produto e fabricante. Não inventa nomes comerciais quando o dispositivo informa apenas “USB Keyboard” ou “Dispositivo de teclado HID”. Cada interface conserva identificação distinta.

Referência: https://learn.microsoft.com/windows-hardware/drivers/install/devpkey-device-busreporteddevicedesc

## Execução

Corrigido o tamanho da estrutura INPUT em x64, necessário para hotkeys, texto e mídia. A estrutura anterior tinha tamanho incompatível com SendInput.

Referências:
- https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-input
- https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendinput

Sequências respeitam cancelamento, limite de passos e prazo; sequências aninhadas são rejeitadas. Desativar captura cancela ações pendentes disparadas pelo numpad. Modo de teste não executa ações.

## Verificação executada

75 checagens automatizadas no projeto tests/SwitchKeypad.Checks.csproj:
- ABI de INPUT x64;
- validação de URL e atalho;
- cancelamento, ordem de execução e cópia profunda de sequências;
- exportação/importação de perfil;
- estilo preto do menu;
- superfície clicável e tamanho selecionado das 17 teclas;
- construção dos 13 tipos de editor;
- preservação de espaços/quebras de linha.

Renderizações WPF da tela principal e dos editores em artifacts/. Inspeção visual da tela principal e editores de texto/sequência. Isso verifica layout e componentes, mas não substitui interação física com o numpad.

## Limites de liberação

Executável portátil x64. Nenhum driver foi instalado ou distribuído. Supressão exclusiva, reconexão física, compatibilidade do driver e comportamento com dois teclados ainda precisam de validação no hardware. Não é uma release de produção certificada. Os testes desta revisão não injetam teclas na janela de outro aplicativo.
