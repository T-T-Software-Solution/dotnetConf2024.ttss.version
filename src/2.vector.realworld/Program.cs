using VectorApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddSingleton<OpenAiService>();
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<LineService>(); // Register LineService
builder.Services.AddHttpClient<OpenAiService>();
builder.Services.AddHttpClient<LineService>(); // Add HttpClient for LineService
builder.Services.AddControllers();

var app = builder.Build();

app.UseRouting();
app.MapControllers();
app.Run();
