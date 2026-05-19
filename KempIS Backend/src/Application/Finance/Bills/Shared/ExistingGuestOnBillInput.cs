namespace Application.Finance.Bills.Shared;

public sealed record ExistingGuestOnBillInput(Guid GuestId, bool PaysRecreationFee);
