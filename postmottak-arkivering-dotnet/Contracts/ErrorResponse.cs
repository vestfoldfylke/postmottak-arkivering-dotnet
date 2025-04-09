namespace postmottak_arkivering_dotnet.Contracts;

public record ErrorResponse(string Message, string? ExceptionMessage = null);