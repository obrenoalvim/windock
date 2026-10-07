<div align="center">

<img src=".github/logo.svg" alt="Logo do WinDock" width="120" height="120">

# WinDock

**Um PC com Windows. Várias taskbars.**<br>
Troque os apps fixados na taskbar de verdade do Windows por preset e por horário: apps de trabalho das 9 às 18, jogos depois.

[![Baixar](https://img.shields.io/badge/Baixar-WinDock.zip-FFB547?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/obrenoalvim/windock/releases/latest/download/WinDock-win-x64.zip)

[![Licença: MIT](https://img.shields.io/badge/licen%C3%A7a-MIT-FFB547)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/obrenoalvim/windock?style=flat&logo=github&color=3b9eff)](https://github.com/obrenoalvim/windock/stargazers)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](src/WinDock/WinDock.csproj)
[![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white)](#faq)

[English](README.md) · **Português**

[Como funciona](#como-funciona) · [Uso](#uso) · [Presets](#presets) · [Atalhos](#atalhos) · [Compilar](#compilar) · [FAQ](#faq)

</div>

---

O WinDock não desenha um segundo dock. Os ícones ficam exatamente onde o Windows os coloca. O WinDock é o orquestrador por trás deles: guarda um **preset** por contexto (trabalho, jogos, estudos...), cada um uma cópia salva dos seus apps fixados na taskbar, e troca por horário ou com um clique.

Inspirado no [PrimoDock](https://dock.oprimo.dev/) para macOS.

## Como funciona

O Windows guarda os apps fixados na chave de registro `Taskband` e os atalhos em `%APPDATA%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar`. Um preset é uma lista de programas. Aplicar um preset grava esses dois lugares com exatamente aquela lista e reinicia o `explorer.exe` para o Windows ler, o que leva alguns segundos.

1. Abra o WinDock e escolha os programas de cada preset (**+ Programa**). Cada programa aparece com o ícone real.
2. Defina os dias e horários em que cada preset deve ficar ativo.
3. Clique em **Aplicar**, ou deixe a agenda fazer isso.

A partir daí o WinDock troca a taskbar real sozinho. Nada é instalado no Windows e nada roda em segundo plano além do ícone na bandeja.

## Uso

- **Janela** (ícone na bandeja, ou `Ctrl+Alt+W`): um cartão por preset com nome, os ícones de cada programa fixado e a agenda.
- **+ Programa**: escolha entre os apps do Menu Iniciar (digite para buscar, selecione vários) ou procure qualquer `.exe` ou `.lnk`.
- **Botão direito num ícone**: mover para a esquerda, mover para a direita, remover. A ordem dos ícones é a ordem na taskbar.
- **Usar taskbar atual**: troca os programas do preset pelo que está fixado agora, inclusive apps da Store.
- **Agenda**: ligue os dias da semana, digite o horário de início e fim. Adicione quantos horários quiser.
- **+ Novo preset** cria um preset vazio.
- **Troca automática** liga ou desliga a agenda. **Avisar trocas** mostra uma notificação na bandeja quando troca.
- **Restaurar original** devolve a taskbar que você tinha antes do WinDock mexer em qualquer coisa (salva no primeiro aplicar).

## Presets

Presets iniciais são instalados na primeira execução: **Trabalho** (seg-sex 09:00-18:00), **Jogos** (seg-sex 18:00-09:00), **Estudos** (manual) e **Fim de semana** (sáb-dom). Programas que não estão instalados na sua máquina ficam de fora. A primeira execução também copia a sua taskbar atual para o preset que combina com o horário, então nada muda até você aplicar outro.

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

`days` aceita `Mon-Fri`, `Sat,Sun`, `Fri-Mon`. O `end` é exclusivo e `24:00` é meia-noite, então 18:00 até 09:00 são dois blocos. Vale o primeiro preset (pelo nome do arquivo) que combinar com o horário. `target` é um `.exe` ou `.lnk` (variáveis de ambiente funcionam). Apps da Store importados com **Usar taskbar atual** ficam como entradas opacas.

## Atalhos

| Teclas | Ação |
|---|---|
| `Ctrl+Alt+W` | Abrir ou ocultar a janela |
| `Ctrl+Alt+1` ... `9` | Aplicar o N-ésimo preset |

## Compilar

Precisa do [.NET 10 SDK](https://dotnet.microsoft.com/download) no Windows.

```powershell
dotnet run --project src/WinDock
dotnet test
dotnet publish src/WinDock -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Estrutura: `Models/` (presets, agenda, snapshot), `Services/` (taskbar, resolvedor de agenda, store de presets, bandeja), `ViewModels/` (MVVM Toolkit), `Views/` e `Themes/` (XAML).

## FAQ

**Por que a tela pisca quando troca?** O Windows só lê os apps fixados quando o shell inicia, então o WinDock reinicia o `explorer.exe`. Janelas abertas do Explorador de Arquivos são fechadas. Desligue a **Troca automática** se não quiser que isso aconteça sozinho.

**Ele fecha meus apps?** Não. Só o shell reinicia. Os programas em execução continuam rodando.

**Pode quebrar minha taskbar?** O estado original é salvo antes da primeira mudança e **Restaurar original** traz de volta. Os snapshots são cópias do que o próprio Windows gravou.

**Quais programas um preset pode ter?** Qualquer `.exe` ou `.lnk`, mais apps da Store importados da taskbar atual.

**Funciona no Windows 10?** Usa a mesma chave de registro e pasta de atalhos do Windows 10, sem nenhuma API exclusiva do Windows 11, mas só foi testado no Windows 11. Relatos do Windows 10 são bem-vindos.

## Licença

[MIT](LICENSE). As fontes incluídas (Michroma, Space Mono) estão sob a SIL Open Font License.
