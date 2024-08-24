using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

IChatCompletionService chatService = new OpenAIChatCompletionService("gpt-4o-mini", "Key");

Console.WriteLine(await chatService.GetChatMessageContentAsync("ท้องฟ้าสีอะไร"));