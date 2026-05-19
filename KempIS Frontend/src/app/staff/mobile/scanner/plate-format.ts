export function formatPlate(raw: string): string {
  const normalized = raw.replace(/[^A-Z0-9]/gi, "").toUpperCase();
  if (normalized.length === 7 && /^\d[A-Z0-9]{2}\d{4}$/.test(normalized)) {
    return `${normalized.slice(0, 3)} ${normalized.slice(3)}`;
  }
  return normalized;
}

export function toPlateCandidate(raw: string): string | null {
  const normalized = raw.replace(/[^A-Z0-9]/gi, "").toUpperCase();
  if (normalized.length < 5 || normalized.length > 9) {
    return null;
  }
  if (!/[A-Z]/.test(normalized) || !/\d/.test(normalized)) {
    return null;
  }
  return formatPlate(normalized);
}
