namespace PrivateTimeTrace.Data;

public enum VisualStyle { Frosted, Liquid }
public enum ChartKind { Line, Bar }

public sealed record AppPreferences(
    VisualStyle Style = VisualStyle.Liquid,
    ChartKind TrendKind = ChartKind.Line,
    ChartKind TopicKind = ChartKind.Bar);
