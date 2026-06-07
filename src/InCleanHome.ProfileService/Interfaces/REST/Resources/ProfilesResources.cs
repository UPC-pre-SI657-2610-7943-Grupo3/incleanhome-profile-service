namespace InCleanHome.ProfileService.Interfaces.REST.Resources;

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
    int ExperienceYears,
    string? Bio);



public record UpdateClientProfileResource(string Name, string? Phone);

public record UpdateWorkerProfileResource(
    string Name,
    string? Phone,
    int Age,
    int ExperienceYears,
    decimal HourlyRate,
    List<string> ServiceTypes,
    List<string> Zones,
    string? Bio);

public record UpdatePhotoResource(string? PhotoUrl);



public record ClientProfileResource(
    int Id,
    int UserId,
    string Name,
    string? Phone,
    string? PhotoUrl);

public record WorkerResource(
    int Id,                 // userId so the frontend can navigate /worker/{id}
    int ProfileId,          // internal profile primary key
    string Name,
    string? Phone,
    int Age,
    string Gender,
    List<string> ServiceTypes,
    List<string> Zones,
    decimal HourlyRate,
    int ExperienceYears,
    string Bio,
    decimal AverageRating,
    int TotalServices,
    string? PhotoUrl);
