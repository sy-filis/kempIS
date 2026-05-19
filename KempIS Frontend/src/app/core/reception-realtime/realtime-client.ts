import { DestroyRef, inject, Injectable, signal } from "@angular/core";

import type { ReceptionEventNames } from "./reception-event-names";
import type {
  EdokladyCancelPayload,
  EdokladyResultPayload,
  EdokladyStartPayload,
  EdokladyStatePayload,
  EdokladyTransactionPayload,
  GuestSignatureClearedPayload,
  GuestSignedPayload,
  PairDisplacedPayload,
  PairJoinPayload,
  PairPeerLeftPayload,
  PairReadyPayload,
  ReceptionErrorPayload,
  SessionPushPayload,
  SignatureCapturedPayload,
  SignatureClearedPayload,
} from "./reception-event-types";

export type ConnectionState =
  | { kind: "idle" }
  | { kind: "connecting" }
  | { kind: "connected" }
  | { kind: "disconnected"; reason: string };

export type OutboundEvents = {
  [ReceptionEventNames.PairJoin]: PairJoinPayload;
  [ReceptionEventNames.SessionPush]: SessionPushPayload;
  [ReceptionEventNames.SessionClear]: Record<string, never>;
  [ReceptionEventNames.GuestSigned]: GuestSignedPayload;
  [ReceptionEventNames.GuestSignatureCleared]: GuestSignatureClearedPayload;
  [ReceptionEventNames.EdokladyTransaction]: EdokladyTransactionPayload;
  [ReceptionEventNames.EdokladyState]: EdokladyStatePayload;
  [ReceptionEventNames.EdokladyResult]: EdokladyResultPayload;
  [ReceptionEventNames.EdokladyCancel]: EdokladyCancelPayload;
  [ReceptionEventNames.SignatureCaptured]: SignatureCapturedPayload;
  [ReceptionEventNames.SignatureCleared]: SignatureClearedPayload;
  [ReceptionEventNames.EdokladyStart]: EdokladyStartPayload;
};

export type InboundEvents = {
  [ReceptionEventNames.PairReady]: PairReadyPayload;
  [ReceptionEventNames.PairPeerLeft]: PairPeerLeftPayload;
  [ReceptionEventNames.PairDisplaced]: PairDisplacedPayload;
  [ReceptionEventNames.Error]: ReceptionErrorPayload;
  [ReceptionEventNames.SessionPush]: SessionPushPayload;
  [ReceptionEventNames.SessionClear]: Record<string, never>;
  [ReceptionEventNames.GuestSigned]: GuestSignedPayload;
  [ReceptionEventNames.GuestSignatureCleared]: GuestSignatureClearedPayload;
  [ReceptionEventNames.EdokladyTransaction]: EdokladyTransactionPayload;
  [ReceptionEventNames.EdokladyState]: EdokladyStatePayload;
  [ReceptionEventNames.EdokladyResult]: EdokladyResultPayload;
  [ReceptionEventNames.EdokladyCancel]: EdokladyCancelPayload;
  [ReceptionEventNames.SignatureCaptured]: SignatureCapturedPayload;
  [ReceptionEventNames.SignatureCleared]: SignatureClearedPayload;
  [ReceptionEventNames.EdokladyStart]: EdokladyStartPayload;
};

export type RealtimeConnectOptions = {
  readonly url: string;
};

export type ListenerHandle = {
  off(): void;
};

type Envelope = { event: string; data: unknown };
type Listener = (payload: unknown) => void;

@Injectable()
export class RealtimeClient {
  private socket: WebSocket | null = null;
  private readonly listeners = new Map<string, Set<Listener>>();
  /** Frames enqueued by `emit` while the socket is `CONNECTING`; flushed
   *  in FIFO order on `onopen`. Restores the buffering behavior the old
   *  `socket.io-client` provided implicitly — consumers (e.g. `pair:join`)
   *  emit synchronously right after `connect()`. */
  private readonly outboundQueue: string[] = [];
  /** Captured from the most recent server-sent `error` event so that the
   *  subsequent close (typically 1008) can surface the diagnostic code in
   *  the `disconnected.reason` field instead of a generic `"policy"`. */
  private lastErrorCode: string | null = null;
  private readonly _state = signal<ConnectionState>({ kind: "idle" });
  readonly state = this._state.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => this.disconnect());
  }

  connect(options: RealtimeConnectOptions): void {
    this.disconnect();
    this._state.set({ kind: "connecting" });
    this.lastErrorCode = null;
    this.outboundQueue.length = 0;
    console.debug("[realtime] connect", { url: options.url });

    const ws = new WebSocket(options.url);
    this.socket = ws;

    ws.onopen = (): void => {
      console.debug("[realtime] connected");
      this._state.set({ kind: "connected" });
      for (const frame of this.outboundQueue) {
        ws.send(frame);
      }
      this.outboundQueue.length = 0;
    };

    ws.onmessage = (event: MessageEvent<string>): void => {
      let envelope: Envelope;
      try {
        envelope = JSON.parse(event.data) as Envelope;
      } catch {
        console.debug("[realtime] recv (non-JSON frame, dropped)", event.data);
        return;
      }
      if (typeof envelope.event !== "string") {
        console.debug(
          "[realtime] recv (missing event field, dropped)",
          envelope
        );
        return;
      }
      console.debug("[realtime] recv", envelope.event, envelope.data);
      if (envelope.event === "error") {
        const code = (envelope.data as { code?: string } | null)?.code;
        if (typeof code === "string") {
          this.lastErrorCode = code;
        }
      }
      this.dispatch(envelope.event, envelope.data);
    };

    ws.onerror = (): void => {
      // The browser fires `close` after `error`, so defer state mutation to
      // `onclose` where we have the close code to map.
      console.debug("[realtime] socket error (close will follow)");
    };

    ws.onclose = (event: CloseEvent): void => {
      const reason = this.mapCloseReason(event);
      console.debug("[realtime] disconnected", { code: event.code, reason });
      this.lastErrorCode = null;
      this.socket = null;
      this.outboundQueue.length = 0;
      this._state.set({ kind: "disconnected", reason });
    };
  }

  disconnect(): void {
    if (this.socket) {
      try {
        this.socket.close(1000, "client_disconnect");
      } catch {
        // Already closing/closed; nothing to do.
      }
      this.socket = null;
    }
    this.listeners.clear();
    this.lastErrorCode = null;
    this.outboundQueue.length = 0;
    this._state.set({ kind: "idle" });
  }

  emit<K extends keyof OutboundEvents>(
    name: K,
    payload: OutboundEvents[K]
  ): void {
    const ws = this.socket;
    if (ws === null) {
      console.debug("[realtime] emit (dropped, no socket)", name, payload);
      return;
    }
    // Snapshot the payload at enqueue time so callers can safely mutate
    // their payload object after `emit` returns.
    const frame = JSON.stringify({ event: name as string, data: payload });
    if (ws.readyState === WebSocket.CONNECTING) {
      console.debug("[realtime] emit (queued, connecting)", name, payload);
      this.outboundQueue.push(frame);
      return;
    }
    if (ws.readyState !== WebSocket.OPEN) {
      console.debug("[realtime] emit (dropped, socket closing)", name, payload);
      return;
    }
    console.debug("[realtime] emit", name, payload);
    ws.send(frame);
  }

  on<K extends keyof InboundEvents>(
    name: K,
    handler: (payload: InboundEvents[K]) => void
  ): ListenerHandle {
    const wrapped: Listener = payload => {
      handler(payload as InboundEvents[K]);
    };
    const key = name as string;
    let set = this.listeners.get(key);
    if (!set) {
      set = new Set<Listener>();
      this.listeners.set(key, set);
    }
    set.add(wrapped);
    return {
      off: (): void => {
        const current = this.listeners.get(key);
        if (!current) {
          return;
        }
        current.delete(wrapped);
        if (current.size === 0) {
          this.listeners.delete(key);
        }
      },
    };
  }

  private dispatch(eventName: string, payload: unknown): void {
    const set = this.listeners.get(eventName);
    if (!set) {
      return;
    }
    // Copy so a listener that removes itself mid-dispatch doesn't mutate
    // the iteration target.
    for (const listener of [...set]) {
      try {
        listener(payload);
      } catch (err) {
        // Swallow consumer-thrown errors so one broken listener doesn't
        // kill the receive loop. Real protocol errors surface via the
        // server's `error` event.
        console.debug("[realtime] listener threw", eventName, err);
      }
    }
  }

  private mapCloseReason(event: CloseEvent): string {
    switch (event.code) {
      case 1000:
        return "closed";
      case 1001:
        return "server_shutdown";
      case 1008:
        return this.lastErrorCode ?? "policy";
      case 1006:
      default:
        return this.lastErrorCode ?? "connection_lost";
    }
  }
}
