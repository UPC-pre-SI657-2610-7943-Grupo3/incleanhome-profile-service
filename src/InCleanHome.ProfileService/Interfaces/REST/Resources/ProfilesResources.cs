namespace InCleanHome.ProfileService.Interfaces.REST.Resources;

// Creation
public record CreateClientProfileResource(int UserId, string Name, string? Phone);

public record CreateWorkerProfileResource(
    int UserId,
    string Name,
    string? Phone,
    int Age,
    string Gender,
    List<string> ServiceTypes,
    List<string> Zones,
    decimal HourlyRate,
    decimal HourlyRateSunday,
    int ExperienceYears,
    string? Bio);

// Updates
public record UpdateClientProfileResource(string Name, string? Phone);

public record UpdateWorkerProfileResource(
    string Name,
    string? Phone,
    int Age,
    int ExperienceYears,
    decimal HourlyRate,
    decimal HourlyRateSunday,
    List<string> ServiceTypes,
    List<string> Zones,
    string? Bio);

public record UpdatePhotoResource(string? PhotoUrl);

// Outputs
public record ClientProfileResource(
    int Id,
    int UserId,
    string Name,
    string? Phone,
    string? PhotoUrl);

public record WorkerResource(
    int Id,
    int ProfileId,
    string Name,
    string? Phone,
    int Age,
    string Gender,
    List<string> ServiceTypes,
    List<string> Zones,
    decimal HourlyRate,
    decimal HourlyRateSunday,
    int ExperienceYears,
    string Bio,
    decimal AverageRating,
    int TotalServices,
    string? PhotoUrl);
