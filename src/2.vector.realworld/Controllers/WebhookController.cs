using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Text.Json;
using Newtonsoft.Json;
using VectorApp.Models;
using VectorApp.Services;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace VectorApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly MongoDbService _mongoDbService;
        private readonly OpenAiService _openAiService;
        private readonly LineService _lineService;
        private readonly IConfiguration _config;

        public WebhookController(MongoDbService mongoDbService, OpenAiService openAiService, LineService lineService, IConfiguration config)
        {
            _mongoDbService = mongoDbService;
            _openAiService = openAiService;
            _lineService = lineService;
            _config = config;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] JsonElement jsonElement)
        {
            // Read the request body as a string
            var requestBody = jsonElement.ToString();

            // Retrieve the 'X-Line-Signature' header
            var signature = Request.Headers["X-Line-Signature"].ToString();

            // Verify the signature
            if (!_lineService.VerifySignature(requestBody, signature))
            {
                // Return Unauthorized response if the signature verification fails
                return Unauthorized(new { status = "failed", message = "Invalid signature" });
            }

            // Deserialize JSON to LineEvent class
            var lineEvent = JsonConvert.DeserializeObject<LineEvent>(requestBody);

            if (lineEvent?.Events != null && lineEvent.Events.Count > 0)
            {
                var userMessage = lineEvent.Events[0].Message.Text;
                var replyToken = lineEvent.Events[0].ReplyToken;

                if (!string.IsNullOrEmpty(userMessage))
                {
                    // Central Decision Logic
                    var analyzeResult = await _openAiService.AnalyzeUserInputAsync(userMessage);

                    if (analyzeResult.IsTravelRelated)
                    {
                        await HandleTravelQuery(userMessage, replyToken, analyzeResult);
                    }
                    else
                    {
                        await HandleGeneralQuery(userMessage, replyToken);
                    }
                }
                else
                {
                    // Send a response when no context is found
                    await _lineService.SendLineReplyAsync(replyToken, "ขอบคุณที่ให้ความสนใจใน T.T. Software ต้องการสอบถามข้อมูลด้านใดครับ");
                }
            }

            return Ok(new { status = "success" });
        }

        private async Task HandleGeneralQuery(string userMessage, string replyToken)
        {
            // Generate embedding using OpenAI service
            var embedding = await _openAiService.GenerateEmbeddingAsync(userMessage);

            // Search for similar texts in MongoDB
            var similarTexts = await _mongoDbService.SearchSimilarTextsAsync(embedding, 3);

            // Number each item in the context
            var context = string.Join("\n", similarTexts.Select((text, index) => $"{index + 1}. {text.Text}"));

            // Get response from OpenAI based on context
            var response = await _openAiService.GetChatCompletionResponseAsync(userMessage, context, OpenAiService.ChatContext.Company);

            if (Convert.ToBoolean(_config["Debug"]))
            {
                response += "\nContext:\n" + context;  // Include context in response for debugging
            }

            // Send response back to LINE API
            await _lineService.SendLineReplyAsync(replyToken, response + "\n\nศึกษาข้อมูลเพิ่มเติมได้ที่ http://www.tt-ss.net");
        }

        private async Task HandleTravelQuery(string userMessage, string replyToken, TravelAnalysisResult analyzeResult)
        {
            List<double> embedding;

            // Generate embedding using OpenAI service for travel context
            embedding = await _openAiService.GenerateEmbeddingAsync(userMessage);

            // Search for similar travel texts in MongoDB
            var similarTexts = await _mongoDbService.SearchByAnalysisAndSimilarityAsync(analyzeResult, embedding, 3);

            // Number each item in the context
            var context = string.Join("\n", similarTexts.Select((text, index) => $"\n{index + 1}. {text.FormattedDescription}"));

            if (!string.IsNullOrEmpty(context))
            {
                // Get response from OpenAI based on travel context
                var response = await _openAiService.GetChatCompletionResponseAsync(userMessage, context, OpenAiService.ChatContext.Travel);

                response = analyzeResult.ConcatMessage + "\n\n" + response + "\n\nข้อมูลอ้างอิงจาก การท่องเที่ยวแห่งประเทศไทย (ททท.) แสดง 3 อันดับล่าสุด ที่ใกล้เคียงเงื่อนไขที่ได้รับ\n" + context + "\n\nแหล่งข้อมูลอ้างอิง https://datacatalog.tat.or.th/dataset/tourist-attraction/resource/82f307c8-490e-432c-a613-be7bf841860a";

                // Send response back to LINE API
                await _lineService.SendLineReplyAsync(replyToken, response);
            }
            else
            {
                await _lineService.SendLineReplyAsync(replyToken, analyzeResult.ConcatMessage + "\n\nไม่มีแหล่งท่องเที่ยวที่คุณกำลังสนใจ ในฐานข้อมูล");
            }
        }

        // Health check endpoint
        [HttpGet("health")]
        public async Task<IActionResult> HealthCheckAsync()
        {
            try
            {
                // Check the health of your services or return a simple OK message
                var healthStatus = new
                {
                    status = "Healthy",
                    database = _mongoDbService.IsConnected ? "Connected" : "Disconnected",
                    openAiService = await _openAiService.IsServiceAvailableAsync() ? "Available" : "Unavailable",
                    timestamp = DateTime.UtcNow
                };

                // Return the health status as a JSON response
                return Ok(healthStatus);
            }
            catch (Exception ex)
            {
                var healthStatus = new
                {
                    status = "Bad",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                };

                return StatusCode(500, healthStatus);
            }            
        }
    }
}
