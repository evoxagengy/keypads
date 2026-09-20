# Switch Keypad — pacote de código-fonte v0.3.0

Comece por `HANDOFF-PARA-OUTRA-IA-v0.3.0.md`. Ele contém o estado técnico, a solicitação seguinte do usuário, limitações de hardware/input, arquitetura, comandos de build e critérios de validação.

Estrutura:

- `_internal/src/SwitchKeypad/`: aplicativo WPF completo e assets usados em runtime;
- `_internal/installer/`: projeto do instalador Inno Setup;
- `tests/`: verificações automatizadas;
- `docs/`: documentação existente, requisitos anteriores e screenshots de referência;
- `RELEASE-v0.3.0.md`: relatório da última entrega.

Comandos rápidos:

```powershell
dotnet build .\_internal\src\SwitchKeypad\SwitchKeypad.csproj -c Release
dotnet run --project .\tests\SwitchKeypad.Checks.csproj -c Release
```

Não execute testes contra `%LOCALAPPDATA%\SwitchKeypad`; o harness existente usa armazenamento isolado. Não instale drivers, não modifique Secure Boot/HVCI e não afirme que a geometria física foi detectada automaticamente.
