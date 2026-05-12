using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProcessadorDiagramas.APIGatewayService.API.Controllers;
using ProcessadorDiagramas.APIGatewayService.API.DTOs;
using ProcessadorDiagramas.APIGatewayService.Application.Commands.CreateDiagramRequest;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Application.Queries.GetAnalysisReport;
using ProcessadorDiagramas.APIGatewayService.Application.Queries.GetDiagramRequest;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Data;
using ProcessadorDiagramas.APIGatewayService.Outbox;
using ProcessadorDiagramas.APIGatewayService.Domain.Entities;
using ProcessadorDiagramas.APIGatewayService.Domain.Enums;
using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Data.Repositories;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Storage;

using static ProcessadorDiagramas.APIGatewayService.Tests.API.DiagramRequestsControllerTests.TestHelpers;

namespace ProcessadorDiagramas.APIGatewayService.Tests.API;

public sealed class DiagramRequestsControllerTests
{
    [Fact]
    public async Task Create_ValidMultipartUpload_ReturnsCreatedAndPersistsMetadata()
    {
        var repositoryMock = new Mock<IDiagramRequestRepository>();
        var outboxRepositoryMock = new Mock<IOutboxRepository>();
        var reportClientMock = new Mock<IReportServiceClient>();
        var fileStorageMock = new Mock<IDiagramFileStorage>();

        DiagramRequest? capturedRequest = null;

        repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<DiagramRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DiagramRequest, CancellationToken>((request, _) => capturedRequest = request)
            .Returns(Task.CompletedTask);

        outboxRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<Stream>(), "architecture.png", "image/png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredDiagramFile(
                "/tmp/uploads/architecture-uploaded.png",
                "architecture.png",
                1024,
                "image/png"));

        using var dbContext = BuildInMemoryDbContext();
        var orchMock = BuildOrchestrationMock();
        var createHandler = new CreateDiagramRequestCommandHandler(
            repositoryMock.Object,
            new OutboxPublisher(outboxRepositoryMock.Object),
            dbContext,
            orchMock,
            NullLogger<CreateDiagramRequestCommandHandler>.Instance);

        var statusHandler = new GetDiagramRequestQueryHandler(repositoryMock.Object, orchMock);
        var reportHandler = new GetAnalysisReportQueryHandler(repositoryMock.Object, reportClientMock.Object);

        var controller = new DiagramRequestsController(
            createHandler,
            statusHandler,
            reportHandler,
            fileStorageMock.Object);

        using var fileContent = new MemoryStream(new byte[1024]);
        var formFile = new FormFile(fileContent, 0, fileContent.Length, "file", "architecture.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var dto = new CreateDiagramRequestDto
        {
            File = formFile,
            Name = "Architecture V1",
            Description = "Initial architecture diagram"
        };

        var response = await controller.Create(dto, CancellationToken.None);

        response.Should().BeOfType<CreatedAtActionResult>();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.FileName.Should().Be("architecture.png");
        capturedRequest.FileSize.Should().Be(1024);
        capturedRequest.ContentType.Should().Be("image/png");
        capturedRequest.StoragePath.Should().Be("/tmp/uploads/architecture-uploaded.png");
        capturedRequest.Name.Should().Be("Architecture V1");
        capturedRequest.Description.Should().Be("Initial architecture diagram");
        capturedRequest.Status.Should().Be(DiagramStatus.Received);

        fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), "architecture.png", "image/png", It.IsAny<CancellationToken>()),
            Times.Once);

        outboxRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_FileLargerThan10Mb_ReturnsBadRequest()
    {
        var repositoryMock = new Mock<IDiagramRequestRepository>();
        var reportClientMock = new Mock<IReportServiceClient>();
        var fileStorageMock = new Mock<IDiagramFileStorage>();

        var controller = BuildGetOnlyController(repositoryMock.Object, reportClientMock.Object, fileStorageMock.Object);

        using var fileContent = new MemoryStream(new byte[(10 * 1024 * 1024) + 1]);
        var formFile = new FormFile(fileContent, 0, fileContent.Length, "file", "architecture.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var dto = new CreateDiagramRequestDto { File = formFile };

        var response = await controller.Create(dto, CancellationToken.None);

        response.Should().BeOfType<BadRequestObjectResult>();
        fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_UnsupportedContentType_ReturnsBadRequest()
    {
        var repositoryMock = new Mock<IDiagramRequestRepository>();
        var reportClientMock = new Mock<IReportServiceClient>();
        var fileStorageMock = new Mock<IDiagramFileStorage>();

        var controller = BuildGetOnlyController(repositoryMock.Object, reportClientMock.Object, fileStorageMock.Object);

        using var fileContent = new MemoryStream(new byte[128]);
        var formFile = new FormFile(fileContent, 0, fileContent.Length, "file", "notes.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var dto = new CreateDiagramRequestDto { File = formFile };

        var response = await controller.Create(dto, CancellationToken.None);

        response.Should().BeOfType<BadRequestObjectResult>();
        fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_NameTooLong_ReturnsBadRequest()
    {
        var repositoryMock = new Mock<IDiagramRequestRepository>();
        var reportClientMock = new Mock<IReportServiceClient>();
        var fileStorageMock = new Mock<IDiagramFileStorage>();

        var controller = BuildGetOnlyController(repositoryMock.Object, reportClientMock.Object, fileStorageMock.Object);

        using var fileContent = new MemoryStream(new byte[128]);
        var formFile = new FormFile(fileContent, 0, fileContent.Length, "file", "architecture.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var dto = new CreateDiagramRequestDto
        {
            File = formFile,
            Name = new string('a', 201)
        };

        var response = await controller.Create(dto, CancellationToken.None);

        response.Should().BeOfType<BadRequestObjectResult>();
        fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_DescriptionTooLong_ReturnsBadRequest()
    {
        var repositoryMock = new Mock<IDiagramRequestRepository>();
        var reportClientMock = new Mock<IReportServiceClient>();
        var fileStorageMock = new Mock<IDiagramFileStorage>();

        var controller = BuildGetOnlyController(repositoryMock.Object, reportClientMock.Object, fileStorageMock.Object);

        using var fileContent = new MemoryStream(new byte[128]);
        var formFile = new FormFile(fileContent, 0, fileContent.Length, "file", "architecture.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var dto = new CreateDiagramRequestDto
        {
            File = formFile,
            Description = new string('b', 1001)
        };

        var response = await controller.Create(dto, CancellationToken.None);

        response.Should().BeOfType<BadRequestObjectResult>();
        fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_ValidMultipartUpload_WithLocalStorage_PersistsFileAndOutboxMessage()
    {
        var uploadRoot = Path.Combine(Path.GetTempPath(), "diagram-upload-tests", Guid.NewGuid().ToString("N"));

        try
        {
            using var dbContext = BuildInMemoryDbContext();

            var repository = new DiagramRequestRepository(dbContext);
            var outboxRepository = new OutboxRepository(dbContext);
            var fileStorage = new LocalDiagramFileStorage(
                Options.Create(new UploadStorageSettings { RootPath = uploadRoot }),
                NullLogger<LocalDiagramFileStorage>.Instance);
            var orchMock = BuildOrchestrationMock();
            var createHandler = new CreateDiagramRequestCommandHandler(
                repository,
                new OutboxPublisher(outboxRepository),
                dbContext,
                orchMock,
                NullLogger<CreateDiagramRequestCommandHandler>.Instance);
            var statusHandler = new GetDiagramRequestQueryHandler(repository, orchMock);
            var reportHandler = new GetAnalysisReportQueryHandler(repository, Mock.Of<IReportServiceClient>());
            var controller = new DiagramRequestsController(createHandler, statusHandler, reportHandler, fileStorage);

            var expectedBytes = new byte[] { 1, 2, 3, 4, 5, 6 };
            using var fileContent = new MemoryStream(expectedBytes);
            var formFile = new FormFile(fileContent, 0, fileContent.Length, "file", "architecture.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            };

            var dto = new CreateDiagramRequestDto
            {
                File = formFile,
                Name = "Architecture V2",
                Description = "Uploaded through integration flow"
            };

            var response = await controller.Create(dto, CancellationToken.None);

            var createdResult = response.Should().BeOfType<CreatedAtActionResult>().Subject;
            var payload = createdResult.Value.Should().BeOfType<CreateDiagramRequestResponse>().Subject;

            var persistedRequest = await dbContext.DiagramRequests.SingleAsync();
            var outboxMessage = await dbContext.OutboxMessages.SingleAsync();

            payload.Id.Should().Be(persistedRequest.Id);
            persistedRequest.FileName.Should().Be("architecture.png");
            persistedRequest.FileSize.Should().Be(expectedBytes.Length);
            persistedRequest.ContentType.Should().Be("image/png");
            persistedRequest.Name.Should().Be("Architecture V2");
            persistedRequest.Description.Should().Be("Uploaded through integration flow");
            persistedRequest.StoragePath.Should().NotBeNullOrWhiteSpace();
            persistedRequest.StoragePath.Should().StartWith(uploadRoot);
            File.Exists(persistedRequest.StoragePath).Should().BeTrue();
            (await File.ReadAllBytesAsync(persistedRequest.StoragePath!)).Should().Equal(expectedBytes);

            outboxMessage.EventType.Should().Be("DiagramRequestCreatedEvent");
            outboxMessage.Payload.Should().Contain("architecture.png");
            outboxMessage.Payload.Should().Contain("image/png");
            outboxMessage.Payload.Should().Contain("Architecture V2");
        }
        finally
        {
            if (Directory.Exists(uploadRoot))
                Directory.Delete(uploadRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetById_ExistingRequest_ReturnsOk()
    {
        var repositoryMock = new Mock<IDiagramRequestRepository>();
        var reportClientMock = new Mock<IReportServiceClient>();
        var fileStorageMock = new Mock<IDiagramFileStorage>();

        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);
        repositoryMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var controller = BuildGetOnlyController(repositoryMock.Object, reportClientMock.Object, fileStorageMock.Object);

        var response = await controller.GetById(request.Id, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        var payload = ((OkObjectResult)response).Value;
        payload.Should().BeOfType<AnalysisStatusResponse>();
    }

    [Fact]
    public async Task GetById_MissingRequest_ReturnsNotFound()
    {
        var repositoryMock = new Mock<IDiagramRequestRepository>();
        var reportClientMock = new Mock<IReportServiceClient>();
        var fileStorageMock = new Mock<IDiagramFileStorage>();

        repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiagramRequest?)null);

        var controller = BuildGetOnlyController(repositoryMock.Object, reportClientMock.Object, fileStorageMock.Object);

        var response = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetReport_AnalysisNotReady_ReturnsConflict()
    {
        var repositoryMock = new Mock<IDiagramRequestRepository>();
        var reportClientMock = new Mock<IReportServiceClient>();
        var fileStorageMock = new Mock<IDiagramFileStorage>();

        var request = DiagramRequest.Create("@startuml\nA->B\n@enduml", DiagramFormat.PlantUML);
        repositoryMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var controller = BuildGetOnlyController(repositoryMock.Object, reportClientMock.Object, fileStorageMock.Object);

        var response = await controller.GetReport(request.Id, CancellationToken.None);

        response.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task GetReport_AnalyzedRequest_ReturnsOk()
    {
        var repositoryMock = new Mock<IDiagramRequestRepository>();
        var reportClientMock = new Mock<IReportServiceClient>();
        var fileStorageMock = new Mock<IDiagramFileStorage>();

        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);
        request.MarkAsProcessing();
        request.MarkAsAnalyzed("https://reports.local/fallback");

        repositoryMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        reportClientMock
            .Setup(r => r.GetReportAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TechnicalReportDto(
                request.Id,
                "Final report",
                "Everything looks good",
                DateTime.UtcNow,
                "https://reports.local/final"));

        var controller = BuildGetOnlyController(repositoryMock.Object, reportClientMock.Object, fileStorageMock.Object);

        var response = await controller.GetReport(request.Id, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        var payload = ((OkObjectResult)response).Value;
        payload.Should().BeOfType<AnalysisReportResponse>();
    }

    private static DiagramRequestsController BuildGetOnlyController(
        IDiagramRequestRepository repository,
        IReportServiceClient reportServiceClient,
        IDiagramFileStorage fileStorage)
    {
        var orchMock = BuildOrchestrationMock();
        var statusHandler = new GetDiagramRequestQueryHandler(repository, orchMock);
        var reportHandler = new GetAnalysisReportQueryHandler(repository, reportServiceClient);

        return new DiagramRequestsController(null!, statusHandler, reportHandler, fileStorage);
    }

    private static AppDbContext BuildInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    internal static class TestHelpers
    {
        /// <summary>Returns a mock orchestration client that always returns null (uses local DB fallback).</summary>
        internal static IUploadOrquestracaoServiceClient BuildOrchestrationMock()
        {
            var mock = new Mock<IUploadOrquestracaoServiceClient>();
            mock.Setup(c => c.GetProcessStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((OrchestrationProcessDto?)null);
            mock.Setup(c => c.RegisterUploadAsync(It.IsAny<RegisterUploadRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((OrchestrationProcessDto?)null);
            return mock.Object;
        }
    }
}
