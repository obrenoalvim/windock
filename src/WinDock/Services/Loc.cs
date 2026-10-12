using System.ComponentModel;
using System.Globalization;

namespace WinDock.Services;

public sealed class Loc : INotifyPropertyChanged
{
    public const string English = "en";
    public const string Portuguese = "pt";

    static readonly Dictionary<string, (string En, string Pt)> Table = new()
    {
        ["subtitle"] = ("Which apps and widgets, and when.", "Quais apps e widgets, e quando."),
        ["close"] = ("Close (Esc)", "Fechar (Esc)"),
        ["closeName"] = ("Close", "Fechar"),
        ["alwaysTitle"] = ("ALWAYS PINNED", "SEMPRE FIXOS"),
        ["alwaysDesc"] = ("Shown in every environment, before the other programs.", "Aparecem em todos os ambientes, antes dos outros programas."),
        ["addProgram"] = ("+ Program", "+ Programa"),
        ["apply"] = ("Apply", "Aplicar"),
        ["useCurrent"] = ("Use current taskbar", "Usar taskbar atual"),
        ["delete"] = ("Delete", "Excluir"),
        ["onTaskbar"] = ("ACTIVE", "ATIVO"),
        ["tabEnvironments"] = ("Environments", "Ambientes"),
        ["tabDock"] = ("Dock", "Dock"),
        ["dockTitle"] = ("DOCK", "DOCK"),
        ["showDock"] = ("Show the dock", "Mostrar o dock"),
        ["replaceTaskbar"] = ("Replace the Windows taskbar", "Substituir a barra de tarefas do Windows"),
        ["replaceTaskbarDesc"] = ("Start, clock and quick settings move into the dock. The taskbar comes back when WinDock closes, even if it crashes.",
            "Iniciar, relógio e configurações rápidas vão para o dock. A barra volta quando o WinDock fecha, mesmo se ele cair."),
        ["autoHide"] = ("Hide automatically", "Ocultar automaticamente"),
        ["reserve"] = ("Reserve screen space", "Reservar espaço na tela"),
        ["reserveDesc"] = ("Maximized windows stop above the dock.", "Janelas maximizadas param acima do dock."),
        ["followWindows"] = ("Windows follow the environment", "Janelas seguem o ambiente"),
        ["followWindowsDesc"] = ("When you switch, the windows of the previous environment step aside and come back when you return.",
            "Ao trocar, as janelas do ambiente anterior saem da frente e voltam quando você voltar."),
        ["syncTaskbar"] = ("Also swap the Windows taskbar pins", "Também trocar os fixados da barra do Windows"),
        ["syncTaskbarDesc"] = ("The old mode: rewrites the taskbar and restarts Explorer on every switch.",
            "O modo antigo: regrava a barra e reinicia o Explorer a cada troca."),
        ["appearance"] = ("APPEARANCE", "APARÊNCIA"),
        ["lens"] = ("Hover effect", "Efeito ao passar o mouse"),
        ["lensNone"] = ("None", "Nenhum"),
        ["lensScale"] = ("Scale", "Escala"),
        ["lensMagnify"] = ("Magnify", "Ampliação"),
        ["magnification"] = ("Magnification", "Ampliação"),
        ["lensWidth"] = ("Lens width", "Largura da lente"),
        ["iconSize"] = ("Icon size", "Tamanho dos ícones"),
        ["theme"] = ("Theme", "Tema"),
        ["themeDark"] = ("Dark", "Escuro"),
        ["themeLight"] = ("Light", "Claro"),
        ["screen"] = ("Screen", "Monitor"),
        ["widgets"] = ("WIDGETS", "WIDGETS"),
        ["countdownDate"] = ("Date", "Data"),
        ["countdownLabel"] = ("Name", "Nome"),
        ["countdownHint"] = ("2026-12-25 for a fixed day, 12-25 to repeat every year.", "2026-12-25 para um dia fixo, 25/12 para repetir todo ano."),
        ["widget_clock"] = ("Clock", "Relógio"),
        ["widget_pomodoro"] = ("Pomodoro", "Pomodoro"),
        ["widget_stopwatch"] = ("Stopwatch", "Cronômetro"),
        ["widget_timer"] = ("Timer", "Timer"),
        ["widget_countdown"] = ("Countdown", "Contagem regressiva"),
        ["widget_progress"] = ("Progress", "Progresso"),
        ["widget_activity"] = ("Activity", "Atividade"),
        ["widget_battery"] = ("Battery", "Bateria"),
        ["countOne"] = ("1 program", "1 programa"),
        ["countMany"] = ("{0} programs", "{0} programas"),
        ["emptyHint"] = ("No programs yet. Click + Program to choose what gets pinned.", "Nenhum programa ainda. Clique em + Programa para escolher o que fica fixo."),
        ["schedule"] = ("SCHEDULE", "AGENDA"),
        ["from"] = ("from", "das"),
        ["to"] = ("to", "às"),
        ["remove"] = ("remove", "remover"),
        ["addSlot"] = ("+ time slot", "+ horário"),
        ["moveLeft"] = ("Move left", "Mover para a esquerda"),
        ["moveRight"] = ("Move right", "Mover para a direita"),
        ["removeFromPreset"] = ("Remove from environment", "Remover do ambiente"),
        ["restoreOriginal"] = ("Restore original taskbar", "Restaurar barra original"),
        ["folder"] = ("Folder", "Pasta"),
        ["newPreset"] = ("+ New environment", "+ Novo ambiente"),
        ["autoSwitch"] = ("Auto-switch", "Troca automática"),
        ["notifySwitch"] = ("Notify on switch", "Avisar trocas"),
        ["nextSwitch"] = ("Next switch: {0} → {1}", "Próxima troca: {0} → {1}"),
        ["free"] = ("free", "livre"),
        ["noSchedule"] = ("No schedule", "Sem agenda"),
        ["notFound"] = ("{0} (not found)", "{0} (não encontrado)"),
        ["deleteAsk"] = ("Delete the environment \"{0}\"?", "Excluir o ambiente \"{0}\"?"),

        ["noPrograms"] = ("{0}: no programs, add some first", "{0}: nenhum programa, adicione alguns primeiro"),
        ["switching"] = ("Switching the taskbar to {0}...", "Trocando a taskbar para {0}..."),
        ["taskbarNow"] = ("Taskbar: {0}", "Taskbar: {0}"),
        ["environmentNow"] = ("Environment: {0}", "Ambiente: {0}"),
        ["failed"] = ("Failed: {0}", "Falhou: {0}"),
        ["noBackup"] = ("No backup of the original taskbar yet", "Nenhum backup da taskbar original ainda"),
        ["restoring"] = ("Restoring the original taskbar...", "Restaurando a taskbar original..."),
        ["restored"] = ("Original taskbar restored", "Taskbar original restaurada"),
        ["copiedCurrent"] = ("Current pins copied to {0}", "Programas fixados agora copiados para {0}"),
        ["presetCreated"] = ("Environment created. Add programs to it.", "Ambiente criado. Adicione programas nele."),
        ["launchFailed"] = ("Could not open {0}", "Não deu para abrir {0}"),

        ["start"] = ("Start", "Iniciar"),
        ["trayIcons"] = ("Show the Windows taskbar and its tray icons", "Mostrar a barra do Windows e os ícones da bandeja"),
        ["quickSettings"] = ("Network, sound and battery", "Rede, som e bateria"),
        ["calendar"] = ("Calendar and notifications", "Calendário e notificações"),
        ["dockSettings"] = ("WinDock settings...", "Configurações do WinDock..."),
        ["open"] = ("Open", "Abrir"),
        ["newWindow"] = ("New window", "Nova janela"),
        ["openFolder"] = ("Open folder", "Abrir pasta"),
        ["openInExplorer"] = ("Open in Explorer", "Abrir no Explorador"),
        ["emptyFolder"] = ("This folder is empty.", "Esta pasta está vazia."),
        ["pinHere"] = ("Pin to this environment", "Fixar neste ambiente"),
        ["pinEverywhere"] = ("Pin to every environment", "Fixar em todos os ambientes"),
        ["unpinHere"] = ("Unpin from this environment", "Desafixar deste ambiente"),
        ["unpinEverywhere"] = ("Unpin from every environment", "Desafixar de todos os ambientes"),
        ["showInExplorer"] = ("Show in Explorer", "Mostrar no Explorador"),
        ["closeWindow"] = ("Close window", "Fechar janela"),
        ["closeWindows"] = ("Close {0} windows", "Fechar {0} janelas"),
        ["reset"] = ("Reset", "Zerar"),
        ["editWidgets"] = ("Choose widgets...", "Escolher widgets..."),
        ["showAllWindows"] = ("Show hidden windows", "Mostrar janelas ocultas"),

        ["pomodoro"] = ("start", "iniciar"),
        ["focus"] = ("focus", "foco"),
        ["break"] = ("break", "pausa"),
        ["paused"] = ("paused", "pausado"),
        ["pomodoroBreak"] = ("Pomodoro done. Take a {0} minute break.", "Pomodoro concluído. Pausa de {0} minutos."),
        ["pomodoroFocus"] = ("Break is over. Back to focus.", "Fim da pausa. Hora de focar."),
        ["stopwatch"] = ("running", "contando"),
        ["stopwatchStart"] = ("start", "iniciar"),
        ["timer"] = ("timer", "timer"),
        ["timerHint"] = ("scroll ±1m", "role ±1m"),
        ["timerDone"] = ("The {0} minute timer is done.", "O timer de {0} minutos terminou."),
        ["christmas"] = ("Christmas", "o Natal"),
        ["countdownBad"] = ("check the date", "confira a data"),
        ["today"] = ("it is today", "é hoje"),
        ["todayIs"] = ("{0} is today", "{0} é hoje"),
        ["dayLeft"] = ("day left", "dia restante"),
        ["dayTo"] = ("day to {0}", "dia para {0}"),
        ["daysLeft"] = ("days left", "dias restantes"),
        ["daysTo"] = ("days to {0}", "dias para {0}"),
        ["progress_day"] = ("of the day", "do dia"),
        ["progress_week"] = ("of the week", "da semana"),
        ["progress_month"] = ("of the month", "do mês"),
        ["progress_year"] = ("of the year", "do ano"),
        ["plugged"] = ("plugged", "tomada"),
        ["dupOne"] = ("1 program was already in the list and was skipped", "1 programa já estava na lista e foi ignorado"),
        ["dupMany"] = ("{0} programs were already in the list and were skipped", "{0} programas já estavam na lista e foram ignorados"),

        ["trayOpen"] = ("Open settings", "Abrir configuração"),
        ["trayAuto"] = ("Auto-switch on schedule", "Troca automática por horário"),
        ["trayNotify"] = ("Notify on switch", "Avisar trocas"),
        ["trayStartup"] = ("Start with Windows", "Iniciar com o Windows"),
        ["trayLanguage"] = ("Language", "Idioma"),
        ["trayExit"] = ("Exit", "Sair"),

        ["pickerTitle"] = ("ADD PROGRAMS", "ADICIONAR PROGRAMAS"),
        ["pickerSub"] = ("Programs, Store apps and installed games.", "Programas, apps da Loja e jogos instalados."),
        ["tabAll"] = ("All", "Todos"),
        ["catPrograms"] = ("Programs", "Programas"),
        ["catStore"] = ("Store", "Loja"),
        ["catSteam"] = ("Steam", "Steam"),
        ["catEpic"] = ("Epic", "Epic"),
        ["cancel"] = ("Cancel", "Cancelar"),
        ["add"] = ("Add", "Adicionar"),
        ["browse"] = ("Browse for file...", "Procurar arquivo..."),
        ["browseFolder"] = ("Folder...", "Pasta..."),
        ["browseFolderTitle"] = ("Choose folders to stack in the dock", "Escolha pastas para empilhar no dock"),
        ["browseTitle"] = ("Choose programs", "Escolha programas"),
        ["browseFilter"] = ("Programs and shortcuts|*.exe;*.lnk", "Programas e atalhos|*.exe;*.lnk"),

        ["day1"] = ("M", "S"),
        ["day2"] = ("T", "T"),
        ["day3"] = ("W", "Q"),
        ["day4"] = ("T", "Q"),
        ["day5"] = ("F", "S"),
        ["day6"] = ("S", "S"),
        ["day7"] = ("S", "D")
    };

    string language = English;

    public static Loc Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language => language;

    public CultureInfo Culture => CultureInfo.GetCultureInfo(language == Portuguese ? "pt-BR" : "en-US");

    public string this[string key] =>
        Table.TryGetValue(key, out var pair) ? (language == Portuguese ? pair.Pt : pair.En) : key;

    public static string T(string key) => Instance[key];

    public static string F(string key, params object[] args) => string.Format(Instance[key], args);

    public void Set(string code)
    {
        var next = code == Portuguese ? Portuguese : English;
        if (next == language) return;
        language = next;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public static IReadOnlyCollection<string> Keys => Table.Keys;
}
