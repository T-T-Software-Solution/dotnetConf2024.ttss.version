using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace VectorApp.Services
{
    public class LineService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public LineService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public bool VerifySignature(string requestBody, string receivedSignature)
        {
            // Retrieve the Channel Secret from the configuration
            var channelSecret = _config["Line:ChannelSecret"];
            var secretKey = Encoding.UTF8.GetBytes(channelSecret);

            // Compute the HMAC-SHA256 hash of the request body
            using (var hmac = new HMACSHA256(secretKey))
            {
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestBody));
                var computedSignature = Convert.ToBase64String(computedHash);

                // Compare the computed signature with the received signature
                return computedSignature == receivedSignature;
            }
        }

        public async Task SendLineReplyAsync(string replyToken, string message)
        {
            var url = "https://api.line.me/v2/bot/message/reply";
            var accessToken = _config["Line:ChannelAccessToken"]; // Get the LINE channel access token from configuration

            var headers = new
            {
                ContentType = "application/json",
                Authorization = $"Bearer {accessToken}"
            };

            var payload = new
            {
                replyToken = replyToken,
                messages = new[]
                {
                    new { type = "text", text = message }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Reply sent successfully.");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to send reply: {response.StatusCode}, {errorContent}");
            }
        }
    }
}
