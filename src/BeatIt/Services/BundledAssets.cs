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
    /// 버전마다 한 번만 푼다. 마커를 두지 않으면 사용자가 지운 기본 에셋이 실행할 때마다 되살아난다.
    /// 이미 있는 파일은 건드리지 않아서 고쳐둔 그림도 그대로 남는다.
    /// </summary>
    public static void EnsureExtracted()
    {
        foreach ((string resource, Func<string> root) in Bundles)
        {
            Extract(resource, root());
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
