using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Configuration; // Ensure this line is present

// สร้างอินสแตนซ์ของบริการแชท OpenAI Platform โดยใช้โมเดล gpt-4o-mini และคีย์ API
IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json") // Assuming the configuration is stored in an appsettings.json file
    .Build();

IChatCompletionService openAIChatService = new OpenAIChatCompletionService(
    modelId: configuration["OpenAI:ModelId"] ?? string.Empty,
    apiKey: configuration["OpenAI:ApiKey"] ?? string.Empty
);

// แสดงผลลัพธ์ของการถามคำถามในคอนโซล
Console.WriteLine("OpenAI");
Console.WriteLine("--------------------");
Console.WriteLine(await openAIChatService.GetChatMessageContentAsync("ท้องฟ้าสีอะไร"));
Console.WriteLine();

// สร้างอินสแตนซ์ของบริการแชท Azure OpenAI Service โดยใช้โมเดล gpt-4o-mini และคีย์ API
IChatCompletionService azureChatService = new AzureOpenAIChatCompletionService(
    deploymentName: configuration["AzureOpenAI:DeploymentName"], // Use the configuration object to access the DeploymentName value
    endpoint: configuration["AzureOpenAI:Endpoint"], // Use the configuration object to access the Endpoint value
    apiKey: configuration["AzureOpenAI:ApiKey"] // Use the configuration object to access the ApiKey value
);

// แสดงผลลัพธ์ของการถามคำถามในคอนโซล
Console.WriteLine("Azure OpenAI");
Console.WriteLine("--------------------");
Console.WriteLine(await azureChatService.GetChatMessageContentAsync("ท้องฟ้าสีอะไร"));
Console.WriteLine();