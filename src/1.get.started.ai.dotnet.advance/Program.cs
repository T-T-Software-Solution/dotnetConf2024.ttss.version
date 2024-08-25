using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

ServiceCollection services = new ServiceCollection();
services.AddOpenAIChatCompletion(
    modelId: "gpt-4o-mini",
    apiKey: "key"
);
IServiceProvider serviceProvider = services.BuildServiceProvider();

IChatCompletionService chatService = serviceProvider.GetRequiredService<IChatCompletionService>();
ChatHistory chatHistory = new ChatHistory();
chatHistory.AddUserMessage("ท้องฟ้าสีอะไร");

Console.WriteLine(await chatService.GetChatMessageContentAsync(chatHistory));