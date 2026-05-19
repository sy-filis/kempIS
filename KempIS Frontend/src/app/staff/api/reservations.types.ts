// Mirrors Domain.Reservations.ReservationStates.ReservationState.
export const ReservationState = {
  Created: 0,
  Confirmed: 1,
  CheckedIn: 2,
  Cancelled: 3,
  Completed: 4,
} as const;

export type ReservationState =
  (typeof ReservationState)[keyof typeof ReservationState];

export type Reservation = {
  id: string;
  number: string;
  reservationMakerName: string;
  reservationMakerSurname: string;
  reservationMakerEmail: string;
  reservationMakerPhone: string;
  groupReservationId: string | null;
  from: string;
  to: string;
  state: ReservationState;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  note: string | null;
  displayName: string | null;
  /** Spot IDs the reservation is pinned to; group bookings without an
   *  assigned spot are omitted (only spot items with a non-null spotId
   *  are returned). */
  spotItems: readonly string[];
};

export type ReservationMonthlySummary = {
  year: number;
  /** 12 entries, index 0 = January. */
  months: readonly number[];
};

// Mirrors `Domain.Reservations.Guests.DocumentType`.
export const GuestDocumentType = {
  Passport: 1,
  IdCard: 2,
  CzechResidencePermit: 3,
  LostPassportConfirmation: 5,
  CzechDiplomatCard: 6,
  ChildInParentPassport: 7,
} as const;

export type GuestDocumentType =
  (typeof GuestDocumentType)[keyof typeof GuestDocumentType];

/** `countryId` is the Guid of a Nationality row - the same table backs
 *  both citizenship and country-of-residence. */
export type ReservationAddress = {
  countryId: string;
  city: string;
  zipCode: string;
  street: string;
  houseNumber: string;
};

export type ReservationDateRange = {
  from: string; // YYYY-MM-DD
  to: string; // YYYY-MM-DD
};

/** `at` is a "HH:MM" pickup time (null when not yet scheduled). */
export type ReservationMealAmount = {
  at: string | null;
  normal: number;
  glutenFree: number;
  lactoseFree: number;
  vegetarian: number;
  glutenFreeLactoseFree: number;
  glutenFreeVegetarian: number;
  lactoseFreeVegetarian: number;
  glutenFreeLactoseFreeVegetarian: number;
};

export const InvoiceStatus = {
  Pending: 0,
  Created: 1,
  Paid: 2,
  Cancelled: 3,
} as const;

export type InvoiceStatus = (typeof InvoiceStatus)[keyof typeof InvoiceStatus];

export const BillKind = {
  Reservation: 0,
  Repair: 1,
  Other: 2,
} as const;

export type BillKind = (typeof BillKind)[keyof typeof BillKind];

export const PaymentType = {
  Cash: 0,
  Card: 1,
} as const;

export type PaymentType = (typeof PaymentType)[keyof typeof PaymentType];

/** For online bookings, `spotId` starts out `null` until reception pins
 *  the item to a concrete cottage. */
export type ReservationDetailSpotItem = {
  id: string;
  spotGroupId: string;
  spotId: string | null;
  hasGivenKey: boolean;
  hasReturnedKeys: boolean;
  /** Bill that already settled this spot item, or `null` if none has. */
  billId: string | null;
};

export type ReservationDetailGuest = {
  id: string;
  billId: string | null;
  firstName: string;
  lastName: string;
  nationalityId: string;
  dateOfBirth: string; // YYYY-MM-DD
  documentType: GuestDocumentType | null;
  documentNumber: string | null;
  address: ReservationAddress;
  reasonOfStay: string;
  stayFrom: string; // YYYY-MM-DD
  stayTo: string; // YYYY-MM-DD
  visaNumber: string | null;
  note: string | null;
  scartation: string | null; // YYYY-MM-DD
  checkInAt: string | null; // ISO-8601 UTC
  checkOutAt: string | null; // ISO-8601 UTC
  hasSignature: boolean;
  signatureCapturedAtUtc: string | null;
  reportedAt: string | null;
};

export type ReservationDetailServiceItem = {
  id: string;
  serviceId: string;
  quantity: number;
  recapSingleQuantity: number;
  recapDayQuantity: number;
};

export type ReservationDetailVehicle = {
  id: string;
  billId: string | null;
  serviceId: string | null;
  registrationNumber: string;
};

export type ReservationDetailMeal = {
  date: string; // YYYY-MM-DD
  breakfast: ReservationMealAmount;
  lunch: ReservationMealAmount;
  lunchPackage: ReservationMealAmount;
  dinner: ReservationMealAmount;
};

export type ReservationDetailInvoice = {
  id: string;
  number: string | null;
  status: InvoiceStatus;
  issuedAt: string; // YYYY-MM-DD
  paidAt: string | null; // YYYY-MM-DD
  linkedBillId: string | null;
};

export type ReservationDetailBill = {
  id: string;
  number: string;
  kind: BillKind;
  issuedAtUtc: string;
  paymentType: PaymentType;
  amount: number;
};

export type ReservationDetailAccessCard = {
  id: string;
  uid: number;
  deposit: number;
  issuedAtUtc: string;
};

export type ReservationDetail = {
  id: string;
  number: string;
  /** Magic-link token used by the public guest endpoints. Combined with
   *  `id` to form `${origin}/reservations/{id}?secret={secret}`. */
  secret: string;
  state: ReservationState;
  from: string;
  to: string;
  reservationMakerName: string;
  reservationMakerSurname: string;
  reservationMakerEmail: string;
  reservationMakerPhone: string;
  groupReservationId: string | null;
  note: string | null;
  displayName: string | null;
  guests: readonly ReservationDetailGuest[];
  spotItems: readonly ReservationDetailSpotItem[];
  serviceItems: readonly ReservationDetailServiceItem[];
  vehicles: readonly ReservationDetailVehicle[];
  meals: readonly ReservationDetailMeal[];
  invoices: readonly ReservationDetailInvoice[];
  bills: readonly ReservationDetailBill[];
  accessCards: readonly ReservationDetailAccessCard[];
  createdAtUtc: string;
  updatedAtUtc: string | null;
};

export type ReservationServiceRequest = {
  serviceId: string;
  quantity: number;
  recapSingleQuantity: number;
  recapDayQuantity: number;
};

/** `id == null` adds a new vehicle; existing vehicles whose id is
 *  omitted from the array get removed. */
export type ReservationVehicleRequest = {
  id: string | null;
  registrationNumber: string;
};

export type ReservationRequest = {
  from: string; // YYYY-MM-DD
  to: string; // YYYY-MM-DD
  name: string;
  surname: string;
  email: string;
  phone: string;
  spotIds: readonly string[];
  services: readonly ReservationServiceRequest[];
  /** `id` null = create, omitted-but-existing = delete. */
  vehicles: readonly ReservationVehicleRequest[];
  note: string | null;
  displayName: string | null;
  groupReservationId: string | null;
};
