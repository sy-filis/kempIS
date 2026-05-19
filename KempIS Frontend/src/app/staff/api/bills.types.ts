export const BillKind = {
  Regular: 0,
  Repair: 1,
} as const;

export type BillKind = (typeof BillKind)[keyof typeof BillKind];

export type BillSummary = {
  id: string;
  number: string;
  kind: BillKind;
  reservationId: string | null;
  checkInAt: string;
  checkOutAt: string;
  issuedAtUtc: string;
  amount: number;
  paymentType: PaymentType;
  /** `null` for bills still open in the cashier. */
  financialClosingId: string | null;
};

export const PaymentType = {
  Cash: 0,
  Card: 1,
} as const;

export type PaymentType = (typeof PaymentType)[keyof typeof PaymentType];

export type BillAddress = {
  countryId: string;
  city: string;
  zipCode: string;
  street: string;
  houseNumber: string;
};

export type BillPayer = {
  name: string;
  surname: string;
  address: BillAddress;
};

export type BillLegalEntity = {
  name: string;
  cin: string;
  tin: string;
  address: BillAddress;
};

export type BillItemRequest = {
  serviceId: string | null;
  quantity: number;
  unitPrice: number;
  /** `POST /bills` strips this (VAT is re-derived from the catalogue).
   *  `POST /bills/repairs` uses `(serviceId, unitPrice, vatRatePercentage)`
   *  as the per-line cap lookup key, so repair items must supply it. */
  vatRatePercentage?: number;
  recapSingleQuantity: number;
  recapDayQuantity: number;
};

export type BillExistingGuestRequest = {
  guestId: string;
  paysRecreationFee: boolean;
};

export type BillNewGuestRequest = {
  firstName: string;
  lastName: string;
  nationalityId: string;
  dateOfBirth: string; // YYYY-MM-DD
  documentType: number;
  documentNumber: string;
  address: BillAddress;
  reasonOfStay: string;
  stayFrom: string; // YYYY-MM-DD
  stayTo: string; // YYYY-MM-DD
  visaNumber: string | null;
  note: string | null;
  paysRecreationFee: boolean;
  /** Base64 PNG (no `data:image/png;base64,` prefix) buffered from a
   *  tablet signature capture before the guest was persisted. The
   *  backend stores it onto `Guest.SignaturePng` when the bill is
   *  created, so the tablet flow doesn't need a follow-up PUT. */
  signaturePngBase64: string | null;
};

export type BillAccessCardRequest = {
  uid: string;
  deposit: number;
  validUntil: string; // YYYY-MM-DD
  note: string | null;
};

export type CreateBillRequest = {
  reservationId: string | null;
  checkInAt: string; // YYYY-MM-DD
  checkOutAt: string; // YYYY-MM-DD
  payer: BillPayer;
  legalEntity: BillLegalEntity | null;
  paymentType: PaymentType;
  languageId: string;
  items: readonly BillItemRequest[];
  linkedInvoiceIds: readonly string[];
  existingGuests: readonly BillExistingGuestRequest[];
  newGuests: readonly BillNewGuestRequest[];
  reservationSpotItemIds: readonly string[];
  accessCards: readonly BillAccessCardRequest[];
};

export type CreateBillResponse = {
  id: string;
};

/** Each `items` line must match an existing original-bill line by the
 *  `(serviceId, unitPrice, vatRatePercentage)` triple; the backend
 *  rejects unknown combinations and caps quantity at the original minus
 *  prior repairs. */
export type CreateRepairBillRequest = {
  originalBillId: string;
  paymentType: PaymentType;
  reason: string;
  items: readonly BillItemRequest[];
};

export type CreateRepairBillResponse = {
  billId: string;
  number: string;
};

export type BillPayerView = {
  name: string;
  surname: string;
  address: BillAddress;
};

export type BillLegalEntityView = {
  name: string;
  cin: string;
  tin: string;
  address: BillAddress;
};

export type BillPaymentView = {
  paymentType: PaymentType;
  amount: number;
};

export type BillItemView = {
  id: string;
  serviceId: string | null;
  quantity: number;
  unitPrice: number;
  vatRatePercentage: number;
  recapSingleQuantity: number;
  recapDayQuantity: number;
};

export type BillDeductionView = {
  id: string;
  invoiceId: string;
  invoiceNumber: string | null;
  amount: number;
};

export type BillRepairSummary = {
  id: string;
  number: string;
  issuedAtUtc: string;
  amount: number;
};

export type BillGuestView = {
  id: string;
  firstName: string;
  lastName: string;
  paysRecreationFee: boolean | null;
  nationalityId?: string;
  dateOfBirth?: string; // YYYY-MM-DD
  documentType?: number | null;
  documentNumber?: string | null;
  address?: BillAddress;
  reasonOfStay?: string;
  visaNumber?: string | null;
  note?: string | null;
  scartation?: string | null; // YYYY-MM-DD
  checkInAt?: string | null; // ISO-8601 UTC
  checkOutAt?: string | null; // ISO-8601 UTC
  hasSignature?: boolean;
  signatureCapturedAtUtc?: string | null;
};

export type BillAccessCardView = {
  id: string;
  uid: string;
  deposit: number;
  validUntil?: string; // YYYY-MM-DD (optional: not yet returned by backend)
  note: string | null;
};

export type BillDetail = {
  id: string;
  number: string;
  kind: BillKind;
  originalBillId: string | null;
  reservationId: string | null;
  languageId: string;
  issuedAtUtc: string;
  checkInAt: string; // YYYY-MM-DD
  checkOutAt: string; // YYYY-MM-DD
  payer: BillPayerView;
  legalEntity: BillLegalEntityView | null;
  payment: BillPaymentView;
  items: readonly BillItemView[];
  deductions: readonly BillDeductionView[];
  repairs: readonly BillRepairSummary[];
  guests: readonly BillGuestView[];
  accessCards?: readonly BillAccessCardView[];
};
