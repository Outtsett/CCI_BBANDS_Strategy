using System.Collections.Generic;
using System.Drawing;
using CCI_BBANDS_Strategy.VolatilityIndicators;
using TradingPlatform.BusinessLayer;

namespace CCI_BBANDS_Strategy.Volatility
{
    public sealed class IndicatorAverageTrueRange : Indicator, IWatchlistIndicator
    {
        // Property to expose the latest ATR value
        public double AtrValue { get; private set; }

        // List to store historical ATR values
        private List<double> atrHistory = new List<double>();

        [InputParameter("Period of Moving Average", 0, 1, 999, 1, 0)]
        public int Period = 13;

        [InputParameter("Type of Moving Average", 1, variants: new object[] {
             "Simple", MaMode.SMA,
             "Exponential", MaMode.EMA,
             "Smoothed", MaMode.SMMA,
             "Linear Weighted", MaMode.LWMA}
        )]
        public MaMode MAType = MaMode.SMA;

        [InputParameter("Calculation type", 5, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

        public int MinHistoryDepths => Period;
        public override string ShortName => $"ATR ({Period}: {MAType})";

        private Indicator ma;
        private IndicatorTrueRange tr;
        private HistoricalDataCustom customHD;

        public IndicatorAverageTrueRange()
            : base()
        {
            this.Name = "Average True Range";
            this.Description = "Measures of market volatility.";
            this.IsUpdateTypesSupported = false;

            this.AddLineSeries("ATR", Color.CadetBlue, 1, LineStyle.Solid);
            this.SeparateWindow = true;
        }

        protected override void OnInit()
        {
            ma = Core.Indicators.BuiltIn.MA(Period, PriceType.Close, MAType, CalculationType);
            ma.UpdateType = IndicatorUpdateType.OnTick;
            this.AddIndicator(tr = new IndicatorTrueRange() { UpdateType = IndicatorUpdateType.OnTick });

            customHD = new HistoricalDataCustom(this);
            customHD.AddIndicator(ma);
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            double trValue = this.tr.GetValue();
            customHD[PriceType.Close, 0] = trValue;

            if (this.Count < MinHistoryDepths)
                return;

            double maValue = ma.GetValue();
            this.SetValue(maValue);

            // Update the public AtrValue property
            this.AtrValue = maValue;

            // Store the ATR value in the historical buffer
            atrHistory.Insert(0, AtrValue);

            // Limit the historical buffer size (optional)
            if (atrHistory.Count > 100)
                atrHistory.RemoveAt(100);
        }

        // Method to retrieve a historical ATR value
        public double GetAtrValue(int periodsAgo)
        {
            return (periodsAgo >= 0 && periodsAgo < atrHistory.Count) ? atrHistory[periodsAgo] : double.NaN;
        }
    }
}
