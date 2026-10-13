using Pindo.App.Input;

namespace Pindo.App.Tests;

public sealed class ModifierChordTests
{
    public static IEnumerable<object[]> Chords()
    {
        foreach (int ctrl in new[] { 0xA2, 0xA3 })
        foreach (int win in new[] { 0x5B, 0x5C })
        foreach (bool ctrlFirst in new[] { true, false })
        foreach (bool ctrlReleasedFirst in new[] { true, false })
            yield return [ctrl, win, ctrlFirst, ctrlReleasedFirst];
    }

    [Theory, MemberData(nameof(Chords))]
    public void TogglesOnceAfterBothKeysReleased(int ctrl, int win, bool ctrlFirst, bool ctrlReleasedFirst)
    {
        var chord = new ModifierChord();
        Assert.False(chord.Update(ctrlFirst ? ctrl : win, true).Toggle);
        Assert.False(chord.Update(ctrlFirst ? win : ctrl, true).Toggle);
        Assert.Equal(default, chord.Update(ctrlFirst ? win : ctrl, true));
        ChordResult first = chord.Update(ctrlReleasedFirst ? ctrl : win, false);
        Assert.True(first.MaskStartMenu);
        Assert.False(first.Toggle);
        ChordResult last = chord.Update(ctrlReleasedFirst ? win : ctrl, false);
        Assert.True(last.Toggle);
        Assert.False(last.MaskStartMenu);
        Assert.Equal(default, chord.Update(ctrl, false));
    }

    [Theory]
    [InlineData(0x44)] // Windows desktop shortcut
    [InlineData(0x25)] // Windows desktop navigation
    [InlineData(0x20)]
    [InlineData(0xA0)]
    public void AdditionalKeyCancelsChord(int extra)
    {
        var chord = new ModifierChord();
        chord.Update(0xA2, true); chord.Update(0x5B, true); chord.Update(extra, true);
        Assert.Equal(default, chord.Update(0x5B, false));
        Assert.Equal(default, chord.Update(0xA2, false));
        chord.Update(extra, false);
        chord.Update(0xA2, true); chord.Update(0x5B, true);
        chord.Update(0x5B, false);
        Assert.True(chord.Update(0xA2, false).Toggle);
    }

    [Fact]
    public void InjectedKeysAndResetCannotCompleteAChord()
    {
        var chord = new ModifierChord();
        chord.Update(0xA2, true, injected: true); chord.Update(0x5B, true, injected: true);
        Assert.Equal(default, chord.Update(0x5B, false, injected: true));
        Assert.Equal(default, chord.Update(0xA2, false, injected: true));
        chord.Update(0xA2, true); chord.Update(0x5B, true);
        chord.Reset([0xA2, 0x5B]);
        Assert.Equal(default, chord.Update(0x5B, false));
        Assert.Equal(default, chord.Update(0xA2, false));
    }

    [Fact]
    public void ExtraKeyHeldBeforeChordBlocksIt()
    {
        var chord = new ModifierChord();
        chord.Update(0x41, true); chord.Update(0xA3, true); chord.Update(0x5C, true);
        chord.Update(0x41, false);
        Assert.Equal(default, chord.Update(0xA3, false));
        Assert.Equal(default, chord.Update(0x5C, false));
    }

    [Fact]
    public void HookCanStartResetAndDisposeWithoutInput()
    {
        using var hook = new ModifierHotkey(() => { });
        hook.Start(); hook.Reset(); hook.Dispose(); hook.Dispose();
    }
}
