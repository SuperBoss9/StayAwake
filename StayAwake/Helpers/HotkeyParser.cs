namespace StayAwake.Helpers;

public static class HotkeyParser
{
    public static bool TryParse(string? text, out uint modifiers, out uint virtualKey, out string error)
    {
        modifiers = 0;
        virtualKey = 0;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Empty hotkey";
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            error = "Hotkey must include modifier and key";
            return false;
        }

        uint mods = Interop.HotkeyInterop.MOD_NOREPEAT;
        uint key = 0;

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    mods |= Interop.HotkeyInterop.MOD_CONTROL;
                    break;
                case "alt":
                    mods |= Interop.HotkeyInterop.MOD_ALT;
                    break;
                case "shift":
                    mods |= Interop.HotkeyInterop.MOD_SHIFT;
                    break;
                case "win":
                case "windows":
                    mods |= Interop.HotkeyInterop.MOD_WIN;
                    break;
                default:
                    key = ParseKey(part);
                    break;
            }
        }

        if (key == 0)
        {
            error = "Unknown key";
            return false;
        }

        // Need at least one real modifier besides NOREPEAT
        if ((mods & ~Interop.HotkeyInterop.MOD_NOREPEAT) == 0)
        {
            error = "Modifier required";
            return false;
        }

        modifiers = mods;
        virtualKey = key;
        return true;
    }

    private static uint ParseKey(string part)
    {
        if (part.Length == 1)
        {
            char c = char.ToUpperInvariant(part[0]);
            if (c is >= 'A' and <= 'Z')
                return (uint)c;
            if (c is >= '0' and <= '9')
                return (uint)c;
        }

        if (part.StartsWith("F", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(part[1..], out int f) && f is >= 1 and <= 24)
            return (uint)(0x70 + f - 1);

        return part.ToUpperInvariant() switch
        {
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "ESC" or "ESCAPE" => 0x1B,
            "INSERT" or "INS" => 0x2D,
            "DELETE" or "DEL" => 0x2E,
            "HOME" => 0x24,
            "END" => 0x23,
            "PGUP" or "PAGEUP" => 0x21,
            "PGDN" or "PAGEDOWN" => 0x22,
            "LEFT" => 0x25,
            "UP" => 0x26,
            "RIGHT" => 0x27,
            "DOWN" => 0x28,
            _ => 0
        };
    }
}
