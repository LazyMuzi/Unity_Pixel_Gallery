using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace PixelGallery.Tests
{
    public class SessionTests
    {
        static Artwork Art() => new Artwork { Id = "art", Title = "시험 그림", Pixels = new byte[1024], Features = new double[10] };
        static ShowRecord Show(int day = 1)
        {
            var taste = new NpcTaste { Id = "warm#0", Name = "관람객" };
            for (int i = 0; i < 10; i++) taste.Tolerance[i] = 1;
            taste.Weights[0] = 1;
            var judgment = Judge.Evaluate(new double[10], new[] { taste }, "art", day);
            int price = Economy.Price(judgment,1);
            return new ShowRecord { Id = "show" + day, ArtworkId = "art", Day = day, Price = price,
                Offer = Economy.Offer(judgment,price,"art",day), Judgment = judgment, Comments = new[] { "좋아요." } };
        }
        [Test]
        public void LightingPurchasePersistsOnceAndDoublesIncomeWithoutChangingArtworkPrice()
        {
            bool fail = false;
            Assert.That(new GallerySession(new SaveData { Coins=60 },_=>{}).BuyGalleryLighting(),Is.False);
            var session = new GallerySession(new SaveData { Day=4,Coins=59 },_=>{});
            Assert.That(session.BuyGalleryLighting(),Is.False);
            session = new GallerySession(new SaveData { Day=4,Coins=60 },_=> { if (fail) throw new IOException("disk full"); });
            session.Start(Art(),Show(4),true); session.Finish(); Assert.That(session.BuyGalleryLighting(),Is.False); session.Resolve(false);
            session.PlaceInGallery("art",0); int price = session.Data.Collection[0].LastShow.Price;
            fail=true; Assert.Throws<IOException>(()=>session.BuyGalleryLighting()); Assert.That(session.Data.Coins,Is.EqualTo(60)); Assert.That(session.Data.GalleryLighting,Is.False);
            fail=false; Assert.That(session.BuyGalleryLighting(),Is.True); Assert.That(session.Data.Coins,Is.Zero); Assert.That(session.BuyGalleryLighting(),Is.False);
            var loaded=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(session.Data)); loaded.Pending=null;
            session=new GallerySession(loaded,_=>{}); Assert.That(session.GalleryIncome,Is.EqualTo(2)); session.EarnGalleryIncome();
            Assert.That(session.Data.Coins,Is.EqualTo(2)); Assert.That(session.Data.Collection[0].LastShow.Price,Is.EqualTo(price));
        }
        [Test]
        public void GalleryIncomeOnlyPaysDisplayedStoredArtworkAndSurvivesFailedSave()
        {
            bool fail = false;
            var session = new GallerySession(new SaveData { Day=4 },_ => { if (fail) throw new IOException("disk full"); });
            Assert.That(session.EarnGalleryIncome(),Is.False);
            session.Start(Art(),Show(4),true); session.Finish(); session.Resolve(false);
            Assert.That(session.GalleryIncome,Is.Zero);
            session.PlaceInGallery("art",0); Assert.That(session.GalleryIncome,Is.EqualTo(1));
            session.PlaceInGallery("art",2); Assert.That(session.GalleryIncome,Is.EqualTo(1));
            var before = session.Data; fail = true;
            Assert.Throws<IOException>(() => session.EarnGalleryIncome()); Assert.That(session.Data,Is.SameAs(before));
            fail = false; session.EarnGalleryIncome(); Assert.That(session.Data.Coins,Is.EqualTo(1));
            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(session.Data)); loaded.Pending=null;
            session = new GallerySession(loaded,_ => {}); Assert.That(session.Data.Coins,Is.EqualTo(1));
            session.Start(session.Data.Collection[0],Show(5),false);
            Assert.That(session.EarnGalleryIncome(),Is.False); session.Finish(); session.Resolve(false);
            session.EarnGalleryIncome(); Assert.That(session.Data.Coins,Is.EqualTo(2));
            int offer = session.Data.Collection[0].LastShow.Offer;
            session.SellStored("art"); Assert.That(session.Data.Coins,Is.EqualTo(2+offer)); Assert.That(session.EarnGalleryIncome(),Is.False);
        }
        [Test]
        public void RegularRemembersArtworkAndCommissionPaysOnceEvenWhenKept()
        {
            bool fail = false;
            var session = new GallerySession(new SaveData(),_ => { if (fail) throw new IOException("disk full"); });
            Assert.That(session.SetCommission(true),Is.False);
            session.Start(Art(),Show(),true); session.Finish(); session.Finish(); session.Resolve(false);
            Assert.That(session.Data.RegularVisits,Is.EqualTo(1)); Assert.That(session.SetCommission(true),Is.True);
            var features = new double[10]; features[0]=.1; features[2]=.8; features[4]=.8;
            Assert.That(session.RegularComment("art",features),Does.Contain("지난번"));
            Assert.That(session.RegularComment("new",features),Does.Contain("따뜻해졌"));
            var artwork = Art(); artwork.Id="new"; artwork.Features=features;
            var show = Show(2); show.ArtworkId="new";
            session.Start(artwork,show,true); Assert.That(session.Data.Pending.CommissionReward,Is.EqualTo(60));
            Assert.That(session.SetCommission(false),Is.False);
            session.Finish(); var before = session.Data; fail=true;
            Assert.Throws<IOException>(() => session.Resolve(false)); Assert.That(session.Data,Is.SameAs(before)); fail=false;
            Assert.That(session.Resolve(false),Is.True); Assert.That(session.Data.Coins,Is.EqualTo(60));
            Assert.That(session.Resolve(false),Is.False); Assert.That(session.SetCommission(true),Is.False);
            Assert.That(session.Data.CommissionCompleted,Is.True); Assert.That(session.Data.Collection.Count,Is.EqualTo(2));
            features[2]=0; Assert.That(session.Data.RegularLastFeatures[2],Is.EqualTo(.8));
            var loaded=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(session.Data)); if(!loaded.HasPending)loaded.Pending=null;
            var restored=new GallerySession(loaded,_ => {});
            Assert.That(restored.Data.RegularVisits,Is.EqualTo(2)); Assert.That(restored.Data.CommissionCompleted,Is.True);
        }
        [Test]
        public void CommissionIsOptionalAndReexhibitionCannotClaimIt()
        {
            var session=new GallerySession(new SaveData(),_ => {});
            var art=Art(); art.Features[0]=.1; art.Features[2]=.8; art.Features[4]=.8;
            session.Start(art,Show(),true); session.Finish(); session.Resolve(false);
            Assert.That(session.Data.Coins,Is.Zero); session.SetCommission(true); session.SetCommission(false);
            Assert.That(session.Data.CommissionAccepted,Is.False); session.SetCommission(true);
            session.Start(session.Data.Collection[0],Show(2),false);
            Assert.That(session.Data.Pending.CommissionReward,Is.Zero); session.Finish(); session.Resolve(false);
            Assert.That(session.Data.CommissionAccepted,Is.True); Assert.That(session.Data.CommissionCompleted,Is.False);
            var f=new double[10]; f[0]=.03; f[2]=.6; f[4]=.65;
            Assert.That(GallerySession.MeetsCommission(f),Is.True); f[2]=.59; Assert.That(GallerySession.MeetsCommission(f),Is.False);
            f[2]=.6; f[4]=.64; Assert.That(GallerySession.MeetsCommission(f),Is.False);
        }
        [Test]
        public void StudioPlacementPersistsMovesAndClearsOnSale()
        {
            bool fail = false;
            var session = new GallerySession(new SaveData(),_ => { if (fail) throw new IOException("disk full"); });
            session.Start(Art(),Show(),true); session.Finish(); session.Resolve(false);
            Assert.That(session.Data.StudioWallIds[0],Is.EqualTo("art"));
            var before = session.Data; fail = true;
            Assert.Throws<IOException>(() => session.PlaceInStudio("art",2)); Assert.That(session.Data,Is.SameAs(before)); fail = false;
            Assert.That(session.PlaceInStudio("missing",1),Is.False);
            Assert.That(session.PlaceInStudio("art",2),Is.True); Assert.That(session.Data.StudioWallIds[0],Is.Null);
            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(session.Data)); if (!loaded.HasPending) loaded.Pending = null;
            var restored = new GallerySession(loaded,_ => {});
            Assert.That(restored.Data.StudioWallIds[2],Is.EqualTo("art"));
            restored.SellStored("art"); foreach (var id in restored.Data.StudioWallIds) Assert.That(string.IsNullOrEmpty(id),Is.True);
            var legacy = new SaveData { StudioWallIds = null }; legacy.Validate(); Assert.That(legacy.StudioWallIds.Length,Is.EqualTo(3));
        }
        [Test]
        public void SaleIsOnceAndFailedWriteLeavesStateUntouched()
        {
            bool fail = false;
            var session = new GallerySession(new SaveData(), _ => { if (fail) throw new IOException("disk full"); });
            session.Start(Art(),Show(),true); session.Finish();
            var before = session.Data; fail = true;
            Assert.Throws<IOException>(() => session.Resolve(true));
            Assert.That(session.Data, Is.SameAs(before)); Assert.That(session.Data.Coins,Is.Zero);
            fail = false; int offer = before.Pending.Offer;
            Assert.That(session.Resolve(true), Is.True);
            Assert.That(session.Resolve(true), Is.False);
            Assert.That(session.SellStored("art"), Is.False);
            Assert.That(session.Data.Coins, Is.EqualTo(offer)); Assert.That(session.Data.Collection, Is.Empty);
            Assert.That(session.Data.Day,Is.EqualTo(2));
        }
        [Test]
        public void KeepRehangAndSellPreserveDraftAndFixedResults()
        {
            var session = new GallerySession(new SaveData(), _ => {});
            session.Start(Art(),Show(),true); session.Finish(); session.Resolve(false);
            var draft = new byte[1024]; draft[0] = 3; session.SaveDraft(draft,"작업 중","draft");
            session.Start(session.Data.Collection[0],Show(2),false);
            Assert.That(session.Data.DraftPixels[0],Is.EqualTo(3)); Assert.That(session.Data.Collection.Count,Is.EqualTo(1));
            Assert.That(session.Data.Collection[0].TimesShown,Is.EqualTo(2));
            var restored = new GallerySession(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(session.Data)),_ => {});
            Assert.That(restored.Data.Pending.Judgment.Visitors[0].Probability,Is.EqualTo(session.Data.Pending.Judgment.Visitors[0].Probability));
            restored.Finish(); restored.Resolve(false);
            int offer = restored.Data.Collection[0].LastShow.Offer;
            Assert.That(restored.SellStored("art"),Is.True); Assert.That(restored.SellStored("art"),Is.False);
            Assert.That(restored.Data.Coins,Is.EqualTo(offer)); Assert.That(restored.Data.DraftTitle,Is.EqualTo("작업 중"));
        }
        [Test]
        public void AtomicSaveRecoversBackupAndRejectsDamagedData()
        {
            string directory = Path.Combine(Path.GetTempPath(),"pixel-gallery-test-" + Guid.NewGuid().ToString("N"));
            string file = Path.Combine(directory,"save.json");
            try
            {
                var store = new SaveStore(file); var session = new GallerySession(store.Load(),store.Write);
                store.Write(new SaveData()); Assert.That(store.Load().Pending,Is.Null);
                session.Start(Art(),Show(),true); int offer = session.Data.Pending.Offer;
                session.Finish();
                var loaded = store.Load(); Assert.That(loaded.Pending.Finished,Is.True);
                Assert.That(loaded.Collection[0].Pixels.Length,Is.EqualTo(1024));
                Assert.That(loaded.Pending.Offer,Is.EqualTo(offer));
                File.WriteAllText(file,"broken"); var recovery = new SaveStore(file);
                var backup = recovery.Load(); Assert.That(recovery.RecoveredFromBackup,Is.True);
                Assert.That(backup.Pending.Finished,Is.False);
                recovery.Write(backup); Assert.That(new SaveStore(file).Load().Pending.Offer,Is.EqualTo(offer));
                backup.Coins = -1; Assert.Throws<InvalidDataException>(() => recovery.Write(backup));
                Assert.That(new SaveStore(file).Load().Coins,Is.Zero);
                File.WriteAllText(file,"broken"); File.WriteAllText(file+".bak","broken");
                Assert.Throws<ArgumentException>(() => new SaveStore(file).Load());
                Assert.That(File.ReadAllText(file),Is.EqualTo("broken"));
                var fresh = new SaveStore(Path.Combine(directory,"fresh.json"));
                var sold = new GallerySession(new SaveData(),fresh.Write);
                sold.Start(Art(),Show(),true); sold.Finish(); sold.Resolve(true);
                Assert.That(fresh.Load().Pending,Is.Null); Assert.That(fresh.Load().Coins,Is.EqualTo(offer));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
        }
        [Test]
        public void PurchasesAreAtomicAndToolsUndoAsOneStroke()
        {
            bool fail = false;
            var session = new GallerySession(new SaveData { Coins = 1000 },_ => { if (fail) throw new IOException("disk full"); });
            var before = session.Data; fail = true;
            Assert.Throws<IOException>(() => session.BuyTool(1)); Assert.That(session.Data,Is.SameAs(before));
            fail = false;
            Assert.That(session.BuyTool(1),Is.True); Assert.That(session.BuyTool(2),Is.True); Assert.That(session.BuyTool(4),Is.True);
            Assert.That(session.BuyTool(4),Is.False); Assert.That(session.Data.Coins,Is.EqualTo(350));
            Assert.That(session.Data.Tools,Is.EqualTo(7)); Assert.That(session.Data.Colors.Count,Is.EqualTo(32));
            session.Data.DraftPixels[0] = 33; Assert.Throws<InvalidDataException>(session.Data.Validate);
            session.Data.DraftPixels[0] = 9; session.Data.Validate();
            var poor = new GallerySession(new SaveData(),_ => Assert.Fail("No write on rejected purchase"));
            Assert.That(poor.BuyTool(1),Is.False);
            var drawing = new Drawing(); drawing.BeginStroke(); drawing.Line(0,0,0,0,9,2,true); drawing.EndStroke();
            Assert.That(drawing.Pixels[0],Is.EqualTo(9)); Assert.That(drawing.Pixels[31],Is.EqualTo(9));
            Assert.That(drawing.Pixels[32+30],Is.EqualTo(9)); drawing.Undo();
            Assert.That(drawing.Pixels,Is.EqualTo(new byte[1024]));
            drawing.BeginStroke(); drawing.Line(16,0,16,31,1); drawing.EndStroke();
            drawing.BeginStroke(); drawing.Fill(0,0,3); drawing.EndStroke();
            Assert.That(drawing.Pixels[15],Is.EqualTo(3)); Assert.That(drawing.Pixels[17],Is.Zero);
            drawing.Undo(); Assert.That(drawing.Pixels[15],Is.Zero); Assert.That(drawing.Pixels[16],Is.EqualTo(1));
        }
        [Test]
        public void VersionOneMigratesAndUnlocksSurviveReload()
        {
            string directory = Path.Combine(Path.GetTempPath(),"pixel-gallery-migration-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); string file = Path.Combine(directory,"save.json");
            try
            {
                var legacy = new SaveData { Version = 1, Coins = 500, Day = 8 };
                File.WriteAllText(file,JsonUtility.ToJson(legacy));
                var store = new SaveStore(file); var loaded = store.Load();
                Assert.That(loaded.Version,Is.EqualTo(2)); Assert.That(loaded.Coins,Is.EqualTo(500));
                Assert.That(loaded.Day,Is.EqualTo(8)); Assert.That(loaded.Colors.Count,Is.EqualTo(32));
                var session = new GallerySession(loaded,store.Write); session.BuyTool(2);
                var pixels = new byte[1024]; pixels[0] = 32; session.SaveDraft(pixels,"해금 그림","new");
                loaded = store.Load(); Assert.That(loaded.Tools,Is.EqualTo(2)); Assert.That(loaded.DraftPixels[0],Is.EqualTo(32));
                Assert.That(loaded.Coins,Is.EqualTo(300)); Assert.That(loaded.Colors.Contains(32),Is.True);
            }
            finally { Directory.Delete(directory,true); }
        }
        [Test]
        public void ThirdSettlementExpandsGalleryAndLaterPricesSurviveReload()
        {
            bool fail = false;
            var session = new GallerySession(new SaveData(),_ => { if (fail) throw new IOException("disk full"); });
            for (int day=1;day<=3;day++)
            {
                session.Start(Art(),Show(day),true); session.Finish(); session.Finish();
                Assert.That(session.Data.GalleryExpanded,Is.False);
                Assert.That(session.Data.Pending.Price,Is.EqualTo(Show(day).Price));
                if (day == 3)
                {
                    var before = session.Data; fail = true;
                    Assert.Throws<IOException>(() => session.Resolve(false)); Assert.That(session.Data,Is.SameAs(before)); fail = false;
                }
                Assert.That(session.Resolve(false),Is.True); Assert.That(session.Resolve(false),Is.False);
            }
            Assert.That(session.Data.Day,Is.EqualTo(4)); Assert.That(session.Data.GalleryExpanded,Is.True);
            var baseShow = Show(4); session.Start(Art(),baseShow,true);
            Assert.That(baseShow.GrowthBonus,Is.False);
            Assert.That(session.Data.Pending.GrowthBonus,Is.True);
            Assert.That(session.Data.Pending.Price,Is.EqualTo(baseShow.Price*120/100));
            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(session.Data));
            var restored = new GallerySession(loaded,_ => {});
            Assert.That(restored.Data.Pending.Price,Is.EqualTo(session.Data.Pending.Price));
            Assert.That(restored.Data.Pending.Offer,Is.EqualTo(Economy.Offer(baseShow.Judgment,baseShow.Price*120/100,"art",4)));
            restored.Finish(); int offer = restored.Data.Pending.Offer; restored.Resolve(true); restored.Resolve(true);
            Assert.That(restored.Data.Coins,Is.EqualTo(offer));
        }
        [Test]
        public void CustomColorsAndGalleryPlacementAreValidatedAndAtomic()
        {
            bool fail = false; var session = new GallerySession(new SaveData { Day = 4 },_ => { if (fail) throw new IOException("disk full"); });
            var art = Art(); art.Pixels[0] = 33; art.CustomColors = new[] { 0x123456 };
            session.SaveDraft(art.Pixels,"色","custom",art.CustomColors);
            Assert.That(session.Data.DraftColors,Is.EqualTo(art.CustomColors));
            session.Start(art,Show(4),true); session.Finish(); session.Resolve(false);
            Assert.That(session.Data.DraftColors,Is.Empty);
            var before = session.Data; fail = true;
            Assert.Throws<IOException>(() => session.PlaceInGallery("art",0)); Assert.That(session.Data,Is.SameAs(before)); fail = false;
            Assert.That(session.PlaceInGallery("art",0),Is.True); session.PlaceInGallery("art",2);
            Assert.That(session.Data.GalleryWallIds[0],Is.Null); Assert.That(session.Data.GalleryWallIds[2],Is.EqualTo("art"));
            Assert.That(session.PlaceInGallery("missing",0),Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.PlaceInGallery("art",3));
            session.SellStored("art"); Assert.That(session.Data.GalleryWallIds[2],Is.Null);
            var corrupt = new SaveData { DraftColors = new[] { -1 } }; Assert.Throws<InvalidDataException>(corrupt.Validate);
            corrupt.DraftColors = new int[224]; Assert.Throws<InvalidDataException>(corrupt.Validate);
            Assert.That(new GallerySession(new SaveData(),_ => {}).PlaceInGallery("art",0),Is.False);
        }
        [Test]
        public void PriceAndFanOfferFollowFormula()
        {
            var judgment = Show().Judgment;
            judgment.Visitors[0].Liked = true; judgment.Visitors[0].Reaction = Reaction.Fan;
            Assert.That(Economy.Price(judgment,1),Is.EqualTo(30));
            Assert.That(Economy.Offer(judgment,30,"art",1),Is.EqualTo(33));
            Assert.That(Economy.Rarity(new double[10],new System.Collections.Generic.List<FeatureSnapshot>()),Is.EqualTo(1));
            var ones = new double[10]; for (int i=0;i<10;i++) ones[i]=1;
            Assert.That(Economy.Rarity(ones,new System.Collections.Generic.List<FeatureSnapshot>{ new FeatureSnapshot { Values = new double[10] } }),Is.EqualTo(1.5));
        }
    }
}
