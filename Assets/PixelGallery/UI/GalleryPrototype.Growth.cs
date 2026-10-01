using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGallery
{
    public sealed partial class GalleryPrototype
    {
        GameObject shop, toolPanel, npcBook, collectionGrid, palettePicker;
        TMP_Text shopWallet, shopMessage, gridStatus, bookText;
        readonly TMP_Text[] toolPrices = new TMP_Text[3];
        readonly Button[] cards = new Button[6];
        readonly RawImage[] cardImages = new RawImage[6];
        readonly Texture2D[] thumbnails = new Texture2D[6];
        int collectionPage;
        readonly System.Collections.Generic.List<int> draftColors = new System.Collections.Generic.List<int>();
        ColorWheelPicker colorWheel;
        Image currentColor;
        int ThemeIndex => ((session.Data.Day-1)/7)%3;
        string ThemeName => new[] { "푸른 계열", "따뜻한 색", "보랏빛" }[ThemeIndex];
        byte PaletteIndex(int slot)
        {
            return (byte)(slot+1);
        }
        double[] Analyze(byte[] pixels,int[] custom = null)
        {
            if (custom == null) custom = draftColors.ToArray();
            var colors = new PixelColor[33+custom.Length];
            for (int i=0;i<colors.Length;i++) { var color = DrawingSurface.ResolveColor((byte)i,palette,custom); colors[i] = new PixelColor(color.r,color.g,color.b); }
            return ArtworkAnalyzer.Analyze(pixels,32,colors,colors[new[] { 7,4,8 }[ThemeIndex]]);
        }
        double FreshnessPenalty(double[] values)
        {
            double nearest = 1;
            foreach (var snapshot in session.Data.Recent)
            {
                double distance = 0;
                for (int i=0;i<9;i++) { double delta = values[i]-snapshot.Values[i]; distance += delta*delta; }
                nearest = Math.Min(nearest,Math.Sqrt(distance/9));
            }
            return .15*(1-nearest);
        }
        void BuildGrowth()
        {
            toolStatus.rectTransform.sizeDelta = new Vector2(300,50); toolStatus.rectTransform.anchoredPosition = new Vector2(-630,0);
            Button(atelier.transform,"도구",.105f,260,90,-630,() => { surface.EndStroke(); toolPanel.SetActive(true); toolPanel.transform.SetAsLastSibling(); });

            var panel = Panel("Shop",safeRoot,new Color32(248,243,232,255)); Stretch(panel); shop = panel.gameObject;
            Label(panel,"상점",64,.94f,110); shopWallet = Label(panel,"",40,.875f,70);
            Label(panel,"모든 색상은 팔레트에서 무료로 사용할 수 있어요",38,.79f,70);
            string[] names = { "큰 붓 2×2", "채우기", "좌우 대칭" };
            int[] costs = { 150,200,300 }, flags = { 1,2,4 };
            for (int i=0;i<3;i++)
            {
                int flag = flags[i], cost = costs[i];
                var button = Button(panel,names[i]+" · "+cost+"코인",.65f-i*.16f,920,110,0,() => BuyTool(flag,cost));
                var name = button.GetComponentInChildren<TMP_Text>(); name.text = names[i];
                name.rectTransform.anchorMax = new Vector2(.64f,1); name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;
                toolPrices[i] = Label(button.transform,"",36,.5f,100);
                toolPrices[i].rectTransform.anchorMin = new Vector2(.64f,0); toolPrices[i].rectTransform.anchorMax = Vector2.one;
                toolPrices[i].rectTransform.offsetMin = toolPrices[i].rectTransform.offsetMax = Vector2.zero;
            }
            shopMessage = Label(panel,"",32,.22f,120);
            Button(panel,"아틀리에로",.075f,440,90,-235,ReturnToAtelier);
            Button(panel,"관람객 도감",.075f,440,90,235,OpenNpcBook);

            var tools = Panel("ConfirmTools",safeRoot,new Color32(248,243,232,255)); Stretch(tools); toolPanel = tools.gameObject;
            Label(tools,"도구와 팔레트",64,.9f,110);
            Button(tools,"큰 붓 선택",.73f,920,90,0,() => SelectTool(1,3));
            Button(tools,"채우기 선택",.61f,920,90,0,() => SelectTool(2,4));
            Button(tools,"대칭 켜기 / 끄기",.49f,920,90,0,() => SelectTool(4,0));
            Button(tools,"스포이드",.37f,240,90,-560,() => { surface.EndStroke(); surface.Tool = 2; UpdateStatus(); toolPanel.SetActive(false); });
            Button(tools,"축소",.37f,240,90,-280,() => surface.Zoom(1/1.25f));
            Button(tools,"확대",.37f,240,90,0,() => surface.Zoom(1.25f));
            Button(tools,"비우기",.37f,240,90,280,() => { toolPanel.SetActive(false); confirmation.SetActive(true); });
            Label(tools,"두 손가락으로 확대·이동 · 잠긴 도구는 상점에서 구매",28,.29f,50);
            Button(tools,"도구 닫기",.1f,920,90,0,() => toolPanel.SetActive(false));

            var picker = Panel("PalettePicker",safeRoot,new Color(0,0,0,.4f)); Stretch(picker); palettePicker = picker.gameObject;
            var paletteCard = Place("PaletteCard",picker,.5f,540,900,-620); paletteCard.gameObject.AddComponent<Image>().color = new Color32(248,243,232,255);
            Label(paletteCard,"색을 찍어 선택하세요",32,.94f,60).color = Color.white;
            paletteCard.GetComponent<Image>().color = new Color32(57,57,57,255);
            var sample = Place("SelectedColor",paletteCard,.82f,220,70); var sampleImage = sample.gameObject.AddComponent<Image>();
            var wheelRect = Place("ColorWheel",paletteCard,.48f,500,500); wheelRect.gameObject.AddComponent<RawImage>();
            colorWheel = wheelRect.gameObject.AddComponent<ColorWheelPicker>(); colorWheel.Initialize(sampleImage);
            Button(paletteCard,"이 색 사용하기",.15f,440,80,0,ApplyWheelColor);
            Button(paletteCard,"팔레트 닫기",.055f,440,60,0,() => palettePicker.SetActive(false));
            var paletteButton = Button(atelier.transform,"팔레트",.415f,260,80,-630,() => { surface.EndStroke(); colorWheel.SetColor(palette[surface.SelectedColor]); palettePicker.SetActive(true); palettePicker.transform.SetAsLastSibling(); });
            var chip = Place("CurrentColor",paletteButton.transform,.5f,60,40,80); currentColor = chip.gameObject.AddComponent<Image>(); currentColor.raycastTarget = false;
            var paletteCaption = paletteButton.GetComponentInChildren<TMP_Text>(); paletteCaption.rectTransform.sizeDelta = new Vector2(-80,0); paletteCaption.rectTransform.anchoredPosition = new Vector2(-35,0);
            palettePicker.SetActive(false);

            var book = Panel("NpcBook",safeRoot,new Color32(248,243,232,255)); Stretch(book); npcBook = book.gameObject;
            Label(book,"관람객 도감",64,.94f,110); bookText = Label(book,"",32,.51f,750);
            Button(book,"상점으로",.045f,420,80,0,() => { npcBook.SetActive(false); RefreshShop(); });

            var grid = Panel("CollectionGrid",safeRoot,new Color32(248,243,232,255)); Stretch(grid); collectionGrid = grid.gameObject;
            Label(grid,"컬렉션",64,.94f,110); gridStatus = Label(grid,"",36,.865f,80);
            for (int i=0;i<6;i++)
            {
                int slot = i;
                var card = Button(grid,"작품 "+(i+1),.68f-(i/3)*.3f,440,280,(i%3-1)*480,() => {
                    collectionIndex = collectionPage*6+slot; collectionGrid.SetActive(false); collection.SetActive(true); RefreshCollection();
                });
                cards[i] = card.GetComponent<Button>();
                var image = Place("Thumbnail",card.transform,.61f,200,200); cardImages[i] = image.gameObject.AddComponent<RawImage>(); cardImages[i].raycastTarget = false;
                var caption = card.GetComponentInChildren<TMP_Text>(); caption.fontSize = 32; caption.textWrappingMode = TextWrappingModes.NoWrap; caption.overflowMode = TextOverflowModes.Ellipsis;
                caption.rectTransform.anchorMin = Vector2.zero; caption.rectTransform.anchorMax = new Vector2(1,.22f);
                caption.rectTransform.offsetMin = caption.rectTransform.offsetMax = Vector2.zero;
            }
            Button(grid,"목록 이전",.13f,340,80,-235,() => { collectionPage = Math.Max(0,collectionPage-1); ShowCollectionGrid(); });
            Button(grid,"목록 다음",.13f,340,80,235,() => { if ((collectionPage+1)*6 < session.Data.Collection.Count) collectionPage++; ShowCollectionGrid(); });
            Button(grid,"아틀리에로",.045f,420,80,0,ReturnToAtelier);
            shop.SetActive(false); toolPanel.SetActive(false); npcBook.SetActive(false); collectionGrid.SetActive(false);
        }
        void ApplyWheelColor()
        {
            Color32 color = colorWheel.Selected; int rgb = (color.r<<16)|(color.g<<8)|color.b;
            for (byte i=1;i<=32;i++) if (palette[i].r == color.r && palette[i].g == color.g && palette[i].b == color.b)
            { surface.SelectedColor = i; surface.Tool = 0; UpdateStatus(); palettePicker.SetActive(false); return; }
            int slot = draftColors.IndexOf(rgb); bool added = slot < 0;
            // ponytail: byte pixels allow 255 colors per artwork; widen indices if larger palettes are needed.
            if (added && draftColors.Count == 223) { ShowError("한 그림에는 최대 255색을 담을 수 있어요. 기존 색을 스포이드로 선택해 주세요."); return; }
            if (added) { slot = draftColors.Count; draftColors.Add(rgb); }
            if (!SaveDraft()) { if (added) draftColors.RemoveAt(slot); return; }
            byte index = (byte)(33+slot); palette[index] = color; surface.SelectedColor = index; surface.Tool = 0;
            UpdateStatus(); palettePicker.SetActive(false);
        }
        void OpenShop()
        {
            if (session == null || session.Data.Pending != null || !SaveDraft()) return;
            toolPanel.SetActive(false); studioHub.SetActive(false); atelier.SetActive(false); shop.SetActive(true); RefreshShop();
        }
        void RefreshShop()
        {
            shopWallet.text = "코인 "+session.Data.Coins;
            shopMessage.text = "코인이 부족하면 도구 가격이 빨간색으로 표시돼요.";
            int[] costs = { 150,200,300 }, flags = { 1,2,4 };
            for (int i=0;i<3;i++)
            {
                bool owned = (session.Data.Tools&flags[i])!=0;
                toolPrices[i].text = owned ? "보유 중" : costs[i]+"코인";
                toolPrices[i].color = !owned && session.Data.Coins < costs[i] ? new Color32(170,42,40,255) : new Color32(40,43,51,255);
            }
        }
        void BuyTool(int flag,int cost)
        {
            bool bought = false; if (!TrySave(() => bought = session.BuyTool(flag))) return;
            RefreshShop(); shopMessage.text = bought ? "도구를 샀어요. 아틀리에의 도구에서 선택하세요." : (session.Data.Tools&flag)!=0 ? "이미 보유한 도구예요." : cost+"코인이 필요해요.";
        }
        void SelectTool(int flag,int tool)
        {
            if (session == null) return;
            if ((session.Data.Tools&flag)==0) { OpenShop(); return; }
            if (flag == 4) surface.Mirror = !surface.Mirror; else surface.Tool = tool;
            toolPanel.SetActive(false); UpdateStatus();
        }
        void OpenNpcBook()
        {
            string[] hints = { "여백과 간결한 색을 좋아해요.", "따뜻하고 밝은 색에 마음이 가요.", "촘촘한 경계와 풍부한 조각을 좋아해요.", "좌우 대칭과 적은 색을 좋아해요.", "이번 주 유행의 색을 찾아요." };
            var text = new System.Text.StringBuilder();
            for (int i=0;i<visitors.Length;i++)
                text.AppendLine(session.Data.SeenNpcs.Contains(visitors[i].Taste.Id) ? visitors[i].Taste.Name+"\n"+hints[i] : "아직 만나지 못한 관람객\n전시를 열면 취향을 알 수 있어요.").AppendLine();
            bookText.text = text.ToString(); npcBook.SetActive(true); npcBook.transform.SetAsLastSibling();
        }
        void ShowCollectionGrid()
        {
            collection.SetActive(false); studioHub.SetActive(false); atelier.SetActive(false); collectionGrid.SetActive(true);
            int count = session.Data.Collection.Count;
            gridStatus.text = count == 0 ? "보유한 그림이 없어요. 정산에서 보유해 보세요." : count+"점 · "+(collectionPage+1)+" / "+((count+5)/6)+"페이지";
            for (int i=0;i<6;i++)
            {
                if (thumbnails[i] != null) { Destroy(thumbnails[i]); thumbnails[i] = null; }
                int index = collectionPage*6+i; cards[i].gameObject.SetActive(index < count);
                if (index >= count) continue;
                var artwork = session.Data.Collection[index];
                cards[i].GetComponentInChildren<TMP_Text>().text = artwork.Title;
                var texture = new Texture2D(32,32,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point };
                var pixels = new Color32[1024]; for (int p=0;p<pixels.Length;p++) pixels[p] = artwork.Pixels[p]==0 ? new Color32(235,229,217,255) : DrawingSurface.ResolveColor(artwork.Pixels[p],palette,artwork.CustomColors);
                texture.SetPixels32(pixels); texture.Apply(); thumbnails[i] = texture; cardImages[i].texture = texture;
            }
        }
    }
}
