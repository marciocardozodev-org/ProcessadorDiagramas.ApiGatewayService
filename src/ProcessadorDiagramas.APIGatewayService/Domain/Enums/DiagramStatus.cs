namespace ProcessadorDiagramas.APIGatewayService.Domain.Enums;

public enum DiagramStatus
{
    /// <summary>Request received and queued for processing.</summary>
    Received = 1,
    /// <summary>Being processed by downstream service.</summary>
    Processing = 2,
    /// <summary>Analysis completed successfully.</summary>
    Analyzed = 3,
    /// <summary>Processing failed.</summary>
    Error = 4
}
