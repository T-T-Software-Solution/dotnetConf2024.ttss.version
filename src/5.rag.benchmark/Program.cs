﻿#pragma warning disable
using System.Diagnostics;
using Microsoft.SemanticKernel.Connectors.InMemory;

class Program
{
    public static string LogFilePath { get; set; }

    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        PrepareLogFile();

        // Configurations for Azure OpenAI and Ollama
        string azureChatModel, azureTextEmbeddingModel, azureAPIEndpoint, azureAPIKey;
        string ollamaChatModel, ollamaTextEmbeddingModel, ollamaAPIEndpoint;

        Utils.ReadDataFromConfig(
            out azureChatModel, out azureTextEmbeddingModel, out azureAPIEndpoint, out azureAPIKey,
            out ollamaChatModel, out ollamaTextEmbeddingModel, out ollamaAPIEndpoint);

        AzureOpenAIClient azureAI;
        IChatClient azureChatClient, ollamaChatClient;
        IEmbeddingGenerator<string, Embedding<float>> azureEmbeddingGenerator, ollamaEmbeddingGenerator;
        PrepareLLMs(azureChatModel, azureTextEmbeddingModel, azureAPIEndpoint, azureAPIKey, ollamaChatModel, ollamaTextEmbeddingModel, ollamaAPIEndpoint, 
            out azureAI, out azureChatClient, out ollamaChatClient, out azureEmbeddingGenerator, out ollamaEmbeddingGenerator);

        bool skipEmbed = false;

        // Parse arguments
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--skip-embed", StringComparison.OrdinalIgnoreCase))
            {
                skipEmbed = true;
            }
        }

        try
        {
            if (!skipEmbed)
            {
                await ReadPDFAndConvertVector("Step1_Azure OpenAI Read PDF English and convert Vector", azureEmbeddingGenerator, isAzure: true, isEnglish: true);
                await ReadPDFAndConvertVector("Step2_Azure OpenAI Read PDF Thai and convert Vector", azureEmbeddingGenerator, isAzure: true, isEnglish: false);
                await ReadPDFAndConvertVector("Step3_Ollama Read PDF English and convert Vector", ollamaEmbeddingGenerator, isAzure: false, isEnglish: true);
                await ReadPDFAndConvertVector("Step4_Ollama Read PDF Thai and convert Vector", ollamaEmbeddingGenerator, isAzure: false, isEnglish: false);
            }
            else
            {
                string skipLog = LogMessage("INFO", "Step1_Azure OpenAI Read PDF English and convert Vector", "Skipped embedding step. Using previously generated data.");
                LogToFile(skipLog);
                Console.WriteLine(skipLog);

                string skipLog2 = LogMessage("INFO", "Step2_Azure OpenAI Read PDF Thai and convert Vector", "Skipped embedding step. Using previously generated data.");
                LogToFile(skipLog2);
                Console.WriteLine(skipLog2);

                string skipLog3 = LogMessage("INFO", "Step3_Ollama Read PDF English and convert Vector", "Skipped embedding step. Using previously generated data.");
                LogToFile(skipLog3);
                Console.WriteLine(skipLog3);

                string skipLog4 = LogMessage("INFO", "Step4_Ollama Read PDF Thai and convert Vector", "Skipped embedding step. Using previously generated data.");
                LogToFile(skipLog4);
                Console.WriteLine(skipLog4);
            }

            await RAG("Step5_Azure OpenAI RAG English", azureEmbeddingGenerator, azureChatClient, isAzure: true, isEnglish: true);
            await RAG("Step6_Azure OpenAI RAG Thai", azureEmbeddingGenerator, azureChatClient, isAzure: true, isEnglish: false);
            await RAG("Step7_Ollama RAG English", ollamaEmbeddingGenerator, ollamaChatClient, isAzure: false, isEnglish: true);
            await RAG("Step8_Ollama RAG Thai", ollamaEmbeddingGenerator, ollamaChatClient, isAzure: false, isEnglish: false);

            Console.WriteLine("All steps completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error occurred: " + ex.Message);
            return 1;
        }
    }

    private static void PrepareLLMs(string azureChatModel, string azureTextEmbeddingModel, string azureAPIEndpoint, string azureAPIKey, string ollamaChatModel, string ollamaTextEmbeddingModel, string ollamaAPIEndpoint, 
        out AzureOpenAIClient azureAI, out IChatClient azureChatClient, out IChatClient ollamaChatClient, 
        out IEmbeddingGenerator<string, Embedding<float>> azureEmbeddingGenerator, out IEmbeddingGenerator<string, Embedding<float>> ollamaEmbeddingGenerator)
    {
        // Step 1: Data Preparation
        // Create chat client and embedding generator for Azure OpenAI
        azureAI = Utils.CreateAzureOpenAIClient(azureAPIEndpoint, azureAPIKey);
        azureChatClient = azureAI.AsChatClient(azureChatModel);
        azureEmbeddingGenerator = azureAI.AsEmbeddingGenerator(azureTextEmbeddingModel);

        // Create chat client and embedding generator for Ollama
        ollamaChatClient = new OllamaApiClient(new Uri(ollamaAPIEndpoint), ollamaChatModel);
        ollamaEmbeddingGenerator = new OllamaApiClient(new Uri(ollamaAPIEndpoint), ollamaTextEmbeddingModel);
    }

    private static async Task ReadPDFAndConvertVector(string stepName, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, bool isAzure, bool isEnglish)
    {
        Stopwatch stopwatch;

        LogStart(stepName, out stopwatch);

        try
        {
            string pdfManualPath = isEnglish ? "manuals" : "manuals_thai";
            string pdfFilePath = Path.Combine("data", pdfManualPath);

            string llmProvider = isAzure ? "azure" : "ollama";
            string language = isEnglish ? "english" : "thai";
            string chunkFilePath = Path.Combine("data", $"manual-chunks-{llmProvider}-{language}.json");

            var manualIngestor = new ManualIngestor(embeddingGenerator);
            await manualIngestor.RunAsync(pdfFilePath, "./data", chunkFilePath);
        }
        catch (Exception ex)
        {
            LogError(stepName, stopwatch, ex);

            throw;
        }

        LogEnd(stepName, stopwatch);
    }
    private static async Task RAG(string stepName, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, IChatClient chatClient, bool isAzure, bool isEnglish)
    {
        Stopwatch stopwatch;

        Console.WriteLine();
        Console.WriteLine();
        
        LogStart(stepName, out stopwatch);

        try
        {
            // Configure product manual service
            var vectorStore = new InMemoryVectorStore();
            var productManualService = new ProductManualService(embeddingGenerator, vectorStore, isAzure);

            var pdfChunks = "";
            if (isAzure && isEnglish)
                pdfChunks = "manual-chunks-azure-english.json";
            else if (isAzure && !isEnglish)
                pdfChunks = "manual-chunks-azure-thai.json";
            else if (!isAzure && isEnglish)
                pdfChunks = "manual-chunks-ollama-english.json";
            else
                pdfChunks = "manual-chunks-ollama-thai.json";

            var prompt = "Please specify the product url.";
            if(!isEnglish)
                prompt = "กรุณาบอกรายละเอียดของแพ็กเกจประกันสุขภาพ ";

            Utils.LoadManualsIntoVectorStore(Path.Combine("data", pdfChunks), productManualService);

            await Utils.RAGAsync(productManualService, chatClient, prompt);
        }
        catch (Exception ex)
        {
            LogError(stepName, stopwatch, ex);

            throw;
        }

        LogEnd(stepName, stopwatch);
    }
    private static void LogError(string stepName, Stopwatch stopwatch, Exception ex)
    {
        stopwatch.Stop();
        string errorLog = LogMessage("ERROR", stepName, ex.Message);
        LogToFile(errorLog);
    }

    private static void LogStart(string stepName, out Stopwatch stopwatch)
    {
        string startLog = LogMessage("INFO", stepName, $"Start embedding process.");
        LogToFile(startLog);
        Console.WriteLine(startLog);

        stopwatch = new Stopwatch();
        stopwatch.Start();
    }
    private static void LogEnd(string stepName, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        string successLog = LogMessage("INFO", stepName, $"Completed in {stopwatch.Elapsed.TotalSeconds:0.00} seconds. {Environment.NewLine}");
        LogToFile(successLog);
        Console.WriteLine(successLog);
    }
    private static void PrepareLogFile()
    {
        if(Directory.Exists("logs") == false)
        {
            Directory.CreateDirectory("logs");
        }

        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        LogFilePath = Path.Combine("logs", $"logs_{timestamp}.log");
    }
    static void LogToFile(string content)
    {
        File.AppendAllText(LogFilePath, content + Environment.NewLine);
    }
    static string LogMessage(string level, string stepName, string message)
    {
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        return $"[{timestamp}] [{level}] [{stepName}] {message}";
    }
}