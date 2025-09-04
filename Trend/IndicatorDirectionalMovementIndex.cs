using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace CCI_BBANDS_Strategy.Trend;

public sealed class IndicatorDirectionalMovementIndex : Indicator, IWatchlistIndicator
{
    // Displays Input Parameter as input field (or checkbox if value type is boolean).
    [InputParameter("Period of Moving Average", 0, 1, 999, 1, 0)]
    public int Period = 14;

    // Displays Input Parameter as drop down list.
    [InputParameter("Type of Moving Average", 1, variants: new object[] {
        "Simple", MaMode.SMA,
        "Exponential", MaMode.EMA,
        "Modified", MaMode.SMMA,
        "Linear Weighted", MaMode.LWMA}
     )]
    public MaMode MAType = MaMode.SMA;
    //
    [InputParameter("Calculation type", 5, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    public int MinHistoryDepths => Period * 2;
    public override string ShortName => $"DMI ({Period}:{MAType})";
  
    private Indicator atr;
    private Indicator firstMA;
    private Indicator secondMA;

    private HistoricalDataCustom firstMaHD;
    private HistoricalDataCustom secondMaHD;

    private double plusDM;
    private double minusDM;

    
    public IndicatorDirectionalMovementIndex()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "Directional Movement Index";
        this.Description = "Identifies whether there is a definable trend in the market.";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("Plus", Color.DodgerBlue, 1, LineStyle.Solid);
        this.AddLineSeries("Minus", Color.Red, 1, LineStyle.Solid);
        this.SeparateWindow = true;
    }

    
    protected override void OnInit()
    {
        // Get ATR and two MA indicators from built-in indicator collection.
        atr = Core.Indicators.BuiltIn.ATR(Period, MAType, CalculationType);
        firstMA = Core.Indicators.BuiltIn.MA(Period, PriceType.Close, MAType, CalculationType);
        secondMA = Core.Indicators.BuiltIn.MA(Period, PriceType.Close, MAType, CalculationType);

        // Create a custom HistoricalData and synchronize it with 'this' (DMI) indicator.
        firstMaHD = new HistoricalDataCustom(this);
        secondMaHD = new HistoricalDataCustom(this);

        // Add auxiliary ATR indicator to the current one.
        this.AddIndicator(atr);

        // Attach MA indicators to custom HistoricalData.
        firstMaHD.AddIndicator(firstMA);
        secondMaHD.AddIndicator(secondMA);
    }

    /// <param name="args">Provides data of updating reason and incoming price.</param>
    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count < Period)
            return;

        // Get an ATR value.
        double smoothedTR = atr.GetValue();

        if (double.IsNaN(smoothedTR) || smoothedTR == 0d)
        {
            plusDM = 0D;
            minusDM = 0D;
        }
        else
        {
            plusDM = this.GetPrice(PriceType.High) - this.GetPrice(PriceType.High, 1);
            if (plusDM < 0.0)
                plusDM = 0.0;
            else
                plusDM *= 100D / smoothedTR;

            minusDM = this.GetPrice(PriceType.Low, 1) - this.GetPrice(PriceType.Low);
            if (minusDM < 0.0)
                minusDM = 0.0;
            else
                minusDM *= 100D / smoothedTR;

            if (plusDM > minusDM)
                minusDM = 0.0;
            else
                plusDM = 0.0;
        }

         
        firstMaHD[PriceType.Close, 0] = plusDM;
        secondMaHD[PriceType.Close, 0] = minusDM;

        // Skip some period for correct calculation.
        if (this.Count < MinHistoryDepths)
            return;

        // Get values from MA indicators.
        double plus = firstMA.GetValue();
        double minus = secondMA.GetValue();

        // Set values to "Plus" and "Minus" line buffers.
        this.SetValue(plus, 0);
        this.SetValue(minus, 1);
    }
}