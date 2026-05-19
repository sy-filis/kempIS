// Static lookups and date helpers used by the reservation plan view.

export type CottageStatus = "clean" | "dirty" | "occupied" | "maintenance";

// paid = checked-in; group = master skupinová bar; linkedToGroup =
// reservation belonging to a group; confirmed = standalone, not checked in.
export type ReservationKind = "paid" | "group" | "linkedToGroup" | "confirmed";

export type CottageStatusConfig = {
  readonly label: string;
  readonly icon: string;
  readonly color: string;
};

export const COTTAGE_STATUS_CONFIG: Record<CottageStatus, CottageStatusConfig> =
  {
    clean: { label: "Uklizeno", icon: "pi-check-circle", color: "#10b981" },
    dirty: { label: "K úklidu", icon: "pi-circle-fill", color: "#f59e0b" },
    occupied: { label: "Obsazeno", icon: "pi-user", color: "#3b82f6" },
    maintenance: { label: "Údržba", icon: "pi-wrench", color: "#71717a" },
  };

export type KindStyle = {
  readonly bg: string;
  readonly border: string;
  readonly text: string;
  readonly accent: string;
  readonly phone: string;
};

export const KIND_STYLES: Record<ReservationKind, KindStyle> = {
  paid: {
    bg: "#fce7f3",
    border: "#ec4899",
    text: "#9d174d",
    accent: "#ec4899",
    phone: "#be185d",
  },
  group: {
    bg: "#fef3c7",
    border: "#f59e0b",
    text: "#78350f",
    accent: "#f59e0b",
    phone: "#92400e",
  },
  // Teal; picked for hue distance from the group yellow, the standalone
  // indigo, and the camp's brand green.
  linkedToGroup: {
    bg: "#ccfbf1",
    border: "#0d9488",
    text: "#134e4a",
    accent: "#0d9488",
    phone: "#115e59",
  },
  confirmed: {
    bg: "#eef2ff",
    border: "#6366f1",
    text: "#312e81",
    accent: "#6366f1",
    phone: "#4338ca",
  },
};

export const MONTHS_CZ: readonly string[] = [
  "Leden",
  "Únor",
  "Březen",
  "Duben",
  "Květen",
  "Červen",
  "Červenec",
  "Srpen",
  "Září",
  "Říjen",
  "Listopad",
  "Prosinec",
];

export const DOW_CZ: readonly string[] = [
  "Po",
  "Út",
  "St",
  "Čt",
  "Pá",
  "So",
  "Ne",
];

export function daysInMonth(year: number, monthIdx: number): number {
  return new Date(year, monthIdx + 1, 0).getDate();
}

// Monday=0, Sunday=6.
export function dowMon(year: number, monthIdx: number, day: number): number {
  return (new Date(year, monthIdx, day).getDay() + 6) % 7;
}
