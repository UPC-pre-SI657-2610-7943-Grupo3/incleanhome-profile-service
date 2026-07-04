using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InCleanHome.ProfileService.Domain.Model.Aggregates;

/// <summary>
///   Tracks which ReviewSubmittedEvents have already been applied to the
///   worker rating. Both the HTTP internal endpoint (synchronous path from
///   Reviews Service) and the RabbitMQ consumer (broker fallback) check this
///   table before applying, so a worker's rating cannot be incremented twice
///   for the same review even if both paths fire.
/// </summary>
[Table("ProcessedReviewEvents")]
public class ProcessedReviewEvent
{
    [Key]
    public int ReviewId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
