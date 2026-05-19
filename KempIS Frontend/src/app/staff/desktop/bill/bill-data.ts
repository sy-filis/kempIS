export type FeeCategoryId = "adult" | "child6_18" | "child0_6" | "senior";

export type DocType = "op" | "passport" | "other";

export type PreloadedGuest = {
  readonly id: string;
  readonly firstName: string;
  readonly surname: string;
  readonly birth: string;
  readonly street: string;
  readonly houseNumber: string;
  readonly postalCode: string;
  readonly city: string;
  readonly country: string;
  readonly citizenship: string;
  readonly docType: DocType;
  readonly docNumber: string;
  readonly fee: FeeCategoryId;
  readonly checked: boolean;
  /** Independent of `checked`; only effective when the guest is linked. */
  readonly paysFee: boolean;
  readonly payer: boolean;
};

export type FeeCategory = {
  readonly id: FeeCategoryId;
  readonly label: string;
  readonly rate: number;
  readonly note: string;
};

export type Vehicle = {
  readonly id: string;
  /** Backend Vehicle.Id for rows hydrated from a reservation; null for
   *  rows added in the wizard. */
  readonly persistentId: string | null;
  readonly plate: string;
  readonly type: string;
  /** Null while the row still needs a service pick. */
  readonly serviceId: string | null;
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

export type MealDay = {
  readonly date: string;
  readonly dow: string;
  readonly day: number;
  /** Breakfast count (all diet variants summed). */
  readonly b: number;
  readonly l: number;
  /** Lunch packet (balíček); separate from sit-down lunch. */
  readonly lp: number;
  readonly d: number;
};

export type RecapRow = {
  readonly id: string;
  readonly service: string;
  readonly days: number;
  readonly price: number;
  readonly qty: number;
  readonly vat: number;
};

export type Country = { readonly code: string; readonly label: string };

export const COUNTRIES: readonly Country[] = [
  { code: "CZ", label: "Česká republika" },
  { code: "SK", label: "Slovensko" },
  { code: "DE", label: "Německo" },
  { code: "AT", label: "Rakousko" },
  { code: "PL", label: "Polsko" },
  { code: "HU", label: "Maďarsko" },
  { code: "GB", label: "Velká Británie" },
  { code: "NL", label: "Nizozemsko" },
  { code: "FR", label: "Francie" },
  { code: "IT", label: "Itálie" },
  { code: "OTH", label: "Jiný stát" },
];

export type DocTypeOption = { readonly id: DocType; readonly label: string };

export const DOC_TYPES: readonly DocTypeOption[] = [
  { id: "op", label: "Občanský průkaz" },
  { id: "passport", label: "Cestovní pas" },
  { id: "other", label: "Jiný doklad / bez dokladu" },
];

export const FEE_CATEGORIES: readonly FeeCategory[] = [
  { id: "adult", label: "Dospělý", rate: 30, note: "18+" },
  { id: "child6_18", label: "Dítě 6–17 let", rate: 15, note: "snížená sazba" },
  { id: "child0_6", label: "Dítě do 6 let", rate: 0, note: "osvobozeno" },
  { id: "senior", label: "Senior 65+", rate: 0, note: "osvobozeno" },
];

export type BillStep = {
  readonly id: string;
  readonly label: string;
  readonly short: string;
  readonly icon: string;
  readonly requiresReservation?: boolean;
};

export const BILL_STEPS: readonly BillStep[] = [
  {
    id: "period",
    label: "Termín a hosté",
    short: "Termín",
    icon: "pi-calendar",
  },
  {
    id: "vehicles",
    label: "Vozidla a stany",
    short: "Vozidla",
    icon: "pi-car",
  },
  {
    id: "cottages",
    label: "Chatky",
    short: "Chatky",
    icon: "pi-home",
    requiresReservation: true,
  },
  {
    id: "meals",
    label: "Stravování",
    short: "Strava",
    icon: "pi-shopping-cart",
    requiresReservation: true,
  },
  {
    id: "invoices",
    label: "Faktury",
    short: "Faktury",
    icon: "pi-file",
    requiresReservation: true,
  },
  { id: "other", label: "Ostatní služby", short: "Ostatní", icon: "pi-bolt" },
  { id: "recap", label: "Rekapitulace", short: "Rekap", icon: "pi-list" },
  { id: "accessCards", label: "Karty", short: "Karty", icon: "pi-id-card" },
  { id: "payment", label: "Platba", short: "Platba", icon: "pi-credit-card" },
];

export const STEP_LABELS: Record<string, string> = {
  period: "Pokračovat na vozidla",
  vehicles: "Pokračovat na chatky",
  cottages: "Pokračovat na stravu",
  meals: "Pokračovat na faktury",
  invoices: "Pokračovat na ostatní",
  other: "Pokračovat na rekapitulaci",
  recap: "Pokračovat na karty",
  accessCards: "Vystavit a pokračovat na platbu",
  payment: "Přijmout platbu a uzavřít",
};
