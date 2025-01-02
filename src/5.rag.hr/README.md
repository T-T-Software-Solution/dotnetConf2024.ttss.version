# รายละเอียด
https://www.loom.com/share/0600fd47db0f45eea7b05d89098e05a9

## โครงสร้างโปรเจกต์

- **ManualIngestor.cs** - Service สำหรับการ ingestion ที่ใช้ดึงข้อมูลจากไฟล์ PDF ของคู่มือผลิตภัณฑ์, แบ่งข้อความออกเป็นส่วนเล็ก ๆ, สร้าง embeddings และบันทึกเป็นไฟล์ JSON  
- **ProductManualService.cs** - Service สำหรับการจัดเก็บที่ใช้ `IVectorStore` เพื่อบันทึกและค้นหา embeddings ของคู่มือผลิตภัณฑ์  
- **Employeeummarizer** - AI service ที่ใช้โมเดล AI เพื่อสร้างสรุปสำหรับ customer support Employee  

## ความต้องการระบบ

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)  
- [Ollama - Local LLMs](https://ollama.com/download)  
- [Azure Subscription - Cloud LLMs](https://azure.microsoft.com/free/cognitive-services?azure-portal=true)  

## การตั้งค่า (Configuration)

สำหรับแอปพลิเคชันนี้ สามารถใช้โมเดลภาษาและ embedding แบบ local ผ่าน Ollama หรือโมเดลที่โฮสต์บน Azure OpenAI  

### Ollama

1. ดาวน์โหลดโมเดลภาษา [llama3.2](https://ollama.com/library/llama3.2):

    ```bash
    ollama pull llama3.2
    ```

2. ดาวน์โหลดโมเดล embedding [all-minilm](https://ollama.com/library/all-minilm):

    ```bash
    ollama pull all-minilm
    ```

3. ในไฟล์ `appsettings.json` ตั้งค่าดังนี้:
    ```json
    `useAzureOpenAI : false`
    
    "Ollama": {
        "ChatModel": "llama3.2",
        "TextEmbeddingModel": "all-minilm",
        "Endpoint": "http://localhost:11434/"
    }
    ```

### Azure OpenAI

1. Deploy โมเดล chat และ embedding สำหรับรายละเอียดเพิ่มเติม [ดูเอกสาร Azure OpenAI](https://learn.microsoft.com/azure/ai-services/openai/how-to/create-resource?pivots=web-portal#deploy-a-model)  
    - **chat** - โมเดลภาษาที่รองรับการสนทนา (เช่น `gpt-4o-mini`)  
    - **embedding** - โมเดล embedding (เช่น `text-embedding-3-small`)  

    หากคุณใช้ชื่อ deployment ต่างจาก *chat* และ *embedding* อัปเดตชื่อใน `appsettings.json`  

2. ใน *Program.cs* ตั้งค่าดังนี้:
    - `useAzureOpenAI : true`
  - 
    ```json
    `useAzureOpenAI : true`
    
    "AzureOpenAI": {
        "ChatModel": "gpt-4o-mini",
        "TextEmbeddingModel": "text-embedding-3-small",
        "Endpoint": "{Azure Endpoint}",
        "ApiKey": "{Azure API Key}"
    }
    ```