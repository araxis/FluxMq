namespace FluxMq.UI.Models;

public sealed record DashboardMetricValue(
    string Label,
    double Value,
    string Unit,
    string FormattedValue);

public sealed record FluxChartSeries(
    string Name,
    IReadOnlyList<double> Values,
    IReadOnlyList<string> Labels);
