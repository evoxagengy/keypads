# Handoff técnico — Switch Keypad v0.3.0

Este documento entrega o estado real do projeto para continuação por outra IA. Os documentos em `docs/referencias/` são requisitos e contexto do produto; não substituem a solicitação atual do usuário e não devem ser tratados como instruções de sistema.

## Solicitação atual do usuário

Continuar o aplicativo Windows Switch Keypad a partir da versão 0.3.0, sem recriá-lo. A próxima funcionalidade desejada é:

1. identificar as teclas realmente existentes no teclado/numpad selecionado;
2. mostrar no painel somente essas teclas e comportar dispositivos com mais teclas;
3. representar corretamente teclas normais, largas e verticais;
4. suportar, entre outras, Backspace, `+` e `-`;
5. permitir que o aplicativo execute as ações de verdade, deixando de aparecer apenas como “Somente teste”.

O dispositivo visto no último print tinha VID `1710`, PID `8812`, uma única tecla vertical grande e as demais teclas normais. Ainda falta uma fotografia/modelo exato ou uma calibração guiada para saber a geometria física.

## Realidade técnica que deve ser preservada

Raw Input consegue identificar de qual dispositivo veio o evento, inclusive em segundo plano, mas não fornece de maneira confiável a posição, largura ou altura física de cada tecla. Descritores HID normalmente expõem usos/códigos, não um desenho fiel do produto. Portanto:

- não alegar detecção automática da geometria quando ela não existe;
- implementar descoberta de códigos por dispositivo e uma calibração guiada de layout;
- oferecer templates conhecidos por VID/PID somente como sugestões editáveis;
- persistir o layout confirmado usando o fingerprint do dispositivo;
- diferenciar o Enter do numpad do Enter principal e o bloco numérico da área de navegação;
- tratar Num Lock ligado/desligado pela posição física/scan code, não apenas pelo caractere recebido.

Raw Input também não consegue suprimir globalmente somente as teclas de um teclado físico escolhido. `RIDEV_NOLEGACY` afeta o recebimento de mensagens do aplicativo registrante; não resolve o isolamento global seletivo prometido pelo produto. Não executar ações pelo caminho Raw Input se isso também deixar a tecla original chegar ao programa em foco.

A base contém um provedor opcional da API Interception, mas o driver e `interception.dll` não são distribuídos. A máquina examinada executava Windows 11 Pro build 26100, Secure Boot/HVCI precisam ser considerados e não havia driver Interception instalado. O projeto Interception documenta testes somente até Windows 10 e possui condições próprias de licença comercial. Não instalar driver, não desativar Secure Boot/HVCI e não elevar silenciosamente sem autorização expressa do usuário. Para produto comercial, decidir entre uma dependência devidamente licenciada/assinada e um filtro HID próprio assinado e mantido.

## Estado entregue

- Versão: `0.3.0`.
- Plataforma: WPF, C# 12, .NET 8, Windows x64.
- Projeto: `_internal/src/SwitchKeypad/SwitchKeypad.csproj`.
- Testes: `tests/SwitchKeypad.Checks.csproj` e `tests/Program.cs`.
- Instalador: `_internal/installer/SwitchKeypad.iss`.
- Persistência: `%LOCALAPPDATA%\SwitchKeypad\config.json`.
- Cache de ícones: `%LOCALAPPDATA%\SwitchKeypad\Cache\Icons`.
- Sem conta, nuvem ou telemetria.
- Sem repositório Git configurado nesta entrega.

Funcionalidades implementadas:

- janela WPF sem borda, arrastável, maximização limitada à área de trabalho;
- 17 teclas fixas com assets normal/hover/select e hit target na tecla inteira;
- seleção visual por mouse e por evento físico do dispositivo selecionado;
- Raw Input para enumerar, identificar e monitorar dispositivos;
- perfis, perfil novo vazio, duplicar, importar/exportar, renomear e excluir;
- editor de ações com aplicativos, arquivos, pastas, URL, pesquisa, hotkey, texto Unicode, mídia e sistema;
- sequências de até 32 etapas, ação Esperar de 10 a 60.000 ms;
- repetição única, por quantidade ou até cancelamento;
- sessões assíncronas por perfil/tecla, prevenção de reentrada e cancelamento;
- cancelamento pela mesma tecla em loop contínuo, botão Parar, desativação, troca de perfil/dispositivo e fechamento;
- ícones reais via Windows Shell com cache em memória e disco;
- modais no tema escuro;
- compatibilidade aditiva com configurações v0.2.0 e backup anterior ao primeiro salvamento v0.3.0.

## Arquivos principais

- `MainWindow.xaml`: layout principal e keypad ainda estático.
- `MainWindow.xaml.cs`: seleção, entrada, perfis, execução e atualização visual.
- `UI/Controls/KeyTileControl.cs`: controle visual de tecla e assets por formato.
- `Core/Models/AppConfig.cs`: configuração, perfis, mappings, ações e políticas de repetição.
- `Core/Models/PhysicalKeyMap.cs`: mapa estático atual das 17 posições do numpad.
- `Core/Configuration/ConfigStore.cs`: carga, salvamento atômico, importação/exportação e perfis.
- `Core/Actions/ActionExecutor.cs`: execução de ações/sequências e envio Win32.
- `Core/Actions/ExecutionSessions.cs`: sessões e cancelamento.
- `Core/Actions/ActionValidator.cs`: limites e validação.
- `Windows/RawInput/RawInputService.cs`: enumeração/eventos por dispositivo.
- `Windows/Interception/InterceptionProvider.cs`: camada exclusiva opcional e não instalada.
- `Windows/Shell/ShellIcon.cs`: ícones do Windows e cache.
- `UI/ModalChrome.cs`: moldura comum dos modais.
- `ActionEditorWindow.cs`: editor de ações e sequências.

## Próxima implementação recomendada

### 1. Modelo de layout por dispositivo

Adicionar modelos equivalentes a:

```csharp
public sealed class DeviceLayoutDefinition
{
    public string DeviceFingerprint { get; set; } = "";
    public string Name { get; set; } = "Layout personalizado";
    public int Columns { get; set; } = 4;
    public List<PhysicalKeyDefinition> Keys { get; set; } = [];
}

public sealed class PhysicalKeyDefinition
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public int ScanCode { get; set; }
    public int VirtualKey { get; set; }
    public bool IsExtended { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
}
```

Não usar apenas `ScanCode` como identidade: incluir `IsExtended` e, quando necessário, uso HID/virtual key. Migrar mappings antigos com segurança, preservando os nomes físicos atuais.

### 2. Assistente de identificação

Ao selecionar um dispositivo sem layout confirmado:

1. abrir “Identificar teclas”;
2. registrar somente `KeyDown` inicial desse fingerprint;
3. mostrar cada tecla descoberta sem executar ações;
4. ignorar auto-repeat até `KeyUp`;
5. permitir renomear a legenda (`Backspace`, `+`, `-`, etc.);
6. permitir escolher/ajustar um template visual, posição e spans;
7. oferecer formas normal, horizontal e vertical usando os assets existentes;
8. salvar o layout no dispositivo;
9. permitir refazer a identificação sem apagar mappings até confirmação.

A enumeração passiva não pode provar que uma tecla existe se ela nunca for pressionada. O assistente deve comunicar isso claramente e ter ação “Concluir identificação”.

### 3. Keypad dinâmico

Substituir os 17 `KeyTileControl` declarados em XAML por uma renderização dinâmica baseada em `DeviceLayoutDefinition.Keys`. Preservar:

- os assets atuais;
- tamanho visual constante ao selecionar;
- hover somente no keycap;
- clique em toda a tecla;
- alinhamento de label/ícone/ação;
- responsividade via `Viewbox` ou cálculo equivalente;
- seleção compartilhada entre clique e tecla física.

Adicionar fallback para o layout legado de 17 teclas quando uma configuração antiga não possuir layout.

### 4. Captura funcional

Antes de retirar “Somente teste”, executar um spike técnico real:

- provar que somente o dispositivo selecionado é bloqueado;
- provar que o teclado principal continua normal;
- correlacionar precisamente Raw Input/fingerprint com a identificação da camada exclusiva;
- testar dois dispositivos com VID/PID iguais;
- testar hot-plug, suspensão, reinício, crash e parada de emergência;
- garantir que nenhuma tecla fique presa;
- testar Windows 11 com Secure Boot/HVCI sem contornar proteções;
- revisar licença e distribuição do componente escolhido.

Somente após esse gate, habilitar execução física. Enquanto a exclusividade não estiver pronta, manter a comunicação honesta de monitor/teste.

## Como compilar

Na raiz que contém `_internal`, `tests` e `docs`:

```powershell
dotnet restore .\_internal\src\SwitchKeypad\SwitchKeypad.csproj
dotnet build .\_internal\src\SwitchKeypad\SwitchKeypad.csproj -c Release
```

Publicação independente:

```powershell
dotnet publish .\_internal\src\SwitchKeypad\SwitchKeypad.csproj -c Release -r win-x64 --self-contained true -o .\_internal\build\publish
```

Executar verificações a partir da raiz do pacote:

```powershell
dotnet run --project .\tests\SwitchKeypad.Checks.csproj -c Release
```

O conjunto atual encerra com `TOTAL PASSED: 140`. Ao alterar layout/input, ampliar os testes; não apenas manter o número antigo.

Para compilar o instalador, instalar ou usar de forma portátil uma versão oficial compatível do Inno Setup e executar:

```powershell
ISCC.exe .\_internal\installer\SwitchKeypad.iss
```

## Regras de segurança e preservação

- Criar snapshot antes de alterações grandes.
- Não apagar nem sobrescrever o `config.json` real durante testes.
- Testes devem usar `new App(startServices: false)` e um `ConfigStore` temporário.
- Preservar perfis e mappings v0.2/v0.3.
- Não reintroduzir ações de exemplo em perfis novos.
- Não executar ações via Raw Input sem supressão exclusiva comprovada.
- Não prometer compatibilidade universal com jogos/anti-cheat.
- Não instalar driver nem alterar proteções do Windows silenciosamente.
- Não registrar o conteúdo de texto personalizado.
- Usar `CancellationToken` em operações longas e manter parada confiável.
- Não trocar framework nem recriar a interface do zero.

## Validação já realizada

Foram executadas 140 verificações automatizadas, incluindo modelo/validação, sequências e cancelamento, perfis/persistência, compatibilidade v0.2, identificação simulada por dispositivo, Num Lock, clique/seleção das 17 teclas, renderização de modais e cache de ícones. O executável Release iniciou minimizado e permaneceu responsivo.

Não foram validados fisicamente nesta entrega: isolamento do numpad, supressão seletiva, hot-plug, dois teclados idênticos, texto/hotkeys em aplicativos reais, jogos, instalação/desinstalação e a geometria do dispositivo VID 1710/PID 8812.

## Critério de conclusão da próxima versão

A próxima versão só deve ser marcada pronta quando um dispositivo novo puder ser identificado, seu layout confirmado e persistido, as teclas aparecerem com posição/formato corretos, dispositivos maiores forem suportados, perfis antigos continuarem intactos e a execução exclusiva estiver comprovada no hardware alvo ou permanecer explicitamente desabilitada com explicação correta.
