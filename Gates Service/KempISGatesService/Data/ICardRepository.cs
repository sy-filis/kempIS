using KempISGatesService.Models;

namespace KempISGatesService.Data;

public interface ICardRepository
{
  CardUpsertOutcome Upsert(int key, DateTimeOffset validTo, string realName, string note);

  bool Delete(int key);

  // Opens and disposes a connection so an unreachable Users database surfaces at startup.
  void Probe();
}
