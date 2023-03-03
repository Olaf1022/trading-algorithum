/*
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
using QuantConnect.Interfaces;
using QuantConnect.Algorithm.Framework.Alphas;
using QuantConnect.Algorithm.Framework.Alphas.Analysis;

namespace QuantConnect.Lean.Engine.Alphas
{
    /// <summary>
    /// Manages alpha charting responsibilities.
    /// </summary>
    public class ChartingInsightManagerExtension : IInsightManagerExtension
    {
        /// <summary>
        /// The string name used for the Alpha Assets chart
        /// </summary>
        public const string AlphaAssets = "Alpha Assets";
        private readonly bool _liveMode;

        private const int BacktestChartSamples = 1000;
        private DateTime _lastInsightCountSampleDateUtc;
        private int _dailyInsightCount;

        // Keep track, we only want to add the charts if the algorithm is producing insights
        private bool _chartsAdded;
        private IAlgorithm _algorithm;

        private readonly Chart _totalInsightCountChart = new Chart("Insight Count");
        private readonly Series _totalInsightCountSeries = new Series("Count", SeriesType.Bar, "#");

        /// <summary>
        /// Gets or sets the interval at which alpha charts are updated. This is in realtion to algorithm time.
        /// </summary>
        protected TimeSpan SampleInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Initializes a new instance of the <see cref="ChartingInsightManagerExtension"/> class
        /// </summary>
        /// <param name="algorithm">The algorithm instance. This is only used for adding the charts
        /// to the algorithm. We purposefully do not save a reference to avoid potentially inconsistent reads</param>
        /// <param name="statisticsManager">Statistics manager used to access mean population scores for charting</param>
        public ChartingInsightManagerExtension(IAlgorithm algorithm, StatisticsInsightManagerExtension statisticsManager)
        {
            _algorithm = algorithm;
            _liveMode = algorithm.LiveMode;

            // Add a series for insight count over sample period to the "Insight Count" chart
            _totalInsightCountChart.AddSeries(_totalInsightCountSeries);
        }

        /// <summary>
        /// Invokes the manager at the end of the time step.
        /// Samples and plots insight counts and population score.
        /// </summary>
        /// <param name="frontierTimeUtc">The current frontier time utc</param>
        public void Step(DateTime frontierTimeUtc)
        {
            // Only add our charts to the algorithm when we actually have an insight 
            // We will still update our internal charts anyways, but this keeps Alpha charts out of
            // algorithms that don't use the framework.
            if (!_chartsAdded && _dailyInsightCount > 0)
            {
                _algorithm.AddChart(_totalInsightCountChart);

                _chartsAdded = true;
            }

            // sample insight/symbol counts each utc day change
            if (frontierTimeUtc.Date > _lastInsightCountSampleDateUtc)
            {
                _lastInsightCountSampleDateUtc = frontierTimeUtc.Date;

                // add sum of daily insight counts to the total insight count series
                _totalInsightCountSeries.AddPoint(frontierTimeUtc.Date, _dailyInsightCount);

                // Resetting our storage
                _dailyInsightCount = 0;
            }
        }

        /// <summary>
        /// Invoked after <see cref="IAlgorithm.Initialize"/> has been called.
        /// Determines chart sample interval and initial sample times
        /// </summary>
        /// <remarks>
        /// While the algorithm instance is provided, it's highly recommended to not maintain
        /// a direct reference to it as there is no way to guarantee consistence reads.
        /// </remarks>
        /// <param name="algorithmStartDate">The start date of the algorithm</param>
        /// <param name="algorithmEndDate">The end date of the algorithm</param>
        /// <param name="algorithmUtcTime">The algorithm's current utc time</param>
        public void InitializeForRange(DateTime algorithmStartDate, DateTime algorithmEndDate, DateTime algorithmUtcTime)
        {
            if (_liveMode)
            {
                // live mode we'll sample each minute
                SampleInterval = Time.OneMinute;
            }
            else
            {
                // space out backtesting samples evenly
                var backtestPeriod = algorithmEndDate - algorithmStartDate;
                SampleInterval = TimeSpan.FromTicks(backtestPeriod.Ticks / BacktestChartSamples);
            }

            _lastInsightCountSampleDateUtc = algorithmUtcTime.RoundDown(Time.OneDay);
        }

        /// <summary>
        /// Handles the <see cref="IAlgorithm.InsightsGenerated"/> event.
        /// Keep daily and total count of insights by symbol
        /// </summary>
        /// <param name="context">The newly generated insight analysis context</param>
        public void OnInsightGenerated(InsightAnalysisContext context)
        {
            _dailyInsightCount++;
        }

        /// <summary>
        /// NOP - Charting is more concerned with population vs individual insights
        /// </summary>
        /// <param name="context">Context whose insight has just completed analysis</param>
        public void OnInsightClosed(InsightAnalysisContext context)
        {
        }

        /// <summary>
        /// NOP - Charting is more concerned with population vs individual insights
        /// </summary>
        /// <param name="context">Context whose insight has just completed analysis</param>
        public void OnInsightAnalysisCompleted(InsightAnalysisContext context)
        {
        }
    }
}
