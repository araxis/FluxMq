namespace FluxMq.UI.Services;

public static class DashboardLatestEventVisualOptions
{
    public const string HeaderKey = "latest.header";
    public const string ShowHeaderKey = "latest.showHeader";
    public const string ShowTypeKey = "latest.showType";
    public const string ShowTopicKey = "latest.showTopic";
    public const string ShowStatusKey = "latest.showStatus";
    public const string ShowTimestampKey = "latest.showTimestamp";
    public const string ShowPayloadKey = "latest.showPayload";
    public const string EmptyTextKey = "latest.emptyText";
    public const string HeaderColorKey = "latest.headerColor";
    public const string DetailColorKey = "latest.detailColor";
    public const string PayloadColorKey = "latest.payloadColor";

    public const string LegacyShowTopicKey = "showTopic";
    public const string LegacyShowStatusKey = "showStatus";
    public const string LegacyShowPayloadKey = "showPayload";
    public const string LegacyTimestampFormatKey = "timestampFormat";

    public const string DefaultHeader = "Latest event";
    public const string DefaultEmptyText = "No events yet";
    public const string DefaultHeaderColor = "#f3f7fb";
    public const string DefaultDetailColor = "#9fb0c5";
    public const string DefaultPayloadColor = "#d3e2f3";
}
