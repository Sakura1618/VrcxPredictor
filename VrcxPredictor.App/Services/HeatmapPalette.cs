using System;
using System.Windows.Media;

namespace VrcxPredictor.App.Services;

public static class HeatmapPalette
{
    public static Brush BrushFor(double v)
    {
        v = Math.Clamp(v, 0, 1);

        if (v <= 0.0001)
            return Brushes.Transparent;

        var anchors = new (double t, Color c)[]
        {
            (0.00, Color.FromArgb(0,   0,   0,   0)),
            (0.10, Color.FromArgb(180,  0, 160, 170)),
            (0.35, Color.FromArgb(200,  0, 200, 120)),
            (0.60, Color.FromArgb(210, 240, 210,  80)),
            (0.80, Color.FromArgb(220, 240, 140,  60)),
            (1.00, Color.FromArgb(230, 240,  60,  60)),
        };

        (double t0, Color c0) = anchors[0];
        (double t1, Color c1) = anchors[^1];

        for (int i = 0; i < anchors.Length - 1; i++)
        {
            if (v >= anchors[i].t && v <= anchors[i + 1].t)
            {
                t0 = anchors[i].t; c0 = anchors[i].c;
                t1 = anchors[i + 1].t; c1 = anchors[i + 1].c;
                break;
            }
        }

        double u = (Math.Abs(t1 - t0) < 1e-9) ? 0 : (v - t0) / (t1 - t0);

        byte A = (byte)(c0.A + (c1.A - c0.A) * u);
        byte R = (byte)(c0.R + (c1.R - c0.R) * u);
        byte G = (byte)(c0.G + (c1.G - c0.G) * u);
        byte B = (byte)(c0.B + (c1.B - c0.B) * u);

        return new SolidColorBrush(Color.FromArgb(A, R, G, B));
    }
}
