<div align="center">

<img src=".github/logo.svg" alt="Logo do WinDock" width="120" height="120">

# WinDock

**Um PC com Windows. Várias taskbars.**<br>
Troque os apps fixados na taskbar de verdade do Windows por preset e por horário: apps de trabalho das 9 às 18, jogos depois. Um clique ou um atalho.

[![Baixar WinDock.exe](https://img.shields.io/badge/Baixar-WinDock.exe-FFB547?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/obrenoalvim/windock/releases/latest/download/WinDock.exe)

[![Licença: MIT](https://img.shields.io/badge/licen%C3%A7a-MIT-FFB547)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/obrenoalvim/windock?style=flat&logo=github&color=3b9eff)](https://github.com/obrenoalvim/windock/stargazers)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](src/WinDock/WinDock.csproj)
[![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white)](#faq)

[English](README.md) · **Português**

[Instalar](#instalar) · [Primeiros passos](#primeiros-passos) · [Atalhos](#atalhos-de-teclado) · [Como funciona](#como-funciona) · [Presets](#presets) · [FAQ](#faq) · [Compilar](#compilar)

</div>

---

O WinDock não desenha um segundo dock. Os ícones ficam exatamente onde o Windows os coloca. O WinDock é o orquestrador por trás deles: guarda um **preset** por contexto (trabalho, jogos, estudos...), cada um uma lista de programas mostrados com o ícone real, e troca a taskbar por horário ou com um clique.

Inspirado no [PrimoDock](https://dock.oprimo.dev/) para macOS.

## Instalar

Você não precisa instalar o .NET nem mais nada. É um arquivo só.

1. **Baixe** o [`WinDock.exe`](https://github.com/obrenoalvim/windock/releases/latest/download/WinDock.exe) (uns 65 MB, tudo incluso).
2. **Dê dois cliques.** Se o Windows mostrar uma tela azul "O Windows protegeu seu PC", clique em **Mais informações** e depois em **Executar assim mesmo**. Essa tela aparece porque o arquivo ainda não tem assinatura digital, e todo app novo passa por ela.
3. **O WinDock abre** e coloca um ícone na bandeja do sistema, perto do relógio (clique na setinha se não estiver visível). Fechar a janela não encerra o app, ele continua rodando ali.
4. Opcional: clique com o botão direito no ícone da bandeja e ligue **Iniciar com o Windows** para ele abrir junto com o PC.

Para remover: clique com o botão direito no ícone da bandeja, use **Restaurar original** na janela se quiser a taskbar antiga de volta, depois **Sair** e apague o `WinDock.exe` e a pasta `%APPDATA%\WinDock`.

## Primeiros passos

1. Abra a janela (ícone na bandeja, ou `Ctrl+Alt+W`). Você vê um cartão por preset: **Trabalho**, **Jogos**, **Estudos** e **Fim de semana**. Sua taskbar atual foi copiada para o que combina com o horário de agora.
2. Clique em **+ Programa** em um cartão e escolha o que deve ficar fixo. O seletor tem abas para seus programas, apps da Microsoft Store (WhatsApp, Calculadora...) e os jogos da Steam e da Epic Games instalados.
3. Em **Agenda**, escolha os dias e horários em que o preset deve ficar ativo. Ou pule isso e troque na mão.
4. Clique em **Aplicar**, ou espere a agenda fazer. A taskbar muda em cerca de meio segundo para um preset já usado antes.

Os programas do cartão **Sempre fixos**, no topo (por exemplo Brave, Explorador de Arquivos, Spotify), vão primeiro em todos os presets, então cada preset só precisa dos extras.

## Atalhos de teclado

| Teclas | O que faz |
|---|---|
| `Ctrl+Alt+W` | Abrir ou ocultar a janela do WinDock |
| `Ctrl+Alt+1` ... `9` | Aplicar o 1º ... 9º preset (a ordem dos cartões) |
| `Esc` | Fechar a janela (o app continua rodando na bandeja) |

| Mouse | O que faz |
|---|---|
| Ícone da bandeja, clique esquerdo | Abrir ou ocultar a janela |
| Ícone da bandeja, clique direito | Lista de presets, opções e sair |
| Botão direito num ícone de app do cartão | Mover para a esquerda, mover para a direita, remover |
| **Aplicar** no cartão | Colocar aquele preset na taskbar |
| **Usar taskbar atual** no cartão | Trocar os programas do cartão pelo que está fixado agora |
| **+ Novo preset** | Criar um preset vazio |
| **Restaurar original** | Trazer de volta a taskbar que você tinha antes do WinDock mexer |

O menu da bandeja também tem **Troca automática por horário**, **Avisar trocas** e **Iniciar com o Windows**. Abrir o WinDock uma segunda vez só traz a janela para a frente.

## Como funciona

O Windows guarda os apps fixados na chave de registro `Taskband` e os atalhos em `%APPDATA%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar`. Um preset é uma lista de programas. Aplicar um preset grava esses dois lugares com exatamente aquela lista e reinicia o `explorer.exe` para o Windows ler. O WinDock cobre esse reinício com uma imagem da taskbar para não parecer uma piscada.

- Trocar de preset nunca fecha seus programas. Só o shell do Windows reinicia.
- A taskbar original é salva antes da primeira mudança, então o **Restaurar original** sempre funciona.
- Se os apps fixados já são os pedidos, nada reinicia.
- Se você pedir outro preset no meio de uma troca, vale o último.

## Presets

Presets iniciais são instalados na primeira execução: **Trabalho** (seg-sex 09:00-18:00), **Jogos** (seg-sex 18:00-09:00), **Estudos** (manual) e **Fim de semana** (sáb-dom). Programas que não estão instalados na sua máquina ficam de fora.

Os presets ficam em `%APPDATA%\WinDock\presets` como JSON, e a janela edita por você:

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
    { "name": "Spotify", "target": "%APPDATA%\\Spotify\\Spotify.exe" }
  ]
}
```

`days` aceita `Mon-Fri`, `Sat,Sun`, `Fri-Mon`. O `end` é exclusivo e `24:00` é meia-noite, então 18:00 até 09:00 são dois blocos. Vale o primeiro preset (pelo nome do arquivo) que combinar com o horário. `target` é um `.exe` ou `.lnk` (variáveis de ambiente funcionam). Apps da Loja e jogos são salvos como um launcher (`target` mais `args`, `icon` e um `appId`), por exemplo `steam://rungameid/1243130`.

## Compilar

Precisa do [.NET 10 SDK](https://dotnet.microsoft.com/download) no Windows.

```powershell
dotnet run --project src/WinDock
dotnet test
dotnet publish src/WinDock -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

O último comando gera o `publish/WinDock.exe` único que vai em cada release. Estrutura: `Models/` (presets, agenda, snapshot), `Services/` (taskbar, montagem dos blobs, catálogo, bandeja), `ViewModels/` (MVVM Toolkit), `Views/` e `Themes/` (XAML).

## FAQ

**Por que há uma pausa quando troca?** O Windows só lê os apps fixados quando o shell inicia, então o WinDock reinicia o `explorer.exe`, o que leva uns dois segundos. Para ficar sutil, o WinDock cobre o reinício com imagens da taskbar: ele lembra como cada preset ficou, então um preset já usado aparece em cerca de meio segundo enquanto o Windows se recupera por trás, e depois faz a transição suave para o real. A primeira troca para um preset nunca visto segura a imagem antiga. Você pode escolher outro preset na hora: vale sempre o último pedido. Janelas abertas do Explorador de Arquivos ainda são fechadas pelo reinício, e os ícones da área de trabalho piscam. Desligue a **Troca automática** se não quiser que isso aconteça sozinho. O Windows 11 24H2 em diante bloqueia fixar apps por outros programas, então não existe jeito de adicionar sem reiniciar.

**Ele fecha meus apps?** Não. Só o shell reinicia. Os programas em execução continuam rodando.

**Pode quebrar minha taskbar?** O estado original é salvo antes da primeira mudança e **Restaurar original** traz de volta. Se gravar a taskbar nova falhar, a anterior é recolocada.

**Por que um ícone apareceu em branco?** O Windows mantém uma entrada por programa. O mesmo programa listado duas vezes, mesmo por atalhos diferentes, gerava um ícone em branco. O WinDock agora remove as duplicatas para você.

**Quais programas um preset pode ter?** Qualquer `.exe` ou `.lnk`, apps da Microsoft Store e jogos instalados da Steam e da Epic Games.

**Funciona no Windows 10?** Usa a mesma chave de registro e pasta de atalhos do Windows 10, sem nenhuma API exclusiva do Windows 11, mas só foi testado no Windows 11. Relatos do Windows 10 são bem-vindos.

**É seguro? Por que o Windows avisa?** O código está todo neste repositório. O aviso é o Windows sendo cauteloso com um arquivo sem assinatura. O WinDock grava só o armazenamento de apps fixados do seu próprio usuário e reinicia o shell, nada além disso.

## Licença

[MIT](LICENSE). As fontes incluídas (Michroma, Space Mono) estão sob a SIL Open Font License.
