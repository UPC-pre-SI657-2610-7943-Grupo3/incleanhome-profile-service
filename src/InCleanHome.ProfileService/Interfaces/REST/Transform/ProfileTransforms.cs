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
    public static WorkerResource ToResourceFromEntity(WorkerProfile w)
        => new(
            w.UserId,            // Id = UserId for frontend route compatibility (/worker/{userId})
            w.Id,                // ProfileId = internal PK
            w.Name, w.Phone, w.Age, w.Gender,
            w.ServiceTypes, w.Zones,
            w.HourlyRate, w.HourlyRateSunday,
            w.ExperienceYears, w.Bio,
            w.AverageRating, w.TotalServices,
            w.PhotoUrl);
}
