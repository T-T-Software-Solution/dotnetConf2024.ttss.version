using System.Text.Json.Serialization;

public class TravelAnalysisResult
{
    [JsonPropertyName("is_travel_related")]
    public bool IsTravelRelated { get; set; }

    [JsonPropertyName("place_name")]
    public string? PlaceName { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("province")]
    public string? Province { get; set; }

    [JsonPropertyName("district")]
    public string? District { get; set; }

    [JsonPropertyName("place_type")]
    public string? PlaceType { get; set; }

    [JsonPropertyName("month")]
    public string? Month { get; set; }

    [JsonPropertyName("day")]
    public string? Day { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    // New concatenated message property
    [JsonIgnore]
    public string ConcatMessage
    {
        get
        {
            var messageBuilder = new System.Text.StringBuilder();
            messageBuilder.AppendLine("ผลการค้นหาสถานที่ท่องเที่ยว จากเงื่อนไขดังนี้");

            // Add each property with value to the message
            bool hasValue = false;

            if (!string.IsNullOrEmpty(PlaceName))
            {
                messageBuilder.AppendLine($"ชื่อสถานที่: {PlaceName}");
                hasValue = true;
            }
            if (!string.IsNullOrEmpty(Region))
            {
                messageBuilder.AppendLine($"ภาค: {Region}");
                hasValue = true;
            }
            if (!string.IsNullOrEmpty(Province))
            {
                messageBuilder.AppendLine($"จังหวัด: {Province}");
                hasValue = true;
            }
            if (!string.IsNullOrEmpty(District))
            {
                messageBuilder.AppendLine($"อำเภอ: {District}");
                hasValue = true;
            }
            if (!string.IsNullOrEmpty(PlaceType))
            {
                messageBuilder.AppendLine($"ประเภทสถานที่: {PlaceType}");
                hasValue = true;
            }
            if (!string.IsNullOrEmpty(Month))
            {
                messageBuilder.AppendLine($"เดือน: {Month}");
                hasValue = true;
            }
            if (!string.IsNullOrEmpty(Day))
            {
                messageBuilder.AppendLine($"วัน: {Day}");
                hasValue = true;
            }
            if (!string.IsNullOrEmpty(Time))
            {
                messageBuilder.AppendLine($"เวลา: {Time}");
                hasValue = true;
            }

            // If no values are provided, return the default message
            if (!hasValue)
            {
                return "ไม่สามารถเข้าใจเงื่อนไขการค้นหา จากข้อความที่ส่งเข้ามา";
            }

            return messageBuilder.ToString().Trim(); // Remove any trailing newline
        }
    }
}
