using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace CCI_BBANDS_Strategy.Channels
{
    public sealed class IndicatorBollingerBands : Indicator, IWatchlistIndicator
    {
        // Properties to expose Bollinger Bands values for external access
        public double BollingerUpper { get; private set; }  // Upper band
        public double BollingerMiddle { get; private set; } // Middle band (moving average)
        public double BollingerLower { get; private set; }  // Lower band

        // Lists to store historical Bollinger Bands values
        private List<double> upperHistory = new List<double>();
        private List<double> middleHistory = new List<double>();
        private List<double> lowerHistory = new List<double>();

        [InputParameter("Period of MA for envelopes", 0, 1, 999)]
        public int Period = 20;

        [InputParameter("Value of confidence interval", 1, 0.01, 100.0, 0.01, 2)]
        public double D = 2.0;

        [InputParameter("Sources prices for MA", 2, variants: new object[] {
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
        public PriceType SourcePrices = PriceType.Close;

        [InputParameter("Type of moving average", 3, variants: new object[]{
            "Simple Moving Average", MaMode.SMA,
            "Exponential Moving Average", MaMode.EMA,
            "Smoothed Moving Average", MaMode.SMMA,
            "Linearly Weighted Moving Average", MaMode.LWMA,
        })]
        public MaMode MaType = MaMode.SMA;

        [InputParameter("Calculation type", 4, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = DEFAULT_CALCULATION_TYPE;

        public int MinHistoryDepths => Period;
        public override string ShortName => $"BB ({Period}:{D})";

        private Indicator ma;

        public IndicatorBollingerBands()
            : base()
        {
            Name = "Bollinger Bands";
            Description = "Provides a relative definition of high and low based on standard deviation and a simple moving average";

            AddLineSeries("Upper Band", Color.Red, 2, LineStyle.Solid);
            AddLineSeries("Middle Band", Color.Gray, 2, LineStyle.Solid);
            AddLineSeries("Lower Band", Color.Green, 2, LineStyle.Solid);

            SeparateWindow = false;
        }

        protected override void OnInit()
        {
            ma = Core.Indicators.BuiltIn.MA(Period, SourcePrices, MaType, CalculationType);
            AddIndicator(ma);
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            if (Count < MinHistoryDepths)
                return;

            // Calculate moving average (middle band)
            double maValue = ma.GetValue();

            // Calculate standard deviation
            double sum = 0.0;
            for (int i = 0; i < Period; i++)
                sum += Math.Pow(GetPrice(SourcePrices, i) - maValue, 2);

            double deviation = D * Math.Sqrt(sum / Period);

            // Update the Bollinger Bands properties
            BollingerUpper = maValue + deviation;  // Upper band
            BollingerMiddle = maValue;             // Middle band
            BollingerLower = maValue - deviation;  // Lower band

            // Set values for chart display
            SetValue(BollingerUpper, 0);
            SetValue(BollingerMiddle, 1);
            SetValue(BollingerLower, 2);

            // Store historical values
            upperHistory.Insert(0, BollingerUpper);
            middleHistory.Insert(0, BollingerMiddle);
            lowerHistory.Insert(0, BollingerLower);

            // Limit the historical buffer size (optional)
            if (upperHistory.Count > 100)
                upperHistory.RemoveAt(100);
            if (middleHistory.Count > 100)
                middleHistory.RemoveAt(100);
            if (lowerHistory.Count > 100)
                lowerHistory.RemoveAt(100);
        }

        // Methods to retrieve historical Bollinger Bands values
        public double GetBollingerUpper(int periodsAgo)
        {
            return periodsAgo >= 0 && periodsAgo < upperHistory.Count ? upperHistory[periodsAgo] : double.NaN;
        }

        public double GetBollingerMiddle(int periodsAgo)
        {
            return periodsAgo >= 0 && periodsAgo < middleHistory.Count ? middleHistory[periodsAgo] : double.NaN;
        }

        public double GetBollingerLower(int periodsAgo)
        {
            return periodsAgo >= 0 && periodsAgo < lowerHistory.Count ? lowerHistory[periodsAgo] : double.NaN;
        }
    }
}
