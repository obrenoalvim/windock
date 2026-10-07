using System.ComponentModel;
using System.Globalization;

namespace WinDock.Services;

public sealed class Loc : INotifyPropertyChanged
{
    public const string English = "en";
    public const string Portuguese = "pt";

    static readonly Dictionary<string, (string En, string Pt)> Table = new()
    {
        ["subtitle"] = ("What is on the taskbar, and when.", "Quem fica na taskbar, e quando."),
        ["close"] = ("Close (Esc)", "Fechar (Esc)"),
        ["closeName"] = ("Close", "Fechar"),
        ["alwaysTitle"] = ("ALWAYS PINNED", "SEMPRE FIXOS"),
        ["alwaysDesc"] = ("Added to every preset, before the other programs.", "Entram em todos os presets, antes dos outros programas."),
        ["addProgram"] = ("+ Program", "+ Programa"),
        ["apply"] = ("Apply", "Aplicar"),
        ["useCurrent"] = ("Use current taskbar", "Usar taskbar atual"),
        ["delete"] = ("Delete", "Excluir"),
        ["onTaskbar"] = ("ON TASKBAR", "NA TASKBAR"),
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
        ["removeFromPreset"] = ("Remove from preset", "Remover do preset"),
        ["restoreOriginal"] = ("Restore original", "Restaurar original"),
        ["folder"] = ("Folder", "Pasta"),
        ["newPreset"] = ("+ New preset", "+ Novo preset"),
        ["autoSwitch"] = ("Auto-switch", "Troca automática"),
        ["notifySwitch"] = ("Notify on switch", "Avisar trocas"),
        ["nextSwitch"] = ("Next switch: {0} → {1}", "Próxima troca: {0} → {1}"),
        ["free"] = ("free", "livre"),
        ["noSchedule"] = ("No schedule", "Sem agenda"),
        ["notFound"] = ("{0} (not found)", "{0} (não encontrado)"),
        ["deleteAsk"] = ("Delete the preset \"{0}\"?", "Excluir o preset \"{0}\"?"),

        ["noPrograms"] = ("{0}: no programs, add some first", "{0}: nenhum programa, adicione alguns primeiro"),
        ["switching"] = ("Switching the taskbar to {0}...", "Trocando a taskbar para {0}..."),
        ["taskbarNow"] = ("Taskbar: {0}", "Taskbar: {0}"),
        ["failed"] = ("Failed: {0}", "Falhou: {0}"),
        ["noBackup"] = ("No backup of the original taskbar yet", "Nenhum backup da taskbar original ainda"),
        ["restoring"] = ("Restoring the original taskbar...", "Restaurando a taskbar original..."),
        ["restored"] = ("Original taskbar restored", "Taskbar original restaurada"),
        ["copiedCurrent"] = ("Current pins copied to {0}", "Programas fixados agora copiados para {0}"),
        ["presetCreated"] = ("Preset created. Add programs to it.", "Preset criado. Adicione programas nele."),
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

    public CultureInfo Culture => language == Portuguese ? new CultureInfo("pt-BR") : new CultureInfo("en-US");

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
