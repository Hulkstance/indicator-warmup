using Binance.Net.Clients;
using Binance.Net.Enums;

// Magic strings are bad
const string symbol = "BTCBUSD";
const KlineInterval interval = KlineInterval.OneMinute;
const int smaPeriod = 5;
const int limit = 1000;

var restClient = new BinanceClient();
var socketClient = new BinanceSocketClient();
var sma = new SimpleMovingAverage(smaPeriod);

// Warmup indicator
var callResult = await restClient.SpotApi.ExchangeData.GetKlinesAsync(symbol, interval, limit: limit);

if (!callResult)
{
    Console.WriteLine($"Failed to connect: {callResult.Error}");
    return;
}

var klines = callResult.Data.ToList();
klines.RemoveAt(klines.Count - 1); // The last candle is unfinished

foreach (var kline in klines)
{
    sma.ComputeNextValue(kline.ClosePrice);
}

Console.WriteLine($"Last candle is: {klines.Last().OpenTime}");

// Further on
var subscriptionResult = await socketClient.SpotStreams.SubscribeToKlineUpdatesAsync(symbol, interval, data =>
{
    if (data.Data.Data.Final)
    {
        var ma = sma.ComputeNextValue(data.Data.Data.ClosePrice);
        Console.WriteLine($"Time: {data.Data.Data.OpenTime} | MA({smaPeriod}): {ma:f2}");
    }
});

if (!subscriptionResult)
{
    Console.WriteLine($"Failed to connect: {subscriptionResult.Error}");
    return;
}

subscriptionResult.Data.ConnectionClosed += () =>
{
    Console.WriteLine("Connection closed");
};
subscriptionResult.Data.ConnectionLost += () =>
{
    Console.WriteLine("Connection lost");
};
subscriptionResult.Data.ConnectionRestored += (time) =>
{
    Console.WriteLine("Connection restored");
};

Console.ReadLine();

public interface IIndicator<TInput, out TOutput> where TInput : notnull
{
    bool IsReady { get; }

    List<TInput> Source { get; }

    TOutput ComputeNextValue(TInput source);

    void Reset();
}

public sealed class SimpleMovingAverage : IIndicator<decimal, decimal>
{
    private readonly int _period;
    private readonly Queue<decimal> _queue = new();

    public SimpleMovingAverage(int period)
    {
        if (period <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "The period cannot be less than or equal to 1");
        }

        _period = period;
    }

    public bool IsReady => _queue.Count == _period;

    public List<decimal> Source => _queue.ToList();

    public decimal ComputeNextValue(decimal source)
    {
        _queue.Enqueue(source);

        if (_queue.Count > _period)
        {
            _queue.Dequeue();
        }

        var items = _queue.ToArray();
        return _queue.Count < _period ? 0 : items.Sum() / _queue.Count;
    }

    public void Reset()
    {
        _queue.Clear();
    }
}
