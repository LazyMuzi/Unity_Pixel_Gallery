using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PixelGallery.Tests
{
    public class PrototypePlayTests
    {
        string backupDirectory;
        [UnitySetUp]
        public IEnumerator PreservePlayerSave()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            yield return null;
            backupDirectory = Path.Combine(Path.GetTempPath(), "pixel-gallery-play-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backupDirectory);
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(GalleryPrototype.SavePath + suffix)) File.Move(GalleryPrototype.SavePath + suffix, Path.Combine(backupDirectory,"save" + suffix));
        }
        [UnityTearDown]
        public IEnumerator RestorePlayerSave()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            yield return null;
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                if (File.Exists(GalleryPrototype.SavePath + suffix)) File.Delete(GalleryPrototype.SavePath + suffix);
                string original = Path.Combine(backupDirectory,"save" + suffix);
                if (File.Exists(original)) File.Move(original,GalleryPrototype.SavePath + suffix);
            }
            Directory.Delete(backupDirectory);
        }
        [UnityTest]
        public IEnumerator DrawExhibitResumeAndSellThroughActualButtons()
        {
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null;
            yield return EnterCanvas();
            var game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            var surface = UnityEngine.Object.FindFirstObjectByType<DrawingSurface>();
            Assert.That(game, Is.Not.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            game.Evaluate(); Assert.That(game.LastJudgment, Is.Null);
            var rect = (RectTransform)surface.transform;
            var e = new PointerEventData(EventSystem.current) { pointerId = -1, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            e.position = RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(Vector3.zero));
            EventSystem.current.RaycastAll(e,hits);
            Assert.That(hits.Exists(hit => hit.gameObject == surface.gameObject), Is.True);
            e.position = Point(rect,0,8); surface.OnPointerDown(e);
            e.position = Point(rect,29,8); surface.OnDrag(e); surface.OnPointerUp(e);
            Assert.That(game.Drawing.CanExhibit,Is.False);
            e.position = Point(rect,30,8); surface.OnPointerDown(e); surface.OnPointerUp(e);
            Assert.That(game.Drawing.CanExhibit,Is.True);
            FindButton("완성하고 전시하기").onClick.Invoke();
            FindButton("돌아가기").onClick.Invoke();
            FindButton("되돌리기").onClick.Invoke(); Assert.That(game.Drawing.CanExhibit,Is.False);
            e.position = Point(rect,30,8); surface.OnPointerDown(e); surface.OnPointerUp(e);
            FindButton("완성하고 전시하기").onClick.Invoke();
            FindButton("전시 시작").onClick.Invoke();
            Assert.That(game.LastJudgment.Visitors.Length, Is.EqualTo(10));
            Assert.That(game.Saved.Pending.Finished,Is.False);
            var fixedResults = game.Saved.Pending.Judgment;
            var fixedComments = game.Saved.Pending.Comments;
            int offer = game.Saved.Pending.Offer;
            FindButton("빠르게 / 보통").onClick.Invoke(); yield return null;
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null;
            game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            Assert.That(game.Saved.Pending.Finished,Is.True);
            Assert.That(game.LastJudgment.BuyerIndex,Is.EqualTo(fixedResults.BuyerIndex));
            Assert.That(game.Saved.Pending.Comments,Is.EqualTo(fixedComments));
            for (int i=0;i<fixedResults.Visitors.Length;i++)
            {
                var actual = game.LastJudgment.Visitors[i]; var expected = fixedResults.Visitors[i];
                Assert.That(actual.Id,Is.EqualTo(expected.Id)); Assert.That(actual.Reaction,Is.EqualTo(expected.Reaction));
                Assert.That(actual.Guaranteed,Is.EqualTo(expected.Guaranteed));
                Assert.That(actual.Score,Is.EqualTo(expected.Score).Within(1e-12));
                Assert.That(actual.Probability,Is.EqualTo(expected.Probability).Within(1e-12));
                Assert.That(actual.Confidence,Is.EqualTo(expected.Confidence).Within(1e-12));
            }
            Assert.That(game.Saved.Pending.Offer,Is.EqualTo(offer));
            var resultField = typeof(GalleryPrototype).GetField("resultText",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var resultText = (TMPro.TMP_Text)resultField.GetValue(game);
            Assert.That(resultText.text,Does.Contain("마음에 드는 감상 하나"));
            Assert.That(resultText.text,Does.Not.Contain("확률").And.Not.Contain("신뢰도").And.Not.Contain("팬 보장"));
            var commentMethod = typeof(GalleryPrototype).GetMethod("Comment",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var featureField = typeof(GalleryPrototype).GetField("analyzedFeatures",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var originalFeatures = featureField.GetValue(game);
            var profiles = (NpcProfile[])typeof(GalleryPrototype).GetField("visitors",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(game);
            for (int feature=0;feature<10;feature++)
            {
                var values = new double[10]; values[feature] = profiles[0].Taste.Targets[feature]-.01;
                featureField.SetValue(game,values);
                var verdict = new NpcVerdict { Id = profiles[0].Taste.Id+"#0", WeakestFeature = feature };
                string more = (string)commentMethod.Invoke(game,new object[] { verdict });
                values[feature] += .02;
                string less = (string)commentMethod.Invoke(game,new object[] { verdict });
                Assert.That(more,Is.Not.EqualTo(less));
                Assert.That(more+less,Does.Not.Contain("높이면").And.Not.Contain("채움률").And.Not.Contain("복잡도"));
                verdict.Liked = true; verdict.Confidence = 0;
                Assert.That((string)commentMethod.Invoke(game,new object[] { verdict }),Does.Not.Contain("보고 싶어요"));
            }
            featureField.SetValue(game,originalFeatures);
            FindButton("판매하기").onClick.Invoke(); FindButton("취소").onClick.Invoke();
            Assert.That(game.Saved.Coins,Is.Zero);
            FindButton("판매하기").onClick.Invoke(); FindButton("판매 확정").onClick.Invoke();
            game.SellArtwork(); game.SkipExhibition();
            Assert.That(game.Saved.Coins,Is.EqualTo(offer)); Assert.That(game.Saved.Day,Is.EqualTo(2));
            Assert.That(game.Saved.Collection,Is.Empty);
            Assert.That(GameObject.Find("StudioHub"),Is.Not.Null);
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null;
            Assert.That(UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>().Saved.Coins,Is.EqualTo(offer));
        }
        [UnityTest]
        public IEnumerator KeepRehangSkipAndSellStoredArtwork()
        {
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null;
            var game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            for (int i=0;i<31;i++) game.Drawing.Paint(i,0,3);
            game.Evaluate(); FindButton("전시 시작").onClick.Invoke();
            FindButton("건너뛰기").onClick.Invoke(); FindButton("보유하기").onClick.Invoke();
            Assert.That(game.Saved.Collection.Count,Is.EqualTo(1)); Assert.That(game.Saved.Coins,Is.Zero);
            game.Drawing.Paint(4,4,7); FindButton("컬렉션").onClick.Invoke();
            FindButton("작품 1").onClick.Invoke(); FindButton("다시 전시").onClick.Invoke();
            Assert.That(game.Saved.Collection[0].TimesShown,Is.EqualTo(2));
            game.GetType().GetField("elapsed",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(game,GalleryPrototype.ExhibitionSeconds-.01f);
            yield return new WaitForSeconds(.05f);
            Assert.That(game.Saved.Pending.Finished,Is.True);
            FindButton("보유하기").onClick.Invoke();
            Assert.That(game.Drawing.Pixels[4*32+4],Is.EqualTo(7));
            int offer=game.Saved.Collection[0].LastShow.Offer;
            FindButton("컬렉션").onClick.Invoke(); FindButton("작품 1").onClick.Invoke(); FindButton("그림 팔기").onClick.Invoke();
            FindButton("판매 확정").onClick.Invoke();
            Assert.That(game.Saved.Coins,Is.EqualTo(offer)); Assert.That(game.Saved.Collection,Is.Empty);
            Assert.That(game.Drawing.Pixels[4*32+4],Is.EqualTo(7));
        }
        [UnityTest]
        public IEnumerator ShopUnlocksToolsAndFreePaletteKeepsDraft()
        {
            new SaveStore(GalleryPrototype.SavePath).Write(new SaveData { Coins = 1000, Day = 8 });
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null;
            var game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            game.Drawing.Paint(4,4,3); FindButton("상점").onClick.Invoke();
            Assert.That(GameObject.Find("물감 1"),Is.Null);
            Assert.That(game.Saved.Colors.Count,Is.EqualTo(32));
            FindButton("큰 붓 2×2 · 150코인").onClick.Invoke(); FindButton("채우기 · 200코인").onClick.Invoke();
            FindButton("좌우 대칭 · 300코인").onClick.Invoke();
            Assert.That(game.Saved.Coins,Is.EqualTo(350)); Assert.That(game.Saved.Tools,Is.EqualTo(7));
            FindButton("아틀리에로").onClick.Invoke(); Assert.That(game.Drawing.Pixels[4*32+4],Is.EqualTo(3));
            yield return EnterCanvas();
            FindButton("팔레트").onClick.Invoke();
            UnityEngine.Object.FindFirstObjectByType<ColorWheelPicker>().SetColor(new Color32(25,29,32,255)); FindButton("이 색 사용하기").onClick.Invoke();
            var surface = UnityEngine.Object.FindFirstObjectByType<DrawingSurface>();
            Assert.That(surface.SelectedColor,Is.EqualTo(32));
            Assert.That(GameObject.Find("PalettePicker"),Is.Null);
            FindButton("팔레트").onClick.Invoke();
            UnityEngine.Object.FindFirstObjectByType<ColorWheelPicker>().SetColor(new Color32(37,40,48,255)); FindButton("이 색 사용하기").onClick.Invoke();
            FindButton("도구").onClick.Invoke(); FindButton("큰 붓 선택").onClick.Invoke();
            FindButton("도구").onClick.Invoke(); FindButton("대칭 켜기 / 끄기").onClick.Invoke();
            Assert.That(surface.Tool,Is.EqualTo(3)); Assert.That(surface.Mirror,Is.True);
            var e = new PointerEventData(EventSystem.current) { pointerId = -1, button = PointerEventData.InputButton.Left };
            e.position = Point((RectTransform)surface.transform,0,0); surface.OnPointerDown(e); surface.OnPointerUp(e);
            Assert.That(game.Drawing.Pixels[0],Is.EqualTo(1)); Assert.That(game.Drawing.Pixels[31],Is.EqualTo(1));
            FindButton("도구").onClick.Invoke(); FindButton("채우기 선택").onClick.Invoke();
            e.position = Point((RectTransform)surface.transform,10,10); surface.OnPointerDown(e);
            Assert.That(game.Drawing.CanExhibit,Is.True);
            game.Evaluate(); FindButton("전시 시작").onClick.Invoke();
            var grownRoom = UnityEngine.Object.FindFirstObjectByType<GalleryRoom>();
            Assert.That(grownRoom.transform.Find("Floor").localScale.x,Is.EqualTo(17.5f));
            Assert.That(game.Saved.Pending.GrowthBonus,Is.True);
            for (int frame=0;frame<600;frame++) grownRoom.Advance(.1f);
            Assert.That(grownRoom.EvaluatedCount,Is.EqualTo(10));
            Assert.That(game.Saved.Pending.Theme,Is.EqualTo("따뜻한 색")); Assert.That(game.Saved.SeenNpcs.Count,Is.EqualTo(5));
            double firstScore = game.Saved.Pending.Judgment.Visitors[0].Score;
            game.SkipExhibition(); FindButton("보유하기").onClick.Invoke();
            FindButton("컬렉션").onClick.Invoke(); FindButton("작품 1").onClick.Invoke();
            FindButton("다시 전시").onClick.Invoke();
            Assert.That(game.Saved.Pending.Judgment.Visitors[0].Score,Is.EqualTo(firstScore-.15).Within(1e-12));
            Assert.That(game.Saved.Pending.Judgment.Visitors.Length,Is.EqualTo(10));
            game.SkipExhibition(); FindButton("보유하기").onClick.Invoke();
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null;
            game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            Assert.That(game.Saved.Coins,Is.EqualTo(350)); Assert.That(game.Saved.Tools,Is.EqualTo(7));
            Assert.That(game.Saved.SeenNpcs.Count,Is.EqualTo(5)); Assert.That(game.Saved.Colors.Contains(9),Is.True);
            FindButton("상점").onClick.Invoke(); FindButton("관람객 도감").onClick.Invoke();
            Assert.That(GameObject.Find("NpcBook"),Is.Not.Null);
        }
        [UnityTest]
        public IEnumerator ColorWheelArtworkSurvivesReloadAndGalleryPlacement()
        {
            new SaveStore(GalleryPrototype.SavePath).Write(new SaveData { Day = 4 });
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null; yield return EnterCanvas();
            var game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            var surface = UnityEngine.Object.FindFirstObjectByType<DrawingSurface>();
            FindButton("팔레트").onClick.Invoke();
            var wheel = UnityEngine.Object.FindFirstObjectByType<ColorWheelPicker>(); var wheelRect = (RectTransform)wheel.transform;
            var input = new PointerEventData(EventSystem.current) { pointerId = -1, button = PointerEventData.InputButton.Left };
            input.position = RectTransformUtility.WorldToScreenPoint(null,wheelRect.TransformPoint(new Vector3(wheelRect.rect.width*.435f,0,0)));
            wheel.OnPointerDown(input); wheel.OnPointerUp(input);
            input.position = RectTransformUtility.WorldToScreenPoint(null,wheelRect.TransformPoint(new Vector3(wheelRect.rect.width*.05f,wheelRect.rect.height*.1f,0)));
            wheel.OnPointerDown(input); wheel.OnPointerUp(input);
            Color32 firstColor = wheel.Selected; Assert.That(firstColor.r,Is.GreaterThan(firstColor.g));
            FindButton("이 색 사용하기").onClick.Invoke(); Assert.That(surface.SelectedColor,Is.EqualTo(33));
            for(int i=0;i<31;i++) game.Drawing.Paint(i,0,surface.SelectedColor);
            game.Evaluate(); FindButton("전시 시작").onClick.Invoke(); game.SkipExhibition(); FindButton("보유하기").onClick.Invoke();
            string firstId = game.Saved.Collection[0].Id;
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null;
            game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            var room = UnityEngine.Object.FindFirstObjectByType<GalleryRoom>();
            var studioTexture = (Texture2D)room.transform.Find("Studio/StudioArtwork0").GetComponent<MeshRenderer>().sharedMaterial.mainTexture;
            Assert.That((Color32)studioTexture.GetPixel(0,0),Is.EqualTo(firstColor));
            yield return EnterCanvas(); surface = UnityEngine.Object.FindFirstObjectByType<DrawingSurface>();
            FindButton("팔레트").onClick.Invoke(); wheel = UnityEngine.Object.FindFirstObjectByType<ColorWheelPicker>();
            wheel.SetColor(new Color32(18,93,211,255)); FindButton("이 색 사용하기").onClick.Invoke();
            Assert.That(surface.SelectedColor,Is.EqualTo(33));
            for(int i=0;i<31;i++) game.Drawing.Paint(i,0,33);
            game.Evaluate(); FindButton("전시 시작").onClick.Invoke(); game.SkipExhibition(); FindButton("보유하기").onClick.Invoke();
            Assert.That(game.Saved.Collection[0].CustomColors[0],Is.Not.EqualTo(game.Saved.Collection[1].CustomColors[0]));
            FindButton("컬렉션").onClick.Invoke(); FindButton("작품 1").onClick.Invoke(); FindButton("전시장에 걸기").onClick.Invoke(); FindButton("왼쪽 벽").onClick.Invoke();
            Assert.That(game.Saved.GalleryWallIds[0],Is.EqualTo(firstId));
            Assert.That(new SaveStore(GalleryPrototype.SavePath).Load().GalleryWallIds[0],Is.EqualTo(firstId));
            FindButton("다음").onClick.Invoke(); FindButton("다시 전시").onClick.Invoke();
            var galleryTexture = (Texture2D)room.transform.Find("GalleryFrame/Painting").GetComponent<MeshRenderer>().sharedMaterial.mainTexture;
            Assert.That((Color32)galleryTexture.GetPixel(0,0),Is.EqualTo(firstColor));
            for(int i=0;i<600;i++) room.Advance(.1f);
            Assert.That(room.OtherArtworkVisits,Is.GreaterThan(0)); Assert.That(room.EvaluatedCount,Is.EqualTo(10));
        }
        [UnityTest]
        public IEnumerator CanvasFocusUsesSameSceneAndPainterRespondsToStroke()
        {
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null;
            var game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            var room = UnityEngine.Object.FindFirstObjectByType<GalleryRoom>();
            var scene = SceneManager.GetActiveScene().handle;
            Assert.That(game.IsDrawingStage,Is.False);
            var before = room.CameraPosition;
            FindButton("캔버스").onClick.Invoke(); yield return null;
            Assert.That(game.IsDrawingStage,Is.False);
            yield return new WaitForSeconds(GalleryRoom.FocusSeconds+.1f);
            Assert.That(SceneManager.GetActiveScene().handle,Is.EqualTo(scene));
            Assert.That(game.IsDrawingStage,Is.True); Assert.That(room.CanvasReady,Is.True);
            Assert.That(Vector3.Distance(before,room.CameraPosition),Is.GreaterThan(1));
            var dim = GameObject.Find("ConfirmDrawingDim").GetComponent<UnityEngine.UI.Image>();
            Assert.That(dim.color.a,Is.InRange(.65f,.8f));
            var surface = UnityEngine.Object.FindFirstObjectByType<DrawingSurface>();
            Assert.That(((RectTransform)surface.transform.parent).anchoredPosition.x,Is.GreaterThan(0));
            Assert.That(room.transform.Find("Studio/Painter").localPosition.x,Is.LessThan(0));
            Assert.That(((RectTransform)surface.transform.parent).rect.size,Is.EqualTo(((RectTransform)surface.transform).rect.size));
            Assert.That(surface.transform.parent.GetComponent<UnityEngine.UI.Image>().color.a,Is.Zero);
            Assert.That(game.GetComponentInChildren<TMPro.TMP_InputField>(true).transform.parent.name,Is.EqualTo("ConfirmHang"));
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4]; ((RectTransform)surface.transform).GetWorldCorners(corners);
            var pixels = surface.GetComponentInParent<Canvas>().pixelRect;
            var camera = room.GetComponentInChildren<Camera>();
            var easel = room.transform.Find("Studio/EaselCanvas");
            Vector3 lower = camera.WorldToViewportPoint(easel.TransformPoint(new Vector3(-.5f,-.5f,0)));
            Vector3 upper = camera.WorldToViewportPoint(easel.TransformPoint(new Vector3(.5f,.5f,0)));
            Assert.That(lower.x,Is.EqualTo((corners[0].x-pixels.x)/pixels.width).Within(.002f));
            Assert.That(lower.y,Is.EqualTo((corners[0].y-pixels.y)/pixels.height).Within(.002f));
            Assert.That(upper.x,Is.EqualTo((corners[2].x-pixels.x)/pixels.width).Within(.002f));
            Assert.That(upper.y,Is.EqualTo((corners[2].y-pixels.y)/pixels.height).Within(.002f));
            Assert.That(surface.GetComponentInParent<UnityEngine.UI.CanvasScaler>().referenceResolution,Is.EqualTo(new Vector2(1920,1080)));
            var e = new PointerEventData(EventSystem.current) { pointerId=-1,button=PointerEventData.InputButton.Left };
            e.position = Point((RectTransform)surface.transform,5,5);
            surface.OnPointerDown(e); surface.OnPointerUp(e);
            Assert.That(room.IsPainting,Is.True);
            var arm = room.transform.Find("Studio/Painter/Body/PaintingArm");
            yield return new WaitForSeconds(.1f); var paintingPose = arm.localRotation;
            yield return new WaitForSeconds(.9f);
            Assert.That(room.IsPainting,Is.False);
            Assert.That(Quaternion.Angle(paintingPose,arm.localRotation),Is.GreaterThan(20));
            FindButton("작업실로").onClick.Invoke();
            Assert.That(game.IsDrawingStage,Is.False); Assert.That(game.Saved.DraftPixels[5*32+5],Is.EqualTo(1));
            yield return EnterCanvas(); Assert.That(game.Drawing.Pixels[5*32+5],Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator TwoFingerGestureZoomsWithoutPaintingAndResetsWhenClosed()
        {
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return EnterCanvas();
            var surface = UnityEngine.Object.FindFirstObjectByType<DrawingSurface>();
            var screen = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Touchscreen>();
            try
            {
                var rect = (RectTransform)surface.transform;
                Vector2 first = Point(rect,12,16), second = Point(rect,20,16);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(screen,new UnityEngine.InputSystem.LowLevel.TouchState { touchId=91,phase=UnityEngine.InputSystem.TouchPhase.Began,position=first });
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(screen,new UnityEngine.InputSystem.LowLevel.TouchState { touchId=92,phase=UnityEngine.InputSystem.TouchPhase.Began,position=second });
                UnityEngine.InputSystem.InputSystem.Update();
                var e = new PointerEventData(EventSystem.current) { pointerId=91,button=PointerEventData.InputButton.Left,position=first };
                surface.OnPointerDown(e);
                Assert.That(surface.Drawing.Pixels[16*32+12],Is.Zero);
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(screen,new UnityEngine.InputSystem.LowLevel.TouchState { touchId=91,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=first-Vector2.right*80 });
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(screen,new UnityEngine.InputSystem.LowLevel.TouchState { touchId=92,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=second+Vector2.right*80 });
                UnityEngine.InputSystem.InputSystem.Update(); yield return null;
                Assert.That(rect.localScale.x,Is.GreaterThan(1));
                FindButton("작업실로").onClick.Invoke();
            }
            finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(screen); }
            yield return EnterCanvas();
            surface = UnityEngine.Object.FindFirstObjectByType<DrawingSurface>();
            var next = new PointerEventData(EventSystem.current) { pointerId=-1,button=PointerEventData.InputButton.Left,position=Point((RectTransform)surface.transform,16,16) };
            surface.OnPointerDown(next); surface.OnPointerUp(next);
            Assert.That(surface.Drawing.Pixels[16*32+16],Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator ZoomedDrawingRejectsStrokesOutsideViewport()
        {
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return EnterCanvas();
            var game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            var surface = UnityEngine.Object.FindFirstObjectByType<DrawingSurface>();
            surface.Zoom(2);
            var rect = (RectTransform)surface.transform;
            var e = new PointerEventData(EventSystem.current) { pointerId=-1,button=PointerEventData.InputButton.Left };
            e.position = Point(rect,31,16); surface.OnPointerDown(e); surface.OnPointerUp(e);
            Assert.That(game.Drawing.Pixels[16*32+31],Is.Zero);
            e.position = Point(rect,16,16); surface.OnPointerDown(e);
            e.position = Point(rect,31,16); surface.OnDrag(e); surface.OnPointerUp(e);
            Assert.That(game.Drawing.Pixels[16*32+16],Is.EqualTo(1));
            Assert.That(game.Drawing.Pixels[16*32+31],Is.Zero);
            game.Drawing.Undo();
            Assert.That(game.Drawing.Pixels[16*32+16],Is.Zero);
        }
        [UnityTest]
        public IEnumerator RegularRequestAcceptsWarmNewArtAndPaysOnKeepAfterReload()
        {
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null;
            var game=UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            FindButton("단골의 부탁").onClick.Invoke(); Assert.That(FindButton("부탁 수락하기").interactable,Is.False); FindButton("나중에").onClick.Invoke();
            for(int i=0;i<31;i++)game.Drawing.Paint(i,0,7);
            game.Evaluate(); game.StartExhibition(); game.SkipExhibition(); FindButton("보유하기").onClick.Invoke();
            Assert.That(game.Saved.RegularVisits,Is.EqualTo(1));
            FindButton("단골의 부탁").onClick.Invoke(); FindButton("부탁 수락하기").onClick.Invoke();
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null; game=UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            Assert.That(game.Saved.CommissionAccepted,Is.True);
            for(int i=0;i<31;i++)game.Drawing.Paint(i,0,4);
            game.Evaluate(); game.StartExhibition();
            Assert.That(game.Saved.Pending.CommissionReward,Is.EqualTo(60));
            Assert.That(game.Saved.Pending.Comments[1],Does.Contain("따뜻해졌"));
            Assert.That(game.Saved.Pending.Judgment.Visitors[1].Name,Does.StartWith("단골"));
            var room=UnityEngine.Object.FindFirstObjectByType<GalleryRoom>(); Assert.That(room.transform.Find("Visitor1/Body/HeadPose/RegularHat"),Is.Not.Null);
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null; game=UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            Assert.That(game.Saved.Pending.CommissionReward,Is.EqualTo(60)); FindButton("보유하기").onClick.Invoke();
            Assert.That(game.Saved.Coins,Is.EqualTo(60)); Assert.That(game.Saved.CommissionCompleted,Is.True); Assert.That(game.Saved.Collection.Count,Is.EqualTo(2));
            FindButton("단골의 부탁").onClick.Invoke(); Assert.That(FindButton("부탁 완료").interactable,Is.False); FindButton("나중에").onClick.Invoke();
        }
        [UnityTest]
        public IEnumerator CollectionPlacementAndSaleButtonsStayInsideCard()
        {
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null;
            var game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            for (int i=0;i<31;i++) game.Drawing.Paint(i,0,3);
            game.Evaluate(); game.StartExhibition(); game.SkipExhibition(); FindButton("보유하기").onClick.Invoke();
            FindButton("컬렉션").onClick.Invoke();
            FindButton("작품 1").onClick.Invoke();
            FindButton("작업실에 걸기").onClick.Invoke(); yield return null;
            FindButton("액자 3").onClick.Invoke();
            var room = UnityEngine.Object.FindFirstObjectByType<GalleryRoom>();
            var texture = (Texture2D)room.transform.Find("Studio/StudioArtwork2").GetComponent<MeshRenderer>().sharedMaterial.mainTexture;
            Assert.That(texture.GetPixel(0,0),Is.Not.EqualTo(texture.GetPixel(31,31)));
            var data = new SaveStore(GalleryPrototype.SavePath).Load(); Assert.That(data.StudioWallIds[2],Is.EqualTo(data.Collection[0].Id)); Assert.That(string.IsNullOrEmpty(data.StudioWallIds[0]),Is.True);
            FindButton("컬렉션").onClick.Invoke(); FindButton("작품 1").onClick.Invoke();
            FindButton("그림 팔기").onClick.Invoke(); Canvas.ForceUpdateCanvases();
            var card = (RectTransform)GameObject.Find("SaleCard").transform;
            var cancel = (RectTransform)FindButton("취소").transform; var confirm = (RectTransform)FindButton("판매 확정").transform;
            Assert.That(cancel.anchoredPosition.x,Is.EqualTo(-confirm.anchoredPosition.x));
            foreach (var button in new[] {cancel,confirm}) Assert.That(Mathf.Abs(button.anchoredPosition.x)+button.rect.width/2,Is.LessThan(card.rect.width/2));
            FindButton("판매 확정").onClick.Invoke(); foreach (var id in new SaveStore(GalleryPrototype.SavePath).Load().StudioWallIds) Assert.That(string.IsNullOrEmpty(id),Is.True);
        }
        [UnityTest]
        public IEnumerator MuseumHasSimultaneousVisitorsWhoEachEvaluateOnce()
        {
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null;
            var game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            for (int i=0;i<31;i++) game.Drawing.Paint(i,0,3);
            game.Evaluate(); FindButton("전시 시작").onClick.Invoke();
            var room = UnityEngine.Object.FindFirstObjectByType<GalleryRoom>();
            Assert.That(room.VisitorCount,Is.EqualTo(10));
            var museum = (RectTransform)GameObject.Find("MuseumView").transform;
            Assert.That(museum.anchorMin,Is.EqualTo(Vector2.zero)); Assert.That(museum.anchorMax,Is.EqualTo(Vector2.one));
            foreach (string name in new[] { "빠르게 / 보통","건너뛰기" })
            {
                var control = (RectTransform)FindButton(name).transform;
                Assert.That(control.anchorMin.x,Is.EqualTo(1)); Assert.That(control.anchorMin.y,Is.GreaterThan(.9f));
                Assert.That(control.sizeDelta.x,Is.LessThanOrEqualTo(240));
            }
            Assert.That(room.transform.Find("Visitor0/Body/HeadPose/Head").GetComponent<MeshRenderer>(),Is.Not.Null); Assert.That(room.transform.Find("Visitor0/PixelCharacter"),Is.Null); Assert.That(room.View.IsCreated(),Is.True);
            foreach (var text in GameObject.Find("Gallery").GetComponentsInChildren<TMPro.TMP_Text>())
                Assert.That(System.Text.RegularExpressions.Regex.IsMatch(text.text,"[0-9]"),Is.False);
            Assert.That(room.ArtworkTexture.width,Is.EqualTo(32));
            foreach (string prop in new[] { "Bench","Plant","SculptureHead" })
                Assert.That(room.transform.Find(prop).GetComponent<MeshRenderer>().sharedMaterial.mainTexture,Is.Null);
            Assert.That(room.transform.Find("GalleryLight").GetComponent<Light>().intensity,Is.LessThan(.5f));
            Assert.That(room.transform.Find("ArtworkSpot2").GetComponent<Light>().intensity,Is.GreaterThan(room.transform.Find("ArtworkSpot0").GetComponent<Light>().intensity));
            int moving = 0;
            var starts = new Vector3[10];
            for (int i=0;i<10;i++) starts[i] = room.transform.Find("Visitor"+i).localPosition;
            for (int frame=0;frame<10;frame++) room.Advance(.1f);
            for (int i=0;i<10;i++) if ((starts[i]-room.transform.Find("Visitor"+i).localPosition).sqrMagnitude>.01f) moving++;
            Assert.That(moving,Is.GreaterThan(1));
            Assert.That(Quaternion.Angle(room.transform.Find("Visitor0/Body/LeftLeg").localRotation,room.transform.Find("Visitor0/Body/RightLeg").localRotation),Is.GreaterThan(1));
            for (int i=0;i<10;i++) Assert.That(room.transform.Find("Visitor"+i).localPosition.y,Is.Zero);
            bool sawInterest = false, sawFeeling = false, sawDeparture = false, sawGesture = false;
            bool regularInterest = false, regularFeeling = false;
            var visitorsField = typeof(GalleryRoom).GetField("visitors",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var visitorStates = (System.Collections.IList)visitorsField.GetValue(room);
            float minimumSpacing = float.MaxValue;
            var roots = new Transform[10]; for(int i=0;i<10;i++) roots[i] = room.transform.Find("Visitor"+i);
            for (int frame=0;frame<590;frame++)
            {
                room.Advance(.1f);
                regularInterest |= room.RegularVisitStage == 1;
                regularFeeling |= room.RegularVisitStage == 2;
                for (int i=0;i<10;i++)
                {
                    var visitor = visitorStates[i]; int state = (int)visitor.GetType().GetField("State").GetValue(visitor);
                    var badge = room.transform.Find("Visitor"+i+"/InterestEmoji");
                    Assert.That(badge.gameObject.activeSelf,Is.EqualTo(state == 2));
                    if (badge.gameObject.activeSelf)
                    {
                        bool interest = badge.GetComponent<MeshRenderer>().sharedMaterial.name == "Emoji1";
                        sawInterest |= interest; sawFeeling |= !interest;
                        var pose = room.transform.Find("Visitor"+i+"/Body/HeadPose");
                        sawGesture |= !interest && Quaternion.Angle(pose.localRotation,Quaternion.identity)>1;
                        if (!interest)
                        {
                            int emotion = (int)visitor.GetType().GetField("Emotion").GetValue(visitor);
                            float until = (float)visitor.GetType().GetField("StopUntil").GetValue(visitor);
                            float reactedAt = (float)visitor.GetType().GetField("ReactAt").GetValue(visitor);
                            float duration = until-reactedAt;
                            Assert.That(duration,Is.InRange(emotion == 4 ? 2f : emotion == 3 ? 7f : 5f,emotion == 4 ? 2.8f : emotion == 3 ? 7.8f : 5.8f));
                        }
                    }
                    else
                    {
                        if (sawFeeling) sawDeparture = true;
                        Assert.That(Quaternion.Angle(room.transform.Find("Visitor"+i+"/Body/HeadPose").localRotation,Quaternion.identity),Is.LessThan(.01f));
                    }
                }
                for(int i=0;i<10;i++) for(int j=i+1;j<10;j++) minimumSpacing = Mathf.Min(minimumSpacing,Vector3.Distance(roots[i].localPosition,roots[j].localPosition));
            }
            Assert.That(minimumSpacing,Is.GreaterThan(.45f));
            Assert.That(room.OtherArtworkVisits,Is.GreaterThan(10));
            Assert.That(room.EvaluatedCount,Is.EqualTo(10),"Every visitor must react before the exhibition ends without Complete");
            Assert.That(room.RegularVisitStage,Is.EqualTo(2));
            Assert.That(regularInterest && regularFeeling,Is.True);
            int likes = 0; foreach (var v in game.LastJudgment.Visitors) if (v.Liked) likes++;
            Assert.That(room.Likes,Is.EqualTo(likes));
            Assert.That(sawInterest && sawFeeling && sawDeparture && sawGesture,Is.True);
            room.Advance(10); room.Complete(); room.Complete();
            Assert.That(room.EvaluatedCount,Is.EqualTo(10)); Assert.That(room.Likes,Is.EqualTo(likes));
            game.SkipExhibition(); FindButton("보유하기").onClick.Invoke();
            Assert.That(room.CanvasReady,Is.False);
            Assert.That(room.transform.Find("Studio").gameObject.activeSelf,Is.True);
            yield return SceneManager.LoadSceneAsync("M0Prototype"); yield return null;
            game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            for (int i=0;i<31;i++) game.Drawing.Paint(i,0,7);
            game.Evaluate(); FindButton("전시 시작").onClick.Invoke();
            Assert.That(game.Saved.HasPending,Is.True);
        }
        static IEnumerator EnterCanvas()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<GalleryPrototype>();
            if (game.IsDrawingStage) yield break;
            FindButton("캔버스").onClick.Invoke();
            yield return new WaitForSeconds(GalleryRoom.FocusSeconds+.1f);
            Assert.That(game.IsDrawingStage,Is.True);
        }
        static Vector2 Point(RectTransform rect,int x,int y)
        {
            return RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(new Vector3((x+.5f)/32*rect.rect.width-rect.rect.width/2,(y+.5f)/32*rect.rect.height-rect.rect.height/2,0)));
        }
        static UnityEngine.UI.Button FindButton(string name)
        {
            foreach (var button in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
                if (button.name == name) return button;
            Assert.Fail("Button missing: " + name); return null;
        }
    }
}
