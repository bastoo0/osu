// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Catch.Difficulty.Evaluators
{
    public static class DensityEvaluator
    {
        private const int lookback_count = 5;

        /// <summary>
        ///  Evaluates the density for a specific object.
        ///  We are using a sliding window of 5 previous objects to compute a smoothed density value for each object.
        /// </summary>
        public static double ComputeObjectDensity(DifficultyHitObject current, List<DifficultyHitObject> allObjects, int currentIndex)
        {
            var catchCurrent = (CatchDifficultyHitObject)current;

            // Get previous n objects
            var previousObjects = getPreviousObjects(allObjects, currentIndex, lookback_count);

            if (previousObjects.Count == 0)
                return 0;

            double totalWeight = 0;

            // Calculate weighted density based on time differences
            for (int i = 0; i < previousObjects.Count; i++)
            {
                double timeDiff = Math.Abs(catchCurrent.StartTime - previousObjects[i].StartTime);

                // Using exponential decay to ensure weights are between 0 and 1
                double weight = Math.Exp(-timeDiff / 1000.0); // Divide by 1000 to convert ms to sec
                totalWeight += weight;
            }

            // For the first objects, previousObjects.Count < lookback_count so we have to divide by the
            // actual number of objects we found to ensure early objects in the beatmap are treated fairly
            return totalWeight / previousObjects.Count;
        }

        // Gets the previous n objects before the current index
        private static List<DifficultyHitObject> getPreviousObjects(List<DifficultyHitObject> allObjects, int currentIndex, int count)
        {
            var result = new List<DifficultyHitObject>();

            // Get previous objects
            for (int i = Math.Max(0, currentIndex - count); i < currentIndex; i++)
            {
                result.Add(allObjects[i]);
            }

            return result;
        }

        /// <summary>
        ///  Evaluates the smoothed density for all objects in a beatmap.
        ///  We are using a sliding window of 5 previous objects to compute a smoothed density value for each object.
        /// </summary>
        public static List<double> ComputeBeatmapDensities(List<DifficultyHitObject> beatmapObjects)
        {
            var densities = new List<double>();

            for (int i = 0; i < beatmapObjects.Count; i++)
            {
                double density = ComputeObjectDensity(beatmapObjects[i], beatmapObjects, i);
                densities.Add(density);
            }

            return densities;
        }
    }
}
