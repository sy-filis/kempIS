import { isoToDate } from "../../../shared/date-iso";
import { formatDateRange } from "../../../shared/format-date-range";
import type { Guest } from "../../api/guests.types";

export type GuestStatus = "arriving" | "in-camp" | "checked-out";

export const HOST_COUNTRY_ALPHA2 = "CZ";

const DOCUMENT_TYPE_LABELS_CS: Record<number, string> = {
  1: "Cestovní pas",
  2: "Občanský průkaz",
  3: "Povolení k pobytu v ČR",
  4: "Povolení k pobytu v EU",
  5: "Náhradní cestovní doklad",
  6: "Diplomatický průkaz ČR",
  7: "Dítě v pasu rodiče",
};

const STATUS_LABELS_CS: Record<GuestStatus, string> = {
  "arriving": "Před příjezdem",
  "in-camp": "V kempu",
  "checked-out": "Odhlášen",
};

const STATUS_SEVERITY: Record<GuestStatus, "info" | "success" | "secondary"> = {
  "arriving": "info",
  "in-camp": "success",
  "checked-out": "secondary",
};

const STATUS_ICON: Record<GuestStatus, string> = {
  "arriving": "pi pi-clock",
  "in-camp": "pi pi-check",
  "checked-out": "pi pi-sign-out",
};

export function computeStatus(guest: Guest): GuestStatus {
  if (guest.checkOutAt !== null) {
    return "checked-out";
  }
  if (guest.checkInAt !== null) {
    return "in-camp";
  }
  return "arriving";
}

export function statusLabel(status: GuestStatus): string {
  return STATUS_LABELS_CS[status];
}

export function statusSeverity(
  status: GuestStatus
): "info" | "success" | "secondary" {
  return STATUS_SEVERITY[status];
}

export function statusIcon(status: GuestStatus): string {
  return STATUS_ICON[status];
}

export function documentTypeLabel(type: number | null): string {
  if (type === null) {
    return "—";
  }
  return DOCUMENT_TYPE_LABELS_CS[type] ?? "Doklad";
}

export function formatDob(iso: string): string {
  const d = isoToDate(iso);
  if (!d) {
    return iso;
  }
  return d.toLocaleDateString("cs-CZ", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

export function formatStay(from: string, to: string): string {
  return formatDateRange(from, to);
}

export function isCzechGuest(alpha2: string): boolean {
  return alpha2 === HOST_COUNTRY_ALPHA2;
}
