/** Wire types for the backend's `/api/edoklady/*` endpoints. Mirror of
 *  `Application.Abstractions.EDoklady`. Enums are JSON ordinals via
 *  default `System.Text.Json` settings. */

export const TransactionStateKind = {
  Canceled: 0,
  Failed: 1,
  Finished: 2,
  Open: 3,
  ResponseReceived: 4,
  Unfinished: 5,
  WaitingForResponse: 6,
  Timeout: 7,
} as const;
export type TransactionStateKind =
  (typeof TransactionStateKind)[keyof typeof TransactionStateKind];

export const PresentationOutcome = {
  Success: 0,
  Untrusted: 1,
  UnknownError: 2,
  MissingData: 3,
  Expired: 4,
} as const;
export type PresentationOutcome =
  (typeof PresentationOutcome)[keyof typeof PresentationOutcome];

export const AttributeDataType = {
  String: 0,
  Photo: 1,
  Date: 2,
  Boolean: 3,
  Sex: 4,
  ChangeOfData: 5,
  Image: 6,
} as const;
export type AttributeDataType =
  (typeof AttributeDataType)[keyof typeof AttributeDataType];

export type Geolocation = {
  latitude: number;
  longitude: number;
  toleranceInMeters: number;
};

export type QrCode = {
  data: string;
  validTo: string;
};

export type VirtualServiceCounter = {
  id: string;
  name: string | null;
  qrCode: QrCode;
  geolocation: Geolocation | null;
};

export type TransactionState = {
  id: string;
  state: TransactionStateKind;
  validTo: string;
};

export type PresentedAttribute = {
  name: string;
  dataType: AttributeDataType;
  value: string;
};

export type MissingAttribute = {
  name: string;
  dataType: AttributeDataType;
};

export type PresentedDocument = {
  documentName: string;
  obtained: readonly PresentedAttribute[];
  missing: readonly MissingAttribute[] | null;
  mDoc: string | null;
};

export type TransactionResult = {
  outcome: PresentationOutcome;
  documents: readonly PresentedDocument[];
};
