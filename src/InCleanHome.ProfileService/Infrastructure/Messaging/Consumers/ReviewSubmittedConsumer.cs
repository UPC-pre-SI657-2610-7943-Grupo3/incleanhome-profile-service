using InCleanHome.ProfileService.Domain.Model.Commands;
using InCleanHome.ProfileService.Domain.Services;
using InCleanHome.ProfileService.Infrastructure.Messaging.Events;
using MassTransit;

namespace InCleanHome.ProfileService.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumes <see cref="ReviewSubmittedEvent"/> from Reviews Service and updates
/// the WorkerProfile's running average rating and total services count.
/// </summary>
public class ReviewSubmittedConsumer(
    IWorkerProfileCommandService workerCommandService,
    ILogger<ReviewSubmittedConsumer> logger) : IConsumer<ReviewSubmittedEvent>
{
    public async Task Consume(ConsumeContext<ReviewSubmittedEvent> ctx)
    {
        var evt = ctx.Message;
        logger.LogInformation(
            "[ReviewSubmitted] worker={WorkerId} rating={Rating}",
            evt.WorkerId, evt.Rating);

        try
        {
            await workerCommandService.Handle(
                new RegisterWorkerCompletedServiceCommand(evt.WorkerId, evt.Rating));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to update worker {WorkerId} stats from review {ReviewId}",
                evt.WorkerId, evt.ReviewId);
            throw; // MassTransit will retry/move to error queue.
        }
    }
}
