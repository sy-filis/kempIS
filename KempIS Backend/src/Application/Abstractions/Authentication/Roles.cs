namespace Application.Abstractions.Authentication;

public static class Roles
{
  public const string Guest = nameof(Guest);
  public const string Receptionist = nameof(Receptionist);
  public const string Accountant = nameof(Accountant);
  public const string CleaningStaff = nameof(CleaningStaff);
  public const string Manager = nameof(Manager);

  public static readonly IReadOnlyList<string> All =
    [Guest, Receptionist, Accountant, CleaningStaff, Manager];
}
