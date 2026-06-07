using InCleanHome.ProfileService.Domain.Model.Aggregates;
using InCleanHome.ProfileService.Interfaces.REST.Resources;

namespace InCleanHome.ProfileService.Interfaces.REST.Transform;

public static class ClientResourceFromEntityAssembler
{
    public static ClientProfileResource ToResourceFromEntity(ClientProfile c)
        => new(c.Id, c.UserId, c.Name, c.Phone, c.PhotoUrl);
}

public static class WorkerResourceFromEntityAssembler
{
    /// <summary>
    /// Maps a <see cref="WorkerProfile"/> to its public resource. We expose
    /// <c>UserId</c> as <c>Id</c> for compatibility with the frontend, which
    /// identifies workers by user id for navigation (/worker/{userId}) and
    /// messaging routes. <c>ProfileId</c> exposes the internal PK for callers
    /// that need it.
    /// </summary>
    public static WorkerResource ToResourceFromEntity(WorkerProfile w)
        => new(
            w.UserId,
            w.Id,
            w.Name,
            w.Phone,
            w.Age,
            w.Gender,
            w.ServiceTypes,
            w.Zones,
            w.HourlyRate,
            w.ExperienceYears,
            w.Bio,
            w.AverageRating,
            w.TotalServices,
            w.PhotoUrl);
}
