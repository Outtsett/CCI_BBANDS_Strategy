using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace CCI_BBANDS_Strategy.Oscillators
{
    public sealed class IndicatorRelativeStrengthIndex : Indicator, IWatchlistIndicator
    {
        // Properties to expose RSI and MA values for external access
        public double RsiValue { get; private set; }   // Latest RSI value
        public double MaValue { get; private set; }    // Smoothed MA of RSI

        // Lists to store historical RSI and MA values
        private List<double> rsiHistory = new List<double>();
        private List<double> maHistory = new List<double>();

        [InputParameter("RSI period", 0, 1, 9999)]
        public int Period = 14;

        [InputParameter("Sources prices for the RSI line", 1, variants: new object[] {
             "Close", PriceType.Close,
             "Open", PriceType.Open,
        })]
        public PriceType SourcePrice = PriceType.Close;

        [InputParameter("Mode for the RSI line", 2, variants: new object[] {
             "Simple", RSIMode.Simple,
             "Exponential", RSIMode.Exponential}
        )]
        public RSIMode SourceRSI = RSIMode.Exponential;

        [InputParameter("Type of Moving Average", 3, variants: new object[] {
            "Simple", MaMode.SMA,
            "Exponential", MaMode.EMA,
            "Smoothed Modified", MaMode.SMMA,
            "Linear Weighted", MaMode.LWMA}
        )]
        public MaMode MaType = MaMode.SMA;

        [InputParameter("Calculation type", 4, variants: new object[]
        {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

        [InputParameter("Smoothing period", 5, 1, 9999)]
        public int MAPeriod = 5;

        public int MinHistoryDepths
        {
            get
            {
                int minHistoryDepths = Period + MAPeriod;
                if (CalculationType == IndicatorCalculationType.ByPeriod)
                    minHistoryDepths += CALCULATION_PERIOD;
                return minHistoryDepths;
            }
        }
        public override string ShortName => $"RSI ({Period}: {SourcePrice})";

        private Indicator ma;
        private HistoricalDataCustom histCustom;

        private double prevV = 0.0;
        private double prevP = 0.0;
        private double sumV = 0.0;
        private double sumP = 0.0;
        private const int CALCULATION_PERIOD = 500;

        public IndicatorRelativeStrengthIndex()
            : base()
        {
            this.Name = "Relative Strength Index";
            this.Description = "RSI is classified as a momentum oscillator, measuring the velocity and magnitude of directional price movements";

            this.AddLineSeries("RSI Line", Color.Green, 1, LineStyle.Solid);
            this.AddLineSeries("MA Line", Color.PowderBlue, 1, LineStyle.Solid);
            this.AddLineLevel(70, "Upper Limit", Color.Red, 1, LineStyle.Solid);
            this.AddLineLevel(30, "Lower Limit", Color.Blue, 1, LineStyle.Solid);
            this.AddLineLevel(50, "Middle Limit", Color.Gray, 1, LineStyle.Solid);

            this.SeparateWindow = true;
        }

        protected override void OnInit()
        {
            histCustom = new HistoricalDataCustom(this);
            ma = Core.Indicators.BuiltIn.MA(MAPeriod, SourcePrice, MaType, CalculationType);
            histCustom.AddIndicator(ma);
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            if (args.Reason != UpdateReason.NewTick)
            {
                prevV = sumV;
                prevP = sumP;
            }
            if (this.Count < Period || Period == 0)
                return;

            if (SourceRSI == RSIMode.Simple)
                CalcSimple();
            else
            {
                if (CalculationType == IndicatorCalculationType.ByPeriod && this.Count > CALCULATION_PERIOD + Period)
                    CalcExponByPeriod();
                else
                    CalcExpon();
            }

            // Set the RSI and MA values to properties for external access
            RsiValue = this.GetValue();       // Latest RSI value
            MaValue = ma.GetValue();          // Latest smoothed MA value
            histCustom.SetValue(0d, 0d, 0d, RsiValue);
            this.SetValue(MaValue, 1);

            // Store the RSI and MA values in historical buffers
            rsiHistory.Insert(0, RsiValue);
            maHistory.Insert(0, MaValue);

            // Limit the historical buffer size (optional)
            if (rsiHistory.Count > 100)
                rsiHistory.RemoveAt(100);
            if (maHistory.Count > 100)
                maHistory.RemoveAt(100);
        }

        protected override void OnClear()
        {
            prevP = default;
            prevV = default;
            sumP = default;
            sumV = default;
        }

        // Method to retrieve a historical RSI value
        public double GetRsiValue(int periodsAgo)
        {
            return (periodsAgo >= 0 && periodsAgo < rsiHistory.Count) ? rsiHistory[periodsAgo] : double.NaN;
        }

        // Method to retrieve a historical MA value
        public double GetMaValue(int periodsAgo)
        {
            return (periodsAgo >= 0 && periodsAgo < maHistory.Count) ? maHistory[periodsAgo] : double.NaN;
        }

        private void CalcSimple(int offset = 0, bool setValue = true)
        {
            sumV = 0D;
            sumP = 0D;

            for (int i = 0; i < Period; i++)
            {
                double diff = this.GetPrice(SourcePrice, i + offset) - this.GetPrice(SourcePrice, i + 1 + offset);
                if (double.IsNaN(diff))
                    continue;

                if (diff > 0D)
                    sumV += diff;
                else
                    sumP -= diff;
            }

            if (setValue)
            {
                double value = sumP != 0D ? 100D * (1.0 - 1.0 / (1.0 + sumV / sumP)) : 100D;
                this.SetValue(value);
            }
        }

        private void CalcExpon()
        {
            if (this.Count == Period + 1)
            {
                CalcSimple();
                prevV = sumV = sumV / Period;
                prevP = sumP = sumP / Period;
            }
            else
            {
                double diff = this.GetPrice(SourcePrice) - this.GetPrice(SourcePrice, 1);
                if (diff > 0D)
                {
                    sumV = (prevV * (Period - 1) + diff) / Period;
                    sumP = prevP * (Period - 1) / Period;
                }
                else
                {
                    sumV = prevV * (Period - 1) / Period;
                    sumP = (prevP * (Period - 1) - diff) / Period;
                }
            }
            double rsi = sumP != 0D ? 100D - 100D / (1.0 + sumV / sumP) : 0D;
            this.SetValue(rsi);
        }

        private void CalcExponByPeriod(int offset = 0)
        {
            int startOffset = offset + CALCULATION_PERIOD;

            if (this.Count <= startOffset + Period)
                return;

            CalcSimple(startOffset, setValue: false);
            prevV = sumV = sumV / Period;
            prevP = sumP = sumP / Period;

            for (int i = startOffset - 1; i >= offset; i--)
            {
                double diff = this.GetPrice(SourcePrice, i) - this.GetPrice(SourcePrice, i + 1);
                if (diff > 0D)
                {
                    sumV = (prevV * (Period - 1) + diff) / Period;
                    sumP = prevP * (Period - 1) / Period;
                }
                else
                {
                    sumV = prevV * (Period - 1) / Period;
                    sumP = (prevP * (Period - 1) - diff) / Period;
                }
            }

            double rsi = sumP != 0D ? 100D - 100D / (1.0 + sumV / sumP) : 0D;
            this.SetValue(rsi);
        }
    }
}
