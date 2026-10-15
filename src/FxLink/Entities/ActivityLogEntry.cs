namespace FxLink.Entities;

public sealed record ActivityLogEntry(
    string ArgumentAssemblyQualifiedName,
    string LogAssemblyQualifiedName,
    string LogJson);
