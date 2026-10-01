using System;
using System.Collections.Generic;

namespace PixelGallery
{
    public sealed class Drawing
    {
        public const int Size = 32;
        public const int MinimumPaintedPixels = 31;
        public byte[] Pixels { get; private set; } = new byte[Size * Size];
        readonly List<byte[]> undo = new List<byte[]>();
        byte[] stroke;
        public bool CanUndo => undo.Count > 0;
        public bool CanExhibit
        {
            get { int count = 0; foreach (byte p in Pixels) if (p != 0) count++; return count >= MinimumPaintedPixels; }
        }
        public void Load(byte[] pixels)
        {
            if (pixels == null || pixels.Length != Size * Size) throw new ArgumentException("Invalid drawing.");
            Pixels = (byte[])pixels.Clone(); undo.Clear(); stroke = null;
        }
        public void BeginStroke() { if (stroke == null) stroke = (byte[])Pixels.Clone(); }
        public bool Paint(int x, int y, byte color)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return false;
            int i = y * Size + x;
            if (Pixels[i] == color) return false;
            Pixels[i] = color; return true;
        }
        public bool Fill(int x, int y, byte color)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return false;
            byte before = Pixels[y*Size+x]; if (before == color) return false;
            var queue = new int[Pixels.Length]; int head = 0, tail = 1;
            queue[0] = y*Size+x; Pixels[queue[0]] = color;
            while (head < tail)
            {
                int index = queue[head++], cx = index%Size, cy = index/Size;
                if (cx > 0 && Pixels[index-1] == before) { Pixels[index-1] = color; queue[tail++] = index-1; }
                if (cx < Size-1 && Pixels[index+1] == before) { Pixels[index+1] = color; queue[tail++] = index+1; }
                if (cy > 0 && Pixels[index-Size] == before) { Pixels[index-Size] = color; queue[tail++] = index-Size; }
                if (cy < Size-1 && Pixels[index+Size] == before) { Pixels[index+Size] = color; queue[tail++] = index+Size; }
            }
            return true;
        }
        public bool Line(int x0, int y0, int x1, int y1, byte color, int width = 1, bool mirror = false)
        {
            bool changed = false;
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, error = dx + dy;
            while (true)
            {
                for (int ox=0;ox<width;ox++) for (int oy=0;oy<width;oy++)
                {
                    changed |= Paint(x0+ox,y0+oy,color);
                    if (mirror) changed |= Paint(Size-1-x0-ox,y0+oy,color);
                }
                if (x0 == x1 && y0 == y1) break;
                int twice = 2 * error;
                if (twice >= dy) { error += dy; x0 += sx; }
                if (twice <= dx) { error += dx; y0 += sy; }
            }
            return changed;
        }
        public void EndStroke()
        {
            if (stroke == null) return;
            bool changed = false;
            for (int i = 0; i < Pixels.Length; i++) if (Pixels[i] != stroke[i]) { changed = true; break; }
            if (changed)
            {
                if (undo.Count == 30) undo.RemoveAt(0);
                undo.Add(stroke);
            }
            stroke = null;
        }
        public void Undo()
        {
            EndStroke();
            if (!CanUndo) return;
            Pixels = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1);
        }
        public void Clear() { BeginStroke(); Array.Clear(Pixels, 0, Pixels.Length); EndStroke(); }
    }
}
