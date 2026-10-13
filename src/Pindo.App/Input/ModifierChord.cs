namespace Pindo.App.Input;

internal readonly record struct ChordResult(bool Toggle = false, bool MaskStartMenu = false);

internal sealed class ModifierChord
{
    private readonly HashSet<int> _pressed = [];
    private bool _armed;
    private bool _blocked;
    private bool _masked;
    private static bool Ctrl(int key) => key is 0xA2 or 0xA3;
    private static bool Win(int key) => key is 0x5B or 0x5C;

    public ChordResult Update(int key, bool down, bool injected = false)
    {
        if (injected) return default;
        if (down)
        {
            if (!_pressed.Add(key)) return default;
            if (!Ctrl(key) && !Win(key)) _blocked = true;
            if (!_blocked && _pressed.Any(Ctrl) && _pressed.Any(Win)) _armed = true;
            return default;
        }
        if (!_pressed.Remove(key)) return default;
        bool mask = _armed && !_blocked && !_masked && (Ctrl(key) || Win(key));
        if (mask) _masked = true;
        bool toggle = _armed && !_blocked && !_pressed.Any(Ctrl) && !_pressed.Any(Win);
        if (_pressed.Count == 0) Reset();
        return new(toggle, mask);
    }

    public void Reset(IEnumerable<int>? heldKeys = null)
    {
        _pressed.Clear();
        if (heldKeys is not null) _pressed.UnionWith(heldKeys);
        _armed = _masked = false;
        _blocked = _pressed.Count > 0;
    }
}
