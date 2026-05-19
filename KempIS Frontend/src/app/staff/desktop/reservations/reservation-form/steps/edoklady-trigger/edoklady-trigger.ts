import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  output,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";

import { ButtonModule } from "primeng/button";
import { MessageModule } from "primeng/message";
import { ProgressSpinnerModule } from "primeng/progressspinner";
import { Subject, takeUntil } from "rxjs";

import {
  type EdokladyDraft,
  mapPresentationToDraft,
} from "../../../../../../core/edoklady/edoklady-attributes";
import { EdokladyCounterStore } from "../../../../../../core/edoklady/edoklady-counter.store";
import {
  EdokladyPresentationService,
  type PresentationFailure,
  type PresentationStatus,
} from "../../../../../../core/edoklady/edoklady-presentation.service";
import { TransactionStateKind } from "../../../../../../core/edoklady/edoklady.types";
import { NationalitiesStore } from "../../../../../../core/nationalities/nationalities.store";
import { ReceptionPairingService } from "../../../../bill/tablet-pairing/reception-pairing.service";

@Component({
  selector: "kemp-is-edoklady-trigger",
  standalone: true,
  imports: [ButtonModule, MessageModule, ProgressSpinnerModule],
  templateUrl: "./edoklady-trigger.html",
  styleUrl: "./edoklady-trigger.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EdokladyTrigger {
  private readonly presentation = inject(EdokladyPresentationService);
  private readonly nationalities = inject(NationalitiesStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly pairing = inject(ReceptionPairingService);
  private readonly counterStore = inject(EdokladyCounterStore);

  readonly presented = output<EdokladyDraft>();

  protected readonly status = signal<PresentationStatus>({ kind: "starting" });
  protected readonly isRunning = signal<boolean>(false);
  protected readonly failure = signal<PresentationFailure | null>(null);

  // Tears down the in-flight polling without waiting for the hard timeout.
  private readonly cancel$ = new Subject<void>();

  /** Set while a presentation is broadcasting to a paired tablet; cleared
   *  on terminal status / cancel / failure. */
  private broadcastIds: {
    clientGuestId: string;
    transactionId: string;
  } | null = null;

  private readonly nationalitiesByAlpha3 = computed(() => {
    const map = new Map<
      string,
      ReturnType<NationalitiesStore["all"]>[number]
    >();
    for (const n of this.nationalities.all()) {
      map.set(n.alpha3, n);
    }
    return map;
  });

  protected readonly waitingLabel = computed<string>(() => {
    const s = this.status();
    if (s.kind !== "waiting") {
      return "";
    }
    switch (s.state) {
      case TransactionStateKind.Open:
      case TransactionStateKind.WaitingForResponse:
        return "Čeká na předložení dokladu…";
      case TransactionStateKind.ResponseReceived:
      case TransactionStateKind.Unfinished:
        return "Doklad přijat, zpracovává se…";
      default:
        return "Probíhá ověření…";
    }
  });

  protected readonly failureLabel = computed<string>(() => {
    const f = this.failure();
    if (f === null) {
      return "";
    }
    return failureToCzech(f);
  });

  protected async start(): Promise<void> {
    if (this.isRunning()) {
      return;
    }
    this.failure.set(null);
    this.isRunning.set(true);
    this.status.set({ kind: "starting" });
    await this.startTabletBroadcastIfPaired();

    this.presentation
      .start()
      .pipe(takeUntil(this.cancel$), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: status => this.handleStatus(status),
        error: () => {
          this.failure.set("transport");
          this.isRunning.set(false);
          this.endTabletBroadcastViaCancel();
        },
      });
  }

  protected cancel(): void {
    if (!this.isRunning()) {
      return;
    }
    this.cancel$.next();
    this.failure.set("canceled");
    this.isRunning.set(false);
    this.endTabletBroadcastViaCancel();
  }

  /** If a tablet is paired, fetch the eDokladys VSC QR and send it so the
   *  tablet shows the QR alongside the local presentation. Best-effort:
   *  a counter-fetch failure leaves the trigger in local-only mode. */
  private async startTabletBroadcastIfPaired(): Promise<void> {
    this.broadcastIds = null;
    if (!this.pairing.isPaired()) {
      return;
    }
    try {
      const counter = await this.counterStore.ensureCounter();
      const ids = {
        clientGuestId: crypto.randomUUID(),
        transactionId: crypto.randomUUID(),
      };
      this.pairing.broadcastEdokladyTransaction(
        ids.clientGuestId,
        ids.transactionId,
        counter.qrCode.data,
        counter.qrCode.validTo
      );
      this.broadcastIds = ids;
    } catch {
      // Counter unavailable — continue with local-only presentation.
    }
  }

  private endTabletBroadcastViaCancel(): void {
    if (this.broadcastIds === null) {
      return;
    }
    this.pairing.broadcastEdokladyCancel(this.broadcastIds.clientGuestId);
    this.broadcastIds = null;
  }

  protected dismissFailure(): void {
    this.failure.set(null);
  }

  private handleStatus(status: PresentationStatus): void {
    this.status.set(status);
    if (this.broadcastIds) {
      this.pairing.broadcastEdokladyStatus(
        this.broadcastIds.clientGuestId,
        this.broadcastIds.transactionId,
        status
      );
    }
    if (status.kind === "completed") {
      const draft = mapPresentationToDraft(
        status.result,
        this.nationalitiesByAlpha3()
      );
      if (draft) {
        this.presented.emit(draft);
        this.failure.set(null);
      } else {
        this.failure.set("missing-data");
      }
      this.isRunning.set(false);
      this.broadcastIds = null;
    } else if (status.kind === "failed") {
      this.failure.set(status.reason);
      this.isRunning.set(false);
      this.broadcastIds = null;
    }
  }
}

function failureToCzech(reason: PresentationFailure): string {
  switch (reason) {
    case "canceled":
      return "Předložení zrušeno.";
    case "failed":
    case "unknown":
      return "eDoklady nahlásily chybu.";
    case "timeout":
    case "client-timeout":
      return "Časový limit pro předložení dokladu vypršel.";
    case "untrusted":
      return "Doklad není důvěryhodný.";
    case "missing-data":
      return "Doklad neobsahuje požadované údaje.";
    case "expired":
      return "Platnost relace vypršela.";
    case "transport":
      return "Spojení se serverem selhalo.";
  }
}
