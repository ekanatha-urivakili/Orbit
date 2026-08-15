namespace Orbit.Infrastructure.Email;

/// <summary>
/// Bound from configuration section <c>Email:Smtp</c>. <see cref="Username"/> empty means an
/// unauthenticated relay (Mailpit's local dev default); <see cref="UseStartTls"/> is off for the
/// same reason.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseStartTls { get; set; }
    public string FromAddress { get; set; } = "no-reply@orbit.local";
    public string FromName { get; set; } = "Orbit";
}
