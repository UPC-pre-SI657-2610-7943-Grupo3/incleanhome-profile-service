using InCleanHome.ProfileService.Domain.Model.Aggregates;
using InCleanHome.ProfileService.Domain.Model.Commands;
using InCleanHome.ProfileService.Domain.Model.ValueObjects;
using InCleanHome.ProfileService.Domain.Repositories;
using InCleanHome.ProfileService.Domain.Services;
using InCleanHome.ProfileService.Infrastructure.Messaging.Events;
using MassTransit;

namespace InCleanHome.ProfileService.Application.Internal.CommandServices;

public class WorkerProfileCommandService(
    IWorkerProfileRepository repository,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    ILogger<WorkerProfileCommandService> logger) : IWorkerProfileCommandService
{
    public async Task<WorkerProfile> Handle(CreateWorkerProfileCommand c)
    {
        var existing = await repository.FindByUserIdAsync(c.UserId);
        if (existing is not null)
            throw new InvalidOperationException($"Worker profile for user {c.UserId} already exists");

        if (!Gender.IsValid(c.Gender))
            throw new ArgumentException($"Invalid gender: {c.Gender}");
        if (c.Age < 18 || c.Age > 70)
            throw new ArgumentException("Age must be between 18 and 70");
        if (c.HourlyRate < 0)
            throw new ArgumentException("HourlyRate must be non-negative");

        var profile = new WorkerProfile(
            c.UserId, c.Name, c.Phone, c.Age, c.Gender,
            c.ServiceTypes, c.Zones, c.HourlyRate, c.HourlyRateSunday,
            c.ExperienceYears, c.Bio);
        await repository.AddAsync(profile);
        await unitOfWork.CompleteAsync();
        return profile;
    }

    public async Task<WorkerProfile?> Handle(UpdateWorkerProfileCommand c)
    {
        var profile = await repository.FindByUserIdAsync(c.UserId);
        if (profile == null) return null;
        profile.Update(c.Name, c.Phone, c.Age, c.ServiceTypes, c.Zones,
            c.HourlyRate, c.HourlyRateSunday, c.ExperienceYears, c.Bio);
        repository.Update(profile);
        await unitOfWork.CompleteAsync();

        await SafePublishAsync(new WorkerProfileUpdatedEvent
        {
            UserId        = profile.UserId,
            ProfileId     = profile.Id,
            AverageRating = profile.AverageRating,
            TotalServices = profile.TotalServices
        });

        return profile;
    }

    public async Task<WorkerProfile?> Handle(RegisterWorkerCompletedServiceCommand c)
    {
        var profile = await repository.FindByUserIdAsync(c.UserId);
        if (profile == null) return null;
        profile.RegisterCompletedService(c.Rating);
        repository.Update(profile);
        await unitOfWork.CompleteAsync();

        await SafePublishAsync(new WorkerProfileUpdatedEvent
        {
            UserId        = profile.UserId,
            ProfileId     = profile.Id,
            AverageRating = profile.AverageRating,
            TotalServices = profile.TotalServices
        });

        return profile;
    }

    public async Task<WorkerProfile?> Handle(UpdateWorkerPhotoCommand c)
    {
        var profile = await repository.FindByUserIdAsync(c.UserId);
        if (profile == null) return null;
        profile.SetPhoto(c.PhotoUrl);
        repository.Update(profile);
        await unitOfWork.CompleteAsync();
        return profile;
    }

    private async Task SafePublishAsync<T>(T evt) where T : class
    {
        try { await publishEndpoint.Publish(evt); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish {EventType}. Continuing without eventing.", typeof(T).Name);
        }
    }
}
