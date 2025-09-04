using System;
using TradingPlatform.BusinessLayer;

namespace CCI_BBANDS_Strategy.Volume;

public class IndicatorCumulativeDelta : Indicator
{
    public double CumulativeDelta { get; private set; } // Exposes the calculated cumulative delta

    public IndicatorCumulativeDelta()
    {
        this.Name = "Cumulative Delta (Calculation Only)";
        this.Description = "Cumulative Delta calculation for use in other indicators.";
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        // Ensure there are enough bars for calculation
        if (this.Count < 2)
            return;

        // Access the current volume analysis data
        var currentVolumeData = this.GetVolumeAnalysisData(0);

        // Safeguard against null volume data
        if (currentVolumeData?.Total == null)
            return;

        // Calculate cumulative delta by summing the delta values
        CumulativeDelta += currentVolumeData.Total.Delta;

        // Set the cumulative delta value for external use
        this.SetValue(CumulativeDelta);
    }

    protected override void OnClear()
    {
        // Reset cumulative delta when data is cleared
        CumulativeDelta = 0.0;
        base.OnClear();
    }
}
