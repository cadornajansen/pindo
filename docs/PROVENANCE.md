# Source and dependency provenance

Pindo retains UI and utility code from earlier work. Renaming or reducing it does not make it newly authored.

- The earlier desktop snapshot was imported on October 9, 2026, from the existing local checkout. Its source HEAD was `960ba2e` (September 21, 2026), plus modified/untracked work whose creation dates were not independently established. The compact WPF composer, presentation helpers and tray assets retain that lineage.
- The shared Core/Tools implementation was integrated with PR #2's history preserved in commit `0056687`. It is now maintained as Pindo.Core/Pindo.Tools.
- The pre-cleanup shared baseline is `8804d20`. Detailed historical import inventories, validation notes and previous disclosures remain accessible in Git history. The current cleanup was AI-assisted using OpenAI Codex; it does not imply completion of any past hackathon requirements.

## Retained dependencies

The application uses C#, .NET 10, WPF, Windows Forms for the tray, Win32 calls, and OpenRouter over HTTP. It has no third-party runtime NuGet package references. Tests use xUnit, its Visual Studio runner and Microsoft.NET.Test.Sdk; exact versions are in the test projects.

Separately installed tools retain their own licensing and notices:

- [ImageMagick license](https://imagemagick.org/script/license.php).
- [Poppler source and notices](https://gitlab.freedesktop.org/poppler/poppler): GPL-licensed project; binaries are installed separately.
- [yt-dlp release notices](https://github.com/yt-dlp/yt-dlp/blob/2026.08.19/THIRD_PARTY_LICENSES.txt): upstream source is Unlicense; packaged executables include additional components and obligations.
- [FFmpeg legal and build conditions](https://ffmpeg.org/legal.html): obligations depend on the installed build; the previously tested Windows build enables GPL/version 3.

No native binaries or credentials are redistributed here. This cleanup does not select a new project license or remove upstream notices.
