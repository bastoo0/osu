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
        private const double star_rating_offset = 0.5467483270377931;
        private const double star_rating_scale = 1.1969888491840726;
        private const double mid_star_rating_threshold = 1.3026447699860508;
        private const double mid_star_rating_scale = -0.5914145570534111;
        private const double high_star_rating_threshold = 2.079508421306436;
        private const double high_star_rating_scale = 0.8085636238671289;
        private const double post_star_rating_offset = -0.01858668603080208;
        private const double post_star_rating_scale = 2.732209554408997;
        private const double post_mid_star_rating_threshold = 3.4564218339274353;
        private const double post_mid_star_rating_scale = -0.42889484625025753;
        private const double post_high_star_rating_threshold = 17.710486190491544;
        private const double post_high_star_rating_scale = -0.03463517053695001;
        private const double final_star_rating_offset = -0.0003881871775509096;
        private const double final_star_rating_scale = 0.7339936105205869;
        private const double final_mid_star_rating_threshold = 7.12798252996874;
        private const double final_mid_star_rating_scale = 0.17382650167160202;
        private const double final_high_star_rating_threshold = 11.055201986167205;
        private const double final_high_star_rating_scale = -0.7068820225170358;

        public override int Version => 2026041205;

        public CatchDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0)
                return new CatchDifficultyAttributes { Mods = mods };

            double baseStarRating = Math.Sqrt(skills.OfType<HarmonicMovement>().Single().DifficultyValue()) * difficulty_multiplier;
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
