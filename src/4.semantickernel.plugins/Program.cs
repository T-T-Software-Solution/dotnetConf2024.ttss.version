#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

// Import packages
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json.Serialization;
using System.ComponentModel;

namespace SemanticKernelGettingStarted
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            // Populate values from your OpenAI deployment
            string aiMode;
            string ollamaChatModel, ollamaAPIEndpoint;
            string openAIChatModel, openAIKey;
            string azureChatModel, azureAPIEndpoint, azureAPIKey;

            ReadDataFromConfig(out aiMode,
                out ollamaChatModel, out ollamaAPIEndpoint,
                out openAIChatModel, out openAIKey,
                out azureChatModel, out azureAPIEndpoint, out azureAPIKey);

            IKernelBuilder builder = AddAIServices(aiMode,
                ollamaChatModel, ollamaAPIEndpoint,
                openAIChatModel, openAIKey,
                azureChatModel, azureAPIEndpoint, azureAPIKey);

            AddEnterpriseServices(builder);

            Kernel kernel = BuildKernel(builder);
            IChatCompletionService chatCompletionService = GetChatService(kernel);

            AddPluginToTheKernel(kernel);

            OpenAIPromptExecutionSettings openAIPromptExecutionSettings = SetupPlannerAutoPluginCalling();
            ChatHistory history = SetupChatHistory();

            // Initiate a back-and-forth chat
            string? userInput;
            do
            {
                userInput = await InvokeTheKernel(kernel, chatCompletionService, openAIPromptExecutionSettings, history);
            } while (userInput is not null);
        }
        private static void ReadDataFromConfig(
            out string aiMode,
            out string ollamaChatModel, out string ollamaAPIEndpoint,
            out string openAIChatModel, out string openAIKey,
            out string azureChatModel, out string azureAPIEndpoint, out string azureAPIKey)

        {
            IConfiguration configuration = new ConfigurationBuilder()
                            .AddJsonFile("appsettings.json")
                            .Build();

            aiMode = configuration["AIMode"] ?? throw new ArgumentNullException("AIMode");

            ollamaChatModel = configuration["Ollama:ChatModel"] ?? throw new ArgumentNullException("Ollama:ChatModel");
            ollamaAPIEndpoint = configuration["Ollama:Endpoint"] ?? throw new ArgumentNullException("Ollama:Endpoint");

            openAIChatModel = configuration["OpenAI:ChatModel"] ?? throw new ArgumentNullException("OpenAI:ChatModel");
            openAIKey = configuration["OpenAI:ApiKey"] ?? throw new ArgumentNullException("OpenAI:ApiKey");

            azureChatModel = configuration["AzureOpenAI:ChatModel"] ?? throw new ArgumentNullException("AzureOpenAI:ChatModel");
            azureAPIEndpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new ArgumentNullException("AzureOpenAI:Endpoint");
            azureAPIKey = configuration["AzureOpenAI:ApiKey"] ?? throw new ArgumentNullException("AzureOpenAI:ApiKey");
        }
        private static IKernelBuilder AddAIServices(string aiMode,
            string ollamaChatModel, string ollamaAPIEndpoint,
            string openAIChatModel, string openAIKey,
            string azureChatModel, string azureAPIEndpoint, string azureAPIKey)
        {
            switch (aiMode)
            {
                case "Ollama":
                    return Kernel.CreateBuilder().AddOllamaChatCompletion(
                        modelId: ollamaChatModel,
                        endpoint: new Uri(ollamaAPIEndpoint));
                case "OpenAI":
                    return Kernel.CreateBuilder().AddOpenAIChatCompletion(
                        modelId: openAIChatModel,
                        apiKey: openAIKey);
                case "AzureOpenAI":
                    return Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(
                        deploymentName: azureChatModel,
                        endpoint: azureAPIEndpoint,
                        apiKey: azureAPIKey);
                default:
                    throw new ArgumentException("Invalid AI mode");
            }
        }
        private static void AddEnterpriseServices(IKernelBuilder builder)
        {
            // Add enterprise components
            builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));
        }
        private static Kernel BuildKernel(IKernelBuilder builder)
        {
            // Build the kernel
            return builder.Build();
        }
        private static IChatCompletionService GetChatService(Kernel kernel)
        {
            return kernel.Services.GetRequiredService<IChatCompletionService>();
        }
        private static void AddPluginToTheKernel(Kernel kernel)
        {
            // Add a plugin (the LightsPlugin class is defined below)
            kernel.Plugins.AddFromType<LightsPlugin>("Lights");
        }
        private static OpenAIPromptExecutionSettings SetupPlannerAutoPluginCalling()
        {
            // Enable planning
            return new()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };
        }
        private static ChatHistory SetupChatHistory()
        {
            // Create a history store the conversation
            return new ChatHistory();
        }
        private static async Task<string?> InvokeTheKernel(Kernel kernel, IChatCompletionService chatCompletionService, OpenAIPromptExecutionSettings openAIPromptExecutionSettings, ChatHistory history)
        {
            string? userInput;
            // Collect user input
            Console.Write("User > ");
            userInput = Console.ReadLine();

            // Add user input
            history.AddUserMessage(userInput);

            // Get the response from the AI
            var result = await chatCompletionService.GetChatMessageContentAsync(
                history,
                executionSettings: openAIPromptExecutionSettings,
                kernel: kernel);

            // Print the results
            Console.WriteLine("Assistant > " + result);

            // Add the message from the agent to the chat history
            history.AddMessage(result.Role, result.Content ?? string.Empty);
            return userInput;
        }

        public class LightsPlugin
        {
            // Mock data for the lights
            private readonly List<LightModel> lights = new()
            {
                new LightModel { Id = 1, Name = "Table Lamp", ThaiName = "โคมไฟตั้งโต๊ะ", IsOn = false },
                new LightModel { Id = 2, Name = "Porch light", ThaiName = "ไฟระเบียง", IsOn = false },
                new LightModel { Id = 3, Name = "Chandelier", ThaiName = "โคมไฟระย้า", IsOn = false }
            };

            [KernelFunction("get_lights")]
            [Description("Gets a list of lights and their current state")]
            [return: Description("An array of lights")]
            public async Task<List<LightModel>> GetLightsAsync()
            {
                return lights;
            }

            [KernelFunction("change_state")]
            [Description("Changes the state of the light")]
            [return: Description("The updated state of the light; will return null if the light does not exist")]
            public async Task<LightModel?> ChangeStateAsync(int id, bool isOn)
            {
                var light = lights.FirstOrDefault(light => light.Id == id);

                if (light == null)
                {
                    return null;
                }

                // Update the light with the new state
                light.IsOn = isOn;

                return light;
            }
        }
        public class LightModel
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }
            [JsonPropertyName("thainame")]
            public string ThaiName { get; set; }

            [JsonPropertyName("is_on")]
            public bool? IsOn { get; set; }
        }
    }
}