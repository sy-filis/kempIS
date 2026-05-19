import type { Nationality } from "../api/public-reservations.types";

export const DocumentType = {
  Passport: 1,
  IdCard: 2,
  CzechResidencePermit: 3,
  ForeignEuResidencePermit: 4,
  LostPassportConfirmation: 5,
  CzechDiplomatCard: 6,
  ChildInParentPassport: 7,
} as const;

export type DocumentType = (typeof DocumentType)[keyof typeof DocumentType];

export const HOST_COUNTRY_ALPHA2 = "CZ";

const CZ_OR_EU_TYPES: readonly DocumentType[] = [
  DocumentType.Passport,
  DocumentType.IdCard,
];

const NON_EU_TYPES: readonly DocumentType[] = [
  DocumentType.Passport,
  DocumentType.CzechResidencePermit,
  DocumentType.ForeignEuResidencePermit,
  DocumentType.LostPassportConfirmation,
  DocumentType.CzechDiplomatCard,
  DocumentType.ChildInParentPassport,
];

export function documentTypesForNationality(
  nationality: Pick<Nationality, "alpha2" | "isEu"> | undefined
): readonly DocumentType[] {
  if (!nationality) {
    return [];
  }
  if (nationality.alpha2 === HOST_COUNTRY_ALPHA2 || nationality.isEu) {
    return CZ_OR_EU_TYPES;
  }
  return NON_EU_TYPES;
}
