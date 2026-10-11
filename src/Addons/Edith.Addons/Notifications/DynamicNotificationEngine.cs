namespace Edith.Addons.Notifications;

public sealed record ShiftSchedule
{
    public required Guid UserId { get; init; }
    public required string UserName { get; init; }
    public required TimeOnly ShiftStart { get; init; }
    public required TimeOnly ShiftEnd { get; init; }
    public bool IsOnDuty { get; init; } = true;
}

public sealed record NotificationDispatchResult
{
    public Guid AlertId { get; init; } = Guid.NewGuid();
    public required string TargetChannel { get; init; } // DIRECT_ANALYST | ON_DUTY_SUPERVISOR | TEAMS_WEBHOOK
    public required string RecipientName { get; init; }
    public required string Message { get; init; }
    public required bool WasEscalatedDueToShift { get; init; }
    public DateTimeOffset DispatchedAt { get; init; } = DateTimeOffset.UtcNow;
}

public interface IDynamicNotificationEngine
{
    NotificationDispatchResult RouteAlert(
        Guid preferredUserId,
        string preferredUserName,
        string alertTitle,
        string alertBody,
        TimeOnly currentTime,
        IReadOnlyList<ShiftSchedule> activeSchedules,
        string fallbackSupervisorName = "MLRO Duty Officer");
}

public sealed class DynamicNotificationEngine : IDynamicNotificationEngine
{
    public NotificationDispatchResult RouteAlert(
        Guid preferredUserId,
        string preferredUserName,
        string alertTitle,
        string alertBody,
        TimeOnly currentTime,
        IReadOnlyList<ShiftSchedule> activeSchedules,
        string fallbackSupervisorName = "MLRO Duty Officer")
    {
        var analystSchedule = activeSchedules.FirstOrDefault(s => s.UserId == preferredUserId);

        var isOnDuty = analystSchedule != null &&
                       analystSchedule.IsOnDuty &&
                       currentTime >= analystSchedule.ShiftStart &&
                       currentTime <= analystSchedule.ShiftEnd;

        if (isOnDuty)
        {
            return new NotificationDispatchResult
            {
                TargetChannel = "DIRECT_ANALYST",
                RecipientName = preferredUserName,
                Message = $"[{alertTitle}] {alertBody}",
                WasEscalatedDueToShift = false
            };
        }

        // Off-duty: Route to an on-duty analyst or fallback supervisor
        var onDutySubstitute = activeSchedules.FirstOrDefault(s =>
            s.IsOnDuty &&
            currentTime >= s.ShiftStart &&
            currentTime <= s.ShiftEnd);

        var recipient = onDutySubstitute?.UserName ?? fallbackSupervisorName;
        var channel = onDutySubstitute != null ? "ON_DUTY_ANALYST" : "TEAMS_DUTY_ESCALATION";

        return new NotificationDispatchResult
        {
            TargetChannel = channel,
            RecipientName = recipient,
            Message = $"[SHIFT ESCALATION] [{alertTitle}] (Original assignee {preferredUserName} is off-duty): {alertBody}",
            WasEscalatedDueToShift = true
        };
    }
}
