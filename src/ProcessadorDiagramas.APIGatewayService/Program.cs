using Amazon.SimpleNotificationService;
using Amazon.SQS;
using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.APIGatewayService.Application.Commands.CreateDiagramRequest;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Application.Queries.GetAnalysisReport;
using ProcessadorDiagramas.APIGatewayService.Application.Queries.GetDiagramRequest;
using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;
using ProcessadorDiagramas.APIGatewayService.EventHandlers;
using ProcessadorDiagramas.APIGatewayService.Inbox;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Auth;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Clients.Reports;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Data;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Data.Repositories;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Messaging;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Storage;
using ProcessadorDiagramas.APIGatewayService.Outbox;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, options =>
    {
        var authSection = builder.Configuration.GetSection("Auth");
        options.HeaderName = authSection["HeaderName"] ?? "X-Api-Key";
        options.ClientApiKey = authSection["ClientApiKey"] ?? string.Empty;
        options.InternalApiKey = authSection["InternalApiKey"] ?? string.Empty;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.ClientAccess, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(AuthorizationPolicies.InternalAccess, policy =>
        policy.RequireAuthenticatedUser().RequireRole("internal"));
});

// --- Database ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Repositories ---
builder.Services.AddScoped<IDiagramRequestRepository, DiagramRequestRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IInboxRepository, InboxRepository>();
builder.Services.Configure<UploadStorageSettings>(builder.Configuration.GetSection("UploadStorage"));
builder.Services.AddScoped<IDiagramFileStorage, LocalDiagramFileStorage>();

// --- AWS / Messaging ---
var enableAwsServices = !builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("EnableAwsServices", false);

if (enableAwsServices)
{
    builder.Services.Configure<AwsSettings>(builder.Configuration.GetSection("Aws"));
    builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
    builder.Services.AddAWSService<IAmazonSimpleNotificationService>();
    builder.Services.AddAWSService<IAmazonSQS>();
    builder.Services.AddScoped<IMessageBus, AwsMessageBus>();
}
else
{
    // Dummy implementation for testing
    builder.Services.AddScoped<IMessageBus, DummyMessageBus>();
}

// --- Downstream REST clients ---
builder.Services.Configure<ReportServiceSettings>(builder.Configuration.GetSection("ReportService"));
var reportServiceSettings = builder.Configuration.GetSection("ReportService").Get<ReportServiceSettings>() ?? new ReportServiceSettings();
if (reportServiceSettings.UseMock)
{
    builder.Services.AddScoped<IReportServiceClient, MockReportServiceClient>();
}
else
{
    builder.Services.AddHttpClient<IReportServiceClient, HttpReportServiceClient>((serviceProvider, httpClient) =>
    {
        var settings = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<ReportServiceSettings>>()
            .Value;

        if (Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri))
            httpClient.BaseAddress = baseUri;
    });
}

// --- Application handlers ---
builder.Services.AddScoped<CreateDiagramRequestCommandHandler>();
builder.Services.AddScoped<GetDiagramRequestQueryHandler>();
builder.Services.AddScoped<GetAnalysisReportQueryHandler>();
builder.Services.AddScoped<OutboxPublisher>();

// --- Event handlers ---
builder.Services.AddScoped<DiagramProcessedEventHandler>();
builder.Services.AddScoped<IEventHandler>(serviceProvider =>
    serviceProvider.GetRequiredService<DiagramProcessedEventHandler>());

// --- Background workers ---
if (enableAwsServices)
{
    builder.Services.AddHostedService<OutboxWorker>();
    builder.Services.AddHostedService<InboxConsumer>();
}

// --- API ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ProcessadorDiagramas API Gateway", Version = "v1" });

    c.AddSecurityDefinition(ApiKeyAuthenticationHandler.SchemeName, new OpenApiSecurityScheme
    {
        Description = "Informe a API key no header X-Api-Key.",
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = ApiKeyAuthenticationHandler.SchemeName
                }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

// Required for integration test WebApplicationFactory
public partial class Program { }
