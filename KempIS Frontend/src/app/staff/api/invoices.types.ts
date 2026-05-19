export const InvoiceStatus = {
  Draft: 0,
  Created: 1,
  Paid: 2,
} as const;

export type InvoiceStatus = (typeof InvoiceStatus)[keyof typeof InvoiceStatus];

/** Adds the virtual `AfterDue` case to the persisted `InvoiceStatus`
 *  values. Serialized as the enum name (string) to match the backend's
 *  default binding for `[AsParameters]` enum query parameters. */
export const InvoiceStateFilter = {
  Draft: "Draft",
  Created: "Created",
  Paid: "Paid",
  AfterDue: "AfterDue",
} as const;

export type InvoiceStateFilter =
  (typeof InvoiceStateFilter)[keyof typeof InvoiceStateFilter];

export type InvoiceReservationOverview = {
  id: string;
  number: string;
  from: string;
  to: string;
};

export type InvoiceSummary = {
  id: string;
  reservation: InvoiceReservationOverview;
  number: string | null;
  status: InvoiceStatus;
  issuedAt: string; // YYYY-MM-DD
  paidAt: string | null; // YYYY-MM-DD
  dueTo: string | null; // YYYY-MM-DD
  linkedBillId: string | null;
  totalAmount: number;
};

export type InvoiceAddressInput = {
  countryId: string;
  city: string;
  zipCode: string;
  street: string;
  houseNumber: string;
};

export type InvoicePayerInput = {
  name: string;
  surname: string;
  address: InvoiceAddressInput;
};

export type InvoiceLegalEntityInput = {
  name: string;
  cin: string;
  tin: string;
  address: InvoiceAddressInput;
};

export type InvoiceItemInput = {
  /** `null` for ad-hoc rows the receptionist typed by hand. */
  serviceGuid: string | null;
  name: string;
  quantity: number;
  unitPrice: number;
  vatRatePercentage: number;
};

/** Backend predicate validator requires exactly one of `payer` /
 *  `legalEntity` (XOR; sending both is rejected). */
export type CreateInvoiceRequest = {
  reservationId: string;
  payer?: InvoicePayerInput;
  legalEntity?: InvoiceLegalEntityInput;
  email: string;
  phoneNumber: string;
  items: readonly InvoiceItemInput[];
};

export type CreateInvoiceResponse = {
  invoiceId: string;
};

export type GetInvoiceItemView = {
  id: string;
  serviceGuid: string | null;
  name: string;
  quantity: number;
  /** Net unit price. The form stores gross internally and converts on
   *  prefill / submit. */
  unitPrice: number;
  vatRatePercentage: number;
};

export type GetInvoiceResponse = {
  id: string;
  reservationId: string;
  number: string | null;
  status: InvoiceStatus;
  issuedAt: string; // YYYY-MM-DD
  dueTo: string | null; // YYYY-MM-DD
  paidAt: string | null; // YYYY-MM-DD
  linkedBillId: string | null;
  email: string;
  phoneNumber: string;
  /** Exactly one of `payer` / `legalEntity` is non-null. */
  payer: InvoicePayerInput | null;
  legalEntity: InvoiceLegalEntityInput | null;
  items: readonly GetInvoiceItemView[];
};

/** Same shape as create minus `reservationId` (immutable on a draft).
 *  Backend rejects edits once the invoice leaves `Draft`. */
export type UpdateInvoiceRequest = Omit<CreateInvoiceRequest, "reservationId">;

export type MarkInvoiceCreatedRequest = {
  number: string;
  issuedAt: string; // YYYY-MM-DD
  dueTo: string; // YYYY-MM-DD
};

export type MarkInvoicePaidRequest = {
  paidAt: string; // YYYY-MM-DD
};

export type AresAddressResponse = {
  countryCode: string;
  city: string;
  zipCode: string;
  street: string | null;
  houseNumber: string;
};

export type LegalEntityFinderResponse = {
  name: string;
  cin: string;
  tin: string | null;
  address: AresAddressResponse;
};
