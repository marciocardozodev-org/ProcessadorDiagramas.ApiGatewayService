using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using ProcessadorDiagramas.APIGatewayService.Application.Commands.CreateDiagramRequest;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Contracts.Events;
using ProcessadorDiagramas.APIGatewayService.Domain.Entities;
using ProcessadorDiagramas.APIGatewayService.Domain.Enums;
using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Data;
using ProcessadorDiagramas.APIGatewayService.Outbox;

namespace ProcessadorDiagramas.APIGatewayService.Tests.Application;

public sealed class CreateDiagramRequestCommandHandlerTests
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<IDiagramRequestRepository> _repositoryMock;
    private readonly Mock<IOutboxRepository> _outboxRepositoryMock;
    private readonly CreateDiagramRequestCommandHandler _handler;

    public CreateDiagramRequestCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _repositoryMock = new Mock<IDiagramRequestRepository>();
        _outboxRepositoryMock = new Mock<IOutboxRepository>();

        var outboxPublisher = new OutboxPublisher(_outboxRepositoryMock.Object);

        _handler = new CreateDiagramRequestCommandHandler(
            _repositoryMock.Object,
            outboxPublisher,
            _dbContext);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_ReturnsReceivedResponse()
    {
        var command = new CreateDiagramRequestCommand(
            "/tmp/uploads/diagram-123.png",
            "diagram.png",
            2048,
            "image/png",
            "Architecture",
            "Checkout flow diagram");

        DiagramRequest? capturedRequest = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<DiagramRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DiagramRequest, CancellationToken>((req, _) => capturedRequest = req)
            .Returns(Task.CompletedTask);

        _outboxRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Status.Should().Be(DiagramStatus.Received.ToString());
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        capturedRequest.Should().NotBeNull();
        capturedRequest!.FileName.Should().Be("diagram.png");
        capturedRequest.FileSize.Should().Be(2048);
        capturedRequest.ContentType.Should().Be("image/png");
        capturedRequest.StoragePath.Should().Be("/tmp/uploads/diagram-123.png");
        capturedRequest.Name.Should().Be("Architecture");
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_AddsOutboxMessageWithCorrectEventType()
    {
        var command = new CreateDiagramRequestCommand(
            "/tmp/uploads/diagram-456.pdf",
            "diagram.pdf",
            4096,
            "application/pdf",
            null,
            null);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<DiagramRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        OutboxMessage? capturedOutbox = null;
        _outboxRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((msg, _) => capturedOutbox = msg)
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(command);

        _outboxRepositoryMock.Verify(r => r.AddAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        capturedOutbox.Should().NotBeNull();
        capturedOutbox!.EventType.Should().Be(nameof(DiagramRequestCreatedEvent));
        capturedOutbox.Payload.Should().Contain("diagram.pdf");
        capturedOutbox.Payload.Should().Contain("application/pdf");
    }

    [Fact]
    public async Task HandleAsync_InvalidStoragePath_ThrowsArgumentException()
    {
        var command = new CreateDiagramRequestCommand(
            string.Empty,
            "diagram.pdf",
            1024,
            "application/pdf",
            null,
            null);

        var act = async () => await _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
