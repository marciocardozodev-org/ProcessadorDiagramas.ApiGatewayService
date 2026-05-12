using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProcessadorDiagramas.APIGatewayService.API.Controllers;
using ProcessadorDiagramas.APIGatewayService.API.DTOs;
using ProcessadorDiagramas.APIGatewayService.Domain.Entities;
using ProcessadorDiagramas.APIGatewayService.Domain.Enums;
using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;
using ProcessadorDiagramas.APIGatewayService.EventHandlers;

namespace ProcessadorDiagramas.APIGatewayService.Tests.API;

public sealed class InternalDiagramEventsControllerTests
{
    [Fact]
    public async Task DiagramProcessed_SuccessPayload_ReturnsAcceptedAndMarksRequestAsAnalyzed()
    {
        var repositoryMock = new Mock<IDiagramRequestRepository>();
        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);
        request.MarkAsProcessing();

        repositoryMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var handler = new DiagramProcessedEventHandler(
            repositoryMock.Object,
            NullLogger<DiagramProcessedEventHandler>.Instance);
        var controller = new InternalDiagramEventsController(handler);
        var processedAt = new DateTime(2026, 5, 12, 10, 15, 0, DateTimeKind.Utc);

        var response = await controller.DiagramProcessed(
            new SimulateDiagramProcessedRequestDto
            {
                DiagramRequestId = request.Id,
                IsSuccess = true,
                ResultUrl = "https://reports.local/analysis-123",
                ProcessedAt = processedAt
            },
            CancellationToken.None);

        var acceptedResult = response.Should().BeOfType<AcceptedResult>().Subject;
        request.Status.Should().Be(DiagramStatus.Analyzed);
        request.ReportUrl.Should().Be("https://reports.local/analysis-123");
        acceptedResult.Value.Should().NotBeNull();
        repositoryMock.Verify(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DiagramProcessed_FailurePayload_ReturnsAcceptedAndMarksRequestAsError()
    {
        var repositoryMock = new Mock<IDiagramRequestRepository>();
        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);
        request.MarkAsProcessing();

        repositoryMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var handler = new DiagramProcessedEventHandler(
            repositoryMock.Object,
            NullLogger<DiagramProcessedEventHandler>.Instance);
        var controller = new InternalDiagramEventsController(handler);

        var response = await controller.DiagramProcessed(
            new SimulateDiagramProcessedRequestDto
            {
                DiagramRequestId = request.Id,
                IsSuccess = false,
                ErrorMessage = "Falha de validacao"
            },
            CancellationToken.None);

        response.Should().BeOfType<AcceptedResult>();
        request.Status.Should().Be(DiagramStatus.Error);
        request.ErrorMessage.Should().Be("Falha de validacao");
        repositoryMock.Verify(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }
}