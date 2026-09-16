# 기본 테마의 타격 이펙트 PNG 를 그린다. 기본 이펙트를 바꿀 때만 다시 돌리면 된다.
param(
    [string]$Output = "src\BeatIt\assets\themes\default\effects"
)

$ErrorActionPreference = "Stop"

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class EffectBuilder
{
    // 캐릭터를 가리지 않도록 작고 얇게 그린다.
    private const int Size = 128;
    private const float Center = Size / 2f;

    public static void BuildAll(string folder)
    {
        Save(folder, "impact-star.png", Star);
        Save(folder, "impact-burst.png", Burst);
        Save(folder, "impact-ring.png", Ring);
        Save(folder, "impact-spark.png", Spark);
    }

    private static void Save(string folder, string name, Action<Graphics> paint)
    {
        using (var bitmap = new Bitmap(Size, Size, PixelFormat.Format32bppArgb))
        {
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                paint(graphics);
            }

            bitmap.Save(Path.Combine(folder, name), ImageFormat.Png);
        }
    }

    // 뾰족한 별. 가장 흔한 "퍽" 하는 타격감.
    private static void Star(Graphics g)
    {
        PointF[] points = Points(6, 48f, 19f, 0f);
        using (var fill = new SolidBrush(Color.FromArgb(225, 255, 206, 102)))
        using (var edge = new Pen(Color.FromArgb(235, 214, 118, 32), 3f) { LineJoin = LineJoin.Round })
        {
            g.FillPolygon(fill, points);
            g.DrawPolygon(edge, points);
        }
    }

    // 사방으로 뻗는 선. 세게 맞은 느낌.
    private static void Burst(Graphics g)
    {
        using (var pen = new Pen(Color.FromArgb(220, 255, 190, 96), 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            for (int i = 0; i < 8; i++)
            {
                double angle = Math.PI * 2 * i / 8;
                float near = i % 2 == 0 ? 20f : 16f;
                float far = i % 2 == 0 ? 50f : 38f;
                var from = new PointF(Center + (float)(Math.Cos(angle) * near), Center + (float)(Math.Sin(angle) * near));
                var to = new PointF(Center + (float)(Math.Cos(angle) * far), Center + (float)(Math.Sin(angle) * far));
                g.DrawLine(pen, from, to);
            }
        }
    }

    // 퍼져 나가는 충격파.
    private static void Ring(Graphics g)
    {
        using (var outer = new Pen(Color.FromArgb(210, 255, 255, 255), 4f))
        using (var inner = new Pen(Color.FromArgb(180, 255, 198, 110), 2.5f))
        {
            g.DrawEllipse(outer, Center - 46f, Center - 46f, 92f, 92f);
            g.DrawEllipse(inner, Center - 30f, Center - 30f, 60f, 60f);
        }
    }

    // 네 갈래 반짝임. 가벼운 타격에 어울린다.
    private static void Spark(Graphics g)
    {
        PointF[] points = Points(4, 48f, 9f, 0f);
        using (var fill = new SolidBrush(Color.FromArgb(225, 250, 252, 255)))
        using (var edge = new Pen(Color.FromArgb(220, 120, 198, 255), 2.5f) { LineJoin = LineJoin.Round })
        {
            g.FillPolygon(fill, points);
            g.DrawPolygon(edge, points);
        }
    }

    // 바깥/안쪽 반지름을 번갈아 찍어 별 모양 꼭짓점을 만든다.
    private static PointF[] Points(int spikes, float outer, float inner, float startDegrees)
    {
        var points = new PointF[spikes * 2];
        for (int i = 0; i < points.Length; i++)
        {
            double angle = Math.PI * 2 * i / points.Length + startDegrees * Math.PI / 180.0 - Math.PI / 2;
            float radius = i % 2 == 0 ? outer : inner;
            points[i] = new PointF(Center + (float)(Math.Cos(angle) * radius), Center + (float)(Math.Sin(angle) * radius));
        }

        return points;
    }
}
"@

$folder = Join-Path (Get-Location).Path $Output
New-Item -ItemType Directory -Force -Path $folder | Out-Null
[EffectBuilder]::BuildAll($folder)
Get-ChildItem $folder -Filter *.png | ForEach-Object { $_.Name }
