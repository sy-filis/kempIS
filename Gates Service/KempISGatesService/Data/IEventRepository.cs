using KempISGatesService.Models;

namespace KempISGatesService.Data;

public interface IEventRepository
{
  void InsertCardEvent(string keyValue, EventOperation operation, string realName, SenzorId senzorId);

  void InsertLifecycleEvent(EventOperation operation);

  // Opens and disposes a connection so an unreachable Events database surfaces at startup.
  void Probe();
}
