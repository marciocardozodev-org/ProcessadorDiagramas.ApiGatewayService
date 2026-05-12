using FluentAssertions;
using Moq;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Application.Queries.GetDiagramRequest;
using ProcessadorDiagramas.APIGatewayService.Domain.Entities;
using ProcessadorDiagramas.APIGatewayService.Domain.Enums;
using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;

namespace ProcessadorDiagramas.APIGatewayService.Tests.Application;

public sealed class GetDiagramRequestQueryHandlerTests
{
    private readonly Mock<IDiagramRequestRepository> _repositoryMock;
    private readonly Mock<IUploadOrquestracaoServiceClient> _orchestrationMock;
    private readonly GetDiagramRequestQueryHandler _handler;

    public GetDiagramRequestQueryHandlerTests()
    {
        _repositoryMock = new Mock<IDiagramRequestRepository>();
        _orchestrationMock = new Mock<IUploadOrquestracaoServiceClient>();
        // Default: orchestration service returns null (mock/unavailable) so tests use local DB.
        _orchestrationMock
            .Setup(c => c.GetProcessStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrchestrationProcessDto?)null);
        _handler = new GetDiagramRequestQueryHandler(_repositoryMock.Object, _orchestrationMock.Object);
    }

    [Fact]
    public async Task HandleAsync_RequestNotFound_ReturnsNull()
    {
        var id = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiagramRequest?)null);

        var result = await _handler.HandleAsync(new GetDiagramRequestQuery(id));

        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_RequestExists_ReturnsAnalysisStatusResponse()
    {
        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);
        request.MarkAsProcessing();
        request.MarkAsError("Renderer timeout");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _handler.HandleAsync(new GetDiagramRequestQuery(request.Id));

        result.Should().NotBeNull();
        result!.Id.Should().Be(request.Id);
        result.Status.Should().Be(DiagramStatus.Error.ToString());
        result.Format.Should().Be(DiagramFormat.Mermaid.ToString());
        result.ErrorMessage.Should().Be("Renderer timeout");
        result.ReportUrl.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_OrchestrationServiceAvailable_ReturnsOrchestrationStatus()
    {
        var processId = Guid.NewGuid();
        var orchestrationDto = new OrchestrationProcessDto(
            processId,
            "Processing",
            DateTime.UtcNow.AddMinutes(-2),
            DateTime.UtcNow.AddMinutes(-1),
            null,
            null);

        _orchestrationMock
            .Setup(c => c.GetProcessStatusAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orchestrationDto);

        // Local DB still needed for format resolution.
        var localRequest = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(localRequest);

        var result = await _handler.HandleAsync(new GetDiagramRequestQuery(processId));

        result.Should().NotBeNull();
        result!.Id.Should().Be(processId);
        result.Status.Should().Be("Processing");
        result.Format.Should().Be(DiagramFormat.Mermaid.ToString());
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_OrchestrationUnavailable_FallsBackToLocalDb()
    {
        // _orchestrationMock already returns null (set in constructor).
        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);
        request.MarkAsProcessing();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _handler.HandleAsync(new GetDiagramRequestQuery(request.Id));

        result.Should().NotBeNull();
        result!.Status.Should().Be("Processing");
        result.Format.Should().Be(DiagramFormat.Mermaid.ToString());
    }
}
