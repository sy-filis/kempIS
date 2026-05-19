import { inject, Injectable } from "@angular/core";

import {
  catchError,
  defer,
  from,
  map,
  Observable,
  of,
  startWith,
  switchMap,
  takeUntil,
  takeWhile,
  timer,
} from "rxjs";

import { EdokladyCounterStore } from "./edoklady-counter.store";
import { EdokladyApi } from "./edoklady.api";
import {
  PresentationOutcome,
  type TransactionResult,
  type TransactionState,
  TransactionStateKind,
} from "./edoklady.types";

/** Agreed staff-side polling interval. */
const POLL_INTERVAL_MS = 5_000;

/** The counter QR is permanent (backend rotates near expiry), so a
 *  timeout doesn't lose state; the receptionist can retry. */
const HARD_TIMEOUT_MS = 180_000;

export type PresentationFailure =
  | "canceled"
  | "failed"
  | "timeout"
  | "client-timeout"
  | "untrusted"
  | "missing-data"
  | "unknown"
  | "expired"
  | "transport";

export type PresentationStatus =
  | { kind: "starting" }
  | { kind: "waiting"; state: TransactionStateKind }
  | { kind: "completed"; result: TransactionResult }
  | { kind: "failed"; reason: PresentationFailure };

@Injectable({ providedIn: "root" })
export class EdokladyPresentationService {
  private readonly counterStore = inject(EdokladyCounterStore);
  private readonly api = inject(EdokladyApi);

  start(): Observable<PresentationStatus> {
    const flow$: Observable<PresentationStatus> = defer(() =>
      from(this.counterStore.ensureCounter())
    ).pipe(
      switchMap(counter => this.api.startPresentation(counter.id)),
      switchMap(({ transactionId }) => this.pollUntilTerminal(transactionId)),
      takeUntil(timer(HARD_TIMEOUT_MS)),
      catchError(
        (): Observable<PresentationStatus> =>
          of({ kind: "failed", reason: "transport" })
      )
    );

    // If the hard timeout fires before a terminal status is emitted,
    // `takeUntil` completes the stream silently. Convert that into an
    // explicit `client-timeout` failure.
    return withClientTimeoutFallback(flow$).pipe(
      startWith<PresentationStatus>({ kind: "starting" })
    );
  }

  private pollUntilTerminal(
    transactionId: string
  ): Observable<PresentationStatus> {
    return timer(0, POLL_INTERVAL_MS).pipe(
      switchMap(() => this.api.getTransaction(transactionId)),
      switchMap(state => this.handleState(transactionId, state)),
      // `inclusive: true` lets the terminal pass through before the
      // source completes, so consumers see the final value.
      takeWhile(
        status => status.kind !== "completed" && status.kind !== "failed",
        true
      )
    );
  }

  private handleState(
    transactionId: string,
    state: TransactionState
  ): Observable<PresentationStatus> {
    switch (state.state) {
      case TransactionStateKind.Open:
      case TransactionStateKind.WaitingForResponse:
      case TransactionStateKind.ResponseReceived:
      case TransactionStateKind.Unfinished:
        return of<PresentationStatus>({ kind: "waiting", state: state.state });
      case TransactionStateKind.Canceled:
        return of<PresentationStatus>({ kind: "failed", reason: "canceled" });
      case TransactionStateKind.Failed:
        return of<PresentationStatus>({ kind: "failed", reason: "failed" });
      case TransactionStateKind.Timeout:
        return of<PresentationStatus>({ kind: "failed", reason: "timeout" });
      case TransactionStateKind.Finished:
        return this.api
          .getTransactionResult(transactionId)
          .pipe(
            map<TransactionResult, PresentationStatus>(result =>
              result.outcome === PresentationOutcome.Success
                ? { kind: "completed", result }
                : { kind: "failed", reason: outcomeToFailure(result.outcome) }
            )
          );
    }
  }
}

function outcomeToFailure(outcome: PresentationOutcome): PresentationFailure {
  switch (outcome) {
    case PresentationOutcome.Success:
      return "unknown"; // unreachable from handleState
    case PresentationOutcome.Untrusted:
      return "untrusted";
    case PresentationOutcome.MissingData:
      return "missing-data";
    case PresentationOutcome.UnknownError:
      return "unknown";
    case PresentationOutcome.Expired:
      return "expired";
  }
}

function withClientTimeoutFallback(
  source: Observable<PresentationStatus>
): Observable<PresentationStatus> {
  return new Observable<PresentationStatus>(subscriber => {
    let sawTerminal = false;
    const sub = source.subscribe({
      next: value => {
        if (value.kind === "completed" || value.kind === "failed") {
          sawTerminal = true;
        }
        subscriber.next(value);
      },
      error: err => subscriber.error(err),
      complete: () => {
        if (!sawTerminal) {
          subscriber.next({ kind: "failed", reason: "client-timeout" });
        }
        subscriber.complete();
      },
    });
    return () => sub.unsubscribe();
  });
}
