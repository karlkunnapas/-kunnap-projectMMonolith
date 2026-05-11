namespace Modules.Users.Domain;

internal sealed class AppRefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RefreshToken { get; set; } = Guid.NewGuid().ToString();
    public DateTime Expiration { get; set; } = DateTime.UtcNow.AddDays(7);
    public string? PreviousRefreshToken { get; set; }
    public DateTime PreviousExpiration { get; set; } = DateTime.UtcNow.AddDays(7);

    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
}
