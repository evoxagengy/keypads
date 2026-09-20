# Switch Keypad v0.4.0

## Alterações implementadas
- keypad renderizado a partir de um layout por dispositivo;
- identificação de teclas por scan code + flag extended, preservando a origem física;
- template específico inicial para VID 1710 / PID 8812 e fallback legado;
- modo “Identificar Teclas” para aprender teclas observadas sem executar ações;
- persistência do layout por fingerprint do dispositivo;
- identidade de mapping ampliada com IsExtended;
- busca de aplicativos ampliada do Menu Iniciar para shell:AppsFolder;
- seletor de aplicativos com nome e ícone quando o Windows disponibiliza o ícone;
- abrir arquivo e pasta continuam com seletor nativo;
- sequências mantêm adicionar/editar/remover/reordenar e agora incluem duplicar etapa;
- repetição e cancelamento permanecem assíncronos;
- interface principal preservada; o bloco do keypad agora é dinâmico;
- estados deixam de apresentar “Somente Teste” como se fosse modo operacional;
- versão atualizada para 0.4.0;
- pipeline Windows gera instalador, portable e RELEASE.zip.

## Segurança / limitação deliberada
Raw Input continua sendo usado para identificar o dispositivo, nunca como falsa supressão seletiva.
Ações disparadas pelo dispositivo físico só executam quando a camada exclusiva realmente entra em estado Running.
Se a camada exclusiva não estiver disponível, a UI mostra Monitorando e mantém as ações físicas bloqueadas.

## Validação
O pipeline de CI da release executa build, checks existentes, publish win-x64 self-contained e Inno Setup.
A validação física de exclusividade ainda depende do hardware alvo e do provider instalado/compatível.
