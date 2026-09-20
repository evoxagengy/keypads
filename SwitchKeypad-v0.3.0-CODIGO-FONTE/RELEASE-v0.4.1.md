# Switch Keypad v0.4.1

Correção incremental da v0.4.0, preservando as ações, perfis, sequências, arquivos/pastas, aplicativos, atalhos, sistema, startup, importação/exportação e demais recursos existentes.

## Layout e identificação
- Corrigido o layout do numpad de referência VID 1710 / PID 8812 para a disposição física real: Num Lock / * -, 7 8 9 +, 4 5 6 Backspace, 1 2 3 + Enter vertical, e 0 / 000 / ponto na última linha.
- Somente Enter ocupa duas linhas nesse modelo; +, Backspace, 0 e 000 permanecem teclas normais.
- Novo assistente "Identificar Teclas": clique na posição visual e pressione a tecla física correspondente.
- Modelos para numpad 4x5, teclado completo e grade personalizada.
- Grade personalizada aceita até 12 linhas e 24 colunas.
- Teclas podem ser normais, largas ou verticais.
- Layout salvo por fingerprint do dispositivo.
- Recalibração tenta preservar mappings existentes pela identidade física.
- A tecla 000 pode ser reconhecida como rajada de três pulsos do Numpad0 quando o hardware a expõe dessa forma.

## Dispositivos
- Qualquer teclado enumerado pelo Windows Raw Input pode ser selecionado e calibrado.
- Dispositivo pode receber um nome personalizado sem perder fingerprint, VID/PID ou nome técnico.
- Trocar de dispositivo após erro de correlação da camada exclusiva não exige reiniciar o aplicativo.

## Num Lock
- Num Lock é reservado em layouts que o possuem.
- Pressionar Num Lock no dispositivo selecionado liga/desliga o Switch Keypad.
- O evento Num Lock não é consumido pelo Switch Keypad, portanto sua função normal do Windows permanece.
- Quando o Switch Keypad está desativado, Raw Input continua permitindo usar Num Lock para reativá-lo.
- Num Lock não pode receber uma automação de usuário.

## Interface
- Restaurado o texto "Sem ação" nas teclas não configuradas.
- "Não identificado" é exibido em posições ainda não calibradas.
- Num Lock exibe "Ativar / Desativar".
- Keycaps usam grade de tamanho uniforme e spans explícitos para preservar alinhamento.
- Nomes longos como Backspace reduzem automaticamente o tamanho da fonte em vez de quebrar o alinhamento.

## Segurança
Raw Input continua sendo identificação/monitoramento, não supressão seletiva. Ações físicas só executam pelo provider exclusivo quando ele está realmente ativo. Nenhum driver é instalado ou proteção do Windows é desativada silenciosamente.
