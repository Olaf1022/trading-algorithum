﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Will generate time steps around the desired <see cref="ITimeProvider"/>
    /// Provided step evaluator should return true when the next time step
    /// is valid and time can advance
    /// </summary>
    public class StepTimeProvider : ITimeProvider
    {
        private readonly ITimeProvider _underlyingTimeProvider;
        private readonly Func<DateTime, bool> _customStepEvaluator;
        private DateTime _currentUtc;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="underlyingTimeProvider">The timer provider instance to wrap</param>
        /// <param name="customStepEvaluator">Function to evaluate whether or not
        /// to advance time. Should return true if provided <see cref="DateTime"/> is a
        /// valid new next time. False will avoid time advancing</param>
        public StepTimeProvider(ITimeProvider underlyingTimeProvider,
            Func<DateTime, bool> customStepEvaluator)
        {
            _underlyingTimeProvider = underlyingTimeProvider;
            _currentUtc = _underlyingTimeProvider.GetUtcNow();
            _customStepEvaluator = customStepEvaluator;
        }

        /// <summary>
        /// Gets the current utc time step
        /// </summary>
        public DateTime GetUtcNow()
        {
            var utcNow = _underlyingTimeProvider.GetUtcNow();

            // we check if we should advance time based on the provided custom step evaluator
            if (_customStepEvaluator(utcNow))
            {
                _currentUtc = utcNow;
            }
            return _currentUtc;
        }
    }
}
