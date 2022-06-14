// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Catch.Difficulty
{
    public class CatchPerformanceCalculator : PerformanceCalculator
    {
        private int fruitsHit;
        private int ticksHit;
        private int tinyTicksHit;
        private int tinyTicksMissed;
        private int misses;

        public CatchPerformanceCalculator()
            : base(new CatchRuleset())
        {

        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var catchAttributes = (CatchDifficultyAttributes)attributes;

            fruitsHit = score.Statistics.GetValueOrDefault(HitResult.Great);
            ticksHit = score.Statistics.GetValueOrDefault(HitResult.LargeTickHit);
            tinyTicksHit = score.Statistics.GetValueOrDefault(HitResult.SmallTickHit);
            tinyTicksMissed = score.Statistics.GetValueOrDefault(HitResult.SmallTickMiss);
            misses = score.Statistics.GetValueOrDefault(HitResult.Miss);

            // We are heavily relying on aim in catch the beat
            double value = Math.Pow(5.0 * Math.Max(1.0, catchAttributes.StarRating / 0.0049) - 4.0, 2) / 170000.0;

            // Longer maps are worth more. "Longer" means how many hits there are which can contribute to combo
            int numTotalHits = totalComboHits();

            // Longer maps are worth more
            double lengthFactor = numTotalHits * 0.5 + catchAttributes.DirectionChangeCount;
            double lengthBonus = Math.Log(lengthFactor + 600, 70) - 1 / Math.Log(lengthFactor + 100, 1000) + 0.77;

            // Longer maps are worth more
            value *= lengthBonus;

            // Penalize misses exponentially. This mainly fixes tag4 maps and the likes until a per-hitobject solution is available
            value *= Math.Pow(0.96, misses);

            // Combo scaling
            if (catchAttributes.MaxCombo > 0)
                value *= Math.Min(Math.Pow(score.MaxCombo, 0.42) / Math.Pow(catchAttributes.MaxCombo, 0.42), 1.0);


            double approachRate = catchAttributes.ApproachRate;
            double circleSize = score.BeatmapInfo.Difficulty.CircleSize;
            double approachRateFactor = 1.0 + (0.1 * Math.Pow(circleSize, 2) - (0.8 * circleSize) + 2.5) / 6;
            if (approachRate > 9.0)
                approachRateFactor = Math.Pow(approachRateFactor, Math.Max(1, 1 + 0.87 * Math.Pow(approachRate - 9.0, 1.7)) * Math.Max(1, Math.Pow(lengthBonus, 0.5)));
                approachRateFactor += 0.08 * (approachRate - 9.0); // 8% for each AR above 9
            if (approachRate > 10.0)
                approachRateFactor += 0.17 * Math.Pow(approachRate - 10.0, 1.4); // Additional 62% at AR 11


            value *= approachRateFactor;

            if (score.Mods.Any(m => m is ModHidden))
            {
                // Hiddens gives almost nothing on max approach rate, and more the lower it is
                if (approachRate <= 10.0)
                    value *= 1.06 + 0.07 * (10.0 - Math.Min(10.0, approachRate)); // 8% for each AR below 10
                else if (approachRate > 10.0)
                    value *= 1 + 0.04 * (11.0 - Math.Min(11.0, approachRate)); // 4% at AR 10, 1% at AR 11

                if (approachRate < 9.0)
                    value *= 1 + 0.03 * (9.0 - approachRate); // Additional 3% for each AR below 9
            }

            if (score.Mods.Any(m => m is ModFlashlight))
            {
                // Apply length bonus again if flashlight is on simply because it becomes a lot harder on longer maps.
                value *= Math.Pow(lengthBonus, 1.61);

                if (approachRate > 8.0f)
                    value *= 0.1f * (approachRate - 8.0f) + 1; // 18% for each AR above 8

            }

            // Scale the aim value with accuracy _slightly_
            value *= Math.Pow(accuracy(), 5.7);

            if (score.Mods.Any(m => m is ModNoFail))
                value *= Math.Max(0.90, 1.0 - 0.02 * misses);

            return new CatchPerformanceAttributes
            {
                Total = value
            };
        }

        private double accuracy() => totalHits() == 0 ? 0 : Math.Clamp((double)totalSuccessfulHits() / totalHits(), 0, 1);
        private int totalHits() => tinyTicksHit + ticksHit + fruitsHit + misses + tinyTicksMissed;
        private int totalSuccessfulHits() => tinyTicksHit + ticksHit + fruitsHit;
        private int totalComboHits() => misses + ticksHit + fruitsHit;
    }
}
