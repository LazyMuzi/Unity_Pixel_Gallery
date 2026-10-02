using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelGallery
{
    public sealed partial class GalleryPrototype
    {
        public const float ExhibitionSeconds = 60;
        public static string SavePath => Path.Combine(Application.persistentDataPath, "pixel-gallery.json");
        public SaveData Saved => session == null ? null : session.Data;
        GallerySession session;
        GameObject gallery, hangConfirmation, saleConfirmation, collection, saveError, galleryPlacement;
        TMP_Text wallet, hangText, settlement, galleryStatus, bubble, collectionText, errorText, saleText;
        RawImage hangImage, collectionImage;
        GalleryRoom room;
        float elapsed, speed = 1;
        int collectionIndex;
        string saleId;
        bool running;
        int lastRegularStage = -1;
        float regularBubbleUntil;
        RectTransform museumViewport;

        void BuildExhibition()
        {
            var panel = Panel("Gallery", safeRoot, new Color32(23,21,22,255)); Stretch(panel); gallery = panel.gameObject;
            var viewport = Place("MuseumView",panel,.5f,1920,1080); Stretch(viewport); museumViewport = viewport;
            var header = Panel("GalleryHeader",panel,new Color(0,0,0,.55f));
            header.anchorMin = new Vector2(0,.84f); header.anchorMax = Vector2.one; header.offsetMin = header.offsetMax = Vector2.zero;
            var heading = Label(panel,"오늘의 전시",48,.945f,80); heading.color = new Color32(255,232,193,255);
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0,.945f);
            heading.rectTransform.sizeDelta = new Vector2(470,80); heading.rectTransform.anchoredPosition = new Vector2(270,0); heading.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            galleryStatus = Label(panel,"작품마다 다른 시선, 서로 다른 취향",32,.865f,50); galleryStatus.color = new Color32(221,202,178,255);
            galleryStatus.rectTransform.anchorMin = galleryStatus.rectTransform.anchorMax = new Vector2(0,.875f);
            galleryStatus.rectTransform.sizeDelta = new Vector2(650,50); galleryStatus.rectTransform.anchoredPosition = new Vector2(360,0); galleryStatus.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            var image = viewport.gameObject.AddComponent<RawImage>(); image.raycastTarget = false;
            var roomObject = new GameObject("GalleryRoom"); roomObject.transform.SetParent(transform,false);
            room = roomObject.AddComponent<GalleryRoom>(); room.Initialize(galleryLit,galleryUnlit); image.texture = room.View;
            roomObject.SetActive(false);
            bubble = Label(panel,"머리 위 표정으로 감상을 살펴보세요",28,.04f,45); bubble.color = new Color32(239,220,192,255);
            var fast = (RectTransform)Button(panel,"빠르게 / 보통",.94f,240,90,-380,() => speed = speed == 1 ? 2 : 1).transform;
            fast.anchorMin = fast.anchorMax = new Vector2(1,.94f);
            fast.GetComponentInChildren<TMPro.TMP_Text>().fontSize = 28;
            var skip = (RectTransform)Button(panel,"건너뛰기",.94f,200,90,-140,SkipExhibition).transform;
            skip.anchorMin = skip.anchorMax = new Vector2(1,.94f);

            var hang = Panel("ConfirmHang", safeRoot, new Color32(248,243,232,255)); Stretch(hang); hangConfirmation = hang.gameObject;
            Label(hang, "전시 확인", 64, .9f, 110);
            var titleRect = (RectTransform)title.transform; titleRect.SetParent(hang,false);
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(.5f,.76f);
            titleRect.anchoredPosition = new Vector2(350,0); titleRect.sizeDelta = new Vector2(700,80);
            title.onEndEdit.AddListener(_ => hangText.text = "이 그림을 전시할까요?\n"+title.text);
            var thumb = Place("Preview", hang, .53f, 450, 450, -470); hangImage = thumb.gameObject.AddComponent<RawImage>();
            hangText = Label(hang, "", 38, .53f, 260); hangText.rectTransform.sizeDelta = new Vector2(700,260); hangText.rectTransform.anchoredPosition = new Vector2(350,0);
            Button(hang, "돌아가기", .12f, 440, 90, -235, () => hangConfirmation.SetActive(false));
            Button(hang, "전시 시작", .12f, 440, 90, 235, StartExhibition);

            var list = Panel("Collection", safeRoot, new Color32(248,243,232,255)); Stretch(list); collection = list.gameObject;
            Label(list, "컬렉션", 64, .94f, 110);
            var stored = Place("StoredArtwork", list, .53f, 450, 450, -470); collectionImage = stored.gameObject.AddComponent<RawImage>();
            collectionText = Label(list, "", 36, .55f, 280); collectionText.rectTransform.sizeDelta = new Vector2(700,280); collectionText.rectTransform.anchoredPosition = new Vector2(350,0);
            Button(list, "이전", .3f, 300, 90, 180, () => { collectionIndex--; RefreshCollection(); });
            Button(list, "다음", .3f, 300, 90, 520, () => { collectionIndex++; RefreshCollection(); });
            Button(list, "다시 전시", .16f, 260, 90, -435, Rehang);
            Button(list, "작업실에 걸기", .16f, 260, 90, -145, BeginStudioPlacement);
            Button(list, "그림 팔기", .16f, 260, 90, 435, () => { var a = SelectedArtwork(); if (a != null) AskSale(a.Id); });
            Button(list,"전시장에 걸기",.16f,260,90,145,BeginGalleryPlacement);
            Button(list, "목록으로", .05f, 600, 90, 0, ShowCollectionGrid);

            var placement = Panel("GalleryPlacement",safeRoot,new Color(0,0,0,.78f)); Stretch(placement); galleryPlacement = placement.gameObject;
            var placementCard = Place("GalleryPlacementCard",placement,.5f,1100,650); placementCard.gameObject.AddComponent<Image>().color = new Color32(248,243,232,255);
            Label(placementCard,"소장 작품을 걸 벽을 고르세요",44,.8f,100);
            Label(placementCard,"관람객들이 다음 전시에서 함께 감상해요.\n가운데에는 이번 전시 작품이 걸립니다.",32,.59f,150);
            string[] wallNames = { "왼쪽 벽", "오른쪽 벽", "옆 벽" };
            for (int i=0;i<3;i++)
            {
                int slot = i; Button(placementCard,wallNames[i],.34f,300,90,(i-1)*330,() => {
                    var art = SelectedArtwork(); if (art == null) return;
                    if (TrySave(() => session.PlaceInGallery(art.Id,slot))) { galleryPlacement.SetActive(false); RefreshCollection(); }
                });
            }
            Button(placementCard,"배치 닫기",.12f,500,80,0,() => galleryPlacement.SetActive(false)); galleryPlacement.SetActive(false);

            var sale = Panel("ConfirmSale", safeRoot, new Color(0,0,0,.85f)); Stretch(sale); saleConfirmation = sale.gameObject;
            var saleCard = Place("SaleCard", sale, .5f, 960, 720);
            saleCard.gameObject.AddComponent<Image>().color = new Color32(248,243,232,255);
            saleText = Label(saleCard, "", 44, .65f, 240);
            Button(saleCard, "취소", .3f, 300, 90, -180, () => saleConfirmation.SetActive(false));
            Button(saleCard, "판매 확정", .3f, 300, 90, 180, SellArtwork);
            var error = Panel("SaveError", safeRoot, new Color(0,0,0,.9f)); Stretch(error); saveError = error.gameObject;
            errorText = Label(error, "", 40, .57f, 360); errorText.color = Color.white;
            Button(error, "닫기", .34f, 500, 128, 0, () => { if (session != null) saveError.SetActive(false); });
            gallery.SetActive(false); hangConfirmation.SetActive(false); saleConfirmation.SetActive(false); collection.SetActive(false); saveError.SetActive(false);
        }
        void LoadSession()
        {
            TrySave(() => {
                var store = new SaveStore(SavePath);
                session = new GallerySession(store.Load(), store.Write);
                RestoreDraft();
                if (session.Data.Pending == null) ShowStudioHub();
                if (session.Data.Pending != null)
                {
                    var artwork = session.Data.Collection.Find(a => a.Id == session.Data.Pending.ArtworkId);
                    BeginExhibition(artwork); running = false;
                    if (TrySave(session.Finish)) ShowResults();
                }
                if (store.RecoveredFromBackup) ShowError("이전 정상 저장에서 복원했습니다.");
            });
        }
        bool TrySave(Action action)
        {
            try { action(); return true; }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException || error is OverflowException)
            { ShowError("저장을 완료하지 못했습니다.\n기존 저장은 보존했습니다.\n" + error.Message); return false; }
        }
        void ShowError(string message) { errorText.text = message; saveError.SetActive(true); saveError.transform.SetAsLastSibling(); }
        bool SaveDraft()
        {
            if (session == null || session.Data.Pending != null) return false;
            if (string.IsNullOrEmpty(artworkId)) artworkId = Guid.NewGuid().ToString("N");
            return TrySave(() => session.SaveDraft(drawing.Pixels, title.text, artworkId,draftColors.ToArray()));
        }
        void DraftChanged() { judgment = null; artworkId = null; UpdateStatus(); SaveDraft(); }
        void RestoreDraft()
        {
            draftColors.Clear(); draftColors.AddRange(session.Data.DraftColors);
            for (int i=0;i<draftColors.Count;i++) palette[33+i] = DrawingSurface.ResolveColor((byte)(33+i),palette,session.Data.DraftColors);
            surface.SelectedColor = 1;
            drawing.Load(session.Data.DraftPixels); artworkId = session.Data.DraftId;
            title.SetTextWithoutNotify(session.Data.DraftTitle); surface.Refresh(); UpdateStatus();
            wallet.text = $"{session.Data.Day}일차 · 코인 {session.Data.Coins} · {ThemeName}";
        }
        void MakePreview(byte[] source,int[] custom)
        {
            if (preview != null) Destroy(preview);
            preview = new Texture2D(32,32,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point };
            var pixels = new Color32[1024];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = source[i] == 0 ? new Color32(235,229,217,255) : DrawingSurface.ResolveColor(source[i],palette,custom);
            preview.SetPixels32(pixels); preview.Apply();
        }
        ShowRecord CreateShow(Artwork artwork)
        {
            artwork = artwork.Copy(); artwork.Features = Analyze(artwork.Pixels,artwork.CustomColors);
            analyzedFeatures = artwork.Features;
            var tastes = new NpcTaste[visitors.Length * 2];
            for (int i = 0; i < tastes.Length; i++)
            {
                var original = visitors[i % visitors.Length].Taste;
                tastes[i] = new NpcTaste { Id = original.Id + "#" + (i / visitors.Length), Name = (i < visitors.Length && original.Id == "warm" && session.Data.RegularVisits > 0 ? "단골 · " : "")+original.Name,
                    Targets = original.Targets, Weights = original.Weights, Tolerance = original.Tolerance,
                    Baseline = original.Baseline, Slope = original.Slope };
            }
            var verdict = Judge.Evaluate(artwork.Features, tastes, artwork.Id, session.Data.Day,FreshnessPenalty(artwork.Features));
            var comments = new string[tastes.Length];
            for (int i = 0; i < comments.Length; i++) comments[i] = (verdict.Visitors[i].Id == "warm#0" ? session.RegularComment(artwork.Id,artwork.Features)+"\n" : "")+Comment(verdict.Visitors[i]);
            int price = Economy.Price(verdict, Economy.Rarity(artwork.Features, session.Data.Recent));
            return new ShowRecord { Id = Guid.NewGuid().ToString("N"), ArtworkId = artwork.Id, Day = session.Data.Day,
                Theme = ThemeName, ShownAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Judgment = verdict, Comments = comments, Price = price,
                Offer = Economy.Offer(verdict,price,artwork.Id,session.Data.Day) };
        }
        public void StartExhibition()
        {
            if (session == null || session.Data.Pending != null || !hangConfirmation.activeSelf || !drawing.CanExhibit) return;
            var artwork = new Artwork { Id = artworkId, Title = string.IsNullOrWhiteSpace(title.text) ? "무제" : title.text,
                Pixels = (byte[])drawing.Pixels.Clone(), CustomColors = draftColors.ToArray(), Features = Analyze(drawing.Pixels),
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            if (!TrySave(() => session.Start(artwork,CreateShow(artwork),true))) return;
            BeginExhibition(artwork);
        }
        void BeginExhibition(Artwork artwork)
        {
            judgment = session.Data.Pending.Judgment;
            MakePreview(artwork.Pixels,artwork.CustomColors); room.Begin(session.Data.Pending,preview,museumViewport.rect.width/museumViewport.rect.height,session.Data.GalleryExpanded);
            room.RefreshGalleryArt(session.Data,palette);
            galleryStatus.text = session.Data.GalleryExpanded ? "확장된 미술관 · 작품 가치 +20%" : "작품마다 다른 시선, 서로 다른 취향";
            studioHub.SetActive(false); studioBackdrop.SetActive(false); enteringDrawing = false;
            collectionGrid.SetActive(false);
            hangConfirmation.SetActive(false); collection.SetActive(false); atelier.SetActive(false); results.SetActive(false);
            gallery.SetActive(true); elapsed = 0; speed = 1; running = true; lastRegularStage = -1; regularBubbleUntil = 0;
            bubble.text = "머리 위 표정으로 감상을 살펴보세요";
            TickExhibition();
        }
        void TickExhibition()
        {
            if (!running) return;
            elapsed = Mathf.Min(ExhibitionSeconds, elapsed + Time.deltaTime * speed);
            room.Advance(Time.deltaTime * speed);
            int stage = room.RegularVisitStage;
            if (stage != lastRegularStage && stage > 0)
            {
                lastRegularStage = stage;
                int index = System.Array.FindIndex(judgment.Visitors,v => v.Id == "warm#0");
                bubble.text = stage == 1 ? "금색 모자의 손님이 그림을 살펴보고 있어요" : judgment.Visitors[index].Name+" · "+session.Data.Pending.Comments[index].Split('\n')[0];
                regularBubbleUntil = elapsed+6;
            }
            if (regularBubbleUntil > 0 && elapsed > regularBubbleUntil) { bubble.text = "머리 위 표정으로 감상을 살펴보세요"; regularBubbleUntil = 0; }
            if (elapsed >= ExhibitionSeconds) SkipExhibition();
        }
        public void SkipExhibition()
        {
            if (session == null || session.Data.Pending == null) return;
            running = false; room.Complete();
            if (TrySave(session.Finish)) ShowResults();
        }
        void ShowResults()
        {
            enteringDrawing = false; studioHub.SetActive(false); studioBackdrop.SetActive(false); room.gameObject.SetActive(false);
            var show = session.Data.Pending; judgment = show.Judgment;
            var artwork = session.Data.Collection.Find(a => a.Id == show.ArtworkId);
            MakePreview(artwork.Pixels,artwork.CustomColors); previewImage.texture = preview;
            var text = new StringBuilder("모든 취향을 맞출 필요는 없어요.\n마음에 드는 감상 하나를 다음 그림에 담아보세요.\n\n"); int likes = 0, fans = 0;
            for (int i = 0; i < judgment.Visitors.Length; i++)
            {
                var v = judgment.Visitors[i]; if (v.Liked) likes++; if (v.Reaction == Reaction.Fan) fans++;
                text.Append(v.Name).Append(" · ").AppendLine(reactions[(int)v.Reaction]).AppendLine(show.Comments[i]);
                text.AppendLine();
            }
            var content = (RectTransform)resultText.transform.parent;
            content.anchoredPosition = Vector2.zero;
            resultTitle.text = artwork.Title+" · "+show.Day+"일차";
            resultStats.text = $"좋아요 {likes}명 · 열성팬 {fans}명\n작품 가치 {show.Price}코인";
            resultOffer.text = judgment.Visitors[judgment.BuyerIndex].Name+"의 구매 제안\n"+show.Offer+"코인";
            resultReward.text = show.CommissionReward > 0 ? $"부탁 완료 +{show.CommissionReward}코인 · 판매하거나 보유하면 지급" : session.Data.CommissionAccepted ? "단골의 부탁은 다음 새 그림으로 이어가요" : "보유하면 작업실에 걸어둘 수 있어요";
            resultGrowth.text = show.GrowthBonus ? "확장된 미술관 · 작품 가치 +20% 적용" : show.Day >= 4 ? "확장 보너스는 다음 전시부터 적용돼요" : show.Day == 3 ? "정산하면 미술관 확장 · 다음 전시부터 작품 가치 +20%" : "미술관 확장까지 전시 "+(3-show.Day)+"회";
            int first = Array.FindIndex(judgment.Visitors,v => v.Id == "warm#0" && v.Liked);
            if (first < 0) first = judgment.BuyerIndex;
            int second = Array.FindIndex(judgment.Visitors,v => !v.Liked);
            if (second < 0) second = judgment.BuyerIndex == first ? (first+1)%judgment.Visitors.Length : judgment.BuyerIndex;
            for (int i=0;i<featuredComments.Length;i++)
            {
                int index = i == 0 ? first : second;
                var visitor = judgment.Visitors[index];
                featuredComments[i].text = visitor.Name+" · "+reactions[(int)visitor.Reaction]+"\n"+show.Comments[index];
            }
            resultText.text = text.ToString();
            content.sizeDelta = new Vector2(-40,resultText.GetPreferredValues(resultText.text,1060,0).y+30);
            allComments.SetActive(false);
            gallery.SetActive(false); atelier.SetActive(false); collection.SetActive(false); results.SetActive(true);
        }
        void AskSale(string id)
        {
            if (session == null) return;
            var show = id == null ? session.Data.Pending : session.Data.Collection.Find(a => a.Id == id)?.LastShow;
            if (show == null || !show.Finished) return;
            saleId = id; saleText.text = $"{show.Offer}코인에 판매할까요?\n그림은 컬렉션에서 사라집니다.\n판매는 되돌릴 수 없어요.";
            if (id == null && show.CommissionReward > 0) saleText.text += $"\n부탁 보상 +{show.CommissionReward} · 총 {show.Offer+show.CommissionReward}코인";
            saleConfirmation.SetActive(true); saleConfirmation.transform.SetAsLastSibling();
        }
        public void SellArtwork()
        {
            if (!saleConfirmation.activeSelf) return;
            var show = saleId == null ? session.Data.Pending : session.Data.Collection.Find(a => a.Id == saleId)?.LastShow;
            if (show == null) return;
            int coins = show.Offer+(saleId == null ? show.CommissionReward : 0);
            int previousCoins = session.Data.Coins; bool wasExpanded = session.Data.GalleryExpanded;
            if (!TrySave(() => { if (saleId == null) session.Resolve(true); else session.SellStored(saleId); })) return;
            saleConfirmation.SetActive(false); ReturnToAtelier(); status.text = $"판매 완료\n+{coins}코인";
            ShowSettlementReward(previousCoins,!wasExpanded && session.Data.GalleryExpanded,true);
        }
        public void KeepArtwork()
        {
            if (session == null || session.Data.Pending == null || !session.Data.Pending.Finished) return;
            int previousCoins = session.Data.Coins; bool wasExpanded = session.Data.GalleryExpanded;
            if (!TrySave(() => session.Resolve(false))) return;
            ReturnToAtelier(); ShowSettlementReward(previousCoins,!wasExpanded && session.Data.GalleryExpanded,false);
        }
        void ReturnToAtelier()
        {
            running = false; room.gameObject.SetActive(false); gallery.SetActive(false); results.SetActive(false); collection.SetActive(false); shop.SetActive(false); toolPanel.SetActive(false); npcBook.SetActive(false); collectionGrid.SetActive(false); regularRequest.SetActive(false); palettePicker.SetActive(false); galleryPlacement.SetActive(false);
            RestoreDraft(); ShowStudioHub();
        }
        void OpenCollection()
        {
            if (session == null || session.Data.Pending != null || !SaveDraft()) return;
            collectionPage = 0; ShowCollectionGrid();
        }
        Artwork SelectedArtwork()
        {
            int count = session.Data.Collection.Count;
            if (count == 0) return null;
            collectionIndex = (collectionIndex % count + count) % count;
            return session.Data.Collection[collectionIndex];
        }
        void RefreshCollection()
        {
            var a = SelectedArtwork(); collectionImage.gameObject.SetActive(a != null);
            if (a == null) { collectionText.text = "보유한 그림이 없어요.\n정산에서 보유하기를 선택해 보세요."; return; }
            MakePreview(a.Pixels,a.CustomColors); collectionImage.texture = preview;
            collectionText.text = $"{collectionIndex+1}/{session.Data.Collection.Count} · {a.Title}\n전시 {a.TimesShown}회 · 제안 {a.LastShow.Offer}코인";
            int slot = Array.IndexOf(session.Data.GalleryWallIds,a.Id);
            if (slot >= 0) collectionText.text += "\n전시장 배치: "+new[] { "왼쪽 벽", "오른쪽 벽", "옆 벽" }[slot]+" · 관람 수입 "+(session.Data.GalleryLighting ? 2 : 1)+"코인 / 20초";
            else if (session.Data.GalleryExpanded) collectionText.text += "\n전시장에 걸면 관람 수입이 쌓여요";
        }
        void BeginGalleryPlacement()
        {
            if (!session.Data.GalleryExpanded) { ShowError("전시 3회 정산 후 미술관이 확장되면 소장 작품을 걸 수 있어요."); return; }
            if (SelectedArtwork() == null) return;
            galleryPlacement.SetActive(true); galleryPlacement.transform.SetAsLastSibling();
        }
        void Rehang()
        {
            var artwork = SelectedArtwork(); if (artwork == null) return;
            artwork = artwork.Copy(); artwork.Features = Analyze(artwork.Pixels,artwork.CustomColors);
            if (TrySave(() => session.Start(artwork,CreateShow(artwork),false))) BeginExhibition(artwork);
        }
        void OnApplicationPause(bool paused) { if (paused && session != null && session.Data.Pending == null) { surface.EndStroke(); SaveDraft(); } }
        void OnApplicationQuit() { if (session != null && session.Data.Pending == null) { surface.EndStroke(); SaveDraft(); } }
    }
}
