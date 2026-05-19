namespace Domain.Finance.Payments;

public sealed record Payment(
  PaymentType PaymentType,
  decimal Amount);

public enum PaymentType
{
  Cash,
  Card
}
