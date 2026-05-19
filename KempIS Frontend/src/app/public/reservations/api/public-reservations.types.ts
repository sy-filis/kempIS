// GET /availability

export type EventNotice = {
  eventId: string;
  name: string;
  description: string | null;
  startsAtUtc: string;
  endsAtUtc: string | null;
};

export type SpotGroupAvailability = {
  spotGroupId: string;
  name: string;
  capacity: number;
  totalSpots: number;
  occupied: number;
  available: number;
  imageUrl: string | null;
  detailsUrl: string | null;
  events: readonly EventNotice[];
};

export type AvailabilityResponse = {
  spotGroups: readonly SpotGroupAvailability[];
};

// POST /reservations/web

export type RequestedSpotGroupDto = {
  spotGroupId: string;
  quantity: number;
};

export type CreateWebReservationRequest = {
  name: string;
  surname: string;
  email: string;
  phone: string;
  from: string; // YYYY-MM-DD
  to: string; // YYYY-MM-DD
  requestedSpots: readonly RequestedSpotGroupDto[];
  note?: string;
  groupReservationId?: string;
  groupReservationSecret?: string;
};

export type CreateWebReservationResponse = {
  id: string;
  number: string;
  secret: string;
};

// GET /nationalities

export type Nationality = {
  id: string;
  name: string;
  alpha2: string;
  alpha3: string;
  numeric: string;
  visaRequired: boolean;
  biometricsRequired: boolean;
  isEu: boolean;
  languageId: string;
  languageCode: string;
};

// GET /reservations/{id}/guest?secret=...

export type ReservationGuestGroupSpot = {
  id: string;
  name: string;
};

export type ReservationGuestServiceText = {
  languageId: string;
  printText: string;
};

export type ReservationGuestSpotItem = {
  spotGroupId: string;
  spotGroupName: string;
  spotId: string | null;
  spotName: string | null;
  groupSpots: readonly ReservationGuestGroupSpot[];
  serviceTexts: readonly ReservationGuestServiceText[];
};

export type ReservationGuestMealAmount = {
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

export type ReservationGuestMeal = {
  date: string; // YYYY-MM-DD
  breakfast: ReservationGuestMealAmount;
  lunch: ReservationGuestMealAmount;
  lunchPackage: ReservationGuestMealAmount;
  dinner: ReservationGuestMealAmount;
};

export type ReservationGuestBill = {
  id: string;
  number: string;
  kind: string;
  amount: number;
};

export type ReservationForGuestResponse = {
  id: string;
  number: string;
  state: string;
  from: string; // YYYY-MM-DD
  to: string; // YYYY-MM-DD
  name: string;
  surname: string;
  note: string | null;
  groupReservationId: string | null;
  spotItems: readonly ReservationGuestSpotItem[];
  meals: readonly ReservationGuestMeal[];
  bills: readonly ReservationGuestBill[];
};
