const ISO_DATE_RE = /^(\d{4})-(\d{2})-(\d{2})$/;

export function dateToIso(date: Date | null | undefined): string {
  if (!date || Number.isNaN(date.getTime())) {
    return "";
  }
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

export function isoToDate(iso: string): Date | null {
  const match = ISO_DATE_RE.exec(iso);
  if (!match) {
    return null;
  }
  const [, yStr, mStr, dStr] = match;
  const y = Number(yStr);
  const m = Number(mStr);
  const d = Number(dStr);
  const date = new Date(y, m - 1, d);
  if (
    date.getFullYear() !== y ||
    date.getMonth() !== m - 1 ||
    date.getDate() !== d
  ) {
    return null; // out-of-range like 2026-13-01 or 2026-02-30
  }
  return date;
}
