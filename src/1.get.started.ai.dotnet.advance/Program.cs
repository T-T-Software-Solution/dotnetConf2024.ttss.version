using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Build configuration
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Get configuration values
        string modelId = configuration["OpenAI:ModelId"] ?? string.Empty;
        string apiKey = configuration["OpenAI:ApiKey"] ?? string.Empty;

        // Setup services
        ServiceCollection services = new ServiceCollection();

        services.AddOpenAIChatCompletion(
            modelId: modelId,
            apiKey: apiKey
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
            string userInput = Console.ReadLine() ?? string.Empty;
            if (userInput == string.Empty)
                continue;

            chatHistory.AddUserMessage(userInput);

            var assistant = await chatService.GetChatMessageContentAsync(chatHistory, settings, kernel);
            Console.WriteLine(assistant);
            chatHistory.Add(assistant);
        }
    }
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