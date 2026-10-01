using System;
using System.Collections.Generic;
using System.IO;

namespace PixelGallery
{
    [Serializable]
    public sealed class Artwork
    {
        public string Id, Title;
        public byte[] Pixels;
        public int[] CustomColors = new int[0];
        public double[] Features;
        public long CreatedAt;
        public int TimesShown;
        public ShowRecord LastShow;
        public Artwork Copy() { return (Artwork)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class ShowRecord
    {
        public string Id, ArtworkId, Theme;
        public int Day, Price, Offer, CommissionReward;
        public long ShownAt;
        public Judgment Judgment;
        public string[] Comments;
        public bool Finished, GrowthBonus;
        public ShowRecord Copy() { return (ShowRecord)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class FeatureSnapshot { public double[] Values; }

    [Serializable]
    public sealed class SaveData
    {
        public int Version = 2, Coins, Day = 1, Tools;
        public List<int> Colors = new List<int> { 1,2,3,4,5,6,7,8 };
        public List<string> SeenNpcs = new List<string>();
        public byte[] DraftPixels = new byte[Drawing.Size * Drawing.Size];
        public string DraftTitle = "나의 첫 그림", DraftId;
        public List<Artwork> Collection = new List<Artwork>();
        public List<FeatureSnapshot> Recent = new List<FeatureSnapshot>();
        public ShowRecord Pending;
        public bool HasPending;
        public string HangingId;
        public string[] StudioWallIds = new string[3], GalleryWallIds = new string[3];
        public int[] DraftColors = new int[0];
        public int RegularVisits;
        public double[] RegularLastFeatures = new double[0];
        public string RegularLastArtworkId;
        public bool CommissionAccepted, CommissionCompleted;
        public bool GalleryExpanded => Day >= 4;
        public SaveData Copy()
        {
            var copy = (SaveData)MemberwiseClone();
            copy.Collection = new List<Artwork>(Collection); copy.Recent = new List<FeatureSnapshot>(Recent);
            copy.StudioWallIds = (string[])StudioWallIds.Clone(); copy.GalleryWallIds = (string[])GalleryWallIds.Clone();
            copy.DraftColors = (int[])DraftColors.Clone();
            copy.RegularLastFeatures = (double[])RegularLastFeatures.Clone();
            copy.Colors = new List<int>(Colors); copy.SeenNpcs = new List<string>(SeenNpcs);
            return copy;
        }
        public void Validate()
        {
            if (Version != 2 || Coins < 0 || Day < 1 || Collection == null || Recent == null || Recent.Count > 10)
                throw new InvalidDataException("Unsupported or damaged save.");
            if (Colors == null || Colors.Count < 8 || Colors.Count > 32 || Tools < 0 || Tools > 7 || SeenNpcs == null || SeenNpcs.Count > 5)
                throw new InvalidDataException("Invalid unlocks.");
            var colors = new HashSet<int>();
            foreach (int color in Colors) if (color < 1 || color > 32 || !colors.Add(color)) throw new InvalidDataException("Invalid color unlock.");
            for (int i=1;i<=8;i++) if (!colors.Contains(i)) throw new InvalidDataException("Missing starter color.");
            var seen = new HashSet<string>();
            foreach (string npc in SeenNpcs) if (!seen.Add(npc) || (npc != "minimalist" && npc != "warm" && npc != "detail" && npc != "order" && npc != "trend")) throw new InvalidDataException("Invalid NPC unlock.");
            if (RegularLastFeatures == null) RegularLastFeatures = new double[0];
            if (RegularVisits < 0 || (RegularVisits == 0 ? RegularLastFeatures.Length != 0 : RegularLastFeatures.Length != 10) || (CommissionAccepted && (CommissionCompleted || RegularVisits == 0))) throw new InvalidDataException("Invalid regular visitor state.");
            if (RegularLastFeatures.Length > 0) ValidateFeatures(RegularLastFeatures);
            for (int i=1;i<=32;i++) if (!Colors.Contains(i)) Colors.Add(i);
            if (DraftColors == null) DraftColors = new int[0];
            ValidatePixels(DraftPixels,DraftColors);
            if (DraftTitle == null || DraftTitle.Length > 40) throw new InvalidDataException("Invalid draft title.");
            var ids = new HashSet<string>();
            foreach (var artwork in Collection)
            {
                if (artwork == null || string.IsNullOrEmpty(artwork.Id) || !ids.Add(artwork.Id) || artwork.Title == null || artwork.Title.Length > 40 || artwork.TimesShown < 1)
                    throw new InvalidDataException("Invalid artwork.");
                if (artwork.CustomColors == null) artwork.CustomColors = new int[0];
                ValidatePixels(artwork.Pixels,artwork.CustomColors); ValidateFeatures(artwork.Features); ValidateShow(artwork.LastShow);
                if (artwork.LastShow.ArtworkId != artwork.Id) throw new InvalidDataException("Artwork result mismatch.");
            }
            if (StudioWallIds == null) StudioWallIds = new string[3];
            if (StudioWallIds.Length != 3) throw new InvalidDataException("Invalid studio layout.");
            var wallIds = new HashSet<string>();
            foreach (string id in StudioWallIds) if (!string.IsNullOrEmpty(id) && (!ids.Contains(id) || !wallIds.Add(id))) throw new InvalidDataException("Invalid studio artwork.");
            if (GalleryWallIds == null) GalleryWallIds = new string[3];
            if (GalleryWallIds.Length != 3) throw new InvalidDataException("Invalid gallery layout.");
            wallIds.Clear();
            foreach (string id in GalleryWallIds) if (!string.IsNullOrEmpty(id) && (!ids.Contains(id) || !wallIds.Add(id))) throw new InvalidDataException("Invalid gallery artwork.");
            foreach (var snapshot in Recent)
            {
                if (snapshot == null) throw new InvalidDataException("Invalid history.");
                ValidateFeatures(snapshot.Values);
            }
            if (!string.IsNullOrEmpty(HangingId) && !ids.Contains(HangingId)) throw new InvalidDataException("Missing hanging artwork.");
            if (HasPending != (Pending != null)) throw new InvalidDataException("Invalid exhibition state.");
            if (Pending != null)
            {
                ValidateShow(Pending);
                var artwork = Collection.Find(a => a.Id == Pending.ArtworkId);
                if (Pending.Day != Day || artwork == null || artwork.LastShow.Id != Pending.Id || HangingId != artwork.Id)
                    throw new InvalidDataException("Invalid pending exhibition.");
            }
        }
        static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
        static void ValidateFeatures(double[] values)
        {
            if (values == null || values.Length != 10) throw new InvalidDataException("Invalid features.");
            foreach (double v in values) if (!Finite(v) || v < 0 || v > 1) throw new InvalidDataException("Invalid feature value.");
        }
        void ValidatePixels(byte[] pixels,int[] custom)
        {
            if (pixels == null || pixels.Length != 1024) throw new InvalidDataException("Invalid pixel data.");
            if (custom.Length > 223) throw new InvalidDataException("Too many artwork colors.");
            foreach (int color in custom) if (color < 0 || color > 0xffffff) throw new InvalidDataException("Invalid artwork color.");
            foreach (byte pixel in pixels) if (pixel > 32+custom.Length) throw new InvalidDataException("Unknown palette index.");
        }
        static void ValidateShow(ShowRecord show)
        {
            if (show == null || string.IsNullOrEmpty(show.Id) || string.IsNullOrEmpty(show.ArtworkId) || show.Day < 1 || show.Price < 1 || show.Offer < 1 || show.Comments == null)
                throw new InvalidDataException("Invalid show result.");
            if (show.CommissionReward != 0 && show.CommissionReward != 60) throw new InvalidDataException("Invalid commission reward.");
            var judge = show.Judgment;
            if (judge == null || judge.Visitors == null || judge.Visitors.Length < 1 || judge.Visitors.Length > 12 || judge.BuyerIndex < 0 || judge.BuyerIndex >= judge.Visitors.Length || judge.BuyerProbabilities == null || judge.BuyerProbabilities.Length != judge.Visitors.Length || show.Comments.Length != judge.Visitors.Length)
                throw new InvalidDataException("Invalid judge result.");
            double sum = 0;
            for (int i = 0; i < judge.Visitors.Length; i++)
            {
                var v = judge.Visitors[i];
                if (v == null || string.IsNullOrEmpty(v.Id) || v.Name == null || !Finite(v.Score) || !Finite(v.Probability) || v.Probability < 0 || v.Probability > 1 || !Finite(v.Confidence) || v.Confidence < 0 || v.Confidence > 1 || v.WeakestFeature < 0 || v.WeakestFeature >= 10 || v.Reaction < Reaction.Passing || v.Reaction > Reaction.Fan || v.Liked != (v.Reaction >= Reaction.Like) || show.Comments[i] == null)
                    throw new InvalidDataException("Invalid visitor.");
                double p = judge.BuyerProbabilities[i];
                if (!Finite(p) || p < 0 || p > 1 || (!v.Liked && p != 0)) throw new InvalidDataException("Invalid buyer probability.");
                sum += p;
            }
            if (!judge.Visitors[judge.BuyerIndex].Liked || Math.Abs(sum - 1) > .00001 || !Finite(judge.BuyerConfidence) || judge.BuyerConfidence < 0 || judge.BuyerConfidence > 1)
                throw new InvalidDataException("Invalid buyer.");
        }
    }

    public sealed class GallerySession
    {
        public SaveData Data { get; private set; }
        readonly Action<SaveData> persist;
        public GallerySession(SaveData data, Action<SaveData> persist)
        {
            data.Validate(); Data = data; this.persist = persist ?? throw new ArgumentNullException(nameof(persist));
        }
        void Commit(SaveData next) { next.Validate(); persist(next); Data = next; }
        public static bool MeetsCommission(double[] features) => features != null && features.Length == 10 && features[(int)Feature.Coverage] >= .03 && features[(int)Feature.Warmth] >= .6 && features[(int)Feature.Brightness] >= .65;
        public bool SetCommission(bool accepted)
        {
            if (Data.Pending != null || Data.RegularVisits == 0 || Data.CommissionCompleted) return false;
            var next = Data.Copy(); next.CommissionAccepted = accepted; Commit(next); return true;
        }
        public string RegularComment(string artworkId,double[] current)
        {
            if (Data.RegularVisits == 0) return "처음 뵙네요. 따뜻한 색의 그림을 좋아해요. 다음 전시에도 들를게요.";
            if (Data.RegularLastArtworkId == artworkId) return "지난번에 본 그림이네요. 다시 천천히 살펴볼게요.";
            double warmth = current[(int)Feature.Warmth]-Data.RegularLastFeatures[(int)Feature.Warmth];
            double brightness = current[(int)Feature.Brightness]-Data.RegularLastFeatures[(int)Feature.Brightness];
            if (Math.Abs(warmth) >= .15) return warmth > 0 ? "지난 그림보다 색이 따뜻해졌네요. 새 그림도 천천히 볼게요." : "지난 그림보다 차분하고 서늘한 색이네요. 다른 분위기도 반가워요.";
            if (Math.Abs(brightness) >= .15) return brightness > 0 ? "지난 그림보다 밝아졌네요. 작업실에 빛이 들어온 것 같아요." : "지난 그림보다 빛이 잔잔해졌네요. 천천히 들여다볼게요.";
            return "지난 그림의 분위기가 기억나요. 이번 그림에서도 작가님다운 색이 보이네요.";
        }
        public bool BuyTool(int tool)
        {
            int price = tool == 1 ? 150 : tool == 2 ? 200 : tool == 4 ? 300 : throw new ArgumentOutOfRangeException(nameof(tool));
            if (Data.Pending != null || (Data.Tools & tool) != 0 || Data.Coins < price) return false;
            var next = Data.Copy(); next.Coins -= price; next.Tools |= tool; Commit(next); return true;
        }
        public void SaveDraft(byte[] pixels, string title, string id,int[] customColors = null)
        {
            if (Data.Pending != null) return;
            var next = Data.Copy(); next.DraftPixels = (byte[])pixels.Clone(); next.DraftTitle = title; next.DraftId = id;
            if (customColors != null) next.DraftColors = (int[])customColors.Clone();
            Commit(next);
        }
        public void Start(Artwork artwork, ShowRecord show, bool fromDraft)
        {
            if (Data.Pending != null || show.Day != Data.Day || show.ArtworkId != artwork.Id) throw new InvalidOperationException("Exhibition already active or mismatched.");
            var next = Data.Copy(); show = show.Copy();
            show.GrowthBonus = Data.GalleryExpanded;
            if (show.GrowthBonus)
            {
                show.Price = checked(show.Price*120)/100;
                show.Offer = Economy.Offer(show.Judgment,show.Price,artwork.Id,Data.Day);
            }
            show.CommissionReward = fromDraft && !next.Collection.Exists(a => a.Id == artwork.Id) && next.CommissionAccepted && !next.CommissionCompleted && MeetsCommission(artwork.Features) ? 60 : 0;
            var copy = artwork.Copy(); copy.TimesShown++; copy.LastShow = show;
            int index = next.Collection.FindIndex(a => a.Id == copy.Id);
            if (index < 0) next.Collection.Add(copy); else next.Collection[index] = copy;
            foreach (var visitor in show.Judgment.Visitors)
            {
                string id = visitor.Id.Split('#')[0];
                if (!next.SeenNpcs.Contains(id)) next.SeenNpcs.Add(id);
            }
            next.Pending = show; next.HasPending = true; next.HangingId = copy.Id;
            next.Recent.Add(new FeatureSnapshot { Values = (double[])copy.Features.Clone() });
            if (next.Recent.Count > 10) next.Recent.RemoveAt(0);
            if (fromDraft) { next.DraftPixels = new byte[1024]; next.DraftId = null; next.DraftTitle = "나의 다음 그림"; next.DraftColors = new int[0]; }
            Commit(next);
        }
        public void Finish()
        {
            if (Data.Pending == null || Data.Pending.Finished) return;
            var next = Data.Copy(); next.Pending = Data.Pending.Copy(); next.Pending.Finished = true;
            int index = next.Collection.FindIndex(a => a.Id == next.Pending.ArtworkId);
            var artwork = next.Collection[index].Copy(); artwork.LastShow = next.Pending; next.Collection[index] = artwork;
            if (Array.Exists(next.Pending.Judgment.Visitors,v => v.Id == "warm#0"))
            {
                next.RegularVisits = checked(next.RegularVisits+1); next.RegularLastFeatures = (double[])artwork.Features.Clone(); next.RegularLastArtworkId = artwork.Id;
            }
            Commit(next);
        }
        public bool Resolve(bool sell)
        {
            if (Data.Pending == null || !Data.Pending.Finished) return false;
            var next = Data.Copy();
            if (next.Pending.CommissionReward > 0 && !next.CommissionCompleted)
            {
                next.Coins = checked(next.Coins+next.Pending.CommissionReward); next.CommissionCompleted = true; next.CommissionAccepted = false;
            }
            if (sell)
            {
                next.Coins = checked(next.Coins + next.Pending.Offer);
                next.Collection.RemoveAll(a => a.Id == next.Pending.ArtworkId); ClearStudio(next,next.Pending.ArtworkId); next.HangingId = null;
            }
            if (!sell && Array.IndexOf(next.StudioWallIds,next.Pending.ArtworkId) < 0)
                for (int i=0;i<3;i++) if (string.IsNullOrEmpty(next.StudioWallIds[i])) { next.StudioWallIds[i] = next.Pending.ArtworkId; break; }
            next.Pending = null; next.HasPending = false; next.Day = checked(next.Day + 1); Commit(next); return true;
        }
        static void ClearStudio(SaveData data,string id)
        {
            for (int i=0;i<3;i++)
            { if (data.StudioWallIds[i] == id) data.StudioWallIds[i] = null; if (data.GalleryWallIds[i] == id) data.GalleryWallIds[i] = null; }
        }
        public bool PlaceInStudio(string id,int slot)
        {
            if (slot < 0 || slot >= 3) throw new ArgumentOutOfRangeException(nameof(slot));
            if (Data.Pending != null || !Data.Collection.Exists(a => a.Id == id)) return false;
            var next = Data.Copy(); ClearStudio(next,id); next.StudioWallIds[slot] = id; Commit(next); return true;
        }
        public bool PlaceInGallery(string id,int slot)
        {
            if (slot < 0 || slot >= 3) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!Data.GalleryExpanded || Data.Pending != null || !Data.Collection.Exists(a => a.Id == id)) return false;
            var next = Data.Copy();
            for (int i=0;i<3;i++) if (next.GalleryWallIds[i] == id) next.GalleryWallIds[i] = null;
            next.GalleryWallIds[slot] = id; Commit(next); return true;
        }
        public bool SellStored(string id)
        {
            if (Data.Pending != null) return false;
            var artwork = Data.Collection.Find(a => a.Id == id);
            if (artwork == null) return false;
            var next = Data.Copy(); next.Coins = checked(next.Coins + artwork.LastShow.Offer);
            next.Collection.RemoveAll(a => a.Id == id); ClearStudio(next,id);
            if (next.HangingId == id) next.HangingId = null;
            Commit(next); return true;
        }
    }
}
