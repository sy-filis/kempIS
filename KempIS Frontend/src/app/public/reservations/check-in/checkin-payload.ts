import { DocumentType } from "./document-types";

export type CheckInModelLike = {
  firstName: string;
  lastName: string;
  birthDate: string;
  street: string;
  houseNumber: string;
  zipCode: string;
  city: string;
  countryId: string;
  nationalityId: string;
  documentType: DocumentType | null;
  documentNumber: string;
  biometric: boolean;
  visaNumber: string;
  plates: readonly string[];
  signaturePngBase64: string;
};

export type CheckInGuestDto = {
  firstName: string;
  lastName: string;
  birthDate: string;
  nationalityId: string;
  documentType: DocumentType | null;
  documentNumber: string | null;
  visaNumber: string | null;
  address: {
    countryId: string;
    city: string;
    zipCode: string;
    street: string;
    houseNumber: string;
  };
  signaturePngBase64: string | null;
};

export type CheckInVehicleDto = {
  registrationNumber: string;
  description: string | null;
};

export type CheckInRequest = {
  guests: readonly CheckInGuestDto[];
  vehicles: readonly CheckInVehicleDto[];
};

const BIOMETRIC_VISA_SENTINEL = "BIOMETRIKA";

export function toWireDto(model: CheckInModelLike): CheckInRequest {
  const trim = (s: string): string => s.trim();
  const docNumber = trim(model.documentNumber);
  const typedVisa = trim(model.visaNumber);
  const isBiometricPassport =
    model.documentType === DocumentType.Passport && model.biometric;
  const visaNumber = isBiometricPassport
    ? BIOMETRIC_VISA_SENTINEL
    : typedVisa === ""
      ? null
      : typedVisa;

  return {
    guests: [
      {
        firstName: trim(model.firstName),
        lastName: trim(model.lastName),
        birthDate: model.birthDate,
        nationalityId: model.nationalityId,
        documentType: model.documentType,
        documentNumber: docNumber === "" ? null : docNumber,
        visaNumber,
        address: {
          countryId: model.countryId,
          city: trim(model.city),
          zipCode: trim(model.zipCode),
          street: trim(model.street),
          houseNumber: trim(model.houseNumber),
        },
        signaturePngBase64: model.signaturePngBase64 || null,
      },
    ],
    vehicles: model.plates
      .map(p => p.trim())
      .filter(p => p.length > 0)
      .map(registrationNumber => ({
        registrationNumber,
        description: null,
      })),
  };
}
