using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProcessadorDiagramas.APIGatewayService.Contracts.Events;
using ProcessadorDiagramas.APIGatewayService.Domain.Entities;
using ProcessadorDiagramas.APIGatewayService.Domain.Enums;
using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;
using ProcessadorDiagramas.APIGatewayService.EventHandlers;

namespace ProcessadorDiagramas.APIGatewayService.Tests.EventHandlers;

public sealed class DiagramProcessedEventHandlerTests
{
    private readonly Mock<IDiagramRequestRepository> _repositoryMock;
    private readonly DiagramProcessedEventHandler _handler;

    public DiagramProcessedEventHandlerTests()
    {
        _repositoryMock = new Mock<IDiagramRequestRepository>();
        _handler = new DiagramProcessedEventHandler(
            _repositoryMock.Object,
            NullLogger<DiagramProcessedEventHandler>.Instance);
    }

    [Fact]
    public void EventType_ShouldBeDiagramProcessedEvent()
    {
        _handler.EventType.Should().Be(nameof(DiagramProcessedEvent));
    }

    [Fact]
    public async Task HandleAsync_SuccessEvent_MarksRequestAsAnalyzed()
    {
        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);
        request.MarkAsProcessing();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var @event = new DiagramProcessedEvent(
            request.Id,
            IsSuccess: true,
            ResultUrl: "https://example.com/result.png",
            ErrorMessage: null,
            ProcessedAt: DateTime.UtcNow);

        var payload = JsonSerializer.Serialize(@event);

        await _handler.HandleAsync(payload);

        request.Status.Should().Be(DiagramStatus.Analyzed);
        request.ReportUrl.Should().Be("https://example.com/result.png");
        _repositoryMock.Verify(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_FailureEvent_MarksRequestAsError()
    {
        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);
        request.MarkAsProcessing();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var @event = new DiagramProcessedEvent(
            request.Id,
            IsSuccess: false,
            ResultUrl: null,
            ErrorMessage: "Invalid diagram syntax",
            ProcessedAt: DateTime.UtcNow);

        var payload = JsonSerializer.Serialize(@event);

        await _handler.HandleAsync(payload);

        request.Status.Should().Be(DiagramStatus.Error);
        request.ErrorMessage.Should().Be("Invalid diagram syntax");
        _repositoryMock.Verify(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RequestNotFound_DoesNotThrow()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiagramRequest?)null);

        var @event = new DiagramProcessedEvent(
            Guid.NewGuid(),
            IsSuccess: true,
            ResultUrl: null,
            ErrorMessage: null,
            ProcessedAt: DateTime.UtcNow);

        var act = async () => await _handler.HandleAsync(JsonSerializer.Serialize(@event));

        await act.Should().NotThrowAsync();
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<DiagramRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InvalidPayload_DoesNotThrow()
    {
        var act = async () => await _handler.HandleAsync("not-a-valid-json");

        await act.Should().NotThrowAsync();
    }
}
