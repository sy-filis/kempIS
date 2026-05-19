import type { TransactionResult } from "./edoklady.types";
import { isoToDate } from "../../shared/date-iso";
import { DocumentType } from "../../staff/desktop/reservations/reservation-form/reservation-form-stub-data";
import type { Nationality } from "../../staff/desktop/system-settings/shared/types";

/** Document name the backend hard-codes when starting a presentation
 *  (`EDokladyClient.cs:16`). */
const CZECH_MID_DOCUMENT_NAME = "org.iso.18013.5.1.CZ.mID";

export type EdokladyDraft = {
  firstName: string;
  lastName: string;
  /** `null` if `birth_date` was not in the obtained credentials. */
  birth: Date | null;
  documentType: DocumentType;
  documentNumber: string;
  /** `""` if the alpha-3 code didn't match any nationality (UI shows a
   *  blank picker for the receptionist to fix). */
  nationalityId: string;
  address: {
    street: string;
    houseNumber: string;
    zipCode: string;
    city: string;
    countryCode: string;
  };
};

/** Returns `null` if the result does not contain the Czech mID
 *  document; the trigger component treats that as `"missing-data"`. */
export function mapPresentationToDraft(
  result: TransactionResult,
  nationalitiesByAlpha3: ReadonlyMap<string, Nationality>
): EdokladyDraft | null {
  const document = result.documents.find(
    d => d.documentName === CZECH_MID_DOCUMENT_NAME
  );
  if (!document) {
    return null;
  }

  const attrs = new Map<string, string>(
    document.obtained.map(a => [a.name, a.value])
  );
  const value = (name: string): string => attrs.get(name) ?? "";

  const givenName = value("given_name");
  const familyName = value("family_name");
  const documentNumber = value("document_number");
  const birthIso = value("birth_date");

  const nationalityAlpha3 = value("nationality").toUpperCase();
  const nationality = nationalityAlpha3
    ? nationalitiesByAlpha3.get(nationalityAlpha3)
    : undefined;

  // `resident_city` overrides `resident_part_of_city` when both are
  // present; if only the part-of-city is present (e.g. historical
  // Prague district encoding) we fall back to it.
  const street = value("resident_street");
  const houseNumber = value("resident_city_house_number");
  const cityProper = value("resident_city");
  const cityPart = value("resident_part_of_city");
  const city = cityProper || cityPart;

  return {
    firstName: titleCase(givenName),
    lastName: titleCase(familyName),
    birth: isoToDate(birthIso),
    documentType: DocumentType.IdCard,
    documentNumber,
    nationalityId: nationality?.id ?? "",
    address: {
      street,
      houseNumber,
      zipCode: "",
      city,
      // Czech mobile ID is only issued to Czech nationals; the
      // residence address is always in CZ regardless of whether the
      // nationality catalogue includes the issuing-country alpha-3.
      countryCode: "CZ",
    },
  };
}

function titleCase(value: string): string {
  if (value === "") {
    return value;
  }
  return value
    .toLocaleLowerCase("cs")
    .replace(
      /(^|[\s\-'])(\p{L})/gu,
      (_, sep: string, ch: string) => sep + ch.toLocaleUpperCase("cs")
    );
}
