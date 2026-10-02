using UnityEngine;
using UnityEngine.UI;

namespace PixelGallery
{
    public sealed partial class GalleryPrototype
    {
        GameObject studioHub, studioBackdrop;
        RectTransform canvasHit;
        GameObject studioPlacement;
        readonly RectTransform[] studioSlotButtons = new RectTransform[3];
        string placingArtworkId;
        GameObject regularRequest;
        TMPro.TMP_Text requestText;
        Button requestAction;
        TMPro.TMP_Text growthStatus;
        bool enteringDrawing;
        Vector2 lastCanvasSize;
        GameObject rewardNotice;
        CanvasGroup rewardFade;
        TMPro.TMP_Text rewardCoins;
        float rewardTime;
        int rewardFrom, rewardTo, displayedRewardCoins;
        float galleryIncomeTime;
        int lastGalleryIncome;
        public bool IsDrawingStage => atelier.activeSelf;

        void BuildStudioUI()
        {
            var backdrop = Place("StudioView",safeRoot.parent,.5f,1080,1920); Stretch(backdrop); backdrop.SetSiblingIndex(1);
            backdrop.gameObject.AddComponent<RawImage>().texture = room.View; backdrop.GetComponent<RawImage>().raycastTarget = false;
            studioBackdrop = backdrop.gameObject;
            var hub = Panel("StudioHub",safeRoot,Color.clear); Stretch(hub); studioHub = hub.gameObject;
            var notice = Panel("SettlementNotice",hub,new Color32(43,43,36,245));
            notice.anchorMin = notice.anchorMax = new Vector2(.5f,.75f); notice.sizeDelta = new Vector2(1000,220);
            rewardNotice = notice.gameObject; rewardFade = rewardNotice.AddComponent<CanvasGroup>(); rewardFade.blocksRaycasts = false;
            rewardCoins = ResultLabel(notice,"",38,.68f,75,940);
            ResultLabel(notice,"",28,.27f,90,940).name = "RewardDetail";
            rewardNotice.SetActive(false);
            var heading = Label(hub,"나의 작업실",64,.94f,110); heading.color = new Color32(255,237,206,255);
            heading.rectTransform.sizeDelta = new Vector2(440,110); heading.rectTransform.anchoredPosition = new Vector2(-720,0);
            growthStatus = Label(hub,"",34,.2f,120); growthStatus.color = Color.white;
            canvasHit = (RectTransform)Button(hub,"캔버스",.5f,400,500,0,EnterDrawing).transform;
            canvasHit.GetComponent<Image>().color = Color.clear; canvasHit.GetComponentInChildren<TMPro.TMP_Text>().text = "";
            Button(hub,"상점",.075f,440,90,-470,OpenShop);
            Button(hub,"컬렉션",.075f,440,90,0,OpenCollection);
            Button(hub,"단골의 부탁",.075f,440,90,470,OpenRegularRequest);
            var request = Panel("RegularRequest",safeRoot,new Color(0,0,0,.78f)); Stretch(request); regularRequest = request.gameObject;
            var requestCard = Place("RequestCard",request,.5f,1080,760); requestCard.gameObject.AddComponent<Image>().color = new Color32(248,243,232,255);
            Label(requestCard,"단골의 부탁",56,.85f,100); requestText = Label(requestCard,"",34,.57f,350);
            requestAction = Button(requestCard,"부탁 수락하기",.18f,440,90,-235,ToggleRegularRequest).GetComponent<Button>();
            Button(requestCard,"나중에",.18f,440,90,235,() => regularRequest.SetActive(false));
            regularRequest.SetActive(false);
            var placement = Panel("StudioPlacement",safeRoot,Color.clear); Stretch(placement); studioPlacement = placement.gameObject;
            Label(placement,"그림을 걸 액자를 누르세요",44,.2f,100).color = Color.white;
            for (int i=0;i<3;i++)
            {
                int slot = i;
                studioSlotButtons[i] = (RectTransform)Button(placement,"액자 "+(i+1),.5f,180,80,0,() => PlaceStudioArtwork(slot)).transform;
            }
            Button(placement,"배치 취소",.075f,300,90,0,() => { studioPlacement.SetActive(false); studioHub.SetActive(true); });
            studioPlacement.SetActive(false);
            var dim = Panel("ConfirmDrawingDim",atelier.transform,new Color(0,0,0,.72f)); Stretch(dim); dim.SetAsFirstSibling();
            foreach (var text in atelier.GetComponentsInChildren<TMPro.TMP_Text>())
                if (text.transform.parent == atelier.transform) text.color = new Color32(255,244,221,255);
            var back = Button(atelier.transform,"작업실로",.945f,200,80,0,ExitDrawing);
            var backRect = (RectTransform)back.transform; backRect.anchorMin = backRect.anchorMax = new Vector2(0,.945f);
            backRect.anchoredPosition = new Vector2(130,0); StyleDrawingButton(back);
            surface.Stroked = room.PaintGesture; room.ShowStudio(surface.Texture,uiCanvas.pixelRect.width/uiCanvas.pixelRect.height);
            atelier.SetActive(false);
        }
        void ShowSettlementReward(int previousCoins, bool expanded, bool sold)
        {
            rewardFrom = previousCoins; rewardTo = session.Data.Coins; displayedRewardCoins = -1; rewardTime = 0;
            var detail = rewardNotice.transform.Find("RewardDetail").GetComponent<TMPro.TMP_Text>();
            detail.text = (sold ? "판매 완료" : "컬렉션에 보관했어요")+(expanded ? "\n미술관 확장! 다음 전시부터 작품 가치 +20%" : "\n새로운 그림을 기다리고 있어요");
            rewardFade.alpha = 1; rewardNotice.SetActive(true); rewardNotice.transform.SetAsLastSibling();
        }
        void OpenRegularRequest()
        {
            regularRequest.SetActive(true); regularRequest.transform.SetAsLastSibling();
            bool available = session.Data.RegularVisits > 0 && !session.Data.CommissionCompleted;
            requestAction.interactable = available;
            string caption = session.Data.CommissionCompleted ? "부탁 완료" : session.Data.CommissionAccepted ? "부탁 내려놓기" : "부탁 수락하기";
            requestAction.name = caption; requestAction.GetComponentInChildren<TMPro.TMP_Text>().text = caption;
            requestText.text = session.Data.RegularVisits == 0 ? "첫 전시를 열면 따뜻한 색을 좋아하는\n관람객을 만날 수 있어요.\n그 뒤 작은 부탁이 도착합니다." :
                session.Data.CommissionCompleted ? "따뜻한 그림, 고마워요.\n다음 전시에도 그림을 보러 들를게요.\n받은 보상: 물감 한 색 값인 60코인" :
                "따뜻한 색을 좋아하는 단골의 편지\n\n‘햇살이 드는 방에 걸고 싶은 그림을 부탁해요.’\n빨강·주황·노랑을 주로, 밝은 물감으로 그려주세요.\n무엇을 그릴지는 자유예요. 기한도 없어요.\n새 그림으로 전시 · 판매하거나 보유하면 보상 60코인";
            if (session.Data.CommissionAccepted) requestText.text += "\n수락한 부탁 · 다른 그림을 그려도 괜찮아요";
        }
        void ToggleRegularRequest()
        {
            if (!TrySave(() => session.SetCommission(!session.Data.CommissionAccepted))) return;
            regularRequest.SetActive(false);
        }
        public void EnterDrawing()
        {
            if (session == null || session.Data.Pending != null || enteringDrawing || atelier.activeSelf) return;
            rewardNotice.SetActive(false);
            surface.Zoom(.25f);
            ((RectTransform)surface.transform).anchoredPosition = Vector2.zero;
            atelier.SetActive(true); Canvas.ForceUpdateCanvases();
            FitDrawingCamera(false);
            atelier.SetActive(false);
            enteringDrawing = true; studioHub.SetActive(false);
        }
        void FitDrawingCamera(bool snap)
        {
            var pixels = uiCanvas.pixelRect; lastCanvasSize = pixels.size;
            var corners = new Vector3[4]; ((RectTransform)surface.transform.parent).GetWorldCorners(corners);
            var viewport = new Rect((corners[0].x-pixels.x)/pixels.width,(corners[0].y-pixels.y)/pixels.height,
                (corners[2].x-corners[0].x)/pixels.width,(corners[2].y-corners[0].y)/pixels.height);
            room.FocusCanvas(viewport,pixels.width/pixels.height,snap);
        }

        public void ExitDrawing()
        {
            surface.EndStroke(); if (!SaveDraft()) return;
            ShowStudioHub();
        }
        void ShowStudioHub()
        {
            UpdateStudioGrowth();
            enteringDrawing = false; toolPanel.SetActive(false); palettePicker.SetActive(false); atelier.SetActive(false); studioHub.SetActive(true); studioBackdrop.SetActive(true);
            room.ShowStudio(surface.Texture,uiCanvas.pixelRect.width/uiCanvas.pixelRect.height);
            room.RefreshStudioArt(session.Data,palette);
        }
        void UpdateStudioGrowth()
        {
            growthStatus.text = "캔버스를 눌러 그림을 시작하세요\n"+(session.Data.GalleryExpanded ? "작품 가치 +20% · 관람 수입 "+session.GalleryIncome+"코인 / 20초 · 보유 "+session.Data.Coins+"코인" : "미술관 확장까지 "+System.Math.Min(session.Data.Day-1,3)+" / 3회 전시 완료");
        }
        void TickGalleryIncome()
        {
            if (session == null) return;
            int income = session.GalleryIncome;
            if (income != lastGalleryIncome) { galleryIncomeTime = 0; lastGalleryIncome = income; }
            if (income == 0 || saveError.activeSelf || !Application.isFocused) return;
            galleryIncomeTime += Time.deltaTime;
            if (galleryIncomeTime < 20) return;
            if (!TrySave(() => session.EarnGalleryIncome())) return;
            galleryIncomeTime = 0; UpdateStudioGrowth();
            if (shop.activeSelf) RefreshShop();
        }
        void BeginStudioPlacement()
        {
            var artwork = SelectedArtwork(); if (artwork == null) return;
            placingArtworkId = artwork.Id; collection.SetActive(false); ShowStudioHub();
            studioHub.SetActive(false); studioPlacement.SetActive(true);
        }
        void PlaceStudioArtwork(int slot)
        {
            if (!TrySave(() => session.PlaceInStudio(placingArtworkId,slot))) return;
            studioPlacement.SetActive(false); ShowStudioHub();
        }
        void TickStudio()
        {
            TickGalleryIncome();
            if (rewardNotice.activeSelf)
            {
                rewardTime += Time.unscaledDeltaTime;
                int coins = Mathf.RoundToInt(Mathf.Lerp(rewardFrom,rewardTo,Mathf.Clamp01(rewardTime/.6f)));
                if (coins != displayedRewardCoins)
                {
                    displayedRewardCoins = coins;
                    rewardCoins.text = rewardTo == rewardFrom ? "작품을 소장했어요" : $"+{rewardTo-rewardFrom}코인 · 보유 {coins}코인";
                }
                rewardFade.alpha = Mathf.Clamp01((3-rewardTime)/.3f);
                if (rewardTime >= 3) rewardNotice.SetActive(false);
            }

            if (atelier.activeSelf && lastCanvasSize != uiCanvas.pixelRect.size)
            {
                Canvas.ForceUpdateCanvases(); FitDrawingCamera(true);
            }
            if (enteringDrawing && room.CanvasReady) { enteringDrawing = false; atelier.SetActive(true); }
            if (studioPlacement.activeSelf)
                for (int i=0;i<3;i++)
                {
                    Vector2 viewport = room.StudioSlotViewport(i); var pixels = uiCanvas.pixelRect;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(safeRoot,pixels.position+Vector2.Scale(viewport,pixels.size),null,out var point);
                    studioSlotButtons[i].anchorMin = studioSlotButtons[i].anchorMax = Vector2.one*.5f;
                    studioSlotButtons[i].anchoredPosition = point;
                }
            if (studioHub.activeSelf)
            {
                Vector2 position = room.CanvasViewport;
                canvasHit.anchorMin = canvasHit.anchorMax = position; canvasHit.anchoredPosition = Vector2.zero;
            }
        }
    }
}
