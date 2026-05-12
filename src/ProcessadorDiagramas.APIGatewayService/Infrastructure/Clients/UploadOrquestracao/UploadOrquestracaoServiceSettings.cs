namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Clients.UploadOrquestracao;

public sealed class UploadOrquestracaoServiceSettings
{
    /// <summary>Base URL of the UploadOrquestracaoService, e.g. http://upload-orquestracao:8082</summary>
    public string BaseUrl { get; set; } = "http://localhost:8082";

    /// <summary>Path to register a new upload. POST {BaseUrl}{RegisterUploadPath}</summary>
    public string RegisterUploadPath { get; set; } = "/api/uploads";

    /// <summary>Path template to query process status. GET {BaseUrl}{GetStatusPathTemplate}</summary>
    public string GetStatusPathTemplate { get; set; } = "/api/uploads/{processId}";

    /// <summary>When true, uses MockUploadOrquestracaoServiceClient (no HTTP calls).</summary>
    public bool UseMock { get; set; } = true;
}
