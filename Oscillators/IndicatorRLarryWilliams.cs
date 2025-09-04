using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace CCI_BBANDS_Strategy.Oscillators;

public sealed class IndicatorRLarryWilliams : Indicator, IWatchlistIndicator
{
    // Property to expose the latest %R value
    public double RlwValue { get; private set; }

    // List to store historical %R values
    private List<double> rlwHistory = new List<double>();

    [InputParameter("Period", 0, 1, 999, 1, 0)]
    public int Period = 14;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"RLW ({Period})";

    public IndicatorRLarryWilliams()
        : base()
    {
        this.Name = "%R Larry Williams";
        this.Description = "Uses Stochastic to determine overbought and oversold levels";

        this.AddLineSeries("RLW Line", Color.Blue, 1, LineStyle.Solid);
        this.AddLineLevel(-20, "Upper Limit", Color.Red, 1, LineStyle.Solid);
        this.AddLineLevel(-80, "Lower Limit", Color.Red, 1, LineStyle.Solid);

        this.SeparateWindow = true;
    }

    /// <param name="args">Provides data of updating reason and incoming price.</param>
    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count < Period)
            return;

        double highestPrice = Enumerable.Range(0, Period).Select(i => this.GetPrice(PriceType.High, i)).Max();
        double lowestPrice = Enumerable.Range(0, Period).Select(i => this.GetPrice(PriceType.Low, i)).Min();

        if (highestPrice - lowestPrice > 1e-7)
        {
            // Calculate the %R value and set it to both the property and the line series for chart display
            RlwValue = -100 * (highestPrice - this.GetPrice(PriceType.Close)) / (highestPrice - lowestPrice);
            this.SetValue(RlwValue);

            // Store the %R value in the historical buffer
            rlwHistory.Insert(0, RlwValue);

            // Limit the historical buffer size (optional)
            if (rlwHistory.Count > 100)
                rlwHistory.RemoveAt(100);
        }
    }

    // Method to retrieve a historical %R value
    public double GetRlwValue(int periodsAgo)
    {
        return (periodsAgo >= 0 && periodsAgo < rlwHistory.Count) ? rlwHistory[periodsAgo] : double.NaN;
    }
}
