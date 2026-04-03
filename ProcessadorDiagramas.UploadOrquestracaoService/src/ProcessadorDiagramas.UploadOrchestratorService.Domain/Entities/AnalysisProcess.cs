using ProcessadorDiagramas.UploadOrchestratorService.Domain.Enums;
using ProcessadorDiagramas.UploadOrchestratorService.Domain.Exceptions;

namespace ProcessadorDiagramas.UploadOrchestratorService.Domain.Entities;

public sealed class AnalysisProcess
{
    private readonly List<AnalysisProcessStatusHistory> _statusHistory = [];

    private AnalysisProcess()
    {
        OriginalFileName = string.Empty;
        StoredFileKey = string.Empty;
        ContentType = string.Empty;
        CorrelationId = string.Empty;
    }

    private AnalysisProcess(
        Guid id,
        string originalFileName,
        string storedFileKey,
        string contentType,
        long fileSize,
        AnalysisProcessStatus status,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        string? failureReason,
        string correlationId)
    {
        Id = id;
        OriginalFileName = originalFileName;
        StoredFileKey = storedFileKey;
        ContentType = contentType;
        FileSize = fileSize;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        FailureReason = failureReason;
        CorrelationId = correlationId;
    }

    public Guid Id { get; private set; }
    public string OriginalFileName { get; private set; }
    public string StoredFileKey { get; private set; }
    public string ContentType { get; private set; }
    public long FileSize { get; private set; }
    public AnalysisProcessStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public string CorrelationId { get; private set; }
    public IReadOnlyCollection<AnalysisProcessStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public static AnalysisProcess CreateReceived(
        Guid id,
        string originalFileName,
        string storedFileKey,
        string contentType,
        long fileSize,
        string correlationId,
        DateTime? nowUtc = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Id do processo e obrigatorio.");
        }

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new DomainValidationException("Nome original do arquivo e obrigatorio.");
        }

        if (string.IsNullOrWhiteSpace(storedFileKey))
        {
            throw new DomainValidationException("Chave do arquivo armazenado e obrigatoria.");
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new DomainValidationException("ContentType e obrigatorio.");
        }

        if (fileSize <= 0)
        {
            throw new DomainValidationException("FileSize deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new DomainValidationException("CorrelationId e obrigatorio.");
        }

        var now = (nowUtc ?? DateTime.UtcNow).ToUniversalTime();

        var process = new AnalysisProcess(
            id,
            originalFileName.Trim(),
            storedFileKey.Trim(),
            contentType.Trim(),
            fileSize,
            AnalysisProcessStatus.Recebido,
            now,
            now,
            null,
            correlationId.Trim());

        process.AddHistory(AnalysisProcessStatus.Recebido, now, null);

        return process;
    }

    public void MarkAsProcessing(DateTime? nowUtc = null)
    {
        EnsureTransition(AnalysisProcessStatus.EmProcessamento);

        var now = (nowUtc ?? DateTime.UtcNow).ToUniversalTime();

        Status = AnalysisProcessStatus.EmProcessamento;
        UpdatedAtUtc = now;
        FailureReason = null;
        AddHistory(AnalysisProcessStatus.EmProcessamento, now, null);
    }

    public void MarkAsAnalyzed(DateTime? nowUtc = null)
    {
        EnsureTransition(AnalysisProcessStatus.Analisado);

        var now = (nowUtc ?? DateTime.UtcNow).ToUniversalTime();

        Status = AnalysisProcessStatus.Analisado;
        UpdatedAtUtc = now;
        FailureReason = null;
        AddHistory(AnalysisProcessStatus.Analisado, now, null);
    }

    public void MarkAsFailed(string failureReason, DateTime? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new DomainValidationException("Motivo da falha e obrigatorio para status Erro.");
        }

        EnsureTransition(AnalysisProcessStatus.Erro);

        var now = (nowUtc ?? DateTime.UtcNow).ToUniversalTime();

        Status = AnalysisProcessStatus.Erro;
        UpdatedAtUtc = now;
        FailureReason = failureReason.Trim();
        AddHistory(AnalysisProcessStatus.Erro, now, FailureReason);
    }

    private void EnsureTransition(AnalysisProcessStatus targetStatus)
    {
        var isValidTransition = Status switch
        {
            AnalysisProcessStatus.Recebido => targetStatus is AnalysisProcessStatus.EmProcessamento or AnalysisProcessStatus.Erro,
            AnalysisProcessStatus.EmProcessamento => targetStatus is AnalysisProcessStatus.Analisado or AnalysisProcessStatus.Erro,
            AnalysisProcessStatus.Analisado => false,
            AnalysisProcessStatus.Erro => false,
            _ => false
        };

        if (!isValidTransition)
        {
            throw new DomainValidationException($"Transicao invalida de '{Status}' para '{targetStatus}'.");
        }
    }

    private void AddHistory(AnalysisProcessStatus status, DateTime changedAtUtc, string? reason)
    {
        _statusHistory.Add(new AnalysisProcessStatusHistory(
            Guid.NewGuid(),
            Id,
            status,
            changedAtUtc,
            reason));
    }
}
