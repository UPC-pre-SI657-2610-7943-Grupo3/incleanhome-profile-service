namespace InCleanHome.ProfileService.Infrastructure.Messaging.Events;


public record WorkerProfileUpdatedEvent
{
    public int UserId { get; init; }
    public int ProfileId { get; init; }
    public decimal AverageRating { get; init; }
    public int TotalServices { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public record ClientProfileUpdatedEvent
{
    public int UserId { get; init; }
    public int ProfileId { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}


/// <summary>Published by Reviews Service when a customer rates a service.</summary>
public record ReviewSubmittedEvent
{
    public int ReviewId { get; init; }
    public int BookingId { get; init; }
    public int ClientId { get; init; }
    public int WorkerId { get; init; }
    public int Rating { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Published by IAM Service when a user is deleted.</summary>
public record UserDeletedEvent
{
    public int UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DeletedBy { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
