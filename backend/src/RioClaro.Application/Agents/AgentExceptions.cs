namespace RioClaro.Application.Agents;

public sealed class AgentNotConfiguredException : Exception
{
}

public sealed class AgentTimeoutException : Exception
{
}

public sealed class AgentProviderException : Exception
{
}

public sealed class AgentInvalidResponseException : Exception
{
}

public sealed class AgentResponseTooLargeException : Exception
{
}
