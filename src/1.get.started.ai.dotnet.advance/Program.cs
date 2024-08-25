using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

ServiceCollection services = new ServiceCollection();

services.AddOpenAIChatCompletion(
    modelId: "gpt-4o-mini",
    apiKey: "key"
);

services.AddKernel();
// services.AddLogging(builder =>
// {
//     builder.AddConsole().SetMinimumLevel(LogLevel.Trace);
// });

IServiceProvider serviceProvider = services.BuildServiceProvider();
IChatCompletionService chatService = serviceProvider.GetRequiredService<IChatCompletionService>();
Kernel kernel = serviceProvider.GetRequiredService<Kernel>();

kernel.ImportPluginFromType<Demographics>();
PromptExecutionSettings settings = new OpenAIPromptExecutionSettings() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };

ChatHistory chatHistory = new ChatHistory();
while (true)
{
    Console.Write("กรุณาระบุความต้องการ: ");
    chatHistory.AddUserMessage(Console.ReadLine());


    var assistant = await chatService.GetChatMessageContentAsync(chatHistory, settings, kernel);
    Console.WriteLine(assistant);
    chatHistory.Add(assistant);
}

class Demographics
{
    [KernelFunction]
    public int GetPersonAge(string name)
    {
        return name switch
        {
            "ไข่ดาว" => 22,
            "ไข่เจียว" => 18,
            _ => 30
        };
    }
}