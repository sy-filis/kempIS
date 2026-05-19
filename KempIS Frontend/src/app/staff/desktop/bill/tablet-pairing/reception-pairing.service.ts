import { computed, inject, Injectable, signal } from "@angular/core";

import { firstValueFrom, Subject, takeUntil } from "rxjs";

import { ApiClient } from "../../../../core/api/api-client";
import { EdokladyCounterStore } from "../../../../core/edoklady/edoklady-counter.store";
import {
  EdokladyPresentationService,
  type PresentationFailure,
  type PresentationStatus,
} from "../../../../core/edoklady/edoklady-presentation.service";
export type { PresentationStatus } from "../../../../core/edoklady/edoklady-presentation.service";
import {
  AttributeDataType,
  type PresentedDocument,
  TransactionStateKind,
} from "../../../../core/edoklady/edoklady.types";
import { RealtimeClient } from "../../../../core/reception-realtime/realtime-client";
import {
  type EdokladyOutcome,
  type EdokladyState,
  ReceptionEventNames,
} from "../../../../core/reception-realtime/reception-event-names";
import type {
  BillSummaryDto,
  EdokladyResultPayload,
  EdokladyStartPayload,
  GuestSigningEntryDto,
  PresentedAttributeDto,
  SignatureCapturedPayload,
} from "../../../../core/reception-realtime/reception-event-types";
import { ReceptionPairCodesApi } from "../../../../core/reception-realtime/reception-pair-codes.api";
import { injectReceptionRealtimeUrlBuilder } from "../../../../core/reception-realtime/reception-realtime-url";

export type PairingState =
  | { kind: "idle" }
  | { kind: "issuing" }
  | { kind: "waitingForTablet"; pairCode: string; expiresAtUtc: string }
  | { kind: "paired" }
  | { kind: "displaced" }
  | { kind: "error"; message: string };

export type DesktopGuestSink = {
  /** Returns the persisted backend guest id, or null for a draft. */
  resolvePersistedGuestId(clientGuestId: string): string | null;
  /** Buffer the PNG on a draft guest so POST /bills carries it via
   *  signaturePngBase64; empty string clears. */
  bufferDraftSignature(clientGuestId: string, pngBase64: string): void;
};

@Injectable()
export class ReceptionPairingService {
  private readonly pairCodesApi = inject(ReceptionPairCodesApi);
  private readonly client = inject(RealtimeClient);
  private readonly apiClient = inject(ApiClient);
  private readonly counterStore = inject(EdokladyCounterStore);
  private readonly presentations = inject(EdokladyPresentationService);
  private readonly buildRealtimeUrl = injectReceptionRealtimeUrlBuilder();

  private readonly _state = signal<PairingState>({ kind: "idle" });
  readonly state = this._state.asReadonly();
  readonly isPaired = computed(() => this._state().kind === "paired");

  private readonly edokladyCancellers = new Map<string, Subject<void>>();

  /** Serialized payload of the last `session:push` we emitted. `pushSession`
   *  bails out when called with structurally-identical data so the upstream
   *  reactive flow (a `signal`-driven effect that re-runs on any change in
   *  the bill state) can fire freely without flooding the WebSocket. */
  private lastSessionSerialized: string | null = null;

  private sink: DesktopGuestSink | null = null;
  private rebuildSession: (() => void) | null = null;

  attach(sink: DesktopGuestSink, rebuildSession: () => void): void {
    this.sink = sink;
    this.rebuildSession = rebuildSession;
  }

  /** Clears the bill-context callbacks. Does NOT close the socket — the
   *  pairing instance is hosted by `DesktopHomePage` and the connection
   *  is meant to persist across page navigations. Tablet disconnection
   *  goes through `disconnect()` (settings UI button, peer-left, etc.). */
  detach(): void {
    this.sink = null;
    this.rebuildSession = null;
  }

  async pairTablet(): Promise<void> {
    this._state.set({ kind: "issuing" });
    try {
      const code = await firstValueFrom(this.pairCodesApi.create());
      this._state.set({
        kind: "waitingForTablet",
        pairCode: code.pairCode,
        expiresAtUtc: code.expiresAtUtc,
      });
      this.openSocket(code.pairCode);
    } catch (err) {
      this._state.set({
        kind: "error",
        message: err instanceof Error ? err.message : "Vystavení kódu selhalo.",
      });
    }
  }

  disconnect(): void {
    for (const cancel of this.edokladyCancellers.values()) {
      cancel.next();
      cancel.complete();
    }
    this.edokladyCancellers.clear();
    this.lastSessionSerialized = null;
    this.client.disconnect();
    this._state.set({ kind: "idle" });
  }

  pushSession(payload: {
    bill: BillSummaryDto;
    guests: readonly GuestSigningEntryDto[];
  }): void {
    if (this._state().kind !== "paired") {
      return;
    }
    const serialized = JSON.stringify(payload);
    if (serialized === this.lastSessionSerialized) {
      return;
    }
    this.lastSessionSerialized = serialized;
    this.client.emit(ReceptionEventNames.SessionPush, payload);
  }

  /** Tells the paired tablet to drop the current bill view and return to
   *  the waiting screen. Used when the bill page is destroyed (operator
   *  leaves bill-creation mode) so the tablet doesn't keep showing stale
   *  bill data. No-op when no tablet is paired. */
  clearSession(): void {
    if (this._state().kind !== "paired") {
      return;
    }
    this.lastSessionSerialized = null;
    this.client.emit(ReceptionEventNames.SessionClear, {});
  }

  /** Sends the eDokladys counter QR to the paired tablet so the guest can
   *  scan it with their phone. No-op when no tablet is paired. The caller
   *  is responsible for driving the presentation locally and feeding its
   *  status into `broadcastEdokladyStatus` / `broadcastEdokladyCancel`. */
  broadcastEdokladyTransaction(
    clientGuestId: string,
    transactionId: string,
    vscQrData: string,
    vscQrValidTo: string
  ): void {
    if (this._state().kind !== "paired") {
      return;
    }
    this.client.emit(ReceptionEventNames.EdokladyTransaction, {
      clientGuestId,
      transactionId,
      vscQrData,
      vscQrValidTo,
    });
  }

  /** Mirrors a local presentation's status to the paired tablet using the
   *  same `state`/`result` translation as the tablet-initiated path. */
  broadcastEdokladyStatus(
    clientGuestId: string,
    transactionId: string,
    status: PresentationStatus
  ): void {
    if (this._state().kind !== "paired") {
      return;
    }
    this.translateStatus(clientGuestId, transactionId, status);
  }

  /** Tells the paired tablet to back out of the eDokladys QR view. */
  broadcastEdokladyCancel(clientGuestId: string): void {
    if (this._state().kind !== "paired") {
      return;
    }
    this.client.emit(ReceptionEventNames.EdokladyCancel, { clientGuestId });
  }

  private openSocket(pairCode: string): void {
    const url = this.buildRealtimeUrl();
    this.client.connect({ url });

    this.client.on(ReceptionEventNames.PairReady, () => {
      this._state.set({ kind: "paired" });
      this.rebuildSession?.();
    });

    this.client.on(ReceptionEventNames.PairPeerLeft, () => {
      this.disconnect();
    });

    this.client.on(ReceptionEventNames.PairDisplaced, () => {
      this.client.disconnect();
      this._state.set({ kind: "displaced" });
    });

    this.client.on(ReceptionEventNames.Error, err => {
      this._state.set({
        kind: "error",
        message: `${err.code}: ${err.message}`,
      });
    });

    this.client.on(ReceptionEventNames.SignatureCaptured, payload =>
      this.onSignatureCaptured(payload)
    );

    this.client.on(ReceptionEventNames.SignatureCleared, payload => {
      const persistedId = this.sink?.resolvePersistedGuestId(
        payload.clientGuestId
      );
      if (persistedId !== null && persistedId !== undefined) {
        this.apiClient
          .delete(`/guests/${encodeURIComponent(persistedId)}/signature`)
          .subscribe({ error: () => undefined });
      } else {
        this.sink?.bufferDraftSignature(payload.clientGuestId, "");
      }
      this.client.emit(ReceptionEventNames.GuestSignatureCleared, {
        clientGuestId: payload.clientGuestId,
      });
    });

    this.client.on(
      ReceptionEventNames.EdokladyStart,
      payload => void this.onEdokladyStart(payload)
    );

    this.client.on(ReceptionEventNames.EdokladyCancel, payload => {
      const cancel = this.edokladyCancellers.get(payload.clientGuestId);
      if (cancel) {
        cancel.next();
        cancel.complete();
        this.edokladyCancellers.delete(payload.clientGuestId);
      }
    });

    this.client.emit(ReceptionEventNames.PairJoin, {
      pairCode,
      role: "desktop",
    });
  }

  private onSignatureCaptured(payload: SignatureCapturedPayload): void {
    if (this.sink === null) {
      return;
    }
    const persistedId = this.sink.resolvePersistedGuestId(
      payload.clientGuestId
    );
    const capturedAtUtc = new Date().toISOString();
    if (persistedId !== null) {
      this.apiClient
        .put(`/guests/${encodeURIComponent(persistedId)}/signature`, {
          signaturePngBase64: payload.pngBase64,
        })
        .subscribe({
          next: () =>
            this.client.emit(ReceptionEventNames.GuestSigned, {
              clientGuestId: payload.clientGuestId,
              capturedAtUtc,
            }),
          error: () => undefined,
        });
    } else {
      this.sink.bufferDraftSignature(payload.clientGuestId, payload.pngBase64);
      this.client.emit(ReceptionEventNames.GuestSigned, {
        clientGuestId: payload.clientGuestId,
        capturedAtUtc,
      });
    }
  }

  private async onEdokladyStart(payload: EdokladyStartPayload): Promise<void> {
    try {
      const counter = await this.counterStore.ensureCounter();
      const cancel = new Subject<void>();
      this.edokladyCancellers.set(payload.clientGuestId, cancel);
      // EdokladyPresentationService.start() does not surface the real
      // backend transactionId; synthesize one for tablet correlation.
      const transactionId = crypto.randomUUID();
      let emittedTransaction = false;
      this.presentations
        .start()
        .pipe(takeUntil(cancel))
        .subscribe({
          next: status => {
            if (!emittedTransaction && status.kind !== "starting") {
              emittedTransaction = true;
              this.client.emit(ReceptionEventNames.EdokladyTransaction, {
                clientGuestId: payload.clientGuestId,
                transactionId,
                vscQrData: counter.qrCode.data,
                vscQrValidTo: counter.qrCode.validTo,
              });
            }
            this.translateStatus(payload.clientGuestId, transactionId, status);
          },
          complete: () => {
            this.edokladyCancellers.delete(payload.clientGuestId);
          },
        });
    } catch {
      this.emitFailureResult(payload.clientGuestId, "UnknownError");
    }
  }

  private translateStatus(
    clientGuestId: string,
    transactionId: string,
    status: PresentationStatus
  ): void {
    switch (status.kind) {
      case "starting":
        return;
      case "waiting":
        this.client.emit(ReceptionEventNames.EdokladyState, {
          clientGuestId,
          transactionId,
          state: kindToState(status.state),
        });
        return;
      case "completed": {
        const attributes = flattenAttributes(status.result.documents);
        this.client.emit(ReceptionEventNames.EdokladyResult, {
          clientGuestId,
          outcome: "Success",
          attributes,
        });
        return;
      }
      case "failed":
        this.emitFailureResult(clientGuestId, failureToOutcome(status.reason));
        return;
    }
  }

  private emitFailureResult(
    clientGuestId: string,
    outcome: EdokladyOutcome
  ): void {
    const result: EdokladyResultPayload = {
      clientGuestId,
      outcome,
      attributes: [],
    };
    this.client.emit(ReceptionEventNames.EdokladyResult, result);
  }
}

function kindToState(kind: TransactionStateKind): EdokladyState {
  switch (kind) {
    case TransactionStateKind.Open:
      return "Open";
    case TransactionStateKind.WaitingForResponse:
      return "WaitingForResponse";
    case TransactionStateKind.ResponseReceived:
      return "ResponseReceived";
    case TransactionStateKind.Finished:
      return "Finished";
    case TransactionStateKind.Failed:
      return "Failed";
    case TransactionStateKind.Canceled:
      return "Canceled";
    case TransactionStateKind.Unfinished:
      return "Unfinished";
    case TransactionStateKind.Timeout:
      return "Timeout";
  }
}

function failureToOutcome(reason: PresentationFailure): EdokladyOutcome {
  switch (reason) {
    case "untrusted":
      return "Untrusted";
    case "missing-data":
      return "MissingData";
    case "expired":
      return "Expired";
    case "canceled":
    case "failed":
    case "timeout":
    case "client-timeout":
    case "transport":
    case "unknown":
      return "UnknownError";
  }
}

function flattenAttributes(
  documents: readonly PresentedDocument[]
): readonly PresentedAttributeDto[] {
  const out: PresentedAttributeDto[] = [];
  for (const doc of documents) {
    for (const a of doc.obtained) {
      out.push({
        name: a.name,
        dataType: attributeDataTypeToString(a.dataType),
        value: a.value,
      });
    }
  }
  return out;
}

function attributeDataTypeToString(
  t: AttributeDataType | number
): PresentedAttributeDto["dataType"] {
  switch (t) {
    case AttributeDataType.String:
      return "String";
    case AttributeDataType.Photo:
      return "Photo";
    case AttributeDataType.Date:
      return "Date";
    case AttributeDataType.Boolean:
      return "Boolean";
    case AttributeDataType.Sex:
      return "Sex";
    case AttributeDataType.ChangeOfData:
      return "ChangeOfData";
    case AttributeDataType.Image:
      return "Image";
    default:
      return "String";
  }
}
