<div align="center">

<img src=".github/logo.svg" alt="Logo do WinDock" width="120" height="120">

# WinDock

**Um PC com Windows. Vários ambientes.**<br>
Um dock flutuante com presets que trocam por horário: apps de trabalho das 9 às 18, jogos depois, um clique ou um atalho para mudar.

[![Baixar](https://img.shields.io/badge/Baixar-WinDock.zip-FFB547?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/obrenoalvim/windock/releases/latest/download/WinDock-win-x64.zip)

[![Licença: MIT](https://img.shields.io/badge/licen%C3%A7a-MIT-FFB547)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/obrenoalvim/windock?style=flat&logo=github&color=3b9eff)](https://github.com/obrenoalvim/windock/stargazers)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](src/WinDock/WinDock.csproj)
[![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white)](#faq)

[English](README.md) · **Português**

[Recursos](#recursos) · [Presets](#presets) · [Atalhos](#atalhos) · [Compilar](#compilar) · [FAQ](#faq)

</div>

---

WinDock é um app WPF pequeno que coloca um dock na tela e troca o conteúdo por **preset**. Um preset é um arquivo JSON com os apps daquele contexto, uma cor de destaque, alguns widgets e, opcionalmente, uma agenda. Às 9:00 de um dia útil o dock vira o dock de trabalho; às 18:00 vira o dock de jogos. Ele não substitui a barra de tarefas do Windows, fica ao lado dela.

Inspirado no [PrimoDock](https://dock.oprimo.dev/) para macOS.

## Recursos

- **Presets com agenda.** Dias e faixas de horário por preset, com troca automática. Uma escolha manual vale até a próxima mudança agendada.
- **Ícones reais.** Vêm de cada programa, inclusive Steam, Epic e outros que abrem por URI.
- **Indicador de aberto.** Um ponto sob cada app em execução.
- **Widgets.** Relógio, CPU, RAM e "próxima troca" (para o que o dock vira e quando).
- **Arrastar e soltar.** Solte um `.exe` ou atalho no dock para fixar no preset atual. Botão direito no ícone remove.
- **Ícone na bandeja.** Lista de presets, troca automática, ocultar ao perder o foco, notificações, iniciar com o Windows.
- **Seguro por padrão.** Trocar de preset nunca fecha nada e nunca abre nada, a menos que o preset tenha `launchOnActivate`.
- **Editável à mão.** Os presets ficam em `%APPDATA%\WinDock\presets`. Recarregue pela bandeja, sem reiniciar.

## Presets

Quatro presets iniciais são instalados na primeira execução: **Trabalho** (seg-sex 09:00-18:00), **Jogos** (seg-sex 18:00-09:00), **Estudos** (manual) e **Fim de semana** (sáb-dom). Apps que não estão instalados na sua máquina ficam ocultos automaticamente.

```json
{
  "id": "trabalho",
  "name": "Trabalho",
  "glyph": "💼",
  "accent": "#FFB547",
  "launchOnActivate": false,
  "schedule": [{ "days": "Mon-Fri", "start": "09:00", "end": "18:00" }],
  "widgets": ["next", "cpu", "ram", "clock"],
  "items": [
    { "name": "VS Code", "target": "%LOCALAPPDATA%\\Programs\\Microsoft VS Code\\Code.exe", "process": "Code" },
    { "name": "Steam", "target": "steam://open/main", "icon": "C:\\Program Files (x86)\\Steam\\steam.exe", "process": "steam" }
  ]
}
```

| Campo | Significado |
|---|---|
| `schedule[].days` | `Mon-Fri`, `Sat,Sun`, `Fri-Mon`. Vale o primeiro preset (pelo nome do arquivo) que combinar. |
| `schedule[].start` / `end` | `HH:mm`, o fim é exclusivo, `24:00` é meia-noite. Para 18:00 até 09:00, use dois blocos (`18:00-24:00` e `00:00-09:00`). |
| `items[].target` | Executável, atalho, pasta ou URI (`steam://...`). Variáveis de ambiente e busca no `PATH` funcionam. |
| `items[].icon` | Opcional. Caminho de uma imagem ou exe usado como ícone. Se não existir, o item fica oculto. |
| `items[].process` | Opcional. Nome do processo para o ponto de "aberto", quando difere do nome do exe. |
| `launchOnActivate` | Abre todos os itens quando a agenda ativa o preset. Desligado por padrão. |
| `widgets` | Qualquer um de `clock`, `cpu`, `ram`, `next`. |

O nome do arquivo define a ordem (`01-`, `02-`...), que também é o número do `Ctrl+Alt+N`.

## Atalhos

| Teclas | Ação |
|---|---|
| `Ctrl+Alt+Espaço` | Mostrar ou ocultar o dock |
| `Ctrl+Alt+1` ... `9` | Trocar para o N-ésimo preset |

## Compilar

Precisa do [.NET 10 SDK](https://dotnet.microsoft.com/download) no Windows.

```powershell
dotnet run --project src/WinDock
dotnet test
dotnet publish src/WinDock -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Estrutura: `Models/` (presets, agenda), `Services/` (resolvedor de agenda, launcher, ícones, bandeja, estatísticas), `ViewModels/` (MVVM Toolkit), `Views/` e `Themes/` (XAML).

## FAQ

**Ele esconde ou substitui a barra de tarefas?** Não. É uma janela flutuante, então não há o que quebrar quando o Windows atualiza.

**Ele fecha meus apps de trabalho às 18:00?** Não. Fechar programas pode perder trabalho não salvo. O dock só muda o que mostra.

**Por que um app sumiu de um preset?** O caminho dele não foi encontrado nesta máquina. Corrija o `target` no JSON ou solte o `.exe` correto no dock.

**Isola as janelas por ambiente como os desktops virtuais?** Ainda não.

## Licença

[MIT](LICENSE). As fontes incluídas (Michroma, Space Mono) estão sob a SIL Open Font License.
