import {
  computed,
  DestroyRef,
  effect,
  inject,
  Injectable,
  signal,
} from "@angular/core";
import { Router } from "@angular/router";

import { RealtimeClient } from "../core/reception-realtime/realtime-client";
import { ReceptionEventNames } from "../core/reception-realtime/reception-event-names";
import type {
  BillSummaryDto,
  EdokladyResultPayload,
  EdokladyStatePayload,
  EdokladyTransactionPayload,
  GuestSignedPayload,
  GuestSigningEntryDto,
  PairPeerLeftPayload,
} from "../core/reception-realtime/reception-event-types";
import { injectReceptionRealtimeUrlBuilder } from "../core/reception-realtime/reception-realtime-url";

export type EdokladyPhase =
  | "starting"
  | {
      readonly transaction: EdokladyTransactionPayload;
      readonly state: EdokladyStatePayload | null;
    }
  | { readonly result: EdokladyResultPayload };

export type TabletState =
  | { kind: "idle" }
  | { kind: "connecting"; pairCode: string }
  | { kind: "waiting" }
  | {
      kind: "session";
      bill: BillSummaryDto;
      guests: readonly GuestSigningEntryDto[];
    }
  | { kind: "signing"; clientGuestId: string }
  | {
      kind: "edoklady";
      clientGuestId: string;
      phase: EdokladyPhase;
    }
  | { kind: "ended"; reason: string };

@Injectable()
export class ReceptionTabletService {
  private readonly client = inject(RealtimeClient);
  private readonly router = inject(Router);
  private readonly buildRealtimeUrl = injectReceptionRealtimeUrlBuilder();

  private readonly _state = signal<TabletState>({ kind: "idle" });
  readonly state = this._state.asReadonly();
  readonly isPaired = computed(() => {
    const k = this._state().kind;
    return k !== "idle" && k !== "connecting" && k !== "ended";
  });

  private currentBill: BillSummaryDto | null = null;
  private currentGuests: readonly GuestSigningEntryDto[] = [];

  constructor() {
    inject(DestroyRef).onDestroy(() => this.client.disconnect());

    // Transport-level drops (1006, 1001, unexpected 1000 before pair:ready)
    // don't carry an `error` event from the server, so the explicit `Error`
    // / `PairPeerLeft` / `PairDisplaced` handlers in `connect()` never fire.
    // Watch the underlying socket state so the tablet always falls back to
    // the scan-pair route when the connection dies mid-session.
    effect(() => {
      const conn = this.client.state();
      if (conn.kind !== "disconnected") {
        return;
      }
      const t = this._state();
      if (t.kind === "idle" || t.kind === "ended") {
        return;
      }
      this.endSession(conn.reason);
    });

    // Cold-load fallback: if the tablet state is `idle` we have neither an
    // active session nor a pending connect (typical after a manual reload
    // landing on /session). Force the scan-pair page so the operator can
    // re-pair instead of staring at a blank session view.
    effect(() => {
      if (this._state().kind !== "idle") {
        return;
      }
      if (this.router.url === "/reception-tablet") {
        return;
      }
      void this.router.navigate(["/reception-tablet"]);
    });
  }

  connect(pairCode: string): void {
    this._state.set({ kind: "connecting", pairCode });
    const url = this.buildRealtimeUrl();
    this.client.connect({ url });

    this.client.on(ReceptionEventNames.PairReady, () => {
      this._state.set({ kind: "waiting" });
      void this.router.navigate(["/reception-tablet/waiting"]);
    });

    this.client.on(ReceptionEventNames.PairPeerLeft, (p: PairPeerLeftPayload) =>
      this.endSession(`peer_left:${p.peerRole}`)
    );

    this.client.on(ReceptionEventNames.PairDisplaced, () =>
      this.endSession("displaced")
    );

    this.client.on(ReceptionEventNames.Error, err => this.endSession(err.code));

    this.client.on(ReceptionEventNames.SessionPush, payload => {
      this.currentBill = payload.bill;
      this.currentGuests = payload.guests;
      this._state.set({
        kind: "session",
        bill: payload.bill,
        guests: payload.guests,
      });
      void this.router.navigate(["/reception-tablet/session"]);
    });

    this.client.on(ReceptionEventNames.SessionClear, () => {
      this.currentBill = null;
      this.currentGuests = [];
      this._state.set({ kind: "waiting" });
      void this.router.navigate(["/reception-tablet/waiting"]);
    });

    this.client.on(ReceptionEventNames.GuestSigned, (p: GuestSignedPayload) =>
      this.applyGuestPatch(p.clientGuestId, { hasSignature: true })
    );

    this.client.on(ReceptionEventNames.EdokladyTransaction, p => {
      this._state.set({
        kind: "edoklady",
        clientGuestId: p.clientGuestId,
        phase: { transaction: p, state: null },
      });
      void this.router.navigate([
        "/reception-tablet/session/edoklady",
        p.clientGuestId,
      ]);
    });

    this.client.on(ReceptionEventNames.EdokladyCancel, p => {
      const s = this._state();
      if (s.kind === "edoklady" && s.clientGuestId === p.clientGuestId) {
        this.backToSession();
      }
    });

    this.client.on(ReceptionEventNames.EdokladyState, p => {
      const s = this._state();
      if (s.kind !== "edoklady" || s.clientGuestId !== p.clientGuestId) {
        return;
      }
      const phase = s.phase;
      if (phase === "starting" || "result" in phase) {
        return;
      }
      this._state.set({
        kind: "edoklady",
        clientGuestId: p.clientGuestId,
        phase: { transaction: phase.transaction, state: p },
      });
    });

    this.client.on(ReceptionEventNames.EdokladyResult, p => {
      this._state.set({
        kind: "edoklady",
        clientGuestId: p.clientGuestId,
        phase: { result: p },
      });
      if (p.outcome === "Success") {
        this.applyGuestPatch(p.clientGuestId, { hasEDokladyResult: true });
      }
    });

    this.client.emit(ReceptionEventNames.PairJoin, {
      pairCode,
      role: "tablet",
    });
  }

  startSigning(clientGuestId: string): void {
    this._state.set({ kind: "signing", clientGuestId });
  }

  submitSignature(clientGuestId: string, pngBase64: string): void {
    this.client.emit(ReceptionEventNames.SignatureCaptured, {
      clientGuestId,
      pngBase64,
    });
  }

  cancelEdoklady(clientGuestId: string): void {
    this.client.emit(ReceptionEventNames.EdokladyCancel, { clientGuestId });
    this.backToSession();
  }

  backToSession(): void {
    if (this.currentBill) {
      this._state.set({
        kind: "session",
        bill: this.currentBill,
        guests: this.currentGuests,
      });
      void this.router.navigate(["/reception-tablet/session"]);
    } else {
      this._state.set({ kind: "waiting" });
      void this.router.navigate(["/reception-tablet/waiting"]);
    }
  }

  endSession(reason: string): void {
    this.client.disconnect();
    this._state.set({ kind: "ended", reason });
    void this.router.navigate(["/reception-tablet"]);
  }

  private applyGuestPatch(
    clientGuestId: string,
    patch: Partial<GuestSigningEntryDto>
  ): void {
    this.currentGuests = this.currentGuests.map(g =>
      g.clientGuestId === clientGuestId ? { ...g, ...patch } : g
    );
    if (this.currentBill && this._state().kind === "session") {
      this._state.set({
        kind: "session",
        bill: this.currentBill,
        guests: this.currentGuests,
      });
    }
  }
}
