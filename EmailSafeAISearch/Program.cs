using EmailSafeAISearch.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Presidio (detects PII)
builder.Services.AddHttpClient<IPiiDetector, PresidioClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Presidio:BaseUrl"]!));

// Hide / restore in C#
builder.Services.AddScoped<IPiiService, PiiService>();

// Gemini: answers and embeddings
builder.Services.AddSingleton<ILlmClient, GeminiClient>();
builder.Services.AddSingleton<IEmbeddingClient, GeminiEmbeddingClient>();

// RAG: in-memory email index (built once) + full ask flow
builder.Services.AddSingleton<EmailIndex>();
builder.Services.AddScoped<IAskService, AskService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

app.Run();