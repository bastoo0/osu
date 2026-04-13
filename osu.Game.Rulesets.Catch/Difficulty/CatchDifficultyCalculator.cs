// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Catch.Difficulty.Skills;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Catch.Difficulty
{
    public class CatchDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 4.59;
        private const double star_rating_offset = 0.8305469000727514;
        private const double star_rating_scale = 1.1358973421504561;
        private const double mid_star_rating_threshold = 1.5512539541604746;
        private const double mid_star_rating_scale = -0.5911658328084414;
        private const double high_star_rating_threshold = 2.6193828758158872;
        private const double high_star_rating_scale = 1.0028101231077016;
        private const double post_star_rating_offset = 0.08089796530621726;
        private const double post_star_rating_scale = 1.3184127663894007;
        private const double post_mid_star_rating_threshold = 4.078398689523984;
        private const double post_mid_star_rating_scale = -0.3182619295246969;
        private const double post_high_star_rating_threshold = 1.9025014764094572;
        private const double post_high_star_rating_scale = 0.30153798035350987;
        private const double final_star_rating_offset = 0.07274219722459008;
        private const double final_star_rating_scale = 1.0503353845646375;
        private const double final_mid_star_rating_threshold = 2.5938049431823704;
        private const double final_mid_star_rating_scale = 0.12435396107124866;
        private const double final_high_star_rating_threshold = 7.819975474573456;
        private const double final_high_star_rating_scale = -0.5904199565416541;

        public override int Version => 2026041205;

        public CatchDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0)
                return new CatchDifficultyAttributes { Mods = mods };

            double movement = skills.OfType<HarmonicMovement>().Single().DifficultyValue();
            double precisionPatterns = skills.OfType<PrecisionPatterns>().Single().DifficultyValue();
            double sustainedRatio = skills.OfType<HarmonicMovement>().Single().SustainedRatio;
            double positionEntropy = skills.OfType<HarmonicMovement>().Single().PositionEntropy;
            double directionChangeRatio = skills.OfType<HarmonicMovement>().Single().DirectionChangeRatio;
            double medianDifficulty = skills.OfType<HarmonicMovement>().Single().MedianDifficulty;
            double difficultyMass = skills.OfType<HarmonicMovement>().Single().DifficultyMass;
            double spikiness = skills.OfType<HarmonicMovement>().Single().Spikiness;

            // Normalize entropy: max is log2(16)=4.0
            double normalizedEntropy = positionEntropy / 4.0;

            // For very high movement maps, sustained difficulty indicates predictable zigzag
            // which is less difficult than varied patterns at the same speed
            double movementExcess = Math.Max(0, movement - 0.55) / 0.35;
            double sustainedModifier = 1 + 0.13 * sustainedRatio * (1 - movementExcess);

            // High sustained-movement with low direction-change ratio indicates wide predictable flow
            double sustainedMovementScore = movement * (1 - directionChangeRatio);
            double smsPenalty = 1 - 0.55 * Math.Max(0, sustainedMovementScore - 0.36);

            // Spikiness penalty: maps dominated by a few hard objects are overrated
            double consistencyBonus = 1 + 0.20 * sustainedRatio * Math.Max(0, 3.5 - spikiness) / 3.5;
            double spikyPenalty = 1.0 / (1.0 + 0.020 * Math.Max(0, spikiness - 3.9));

            // Blend harmonic peak difficulty with total difficulty mass
            double blendedMovement = 0.95 * movement + 0.05 * difficultyMass;

            double baseStarRating = Math.Sqrt(blendedMovement) * difficulty_multiplier
                                    * (1 + 0.060 * Math.Sqrt(precisionPatterns))
                                    * sustainedModifier
                                    * (1 + 0.08 * normalizedEntropy)
                                    * smsPenalty
                                    * spikyPenalty
                                    * consistencyBonus;
            double calibratedStarRating = star_rating_offset + star_rating_scale * baseStarRating
                                          + mid_star_rating_scale * Math.Max(0, baseStarRating - mid_star_rating_threshold)
                                          + high_star_rating_scale * Math.Max(0, baseStarRating - high_star_rating_threshold);
            double postCalibratedStarRating = post_star_rating_offset + post_star_rating_scale * calibratedStarRating
                                              + post_mid_star_rating_scale * Math.Max(0, calibratedStarRating - post_mid_star_rating_threshold)
                                              + post_high_star_rating_scale * Math.Max(0, calibratedStarRating - post_high_star_rating_threshold);

            CatchDifficultyAttributes attributes = new CatchDifficultyAttributes
            {
                StarRating = final_star_rating_offset + final_star_rating_scale * postCalibratedStarRating
                             + final_mid_star_rating_scale * Math.Max(0, postCalibratedStarRating - final_mid_star_rating_threshold)
                             + final_high_star_rating_scale * Math.Max(0, postCalibratedStarRating - final_high_star_rating_threshold),
                Mods = mods,
                MaxCombo = beatmap.GetMaxCombo(),
            };

            return attributes;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            CatchHitObject? lastObject = null;

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();

            double clockRate = ModUtils.CalculateRateWithMods(mods);

            float halfCatcherWidth = Catcher.CalculateCatchWidth(beatmap.Difficulty) * 0.5f;

            // For circle sizes above 5.5, reduce the catcher width further to simulate imperfect gameplay.
            halfCatcherWidth *= 1 - (Math.Max(0, beatmap.Difficulty.CircleSize - 5.5f) * 0.0625f);

            // In 2B beatmaps, it is possible that a normal Fruit is placed in the middle of a JuiceStream.
            foreach (var hitObject in CatchBeatmap.GetPalpableObjects(beatmap.HitObjects))
            {
                // We want to only consider fruits that contribute to the combo.
                if (hitObject is Banana || hitObject is TinyDroplet)
                    continue;

                if (lastObject != null)
                    objects.Add(new CatchDifficultyHitObject(hitObject, lastObject, clockRate, halfCatcherWidth, objects, objects.Count));

                lastObject = hitObject;
            }

            return objects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            return new Skill[]
            {
                new HarmonicMovement(mods),
                new PrecisionPatterns(mods),
            };
        }

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new CatchModDoubleTime(),
            new CatchModHalfTime(),
            new CatchModHardRock(),
            new CatchModEasy(),
        };
    }
}
