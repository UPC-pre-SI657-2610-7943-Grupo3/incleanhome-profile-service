using InCleanHome.ProfileService.Domain.Model.Aggregates;
using InCleanHome.ProfileService.Domain.Model.Commands;
using InCleanHome.ProfileService.Domain.Services;
using InCleanHome.ProfileService.Infrastructure.Messaging.Events;
using InCleanHome.ProfileService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace InCleanHome.ProfileService.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumes <see cref="ReviewSubmittedEvent"/> from Reviews Service and updates
/// the WorkerProfile's running average rating and total services count.
/// </summary>
/// <remarks>
/// Reviews Service now ALSO calls Profile Service synchronously via HTTP
/// (POST /api/v1/internal/workers/{id}/register-review) right after saving
/// the review. This consumer becomes the fallback: if the HTTP call failed
/// (network blip, profile-service rebooting) the broker message still
/// arrives later and the rating gets updated.
///
/// Because both paths can succeed (HTTP + RabbitMQ), this consumer
/// deduplicates by ReviewId: we keep a small table of processed IDs and
/// skip events we already applied.
/// </remarks>
public class ReviewSubmittedConsumer(
    IWorkerProfileCommandService workerCommandService,
    ProfileDbContext db,
    ILogger<ReviewSubmittedConsumer> logger) : IConsumer<ReviewSubmittedEvent>
{
    public async Task Consume(ConsumeContext<ReviewSubmittedEvent> ctx)
    {
        var evt = ctx.Message;

        // Dedup: if this ReviewId was already applied (either by the HTTP
        // sync path or a previous delivery of this event), skip it.
        var alreadyProcessed = await db.ProcessedReviewEvents
            .AsNoTracking()
            .AnyAsync(r => r.ReviewId == evt.ReviewId);

        if (alreadyProcessed)
        {
            logger.LogDebug(
                "[ReviewSubmitted] review {ReviewId} already processed; skipping",
                evt.ReviewId);
            return;
        }

        logger.LogInformation(
            "[ReviewSubmitted] worker={WorkerId} rating={Rating} reviewId={ReviewId}",
            evt.WorkerId, evt.Rating, evt.ReviewId);

        try
        {
            await workerCommandService.Handle(
                new RegisterWorkerCompletedServiceCommand(evt.WorkerId, evt.Rating));

            db.ProcessedReviewEvents.Add(new ProcessedReviewEvent
            {
                ReviewId   = evt.ReviewId,
                ProcessedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
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
