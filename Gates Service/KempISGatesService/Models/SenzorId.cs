namespace KempISGatesService.Models;

// Identifier values mirror the legacy Events.SenzorsId column. "Senzor" (Czech) is preserved to match the
// schema; do not rename to "Sensor" without also migrating the database.
public enum SenzorId
{
  ApplicationLifecycleEvent = -1,
  CardCreated = -2,
  CardDeleted = -1,
}
