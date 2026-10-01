using System;
using System.Collections.Generic;

namespace PixelGallery
{
    public static class Economy
    {
        public static int Price(Judgment judgment, double rarity)
        {
            if (judgment == null || judgment.Visitors == null || double.IsNaN(rarity) || rarity < 1 || rarity > 1.5) throw new ArgumentException("Invalid price input.");
            int likes = 0, fans = 0;
            foreach (var v in judgment.Visitors) if (v.Liked) { likes++; if (v.Reaction == Reaction.Fan) fans++; }
            return (int)Math.Round(20 * (1 + .15 * likes) * (1 + .3 * fans) * rarity, MidpointRounding.AwayFromZero);
        }
        public static int Offer(Judgment judgment, int price, string artworkId, int day)
        {
            double factor = judgment.Visitors[judgment.BuyerIndex].Reaction == Reaction.Fan ? 1.1 : .9 + .2 * new Random(Judge.Seed(artworkId,"offer",day)).NextDouble();
            return Math.Max(1, (int)Math.Round(price * factor, MidpointRounding.AwayFromZero));
        }
        public static double Rarity(double[] features, List<FeatureSnapshot> recent)
        {
            if (recent.Count == 0) return 1;
            double distance = 0;
            for (int i = 0; i < 10; i++)
            {
                double mean = 0; foreach (var snapshot in recent) mean += snapshot.Values[i];
                double delta = features[i] - mean / recent.Count; distance += delta * delta;
            }
            return 1 + .5 * Math.Min(1, Math.Sqrt(distance / 10));
        }
    }
}
