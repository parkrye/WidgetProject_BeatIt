using System.IO;
using System.IO.Compression;

namespace BeatIt.Services;

/// <summary>exe 안에 들어 있는 기본 캐릭터를 사용자 캐릭터 폴더로 풀어놓는다.</summary>
public static class CharacterAssets
{
    private const string ResourceName = "BeatIt.characters.zip";

    /// <summary>
    /// 버전마다 한 번만 푼다. 마커를 두지 않으면 사용자가 지운 기본 캐릭터가 실행할 때마다 되살아난다.
    /// 이미 있는 파일은 건드리지 않아서 고쳐둔 그림도 그대로 남는다.
    /// </summary>
    public static void EnsureExtracted()
    {
        try
        {
            Directory.CreateDirectory(CharacterLibrary.UserRoot);

            string marker = Path.Combine(CharacterLibrary.UserRoot, $".bundled-{Version}");
            if (File.Exists(marker))
            {
                return;
            }

            using Stream? stream = typeof(CharacterAssets).Assembly.GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                return;
            }

            using (ZipArchive archive = new(stream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    ExtractIfMissing(entry);
                }
            }

            File.WriteAllText(marker, string.Empty);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // 캐릭터를 못 풀어도 위젯은 떠야 한다. 기본 샌드백으로 떨어진다.
        }
    }

    private static string Version =>
        typeof(CharacterAssets).Assembly.GetName().Version?.ToString() ?? "0";

    private static void ExtractIfMissing(ZipArchiveEntry entry)
    {
        if (string.IsNullOrEmpty(entry.Name))
        {
            return;
        }

        string? target = ResolveInsideRoot(entry.FullName);
        if (target is null || File.Exists(target))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        entry.ExtractToFile(target);
    }

    /// <summary>압축 항목 이름이 폴더 밖을 가리키면(zip slip) 버린다.</summary>
    private static string? ResolveInsideRoot(string entryPath)
    {
        string root = Path.GetFullPath(CharacterLibrary.UserRoot);
        string target = Path.GetFullPath(Path.Combine(root, entryPath));
        string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

        return target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? target : null;
    }
}
