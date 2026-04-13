// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Catch.Difficulty.Evaluators;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    /// <summary>
    /// Uses harmonic weighting (like osu!standard Speed) instead of exponential strain decay.
    /// This paradigm weights the hardest individual objects more heavily than section-based peak decay.
    /// </summary>
    public class HarmonicMovement : Skill
    {
        private const double base_harmonic_scale = 12.0;
        private const double decay_exponent = 0.87;
        private const int position_bins = 16;

        public int PalpableObjectCount => ObjectDifficulties.Count;

        /// <summary>
        /// Ratio of median to 90th-percentile difficulty. Higher = more sustained difficulty.
        /// </summary>
        public double SustainedRatio { get; private set; }

        /// <summary>
        /// Shannon entropy of object positions across the playfield (0 = concentrated, ~4 = spread).
        /// </summary>
        public double PositionEntropy { get; private set; }

        /// <summary>
        /// Proportion of moving objects that involve a direction change.
        /// </summary>
        public double DirectionChangeRatio { get; private set; }

        /// <summary>
        /// Median per-object difficulty value.
        /// </summary>
        public double MedianDifficulty { get; private set; }

        /// <summary>
        /// Total sum of per-object difficulties divided by sqrt(object count).
        /// Captures total difficulty mass of the map, not just peaks.
        /// </summary>
        public double DifficultyMass { get; private set; }

        /// <summary>
        /// Ratio of top-5% mean difficulty to median difficulty. Higher = spikier map.
        /// </summary>
        public double Spikiness { get; private set; }

        private readonly int[] positionBinCounts = new int[position_bins];
        private int totalTrackedObjects;
        private int directionChangeCount;
        private int movingObjectCount;

        public HarmonicMovement(Mod[] mods)
            : base(mods)
        {
        }

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            var catchCurrent = (CatchDifficultyHitObject)current;

            // Track position distribution for entropy
            int bin = Math.Clamp((int)(catchCurrent.BaseObject.EffectiveX / (512.0 / position_bins)), 0, position_bins - 1);
            positionBinCounts[bin]++;
            totalTrackedObjects++;

            // Track direction changes
            if (Math.Abs(catchCurrent.DistanceMoved) > 0.1)
            {
                movingObjectCount++;

                if (current.Index >= 1)
                {
                    var catchLast = (CatchDifficultyHitObject)current.Previous(0);

                    if (Math.Abs(catchLast.DistanceMoved) > 0.1
                        && Math.Sign(catchCurrent.DistanceMoved) != Math.Sign(catchLast.DistanceMoved))
                    {
                        directionChangeCount++;
                    }
                }
            }

            return MovementEvaluator.EvaluateDifficultyOf(current);
        }

        public override double DifficultyValue()
        {
            double difficulty = 0;
            int index = 0;

            double[] difficulties = ObjectDifficulties.Where(p => p > 0).ToArray();

            double lengthRatio = difficulties.Length / 1000.0;
            double adaptiveScale = base_harmonic_scale * Math.Pow(lengthRatio, 0.12);

            foreach (double note in difficulties.OrderDescending())
            {
                double weight = (1 + (adaptiveScale / (1 + index)))
                                / (Math.Pow(index, decay_exponent) + 1 + (adaptiveScale / (1 + index)));

                difficulty += note * weight;
                index++;
            }

            double lengthBonus = 1 + 0.05 * Math.Log(Math.Max(difficulties.Length, 1));

            // Compute sustained ratio
            if (difficulties.Length >= 10)
            {
                double[] sorted = difficulties.OrderBy(x => x).ToArray();
                double median = sorted[sorted.Length / 2];
                double p90 = sorted[(int)(sorted.Length * 0.9)];
                SustainedRatio = p90 > 0 ? median / p90 : 0;
                MedianDifficulty = median;

                // Spikiness: ratio of top 5% mean to median
                int top5Start = (int)(sorted.Length * 0.95);
                double top5Mean = 0;

                for (int i = top5Start; i < sorted.Length; i++)
                    top5Mean += sorted[i];

                top5Mean /= Math.Max(1, sorted.Length - top5Start);
                Spikiness = median > 0 ? top5Mean / median : 0;
            }

            // Compute position entropy
            if (totalTrackedObjects > 10)
            {
                double entropy = 0;

                for (int i = 0; i < position_bins; i++)
                {
                    if (positionBinCounts[i] > 0)
                    {
                        double p = (double)positionBinCounts[i] / totalTrackedObjects;
                        entropy -= p * Math.Log2(p);
                    }
                }

                PositionEntropy = entropy;
            }

            // Compute direction change ratio
            if (movingObjectCount > 10)
                DirectionChangeRatio = (double)directionChangeCount / movingObjectCount;

            // Compute difficulty mass: total difficulty / sqrt(count)
            if (difficulties.Length > 0)
                DifficultyMass = difficulties.Sum() / Math.Sqrt(difficulties.Length);

            return difficulty * lengthBonus;
        }
    }
}
