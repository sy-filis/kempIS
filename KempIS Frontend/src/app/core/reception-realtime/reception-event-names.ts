/** Ported verbatim from
 *  `Application.Reception.Realtime.ReceptionEventNames`. Any divergence
 *  is a bug. */
export const ReceptionEventNames = {
  // Sent by both peers; inspected by server, NOT relayed.
  PairJoin: "pair:join",

  // Server-emitted (not relayed; server is source).
  PairReady: "pair:ready",
  PairPeerLeft: "pair:peer_left",
  PairDisplaced: "pair:displaced",
  Error: "error",

  // Desktop → tablet (relayed).
  SessionPush: "session:push",
  SessionClear: "session:clear",
  GuestSigned: "guest:signed",
  GuestSignatureCleared: "guest:signature_cleared",
  EdokladyTransaction: "edoklady:transaction",
  EdokladyState: "edoklady:state",
  EdokladyResult: "edoklady:result",

  // Tablet → desktop (relayed).
  SignatureCaptured: "signature:captured",
  SignatureCleared: "signature:cleared",
  EdokladyStart: "edoklady:start",

  // Sendable by either peer (relayed).
  EdokladyCancel: "edoklady:cancel",
} as const;

export type ReceptionEventName =
  (typeof ReceptionEventNames)[keyof typeof ReceptionEventNames];

export type EdokladyState =
  | "Open"
  | "WaitingForResponse"
  | "ResponseReceived"
  | "Finished"
  | "Failed"
  | "Canceled"
  | "Unfinished"
  | "Timeout";

export type EdokladyOutcome =
  | "Success"
  | "Untrusted"
  | "UnknownError"
  | "MissingData"
  | "Expired";

export type ReceptionErrorCode =
  | "invalid_pair_code"
  | "role_taken"
  | "not_paired"
  | "payload_too_large"
  | "bad_request";

export type PeerRole = "desktop" | "tablet";
