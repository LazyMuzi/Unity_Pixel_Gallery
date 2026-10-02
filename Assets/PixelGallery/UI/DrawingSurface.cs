using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PixelGallery
{
    public sealed class DrawingSurface : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler
    {
        public Drawing Drawing { get; private set; }
        public byte SelectedColor = 1;
        public int Tool;
        public bool Mirror;
        public System.Action Changed, SelectionChanged;
        public System.Action<Vector2> Stroked;
        public Texture Texture => texture;
        UnityEngine.UI.RawImage image;
        RectTransform rect;
        Canvas canvas;
        Texture2D texture;
        Color32[] palette, display;
        bool dirty, drawing, pinching, changed;
        int lastX, lastY, pointerId;
        float pinchDistance, zoom = 1;
        Vector2 pinchCenter;

        public void Initialize(Drawing value, Color32[] colors)
        {
            Drawing = value; palette = colors;
            rect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            image = GetComponent<UnityEngine.UI.RawImage>();
            texture = new Texture2D(PixelGallery.Drawing.Size, PixelGallery.Drawing.Size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            display = new Color32[PixelGallery.Drawing.Size * PixelGallery.Drawing.Size];
            image.texture = texture;
            dirty = true;
        }

        public static Color32 ResolveColor(byte index,Color32[] palette,int[] custom)
        {
            if (index <= 32) return palette[index];
            int rgb = custom[index-33]; return new Color32((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb,255);
        }
        public void Refresh() { dirty = true; LateUpdate(); }
        public void Zoom(float amount)
        {
            zoom = Mathf.Clamp(zoom * amount, 1, 4);
            rect.localScale = Vector3.one * zoom;
            ClampPan();
        }
        void ClampPan()
        {
            var limit = rect.rect.size * ((zoom - 1) / 2);
            var p = rect.anchoredPosition;
            rect.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -limit.x, limit.x), Mathf.Clamp(p.y, -limit.y, limit.y));
        }
        void Update()
        {
            var screen = Touchscreen.current;
            if (screen == null) return;
            int count = 0; Vector2 first = default, second = default;
            foreach (var touch in screen.touches)
            {
                if (!touch.press.isPressed) continue;
                if (count == 0) first = touch.position.ReadValue();
                else if (count == 1) second = touch.position.ReadValue();
                count++;
            }
            if (count < 2) { pinching = false; return; }
            var viewport = (RectTransform)rect.parent;
            if (!pinching && (!RectTransformUtility.RectangleContainsScreenPoint(viewport, first) || !RectTransformUtility.RectangleContainsScreenPoint(viewport, second))) return;
            EndStroke();
            float distance = Vector2.Distance(first, second);
            Vector2 center = (first + second) / 2;
            if (pinching && pinchDistance > 0)
            {
                Zoom(distance / pinchDistance);
                float scale = canvas.scaleFactor;
                rect.anchoredPosition += (center - pinchCenter) / scale;
                ClampPan();
            }
            pinchDistance = distance; pinchCenter = center; pinching = true;
        }
        void LateUpdate()
        {
            if (!dirty || texture == null) return;
            for (int i = 0; i < display.Length; i++)
            {
                byte p = Drawing.Pixels[i];
                display[i] = p == 0 ? new Color32(248, 243, 232, 255) : palette[p];
            }
            texture.SetPixels32(display); texture.Apply(false); dirty = false;
        }
        bool Cell(PointerEventData e, out int x, out int y)
        {
            x = y = -1;
            if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)rect.parent,e.position,e.pressEventCamera)) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var p);
            x = Mathf.FloorToInt((p.x - rect.rect.xMin) / rect.rect.width * PixelGallery.Drawing.Size);
            y = Mathf.FloorToInt((p.y - rect.rect.yMin) / rect.rect.height * PixelGallery.Drawing.Size);
            return x >= 0 && y >= 0 && x < PixelGallery.Drawing.Size && y < PixelGallery.Drawing.Size;
        }
        public void OnPointerDown(PointerEventData e)
        {
            var screen = Touchscreen.current;
            int touches = 0;
            if (screen != null) foreach (var touch in screen.touches) if (touch.press.isPressed) touches++;
            if (touches > 1) { EndStroke(); return; }
            if (pinching || drawing || e.button != PointerEventData.InputButton.Left || !Cell(e, out int x, out int y)) return;
            if (Tool == 2)
            {
                byte p = Drawing.Pixels[y * PixelGallery.Drawing.Size + x];
                if (p != 0) SelectedColor = p;
                SelectionChanged?.Invoke(); return;
            }
            if (Tool == 4)
            {
                Drawing.BeginStroke(); bool filled = Drawing.Fill(x,y,SelectedColor);
                if (Mirror) filled |= Drawing.Fill(PixelGallery.Drawing.Size-1-x,y,SelectedColor);
                Drawing.EndStroke(); dirty |= filled; if (filled) { Stroked?.Invoke(new Vector2(x/31f,y/31f)); Changed?.Invoke(); } return;
            }
            Drawing.BeginStroke(); drawing = true; changed = false; pointerId = e.pointerId;
            lastX = x; lastY = y; Draw(x, y);
        }
        public void OnDrag(PointerEventData e)
        {
            if (!drawing || pinching || e.pointerId != pointerId) return;
            if (Cell(e, out int x, out int y)) Draw(x, y);
        }
        void Draw(int x, int y)
        {
            bool painted = Drawing.Line(lastX, lastY, x, y, Tool == 1 ? (byte)0 : SelectedColor,Tool == 3 ? 2 : 1,Mirror);
            lastX = x; lastY = y; dirty |= painted; changed |= painted;
            Stroked?.Invoke(new Vector2(x/31f,y/31f));
        }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointerId) EndStroke(); }
        public void OnScroll(PointerEventData e) { Zoom(e.scrollDelta.y > 0 ? 1.2f : 1 / 1.2f); }
        public void EndStroke()
        {
            if (Drawing != null) Drawing.EndStroke();
            if (drawing && changed) Changed?.Invoke();
            drawing = false; changed = false;
        }
        void OnDisable() { EndStroke(); pinching = false; pinchDistance = 0; }
        void OnDestroy() { if (texture != null) Destroy(texture); }
    }
}
