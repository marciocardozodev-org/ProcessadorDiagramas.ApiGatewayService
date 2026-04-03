using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.UploadOrchestratorService.Application.Interfaces;
using ProcessadorDiagramas.UploadOrchestratorService.Infrastructure.Data;
using ProcessadorDiagramas.UploadOrchestratorService.Infrastructure.Data.Repositories;

namespace ProcessadorDiagramas.UploadOrchestratorService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IAnalysisProcessRepository, AnalysisProcessRepository>();
        services.AddScoped<IAnalysisProcessStatusHistoryRepository, AnalysisProcessStatusHistoryRepository>();

        return services;
    }
}
