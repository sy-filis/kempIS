namespace KempISGatesService.Models;

public enum EventOperation
{
  CardCreated = 256,
  CardChanged = 257,
  CardDeleted = 258,
  ProgramBegin = 512,
  ProgramEnd = 513,
}
