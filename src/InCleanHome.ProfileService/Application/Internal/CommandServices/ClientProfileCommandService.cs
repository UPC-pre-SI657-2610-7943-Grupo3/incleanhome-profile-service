using InCleanHome.ProfileService.Domain.Model.Aggregates;
using InCleanHome.ProfileService.Domain.Model.Commands;
using InCleanHome.ProfileService.Domain.Repositories;
using InCleanHome.ProfileService.Domain.Services;

namespace InCleanHome.ProfileService.Application.Internal.CommandServices;

public class ClientProfileCommandService(
    IClientProfileRepository repository,
    IUnitOfWork unitOfWork) : IClientProfileCommandService
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
}
