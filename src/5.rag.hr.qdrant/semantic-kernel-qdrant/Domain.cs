using System.Text.Json.Serialization;

public class ManualChunk
{
    [VectorStoreRecordKey]
    public ulong ChunkId { get; set; }

    [VectorStoreRecordData(IsFilterable = true)]
    public int EmployeeId { get; set; }

    public int PageNumber { get; set; }

    [VectorStoreRecordData(IsFullTextSearchable = true)]
    public string Text { get; set; }

    [VectorStoreRecordVector(Dimensions: 1536, DistanceFunction.CosineSimilarity, IndexKind.Hnsw)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}

public enum EmploymentStatus
{
    Open,  // ทำงานอยู่
    Closed // ไม่ได้ทำแล้ว
}

public class Employee
{
    public int EmployeeId { get; set; } // TicketId -> EmployeeId

    public DateTime CreatedAt { get; set; } // วันที่สร้างข้อมูล

    public string FullName { get; set; } = string.Empty; // ชื่อเต็มของพนักงาน

    public string Position { get; set; } = string.Empty; // ตำแหน่งงาน

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EmploymentStatus EmploymentStatus { get; set; } // สถานะการทำงาน

    public List<Interaction> InteractionHistory { get; set; } = new(); // ประวัติการถามตอบกับ AI
}

public class Interaction
{
    public int InteractionId { get; set; } // MessageId -> InteractionId

    public DateTime Date { get; set; } // วันที่ของการถามตอบ

    public string Content { get; set; } = string.Empty; // เนื้อหาของคำถาม

    public string AIResponse { get; set; } = string.Empty; // คำตอบจาก AI
}