using System.Reflection;
using System.Runtime.InteropServices;
using LocalTutor.Tools.FileOrganization;

namespace LocalTutor.Tests.FileOrganization;

public sealed class OrganizationNativeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "localtutor-organize-native-" + Guid.NewGuid().ToString("N"));
    public OrganizationNativeTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, recursive: true);
    private string At(string name) => Path.Combine(root, name);

    [Fact]
    public async Task UnixFifoIsRejectedBeforeAReadCanBlock()
    {
        if (!OperatingSystem.IsLinux()) return;
        Assert.Equal(0, MkFifo(At("pipe.txt"), 0x180));
        Directory.CreateDirectory(At("sorted"));
        var scope = new OrganizationAccessScope(root, ["pipe.txt"], ["sorted"], [root]);
        var preview = await new OrganizePreviewTool(scope).ExecuteAsync(new(["pipe.txt"], [new(OrganizationRuleKind.Extension, "sorted", Extension: ".txt")]));
        Assert.False(preview.Success); Assert.Equal("RegularFileRequired", preview.Error);
        var duplicates = await new FindDuplicatesTool(scope).ExecuteAsync(new([root]));
        Assert.True(duplicates.Success, duplicates.Error); Assert.Empty(duplicates.Value!.Groups);
        Assert.Contains(duplicates.Value.SkippedFiles, s => s.Code == "RegularFileRequired");
    }

    [Fact]
    public void NativeRenameRefusesOverwriteAndCrossVolumeCopyDeleteFallback()
    {
        if (!OperatingSystem.IsLinux()) return;
        // Exercise the low-level guard directly: a normal workspace cannot approve two separate volume roots.
        MethodInfo move = typeof(OrganizeApplyTool).Assembly.GetType("LocalTutor.Tools.FileOrganization.OrganizationIO")!
            .GetMethod("Move", BindingFlags.Static | BindingFlags.NonPublic)!;
        File.WriteAllText(At("source.txt"), "source"); File.WriteAllText(At("existing.txt"), "existing");
        var conflict = Assert.Throws<TargetInvocationException>(() => move.Invoke(null, [At("source.txt"), At("existing.txt")]));
        Assert.StartsWith("OutputConflict:", conflict.InnerException!.Message);
        Assert.Equal("source", File.ReadAllText(At("source.txt"))); Assert.Equal("existing", File.ReadAllText(At("existing.txt")));
        if (!Directory.Exists("/dev/shm") || !DriveInfo.GetDrives().Any(d => d.Name.TrimEnd('/') == "/dev/shm")) return;
        string destination = Path.Combine("/dev/shm", "localtutor-cross-volume-" + Guid.NewGuid().ToString("N"));
        var crossVolume = Assert.Throws<TargetInvocationException>(() => move.Invoke(null, [At("source.txt"), destination]));
        Assert.StartsWith("CrossVolumeMoveUnsupported:", crossVolume.InnerException!.Message);
        Assert.True(File.Exists(At("source.txt"))); Assert.False(File.Exists(destination));
    }

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int MkFifo(string path, uint mode);
}
