namespace DocumentTranslator.Models;

public sealed record Language(string Code, string DisplayName)
{
    public override string ToString() => $"{DisplayName} ({Code})";
}
