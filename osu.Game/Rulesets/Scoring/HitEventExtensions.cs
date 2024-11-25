﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Scoring
{
    public static class HitEventExtensions
    {
        /// <summary>
        /// Calculates the "unstable rate" for a sequence of <see cref="HitEvent"/>s.
        /// </summary>
        /// <remarks>
        /// Uses <a href="https://en.wikipedia.org/wiki/Algorithms_for_calculating_variance#Welford's_online_algorithm">Welford's online algorithm</a>.
        /// </remarks>
        /// <returns>
        /// A non-null <see langword="double"/> value if unstable rate could be calculated,
        /// and <see langword="null"/> if unstable rate cannot be calculated due to <paramref name="hitEvents"/> being empty.
        /// </returns>
        public static UnstableRateCalculationResult? CalculateUnstableRate(this IReadOnlyList<HitEvent> hitEvents, UnstableRateCalculationResult? result = null)
        {
            Debug.Assert(hitEvents.All(ev => ev.GameplayRate != null));

            result ??= new UnstableRateCalculationResult();

            // Handle rewinding in the simplest way possible.
            if (hitEvents.Count < result.NextProcessableIndex + 1)
                result = new UnstableRateCalculationResult();

            for (int i = result.NextProcessableIndex; i < hitEvents.Count; i++)
            {
                HitEvent e = hitEvents[i];

                if (!AffectsUnstableRate(e))
                    continue;

                result.NextProcessableIndex++;

                // Division by gameplay rate is to account for TimeOffset scaling with gameplay rate.
                double currentValue = e.TimeOffset / e.GameplayRate!.Value;
                double nextMean = result.Mean + (currentValue - result.Mean) / result.NextProcessableIndex;
                result.SumOfSquares += (currentValue - result.Mean) * (currentValue - nextMean);
                result.Mean = nextMean;
            }

            if (result.NextProcessableIndex == 0)
                return null;

            return result;
        }

        /// <summary>
        /// Calculates the average hit offset/error for a sequence of <see cref="HitEvent"/>s, where negative numbers mean the user hit too early on average.
        /// </summary>
        /// <returns>
        /// A non-null <see langword="double"/> value if unstable rate could be calculated,
        /// and <see langword="null"/> if unstable rate cannot be calculated due to <paramref name="hitEvents"/> being empty.
        /// </returns>
        public static double? CalculateAverageHitError(this IEnumerable<HitEvent> hitEvents)
        {
            double[] timeOffsets = hitEvents.Where(AffectsUnstableRate).Select(ev => ev.TimeOffset).ToArray();

            if (timeOffsets.Length == 0)
                return null;

            return timeOffsets.Average();
        }

        public static bool AffectsUnstableRate(HitEvent e) => AffectsUnstableRate(e.HitObject, e.Result);
        public static bool AffectsUnstableRate(HitObject hitObject, HitResult result) => hitObject.HitWindows != HitWindows.Empty && result.IsHit();

        public class UnstableRateCalculationResult
        {
            public int NextProcessableIndex;
            public double SumOfSquares;
            public double Mean;

            public double Result => 10.0 * Math.Sqrt(SumOfSquares / NextProcessableIndex);
        }
    }
}
