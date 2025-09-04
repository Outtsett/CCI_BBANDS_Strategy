using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace CCI_BBANDS_Strategy.Oscillators;

public sealed class IndicatorCommodityChannelIndex : Indicator, IWatchlistIndicator
{
    // Property to expose the CCI value for external access
    private List<double> cciHistory = new List<double>();
    // Property to expose the CCI value for external access
    public double CciValue { get; private set; }

    [InputParameter("Period", 10, 1, 999, 1, 0)]
    public int Period = 14;

    [InputParameter("Sources prices for MA", 20, variants: new object[] {
         "Close", PriceType.Close,
         "Open", PriceType.Open,
         "High", PriceType.High,
         "Low", PriceType.Low,
         "Typical", PriceType.Typical,
         "Medium", PriceType.Median,
         "Weighted", PriceType.Weighted,
         "Volume", PriceType.Volume,
         "Open interest", PriceType.OpenInterest
    })]
    public PriceType SourcePrice = PriceType.Close;

    [InputParameter("Type of Moving Average", 30, variants: new object[] {
         "Simple", MaMode.SMA,
         "Exponential", MaMode.EMA,
         "Modified", MaMode.SMMA,
         "Linear Weighted", MaMode.LWMA }
    )]
    public MaMode MAType = MaMode.SMA;

    [InputParameter("Calculation type", 40, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    public int MinHistoryDepths => Period;
    public override string ShortName => $"CCI ({Period}: {SourcePrice})";

    private Indicator MA;

    public IndicatorCommodityChannelIndex()
        : base()
    {
        this.Name = "Commodity Channel Index";
        this.Description = "Measures the position of price in relation to its moving average";

        this.AddLineSeries("CCI Line", Color.Red, 1, LineStyle.Solid);
        this.AddLineLevel(150, "150", Color.Gray, 1, LineStyle.Solid);
        this.AddLineLevel(100, "100", Color.Gray, 1, LineStyle.Solid);
        this.AddLineLevel(0, "0", Color.Gray, 1, LineStyle.Solid);
        this.AddLineLevel(-100, "-100", Color.Gray, 1, LineStyle.Solid);
        this.AddLineLevel(-150, "-150", Color.Gray, 1, LineStyle.Solid);
        this.SeparateWindow = true;
    }

    protected override void OnInit()
    {
        MA = Core.Indicators.BuiltIn.MA(Period, SourcePrice, MAType, CalculationType);
        this.AddIndicator(MA);
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count < MinHistoryDepths)
            return;

        double maValue = MA.GetValue();
        double deviation = 0;

        // Calculate mean deviation
        for (int i = 0; i < Period; i++)
            deviation += Math.Abs(this.GetPrice(SourcePrice, i) - maValue);

        deviation = 0.015 * (deviation / Period);

        // Calculate the CCI and set it
        CciValue = (this.GetPrice(SourcePrice) - maValue) / deviation;
        this.SetValue(CciValue); // Set CCI value for chart display

        // Store the CCI value in the historical buffer
        cciHistory.Insert(0, CciValue);

        // Limit the historical buffer size (optional)
        if (cciHistory.Count > 100)
            cciHistory.RemoveAt(100);
    }

    // Method to retrieve a historical CCI value
    public double GetCciValue(int periodsAgo)
    {
        return (periodsAgo >= 0 && periodsAgo < cciHistory.Count) ? cciHistory[periodsAgo] : double.NaN;
    }
}
