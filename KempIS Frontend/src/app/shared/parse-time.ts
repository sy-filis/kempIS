// Returns null when cleared, "HH:MM" when parseable, undefined when input
// is non-empty but unparseable (caller treats as "no change").
export function parseTimeInput(
  raw: string | null | undefined
): string | null | undefined {
  if (raw === null || raw === undefined) {
    return null;
  }
  const trimmed = raw.trim();
  if (trimmed === "") {
    return null;
  }
  const normalized = trimmed.replace(/[.\s]/g, ":");
  let hour: number;
  let minute: number;
  if (normalized.includes(":")) {
    const [h, m = "0"] = normalized.split(":");
    hour = Number(h);
    minute = Number(m);
  } else if (/^\d+$/.test(normalized)) {
    if (normalized.length <= 2) {
      hour = Number(normalized);
      minute = 0;
    } else if (normalized.length === 3) {
      hour = Number(normalized.slice(0, 1));
      minute = Number(normalized.slice(1));
    } else if (normalized.length === 4) {
      hour = Number(normalized.slice(0, 2));
      minute = Number(normalized.slice(2));
    } else {
      return undefined;
    }
  } else {
    return undefined;
  }
  if (
    !Number.isFinite(hour) ||
    !Number.isFinite(minute) ||
    hour < 0 ||
    hour > 23 ||
    minute < 0 ||
    minute > 59
  ) {
    return undefined;
  }
  return `${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`;
}
