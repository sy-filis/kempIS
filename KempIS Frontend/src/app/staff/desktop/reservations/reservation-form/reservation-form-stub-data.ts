// Shared types and catalogues used by the reservation-form steps.
// Filename retained for legacy reasons (originally a design-mode stub
// bundle); consumers still import from here.

export type Vehicle = {
  readonly id: string;
  readonly persistentId: string | null;
  readonly plate: string;
  readonly type: string;
  readonly serviceId: string;
  readonly nights: number;
  readonly ratePerNight: number;
};

export type VehicleType = {
  readonly id: string;
  readonly label: string;
  readonly rate: number;
};

export type Tent = {
  readonly id: string;
  readonly label: string;
  readonly ratePerNight: number;
  readonly qty: number;
  readonly nights: number;
};

export const DIET_VARIANTS = [
  "normal",
  "glutenFree",
  "lactoseFree",
  "vegetarian",
  "glutenFreeLactoseFree",
  "glutenFreeVegetarian",
  "lactoseFreeVegetarian",
  "glutenFreeLactoseFreeVegetarian",
] as const;

export type DietVariant = (typeof DIET_VARIANTS)[number];

export const DIET_LABELS: Record<DietVariant, string> = {
  normal: "Standardní",
  glutenFree: "Bezlepková",
  lactoseFree: "Bezlaktózová",
  vegetarian: "Vegetariánská",
  glutenFreeLactoseFree: "Bezlepková + bezlaktózová",
  glutenFreeVegetarian: "Bezlepková vegetariánská",
  lactoseFreeVegetarian: "Bezlaktózová vegetariánská",
  glutenFreeLactoseFreeVegetarian: "Bezlepková + bezlaktózová vegetariánská",
};

export const DIET_TAGS: Record<DietVariant, string> = {
  normal: "",
  glutenFree: "GF",
  lactoseFree: "LF",
  vegetarian: "VG",
  glutenFreeLactoseFree: "GF/LF",
  glutenFreeVegetarian: "GF/VG",
  lactoseFreeVegetarian: "LF/VG",
  glutenFreeLactoseFreeVegetarian: "GF/LF/VG",
};

export const MEAL_KINDS = [
  "breakfast",
  "lunch",
  "lunchPackage",
  "dinner",
] as const;

export type MealKind = (typeof MEAL_KINDS)[number];

export type MealAmounts = Record<DietVariant, number>;

export type MealDay = {
  // ISO YYYY-MM-DD; the wire identity of a meal record.
  readonly iso: string;
  readonly date: string;
  readonly dow: string;
  readonly day: number;
  readonly breakfast: MealAmounts;
  readonly lunch: MealAmounts;
  readonly lunchPackage: MealAmounts;
  readonly dinner: MealAmounts;
  // Pickup time per meal kind as "HH:MM"; null when not scheduled.
  readonly times: Record<MealKind, string | null>;
  // Diet variants pinned visible even when their counts are zero.
  readonly pinnedDiets: readonly DietVariant[];
};

// Mirrors Domain.Guests.DocumentType on the backend; serialized as the
// numeric value. Numeric value 4 (ForeignEuResidencePermit) was removed
// from the backend enum so it is intentionally absent here.
export const DocumentType = {
  Passport: 1,
  IdCard: 2,
  CzechResidencePermit: 3,
  LostPassportConfirmation: 5,
  CzechDiplomatCard: 6,
  ChildInParentPassport: 7,
} as const;

export type DocumentType = (typeof DocumentType)[keyof typeof DocumentType];

export type GuestAddress = {
  readonly street: string;
  readonly houseNumber: string;
  readonly zipCode: string;
  readonly city: string;
  // ISO 3166-1 alpha-2.
  readonly countryCode: string;
};

// Registered guest record ("kniha hostů"). Mirrors Domain.Guests.Guest
// minus reservation-level fields (StayDateRange, ReasonOfStay, etc.).
export type RegistryGuest = {
  readonly id: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly nationalityId: string;
  // "DD. M. YYYY" Czech display format.
  readonly birth: string;
  readonly documentType: DocumentType | null;
  readonly documentNumber: string | null;
  readonly visaNumber: string | null;
  readonly address: GuestAddress;
  readonly note: string | null;
};
