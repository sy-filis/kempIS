using SharedKernel;

namespace Application.Finance.Bills;

public interface IBillDocumentRenderer
{
  Task<Result<BillDocumentRenderResult>> RenderAsync(Guid billId, CancellationToken cancellationToken);
}
