using System.Text.Json.Serialization;

namespace QuickDock.Models;

public enum MouseTrigger
{
    Middle,
    Right,
    XButton1,
    XButton2
}

public sealed class HotkeyConfig
{
    public bool Alt { get; set; } = true;
    public bool Ctrl { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }
    public MouseTrigger Trigger { get; set; } = MouseTrigger.Middle;

    [JsonIgnore]
    public bool HasModifier => Alt || Ctrl || Shift || Win;

    [JsonIgnore]
    public string DisplayText
    {
        get
        {
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(Trigger switch
            {
                MouseTrigger.Middle => "鼠标中键",
                MouseTrigger.Right => "鼠标右键",
                MouseTrigger.XButton1 => "鼠标侧键 1",
                MouseTrigger.XButton2 => "鼠标侧键 2",
                _ => "鼠标中键"
            });
            return string.Join(" + ", parts);
        }
    }

    public HotkeyConfig Clone() => new()
    {
        Alt = Alt,
        Ctrl = Ctrl,
        Shift = Shift,
        Win = Win,
        Trigger = Trigger
    };
}
