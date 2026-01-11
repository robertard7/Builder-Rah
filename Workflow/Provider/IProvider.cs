#nullable enable

namespace RahBuilder.Workflow.Provider;

public interface IProvider
{
    bool Enabled { get; }
    bool Reachable { get; }
}

public interface ILocalProvider : IProvider
{
}

public interface ICloudAssistProvider : IProvider
{
}
