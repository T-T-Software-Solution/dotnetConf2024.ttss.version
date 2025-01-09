using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;

#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0050

namespace HelloVector
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            string chatModel, textEmbeddingModel, azureAPIEndpoint, azureAPIKey;

            ReadDataFromConfig(out chatModel, out textEmbeddingModel, out azureAPIEndpoint, out azureAPIKey);

            var builder = Kernel.CreateBuilder();
            SetupChatCompletion(chatModel, azureAPIEndpoint, azureAPIKey, builder);
            SetupTextEmbedding(textEmbeddingModel, azureAPIEndpoint, azureAPIKey, builder);
            Kernel kernel = builder.Build();

            var question = "ใครชอบอาหารหรือของกิน";

            DisplayTitle(question);

            // ทดลองเรียกใช้ Chat Model โดยตรง
            await DisplayResultOfPureChatModel(kernel, question);

            // ทดลองเรียกใช้ Chat Model และ Text Embedding มาช่วย
            var friends = new List<FriendInfo>()
            {
                new FriendInfo { Name = "นัท", Detail = "ชอบอ่านหนังสือ, ชอบดูหนัง, ชอบเล่นเกม" },
                new FriendInfo { Name = "แพร", Detail = "ชอบฟังเพลง, ชอบวาดรูป, ชอบทำอาหาร" },
                new FriendInfo { Name = "ตูน", Detail = "ชอบกินขนม ชอบดื่มกาแฟ ชอบอาหารไทย" },
                new FriendInfo { Name = "เบล", Detail = "ชอบเล่นกีฬา, ชอบดูซีรีส์, ชอบพบเพื่อน" },
                new FriendInfo { Name = "มาย", Detail = "ชอบอาหารณี่ปุ่น, ชอบช็อปปิ้ง, ชอบอ่านนิยาย" }
            };
            await DisplayResultOfChatModelWithTextEmbedding(kernel, question, friends);
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
            Console.WriteLine("");
            Console.WriteLine("==============");
            Console.WriteLine($"{question}");
            Console.WriteLine("==============");
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

        private struct FriendInfo
        {
            public string Name { get; set; }
            public string Detail { get; set; }
        }

        private static async Task DisplayResultOfChatModelWithTextEmbedding(Kernel kernel, string question, List<FriendInfo> friends)
        {
            DisplayVectorEmbeddingTitle();

            DisplayFriendsInfo(friends);

            string memoryCollectionName = "fanFacts";
            var memory = await ConvertFriendsInfoToVectorEmbedding(kernel, friends, memoryCollectionName);

            await DisplayTotalVector(question, memory, memoryCollectionName);

            List<MemoryQueryResult> queryResults = await DisplayRelatedVector(question, memory, memoryCollectionName);

            await RAG(kernel, question, queryResults);
        }
        private static void DisplayVectorEmbeddingTitle()
        {
            Console.WriteLine("2. ใช้โมเดล `gpt-4o-mini` และมี `text-embedding-3-small` มาช่วยแปลงข้อความเป็น Vector และค้นหาประโยคที่เกี่ยวข้อง");
            Console.WriteLine("--------------------");
        }
        private static void DisplayFriendsInfo(List<FriendInfo> friends)
        {
            // Display the results with their relevance scores
            Console.WriteLine("ข้อมูลความชอบของเพื่อนทั้งหมด");
            foreach (var friend in friends)
            {
                Console.WriteLine($"{friend.Name}: {friend.Detail}");
            }

            Console.WriteLine("");
        }
        private static async Task<SemanticTextMemory> ConvertFriendsInfoToVectorEmbedding(Kernel kernel, List<FriendInfo> friends, string memoryCollectionName)
        {
            // get the embeddings generator service
            var embeddingGenerator = kernel.Services.GetRequiredService<ITextEmbeddingGenerationService>();
            var memory = new SemanticTextMemory(new VolatileMemoryStore(), embeddingGenerator);
            
            int infoIndex = 1;
            foreach (var friend in friends)
            {
                await memory.SaveInformationAsync(memoryCollectionName, id: $"info{infoIndex}", text: $"{friend.Name}: {friend.Detail}");

                infoIndex++;
            }

            return memory;
        }
        private static async Task<List<MemoryQueryResult>> DisplayTotalVector(string question, SemanticTextMemory memory, string memoryCollectionName)
        {
            // Set your desired minimum relevance score
            var minRelevanceScore = 0.0;

            // Query the memory store for relevant data
            var queryResults = new List<MemoryQueryResult>();
            await foreach (var result in memory.SearchAsync(
                memoryCollectionName,
                question,
                limit: 10, // Set a high limit to include all relevant data
                minRelevanceScore: minRelevanceScore))
            {
                queryResults.Add(result);
            }

            // Display the results with their relevance scores
            Console.WriteLine("แปลงเป็น Vector Embedding แล้วดูผลลัพธ์ทั้งหมดคะแนนทั้งหมด");
            foreach (var result in queryResults)
            {
                Console.WriteLine($"Relevance Score: {result.Relevance:F2}, Data: {result.Metadata.Text}");
            }

            Console.WriteLine("");
            return queryResults;
        }
        private static async Task<List<MemoryQueryResult>> DisplayRelatedVector(string question, SemanticTextMemory memory, string memoryCollectionName)
        {
            double minRelevanceScore = 0.40;
            List<MemoryQueryResult> queryResults = new List<MemoryQueryResult>();
            await foreach (var result in memory.SearchAsync(
                memoryCollectionName,
                question,
                limit: 3,
                minRelevanceScore: minRelevanceScore))
            {
                queryResults.Add(result);
            }

            // Display the results with their relevance scores
            Console.WriteLine("ดูผลลัพธ์ที่คะแนนมากกว่าหรือเท่ากับ 0.40");
            foreach (var result in queryResults)
            {
                Console.WriteLine($"Relevance Score: {result.Relevance:F2}, Data: {result.Metadata.Text}");
            }

            Console.WriteLine("");
            return queryResults;
        } 
        private static async Task RAG(Kernel kernel, string question, List<MemoryQueryResult> queryResults)
        {
            // Prepare the relevant facts for the prompt
            var relevantFacts = string.Join("\n", queryResults.Select(r => r.Metadata.Text));

            // Construct the prompt for the language model
            var prompt = $@"
                Question: {question}
                Answer the question using the memory content:

                {relevantFacts}

                Answer:";

            Console.WriteLine("สรุปผลจากคำตอบ");
            var response = kernel.InvokePromptStreamingAsync(prompt);
            await foreach (var result in response)
            {
                Console.Write(result);
            }

            Console.WriteLine("");
            Console.WriteLine("");
        }
    }
}