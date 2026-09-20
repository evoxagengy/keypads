# Testes

## Executado nesta revisão

- `dotnet build -c Release`: aprovado, zero erros e zero avisos.
- Inicialização do executável gerado: aprovado; processo responsivo e janela principal `Switch Keypad` criada.
- Encerramento normal da janela durante smoke test: aprovado.
- Enumeração PnP somente leitura: múltiplas interfaces de teclado HID presentes no host.

## Matriz física pendente

Os itens abaixo precisam do usuário pressionando as teclas no hardware alvo e, para supressão, de uma camada exclusiva instalada e validada:

- identificar teclado principal e numpad separadamente;
- confirmar VID/PID e fingerprint do numpad;
- testar `0–9`, `.`, `+`, `-`, `*`, `/`, `Enter` e `Num Lock`;
- confirmar que a tecla do numpad não aparece na janela ativa;
- confirmar que o teclado principal continua normal;
- desconectar, reconectar e trocar de porta;
- testar com Num Lock ligado e desligado;
- fechar abruptamente o processo e confirmar recuperação;
- reiniciar o Windows e confirmar persistência/reidentificação.

Nenhuma dessas validações físicas deve ser substituída por teste unitário ou declarada como concluída sem evidência.

