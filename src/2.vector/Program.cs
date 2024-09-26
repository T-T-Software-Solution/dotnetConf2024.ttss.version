using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;

#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0050

namespace HelloVector
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string chatModel, textEmbeddingModel, azureAPIEndpoint, azureAPIKey;

            ReadDataFromConfig(out chatModel, out textEmbeddingModel, out azureAPIEndpoint, out azureAPIKey);

            var builder = Kernel.CreateBuilder();
            SetupChatCompletion(chatModel, azureAPIEndpoint, azureAPIKey, builder);
            SetupTextEmbedding(textEmbeddingModel, azureAPIEndpoint, azureAPIKey, builder);
            Kernel kernel = builder.Build();

            var question = "เพื่อนคนไหนชอบอาหาร? บอกมาทั้งหมดที่เกี่ยวข้อง";

            DisplayTitle(question);
            //await DisplayResultOfPureChatModel(kernel, question);
            await DisplayResultOfChatModelWithTextEmbedding(kernel, question);
        }
        private static void ReadDataFromConfig(out string chatModel, out string textEmbeddingModel, out string azureAPIEndpoint, out string azureAPIKey)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                            .AddJsonFile("appsettings.json")
                            .Build();

            chatModel = configuration["AzureOpenAI:ChatModel"] ?? throw new ArgumentNullException("AzureOpenAI:ChatModel");
            textEmbeddingModel = configuration["AzureOpenAI:TextEmbeddingModel"] ?? throw new ArgumentNullException("AzureOpenAI:TextEmbeddingModel");
            azureAPIEndpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new ArgumentNullException("AzureOpenAI:Endpoint");
            azureAPIKey = configuration["AzureOpenAI:ApiKey"] ?? throw new ArgumentNullException("AzureOpenAI:ApiKey");
        }
        private static void SetupChatCompletion(string chatModel, string azureAPIEndpoint, string azureAPIKey, IKernelBuilder builder)
        {
            builder.AddAzureOpenAIChatCompletion(
                            deploymentName: chatModel,
                            endpoint: azureAPIEndpoint,
                            apiKey: azureAPIKey
                        );
        }
        private static void SetupTextEmbedding(string textEmbeddingModel, string azureAPIEndpoint, string azureAPIKey, IKernelBuilder builder)
        {
            builder.AddAzureOpenAITextEmbeddingGeneration(
                            deploymentName: textEmbeddingModel,
                            endpoint: azureAPIEndpoint,
                            apiKey: azureAPIKey
                        );
        }
        private static void DisplayTitle(string question)
        {
            Console.WriteLine($"โปรแกรมนี้จะทำงาน 2 หน้าที่: {question}");
            Console.WriteLine("");
        }
        private static async Task DisplayResultOfPureChatModel(Kernel kernel, string question)
        {
            Console.WriteLine($"`1. ใช้โมเดล `gpt-4o-mini` โดยตรง");
            Console.WriteLine("--------------------");
            var response = kernel.InvokePromptStreamingAsync(question);
            await foreach (var result in response)
            {
                Console.Write(result);
            }

            // separator
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("==============");
            Console.WriteLine("");
        }
        private static async Task DisplayResultOfChatModelWithTextEmbedding(Kernel kernel, string question)
        {
            Console.WriteLine("2. ใช้โมเดล `gpt-4o-mini` และมี Text Embedding มาช่วยค้นหาประโยคที่เกี่ยวข้อง");
            Console.WriteLine("--------------------");

            // get the embeddings generator service
            var embeddingGenerator = kernel.Services.GetRequiredService<ITextEmbeddingGenerationService>();
            var memory = new SemanticTextMemory(new VolatileMemoryStore(), embeddingGenerator);

            // add facts to the collection
            const string MemoryCollectionName = "fanFacts";

            await memory.SaveInformationAsync(MemoryCollectionName, id: "info1", text: "นัท: อ่านหนังสือ, ดูหนัง, เล่นเกม");
            await memory.SaveInformationAsync(MemoryCollectionName, id: "info2", text: "แพร: ฟังเพลง, วาดรูป, ทำอาหาร");
            await memory.SaveInformationAsync(MemoryCollectionName, id: "info3", text: "ตูน: ท่องเที่ยว, ถ่ายรูป, กินอาหารอร่อย");   
            await memory.SaveInformationAsync(MemoryCollectionName, id: "info4", text: "เบล: เล่นกีฬา, ดูซีรีส์, พบเพื่อน");
            await memory.SaveInformationAsync(MemoryCollectionName, id: "info5", text: "มาย: ทำอาหาร, ช็อปปิ้ง, อ่านนิยาย");

            TextMemoryPlugin memoryPlugin = new(memory);

            // Import the text memory plugin into the Kernel.
            kernel.ImportPluginFromObject(memoryPlugin);

            OpenAIPromptExecutionSettings settings = new()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            };

            var prompt = @"
                Question: {{$input}}
                Answer the question using the memory content: {{Recall}}";

            var arguments = new KernelArguments(settings)
            {
                { "input", question },
                { "collection", MemoryCollectionName }
            };

            var responseTwo = kernel.InvokePromptStreamingAsync(prompt, arguments);
            await foreach (var result in responseTwo)
            {
                Console.Write(result);
            }

            Console.WriteLine($"");
        }
    }
}