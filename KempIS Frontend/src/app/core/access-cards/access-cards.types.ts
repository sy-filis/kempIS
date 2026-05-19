export type AccessCardBillSummary = {
  id: string;
  number: string;
};

export type AccessCard = {
  id: string;
  uid: number;
  deposit: number;
  issuedAtUtc: string;
  note: string | null;
  bill: AccessCardBillSummary | null;
  /** Inclusive expiry date (ISO `YYYY-MM-DD`). The gate service refuses
   *  cards whose `validUntil` has passed. */
  validUntil: string;
};

export type IssueAccessCardRequest = {
  uid: number;
  deposit: number;
  billId: string | null;
  note: string | null;
  validUntil: string; // YYYY-MM-DD
};

export type UpdateAccessCardRequest = {
  validUntil: string; // YYYY-MM-DD
  note: string | null;
};
