using System;
using System.Diagnostics;
using NUnit.Framework;

namespace PixelGallery.Tests
{
    public class PrototypeTests
    {
        readonly PixelColor[] palette = { new PixelColor(0,0,0), new PixelColor(255,0,0), new PixelColor(0,0,255) };
        [Test]
        public void AnalyzerHandlesEmptyAndSolidCanvases()
        {
            var blank = ArtworkAnalyzer.Analyze(new byte[16],4,palette,palette[1]);
            Assert.That(blank[0], Is.Zero); Assert.That(blank[5], Is.EqualTo(1)); Assert.That(blank[8], Is.Zero);
            var solid = new byte[16]; for (int i = 0; i < 16; i++) solid[i] = 1;
            var f = ArtworkAnalyzer.Analyze(solid,4,palette,palette[1]);
            Assert.That(f, Is.EqualTo(new[] { 1.0,1.0/16,1,1,1,1,0,1.0/16,1,1 }).Within(1e-10));
        }
        [Test]
        public void AnalyzerCountsFourConnectedRegionsAndBoundaries()
        {
            var p = new byte[16]; for (int i = 0; i < 16; i++) p[i] = (byte)((i % 4 + i / 4) % 2 + 1);
            var f = ArtworkAnalyzer.Analyze(p,4,palette,palette[1]);
            Assert.That(f[2], Is.EqualTo(.5)); Assert.That(f[5], Is.Zero);
            Assert.That(f[6], Is.EqualTo(1)); Assert.That(f[7], Is.EqualTo(1));
            p = new byte[16]; p[0] = 1;
            f = ArtworkAnalyzer.Analyze(p,4,palette,palette[1]);
            Assert.That(f[8], Is.EqualTo(0).Within(1e-10)); Assert.That(f[6], Is.EqualTo(2.0/24).Within(1e-10));
        }
        [Test]
        public void InvalidInputsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => ArtworkAnalyzer.Analyze(new byte[] { 3,0,0,0 },2,palette,palette[1]));
            var f = new double[10]; f[0] = double.NaN;
            Assert.Throws<ArgumentException>(() => Judge.Evaluate(f,new[] { Taste("a",0) },"art",1));
            Assert.Throws<ArgumentException>(() => Judge.Roll(1.1,1));
        }
        static NpcTaste Taste(string id, double target)
        {
            var t = new NpcTaste { Id = id, Name = id };
            for (int i = 0; i < 10; i++) t.Tolerance[i] = 1;
            t.Weights[0] = 1; t.Targets[0] = target; return t;
        }
        [Test]
        public void JudgeIsDeterministicAndGuaranteesBestFan()
        {
            var tastes = new[] { Taste("a",1), Taste("b",.9), Taste("c",.8) };
            var a = Judge.Evaluate(new double[10],tastes,"art",1,10);
            var b = Judge.Evaluate(new double[10],tastes,"art",1,10);
            Assert.That(a.Visitors[2].Liked, Is.True); Assert.That(a.Visitors[2].Guaranteed, Is.True);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(a.Visitors[i].Liked, Is.EqualTo(b.Visitors[i].Liked));
                Assert.That(a.Visitors[i].Probability, Is.EqualTo(b.Visitors[i].Probability));
            }
            Assert.That(a.BuyerIndex, Is.EqualTo(b.BuyerIndex));
            Assert.That(a.Visitors[a.BuyerIndex].Liked, Is.True);
        }
        [Test]
        public void LikeRollsAreCalibratedAcrossThousandSeeds()
        {
            int likes = 0;
            for (int i = 0; i < 1000; i++) if (Judge.Roll(.7,Judge.Seed("art-" + i,"npc",1))) likes++;
            Assert.That(likes, Is.InRange(650,750));
        }
        [Test]
        public void BuyerSoftmaxExcludesNonFansAndConvergesToBest()
        {
            var v = new[] { new NpcVerdict { Liked = true, Score = .7 }, new NpcVerdict { Liked = true, Score = .9 }, new NpcVerdict { Liked = false, Score = 100 } };
            var p = Judge.BuyerProbabilities(v,.1);
            Assert.That(p[2], Is.Zero); Assert.That(p[0]+p[1], Is.EqualTo(1).Within(1e-10));
            p = Judge.BuyerProbabilities(v,1e-9); Assert.That(p[1], Is.EqualTo(1));
            p = Judge.BuyerProbabilities(v,0); Assert.That(p[1], Is.EqualTo(1));
        }
        [Test]
        public void DrawingInterpolatesAndUndoesStrokesWithThirtyStepLimit()
        {
            var d = new Drawing(); d.BeginStroke(); Assert.That(d.Line(0,0,31,31,1), Is.True); Assert.That(d.Line(0,0,31,31,1), Is.False); d.EndStroke();
            for (int i = 0; i < 32; i++) Assert.That(d.Pixels[i*32+i], Is.EqualTo(1));
            d.Undo(); Assert.That(d.Pixels, Is.All.Zero);
            for (int i = 0; i < 31; i++) { d.BeginStroke(); d.Paint(i,0,1); d.EndStroke(); }
            for (int i = 0; i < 30; i++) d.Undo();
            Assert.That(d.CanUndo, Is.False); Assert.That(d.Pixels[0], Is.EqualTo(1));
        }
        [Test]
        public void ExhibitThresholdAndClearUndoAreExact()
        {
            var d = new Drawing();
            for (int i = 0; i < 30; i++) d.Paint(i%32,i/32,1);
            Assert.That(d.CanExhibit, Is.False); d.Paint(30,0,1); Assert.That(d.CanExhibit, Is.True);
            d.Clear(); Assert.That(d.CanExhibit, Is.False); d.Undo(); Assert.That(d.CanExhibit, Is.True);
        }
        [Test]
        public void ConfiguredNpcTastesDisagreeAboutSimpleRedRectangle()
        {
            var profiles = new[] {
                UnityEditor.AssetDatabase.LoadAssetAtPath<NpcProfile>("Assets/PixelGallery/Data/minimalist.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<NpcProfile>("Assets/PixelGallery/Data/warm.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<NpcProfile>("Assets/PixelGallery/Data/detail.asset") };
            var pixels = new byte[1024];
            for (int y = 8; y < 24; y++) for (int x = 8; x < 24; x++) pixels[y*32+x] = 1;
            var red = new[] { new PixelColor(0,0,0), new PixelColor(220,77,63) };
            var f = ArtworkAnalyzer.Analyze(pixels,32,red,new PixelColor(75,131,185));
            var result = Judge.Evaluate(f,new[] { profiles[0].Taste,profiles[1].Taste,profiles[2].Taste },"red-rectangle",1);
            Assert.That(result.Visitors[0].Score, Is.LessThan(.9));
            Assert.That(result.Visitors[1].Probability, Is.GreaterThan(.8));
            Assert.That(result.Visitors[2].Probability, Is.LessThan(.35));
        }
        [Test]
        public void ReportAnalyzerTiming()
        {
            var p = new byte[1024]; for (int i = 0; i < p.Length; i++) p[i] = (byte)(i%3);
            ArtworkAnalyzer.Analyze(p,32,palette,palette[1]);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++) ArtworkAnalyzer.Analyze(p,32,palette,palette[1]);
            watch.Stop(); TestContext.WriteLine("Analyzer average ms: " + watch.Elapsed.TotalMilliseconds/100);
        }
    }
}
