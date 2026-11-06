using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Records Avalonia's per-pass timings ("Avalonia.Diagnostic.Meter" histograms: compositor render/update on the
/// render thread, measure/arrange/render/input on the UI thread) while running, then writes them to
/// <c>render-trace-*.csv</c> in the log directory with a per-metric summary in the log.
/// Requires the "Avalonia.Diagnostics.Diagnostic.IsEnabled" AppContext switch, set in Program.Main.
/// </summary>
internal static class RenderTrace
{
    private const string MeterName = "Avalonia.Diagnostic.Meter";
    private static readonly string ClassName = nameof(RenderTrace);

    private readonly record struct Sample(double TimeMs, string Metric, double ValueMs);

    private static MeterListener? _listener;
    private static ConcurrentQueue<Sample>? _samples;
    private static long _startTimestamp;
    private static DateTime _startTime;

    public static void Start()
    {
        if (_listener != null)
            return;

        var samples = new ConcurrentQueue<Sample>();
        _samples = samples;
        _startTimestamp = Stopwatch.GetTimestamp();
        _startTime = DateTime.Now;

        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == MeterName && instrument is Histogram<double>)
                    l.EnableMeasurementEvents(instrument);
            }
        };
        // Called on the UI and render threads; keep it allocation-light and lock-free.
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
            samples.Enqueue(new Sample(Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds, instrument.Name, value)));
        listener.Start();
        _listener = listener;
    }

    public static void Stop()
    {
        if (_listener is not { } listener || _samples is not { } samples)
            return;

        listener.Dispose();
        _listener = null;
        _samples = null;
        var duration = Stopwatch.GetElapsedTime(_startTimestamp);
        var path = Path.Combine(Log.CurrentLogDirectory,
            $"render-trace-{_startTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.csv");

        _ = Task.Run(() =>
        {
            try
            {
                var all = samples.ToArray();
                var csv = new StringBuilder("time_ms,metric,value_ms\n");
                foreach (var s in all)
                    csv.Append(CultureInfo.InvariantCulture, $"{s.TimeMs:F3},{s.Metric},{s.ValueMs:F3}\n");
                File.WriteAllText(path, csv.ToString());

                var summary = new StringBuilder();
                summary.Append(CultureInfo.InvariantCulture,
                    $"Render trace {duration.TotalSeconds:F1}s, {all.Length} samples -> {path}");
                foreach (var group in all.GroupBy(s => s.Metric).OrderBy(g => g.Key))
                {
                    var values = group.Select(s => s.ValueMs).Order().ToArray();
                    summary.Append(CultureInfo.InvariantCulture,
                        $"\n  {group.Key}: n={values.Length} ({values.Length / duration.TotalSeconds:F1}/s) " +
                        $"p50={Percentile(values, 0.50):F2}ms p95={Percentile(values, 0.95):F2}ms max={values[^1]:F2}ms");
                }
                Log.Info(ClassName, summary.ToString());
            }
            catch (Exception e)
            {
                Log.Exception(ClassName, "Failed to write render trace", e);
            }
        });
    }

    private static double Percentile(double[] sorted, double p) =>
        sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(p * sorted.Length) - 1)];
}
