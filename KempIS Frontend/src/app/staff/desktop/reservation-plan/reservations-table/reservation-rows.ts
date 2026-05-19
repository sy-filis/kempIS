// Maps API DTOs to the row shape consumed by ReservationsTable.

import type { ReservationRow, ReservationRowKind } from "./reservations-table";
import type { GroupReservation } from "../../../api/group-reservations.types";
import { GroupReservationState } from "../../../api/group-reservations.types";
import type { Reservation } from "../../../api/reservations.types";
import { ReservationState } from "../../../api/reservations.types";
import type { Spot } from "../../../api/spots.types";

const STATE_LABELS_RESERVATION: Record<ReservationState, string> = {
  [ReservationState.Created]: "Vytvořeno",
  [ReservationState.Confirmed]: "Potvrzeno",
  [ReservationState.CheckedIn]: "Ubytováno",
  [ReservationState.Cancelled]: "Zrušeno",
  [ReservationState.Completed]: "Dokončeno",
};

const STATE_LABELS_GROUP: Record<GroupReservationState, string> = {
  [GroupReservationState.Confirmed]: "Potvrzeno",
  [GroupReservationState.Canceled]: "Zrušeno",
};

const ISO_DATE_RE = /^(\d{4})-(\d{2})-(\d{2})$/;

function parseIsoDate(iso: string): Date | null {
  const m = ISO_DATE_RE.exec(iso);
  if (!m) {
    return null;
  }
  const [, y, mo, d] = m;
  return new Date(Number(y), Number(mo) - 1, Number(d));
}

function normalizeDisplayName(raw: string | null | undefined): string {
  return (raw ?? "").trim();
}

export function formatCzechDate(iso: string): string {
  const m = ISO_DATE_RE.exec(iso);
  if (!m) {
    return iso;
  }
  const [, y, mo, d] = m;
  return `${Number(d)}. ${Number(mo)}. ${y}`;
}

export function nightsBetween(fromIso: string, toIso: string): number {
  const from = parseIsoDate(fromIso);
  const to = parseIsoDate(toIso);
  if (!from || !to) {
    return 0;
  }
  return Math.max(0, Math.round((to.getTime() - from.getTime()) / 86_400_000));
}

function reservationKind(r: Reservation): ReservationRowKind {
  if (r.state === ReservationState.Cancelled) {
    return "cancelled";
  }
  if (r.state === ReservationState.CheckedIn) {
    return "paid";
  }
  if (r.groupReservationId) {
    return "linkedToGroup";
  }
  return "confirmed";
}

function cottageLabelFor(
  reservation: Reservation,
  spotsById: ReadonlyMap<string, Spot>
): string {
  const labels: string[] = [];
  for (const spotId of reservation.spotItems) {
    const spot = spotsById.get(spotId);
    if (!spot) {
      continue;
    }
    labels.push(spot.name);
  }
  return labels.length > 0 ? labels.join(", ") : "—";
}

export function reservationsToRows(
  reservations: readonly Reservation[],
  spots: readonly Spot[]
): readonly ReservationRow[] {
  const spotsById = new Map(spots.map(s => [s.id, s] as const));

  return reservations.map(r => {
    const kind = reservationKind(r);
    return {
      id: r.id,
      number: r.number,
      name: r.reservationMakerName.trim(),
      surname: r.reservationMakerSurname.trim(),
      displayName: normalizeDisplayName(r.displayName),
      phone: r.reservationMakerPhone,
      fromIso: r.from,
      toIso: r.to,
      fromLabel: formatCzechDate(r.from),
      toLabel: formatCzechDate(r.to),
      nights: nightsBetween(r.from, r.to),
      cottage: cottageLabelFor(r, spotsById),
      stateLabel: STATE_LABELS_RESERVATION[r.state],
      stateKind: kind,
      isGroup: r.groupReservationId !== null,
    };
  });
}

// Split a full name into first / last so the row fills both Jméno and
// Příjmení columns. Backend stores group organizers as a single name;
// the last whitespace-separated token becomes the surname.
function splitOrganizerName(full: string): { name: string; surname: string } {
  const parts = full.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) {
    return { name: "", surname: "" };
  }
  if (parts.length === 1) {
    return { name: parts[0] ?? "", surname: "" };
  }
  return {
    name: parts.slice(0, -1).join(" "),
    surname: parts[parts.length - 1] ?? "",
  };
}

// Czech plural: 1 chata, 2-4 chaty, 5+ chat.
function cottagesLabel(count: number): string {
  if (count === 1) {
    return "1 chata";
  }
  if (count >= 2 && count <= 4) {
    return `${count} chaty`;
  }
  return `${count} chat`;
}

export function groupReservationsToRows(
  groups: readonly GroupReservation[]
): readonly ReservationRow[] {
  return groups.map(g => {
    const cancelled = g.state === GroupReservationState.Canceled;
    const { name, surname } = splitOrganizerName(g.organizerName);
    return {
      id: g.id,
      number: g.id.slice(0, 8),
      name,
      surname,
      displayName: normalizeDisplayName(g.displayName),
      phone: g.organizerPhone,
      fromIso: g.from,
      toIso: g.to,
      fromLabel: formatCzechDate(g.from),
      toLabel: formatCzechDate(g.to),
      nights: nightsBetween(g.from, g.to),
      cottage: cottagesLabel(g.spotIds.length),
      stateLabel: STATE_LABELS_GROUP[g.state],
      stateKind: cancelled ? "cancelled" : "group",
      isGroup: true,
    };
  });
}
