namespace Application.Abstractions.Documents;

public interface IQrCodeEncoder
{
  byte[] EncodePng(string payload);
}
