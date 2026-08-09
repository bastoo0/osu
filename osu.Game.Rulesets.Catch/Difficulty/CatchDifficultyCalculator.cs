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
        // Calibrated after ordering was frozen to preserve the baseline median SR.
        private const double difficulty_multiplier = 6.07;
        private const double low_ar_reading_bonus = 0.23;
        private const double maximum_reading_bonus = 0.5;
        private const double star_rating_power = 1.65;
        private const double star_rating_scale = 0.20400;

        public override int Version => 20260811;

        public CatchDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0)
                return new CatchDifficultyAttributes { Mods = mods };

            double lowArReading = Math.Sqrt(Math.Max(0, 9.25 - beatmap.Difficulty.ApproachRate));
            double readingScale = Math.Exp(Math.Min(maximum_reading_bonus, low_ar_reading_bonus * lowArReading));
            double rawRating = Math.Sqrt(skills.OfType<Movement>().Single().DifficultyValue()) * readingScale * difficulty_multiplier;

            CatchDifficultyAttributes attributes = new CatchDifficultyAttributes
            {
                // Timing compression and hyperdash saturation reduce the raw top-end range.
                // Apply one monotonic display curve so the 5th, median and 95th percentiles remain
                // in the established SR range without changing any map ordering.
                StarRating = Math.Pow(rawRating, star_rating_power) * star_rating_scale,
                Mods = mods,
                MaxCombo = beatmap.GetMaxCombo(),
            };

            return attributes;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            PalpableCatchHitObject? lastObject = null;
            var pathObjects = new List<TinyDroplet>();

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>(beatmap.HitObjects.Count);

            double clockRate = ModUtils.CalculateRateWithMods(mods);

            float halfCatcherWidth = Catcher.CalculateCatchWidth(beatmap.Difficulty) * 0.5f;

            // For circle sizes above 5.5, reduce the catcher width further to simulate imperfect gameplay.
            halfCatcherWidth *= 1 - (Math.Max(0, beatmap.Difficulty.CircleSize - 5.5f) * 0.0625f);

            // In 2B beatmaps, it is possible that a normal Fruit is placed in the middle of a JuiceStream.
            foreach (var hitObject in CatchBeatmap.GetPalpableObjects(beatmap.HitObjects))
            {
                if (hitObject is Banana)
                    continue;

                // Tiny droplets do not receive their own strain peaks, but constrain the slider
                // path between adjacent combo objects.
                if (hitObject is TinyDroplet tinyDroplet)
                {
                    if (lastObject != null)
                        pathObjects.Add(tinyDroplet);

                    continue;
                }

                if (lastObject != null)
                    objects.Add(new CatchDifficultyHitObject(hitObject, lastObject, clockRate, halfCatcherWidth, objects, objects.Count, pathObjects));

                lastObject = hitObject;
                pathObjects.Clear();
            }

            return objects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            return new Skill[]
            {
                new Movement(mods),
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
