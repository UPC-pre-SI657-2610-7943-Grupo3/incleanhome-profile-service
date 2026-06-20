using InCleanHome.ProfileService.Domain.Repositories;
using InCleanHome.ProfileService.Infrastructure.Messaging.Events;
using MassTransit;

namespace InCleanHome.ProfileService.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumes <see cref="UserDeletedEvent"/> from IAM Service and removes the
/// orphaned ClientProfile/WorkerProfile (whichever exists).
/// </summary>
public class UserDeletedConsumer(
    IClientProfileRepository clientRepo,
    IWorkerProfileRepository workerRepo,
    IUnitOfWork unitOfWork,
    ILogger<UserDeletedConsumer> logger) : IConsumer<UserDeletedEvent>
{
    public async Task Consume(ConsumeContext<UserDeletedEvent> ctx)
    {
        var evt = ctx.Message;
        logger.LogInformation("[UserDeleted] cleaning profiles for user {UserId}", evt.UserId);

        var client = await clientRepo.FindByUserIdAsync(evt.UserId);
        if (client is not null) clientRepo.Remove(client);

        var worker = await workerRepo.FindByUserIdAsync(evt.UserId);
        if (worker is not null) workerRepo.Remove(worker);

        if (client is not null || worker is not null)
            await unitOfWork.CompleteAsync();
    }
}
