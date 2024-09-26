namespace VectorApp.Models
{
    public class TravelEmbeddingResult
    {
        public GeneralDescription? GeneralDescription { get; set; }
        public string? ATT_LOCATION { get; set; }
        public string? ATT_WEBSITE { get; set; }
        public string? ATT_FACEBOOK { get; set; }
        public double Score { get; set; }  // Similarity score
        public string FormattedDescription
        {
            get
            {
                if (GeneralDescription == null)
                {
                    return string.Empty;
                }

                return
                    $"ชื่อ: {GeneralDescription.Name ?? "N/A"}\n" +
                    $"ตำแหน่ง: {GeneralDescription.Location ?? "N/A"}\n" +
                    $"ประเภท: {GeneralDescription.Type ?? "N/A"}\n" +
                    $"เวลาเปิดปิด: {GeneralDescription.OpenCloseTime ?? "N/A"}\n" +
                    $"ค่าเข้า: {GeneralDescription.EntryFee ?? "N/A"}\n" +
                    $"รายละเอียด: {GeneralDescription.Detail ?? "N/A"}\n" +                    
                    $"ติดต่อ: {GeneralDescription.Contact ?? "N/A"}";
            }
        }
    }

    public class GeneralDescription
    {
        public string? Name { get; set; }  // ชื่อ: ATT_NAME_TH ATT_NAME_EN
        public string? Type { get; set; }  // ประเภท: ATTR_SUB_TYPE_TH
        public string? OpenCloseTime { get; set; }  // เวลาเปิดปิด: ATT_START_END
        public string? EntryFee { get; set; }  // ค่าเข้า: ชาวไทย ATT_FEE_TH เด็ก ATT_FEE_TH_KID ชาวต่างชาติ ATT_FEE_EN
        public string? Detail { get; set; }  // รายละเอียด: ATT_DETAIL_TH
        public string? Location { get; set; }  // ตำแหน่ง: PROVINCE_NAME_TH DISTRICT_NAME_TH
        public string? Contact { get; set; }  // ติดต่อ: ATT_FACILITIES_CONTACT ATT_WEBSITE ATT_FACEBOOK
    }

}
