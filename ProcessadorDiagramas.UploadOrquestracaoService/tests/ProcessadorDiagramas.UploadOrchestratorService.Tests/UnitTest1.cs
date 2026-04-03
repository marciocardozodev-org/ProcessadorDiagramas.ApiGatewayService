using FluentAssertions;
using ProcessadorDiagramas.UploadOrchestratorService.Api;

namespace ProcessadorDiagramas.UploadOrchestratorService.Tests;

public class SolutionSmokeTests
{
    [Fact]
    public void Deve_carregar_o_assembly_da_api()
    {
        typeof(AssemblyMarker).Assembly.FullName.Should().NotBeNullOrWhiteSpace();
    }
}