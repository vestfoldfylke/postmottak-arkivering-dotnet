namespace postmottak_arkivering_dotnet.Contracts.Email;

public record MailAttachment(byte[] Content, string Name);