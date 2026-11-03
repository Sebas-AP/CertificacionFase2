// Crea/actualiza los agentes "rio-claro-business-agent" y "rio-claro-invoice-agent"
// directamente con el SDK Azure.AI.Projects.Agents, sin pasar por `azd up`.
//
// Por qué existe este script: a 2026-11-03, el proveedor `microsoft.foundry` de azd
// (probado en azd 1.36.0 y 1.37.2) genera una plantilla ARM interna cuya variable
// `uniqueSuffix` usa la función `resourceGroup()` en un scope de suscripción, donde
// no es válida (error ARM "InvalidTemplate" / "RESOURCEGROUP function not expected").
// azd no permite inspeccionar ni corregir esa plantilla (es generada en memoria y
// `azd infra generate` no la soporta para este proveedor). Mientras Microsoft no
// corrija el bug, los recursos "core" de Foundry (cuenta AIServices + modelo +
// proyecto + conexión) se crean con infra/foundry-manual.bicep, y los agentes
// (que azd crearía vía su propio flujo) se crean con este script.
//
// Requiere: `az login` ya hecho (usa DefaultAzureCredential -> AzureCliCredential),
// y permisos de administrador sobre el recurso Foundry (no basta el rol de runtime
// "Azure AI User"/"Foundry User" que usa el backend).
//
// Variables de entorno requeridas:
//   FOUNDRY_PROJECT_ENDPOINT   p.ej. https://<account>.cognitiveservices.azure.com/api/projects/<project>
//   FOUNDRY_CONNECTION_ID      ARM resource ID completo de la conexión rio-claro-agent-tools-auth
//                              (.../accounts/<account>/projects/<project>/connections/rio-claro-agent-tools-auth)
//   FOUNDRY_MODEL_DEPLOYMENT_NAME   p.ej. gpt-5-mini
//   AGENT_TOOLS_BASE_URL       URL pública HTTPS del backend ya desplegado

using System.Text.Json;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using YamlDotNet.Serialization;

string RequireEnv(string name) =>
    Environment.GetEnvironmentVariable(name)
    ?? throw new InvalidOperationException($"Falta la variable de entorno {name}.");

var projectEndpoint = new Uri(RequireEnv("FOUNDRY_PROJECT_ENDPOINT"));
var connectionId = RequireEnv("FOUNDRY_CONNECTION_ID");
var modelDeploymentName = RequireEnv("FOUNDRY_MODEL_DEPLOYMENT_NAME");
var agentToolsBaseUrl = RequireEnv("AGENT_TOOLS_BASE_URL");

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var foundryDir = Path.Combine(repoRoot, "foundry");

var yamlDeserializer = new DeserializerBuilder().Build();

string LoadYamlField(string fileName, string field)
{
    var text = File.ReadAllText(Path.Combine(foundryDir, "agents", fileName));
    var doc = yamlDeserializer.Deserialize<Dictionary<string, object>>(text);
    return (string)doc[field];
}

var openApiSpecYaml = File
    .ReadAllText(Path.Combine(foundryDir, "agent-tools.openapi.yaml"))
    .Replace("${AGENT_TOOLS_BASE_URL}", agentToolsBaseUrl);
var openApiSpecObject = yamlDeserializer.Deserialize<object>(openApiSpecYaml);
var openApiSpecJson = JsonSerializer.Serialize(openApiSpecObject);

var client = new AgentAdministrationClient(projectEndpoint, new DefaultAzureCredential());

var authDetails = new OpenApiProjectConnectionAuthenticationDetails(
    new OpenApiProjectConnectionSecurityScheme(connectionId));
var openApiFunction = new OpenApiFunctionDefinition(
    "rio_claro_read_only",
    BinaryData.FromString(openApiSpecJson),
    authDetails);
var openApiTool = ProjectsAgentsModelFactory.OpenAPITool(openApiFunction);

var businessDefinition = new DeclarativeAgentDefinition(model: modelDeploymentName)
{
    Instructions = LoadYamlField("business.agent.yaml", "instructions"),
};
businessDefinition.Tools.Add(openApiTool);

Console.WriteLine("Creando/actualizando rio-claro-business-agent...");
var businessVersion = await client.CreateAgentVersionAsync(
    "rio-claro-business-agent",
    new ProjectsAgentVersionCreationOptions(businessDefinition));
Console.WriteLine($"  name={businessVersion.Value.Name}, version={businessVersion.Value.Version}");

var invoiceDefinition = new DeclarativeAgentDefinition(model: modelDeploymentName)
{
    Instructions = LoadYamlField("invoice.agent.yaml", "instructions"),
};

Console.WriteLine("Creando/actualizando rio-claro-invoice-agent...");
var invoiceVersion = await client.CreateAgentVersionAsync(
    "rio-claro-invoice-agent",
    new ProjectsAgentVersionCreationOptions(invoiceDefinition));
Console.WriteLine($"  name={invoiceVersion.Value.Name}, version={invoiceVersion.Value.Version}");

Console.WriteLine();
Console.WriteLine("Configura estos app settings en el backend:");
Console.WriteLine($"  Foundry__ProjectEndpoint={projectEndpoint}");
Console.WriteLine($"  Foundry__BusinessAgentVersion={businessVersion.Value.Version}");
Console.WriteLine($"  Foundry__InvoiceAgentVersion={invoiceVersion.Value.Version}");
Console.WriteLine($"  Foundry__ModelDeploymentName={modelDeploymentName}");
