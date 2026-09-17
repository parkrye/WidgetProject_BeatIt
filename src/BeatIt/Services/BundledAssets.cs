using System.IO;
using System.IO.Compression;

namespace BeatIt.Services;

/// <summary>exe 안에 들어 있는 기본 캐릭터와 기본 테마를 사용자 폴더로 풀어놓는다.</summary>
public static class BundledAssets
{
    private static readonly (string Resource, Func<string> Root)[] Bundles =
    [
        ("BeatIt.characters.zip", () => CharacterLibrary.UserRoot),
        ("BeatIt.themes.zip", () => ThemeLibrary.Root),
    ];

    /// <summary>
    /// 이름이 바뀐 기본 캐릭터. 그냥 새 이름으로 풀면 같은 캐릭터가 두 벌이 되기 때문에,
    /// 풀기 전에 옛 폴더를 새 이름으로 옮긴다. 그 안에 직접 넣어둔 그림도 같이 따라온다.
    /// </summary>
    private static readonly (string From, string To)[] RenamedCharacters =
    [
        ("clawd", "claude"),
    ];

    /// <summary>
    /// 이름이 바뀐 그림. 옛 이름 그대로 두면 곧 풀릴 새 이름 그림과 같은 그림이 두 벌이 되어,
    /// 랜덤으로 뽑을 때 같은 장이 두 배로 걸린다. 이름만 고치니 손봐둔 그림도 그대로 남는다.
    /// </summary>
    private static readonly (string From, string To)[] RenamedFiles =
    [
        ("icons8-clawd-", "icons8-claude-"),
        ("looking-righ-480", "looking-right-480"),
    ];

    /// <summary>
    /// 버전마다 한 번만 푼다. 마커를 두지 않으면 사용자가 지운 기본 에셋이 실행할 때마다 되살아난다.
    /// 이미 있는 파일은 건드리지 않아서 고쳐둔 그림도 그대로 남는다.
    /// </summary>
    public static void EnsureExtracted()
    {
        RenameOldCharacters();

        foreach ((string resource, Func<string> root) in Bundles)
        {
            Extract(resource, root());
        }
    }

    /// <summary>
    /// 이름이 바뀌기 전 경로를 가리키고 있으면 새 경로로 고쳐 돌려준다. 그 밖에는 그대로 둔다.
    /// 캐릭터를 직접 골라둔 사람이 이름을 바꿨다고 기본값으로 떨어지면 안 된다.
    /// </summary>
    public static string? Rename(string? characterPath)
    {
        if (string.IsNullOrWhiteSpace(characterPath))
        {
            return characterPath;
        }

        foreach ((string from, string to) in RenamedCharacters)
        {
            if (SamePath(characterPath, Path.Combine(CharacterLibrary.UserRoot, from)))
            {
                return Path.Combine(CharacterLibrary.UserRoot, to);
            }
        }

        return characterPath;
    }

    private static void RenameOldCharacters()
    {
        foreach ((string from, string to) in RenamedCharacters)
        {
            string old = Path.Combine(CharacterLibrary.UserRoot, from);
            string renamed = Path.Combine(CharacterLibrary.UserRoot, to);

            // 새 이름이 이미 있으면 폴더는 손대지 않는다. 두 벌을 합칠 방법이 없다.
            if (Directory.Exists(old) && !Directory.Exists(renamed) && !TryMove(old, renamed))
            {
                continue;
            }

            RenameFilesIn(renamed);
        }
    }

    private static void RenameFilesIn(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        // 이름을 바꾸는 중에 훑으면 같은 파일이 두 번 걸리거나 빠진다. 먼저 다 담아둔다.
        foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(path);
            string renamed = RenamedFiles.Aggregate(
                name,
                (current, pair) => current.Replace(pair.From, pair.To, StringComparison.OrdinalIgnoreCase));

            if (renamed == name)
            {
                continue;
            }

            string target = Path.Combine(Path.GetDirectoryName(path)!, renamed);
            if (!File.Exists(target))
            {
                TryMove(path, target, directory: false);
            }
        }
    }

    private static bool TryMove(string from, string to, bool directory = true)
    {
        try
        {
            if (directory)
            {
                Directory.Move(from, to);
            }
            else
            {
                File.Move(from, to);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 못 옮기면 옛 이름 그대로 남는다. 위젯은 그대로 뜬다.
            return false;
        }
    }

    private static bool SamePath(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return false;
        }
    }

    private static void Extract(string resourceName, string root)
    {
        try
        {
            Directory.CreateDirectory(root);

            string marker = Path.Combine(root, $".bundled-{Version}");
            if (File.Exists(marker))
            {
                return;
            }

            using Stream? stream = typeof(BundledAssets).Assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return;
            }

            using (ZipArchive archive = new(stream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    ExtractIfMissing(entry, root);
                }
            }

            File.WriteAllText(marker, string.Empty);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // 에셋을 못 풀어도 위젯은 떠야 한다. 기본 샌드백과 기본 글꼴로 떨어진다.
        }
    }

    private static string Version =>
        typeof(BundledAssets).Assembly.GetName().Version?.ToString() ?? "0";

    private static void ExtractIfMissing(ZipArchiveEntry entry, string root)
    {
        if (string.IsNullOrEmpty(entry.Name))
        {
            return;
        }

        string? target = ResolveInsideRoot(root, entry.FullName);
        if (target is null || File.Exists(target))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        entry.ExtractToFile(target);
    }

    /// <summary>압축 항목 이름이 폴더 밖을 가리키면(zip slip) 버린다.</summary>
    private static string? ResolveInsideRoot(string root, string entryPath)
    {
        string full = Path.GetFullPath(root);
        string target = Path.GetFullPath(Path.Combine(full, entryPath));
        string prefix = full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;

        return target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? target : null;
    }
}
