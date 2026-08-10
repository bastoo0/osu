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
        private const double difficulty_multiplier = 4.80;
        private const double low_ar_reading_bonus = 0.24;
        private const double maximum_reading_bonus = 0.5;
        private const double control_skill_weight = 0.4;
        private const double dash_state_saturation = 0.05;

        public override int Version => 20260815;

        public CatchDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0)
                return new CatchDifficultyAttributes { Mods = mods };

            Movement movement = skills.OfType<Movement>().Single();
            Control control = skills.OfType<Control>().Single();
            double lowArReading = Math.Sqrt(Math.Max(0, 9.25 - beatmap.Difficulty.ApproachRate));
            double readingScale = Math.Exp(Math.Min(
                maximum_reading_bonus,
                low_ar_reading_bonus * lowArReading));
            double combinedDifficulty = movement.DifficultyValue() + control_skill_weight * control.DifficultyValue();
            double staminaScale = Math.Clamp(
                Math.Pow(Math.Max(1, movement.ActiveDuration / 1000) / 110, 0.061),
                0.85,
                1.15);
            double sustainedScale = Math.Exp(0.28 * (movement.SustainedStrainRatio - 0.35));
            double fruitScale = Math.Exp(0.25 * (movement.FruitRatio - 0.9));
            double dashStateScale = Math.Exp(-dash_state_saturation * movement.DashStateChangeShare);

            CatchDifficultyAttributes attributes = new CatchDifficultyAttributes
            {
                StarRating = Math.Sqrt(combinedDifficulty) * difficulty_multiplier
                             * readingScale * staminaScale * sustainedScale * fruitScale * dashStateScale,
                Mods = mods,
                MaxCombo = beatmap.GetMaxCombo(),
            };

            return attributes;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            CatchHitObject? lastObject = null;

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>(beatmap.HitObjects.Count);

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
                new Movement(mods),
                new Control(mods),
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
