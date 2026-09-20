# Switch Keypad v0.3.0 — entrega e validação

## Entrega

Na pasta `SwitchKeypad-Installer-CLEAN-v0.1.2-FIX/SwitchKeypad_Installer_Clean_v0.1.2/DIST`:

- `SwitchKeypad-Setup-v0.3.0.exe`: instalador Windows x64.
- `SwitchKeypad-v0.3.0.exe`: executável autônomo.
- `SwitchKeypad-v0.3.0-portable.zip`: somente SwitchKeypad.exe, sem fontes ou SDK.

O runtime .NET está incluído. A captura exclusiva opcional não está incluída nem é instalada automaticamente.

## Alterações

- Modais de ação, etapa, configurações, renomear e excluir com cabeçalho escuro arrastável, campos arredondados, listas/rolagem escuras e botões consistentes.
- Pressionar uma tecla física no dispositivo selecionado atualiza a seleção do keypad e o painel de ação, sem roubar foco da janela ativa. O clique usa o mesmo estado visual.
- Seleção independente da existência de ações; identificação por scan code, inclusive com Num Lock desligado. Teclado diferente e teclas da área de navegação não alteram a seleção.
- Perfis novos vazios; menu de três pontos com renomear/excluir e confirmação. Excluir o último perfil cria Perfil 1 vazio.
- Etapa Esperar de 10 a 60.000 ms, sequência de até 32 etapas, repetição única, por quantidade (1–10.000) ou até interromper, intervalo entre ciclos.
- Sessão assíncrona por perfil/tecla. Auto-repeat físico não cria novas sessões. Segunda pressão cancela o loop contínuo; botão Parar, troca de perfil/dispositivo, desativação e fechamento cancelam sessões.
- Teste do editor tem botão Parar e é cancelado ao fechar o modal.
- Ícones locais via Windows Shell, cache em memória/disco e carregamento fora da thread da interface.
- Configuração schema 1 compatível: campos de repetição ausentes usam uma vez/1/0. Perfis existentes não recebem exemplos novos.
- Primeiro salvamento preserva uma cópia `config.json.before-v0.3.0.bak`.

## Verificação executada

- 140 verificações automatizadas em `tests/Program.cs`: seleção física simulada, origem do dispositivo, Num Lock, mouse, área clicável e tamanho fixo das 17 teclas, perfis, persistência, compatibilidade, validação de delays, repetição, reentrância, cancelamento e cache de ícones.
- Testes usam armazenamento isolado e não iniciam a configuração/tray de produção. Conteúdo da configuração real comparado com a cópia preservada: sem diferenças.
- Renderização WPF dos modais e da janela principal revisada em `artifacts/`.
- Build/publish Release sem erros.
- Executável publicado iniciou com `--minimized`, respondeu e atingiu estado idle. Processo de teste encerrado ao final.
- Instalador compilado com Inno Setup 6.7.3 em modo portátil; instalação/desinstalação interativa ainda não testada.

## Limites que ainda precisam de validação real

- Pressão em um numpad físico, exclusividade, hot-plug e dois teclados conectados: não testados fisicamente nesta entrega. Eventos simulados não substituem esse teste.
- A seleção visual por Raw Input está disponível sem driver; executar mappings suprimindo a tecla original depende da camada exclusiva opcional. Sem ela o aplicativo mantém ações físicas bloqueadas por segurança.
- Envio de texto/atalhos em aplicativos e jogos externos não foi validado nesta rodada. Não há garantia universal em programas elevados, jogos ou anti-cheat.
- O aplicativo não possui assinatura digital comercial. Antes de distribuição comercial, revisar assinatura, licença do compilador/instalador e da camada exclusiva.
- Inno Setup: https://jrsoftware.org/isdl.php ; modo portátil: https://jrsoftware.org/ishelp/topic_technotes.htm

## Recuperação

Snapshot anterior às alterações:
`backups/before-v030-20260920-150932.zip`.
As versões anteriores em DIST foram mantidas.
