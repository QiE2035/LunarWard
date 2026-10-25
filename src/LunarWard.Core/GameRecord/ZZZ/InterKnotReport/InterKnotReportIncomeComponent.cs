using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.ZZZ.InterKnotReport;

public class InterKnotReportIncomeComponent
{

    [JsonPropertyName("action")]
    public string Action { get; set; } = null!;


    [JsonPropertyName("num")]
    public int Num { get; set; }

    /// <summary>
    /// 百分比，单位 %
    /// </summary>
    [JsonPropertyName("percent")]
    public int Percent { get; set; }

}


