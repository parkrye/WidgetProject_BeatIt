# 기본 캐릭터 그림으로 exe 아이콘(.ico)을 만든다. 캐릭터 그림을 바꿀 때만 다시 돌리면 된다.
param(
    [string]$Source = "src\BeatIt\assets\characters\clawd\idle\icons8-clawd-480.png",
    [string]$Output = "src\BeatIt\assets\beatit.ico"
)

$ErrorActionPreference = "Stop"

# ICO 는 바이트 폭이 정확해야 해서 PowerShell 오버로드 해석에 맡기지 않고 C# 으로 쓴다.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class IconBuilder
{
    public static void Build(string sourcePath, string outputPath, int[] sizes)
    {
        var frames = new List<byte[]>();
        using (var source = Image.FromFile(sourcePath))
        {
            foreach (int size in sizes)
            {
                frames.Add(Render(source, size));
            }
        }

        using (var file = File.Create(outputPath))
        using (var writer = new BinaryWriter(file))
        {
            writer.Write((ushort)0);              // 예약
            writer.Write((ushort)1);              // 1 = 아이콘
            writer.Write((ushort)frames.Count);

            // ICONDIRENTRY 는 16바이트 고정이라 데이터 시작 위치를 미리 계산할 수 있다.
            int offset = 6 + 16 * frames.Count;
            for (int i = 0; i < frames.Count; i++)
            {
                byte dimension = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];   // 256 은 0 으로 적는 규칙
                writer.Write(dimension);
                writer.Write(dimension);
                writer.Write((byte)0);            // 팔레트 색 수(0 = 트루컬러)
                writer.Write((byte)0);            // 예약
                writer.Write((ushort)1);          // 평면 수
                writer.Write((ushort)32);         // 비트 수
                writer.Write((uint)frames[i].Length);
                writer.Write((uint)offset);
                offset += frames[i].Length;
            }

            foreach (byte[] frame in frames)
            {
                writer.Write(frame);
            }
        }
    }

    private static byte[] Render(Image source, int size)
    {
        using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.Clear(Color.Transparent);
                graphics.DrawImage(source, 0, 0, size, size);
            }

            using (var buffer = new MemoryStream())
            {
                bitmap.Save(buffer, ImageFormat.Png);
                return buffer.ToArray();
            }
        }
    }
}
"@

$sourcePath = (Resolve-Path $Source).Path
$outputPath = Join-Path (Get-Location).Path $Output
[IconBuilder]::Build($sourcePath, $outputPath, [int[]]@(16, 32, 48, 64, 128, 256))
Write-Output "wrote $Output"
