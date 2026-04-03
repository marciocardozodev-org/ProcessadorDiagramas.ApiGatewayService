using FluentAssertions;
using ProcessadorDiagramas.UploadOrchestratorService.Domain.Entities;
using ProcessadorDiagramas.UploadOrchestratorService.Domain.Enums;
using ProcessadorDiagramas.UploadOrchestratorService.Domain.Exceptions;

namespace ProcessadorDiagramas.UploadOrchestratorService.Tests.Domain;

public class AnalysisProcessTests
{
    [Fact]
    public void Deve_criar_processo_com_status_recebido_e_historico_inicial()
    {
        var now = new DateTime(2026, 4, 2, 10, 30, 0, DateTimeKind.Utc);

        var process = AnalysisProcess.CreateReceived(
            Guid.NewGuid(),
            "arquitetura.png",
            "uploads/2026/04/02/arquivo.png",
            "image/png",
            1024,
            "corr-123",
            now);

        process.Status.Should().Be(AnalysisProcessStatus.Recebido);
        process.CreatedAtUtc.Should().Be(now);
        process.UpdatedAtUtc.Should().Be(now);
        process.FailureReason.Should().BeNull();
        process.StatusHistory.Should().HaveCount(1);
        process.StatusHistory.Single().Status.Should().Be(AnalysisProcessStatus.Recebido);
    }

    [Fact]
    public void Deve_transicionar_de_recebido_para_em_processamento_e_analisado()
    {
        var process = AnalysisProcess.CreateReceived(
            Guid.NewGuid(),
            "diagrama.pdf",
            "uploads/arquivo.pdf",
            "application/pdf",
            2048,
            "corr-456",
            new DateTime(2026, 4, 2, 11, 0, 0, DateTimeKind.Utc));

        process.MarkAsProcessing(new DateTime(2026, 4, 2, 11, 5, 0, DateTimeKind.Utc));
        process.MarkAsAnalyzed(new DateTime(2026, 4, 2, 11, 10, 0, DateTimeKind.Utc));

        process.Status.Should().Be(AnalysisProcessStatus.Analisado);
        process.StatusHistory.Should().HaveCount(3);
        process.StatusHistory.Last().Status.Should().Be(AnalysisProcessStatus.Analisado);
    }

    [Fact]
    public void Deve_falhar_ao_tentar_transicao_invalida_de_recebido_para_analisado()
    {
        var process = AnalysisProcess.CreateReceived(
            Guid.NewGuid(),
            "diagrama.pdf",
            "uploads/arquivo.pdf",
            "application/pdf",
            2048,
            "corr-789");

        var act = () => process.MarkAsAnalyzed();

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*Transicao invalida*");
    }

    [Fact]
    public void Deve_exigir_motivo_quando_status_for_erro()
    {
        var process = AnalysisProcess.CreateReceived(
            Guid.NewGuid(),
            "diagrama.pdf",
            "uploads/arquivo.pdf",
            "application/pdf",
            2048,
            "corr-999");

        var act = () => process.MarkAsFailed(" ");

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*Motivo da falha*obrigatorio*");
    }
}
