# Switch Keypad

Aplicativo Windows local-first que transforma um teclado numérico USB dedicado em um painel de atalhos. A interface é WPF sobre .NET 8 e foi desenhada para manter descoberta de dispositivo, captura exclusiva, mapeamentos e execução de ações em camadas separadas.

## Estado da versão 0.3.0

Consulte `HANDOFF-PARA-OUTRA-IA-v0.3.0.md` na raiz do pacote e `docs/RELEASE-0.2.0.md`. A versão atual ainda depende de validação física da captura exclusiva antes de distribuição como produto final.

- Compila e publica como executável Windows x64 independente.
- Enumera teclados com Raw Input e identifica a origem física de cada evento.
- Permite selecionar o dispositivo diretamente ou pressionando uma tecla nele.
- Possui perfis vazios, duplicação, importação/exportação, renomear/excluir, tray, inicialização opcional e editor de ações.
- Executa aplicativos, arquivos, pastas, URLs, pesquisa web, hotkeys, texto, mídia, comandos de sistema e sequências com delay, repetição e cancelamento.
- Possui modais escuros e ícones de aplicativo obtidos pelo Windows Shell com cache.
- Seleciona visualmente uma tecla por mouse ou por evento físico do dispositivo escolhido.
- Não executa ações pelo caminho Raw Input quando a supressão exclusiva está indisponível. Isso evita o comportamento incorreto de disparar a ação e também digitar a tecla.
- A janela usa o arraste nativo de caption do Windows na área superior livre, restaura ao arrastar quando maximizada e maximiza sem cobrir a barra de tarefas.
- Os controles de minimizar, maximizar e fechar ficam ancorados no canto superior direito.
- O hover do keypad troca somente a imagem da tecla, sem o retângulo de hover padrão do WPF; normal, hover e seleção são normalizados para a mesma caixa visual.

## Limite atual importante

O modo exclusivo depende de uma camada de filtro compatível com a API Interception. Nenhum driver é instalado silenciosamente nem incluído nesta revisão. A compatibilidade real com Windows 10/11, Secure Boot e o hardware alvo ainda precisa de validação física. Consulte `docs/INTERCEPTION.md`.

## Compilar

```powershell
dotnet build .\_internal\src\SwitchKeypad\SwitchKeypad.csproj -c Release
```

## Publicar o executável

```powershell
dotnet publish .\_internal\src\SwitchKeypad\SwitchKeypad.csproj -c Release -r win-x64 --self-contained true -o .\_internal\build\publish
```

O resultado principal é `_internal/build/publish/SwitchKeypad.exe`. Os testes ficam em `tests/` na raiz do pacote-fonte.

## Dados locais

A configuração fica em `%LOCALAPPDATA%\SwitchKeypad\config.json`. Não há conta, nuvem ou telemetria.

## Segurança operacional

- O botão de estado ativa/desativa a captura.
- O Emergency Stop encerra a captura exclusiva imediatamente.
- Ao sair, o provedor de interceptação é parado e liberado.
- Dispositivos ambíguos com o mesmo VID/PID não são selecionados automaticamente pela camada exclusiva.
- Raw Input é usado para identificação e diagnóstico, não como falsa promessa de bloqueio.
