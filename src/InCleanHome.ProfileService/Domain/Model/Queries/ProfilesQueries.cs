namespace InCleanHome.ProfileService.Domain.Model.Queries;

public record GetClientProfileByUserIdQuery(int UserId);
public record GetWorkerProfileByUserIdQuery(int UserId);
public record GetWorkerProfileByIdQuery(int Id);
public record GetAllWorkerProfilesQuery;

/// <summary>
/// Search workers with optional filters. If <c>ServiceTypes</c> has elements,
/// the worker must offer ALL of them (AND, not any).
/// <c>ServiceType</c> (singular) is kept for backwards compatibility.
/// </summary>
public record SearchWorkersQuery(
    string? ServiceType,
    string? Zone,
    string? Gender,
    int? MinAge,
    int? MaxAge,
    decimal? MaxHourlyRate,
    decimal? MinRating,
    List<string>? ServiceTypes = null);
