namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Messaging;

public sealed class AwsSettings
{
    public string Region { get; set; } = string.Empty;
    public string TopicArn { get; set; } = string.Empty;
    public string QueueUrl { get; set; } = string.Empty;

    // Used in local development with LocalStack
    public string? ServiceURL { get; set; }
}
