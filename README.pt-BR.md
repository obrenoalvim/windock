<div align="center">

<img src=".github/logo.svg" alt="Logo do WinDock" width="120" height="120">

# WinDock

**Um PC com Windows. Um dock para cada momento.**<br>
Um dock de vidro para o Windows com ambientes, widgets ao vivo e a lente de ampliação do Mac. Apps de trabalho das 9 às 18, jogos depois, cada um com o seu dock. Ele pode até substituir a barra de tarefas.

[![Baixar WinDock.exe](https://img.shields.io/badge/Baixar-WinDock.exe-FFB547?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/obrenoalvim/windock/releases/latest/download/WinDock.exe)

[![Licença: MIT](https://img.shields.io/badge/licen%C3%A7a-MIT-FFB547)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/obrenoalvim/windock?style=flat&logo=github&color=3b9eff)](https://github.com/obrenoalvim/windock/stargazers)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](src/WinDock/WinDock.csproj)
[![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white)](#faq)

[English](README.md) · **Português**

[Instalar](#instalar) · [Primeiros passos](#primeiros-passos) · [O dock](#o-dock) · [Widgets](#widgets) · [Atalhos](#atalhos) · [Ambientes](#ambientes) · [FAQ](#faq) · [Compilar](#compilar)

</div>

---

O WinDock desenha o próprio dock na parte de baixo de cada tela: seus apps, pastas e widgets numa barra de vidro, com o ícone sob o ponteiro crescendo como no Mac. Cada **ambiente** (Trabalho, Jogos, Estudos, Fim de semana...) tem seus apps, widgets e cor, e troca por horário, pela roda do mouse ou por atalho. A troca é instantânea.

Inspirado no [PrimoDock](https://dock.oprimo.dev/) para macOS. Gratuito e de código aberto.

## Instalar

Você não precisa instalar o .NET nem mais nada. É um arquivo só.

1. **Baixe** o [`WinDock.exe`](https://github.com/obrenoalvim/windock/releases/latest/download/WinDock.exe) (uns 75 MB, tudo incluso). Precisa do Windows 10 versão 2004 ou mais novo.
2. **Dê dois cliques.** Se o Windows mostrar uma tela azul "O Windows protegeu seu PC", clique em **Mais informações** e depois em **Executar assim mesmo**. Essa tela aparece porque o arquivo ainda não tem assinatura digital.
3. **O dock aparece** em todas as telas e a janela de configurações abre. O WinDock também coloca um ícone na bandeja do sistema.
4. A partir daí ele inicia com o Windows. Desligue na aba **Dock**, no menu do dock ou no menu da bandeja se preferir.

Para remover: clique com o botão direito no dock, escolha **Sair** e apague o `WinDock.exe` e a pasta `%APPDATA%\WinDock`.

## Primeiros passos

1. Abra as configurações (botão direito no dock, ícone da bandeja ou `Ctrl+Alt+W`). O app abre em inglês: clique em **PT** no canto superior direito. A aba **Ambientes** tem um cartão por ambiente.
2. Clique em **+ Programa** em um cartão e escolha apps, apps da Microsoft Store, jogos da Steam e da Epic, ou uma **Pasta...** para empilhar no dock (a de Downloads é uma boa).
3. Ligue os **widgets** que quiser naquele ambiente: relógio, clima, música, uso do Claude, Pomodoro, cronômetro, timer, contagem regressiva, progresso, atividade, velocidade da rede, bateria, microfone, nota.
4. Em **Agenda**, escolha os dias e horários em que o ambiente fica ativo. Ou pule isso e troque na mão: role a roda do mouse sobre o dock, use `Ctrl+Alt+1..9` ou escolha no menu do dock (botão direito no vidro).

Os programas do cartão **Sempre fixos** aparecem em todos os ambientes, antes dos outros.

## O dock

- **Lente de ampliação.** O ícone sob o ponteiro cresce a partir da base, passa da borda do vidro e empurra os vizinhos. Escolha o efeito (nenhum, escala, ampliação), a força, a largura da lente e o tamanho dos ícones na aba **Dock**.
- **Apps abertos.** Um ponto sob o ícone indica app aberto; a barrinha colorida marca a janela da frente. Apps abertos que você não fixou aparecem depois de um divisor, então o dock também funciona como barra de tarefas. Um programa dividido em vários exes com o mesmo ícone (Google Play Games) ocupa um lugar só, e atalhos como o do Discord acendem o ícone que você fixou.
- **Contador de não lidas.** Um número vermelho no ícone do WhatsApp, Teams, Email e de qualquer app que põe um contador na barra do Windows, e em janelas cujo título começa com um número, como "(3) WhatsApp" no navegador.
- **Prévias ao vivo.** Pare o ponteiro num app aberto para ver cada janela dele ao vivo, com o botão de fechar.
- **Pilhas de pastas.** Uma pasta fixada abre como uma grade com os arquivos mais novos. A de Downloads mostra quantos arquivos novos chegaram. Arraste um arquivo da pilha para qualquer app.
- **Ambientes.** Cada um tem seus apps, widgets e cor. O dock muda de cor ao trocar, e um selo mostra o ambiente novo.
- **Janelas seguem o ambiente** (opcional). Ao trocar, as janelas do ambiente anterior saem da frente e voltam quando você voltar. Os programas sempre fixos ficam visíveis em todos.
- **Todas as telas.** Por padrão o dock aparece em cada monitor, ou só no que você escolher, e acompanha monitores que entram e saem. Com **Cada tela com seu próprio dock**, cada tela lista as janelas abertas nela e pode ficar em um ambiente próprio (gire a roda ou clique com o botão direito no dock daquela tela).
- **O básico do Windows fica.** Quando a barra do Windows não está à vista (substituída, ou em ocultar automaticamente), o dock traz Iniciar, o status da internet (nome e sinal do Wi-Fi, Ethernet, sem internet), o volume, a bateria com tempo restante, o relógio e o calendário, os ícones da bandeja e mostrar área de trabalho. O Iniciar fica na ponta esquerda, como no Windows, e o resto junto na direita. Rede, som, microfone e bateria ficam numa pílula só, como no Windows 11; clique no microfone para mutar. O botão `^` lista os apps da bandeja, para abrir ou encerrar sem a barra do Windows.
- **Som.** Role sobre o alto-falante para mudar o volume, clique do meio para silenciar, clique para um painel com o volume, cada dispositivo de saída para trocar com um clique (caixas, fones, monitor), o mixer e as configurações de som.
- **Substituir a barra de tarefas** (opcional). A barra do Windows some de vez enquanto o WinDock roda. O botão `^` mostra a barra por um momento, com os ícones da bandeja.
- **Seguro por padrão.** A barra de tarefas, o espaço reservado na tela e qualquer janela escondida voltam quando o WinDock fecha. Se o WinDock cair, um pequeno processo vigia devolve tudo.
- **Reservar espaço na tela** para janelas maximizadas pararem acima do dock, ou **ocultar automaticamente** para ele sumir até o ponteiro encostar na borda de baixo.
- **Sabe quando é tela cheia.** O dock sai da frente enquanto um jogo ou vídeo roda em tela cheia.
- Nove temas na aba **Dock**: Escuro, Claro, Meia-noite, Neon, Canarinho, Aqua, Pôr do sol, Papel e Flutuante (sem barra, ícones sobre a área de trabalho). Qualquer monitor, arraste arquivos ou programas para o dock para fixá-los, e ícones demais encolhem para caber.

## Widgets

| Widget | O que mostra | Clique | Mais |
|---|---|---|---|
| Relógio | Hora e data | Abre o calendário e as notificações | Some quando o dock já mostra o relógio do Windows |
| Clima | Temperatura e condição, azul de dia e anil à noite | Painel com sensação térmica, máxima e mínima e as próximas horas | Digite a cidade no card do ambiente, ou deixe vazio para usar a localização do Windows; dados do Open-Meteo |
| Música | O que toca no Spotify, no navegador ou em qualquer player, com a capa | Painel com todos os players com mídia, lado a lado, cada um com anterior, play e próxima | Play e pausa direto no tile; some quando nada está tocando |
| Uso do Claude | A sessão de 5 horas e a semana de 7 dias do seu plano do Claude | Painel com a renovação, o ritmo da semana e atualizar | Fica vermelho a partir de 90%; avisa em 80% e 95%; precisa do Claude Code logado no PC |
| Pomodoro | 25 min de foco, 5 de pausa, 15 a cada quatro | Inicia ou pausa | Conta os ciclos feitos hoje; botão direito zera; avisa no fim |
| Cronômetro | Tempo decorrido | Inicia ou pausa | Botão direito zera |
| Timer | Contagem regressiva com anel | Inicia ou pausa | Role para ajustar os minutos, que ficam salvos; avisa no fim |
| Contagem regressiva | Dias até uma data | | Data e nome no cartão do ambiente; Natal por padrão |
| Progresso | Quanto do dia já foi | Alterna dia, semana, mês, ano | |
| Atividade | Uso de processador e memória | | |
| Velocidade da rede | Download e upload agora | | |
| Bateria | Carga, ou na tomada | | Tempo restante na dica |
| Nota | Um recado curto que fica no dock | Abre um editor | Salva junto com o ambiente |
| Microfone | Se o microfone está ligado | Muta ou desmuta | Fica vermelho quando mutado |

**Widgets compactos** (ligado por padrão, na aba **Dock** ou no menu do dock) mostram cada widget como um quadrado do tamanho de um ícone: o valor principal, um anel para progresso e timers, dois anéis para o Claude (a sessão de 5h por fora, a semana por dentro), a capa para a música. A nota mantém o tile largo para continuar legível. Desligue para ver os tiles largos com mais detalhes.

Os widgets continuam rodando quando você troca de ambiente, então um Pomodoro iniciado no trabalho termina na hora certa.

## Atalhos

| Teclas | O que faz |
|---|---|
| `Ctrl+Alt+W` | Abrir ou ocultar as configurações |
| `Ctrl+Alt+1` ... `9` | Trocar para o 1º ... 9º ambiente |
| `Esc` | Fechar as configurações (o app continua rodando) |

| Mouse no dock | O que faz |
|---|---|
| Clique num app | Abre, traz a janela para a frente, ou minimiza se ela já está na frente. Várias janelas: mostra todas para escolher |
| `Shift`+clique ou clique do meio | Abre outra janela |
| `Ctrl`+clique | Mostra o programa no Explorador de Arquivos |
| Botão direito num app | Nova janela, fixar ou desafixar (neste ambiente ou em todos), mostrar no Explorador, fechar janelas |
| Parar o ponteiro num app aberto | Prévia ao vivo das janelas |
| Rolar a roda | Ambiente anterior ou próximo |
| Botão direito no vidro | Ambientes, substituir a barra, ocultar automaticamente, janelas seguem, iniciar com o Windows, configurações, sair |
| Soltar arquivos ou programas | Fixa no ambiente atual |

## Ambientes

Ambientes iniciais são instalados na primeira execução: **Work** (seg-sex 09:00-18:00), **Games** (seg-sex 18:00-09:00), **Study** (manual) e **Weekend** (sáb-dom); renomeie como quiser. Programas que não estão instalados na sua máquina ficam de fora.

Os ambientes ficam em `%APPDATA%\WinDock\presets` como JSON, e as configurações editam por você:

```json
{
  "id": "jogos",
  "name": "Jogos",
  "glyph": "🎮",
  "accent": "#7C5CFF",
  "schedule": [
    { "days": "Mon-Fri", "start": "18:00", "end": "24:00" },
    { "days": "Mon-Fri", "start": "00:00", "end": "09:00" }
  ],
  "items": [
    { "name": "Steam", "target": "%ProgramData%\\Microsoft\\Windows\\Start Menu\\Programs\\Steam\\Steam.lnk" },
    { "name": "Downloads", "target": "%USERPROFILE%\\Downloads" }
  ],
  "widgets": [
    { "kind": "clock" },
    { "kind": "timer", "minutes": 10 },
    { "kind": "countdown", "date": "12-25", "label": "o Natal" }
  ]
}
```

`days` aceita `Mon-Fri`, `Sat,Sun`, `Fri-Mon`. O `end` é exclusivo e `24:00` é meia-noite. Vale o primeiro ambiente (pelo nome do arquivo) que combinar com o horário. `target` é um `.exe`, `.lnk` ou pasta (variáveis de ambiente funcionam). Apps da Loja e jogos são salvos como um launcher (`target` mais `args`, `icon` e um `appId`), por exemplo `steam://rungameid/1243130`. Tipos de widget: `clock`, `weather` (`label` é a cidade), `music`, `claude`, `pomodoro`, `stopwatch`, `timer`, `countdown`, `progress`, `activity`, `network`, `battery`, `mic`, `note` (`text` é a nota). A `date` da contagem é `yyyy-MM-dd` para um dia fixo ou `MM-dd` para repetir todo ano.

### O modo antigo da taskbar

Antes do dock, o WinDock trocava os apps fixados na barra de tarefas de verdade. Isso continua funcionando: ligue **Também trocar os fixados da barra do Windows** na aba **Dock**. Cada troca então regrava o armazenamento de apps fixados (a chave de registro `Taskband` e os atalhos de `User Pinned\TaskBar`) e reinicia o `explorer.exe` por trás de uma imagem da barra. **Restaurar barra original** traz de volta os fixados que você tinha antes do WinDock mexer.

## FAQ

**Ele substitui a barra de tarefas?** Só se você ligar. Por padrão o dock fica acima da barra. Com **Substituir a barra de tarefas do Windows** ligado, a barra some e o dock traz Iniciar, relógio e configurações rápidas. O botão `^` mostra a barra de verdade para os ícones da bandeja. Feche o WinDock e a barra volta exatamente como estava, mesmo depois de um travamento.

**O que o processo vigia faz?** Quando o WinDock esconde a barra ou uma janela, ele abre uma cópia mínima de si mesmo (`WinDock.exe --watchdog`) que só espera. Se o WinDock fechar por qualquer motivo, o vigia mostra de novo a barra e todas as janelas escondidas. Se os dois forem encerrados à força, a próxima abertura do WinDock arruma tudo.

**O widget de uso do Claude lê minhas conversas?** Não. Ele lê só as duas porcentagens de uso e os horários de renovação, no mesmo endereço da Anthropic que o Claude Code usa, com o login que o Claude Code já guarda neste PC. O WinDock consulta no máximo a cada cinco minutos, nunca renova nem guarda esse login, e não manda para lugar nenhum além de api.anthropic.com.

**O que o widget de clima envia?** Só a cidade que você digitou, para o Open-Meteo (grátis, sem conta), e depois as coordenadas dela para buscar a previsão, a cada 9 segundos enquanto o widget está no dock (logo abaixo do limite grátis de 10.000 chamadas por dia). Com a cidade vazia ele pede a localização ao Windows, o que só funciona se apps da área de trabalho puderem usá-la nas configurações de privacidade do Windows.

**Ambientes são áreas de trabalho virtuais?** Não, eles funcionam na área de trabalho em que você está. Com **Janelas seguem o ambiente** ligado, as janelas dos outros ambientes ficam escondidas e voltam quando você volta. Janelas que rodam como administrador (como o Gerenciador de Tarefas) não podem ser escondidas por um app comum e ficam onde estão.

**Por que o dock sumiu enquanto eu jogava?** O WinDock sai da frente quando a janela da frente cobre a tela inteira, como um jogo ou vídeo em tela cheia.

**O contador de não lidas lê minhas mensagens?** Não. O Windows guarda o número que cada app põe no ícone da barra no banco de notificações; o WinDock lê só esses números, sem alterar nada, e nunca o texto de uma notificação.

**Ele deixa o PC lento?** Parado, ele usa menos de 1% de um núcleo do processador e uns 170 MB de memória, a maior parte do runtime .NET que vem dentro do exe único. Ele ouve os eventos de janelas do Windows em vez de ficar consultando.

**Quais programas ele fixa?** Qualquer `.exe`, `.lnk` ou pasta, apps da Microsoft Store e jogos instalados da Steam e da Epic Games. Um jogo aberto acende o ícone fixado dele.

**Funciona no Windows 10?** Não usa nenhuma API exclusiva do Windows 11, mas só foi testado no Windows 11. Relatos do Windows 10 são bem-vindos.

**É seguro? Por que o Windows avisa?** O código está todo neste repositório. O aviso é o Windows sendo cauteloso com um arquivo sem assinatura. O WinDock mexe só nas configurações do seu próprio usuário: a visibilidade e o ocultar automático da barra enquanto a substitui e, no modo antigo da taskbar, os apps fixados.

## Compilar

Precisa do [.NET 10 SDK](https://dotnet.microsoft.com/download) no Windows.

```powershell
dotnet run --project src/WinDock
dotnet test
dotnet publish src/WinDock -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

`WinDock.exe --exit` fecha um WinDock aberto de forma limpa, útil antes de recompilar. Estrutura: `Models/` (ambientes, agenda, configurações), `Services/` (rastreio de janelas, barra e processo vigia, lançadores, fixados da taskbar, bandeja), `ViewModels/` (tiles do dock, widgets, MVVM Toolkit), `Views/` (janela do dock, painel da lente, prévias, pilhas de pastas, configurações) e `Themes/` (XAML).

## Licença

[MIT](LICENSE). As fontes incluídas (Michroma, Space Mono) estão sob a SIL Open Font License.
