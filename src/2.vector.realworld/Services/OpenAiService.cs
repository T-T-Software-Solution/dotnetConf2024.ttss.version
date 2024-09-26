using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using VectorApp.Models;

namespace VectorApp.Services
{
    public class OpenAiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public OpenAiService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config["OpenAI:ApiKey"]}");
        }

        // Property to check if the OpenAI service is available
        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                // Send a simple GET request to the OpenAI API
                var response = await _httpClient.GetAsync("https://api.openai.com/v1/models");

                // Return true if the request is successful (status code 200-299)
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                // Return false if any exception occurs (network issues, API down, etc.)
                return false;
            }
        }

        // Generates an embedding vector for the given text input
        public async Task<List<double>> GenerateEmbeddingAsync(string text)
        {
            var requestBody = new
            {
                input = text,
                model = _config["OpenAI:EmbeddingModel"]
            };

            var response = await _httpClient.PostAsJsonAsync("https://api.openai.com/v1/embeddings", requestBody);

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            // Deserialize JSON content into the defined OpenAiEmbeddingResponse class
            var result = JsonSerializer.Deserialize<OpenAiEmbeddingResponse>(content);

            // Return the embedding from the first data element
            return result?.Data[0].Embedding ?? new List<double>();
        }

        public enum ChatContext
        {
            Company,
            Travel
        }

        // Generates a chat completion response based on user question and context
        public async Task<string> GetChatCompletionResponseAsync(string userQuestion, string context, ChatContext chatContext)
        {
            // Define system message content based on the chat context
            string systemMessage;
            switch (chatContext)
            {
                case ChatContext.Company:
                    systemMessage = "คุณคือที่ปรึกษาของบริษัท T.T.Software กรุณาตอบคำถามเกี่ยวกับบริษัทฯ เป็นภาษาไทย. คุณเป็นผู้ชาย. ตอบอย่างกระชับและตรงประเด็น.";
                    break;
                case ChatContext.Travel:
                    systemMessage = "คุณคือที่ปรึกษาด้านการท่องเที่ยวในประเทศไทย กรุณาตอบคำถามเกี่ยวกับสถานที่ท่องเที่ยวในประเทศไทย. ตอบอย่างสนุกสนานและเป็นกันเอง. ตอบเฉพาะข้อมูลที่มีใน Context. ห้ามตอบโดยใช้ข้อมูลนอกเหนือจากใน Context. คุณเป็นผู้หญิง.";
                    break;
                default:
                    systemMessage = "คุณคือที่ปรึกษา. กรุณาตอบคำถามตามบริบทที่กำหนด.";
                    break;
            }

            // Create a list of messages for the chat completion request
            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = systemMessage },
                new ChatMessage { Role = "system", Content = $"Context: {context}" },
                new ChatMessage { Role = "user", Content = userQuestion }
            };

            // Create the request body object
            var requestBody = new OpenAiChatCompletionRequest
            {
                Model = _config["OpenAI:ChatCompletionModel"],
                Messages = messages,
                Temperature = double.Parse(_config["OpenAI:Temperature"])
            };

            // Serialize the request body to JSON and send the POST request
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            // Deserialize the JSON response into the OpenAiChatCompletionResponse class
            var result = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(content);

            // Return the content of the first choice's message
            return result?.Choices?[0].Message.Content ?? "No response";
        }

        // Analyzes user input for travel-related queries and returns structured information
        public async Task<TravelAnalysisResult?> AnalyzeUserInputAsync(string userInput)
        {
            var placeTypes = "ศูนย์กีฬา/สนามกีฬาสถานที่ทางการกีฬาทางบก/ทางน้ำ/ทางอากาศ, ธีมปาร์ค (Theme Park), หมู่เกาะ, โรงละคร/โรงมหรสพ (โชว์), วนอุทยาน, ศูนย์ศึกษาธรรมชาติ/พิพิธภัณธ์ธรรมชาติ (พืชพันธุ์), สวนสัตว์/ศูนย์ฝึกสัตว์/พิพิธภัณฑ์สัตว์, ประวัติความเป็นมา, โครงการหลวง/โครงการพระราชดำริ, สวนสนุก/สวนน้ำ, ศูนย์หัตถกรรม, แม่น้ำ/ลำคลอง/แก่ง, ชุมชนโบราณ/โบราณสถาน/โบราณวัตถุ, ภูเขา/ธรณีสัณฐานเฉพาะ, กำแพงเมือง/คูเมือง, ไร่/สวนเกษตร (ฟาร์มสัตว์/ประมง), พรุ/ป่าชายเลน/พื้นที่ชุ่มน้ำ, น้ำตก, ถ้ำ, ศูนย์การเรียนรู้ฯ (เกี่ยวกับกิจกรรม ผลิตภัณฑ์ และภูมิปัญญาในท้องถิ่น), สวนสาธารณะ/สวนหย่อม, พิพิธภัณฑ์, ศูนย์วัฒนธรรม, พระตำหนัก/วัง/พระราชวัง, ศาสนสถาน (วัด/โบสถ์/มัสยิด ฯลฯ), จุดชมวิว, วิถีชีวิตความเป็นอยู่ (ชุมชน), ห้างสรรพสินค้า/แหล่งช้อปปิ้ง/ตลาดสด/ตลาดนัด/ถนนคนเดิน, ทุ่งดอกไม้และพืชพันธุ์, อุทยานแห่งชาติ, แหล่งปะการังน้ำลึก/น้ำตื้น, สุขภาพและความงาม, อนุสาวรีย์/อนุสรณ์สถาน, เขื่อน/อ่างเก็บน้ำ, อ่าว/หาดทราย/ชายทะเล, สถานปฏิบัติธรรม, จุดผ่านแดน/ชายแดน, ศูนย์วิจัย (เกษตร)/สถานีทดลอง (เกษตร), น้ำพุร้อน/บ่อน้ำร้อน/ธารน้ำ, เขตรักษาพันธุ์สัตว์ป่า, หมู่บ้าน, ทะเลสาบ/หนอง/บึง, ตลาดน้ำ/ตลาดโบราณ";
            var regions = "ภาคใต้, ภาคกลาง, ภาคตะวันออก, ภาคเหนือ, ภาคตะวันออกเฉียงเหนือ";
            var provinces = "บุรีรัมย์,สุรินทร์,สุราษฎร์ธานี,อุดรธานี,ชลบุรี,จันทบุรี,ตรัง,สระบุรี,ระยอง,น่าน,ประจวบคีรีขันธ์,พระนครศรีอยุธยา,เชียงใหม่,พิษณุโลก,เชียงราย,ลำปาง,ราชบุรี,สงขลา,ตาก,แพร่,นครสวรรค์,อุบลราชธานี,สตูล,กระบี่,นครปฐม,ฉะเชิงเทรา,นครราชสีมา,นนทบุรี,เพชรบุรี,พัทลุง,พิจิตร,สุพรรณบุรี,ปทุมธานี,เลย,สมุทรปราการ,สมุทรสาคร,ลพบุรี,ศรีสะเกษ,อ่างทอง,กรุงเทพมหานคร,หนองคาย,นครนายก,ขอนแก่น,ระนอง,กาฬสินธุ์,สระแก้ว,ร้อยเอ็ด,กาญจนบุรี,แม่ฮ่องสอน,ปัตตานี,ปราจีนบุรี,สกลนคร,เพชรบูรณ์,ลำพูน,นครศรีธรรมราช,ยะลา,ภูเก็ต,ชุมพร,นครพนม,นราธิวาส,หนองบัวลำภู,พังงา,มุกดาหาร,ตราด,อุตรดิตถ์,ชัยนาท,สุโขทัย,สิงห์บุรี,อำนาจเจริญ,พะเยา,บึงกาฬ,ยโสธร,ชัยภูมิ,สมุทรสงคราม,อุทัยธานี,กำแพงเพชร,มหาสารคาม";

            // Construct the improved prompt for AI to analyze the user input
            var prompt = $"คุณคือผู้ช่วย AI ที่สามารถวิเคราะห์ข้อความเกี่ยวกับการท่องเที่ยวในประเทศไทยได้. " +
                         $"จากข้อความที่ได้รับ, กรุณาตอบคำถามต่อไปนี้โดยใช้ข้อมูลจากข้อความต้นฉบับเท่านั้น. " +
                         $"โปรดแสดงคำตอบในรูปแบบ JSON และระบุข้อมูลตามที่ร้องขอเท่านั้น. " +
                         $"หากไม่มีข้อมูล, โปรดใส่ 'null'. " +
                         $"ค่าที่เป็นไปได้สำหรับแต่ละฟิลด์:\n" +
                         $"- \"month\": \"มกราคม, กุมภาพันธ์, มีนาคม, เมษายน, พฤษภาคม, มิถุนายน, กรกฎาคม, สิงหาคม, กันยายน, ตุลาคม, พฤศจิกายน, ธันวาคม, ทั้งปี\"\n" +
                         $"- \"day\": \"จันทร์, อังคาร, พุธ, พฤหัสบดี, ศุกร์, เสาร์, อาทิตย์, ทุกวัน\"\n" +
                         $"- \"time\": \"เช้า, บ่าย, เย็น, ค่ำ, กลางคืน, ตลอดเวลา, ทั้งวัน\"\n" +
                         $"- \"place_type\": \"{placeTypes}\"\n" +
                         $"- \"region\": \"{regions}\"\n" +
                         $"- \"province\": \"{provinces}\"\n" +
                         $"ตัวอย่าง:\n\n" +
                         $"ข้อความ: \"ต้องการหาที่เที่ยว ในจันทบุรี ที่อยู่ติดทะเล\"\n" +
                         $"Output:\n" +
                         $"{{\n" +
                         $"\"is_travel_related\": true,\n" +
                         $"\"place_name\": null,\n" +
                         $"\"region\": \"ภาคตะวันออก\",\n" +
                         $"\"province\": \"จันทบุรี\",\n" +
                         $"\"district\": null,\n" +
                         $"\"place_type\": \"อ่าว/หาดทราย/ชายทะเล\",\n" +
                         $"\"month\": null,\n" +
                         $"\"day\": null,\n" +
                         $"\"time\": null\n" +
                         $"}}\n\n" +
                         $"ข้อความ: \"ไปเที่ยวเชียงใหม่เดือนธันวาคม อากาศดี\"\n" +
                         $"Output:\n" +
                         $"{{\n" +
                         $"\"is_travel_related\": true,\n" +
                         $"\"place_name\": null,\n" +
                         $"\"region\": \"ภาคเหนือ\",\n" +
                         $"\"province\": \"เชียงใหม่\",\n" +
                         $"\"district\": null,\n" +
                         $"\"place_type\": null,\n" +
                         $"\"month\": \"ธันวาคม\",\n" +
                         $"\"day\": null,\n" +
                         $"\"time\": null\n" +
                         $"}}\n\n" +                         
                         $"ข้อความ: \"บริษัท T.T.Software มีบริการอะไรบ้าง\"\n" +
                         $"Output:\n" +
                         $"{{\n" +
                         $"\"is_travel_related\": false,\n" +
                         $"\"place_name\": null,\n" +
                         $"\"region\": null,\n" +
                         $"\"province\": null,\n" +
                         $"\"district\": null,\n" +
                         $"\"place_type\": null,\n" +
                         $"\"month\": null,\n" +
                         $"\"day\": null,\n" +
                         $"\"time\": null\n" +
                         $"}}\n\n" +
                         $"ตอนนี้กรุณาวิเคราะห์ข้อความต่อไปนี้และแสดงคำตอบในรูปแบบ JSON:\n\n" +
                         $"ข้อความ: \"{userInput}\"\n\n" +
                         $"Output:";

            // Create a list of messages for the chat completion request
            var messages = new
            {
                model = _config["OpenAI:ChatCompletionModel"],
                messages = new[]
                {
            new { role = "system", content = "คุณเป็นผู้ช่วย AI ที่สามารถวิเคราะห์ข้อความในรูปแบบ JSON" },
            new { role = "user", content = prompt }
        }
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(messages), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", requestContent);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var openAiResponse = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(responseContent);

            var analysisResultJson = openAiResponse?.Choices?[0]?.Message?.Content?.Trim();
            if (string.IsNullOrEmpty(analysisResultJson))
            {
                return null;
            }

            analysisResultJson = analysisResultJson.Trim();
            var analysisResult = JsonSerializer.Deserialize<TravelAnalysisResult>(analysisResultJson);

            // skip region if provide province
            if (!string.IsNullOrEmpty(analysisResult.Province))
                analysisResult.Region = null;

            return analysisResult;
        }


    }
}
