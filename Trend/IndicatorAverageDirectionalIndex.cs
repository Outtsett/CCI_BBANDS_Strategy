using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace CCI_BBANDS_Strategy.Trend
{
    /// <summary>
    /// The Average Directional Index (ADX) determines the strength of a prevailing trend.
    /// </summary>
    public sealed class IndicatorAverageDirectionalIndex : Indicator, IWatchlistIndicator
    {
        // Properties to expose ADX, +DI, and -DI values to other parts of the strategy
        public double AdxValue { get; private set; }
        public double DiPlusValue { get; private set; }
        public double DiMinusValue { get; private set; }

        // Lists to store historical values
        private List<double> adxHistory = new List<double>();
        private List<double> diPlusHistory = new List<double>();
        private List<double> diMinusHistory = new List<double>();

        [InputParameter("Period", 0, 1, 999, 1, 0)]
        public int Period = 14;

        [InputParameter("Type of Moving Average", 1, variants: new object[] {
            "Simple", MaMode.SMA,
            "Exponential", MaMode.EMA,
            "Modified", MaMode.SMMA,
            "Linear Weighted", MaMode.LWMA}
        )]
        public MaMode MAType = MaMode.SMA;

        [InputParameter("Calculation type", 5, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

        public int MinHistoryDepths => Period * 2;
        public override string ShortName => $"ADX ({Period}: {MAType})";

        private HistoricalDataCustom customHDadx;
        private HistoricalDataCustom customHDplusDm;
        private HistoricalDataCustom customHDminusDm;

        private Indicator rawAtr;
        private Indicator adxMa;
        private Indicator plusMa;
        private Indicator minusMa;

        public IndicatorAverageDirectionalIndex()
            : base()
        {
            this.Name = "Average Directional Index";
            this.Description = "The ADX determines the strength of a prevailing trend.";

            this.AddLineSeries("ADX'Line", Color.Green, 1, LineStyle.Solid);
            this.AddLineSeries("+DI'Line", Color.Blue, 1, LineStyle.Solid);
            this.AddLineSeries("-DI'Line", Color.Red, 1, LineStyle.Solid);

            this.SeparateWindow = true;
        }

        protected override void OnInit()
        {
            customHDadx = new HistoricalDataCustom(this);
            customHDplusDm = new HistoricalDataCustom(this);
            customHDminusDm = new HistoricalDataCustom(this);

            adxMa = Core.Indicators.BuiltIn.MA(Period, PriceType.Close, MAType, CalculationType);
            plusMa = Core.Indicators.BuiltIn.MA(Period, PriceType.Close, MAType, CalculationType);
            minusMa = Core.Indicators.BuiltIn.MA(Period, PriceType.Close, MAType, CalculationType);

            customHDadx.AddIndicator(adxMa);
            customHDplusDm.AddIndicator(plusMa);
            customHDminusDm.AddIndicator(minusMa);

            rawAtr = Core.Indicators.BuiltIn.ATR(1, MAType, CalculationType);
            this.AddIndicator(rawAtr);
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            GetPlusMinus(out double plusDM, out double minusDM);

            customHDplusDm[PriceType.Close] = plusDM;
            customHDminusDm[PriceType.Close] = minusDM;

            if (this.Count < Period)
                return;

            double plusDI = plusMa.GetValue();
            double minusDI = minusMa.GetValue();

            double adx = plusDI != -minusDI ? 100 * Math.Abs(plusDI - minusDI) / (plusDI + minusDI) : 0D;
            customHDadx[PriceType.Close] = adx;

            if (this.Count < MinHistoryDepths)
                return;

            // Set values to display on chart
            this.SetValue(adxMa.GetValue());
            this.SetValue(plusDI, 1);
            this.SetValue(minusDI, 2);

            // Update public properties for use in OnTick or other strategy methods
            this.AdxValue = adxMa.GetValue();
            this.DiPlusValue = plusDI;
            this.DiMinusValue = minusDI;

            // Store values in historical buffers
            adxHistory.Insert(0, AdxValue);
            diPlusHistory.Insert(0, DiPlusValue);
            diMinusHistory.Insert(0, DiMinusValue);

            // Limit historical buffer size (optional)
            if (adxHistory.Count > 100)
                adxHistory.RemoveAt(100);
            if (diPlusHistory.Count > 100)
                diPlusHistory.RemoveAt(100);
            if (diMinusHistory.Count > 100)
                diMinusHistory.RemoveAt(100);
        }

        // Methods to retrieve historical values
        public double GetAdxValue(int periodsAgo)
        {
            return (periodsAgo >= 0 && periodsAgo < adxHistory.Count) ? adxHistory[periodsAgo] : double.NaN;
        }

        public double GetDiPlusValue(int periodsAgo)
        {
            return (periodsAgo >= 0 && periodsAgo < diPlusHistory.Count) ? diPlusHistory[periodsAgo] : double.NaN;
        }

        public double GetDiMinusValue(int periodsAgo)
        {
            return (periodsAgo >= 0 && periodsAgo < diMinusHistory.Count) ? diMinusHistory[periodsAgo] : double.NaN;
        }

        private void GetPlusMinus(out double plusDM, out double minusDM)
        {
            double rawATR = rawAtr.GetValue();

            if (this.Count < 2 || rawATR == 0)
            {
                plusDM = 0D;
                minusDM = 0D;
                return;
            }

            plusDM = this.High() - this.High(1);
            if (plusDM < 0.0)
                plusDM = 0.0;
            else
                plusDM *= 100D / rawATR;

            minusDM = this.Low(1) - this.Low();
            if (minusDM < 0.0)
                minusDM = 0.0;
            else
                minusDM *= 100D / rawATR;

            if (plusDM > minusDM)
                minusDM = 0.0;
            else
                plusDM = 0.0;
        }
    }
}
