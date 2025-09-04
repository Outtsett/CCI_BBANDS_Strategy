using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;
using System.Diagnostics.Metrics;
using CCI_BBANDS_Strategy.Trend;
using CCI_BBANDS_Strategy.Volatility;
using CCI_BBANDS_Strategy.Oscillators;
using CCI_BBANDS_Strategy.Channels;


namespace CCI_BBANDS_Strategy
{
    public class CCI_BBANDS_Strategy : Strategy, ICurrentAccount, ICurrentSymbol
    {
        // Trading Setup
        [InputParameter("Symbol", 0)]
        public Symbol CurrentSymbol { get; set; }

        [InputParameter("Account", 1)]
        public Account CurrentAccount { get; set; }

        [InputParameter("Quantity", 2, 1, 99999, 1, 2)]
        public double Quantity { get; set; } = 1;

        [InputParameter("Period", 3)]
        public Period Period { get; set; } = Period.MIN5;

        [InputParameter("Start point", 4)]
        public DateTime StartPoint { get; set; } = Core.TimeUtils.DateTimeUtcNow.AddMonths(-3);

        // MACD Settings
        [InputParameter("MACD Fast Period", 10, 1, 999, 1, 0)]
        public int MacdFastPeriod { get; set; } = 12;

        [InputParameter("MACD Slow Period", 11, 1, 999, 1, 0)]
        public int MacdSlowPeriod { get; set; } = 26;

        [InputParameter("MACD Signal Period", 12, 1, 999, 1, 0)]
        public int MacdSignalPeriod { get; set; } = 9;

        // OsMA Threshold
        [InputParameter("OsMA Threshold", 1, 0.0, 2.0, 0.05, 2)]
        public double OsmaThreshold { get; set; } = 0.25;

        // ATR Setting
        [InputParameter("ATR Period", 21, 1, 100, 1, 0)]
        public int AtrPeriod { get; set; } = 14;

        [InputParameter("ATR MA Type", 22, variants:
        [
           "Simple", MaMode.SMA,
           "Exponential", MaMode.EMA,
           "Smoothed", MaMode.SMMA,
           "Linear Weighted", MaMode.LWMA
        ])]
        public MaMode AtrMaType { get; set; } = MaMode.SMA;

        [InputParameter("ATR Stop-Loss Multiplier", 23)]
        public double AtrStopLossMultiplier { get; set; } = 1.50;

        [InputParameter("ATR Take-Profit Multiplier", 24)]
        public double AtrTakeProfitMultiplier { get; set; } = 0.50;

        // ADX Settings
        [InputParameter("ADX Period", 30, 1, 999, 1, 0)]
        public int AdxPeriod { get; set; } = 14;

        // Bollinger Bands Settings
        [InputParameter("Bollinger Bands Period", 40, 1, 999, 1, 0)]
        public int BollingerBandsPeriod { get; set; } = 20;

        [InputParameter("Bollinger Bands Deviation", 41, 0.01, 100.0, 0.01, 2)]
        public double BollingerBandsDeviation { get; set; } = 2;

        // Moving Average Settings for Bollinger Bands
        [InputParameter("Type of moving average", 42, variants: [
            "Simple Moving Average", MaMode.SMA,
            "Exponential Moving Average", MaMode.EMA,
            "Smoothed Moving Average", MaMode.SMMA,
            "Linearly Weighted Moving Average", MaMode.LWMA,
        ])]
        public MaMode BollingerBandsMaType { get; set; } = MaMode.SMA;

        [InputParameter("Calculation type", 4, variants: new object[] {
            "All available data", IndicatorCalculationType.AllAvailableData,
            "By period", IndicatorCalculationType.ByPeriod,
        })]
        public IndicatorCalculationType BollingerBandsCalcType { get; set; } = IndicatorCalculationType.ByPeriod;


        // CCI Settings
        [InputParameter("CCI Period", 50, 1, 999, 1, 0)]
        public int CciPeriod { get; set; } = 14;

        [InputParameter("CCI Source Price", 51, variants:
        [
           "Close", PriceType.Close,
           "Open", PriceType.Open,
        ])]
        public PriceType CciSourcePrice { get; set; } = PriceType.Close;

        [InputParameter("CCI Moving Average Type", 52, variants:
        [
           "Simple", MaMode.SMA,
           "Exponential", MaMode.EMA,
        ])]
        public MaMode CciMaType { get; set; } = MaMode.EMA;

        // DMI Settings
        [InputParameter("DMI Period", 60, 1, 999, 1, 0)]
        public int DmiPeriod { get; set; } = 14;

        [InputParameter("DMI MA Type", 61, variants:
        [
           "Simple", MaMode.SMA,
           "Exponential", MaMode.EMA,
        ])]
        public MaMode DmiMaType { get; set; } = MaMode.EMA;

        [InputParameter("DMI Calculation Type", 62, variants:
        [
           "All available data", IndicatorCalculationType.AllAvailableData,
           "By period", IndicatorCalculationType.ByPeriod
        ])]
        public IndicatorCalculationType DmiCalcType { get; set; } = IndicatorCalculationType.AllAvailableData;

        // Williams %R Settings
        [InputParameter("Williams %R Period", 70, 1, 999, 1, 0)]
        public int WilliamsRPeriod { get; set; } = 14;

        // RSI Settings
        [InputParameter("RSI Period", 80, 1, 999, 1, 0)]
        public int RsiPeriod { get; set; } = 14;

        [InputParameter("RSI Source Price", 81, variants:
        [
           "Close", PriceType.Close,
           "Open", PriceType.Open,
        ])]
        public PriceType RsiSourcePrice { get; set; } = PriceType.Close;

        [InputParameter("RSI Mode", 82, variants:
        [
           "Simple", RSIMode.Simple,
           "Exponential", RSIMode.Exponential
        ])]
        public RSIMode RsiMode { get; set; } = RSIMode.Exponential;
    

        public override string[] MonitoringConnectionsIds => [this.CurrentSymbol?.ConnectionId, this.CurrentAccount?.ConnectionId];

        private HistoricalData hdm;
        private IndicatorMovingAverageConvergenceDivergence indicatorMacd;
        private IndicatorAverageTrueRange indicatorAtr;
        private IndicatorAverageDirectionalIndex indicatorAdx;
        private IndicatorBollingerBands bollingerBands;
        private IndicatorCommodityChannelIndex indicatorCci;
        private IndicatorDirectionalMovementIndex indicatorDmi;
        private IndicatorRLarryWilliams indicatorWilliamsR;
        private IndicatorRelativeStrengthIndex indicatorRsi;
       

        private double totalNetPl = 0;
        private double currentBalance = 0;
        private double totalGrossProfit = 0;
        private double totalGrossLoss = 0;
        private double peakBalance = 0;
        private double totalGrossPl = 0;
        private double totalFee = 0;
        



        public CCI_BBANDS_Strategy()
          : base()
        {
            this.Name = "CCI_BBANDS_Strategy";
            this.Description = "CCI_BBANDS_Strategy";
            this.Period = Period.MIN5;
            this.StartPoint = Core.TimeUtils.DateTimeUtcNow.AddMonths(-3);
        }

        protected override void OnRun()
        {
            // Check if Symbol and Account are specified
            if (this.CurrentSymbol == null || this.CurrentAccount == null)
            {
                this.LogError("Incorrect input parameters... Symbol or Account not specified.");
                this.Stop();
                return;
            }

            // Check if Symbol and Account are from the same connection
            if (this.CurrentSymbol.ConnectionId != this.CurrentAccount.ConnectionId)
            {
                this.LogError("Incorrect input parameters... Symbol and Account from different connections.");
                this.Stop();
                return;
            }

            // Initialize indicators
            this.indicatorMacd = new IndicatorMovingAverageConvergenceDivergence
            {
                FastPeriod = this.MacdFastPeriod,
                SlowPeriod = this.MacdSlowPeriod,
                SignalPeriod = this.MacdSignalPeriod,
            };

            this.indicatorAdx = new IndicatorAverageDirectionalIndex
            {
                Period = this.AdxPeriod
            };

            this.indicatorCci = new IndicatorCommodityChannelIndex
            {
                Period = this.CciPeriod,
                SourcePrice = this.CciSourcePrice,
                MAType = this.CciMaType
            };

            // Initialize Bollinger Bands with the new settings
            this.bollingerBands = new IndicatorBollingerBands
            {
                Period = this.BollingerBandsPeriod,
                D = this.BollingerBandsDeviation,
                MaType = this.BollingerBandsMaType,  // Moving Average type
                CalculationType = this.BollingerBandsCalcType // Calculation type
            };

            this.indicatorAtr = new IndicatorAverageTrueRange
            {
                Period = this.AtrPeriod,
                MAType = this.AtrMaType
            };

            this.indicatorDmi = new IndicatorDirectionalMovementIndex
            {
                Period = this.DmiPeriod,
                MAType = this.DmiMaType,
                CalculationType = this.DmiCalcType
            };

            this.indicatorWilliamsR = new IndicatorRLarryWilliams
            {
                Period = this.WilliamsRPeriod
            };

            this.indicatorRsi = new IndicatorRelativeStrengthIndex
            {
                Period = this.RsiPeriod,
                SourcePrice = this.RsiSourcePrice,
                SourceRSI = this.RsiMode
            };

            
            // Set up historical data and attach indicators
            this.hdm = this.CurrentSymbol.GetHistory(this.Period, this.CurrentSymbol.HistoryType, this.StartPoint);

            // Adding indicators to historical data
            this.hdm.AddIndicator(this.indicatorMacd);
            this.hdm.AddIndicator(this.indicatorAdx);
            this.hdm.AddIndicator(this.indicatorCci);
            this.hdm.AddIndicator(this.bollingerBands);
            this.hdm.AddIndicator(this.indicatorAtr);
            this.hdm.AddIndicator(this.indicatorDmi);
            this.hdm.AddIndicator(this.indicatorWilliamsR);
            this.hdm.AddIndicator(this.indicatorRsi);

            // Ensure all indicators are initialized
            if (this.indicatorMacd == null || this.indicatorAdx == null || this.indicatorCci == null ||
                this.bollingerBands == null || this.indicatorAtr == null || this.indicatorDmi == null ||
                this.indicatorWilliamsR == null || this.indicatorRsi == null)
            {
                this.LogError("One or more indicators failed to initialize.");
                this.Stop();
                return;
            }

            // Log successful initialization
            this.Log("All indicators successfully initialized.", StrategyLoggingLevel.Trading);

            // Subscribe to necessary events
            Core.PositionAdded += this.Core_PositionAdded;
            Core.PositionRemoved += this.Core_PositionRemoved;
            Core.OrdersHistoryAdded += this.Core_OrdersHistoryAdded;
            this.hdm.HistoryItemUpdated += this.Hdm_HistoryItemUpdated;
            Core.TradeAdded += this.Core_TradeAdded;

            // Log strategy start
            this.Log("Strategy is now running.", StrategyLoggingLevel.Trading);
        }
        protected override void OnStop()
        {
            // Log that the strategy is stopping
            Log("Strategy is stopping. Unsubscribing from events and cleaning up resources.", StrategyLoggingLevel.Trading);

            // Unsubscribe from events
            Core.Instance.PositionAdded -= this.Core_PositionAdded;
            Core.Instance.PositionRemoved -= this.Core_PositionRemoved;
            this.hdm.HistoryItemUpdated -= this.Hdm_HistoryItemUpdated;
            Core.TradeAdded += this.Core_TradeAdded;
            // Close all active positions for the current symbol and account
            var activePositions = Core.Instance.Positions
                .Where(p => p.Symbol == this.CurrentSymbol && p.Account == this.CurrentAccount)
                .ToList();

            foreach (var position in activePositions)
            {
                var closeResult = position.Close();
                if (closeResult.Status == TradingOperationResultStatus.Success)
                {
                    Log($"Closed position successfully: Side={position.Side}, Quantity={position.Quantity}, OpenPrice={position.OpenPrice:F2}", StrategyLoggingLevel.Trading);
                }
                else
                {
                    Log($"Failed to close position: Side={position.Side}. Error: {closeResult.Message}", StrategyLoggingLevel.Error);
                }
            }

            // Cancel all pending orders for the current symbol and account
            var pendingOrders = Core.Instance.Orders
                .Where(o => o.Symbol == this.CurrentSymbol && o.Account == this.CurrentAccount &&
                            (o.OrderTypeId == OrderType.Stop || o.OrderTypeId == OrderType.Limit))
                .ToList();

            foreach (var order in pendingOrders)
            {
                var cancelResult = order.Cancel();
                if (cancelResult.Status == TradingOperationResultStatus.Success)
                {
                    Log($"Canceled order successfully: Type={order.OrderTypeId}, Price={order.Price:F2}", StrategyLoggingLevel.Trading);
                }
                else
                {
                    Log($"Failed to cancel order: Type={order.OrderTypeId}. Error: {cancelResult.Message}", StrategyLoggingLevel.Error);
                }
            }

            // Reset internal flags
            orderPlaced = false;

            Log("Strategy stopped: All positions closed, orders canceled, and events unsubscribed.", StrategyLoggingLevel.Trading);
        }
        protected override void OnInitializeMetrics(Meter meter)
        {
            base.OnInitializeMetrics(meter);

            // Create observable counters for tracking metrics
            meter.CreateObservableCounter("total-net-pl", () => this.totalNetPl, description: "Total Net Profit/Loss");
            meter.CreateObservableCounter("total-gross-pl", () => this.totalGrossPl, description: "Total Gross Profit/Loss");
            meter.CreateObservableCounter("total-fee", () => this.totalFee, description: "Total Fee");

        }
        private void Core_TradeAdded(Trade obj)
        {
            if (obj.NetPnl != null)
            {
                this.totalNetPl += obj.NetPnl.Value;
                this.currentBalance += obj.NetPnl.Value;

                // Update gross profit or gross loss
                if (obj.NetPnl.Value > 0)
                {
                    this.totalGrossProfit += obj.GrossPnl != null ? obj.GrossPnl.Value : 0;
                }
                else
                {
                    this.totalGrossLoss += obj.GrossPnl != null ? Math.Abs(obj.GrossPnl.Value) : 0;
                }

                // Update peak balance
                if (this.currentBalance > this.peakBalance)
                {
                    this.peakBalance = this.currentBalance;
                }
            }

            if (obj.GrossPnl != null)
            {
                this.totalGrossPl += obj.GrossPnl.Value;
            }

            if (obj.Fee != null)
            {
                this.totalFee += obj.Fee.Value;
            }
        }



        // Dictionary to track the cumulative quantity filled for each Position ID
        private Dictionary<string, double> positionQuantities = new Dictionary<string, double>();

        private void Core_PositionAdded(Position obj)
        {
            // Ensure the position matches the current symbol and account
            if (obj.Symbol != this.CurrentSymbol || obj.Account != this.CurrentAccount)
                return;

            // Initialize cumulative tracking for the position if not already present
            if (!positionQuantities.ContainsKey(obj.Id))
            {
                positionQuantities[obj.Id] = 0;
            }

            // Update the cumulative quantity filled for this position
            positionQuantities[obj.Id] += obj.Quantity;

            // Check if the cumulative quantity matches or exceeds the intended position size
            if (positionQuantities[obj.Id] < this.Quantity)
            {
                // Log partial fill status and wait for the full position to be filled
                this.Log($"Partial fill for Position {obj.Id}. Current quantity: {positionQuantities[obj.Id]}. Waiting for full fill.", StrategyLoggingLevel.Trading);
                return;
            }

            // Calculate stop-loss and take-profit levels once the position is fully filled
            var (stopLossPrice, takeProfitPrice) = CalculateATRBasedSLTP(obj);

            // Place stop-loss order
            var stopLossOrderResult = Core.Instance.PlaceOrder(new PlaceOrderRequestParameters
            {
                Account = this.CurrentAccount,
                Symbol = this.CurrentSymbol,
                Side = obj.Side == Side.Buy ? Side.Sell : Side.Buy,
                OrderTypeId = OrderType.Stop,
                Quantity = this.Quantity,  // Full quantity for the position
                TriggerPrice = stopLossPrice,
                TimeInForce = TimeInForce.GTC
            });

            if (stopLossOrderResult.Status == TradingOperationResultStatus.Failure)
            {
                this.LogError($"Failed to place Stop-Loss at {stopLossPrice:F2}: {stopLossOrderResult.Message}");
            }

            // Place take-profit order
            var takeProfitOrderResult = Core.Instance.PlaceOrder(new PlaceOrderRequestParameters
            {
                Account = this.CurrentAccount,
                Symbol = this.CurrentSymbol,
                Side = obj.Side == Side.Buy ? Side.Sell : Side.Buy,
                OrderTypeId = OrderType.Limit,
                Quantity = this.Quantity,  // Full quantity for the position
                Price = takeProfitPrice,
                TimeInForce = TimeInForce.GTC
            });

            if (takeProfitOrderResult.Status == TradingOperationResultStatus.Failure)
            {
                this.LogError($"Failed to place Take-Profit at {takeProfitPrice:F2}: {takeProfitOrderResult.Message}");
            }

            // Log successful placement of SL and TP
            this.Log($"Position fully filled with Stop-Loss at {stopLossPrice:F2} and Take-Profit at {takeProfitPrice:F2}.", StrategyLoggingLevel.Trading);

            // Reset cumulative quantity for the position to avoid duplicate SL/TP placements
            positionQuantities.Remove(obj.Id);
        }



        private void Core_PositionRemoved(Position obj)
        {
            // Ensure the position matches the current symbol and account
            if (obj.Symbol != this.CurrentSymbol || obj.Account != this.CurrentAccount)
                return;

            // Reset flags or tracking variables as necessary
            orderPlaced = false;
            positionQuantities.Remove(obj.Id); // Remove the position's quantity tracking if it's stored

            this.Log("Position closed; ready to place a new order.", StrategyLoggingLevel.Trading);

            // Retrieve any remaining stop-loss and take-profit orders for this symbol and account
            var relatedOrders = Core.Instance.Orders
                .Where(o => o.Symbol == this.CurrentSymbol && o.Account == this.CurrentAccount &&
                            (o.OrderTypeId == OrderType.Stop || o.OrderTypeId == OrderType.Limit))
                .ToList();

            // Cancel each related SL/TP order
            foreach (var order in relatedOrders)
            {
                var cancelResult = order.Cancel();

                // Log success or failure of each cancellation attempt
                if (cancelResult.Status == TradingOperationResultStatus.Success)
                {
                    this.Log($"Successfully canceled {order.OrderTypeId} order at {order.Price:F2} for position {obj.Id}.", StrategyLoggingLevel.Trading);
                }
                else
                {
                    this.LogError($"Failed to cancel {order.OrderTypeId} order at {order.Price:F2}. Error: {cancelResult.Message}");
                }
            }

            // Final log to confirm completion of order cancellations for this position
            this.Log($"All associated SL/TP orders for Position {obj.Id} have been processed for cancellation.", StrategyLoggingLevel.Trading);
        }



        //History Methods
        private void Core_OrdersHistoryAdded(OrderHistory obj)
        {
            if (obj.Symbol != this.CurrentSymbol || obj.Account != this.CurrentAccount)
                return;

            if (obj.Status == OrderStatus.Refused)
                this.ProcessTradingRefuse();
        }

        private void Hdm_HistoryItemUpdated(object sender, HistoryEventArgs e)
        {
            var historyItem = e.HistoryItem as HistoryItemBar;
            if (historyItem != null && historyItem.TimeLeft <= DateTime.UtcNow)
            {

                this.OnTick();
            }
        }


        private bool orderPlaced = false;

        protected void OnTick()
        {
            // Retrieve the current open positions for the current symbol and account
            var openPositions = Core.Instance.Positions
                .Where(p => p.Symbol == this.CurrentSymbol && p.Account == this.CurrentAccount)
                .ToArray();

            // If there are no open positions, check for new entry conditions
            if (openPositions.Length == 0)
            {
                // Calculate current indicator values
                double macdValue = this.indicatorMacd.MacdValue;
                double signalValue = this.indicatorMacd.SignalValue;
                double osmaValue = this.indicatorMacd.OsmaValue;
                double adxValue = this.indicatorAdx.AdxValue;
                double diPlusValue = this.indicatorAdx.DiPlusValue;
                double diMinusValue = this.indicatorAdx.DiMinusValue;
                double rsiValue = this.indicatorRsi.RsiValue;
                double movingAverageValue = this.indicatorRsi.MaValue;
                double williamsRValue = this.indicatorWilliamsR.RlwValue;
                double cciValue = this.indicatorCci.CciValue;
                double atrValue = this.indicatorAtr.AtrValue;
                double currentPrice = this.CurrentSymbol.Last;
                double bollingerMiddle = this.bollingerBands.BollingerMiddle;
                double osmaThreshold = this.OsmaThreshold;

                // Ensure sufficient historical data is available before accessing previous values
                if (this.hdm.Count <= 1)
                {
                    this.Log("Insufficient data for previous values.", StrategyLoggingLevel.Trading);
                    return;
                }

                // Retrieve previous indicator values
                double prevMacdValue = this.indicatorMacd.GetValue(1);
                double prevRsiValue = this.indicatorRsi.GetValue(1);
                double prevCciValue = this.indicatorCci.GetValue(1);

                // Retrieve the most recent previous tick price
                var tickHistory = this.CurrentSymbol.GetHistory(Period.TICK1, HistoryType.Last, 2);
                double prevPrice = tickHistory.Count >= 2 ? ((HistoryItemLast)tickHistory[0]).Price : double.NaN;

                // Ensure there are at least two ticks to compare
                if (tickHistory.Count < 2)
                {
                    this.Log("Insufficient tick data for previous price comparison.", StrategyLoggingLevel.Trading);
                    return;
                }

                // Access the previous tick price
                double prevTickPrice = ((HistoryItemLast)tickHistory[tickHistory.Count - 2]).Price;

                // Exit if any indicator values are NaN
                if (double.IsNaN(macdValue) || double.IsNaN(signalValue) || double.IsNaN(adxValue) ||
                    double.IsNaN(currentPrice) || double.IsNaN(atrValue) || double.IsNaN(rsiValue) || double.IsNaN(williamsRValue))
                {
                    this.LogError("One or more indicator values are NaN.");
                    return;
                }
                else
                {
                    // Long entry conditions
                    if (osmaValue > osmaThreshold &&
                        macdValue > signalValue &&
                        diPlusValue > diMinusValue &&
                        cciValue > -50 &&
                        williamsRValue > -80 &&
                        rsiValue > 50 &&
                        rsiValue > movingAverageValue &&
                        macdValue > prevMacdValue &&
                        rsiValue > prevRsiValue &&
                        cciValue > prevCciValue &&
                        currentPrice > prevPrice &&
                        !this.orderPlaced)
                    {
                        this.Log("Conditions met for placing a buy order.", StrategyLoggingLevel.Trading);
                        this.PlaceMarketOrder(Side.Buy);
                        this.orderPlaced = true;
                    }

                    // Short entry conditions
                    else if (osmaValue < -osmaThreshold &&
                             macdValue < signalValue &&
                             diPlusValue < diMinusValue &&
                             cciValue < 50 &&
                             currentPrice < bollingerMiddle &&
                             williamsRValue < -20 &&
                             rsiValue < 50 &&
                             rsiValue < movingAverageValue &&
                             macdValue < prevMacdValue &&
                             rsiValue < prevRsiValue &&
                             cciValue < prevCciValue &&
                             currentPrice < prevPrice &&
                             !this.orderPlaced)
                    {
                        this.Log("Conditions met for placing a sell order.", StrategyLoggingLevel.Trading);
                        this.PlaceMarketOrder(Side.Sell);
                        this.orderPlaced = true;
                    }
                }
            }
        }


        private void PlaceMarketOrder(Side side)
        {
            var existingPosition = Core.Instance.Positions
                .FirstOrDefault(p => p.Symbol.Id == this.CurrentSymbol.Id && p.Account == this.CurrentAccount);

            if (existingPosition != null)
            {
                this.Log("An open position already exists. No new market order will be created.", StrategyLoggingLevel.Trading);
                return;
            }

            try
            {
                var orderResult = Core.Instance.PlaceOrder(new PlaceOrderRequestParameters
                {
                    Account = this.CurrentAccount,
                    Symbol = this.CurrentSymbol,
                    Side = side,
                    OrderTypeId = OrderType.Market,
                    Quantity = this.Quantity,
                    TimeInForce = TimeInForce.GTC
                });

                if (orderResult.Status != TradingOperationResultStatus.Success)
                {
                    throw new Exception($"Order placement failed: {orderResult.Message}");
                }

                this.Log($"Successfully placed {side} market order.", StrategyLoggingLevel.Trading);
            }
            catch (Exception ex)
            {
                this.LogError(ex.Message);
            }
        }

        private (double stopLossPrice, double takeProfitPrice) CalculateATRBasedSLTP(Position position)
        {
            double atrValue = this.indicatorAtr.GetValue(0);
            double tickSize = this.CurrentSymbol.TickSize;
            double priceToUse = position.OpenPrice;

            // Calculate SL and TP prices based on ATR, aligning to tick size
            double stopLossPrice = position.Side == Side.Buy
                ? AlignToTickSize(priceToUse - (AtrStopLossMultiplier * atrValue), tickSize)
                : AlignToTickSize(priceToUse + (AtrStopLossMultiplier * atrValue), tickSize);

            double takeProfitPrice = position.Side == Side.Buy
                ? AlignToTickSize(priceToUse + (AtrTakeProfitMultiplier * atrValue), tickSize)
                : AlignToTickSize(priceToUse - (AtrTakeProfitMultiplier * atrValue), tickSize);

            return (stopLossPrice, takeProfitPrice);
        }


        //Miscellaneous Methods
        private double AlignToTickSize(double price, double tickSize) => Math.Round(price / tickSize) * tickSize;
        private DateTime lastRefuseTime = DateTime.MinValue;
        private readonly TimeSpan refusalCooldown = TimeSpan.FromSeconds(30);

        private void ProcessTradingRefuse()
        {
            // Only log and retry if enough time has passed since the last refusal
            if (DateTime.Now - lastRefuseTime > refusalCooldown)
            {
                this.LogError("Order was refused. Check account balance and margin requirements.");
                lastRefuseTime = DateTime.Now;
            }
        }

    }
}