namespace Infrastructure.Database;

public enum ConstraintKind
{
  ForeignKey,
  Unique,
}

public sealed class DatabaseConstraintViolationException(
  ConstraintKind kind,
  string? constraintName,
  string? detail,
  Exception innerException)
  : Exception(BuildMessage(kind, constraintName, detail), innerException)
{
  public ConstraintKind Kind { get; } = kind;
  public string? ConstraintName { get; } = constraintName;
  public string? Detail { get; } = detail;

  private static string BuildMessage(ConstraintKind kind, string? constraintName, string? detail)
  {
    string label = kind switch
    {
      ConstraintKind.ForeignKey => "Foreign key constraint violation",
      ConstraintKind.Unique => "Unique constraint violation",
      _ => "Database constraint violation"
    };

    var builder = new System.Text.StringBuilder(label);
    if (constraintName is not null)
    {
      builder.Append(" (").Append(constraintName).Append(')');
    }
    if (detail is not null)
    {
      builder.Append(": ").Append(detail);
    }
    return builder.ToString();
  }
}
