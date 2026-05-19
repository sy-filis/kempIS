import { SpotState as ApiSpotState } from "../api/spots.types";

export type SpotState = "free" | "arrival" | "departure" | "occupied" | "ooo";

export type SpotStateConfig = {
  readonly id: SpotState;
  readonly label: string;
  readonly icon: string;
  readonly dot: string;
  readonly bg: string;
  readonly fg: string;
};

export const SPOT_STATE_CONFIGS: Record<SpotState, SpotStateConfig> = {
  free: {
    id: "free",
    label: "Volné",
    icon: "pi-check-circle",
    dot: "#10b981",
    bg: "var(--p-primary-50)",
    fg: "var(--p-primary-700)",
  },
  arrival: {
    id: "arrival",
    label: "Příjezd",
    icon: "pi-sign-in",
    dot: "#2563eb",
    bg: "#dbeafe",
    fg: "#1e40af",
  },
  departure: {
    id: "departure",
    label: "Odjezd",
    icon: "pi-sign-out",
    dot: "#d97706",
    bg: "#fef3c7",
    fg: "#92400e",
  },
  occupied: {
    id: "occupied",
    label: "Obsazeno",
    icon: "pi-user",
    dot: "#52525b",
    bg: "#f4f4f5",
    fg: "#3f3f46",
  },
  ooo: {
    id: "ooo",
    label: "Mimo provoz",
    icon: "pi-wrench",
    dot: "#991b1b",
    bg: "#fee2e2",
    fg: "#991b1b",
  },
};

export const SPOT_STATES: readonly SpotState[] = [
  "free",
  "arrival",
  "departure",
  "occupied",
  "ooo",
];

export function mapApiSpotStateToUi(api: ApiSpotState): SpotState {
  switch (api) {
    case ApiSpotState.Unoccupied:
      return "free";
    case ApiSpotState.Occupied:
      return "occupied";
    case ApiSpotState.ExpectingArrival:
      return "arrival";
    case ApiSpotState.ExpectingDeparture:
      return "departure";
    case ApiSpotState.OutOfOrder:
      return "ooo";
  }
}
