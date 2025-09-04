using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace CCI_BBANDS_Strategy.VolatilityIndicators;

public class IndicatorTrueRange : Indicator
{
    public IndicatorTrueRange()
        : base()
    {
        // Defines indicator's name and description.
        Name = "True Range";

        // Defines line on demand with particular parameters.
        AddLineSeries("TR", Color.CadetBlue, 1, LineStyle.Solid);

        SeparateWindow = true;
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        double tr = CalculateTrueRange();

        SetValue(tr);
    }

    public double CalculateTrueRange(int offset = 0)
    {
        double hi = GetPrice(PriceType.High, offset);
        double lo = GetPrice(PriceType.Low, offset);

        double prevClose = Count <= offset + 1 ? Close(offset)
                                                 : Close(offset + 1);

        return Math.Max(hi - lo, Math.Max(Math.Abs(prevClose - hi), Math.Abs(prevClose - lo)));
    }
}