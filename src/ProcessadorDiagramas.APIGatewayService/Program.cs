using Amazon.SimpleNotificationService;
using Amazon.SQS;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.APIGatewayService.Application.Commands.CreateDiagramRequest;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Application.Queries.GetAnalysisReport;
using ProcessadorDiagramas.APIGatewayService.Application.Queries.GetDiagramRequest;
using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;
using ProcessadorDiagramas.APIGatewayService.EventHandlers;
using ProcessadorDiagramas.APIGatewayService.Inbox;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Clients.Reports;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Data;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Data.Repositories;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Messaging;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Storage;
using ProcessadorDiagramas.APIGatewayService.Outbox;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.Configure<AwsSettings>(builder.Configuration.GetSection("Aws"));
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonSimpleNotificationService>();
builder.Services.AddAWSService<IAmazonSQS>();
builder.Services.AddScoped<IMessageBus, AwsMessageBus>();

// --- Downstream REST clients ---
builder.Services.Configure<ReportServiceSettings>(builder.Configuration.GetSection("ReportService"));
builder.Services.AddHttpClient<IReportServiceClient, HttpReportServiceClient>((serviceProvider, httpClient) =>
{
    var settings = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<ReportServiceSettings>>()
        .Value;

    if (Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri))
        httpClient.BaseAddress = baseUri;
});

// --- Application handlers ---
builder.Services.AddScoped<CreateDiagramRequestCommandHandler>();
builder.Services.AddScoped<GetDiagramRequestQueryHandler>();
builder.Services.AddScoped<GetAnalysisReportQueryHandler>();
builder.Services.AddScoped<OutboxPublisher>();

// --- Event handlers ---
builder.Services.AddScoped<IEventHandler, DiagramProcessedEventHandler>();

// --- Background workers ---
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<InboxConsumer>();

// --- API ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ProcessadorDiagramas API Gateway", Version = "v1" });

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

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Required for integration test WebApplicationFactory
public partial class Program { }
