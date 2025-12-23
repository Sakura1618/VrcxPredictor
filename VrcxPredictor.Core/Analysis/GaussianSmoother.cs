namespace VrcxPredictor.Core.Analysis;

public static class GaussianSmoother
{
    public static double[,] Smooth(double[,] p, double sigmaTime, double sigmaDay)
    {
        var a = (double[,])p.Clone();

        if (sigmaTime > 0)
            a = Convolve1D(a, axis: 1, sigma: sigmaTime, modeWrap: true);

        if (sigmaDay > 0)
            a = Convolve1D(a, axis: 0, sigma: sigmaDay, modeWrap: false);

        return a;
    }

    private static double[,] Convolve1D(double[,] src, int axis, double sigma, bool modeWrap)
    {
        int h = src.GetLength(0);
        int w = src.GetLength(1);

        var kernel = GaussianKernel(sigma);
        int r = kernel.Length / 2;

        var dst = new double[h, w];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            double sum = 0;

            for (int k = -r; k <= r; k++)
            {
                int yy = y, xx = x;

                if (axis == 1)
                {
                    xx = x + k;
                    if (modeWrap)
                    {
                        xx %= w;
                        if (xx < 0) xx += w;
                    }
                    else
                    {
                        xx = Math.Clamp(xx, 0, w - 1);
                    }
                }
                else
                {
                    yy = y + k;
                    yy = modeWrap ? ((yy % h) + h) % h : Math.Clamp(yy, 0, h - 1);
                }

                sum += src[yy, xx] * kernel[k + r];
            }

            dst[y, x] = sum;
        }

        return dst;
    }

    private static double[] GaussianKernel(double sigma)
    {
        int radius = Math.Max(1, (int)Math.Ceiling(3 * sigma));
        int size = radius * 2 + 1;
        var k = new double[size];

        double s2 = 2 * sigma * sigma;
        double total = 0;

        for (int i = -radius; i <= radius; i++)
        {
            double v = Math.Exp(-(i * i) / s2);
            k[i + radius] = v;
            total += v;
        }

        for (int i = 0; i < size; i++)
            k[i] /= total;

        return k;
    }
}
