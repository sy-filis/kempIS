using SharedKernel;

namespace Domain.Reservations.PoliceReports;

public static class PoliceReportErrors
{
  public static readonly Error Unauthorized = Error.Failure(
    "PoliceReport.Unauthorized",
    "UBYPORT rejected the provided credentials.");

  public static readonly Error Rejected = Error.Failure(
    "PoliceReport.Rejected",
    "UBYPORT rejected the submission.");

  public static readonly Error Unavailable = Error.Failure(
    "PoliceReport.Unavailable",
    "UBYPORT is currently unavailable.");
}
