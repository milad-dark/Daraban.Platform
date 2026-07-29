namespace Daraban.Platform.Abstractions;

/// <summary>Resolved once per request from the validated JWT (Task 1.3 SS2.1).</summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid ActiveEntityId { get; }
    bool IsAuthenticated { get; }
}
