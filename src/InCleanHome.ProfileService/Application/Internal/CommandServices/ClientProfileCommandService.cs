using InCleanHome.ProfileService.Domain.Model.Aggregates;
using InCleanHome.ProfileService.Domain.Model.Commands;
using InCleanHome.ProfileService.Domain.Repositories;
using InCleanHome.ProfileService.Domain.Services;
using InCleanHome.ProfileService.Infrastructure.Messaging.Events;
using MassTransit;

namespace InCleanHome.ProfileService.Application.Internal.CommandServices;

public class ClientProfileCommandService(
    IClientProfileRepository repository,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    ILogger<ClientProfileCommandService> logger) : IClientProfileCommandService
{
    public async Task<ClientProfile> Handle(CreateClientProfileCommand command)
    {
        var existing = await repository.FindByUserIdAsync(command.UserId);
        if (existing is not null)
            throw new InvalidOperationException($"Client profile for user {command.UserId} already exists");

        var profile = new ClientProfile(command.UserId, command.Name, command.Phone);
        await repository.AddAsync(profile);
        await unitOfWork.CompleteAsync();
        return profile;
    }

    public async Task<ClientProfile?> Handle(UpdateClientProfileCommand command)
    {
        var profile = await repository.FindByUserIdAsync(command.UserId);
        if (profile == null) return null;

        profile.Update(command.Name, command.Phone);
        repository.Update(profile);
        await unitOfWork.CompleteAsync();

        await SafePublishAsync(new ClientProfileUpdatedEvent
        {
            UserId    = profile.UserId,
            ProfileId = profile.Id
        });

        return profile;
    }

    public async Task<ClientProfile?> Handle(UpdateClientPhotoCommand command)
    {
        var profile = await repository.FindByUserIdAsync(command.UserId);
        if (profile == null) return null;
        profile.SetPhoto(command.PhotoUrl);
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
