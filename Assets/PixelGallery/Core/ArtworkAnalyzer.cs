using System;
using System.Collections.Generic;

namespace PixelGallery
{
    public struct PixelColor
    {
        public byte R, G, B;
        public PixelColor(byte r, byte g, byte b) { R = r; G = g; B = b; }
    }

    public enum Feature { Coverage, Colors, Warmth, Saturation, Brightness, Symmetry, Complexity, Regions, Center, Theme }

    public static class ArtworkAnalyzer
    {
        public static double[] Analyze(byte[] pixels, int size, PixelColor[] palette, PixelColor theme)
        {
            if (size < 2 || pixels == null || pixels.Length != size * size || palette == null || palette.Length < 2)
                throw new ArgumentException("Invalid canvas or palette.");
            var f = new double[10];
            var colors = new HashSet<byte>();
            int filled = 0, symmetry = 0, boundaries = 0, regions = 0;
            double xSum = 0, ySum = 0;
            var visited = new bool[pixels.Length];
            var queue = new int[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte index = pixels[i];
                if (index >= palette.Length) throw new ArgumentException("Pixel outside palette.");
                int x = i % size, y = i / size;
                if (index == pixels[y * size + size - 1 - x]) symmetry++;
                if (x + 1 < size && index != pixels[i + 1]) boundaries++;
                if (y + 1 < size && index != pixels[i + size]) boundaries++;
                if (index == 0) continue;
                filled++;
                colors.Add(index);
                xSum += x; ySum += y;
                var c = palette[index];
                double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
                double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
                double delta = max - min, hue = 0;
                if (delta > 0)
                {
                    if (max == r) hue = ((g - b) / delta + 6) % 6;
                    else if (max == g) hue = (b - r) / delta + 2;
                    else hue = (r - g) / delta + 4;
                    hue *= 60;
                }
                f[2] += delta > 0 && (hue <= 60 || hue >= 300) ? 1 : 0;
                f[3] += max == 0 ? 0 : delta / max;
                f[4] += max;
                double dr = (c.R - theme.R) / 255.0, dg = (c.G - theme.G) / 255.0, db = (c.B - theme.B) / 255.0;
                f[9] += 1 - Math.Sqrt((dr * dr + dg * dg + db * db) / 3);
                if (visited[i]) continue;
                regions++;
                int head = 0, tail = 0;
                queue[tail++] = i; visited[i] = true;
                while (head < tail)
                {
                    int p = queue[head++], px = p % size, py = p / size;
                    if (px > 0) Enqueue(p - 1, index, pixels, visited, queue, ref tail);
                    if (px + 1 < size) Enqueue(p + 1, index, pixels, visited, queue, ref tail);
                    if (py > 0) Enqueue(p - size, index, pixels, visited, queue, ref tail);
                    if (py + 1 < size) Enqueue(p + size, index, pixels, visited, queue, ref tail);
                }
            }
            f[0] = (double)filled / pixels.Length;
            f[1] = Math.Min(1, colors.Count / 16.0);
            f[5] = (double)symmetry / pixels.Length;
            f[6] = (double)boundaries / (2 * size * (size - 1));
            f[7] = (double)regions / pixels.Length;
            if (filled > 0)
            {
                f[2] /= filled; f[3] /= filled; f[4] /= filled; f[9] /= filled;
                double center = (size - 1) / 2.0;
                double dx = xSum / filled - center, dy = ySum / filled - center;
                f[8] = 1 - Math.Sqrt(dx * dx + dy * dy) / (Math.Sqrt(2) * center);
            }
            return f;
        }

        static void Enqueue(int p, byte color, byte[] pixels, bool[] visited, int[] queue, ref int tail)
        {
            if (visited[p] || pixels[p] != color) return;
            visited[p] = true; queue[tail++] = p;
        }
    }
}
