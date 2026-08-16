using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using RespawnSwitch.Application.Douyin;

namespace RespawnSwitch.Windows.DouyinDiscovery;

public sealed class WindowsDouyinQuickSources(IDouyinCandidateValidator validator) : IDouyinQuickCandidateSource
{
    public async Task<IReadOnlyList<DouyinCandidate>> FindAsync(
        string? savedPath,
        CancellationToken cancellationToken)
    {
        var discovered = new List<(string Path, DouyinDiscoverySource Source, bool Running)>();
        if (!string.IsNullOrWhiteSpace(savedPath))
        {
            discovered.Add((savedPath, DouyinDiscoverySource.SavedPath, false));
        }

        discovered.AddRange(FindRunningProcesses());
        discovered.AddRange(FindRegistryCandidates());
        discovered.AddRange(FindStartMenuCandidates());

        var candidates = new List<DouyinCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in discovered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = NormalizeCandidatePath(item.Path);
            if (normalized is null || !seen.Add(normalized))
            {
                continue;
            }

            var candidate = await validator.ValidateAsync(
                normalized,
                item.Source,
                item.Running,
                cancellationToken).ConfigureAwait(false);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    internal static string? NormalizeCandidatePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = Environment.ExpandEnvironmentVariables(value.Trim());
        if (trimmed.StartsWith('"'))
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote > 1)
            {
                trimmed = trimmed[1..closingQuote];
            }
        }
        else
        {
            var comma = trimmed.LastIndexOf(',');
            if (comma > 0 && int.TryParse(trimmed[(comma + 1)..], out _))
            {
                trimmed = trimmed[..comma];
            }
        }

        try
        {
            if (Directory.Exists(trimmed))
            {
                trimmed = Path.Combine(trimmed, "douyin.exe");
            }

            return Path.GetFullPath(trimmed.Trim('"'));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static IEnumerable<(string Path, DouyinDiscoverySource Source, bool Running)> FindRunningProcesses()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName("douyin");
        }
        catch (InvalidOperationException)
        {
            yield break;
        }

        foreach (var process in processes)
        {
            using (process)
            {
                string? path = null;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                }

                if (!string.IsNullOrWhiteSpace(path))
                {
                    yield return (path, DouyinDiscoverySource.RunningProcess, true);
                }
            }
        }
    }

    private static IEnumerable<(string Path, DouyinDiscoverySource Source, bool Running)> FindRegistryCandidates()
    {
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            RegistryKey? baseKey = null;
            try
            {
                baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var appPath = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\douyin.exe");
                if (appPath?.GetValue(null) is string direct)
                {
                    yield return (direct, DouyinDiscoverySource.Registry, false);
                }

                foreach (var root in new[]
                {
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                })
                {
                    using var uninstall = baseKey.OpenSubKey(root);
                    if (uninstall is null)
                    {
                        continue;
                    }

                    string[] names;
                    try
                    {
                        names = uninstall.GetSubKeyNames();
                    }
                    catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
                    {
                        continue;
                    }

                    foreach (var name in names)
                    {
                        using var entry = uninstall.OpenSubKey(name);
                        var displayName = entry?.GetValue("DisplayName") as string;
                        if (!ContainsDouyin(displayName))
                        {
                            continue;
                        }

                        if (entry?.GetValue("DisplayIcon") is string displayIcon)
                        {
                            yield return (displayIcon, DouyinDiscoverySource.Registry, false);
                        }

                        if (entry?.GetValue("InstallLocation") is string installLocation)
                        {
                            yield return (installLocation, DouyinDiscoverySource.Registry, false);
                        }
                    }
                }
            }
            finally
            {
                baseKey?.Dispose();
            }
        }
    }

    private static IEnumerable<(string Path, DouyinDiscoverySource Source, bool Running)> FindStartMenuCandidates()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        };

        foreach (var root in roots.Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)))
        {
            foreach (var shortcut in EnumerateShortcuts(root))
            {
                var target = ResolveShortcut(shortcut);
                if (!string.IsNullOrWhiteSpace(target) &&
                    (ContainsDouyin(Path.GetFileNameWithoutExtension(shortcut)) ||
                     string.Equals(Path.GetFileName(target), "douyin.exe", StringComparison.OrdinalIgnoreCase)))
                {
                    yield return (target, DouyinDiscoverySource.StartMenu, false);
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateShortcuts(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            string[] entries;
            try
            {
                entries = Directory.GetFileSystemEntries(directory);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
                {
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if ((attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        pending.Push(entry);
                    }
                }
                else if (string.Equals(Path.GetExtension(entry), ".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    yield return entry;
                }
            }
        }
    }

    private static string? ResolveShortcut(string shortcutPath)
    {
        object? shell = null;
        object? shortcut = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return null;
            }

            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                [shortcutPath]);
            return shortcut?.GetType().InvokeMember(
                "TargetPath",
                BindingFlags.GetProperty,
                null,
                shortcut,
                null) as string;
        }
        catch (Exception exception) when (exception is COMException or TargetInvocationException or ArgumentException)
        {
            return null;
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            _ = Marshal.FinalReleaseComObject(value);
        }
    }

    private static bool ContainsDouyin(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.Contains("douyin", StringComparison.OrdinalIgnoreCase) || value.Contains("抖音", StringComparison.Ordinal));
}
