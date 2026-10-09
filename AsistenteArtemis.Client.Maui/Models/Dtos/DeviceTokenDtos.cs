namespace AsistenteArtemis.Client.Maui.Models.Dtos;

// Mirror of TodoAppRoll.Api/Models/DTOs/DeviceTokenDtos.cs.

public class DeviceTokenDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public class DeviceTokenCreateDto
{
    public string Name { get; set; } = string.Empty;
}

public class DeviceTokenCreatedDto
{
    public DeviceTokenDto Device { get; set; } = new();
    public string Token { get; set; } = string.Empty;
}
