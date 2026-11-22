using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FunFlow.Plinko.Tests
{
    public class PlinkoMathTests
    {
        private PlinkoConfig _cfg;

        [SetUp]
        public void SetUp()
        {
            _cfg = ScriptableObject.CreateInstance<PlinkoConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_cfg != null)
            {
                Object.DestroyImmediate(_cfg);
            }
        }

        [Test]
        public void PegCountsAndBoardSymmetryMatchDefaultLayout()
        {
            Assert.AreEqual(2, PlinkoMath.GetPegCountInRow(0, _cfg));
            Assert.AreEqual(6, PlinkoMath.GetPegCountInRow(4, _cfg));

            Vector2 spawn = PlinkoMath.GetSpawnPosition(_cfg);
            Assert.AreEqual(0f, spawn.x, 0.0001f);
            Assert.AreEqual(_cfg.topPegY + _cfg.spawnOffsetY, spawn.y, 0.0001f);

            Vector2 centerSlot = PlinkoMath.GetSlotPosition(3, _cfg);
            Assert.AreEqual(0f, centerSlot.x, 0.0001f);
        }

        [Test]
        public void GenerateGuidedPathHitsValidPegsAndLandsInEveryTargetSlot()
        {
            for (int target = 0; target < _cfg.slotCount; target++)
            {
                for (int s = 0; s < 40; s++)
                {
                    int seed = target * 100 + s;
                    var rng = new System.Random(seed);
                    List<PlinkoMath.GuidedStep> path = PlinkoMath.GenerateGuidedPath(target, _cfg, rng);

                    Assert.AreEqual(_cfg.pegRows + 2, path.Count);
                    Assert.IsFalse(path[0].isPegHit);
                    Assert.AreEqual(PlinkoMath.GetSpawnPosition(_cfg), path[0].position);

                    for (int r = 0; r < _cfg.pegRows; r++)
                    {
                        PlinkoMath.GuidedStep step = path[r + 1];
                        Assert.IsTrue(step.isPegHit);
                        Assert.AreEqual(r, step.row);
                        Assert.GreaterOrEqual(step.col, 0);
                        Assert.Less(step.col, PlinkoMath.GetPegCountInRow(r, _cfg));
                        Assert.AreEqual(PlinkoMath.GetPegPosition(step.row, step.col, _cfg), step.position);
                    }

                    for (int i = 1; i < path.Count; i++)
                    {
                        Assert.AreEqual(
                            0.5f * _cfg.horizontalSpacing,
                            Mathf.Abs(path[i].position.x - path[i - 1].position.x),
                            0.001f);
                    }

                    PlinkoMath.GuidedStep last = path[path.Count - 1];
                    Assert.IsFalse(last.isPegHit);
                    Assert.AreEqual(target, last.col);
                    Assert.AreEqual(PlinkoMath.GetSlotPosition(target, _cfg), last.position);
                }
            }
        }

        [Test]
        public void GenerateGuidedPathClampsOutOfRangeTargetSlot()
        {
            List<PlinkoMath.GuidedStep> low = PlinkoMath.GenerateGuidedPath(-5, _cfg, new System.Random(1));
            List<PlinkoMath.GuidedStep> high = PlinkoMath.GenerateGuidedPath(99, _cfg, new System.Random(1));

            Assert.AreEqual(0, low[low.Count - 1].col);
            Assert.AreEqual(_cfg.slotCount - 1, high[high.Count - 1].col);
        }

        [Test]
        public void GenerateGuidedPath_MiddleSlot_ProducesVariedRandomPaths()
        {
            var distinctPaths = new HashSet<string>();
            var sb = new System.Text.StringBuilder();

            for (int seed = 0; seed < 30; seed++)
            {
                List<PlinkoMath.GuidedStep> path = PlinkoMath.GenerateGuidedPath(3, _cfg, new System.Random(seed));
                sb.Clear();
                for (int i = 1; i <= _cfg.pegRows; i++)
                {
                    if (i > 1)
                    {
                        sb.Append('-');
                    }

                    sb.Append(path[i].col);
                }

                distinctPaths.Add(sb.ToString());
            }

            Assert.GreaterOrEqual(distinctPaths.Count, 4);
        }
    }
}
