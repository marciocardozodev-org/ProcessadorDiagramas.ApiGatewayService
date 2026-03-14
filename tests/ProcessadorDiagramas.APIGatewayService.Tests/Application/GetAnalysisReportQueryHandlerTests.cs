using FluentAssertions;
using Moq;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Application.Queries.GetAnalysisReport;
using ProcessadorDiagramas.APIGatewayService.Domain.Entities;
using ProcessadorDiagramas.APIGatewayService.Domain.Enums;
using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;

namespace ProcessadorDiagramas.APIGatewayService.Tests.Application;

public sealed class GetAnalysisReportQueryHandlerTests
{
    private readonly Mock<IDiagramRequestRepository> _repositoryMock;
    private readonly Mock<IReportServiceClient> _reportClientMock;
    private readonly GetAnalysisReportQueryHandler _handler;

    public GetAnalysisReportQueryHandlerTests()
    {
        _repositoryMock = new Mock<IDiagramRequestRepository>();
        _reportClientMock = new Mock<IReportServiceClient>();
        _handler = new GetAnalysisReportQueryHandler(_repositoryMock.Object, _reportClientMock.Object);
    }

    [Fact]
    public async Task HandleAsync_RequestNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiagramRequest?)null);

        var result = await _handler.HandleAsync(new GetAnalysisReportQuery(id));

        result.Outcome.Should().Be(GetAnalysisReportOutcome.NotFound);
        result.Report.Should().BeNull();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task HandleAsync_RequestNotAnalyzed_ReturnsNotReady()
    {
        var request = DiagramRequest.Create("@startuml\nA->B\n@enduml", DiagramFormat.PlantUML);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _handler.HandleAsync(new GetAnalysisReportQuery(request.Id));

        result.Outcome.Should().Be(GetAnalysisReportOutcome.NotReady);
        result.Report.Should().BeNull();
        result.Message.Should().Contain(DiagramStatus.Received.ToString());
    }

    [Fact]
    public async Task HandleAsync_AnalyzedRequest_WithExternalReport_ReturnsReadyWithDetails()
    {
        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);
        request.MarkAsProcessing();
        request.MarkAsAnalyzed("https://reports.local/local-fallback");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var external = new TechnicalReportDto(
            request.Id,
            "Architecture findings",
            "Detected missing retry policy in integration flow.",
            DateTime.UtcNow,
            "https://reports.local/analysis-123");

        _reportClientMock
            .Setup(c => c.GetReportAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(external);

        var result = await _handler.HandleAsync(new GetAnalysisReportQuery(request.Id));

        result.Outcome.Should().Be(GetAnalysisReportOutcome.Ready);
        result.Report.Should().NotBeNull();
        result.Report!.Status.Should().Be(DiagramStatus.Analyzed.ToString());
        result.Report.Summary.Should().Be("Architecture findings");
        result.Report.ReportUrl.Should().Be("https://reports.local/analysis-123");
    }

    [Fact]
    public async Task HandleAsync_AnalyzedRequest_WithoutExternalReport_ReturnsReadyWithFallback()
    {
        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);
        request.MarkAsProcessing();
        request.MarkAsAnalyzed("https://reports.local/fallback-only");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        _reportClientMock
            .Setup(c => c.GetReportAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TechnicalReportDto?)null);

        var result = await _handler.HandleAsync(new GetAnalysisReportQuery(request.Id));

        result.Outcome.Should().Be(GetAnalysisReportOutcome.Ready);
        result.Report.Should().NotBeNull();
        result.Report!.ReportUrl.Should().Be("https://reports.local/fallback-only");
        result.Report.Summary.Should().BeNull();
        result.Report.Details.Should().BeNull();
    }
}
