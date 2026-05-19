using Domain.Finance.Bills;
using SharedKernel;

namespace Application.Finance.Bills;

public interface IBillStickerRenderer
{
  Task<Result<byte[]>> RenderAsync(Bill bill, CancellationToken cancellationToken);
}
