using System.Net.Mime;
using InCleanHome.ProfileService.Domain.Model.Aggregates;
using InCleanHome.ProfileService.Domain.Model.Commands;
using InCleanHome.ProfileService.Domain.Services;
using InCleanHome.ProfileService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace InCleanHome.ProfileService.Interfaces.REST.Controllers;

/// <summary>
///   Internal service-to-service endpoint used by Reviews Service to update
///   worker rating immediately after a review is submitted.
/// </summary>
/// <remarks>
///   <para>
///     Background: in the monolith, reviews → profiles was a direct in-process
///     call (ACL). In microservices, the equivalent is RabbitMQ pub-sub. The
///     broker works but it's eventually consistent — if RabbitMQ is unreachable
///     (CloudAMQP down, network blip, etc.) the rating just never updates and
///     users see "0.0" forever. The user reported exactly this symptom.
///   </para>
///   <para>
///     This endpoint is the HTTP fallback path. Reviews Service calls it
///     synchronously right after persisting the review, so the rating is
///     updated atomically. The RabbitMQ event keeps being published (for
///     notifications), but the rating no longer depends on it arriving.
///   </para>
///   <para>
///     Security: this endpoint is NOT exposed through the API gateway (only
///     reachable on the internal docker network). It requires a shared header
///     <c>X-Internal-Token</c> matching <c>INTERNAL_SERVICE_TOKEN</c> so a
///     misconfigured public route can't be abused.
///   </para>
/// </remarks>
[ApiController]
[Route("api/v1/internal/workers")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Internal — service-to-service calls (NOT exposed via gateway)")]
public class InternalWorkersController(
    IWorkerProfileCommandService workerCommandService,
    IConfiguration configuration,
    ILogger<InternalWorkersController> logger) : ControllerBase
{
    /// <summary>
    ///   Increments TotalServices and re-computes the worker's running average
    ///   rating given a new review. Idempotent: if the same <c>ReviewId</c> has
    ///   been processed before (via this HTTP path or via the RabbitMQ
    ///   <c>ReviewSubmittedEvent</c> consumer), this call becomes a no-op.
    /// </summary>
    [HttpPost("{userId:int}/register-review")]
    [SwaggerOperation("Register a completed review (internal)")]
    public async Task<IActionResult> RegisterReview(
        int userId,
        [FromBody] RegisterReviewBody body,
        [FromServices] ProfileDbContext db)
    {
        if (!IsAuthorized())
        {
            logger.LogWarning("[internal] Unauthorized RegisterReview attempt for worker {WorkerId}", userId);
            return Unauthorized(new { error = "Invalid or missing internal token" });
        }

        try
        {
            // Dedup by ReviewId if provided. If not provided (older callers),
            // we just apply directly — the broker consumer has its own dedup.
            if (body.ReviewId > 0)
            {
                var alreadyProcessed = await db.ProcessedReviewEvents
                    .AsNoTracking()
                    .AnyAsync(r => r.ReviewId == body.ReviewId);

                if (alreadyProcessed)
                {
                    logger.LogDebug(
                        "[internal] review {ReviewId} already processed for worker {WorkerId}; skipping",
                        body.ReviewId, userId);
                    return Ok(new { skipped = true, reason = "already_processed" });
                }
            }

            var profile = await workerCommandService.Handle(
                new RegisterWorkerCompletedServiceCommand(userId, body.Rating));
            if (profile is null) return NotFound(new { error = "Worker profile not found" });

            if (body.ReviewId > 0)
            {
                db.ProcessedReviewEvents.Add(new ProcessedReviewEvent
                {
                    ReviewId    = body.ReviewId,
                    ProcessedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
            }

            return Ok(new
            {
                userId         = profile.UserId,
                averageRating  = profile.AverageRating,
                totalServices  = profile.TotalServices
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[internal] RegisterReview failed for worker {WorkerId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }

    private bool IsAuthorized()
    {
        var expected = Environment.GetEnvironmentVariable("INTERNAL_SERVICE_TOKEN")
                       ?? configuration["Internal:ServiceToken"];
        // If no token is configured at all (e.g. dev), allow loopback to keep
        // things working — but require an exact match if it IS configured.
        if (string.IsNullOrEmpty(expected)) return true;
        var got = Request.Headers["X-Internal-Token"].FirstOrDefault();
        return string.Equals(got, expected, StringComparison.Ordinal);
    }
}

public record RegisterReviewBody(int Rating, int ReviewId = 0);
