using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace PixelGallery
{
    public sealed partial class GalleryPrototype : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset font;
        [SerializeField] NpcProfile[] visitors;
        [SerializeField] Material galleryLit, galleryUnlit;
        Color32[] palette = {
            new Color32(0,0,0,0), new Color32(37,40,48,255), new Color32(255,249,235,255),
            new Color32(220,77,63,255), new Color32(246,170,74,255), new Color32(245,216,91,255),
            new Color32(92,154,113,255), new Color32(75,131,185,255), new Color32(157,108,169,255),
            new Color32(247,170,187,255), new Color32(152,205,220,255), new Color32(169,207,155,255), new Color32(177,152,202,255),
            new Color32(113,76,57,255), new Color32(234,196,166,255), new Color32(148,150,155,255), new Color32(69,75,93,255),
            new Color32(166,43,57,255), new Color32(235,112,40,255), new Color32(255,228,157,255), new Color32(52,108,78,255),
            new Color32(35,84,138,255), new Color32(107,67,139,255), new Color32(238,129,170,255), new Color32(104,193,181,255),
            new Color32(254,252,244,255), new Color32(201,177,140,255), new Color32(117,137,103,255), new Color32(78,114,120,255),
            new Color32(91,77,101,255), new Color32(226,157,105,255), new Color32(199,80,121,255), new Color32(25,29,32,255) };
        readonly string[] reactions = { "지나침", "관심", "좋아요", "열성팬 ★" };
        RectTransform safeRoot;
        Canvas uiCanvas;
        GameObject atelier, results, confirmation;
        Drawing drawing;
        DrawingSurface surface;
        TMP_Text status, toolStatus, resultText;
        TMP_InputField title;
        UnityEngine.UI.Image[] swatches;
        UnityEngine.UI.Outline[] colorBorders;
        string artworkId;
        Judgment judgment;
        double[] analyzedFeatures;
        Texture2D preview;
        UnityEngine.UI.RawImage previewImage;
        Rect lastSafe;
        Vector2Int lastScreen;
        public Drawing Drawing => drawing;
        public Judgment LastJudgment => judgment;

        void Awake()
        {
            Array.Resize(ref palette,256);
            drawing = new Drawing(); artworkId = Guid.NewGuid().ToString("N");
            var canvasObject = new GameObject("PrototypeCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            uiCanvas = canvasObject.GetComponent<Canvas>(); uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
            var background = Panel("Background", canvasObject.transform, new Color32(248,243,232,255));
            Stretch(background);
            safeRoot = Panel("SafeArea", canvasObject.transform, Color.clear);
            ApplySafeArea();
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            BuildAtelier(); BuildResults(); BuildConfirmation(); BuildExhibition(); BuildGrowth(); BuildStudioUI();
            results.SetActive(false); confirmation.SetActive(false);
            LoadSession(); UpdateStatus();
        }
        void Update()
        {
            TickExhibition(); TickStudio();
            if (lastSafe != Screen.safeArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height) ApplySafeArea();
        }
        void ApplySafeArea()
        {
            lastSafe = Screen.safeArea; lastScreen = new Vector2Int(Screen.width, Screen.height);
            #if UNITY_EDITOR
            safeRoot.anchorMin = Vector2.zero; safeRoot.anchorMax = Vector2.one;
#else
            safeRoot.anchorMin = new Vector2(lastSafe.xMin / Screen.width, lastSafe.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(lastSafe.xMax / Screen.width, lastSafe.yMax / Screen.height);
#endif
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }
        void BuildAtelier()
        {
            var panel = Panel("Atelier", safeRoot, Color.clear); Stretch(panel); atelier = panel.gameObject;
            wallet = Label(panel, "", 32, 0.88f, 50); wallet.gameObject.SetActive(false);
            var inputRect = Place("Title", panel, 0.79f, 430, 80, -570);
            inputRect.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            title = inputRect.gameObject.AddComponent<TMP_InputField>();
            var inputText = Label(inputRect, "", 44, 0.5f, 90); Stretch(inputText.rectTransform, 20);
            inputText.alignment = TextAlignmentOptions.MidlineLeft;
            title.textViewport = inputText.rectTransform; title.textComponent = inputText;
            title.characterLimit = 40; title.text = "나의 첫 그림";
            title.onEndEdit.AddListener(_ => SaveDraft());
            var viewport = Place("DrawingViewport", panel, 0.5f, 680, 680, 270);
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.clear;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var imageRect = Place("DrawingSurface", viewport, 0.5f, 680, 680);
            imageRect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            surface = imageRect.gameObject.AddComponent<DrawingSurface>(); surface.Initialize(drawing, palette);
            surface.Changed = DraftChanged;
            surface.SelectionChanged = UpdateStatus;
            for (int i = 1; i < PixelGallery.Drawing.Size; i++)
            {
                var line = Panel("Grid", imageRect, new Color(0.15f,0.12f,0.1f,0.13f));
                line.anchorMin = new Vector2((float)i / PixelGallery.Drawing.Size, 0); line.anchorMax = new Vector2((float)i / PixelGallery.Drawing.Size, 1);
                line.sizeDelta = new Vector2(1, 0); line.anchoredPosition = Vector2.zero;
                var horizontal = Panel("Grid", imageRect, new Color(0.15f,0.12f,0.1f,0.13f));
                horizontal.anchorMin = new Vector2(0, (float)i / PixelGallery.Drawing.Size); horizontal.anchorMax = new Vector2(1, (float)i / PixelGallery.Drawing.Size);
                horizontal.sizeDelta = new Vector2(0, 1); horizontal.anchoredPosition = Vector2.zero;
            }
            toolStatus = Label(panel, "", 28, 0.75f, 50);
            string[] tools = { "연필", "지우개", "되돌리기" };
            for (int i = 0; i < 3; i++)
            {
                int tool = i;
                Button(panel, tools[i], 0.65f-i*.105f, 260, 90, -630, () => {
                    surface.EndStroke();
                    if (tool == 2)
                    {
                        if (drawing.CanUndo) { drawing.Undo(); surface.Refresh(); DraftChanged(); }
                    }
                    else surface.Tool = tool;
                    UpdateStatus();
                });
            }
            swatches = new UnityEngine.UI.Image[8];
            colorBorders = new UnityEngine.UI.Outline[8];
            for (int i = 0; i < 8; i++)
            {
                int slot = i; byte color = (byte)(i + 1);
                var button = Button(panel, "", i < 4 ? 0.315f : 0.215f, 100, 90, -800+(i%4)*115, () => { surface.EndStroke(); surface.SelectedColor = PaletteIndex(slot); surface.Tool = 0; UpdateStatus(); });
                button.GetComponent<UnityEngine.UI.Image>().color = palette[color];
                var swatchButton = button.GetComponent<UnityEngine.UI.Button>();
                var tint = swatchButton.colors; tint.selectedColor = tint.highlightedColor = Color.white; tint.pressedColor = new Color(.9f,.9f,.9f,1); swatchButton.colors = tint;
                swatches[i] = button.GetComponent<UnityEngine.UI.Image>();
                var border = button.AddComponent<UnityEngine.UI.Outline>();
                colorBorders[i] = border;
                border.effectColor = new Color32(33,84,61,255); border.effectDistance = new Vector2(5,-5);
            }
            status = Label(panel, "", 28, 0.13f, 50); status.rectTransform.sizeDelta = new Vector2(500,50); status.rectTransform.anchoredPosition = new Vector2(270,0);
            Button(panel, "완성하고 전시하기", 0.94f, 300, 90, 270, Evaluate);

        }
        void BuildResults()
        {
            var panel = Panel("Results", safeRoot, Color.clear); Stretch(panel); results = panel.gameObject;
            Label(panel, "관람객의 반응", 64, 0.94f, 110);
            var image = Place("Artwork", panel, 0.55f, 400, 400, -580);
            previewImage = image.gameObject.AddComponent<UnityEngine.UI.RawImage>(); previewImage.raycastTarget = false;
            var scrollRect = Place("VisitorScroll", panel, .5f, 1000, 670, 280);
            var scroll = scrollRect.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport = Panel("Viewport", scrollRect, new Color(1,1,1,.4f)); Stretch(viewport);
            viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var content = Place("Content", viewport, 1, 880, 2000);
            content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f,1); content.sizeDelta = new Vector2(-40,2000);
            resultText = Label(content, "", 40, .5f, 2000); Stretch(resultText.rectTransform);
            resultText.alignment = TextAlignmentOptions.TopLeft;
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var hint = Label(panel, "감상 목록 · 위아래로 스크롤", 30, .84f, 50); hint.rectTransform.anchoredPosition = new Vector2(280,0);
            settlement = Label(panel, "", 32, .245f, 190); settlement.rectTransform.sizeDelta = new Vector2(560,190); settlement.rectTransform.anchoredPosition = new Vector2(-580,0);
            Button(panel, "판매하기", .07f, 440, 90, -235, () => AskSale(null));
            Button(panel, "보유하기", .07f, 440, 90, 235, KeepArtwork);
        }
        void BuildConfirmation()
        {
            var shade = Panel("ConfirmClear", safeRoot, new Color(0,0,0,0.75f)); Stretch(shade); confirmation = shade.gameObject;
            Label(shade, "그림을 비울까요?\n되돌리기로 복원할 수 있어요.", 40, 0.56f, 200).color = Color.white;
            Button(shade, "취소", 0.33f, 350, 90, -200, () => confirmation.SetActive(false));
            Button(shade, "비우기", 0.33f, 350, 90, 200, () => { surface.EndStroke(); drawing.Clear(); surface.Refresh(); DraftChanged(); confirmation.SetActive(false); UpdateStatus(); });
        }
        public void Evaluate()
        {
            surface.EndStroke();
            if (session == null || session.Data.Pending != null) return;
            if (!drawing.CanExhibit) { status.text = $"{PixelGallery.Drawing.MinimumPaintedPixels}칸 이상 칠해 주세요 (3%)"; return; }
            if (!SaveDraft()) return;
            MakePreview(drawing.Pixels,draftColors.ToArray()); hangImage.texture = preview;
            hangText.text = "이 그림을 전시할까요?\n" + title.text + "\n기존 전시작은 컬렉션에 남아요.";
            hangConfirmation.SetActive(true);
        }
        string Comment(NpcVerdict v)
        {
            if (v.Liked)
            {
                if (v.Guaranteed) return "제 취향과 다른 점도 있지만, 이 그림에 마음이 가네요.";
                if (v.Id.StartsWith("minimalist#", StringComparison.Ordinal)) return "여백과 간결한 색이 마음을 편하게 해요.";
                if (v.Id.StartsWith("warm#", StringComparison.Ordinal)) return "이 따뜻한 빛, 오래 보게 되네.";
                if (v.Id.StartsWith("order#", StringComparison.Ordinal)) return "양쪽이 차분하게 맞물리는 모습이 좋아요.";
                if (v.Id.StartsWith("trend#", StringComparison.Ordinal)) return "이번 유행의 색을 잘 담았네요.";
                return "작은 조각과 색의 경계가 눈길을 끄네요.";
            }
            NpcTaste taste = null;
            foreach (var profile in visitors) if (v.Id.StartsWith(profile.Taste.Id + "#", StringComparison.Ordinal)) { taste = profile.Taste; break; }
            bool more = analyzedFeatures[v.WeakestFeature] < taste.Targets[v.WeakestFeature];
            switch ((Feature)v.WeakestFeature)
            {
                case Feature.Coverage: return more ? "저는 화면 가득 펼쳐진 그림이 좋아요." : "저는 빈 공간이 있는 그림을 좋아해요.";
                case Feature.Colors: return more ? "다음에는 여러 색이 어우러진 그림도 보고 싶어요." : "저는 적은 색으로 그린 담백한 그림이 좋아요.";
                case Feature.Warmth: return more ? "빨강이나 노랑처럼 따뜻한 색이 더 있으면 좋겠어요." : "파랑이나 초록처럼 시원한 색을 더 보고 싶어요.";
                case Feature.Saturation: return more ? "조금 더 선명한 색을 보고 싶어요." : "저는 부드럽고 차분한 색을 좋아해요.";
                case Feature.Brightness: return more ? "조금 더 밝은 빛이 담긴 그림도 보고 싶어요." : "저는 어두운 색이 만드는 깊은 분위기를 좋아해요.";
                case Feature.Symmetry: return more ? "저는 양쪽이 균형 있게 어우러진 그림이 좋아요." : "한쪽에 변화를 주면 더 재미있게 볼 것 같아요.";
                case Feature.Complexity: return more ? "작은 무늬와 색의 경계가 더 있으면 오래 볼 것 같아요." : "저는 선과 무늬가 적은 간결한 그림을 좋아해요.";
                case Feature.Regions: return more ? "여러 색 조각이 흩어진 그림도 보고 싶어요." : "색 조각을 이어 그리면 더 편안하게 볼 것 같아요.";
                case Feature.Center: return more ? "가운데에 눈길을 끄는 색이 있으면 좋겠어요." : "가장자리에도 색이 퍼지면 더 재미있게 볼 것 같아요.";
                default: return more ? "오늘 유행하는 색을 조금 더 보고 싶어요." : "유행보다 작가님만의 색을 더 보고 싶어요.";
            }
        }
        void UpdateStatus()
        {
            if (toolStatus == null || swatches == null) return;
            if (currentColor != null) currentColor.color = palette[surface.SelectedColor];
            toolStatus.text = new[] { "연필", "지우개", "스포이드", "큰 붓", "채우기" }[surface.Tool] + (surface.Mirror ? " · 대칭" : "");
            for (int i = 0; i < swatches.Length; i++)
            {
                byte color = PaletteIndex(i);
                swatches[i].gameObject.SetActive(color != 0); swatches[i].color = palette[color];
                colorBorders[i].enabled = surface.SelectedColor == color;
            }
            int count = 0; foreach (byte p in drawing.Pixels) if (p != 0) count++;
            if (status != null) status.text = count == 0 ? "색을 골라 자유롭게 그려보세요" : "";
        }
        RectTransform Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
            var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = name.StartsWith("Confirm", StringComparison.Ordinal) || name == "SaveError";
            return (RectTransform)go.transform;
        }
        static RectTransform Place(string name, Transform parent, float y, float w, float h, float x = 0)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = new Vector2(0.5f,y);
            rect.sizeDelta = new Vector2(w,h); rect.anchoredPosition = new Vector2(x,0); return rect;
        }
        static void Stretch(RectTransform r, float inset = 0)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.one * inset; r.offsetMax = Vector2.one * -inset;
        }
        TMP_Text Label(Transform parent, string value, float size, float y, float height)
        {
            var rect = Place("Text",parent,y,920,height);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size;
            text.fontStyle = size >= 56 ? FontStyles.Bold : FontStyles.Normal;
            text.text = value; text.color = new Color32(40,43,51,255); text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false; text.richText = false; return text;
        }
        GameObject Button(Transform parent, string value, float y, float w, float h, float x, UnityEngine.Events.UnityAction action)
        {
            var rect = Place(value, parent, y, w, h, x);
            rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(213,228,213,255);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.onClick.AddListener(action);
            var colors = button.colors; colors.selectedColor = new Color32(154,192,161,255); colors.pressedColor = new Color32(128,173,139,255); colors.fadeDuration = 0.15f; button.colors = colors;
            var text = Label(rect, value, 32, 0.5f, h); Stretch(text.rectTransform, 5);
            if (value == "완성하고 전시하기" || value == "전시 시작" || value == "판매 확정" || value == "판매하기")
            {
                rect.GetComponent<UnityEngine.UI.Image>().color = new Color32(33,84,61,255);
                text.color = Color.white; text.fontStyle = FontStyles.Bold;
            }
            return rect.gameObject;
        }
        void OnDestroy() { if (preview != null) Destroy(preview); foreach (var texture in thumbnails) if (texture != null) Destroy(texture); }
    }
}
