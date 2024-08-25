using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

// สร้างอินสแตนซ์ของบริการแชท OpenAI Platform โดยใช้โมเดล gpt-4o-mini และคีย์ API
IChatCompletionService chatService = new OpenAIChatCompletionService(
    modelId: "gpt-4o-mini",
    apiKey: "key"
    );

// แสดงผลลัพธ์ของการถามคำถามในคอนโซล
Console.WriteLine(await chatService.GetChatMessageContentAsync("ท้องฟ้าสีอะไร"));