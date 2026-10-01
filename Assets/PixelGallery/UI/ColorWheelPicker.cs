using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelGallery
{
    public sealed class ColorWheelPicker : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int Size = 256;
        Texture2D wheel, marker;
        readonly Color32[] pixels = new Color32[Size*Size];
        RectTransform rect, hueMarker, shadeMarker;
        Image preview;
        float hue, saturation, brightness;
        int mode, pointerId;
        public Color Selected => Color.HSVToRGB(hue,saturation,brightness);
        public void Initialize(Image sample)
        {
            rect = (RectTransform)transform; preview = sample;
            wheel = new Texture2D(Size,Size,TextureFormat.RGBA32,false); wheel.wrapMode = TextureWrapMode.Clamp;
            GetComponent<RawImage>().texture = wheel;
            marker = new Texture2D(32,32,TextureFormat.RGBA32,false);
            var dots = new Color32[1024];
            for (int y=0;y<32;y++) for (int x=0;x<32;x++)
            {
                float r = new Vector2(x-15.5f,y-15.5f).magnitude;
                dots[y*32+x] = r > 10 && r < 15 ? (r > 12 ? Color.black : Color.white) : Color.clear;
            }
            marker.SetPixels32(dots); marker.Apply();
            hueMarker = Marker("HueMarker"); shadeMarker = Marker("ShadeMarker");
            SetColor(Color.white);
        }
        RectTransform Marker(string name)
        {
            var obj = new GameObject(name,typeof(RectTransform),typeof(RawImage)); obj.transform.SetParent(transform,false);
            var image = obj.GetComponent<RawImage>(); image.texture = marker; image.raycastTarget = false;
            var target = (RectTransform)obj.transform; target.sizeDelta = Vector2.one*28; return target;
        }
        public void SetColor(Color color)
        {
            Color.RGBToHSV(color,out hue,out saturation,out brightness); Render(); UpdateMarkers();
        }
        void Render()
        {
            for (int y=0;y<Size;y++) for (int x=0;x<Size;x++)
            {
                var point = new Vector2((x+.5f)/Size*2-1,(y+.5f)/Size*2-1); float radius = point.magnitude;
                pixels[y*Size+x] = radius >= .76f && radius <= .98f ? Color.HSVToRGB(Mathf.Repeat(Mathf.Atan2(point.y,point.x)/(2*Mathf.PI),1),1,1) :
                    Mathf.Abs(point.x) <= .48f && Mathf.Abs(point.y) <= .48f ? Color.HSVToRGB(hue,(point.x+.48f)/.96f,(point.y+.48f)/.96f) : Color.clear;
            }
            wheel.SetPixels32(pixels); wheel.Apply(false);
        }
        void UpdateMarkers()
        {
            float angle = hue*2*Mathf.PI;
            hueMarker.anchoredPosition = new Vector2(Mathf.Cos(angle)*.87f,Mathf.Sin(angle)*.87f)*rect.rect.width/2;
            shadeMarker.anchoredPosition = new Vector2(saturation*.96f-.48f,brightness*.96f-.48f)*rect.rect.width/2;
            preview.color = Selected;
        }
        Vector2 Point(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,e.position,e.pressEventCamera,out var point);
            return point/(rect.rect.width/2);
        }
        public void OnPointerDown(PointerEventData e)
        {
            if (mode != 0 || e.button != PointerEventData.InputButton.Left) return;
            var point = Point(e);
            mode = point.magnitude >= .76f && point.magnitude <= 1 ? 1 : Mathf.Abs(point.x)<=.48f && Mathf.Abs(point.y)<=.48f ? 2 : 0;
            pointerId = e.pointerId; OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if (mode == 0 || e.pointerId != pointerId) return;
            var point = Point(e);
            if (mode == 1) { hue = Mathf.Repeat(Mathf.Atan2(point.y,point.x)/(2*Mathf.PI),1); Render(); }
            else { saturation = Mathf.Clamp01((point.x+.48f)/.96f); brightness = Mathf.Clamp01((point.y+.48f)/.96f); }
            UpdateMarkers();
        }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointerId) mode = 0; }
        void OnDisable() { mode = 0; }
        void OnDestroy() { if (wheel != null) Destroy(wheel); if (marker != null) Destroy(marker); }
    }
}
