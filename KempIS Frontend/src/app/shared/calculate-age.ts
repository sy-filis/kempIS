const ISO_DATE_RE = /^(\d{4})-(\d{2})-(\d{2})$/;

export function calculateAgeYears(isoDate: string, today: Date): number | null {
  const match = ISO_DATE_RE.exec(isoDate);
  if (!match) {
    return null;
  }
  const [, yStr, mStr, dStr] = match;
  const birthYear = Number(yStr);
  const birthMonth = Number(mStr);
  const birthDay = Number(dStr);
  let age = today.getFullYear() - birthYear;
  const m = today.getMonth() + 1;
  const d = today.getDate();
  if (m < birthMonth || (m === birthMonth && d < birthDay)) {
    age -= 1;
  }
  return age;
}
