using JetBrains.Annotations;

namespace postmottak_arkivering_dotnet.Contracts;

public record ErrorResponse([UsedImplicitly] string Message, string? ExceptionMessage = null);