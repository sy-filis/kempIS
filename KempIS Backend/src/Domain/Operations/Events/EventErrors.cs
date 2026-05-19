using SharedKernel;

namespace Domain.Operations.Events;

public static class EventErrors
{
  public static Error NotFound(Guid eventId) => Error.NotFound(
      "Events.NotFound",
      $"The Event with the Id = '{eventId}' was not found");
}
