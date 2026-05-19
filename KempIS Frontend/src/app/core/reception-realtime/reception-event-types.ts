import type {
  EdokladyOutcome,
  EdokladyState,
  PeerRole,
  ReceptionErrorCode,
} from "./reception-event-names";

export type BillLineDto = {
  readonly label: string;
  readonly quantity: number;
  readonly unitPrice: number;
  readonly total: number;
};

export type BillSummaryDto = {
  readonly billId: string;
  readonly number: string;
  readonly payerDisplayName: string;
  /** ISO `YYYY-MM-DD`. */
  readonly checkInAt: string;
  readonly checkOutAt: string;
  readonly total: number;
  readonly currency: string;
  readonly lines: readonly BillLineDto[];
};

export type GuestSigningEntryDto = {
  readonly clientGuestId: string;
  readonly fullName: string;
  readonly nationality: string;
  readonly isCzech: boolean;
  readonly hasSignature: boolean;
  readonly hasEDokladyResult: boolean;
};

export type PresentedAttributeDto = {
  readonly name: string;
  /** "String" | "Photo" | "Date" | "Boolean" | "Sex" | "ChangeOfData" | "Image" */
  readonly dataType: string;
  readonly value: string;
};

export type PairJoinPayload = {
  readonly pairCode: string;
  readonly role: PeerRole;
};

export type SessionPushPayload = {
  readonly bill: BillSummaryDto;
  readonly guests: readonly GuestSigningEntryDto[];
};

export type GuestSignedPayload = {
  readonly clientGuestId: string;
  /** ISO 8601 UTC instant. */
  readonly capturedAtUtc: string;
};

export type GuestSignatureClearedPayload = {
  readonly clientGuestId: string;
};

export type EdokladyTransactionPayload = {
  readonly clientGuestId: string;
  readonly transactionId: string;
  readonly vscQrData: string;
  readonly vscQrValidTo: string;
};

export type EdokladyStatePayload = {
  readonly clientGuestId: string;
  readonly transactionId: string;
  readonly state: EdokladyState;
};

export type EdokladyResultPayload = {
  readonly clientGuestId: string;
  readonly outcome: EdokladyOutcome;
  readonly attributes: readonly PresentedAttributeDto[];
};

export type EdokladyCancelPayload = {
  readonly clientGuestId: string;
};

export type SignatureCapturedPayload = {
  readonly clientGuestId: string;
  /** Base64 PNG without `data:image/png;base64,` prefix. */
  readonly pngBase64: string;
};

export type SignatureClearedPayload = {
  readonly clientGuestId: string;
};

export type EdokladyStartPayload = {
  readonly clientGuestId: string;
};

export type PairReadyPayload = {
  readonly peerRole: PeerRole;
};

export type PairPeerLeftPayload = {
  readonly peerRole: PeerRole;
};

export type PairDisplacedPayload = {
  readonly reason: "new_pair";
};

export type ReceptionErrorPayload = {
  readonly code: ReceptionErrorCode;
  readonly message: string;
};
