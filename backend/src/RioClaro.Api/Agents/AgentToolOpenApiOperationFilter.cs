using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RioClaro.Api.Agents;

internal sealed class AgentToolOpenApiOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var relativePath = context.ApiDescription.RelativePath ?? string.Empty;
        if (!relativePath.StartsWith(
                "api/agent-tools/",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "AgentToolBearer"
                }
            }] = Array.Empty<string>()
        });

        operation.Responses.TryAdd("401", new OpenApiResponse
        {
            Description = "Unauthorized"
        });
        operation.Responses.TryAdd("503", new OpenApiResponse
        {
            Description = "Agent tools are not configured"
        });
    }
}
