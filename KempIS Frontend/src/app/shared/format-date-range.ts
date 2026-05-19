import { isoToDate } from "./date-iso";

function fmtShort(d: Date): string {
  return d.toLocaleDateString("cs-CZ", { day: "2-digit", month: "2-digit" });
}

function fmtLong(d: Date): string {
  return d.toLocaleDateString("cs-CZ", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

export function formatDateRange(from: string, to: string): string {
  const f = isoToDate(from);
  const t = isoToDate(to);
  if (!f || !t) {
    return `${from} - ${to}`;
  }
  const sameYear = f.getFullYear() === t.getFullYear();
  return sameYear
    ? `${fmtShort(f)} - ${fmtLong(t)}`
    : `${fmtLong(f)} - ${fmtLong(t)}`;
}
