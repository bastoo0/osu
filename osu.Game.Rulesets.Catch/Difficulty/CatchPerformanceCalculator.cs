// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Catch.Objects;
using System.Collections.Generic;


namespace osu.Game.Rulesets.Catch.Difficulty
{
    public class CatchPerformanceCalculator : PerformanceCalculator
    {
        private int num300;
        private int num100;
        private int num50;
        private int numKatu;
        private int numMiss;

        public CatchPerformanceCalculator()
            : base(new CatchRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var catchAttributes = (CatchDifficultyAttributes)attributes;

            num300 = score.GetCount300() ?? 0; // HitResult.Great
            num100 = score.GetCount100() ?? 0; // HitResult.LargeTickHit
            num50 = score.GetCount50() ?? 0; // HitResult.SmallTickHit
            numKatu = score.GetCountKatu() ?? 0; // HitResult.SmallTickMiss
            numMiss = score.GetCountMiss() ?? 0; // HitResult.Miss PLUS HitResult.LargeTickMiss

            // We are heavily relying on aim in catch the beat
            double value = Math.Pow(5.0 * Math.Max(1.0, catchAttributes.StarRating / 0.0049) - 4.0, 2.0) / 100000.0;

            // Longer maps are worth more. "Longer" means how many hits there are which can contribute to combo
            int numTotalHits = totalComboHits();

            double lengthBonus =
                0.95 + 0.3 * Math.Min(1.0, numTotalHits / 2500.0) +
                (numTotalHits > 2500 ? Math.Log10(numTotalHits / 2500.0) * 0.475 : 0.0);
            value *= lengthBonus;

            value *= Math.Pow(0.97, numMiss);

            // Applying miss penalty (WIP: combo scaling removal)
            double missesPenalty = computeMissesPenalty(score, catchAttributes);
            value *= missesPenalty;

            double approachRate = catchAttributes.ApproachRate;

            double approachRateFactor = 1.0;
            if (approachRate > 9.0)
                approachRateFactor += 0.1 * (approachRate - 9.0); // 10% for each AR above 9
            if (approachRate > 10.0)
                approachRateFactor += 0.1 * (approachRate - 10.0); // Additional 10% at AR 11, 30% total
            else if (approachRate < 8.0)
                approachRateFactor += 0.025 * (8.0 - approachRate); // 2.5% for each AR below 8

            value *= approachRateFactor;

            if (score.Mods.Any(m => m is ModHidden))
            {
                // Hiddens gives almost nothing on max approach rate, and more the lower it is
                if (approachRate <= 10.0)
                    value *= 1.05 + 0.075 * (10.0 - approachRate); // 7.5% for each AR below 10
                else if (approachRate > 10.0)
                    value *= 1.01 + 0.04 * (11.0 - Math.Min(11.0, approachRate)); // 5% at AR 10, 1% at AR 11
            }

            if (score.Mods.Any(m => m is ModFlashlight))
                value *= 1.35 * lengthBonus;

            value *= Math.Pow(accuracy(), 5.5);

            if (score.Mods.Any(m => m is ModNoFail))
                value *= Math.Max(0.90, 1.0 - 0.02 * numMiss);

            return new CatchPerformanceAttributes
            {
                Total = value
            };
        }

        private List<int> getMissedObjectsIndexes(ScoreInfo score)
        {

            List<int> missesFound = [];
            for (int i = 0; i < score.HitEvents.Count; i++)
            {
                var hitEvent = score.HitEvents[i];
                // We are not supposed to pass Bananas and TinyDroplets but leaving the condition as a safeguard just in case
                if (hitEvent.Result == HitResult.Miss && hitEvent.HitObject is not Banana && hitEvent.HitObject is not TinyDroplet)
                {
                    missesFound.Add(i);
                }
            }
            return missesFound;
        }

        private double computeMissesPenalty(ScoreInfo score, CatchDifficultyAttributes catchAttributes)
        {
            List<int> missedObjIdx = getMissedObjectsIndexes(score);

            int totalObjects = catchAttributes.MaxCombo;

            if (missedObjIdx.Count == 0)
                return 1.0;

            List<double> missesValues = new List<double>();

            double strainMin = catchAttributes.MovementStrains.Min();
            double strainMax = catchAttributes.MovementStrains.Max();
            double densityMin = catchAttributes.Densities.Min();
            double densityMax = catchAttributes.Densities.Max();

            for (int i = 0; i < missedObjIdx.Count; i++)
            {
                // The miss doesn't hold the same weight if it was done in a hard part
                // Therefore we must identify in which part the miss has taken place
                // To do that we compare the density and strain of the missed object to the rest of the densities and strains
                // so we can linearly interpolate how hard the part was compared to the rest of the beatmap (between 0 and 1)
                double movementValue = catchAttributes.MovementStrains[i];
                double relativeMovementValue = Math.Min(0, (movementValue - strainMin) / (strainMax - strainMin));
                double densityValue = catchAttributes.Densities[i];
                double relativeDensityValue = Math.Min(0, (densityValue - densityMin) / (densityMax - densityMin));

                // We don't want misses in easy parts to be weighted at 1 so we start at 0.5
                double movementScaling = 0.5;
                double missValue = movementScaling + (movementScaling * relativeMovementValue);
                // Then we want to know if a miss is in the same section of the previous one
                // If that's the case, according to the density, we decrease the miss value
                // because we don't want to penalize chain misses
                if (i > 0)
                {
                    // Check if current miss is close to previous miss
                    int previousMissIndex = missedObjIdx[i - 1];
                    int currentMissIndex = missedObjIdx[i];

                    // Define chain threshold based on density
                    double chainThreshold = 5 * (1 + relativeDensityValue);

                    if (currentMissIndex - previousMissIndex <= chainThreshold)
                    {
                        // For chain misses, increase the missValue (meaning less penalty)
                        // Higher density = more forgiving on chains
                        double chainBonus = 0.3 + (0.4 * relativeDensityValue);
                        missValue *= (1 + chainBonus);
                    }
                }
                missesValues.Add(missValue);

            }

            missesValues.Sort((a, b) => b.CompareTo(a));

            // Apply exponential weighting so that the largest miss matters more
            double weightFactor = 1.8; // Adjust this factor for stronger weighting
            double weightSum = 0.0;

            for (int i = 0; i < missesValues.Count; i++)
            {
                double weightedMiss = missesValues[i] * Math.Pow(weightFactor, i);
                weightSum += weightedMiss;
            }

            double normalizedPenalty = weightSum / totalObjects;

            double penalty = Math.Max(0, 1.0 - weightSum);

            return penalty;
        }

        private double accuracy() => totalHits() == 0 ? 0 : Math.Clamp((double)totalSuccessfulHits() / totalHits(), 0, 1);
        private int totalHits() => num50 + num100 + num300 + numMiss + numKatu;
        private int totalSuccessfulHits() => num50 + num100 + num300;
        private int totalComboHits() => numMiss + num100 + num300;

    }
}
