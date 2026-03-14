using FluentAssertions;
using Moq;
using ProcessadorDiagramas.APIGatewayService.Application.Queries.GetDiagramRequest;
using ProcessadorDiagramas.APIGatewayService.Domain.Entities;
using ProcessadorDiagramas.APIGatewayService.Domain.Enums;
using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;

namespace ProcessadorDiagramas.APIGatewayService.Tests.Application;

public sealed class GetDiagramRequestQueryHandlerTests
{
    private readonly Mock<IDiagramRequestRepository> _repositoryMock;
    private readonly GetDiagramRequestQueryHandler _handler;

    public GetDiagramRequestQueryHandlerTests()
    {
        _repositoryMock = new Mock<IDiagramRequestRepository>();
        _handler = new GetDiagramRequestQueryHandler(_repositoryMock.Object);
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
}
