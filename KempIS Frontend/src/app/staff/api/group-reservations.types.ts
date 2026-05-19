// Backend serializes GroupReservationState as the enum name (not numeric).
export const GroupReservationState = {
  Confirmed: "Confirmed",
  Canceled: "Canceled",
} as const;

export type GroupReservationState =
  (typeof GroupReservationState)[keyof typeof GroupReservationState];

export type GroupReservation = {
  id: string;
  state: GroupReservationState;
  from: string;
  to: string;
  organizerName: string;
  organizerEmail: string;
  organizerPhone: string;
  spotIds: readonly string[];
  createdAtUtc: string;
  displayName: string | null;
};

export type GroupReservationRequest = {
  from: string; // "YYYY-MM-DD"
  to: string; // "YYYY-MM-DD"
  spotIds: readonly string[];
  organizerName: string;
  organizerEmail: string;
  organizerPhone: string;
  note: string | null;
  displayName: string | null;
};

export type GroupReservationDetail = GroupReservation & {
  note: string | null;
};
