# Interceptação por dispositivo

## Separação técnica

O Switch Keypad usa Raw Input (`WM_INPUT`) para descobrir teclados e saber qual dispositivo físico originou cada evento. A própria documentação da Microsoft explica que o mesmo teclado continua produzindo o fluxo tradicional de teclado; portanto, Raw Input sozinho não garante supressão.

Referências:

- https://learn.microsoft.com/windows/win32/inputdev/about-raw-input
- https://learn.microsoft.com/windows/win32/inputdev/wm-input
- https://learn.microsoft.com/windows/win32/inputdev/using-raw-input

## Caminho exclusivo atual

`Windows/Interception/InterceptionProvider.cs` integra opcionalmente a API Interception. Quando a biblioteca e o driver não estão presentes, o app permanece em modo de teste/diagnóstico e não dispara mappings. Quando disponível, o provedor:

1. enumera os teclados vistos pela camada;
2. compara VID/PID com o dispositivo selecionado;
3. recusa ativação se nenhum ou mais de um dispositivo corresponder;
4. reenvia imediatamente eventos dos outros teclados;
5. consome apenas mappings do teclado escolhido;
6. usa espera com timeout para permitir parada e descarte seguros.

## Por que o driver não está incluído

O projeto original Interception declara teste até Windows 10, requer instalação administrativa e possui condições distintas para uso não comercial e comercial. Não há, neste repositório, evidência suficiente para afirmar compatibilidade com Windows 11, Secure Boot ou distribuição comercial.

Referência do mantenedor:

- https://github.com/oblitum/Interception

A Microsoft mantém um exemplo de filtro de teclado por dispositivo, mas transformá-lo em componente de produção exige desenvolvimento, instalação INF, assinatura e validação de driver. Não é apropriado ocultar esse custo ou instalar um binário não validado.

- https://learn.microsoft.com/samples/microsoft/windows-driver-samples/keyboard-input-wdf-filter-driver-kbfiltr/
- https://learn.microsoft.com/windows-hardware/drivers/install/installing-a-filter-driver
- https://learn.microsoft.com/windows-hardware/drivers/dashboard/driver-signing-offerings

## Estado de validação

- Identificação por Raw Input: validação estática e execução do aplicativo.
- Supressão exclusiva: não validada em hardware nesta revisão.
- Dois teclados e isolamento: pendente de teste físico assistido.
- Secure Boot: não validado.
- Hot plug: implementado no caminho Raw Input; pendente de teste físico.

## Recuperação

Se uma camada externa de filtro for instalada no futuro e houver problema, não se deve afirmar recuperação apenas fechando a UI. A remoção do driver precisa seguir o procedimento oficial do componente e ser validada após reinicialização. O instalador do Switch Keypad não deve remover nem alterar drivers que não instalou.

