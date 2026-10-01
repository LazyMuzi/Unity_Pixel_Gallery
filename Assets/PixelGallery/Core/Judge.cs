using System;

namespace PixelGallery
{
    public enum Reaction { Passing, Interested, Like, Fan }

    [Serializable]
    public class NpcTaste
    {
        public string Id, Name;
        public double[] Targets = new double[10], Weights = new double[10], Tolerance = new double[10];
        public double Baseline = 0.6, Slope = 10;
    }

    [Serializable]
    public sealed class NpcVerdict
    {
        public string Id, Name;
        public double Score, Probability, Confidence;
        public bool Liked, Guaranteed;
        public Reaction Reaction;
        public int WeakestFeature;
    }

    [Serializable]
    public sealed class Judgment
    {
        public NpcVerdict[] Visitors;
        public double[] BuyerProbabilities;
        public int BuyerIndex;
        public double BuyerConfidence;
    }

    public static class Judge
    {
        public static Judgment Evaluate(double[] features, NpcTaste[] tastes, string artworkId, int day, double novelty = 0, double temperature = 0.1)
        {
            ValidateFeatures(features);
            if (tastes == null || tastes.Length == 0 || string.IsNullOrEmpty(artworkId) || !Finite(novelty) || novelty < 0 || !Finite(temperature) || temperature < 0)
                throw new ArgumentException("Invalid judgment input.");
            var result = new Judgment { Visitors = new NpcVerdict[tastes.Length] };
            int best = 0;
            for (int n = 0; n < tastes.Length; n++)
            {
                var taste = tastes[n];
                ValidateTaste(taste);
                double score = -novelty, worst = -1;
                int feature = 0;
                for (int i = 0; i < 10; i++)
                {
                    double loss = taste.Weights[i] * Math.Abs(features[i] - taste.Targets[i]) / taste.Tolerance[i];
                    score += taste.Weights[i] - loss;
                    if (loss > worst) { worst = loss; feature = i; }
                }
                double p = 1 / (1 + Math.Exp(-taste.Slope * (score - taste.Baseline)));
                result.Visitors[n] = new NpcVerdict { Id = taste.Id, Name = taste.Name, Score = score, Probability = p,
                    Confidence = Math.Abs(p - 0.5) * 2, Liked = Roll(p, Seed(artworkId, taste.Id, day)), WeakestFeature = feature };
                if (score > result.Visitors[best].Score) best = n;
            }
            if (!result.Visitors[best].Liked) { result.Visitors[best].Liked = true; result.Visitors[best].Guaranteed = true; }
            foreach (var v in result.Visitors)
                v.Reaction = v.Liked ? (v.Score >= 0.9 ? Reaction.Fan : Reaction.Like) : (v.Probability >= 0.35 ? Reaction.Interested : Reaction.Passing);
            result.BuyerProbabilities = BuyerProbabilities(result.Visitors, temperature);
            double roll = new Random(Seed(artworkId, "buyer", day)).NextDouble(), cumulative = 0;
            result.BuyerIndex = best;
            double first = 0, second = 0;
            for (int i = 0; i < result.Visitors.Length; i++)
            {
                double p = result.BuyerProbabilities[i];
                if (p > first) { second = first; first = p; } else if (p > second) second = p;
                cumulative += p;
                if (roll < cumulative) { result.BuyerIndex = i; roll = 2; }
            }
            result.BuyerConfidence = first - second;
            return result;
        }

        public static double[] BuyerProbabilities(NpcVerdict[] visitors, double temperature)
        {
            if (visitors == null || visitors.Length == 0 || !Finite(temperature) || temperature < 0) throw new ArgumentException("Invalid buyer input.");
            int best = -1;
            for (int i = 0; i < visitors.Length; i++)
                if (visitors[i].Liked && (best < 0 || visitors[i].Score > visitors[best].Score)) best = i;
            if (best < 0) throw new ArgumentException("A buyer requires at least one fan.");
            var probabilities = new double[visitors.Length];
            if (temperature == 0) { probabilities[best] = 1; return probabilities; }
            double sum = 0;
            for (int i = 0; i < visitors.Length; i++)
            {
                if (!visitors[i].Liked) continue;
                probabilities[i] = Math.Exp((visitors[i].Score - visitors[best].Score) / temperature);
                sum += probabilities[i];
            }
            for (int i = 0; i < probabilities.Length; i++) probabilities[i] /= sum;
            return probabilities;
        }

        public static bool Roll(double probability, int seed)
        {
            if (!Finite(probability) || probability < 0 || probability > 1) throw new ArgumentException("Invalid probability.");
            return new Random(seed).NextDouble() < probability;
        }

        public static int Seed(string artwork, string npc, int day)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in artwork + "|" + npc + "|" + day.ToString(System.Globalization.CultureInfo.InvariantCulture)) hash = (hash ^ c) * 16777619;
                return (int)hash;
            }
        }

        static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
        static void ValidateFeatures(double[] f)
        {
            if (f == null || f.Length != 10) throw new ArgumentException("Expected ten features.");
            foreach (double v in f) if (!Finite(v) || v < 0 || v > 1) throw new ArgumentException("Feature outside 0..1.");
        }
        static void ValidateTaste(NpcTaste t)
        {
            if (t == null || string.IsNullOrEmpty(t.Id) || t.Targets == null || t.Weights == null || t.Tolerance == null || t.Targets.Length != 10 || t.Weights.Length != 10 || t.Tolerance.Length != 10 || !Finite(t.Baseline) || !Finite(t.Slope) || t.Slope <= 0) throw new ArgumentException("Invalid taste.");
            for (int i = 0; i < 10; i++)
                if (!Finite(t.Targets[i]) || t.Targets[i] < 0 || t.Targets[i] > 1 || !Finite(t.Weights[i]) || t.Weights[i] < 0 || !Finite(t.Tolerance[i]) || t.Tolerance[i] <= 0) throw new ArgumentException("Invalid taste feature.");
        }
    }
}
