export type GuestStayDateRange = {
  from: string; // YYYY-MM-DD
  to: string; // YYYY-MM-DD
};

export type GuestAddress = {
  countryId: string;
  city: string;
  zipCode: string;
  street: string;
  houseNumber: string;
};

export type Guest = {
  id: string;
  reservationId: string | null;
  billId?: string | null;
  /** Tri-state: `null` means not asked yet (unbilled). */
  paysRecreationFee?: boolean | null;
  firstName: string;
  lastName: string;
  nationalityId?: string;
  dateOfBirth: string; // YYYY-MM-DD
  documentType: number | null;
  documentNumber: string | null;
  nationalityName: string;
  nationalityAlpha2: string;
  address?: GuestAddress;
  reasonOfStay?: string;
  stayDateRange: GuestStayDateRange;
  visaNumber?: string | null;
  note?: string | null;
  scartation?: string | null; // YYYY-MM-DD
  checkInAt: string | null; // ISO-8601 UTC
  checkOutAt: string | null; // ISO-8601 UTC
  hasSignature: boolean;
  signatureCapturedAtUtc?: string | null;
};

export type GuestDetail = {
  id: string;
  reservationId: string | null;
  billId: string | null;
  paysRecreationFee: boolean | null;
  firstName: string;
  lastName: string;
  nationalityId: string;
  dateOfBirth: string; // YYYY-MM-DD
  documentType: number | null;
  documentNumber: string | null;
  address: GuestAddress;
  reasonOfStay: string;
  stayDateRange: GuestStayDateRange | null;
  visaNumber: string | null;
  note: string | null;
  scartation: string | null; // YYYY-MM-DD
  checkInAt: string | null; // ISO-8601 UTC
  checkOutAt: string | null; // ISO-8601 UTC
  hasSignature: boolean;
  signatureCapturedAtUtc: string | null;
};
