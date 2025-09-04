using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace CCI_BBANDS_Strategy.Oscillators;

public sealed class IndicatorMovingAverageConvergenceDivergence : Indicator, IWatchlistIndicator
{
    // Properties to expose MACD, Signal, and OsMA values for external access
    public double MacdValue { get; private set; }
    public double SignalValue { get; private set; }
    public double OsmaValue { get; private set; }

    [InputParameter("Period of fast EMA", 0, 1, 999, 1, 0)]
    public int FastPeriod = 12;

    [InputParameter("Period of slow EMA", 1, 1, 999, 1, 0)]
    public int SlowPeriod = 26;

    [InputParameter("Period of signal SMA", 2, 1, 999, 1, 0)]
    public int SignalPeriod = 9;

    [InputParameter("Calculation type", 4, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    public int MinHistoryDepths => MaxEMAPeriod + SignalPeriod;
    public override string ShortName => $"MACD ({FastPeriod}: {SlowPeriod}: {SignalPeriod})";
   
    private int MaxEMAPeriod => Math.Max(FastPeriod, SlowPeriod);

    private Indicator fastEMA;
    private Indicator slowEMA;
    private Indicator sma;
    private HistoricalDataCustom customHD;

    public IndicatorMovingAverageConvergenceDivergence()
        : base()
    {
        this.Name = "Moving Average Convergence/Divergence";
        this.Description = "A trend-following momentum indicator that shows the relationship between two moving averages of prices";

        this.AddLineSeries("MACD", Color.DodgerBlue, 1, LineStyle.Solid);
        this.AddLineSeries("Signal", Color.Red, 1, LineStyle.Solid);
        this.AddLineSeries("OsMA", Color.Green, 4, LineStyle.Histogramm);

        this.SeparateWindow = true;
    }

    protected override void OnInit()
    {
        fastEMA = Core.Indicators.BuiltIn.EMA(FastPeriod, PriceType.Typical, CalculationType);
        slowEMA = Core.Indicators.BuiltIn.EMA(SlowPeriod, PriceType.Typical, CalculationType);
        sma = Core.Indicators.BuiltIn.SMA(SignalPeriod, PriceType.Close);

        customHD = new HistoricalDataCustom(this);
        customHD.AddIndicator(sma);

        this.AddIndicator(fastEMA);
        this.AddIndicator(slowEMA);
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count < MaxEMAPeriod)
            return;

        // Calculate MACD line
        double differ = fastEMA.GetValue() - slowEMA.GetValue();
        this.SetValue(differ); // MACD line buffer
        MacdValue = differ;    // Update MacdValue property

        customHD[PriceType.Close, 0] = differ;

        if (this.Count < MinHistoryDepths)
            return;

        // Calculate Signal line
        double signal = sma.GetValue();
        if (double.IsNaN(signal))
            return;

        this.SetValue(signal, 1); // Signal line buffer
        SignalValue = signal;     // Update SignalValue property

        // Calculate OsMA
        double osma = differ - signal;
        this.SetValue(osma, 2);   // OsMA line buffer
        OsmaValue = osma;         // Update OsmaValue property
    }
}
