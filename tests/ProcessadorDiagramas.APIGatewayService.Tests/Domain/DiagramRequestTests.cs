using FluentAssertions;
using ProcessadorDiagramas.APIGatewayService.Domain.Entities;
using ProcessadorDiagramas.APIGatewayService.Domain.Enums;

namespace ProcessadorDiagramas.APIGatewayService.Tests.Domain;

public sealed class DiagramRequestTests
{
    [Fact]
    public void Create_ValidContent_ReturnsReceivedRequest()
    {
        var request = DiagramRequest.Create("@startuml\nActorA -> ActorB\n@enduml", DiagramFormat.PlantUML);

        request.Id.Should().NotBeEmpty();
        request.Status.Should().Be(DiagramStatus.Received);
        request.Format.Should().Be(DiagramFormat.PlantUML);
        request.DiagramContent.Should().NotBeNullOrWhiteSpace();
        request.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        request.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void CreateFromUpload_ValidFileMetadata_ReturnsReceivedRequestWithFileData()
    {
        var request = DiagramRequest.CreateFromUpload(
            "/tmp/uploads/diagram-uploaded.png",
            "diagram.png",
            1024,
            "image/png",
            "Architecture",
            "Payment flow");

        request.Status.Should().Be(DiagramStatus.Received);
        request.FileName.Should().Be("diagram.png");
        request.FileSize.Should().Be(1024);
        request.ContentType.Should().Be("image/png");
        request.StoragePath.Should().Be("/tmp/uploads/diagram-uploaded.png");
        request.Name.Should().Be("Architecture");
        request.Description.Should().Be("Payment flow");
        request.DiagramContent.Should().Be("/tmp/uploads/diagram-uploaded.png");
        request.Format.Should().Be(DiagramFormat.DrawIo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void Create_EmptyContent_ThrowsArgumentException(string? content)
    {
        var act = () => DiagramRequest.Create(content!, DiagramFormat.Mermaid);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateFromUpload_InvalidFileSize_ThrowsArgumentException()
    {
        var act = () => DiagramRequest.CreateFromUpload(
            "/tmp/uploads/diagram-uploaded.png",
            "diagram.png",
            0,
            "image/png",
            null,
            null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsProcessing_FromReceived_SetsProcessingStatus()
    {
        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);

        request.MarkAsProcessing();

        request.Status.Should().Be(DiagramStatus.Processing);
        request.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsProcessing_FromNonReceived_ThrowsInvalidOperation()
    {
        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);
        request.MarkAsProcessing();

        var act = () => request.MarkAsProcessing();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsAnalyzed_FromProcessing_SetsAnalyzedStatus()
    {
        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);
        request.MarkAsProcessing();

        request.MarkAsAnalyzed("https://reports.example.com/1");

        request.Status.Should().Be(DiagramStatus.Analyzed);
        request.ReportUrl.Should().Be("https://reports.example.com/1");
        request.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsAnalyzed_FromReceived_ThrowsInvalidOperation()
    {
        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);

        var act = () => request.MarkAsAnalyzed();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsError_FromAnyStatus_SetsErrorStatus()
    {
        var request = DiagramRequest.Create("graph TD; A-->B;", DiagramFormat.Mermaid);

        request.MarkAsError("Timeout during rendering");

        request.Status.Should().Be(DiagramStatus.Error);
        request.ErrorMessage.Should().Be("Timeout during rendering");
        request.UpdatedAt.Should().NotBeNull();
    }
}
