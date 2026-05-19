import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  model,
  signal,
} from "@angular/core";

import { ButtonModule } from "primeng/button";
import { DialogModule } from "primeng/dialog";
import { MessageModule } from "primeng/message";
import { ProgressSpinnerModule } from "primeng/progressspinner";

import { QrRenderComponent } from "./qr-render.component";
import { ReceptionPairingService } from "./reception-pairing.service";

@Component({
  selector: "kemp-is-connect-tablet-dialog",
  standalone: true,
  imports: [
    ButtonModule,
    DialogModule,
    MessageModule,
    ProgressSpinnerModule,
    QrRenderComponent,
  ],
  templateUrl: "./connect-tablet-dialog.component.html",
  styleUrl: "./connect-tablet-dialog.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConnectTabletDialogComponent {
  private readonly pairing = inject(ReceptionPairingService);

  readonly visible = model<boolean>(false);

  protected readonly state = this.pairing.state;

  protected readonly waitingState = computed(() => {
    const s = this.state();
    return s.kind === "waitingForTablet" ? s : null;
  });

  private readonly now = signal<number>(Date.now());

  protected readonly remainingSeconds = computed<number | null>(() => {
    const w = this.waitingState();
    if (!w) {
      return null;
    }
    const expires = Date.parse(w.expiresAtUtc);
    if (Number.isNaN(expires)) {
      return null;
    }
    return Math.max(0, Math.ceil((expires - this.now()) / 1000));
  });

  protected readonly remainingLabel = computed<string | null>(() => {
    const s = this.remainingSeconds();
    if (s === null) {
      return null;
    }
    const m = Math.floor(s / 60);
    const r = s % 60;
    return `${m}:${String(r).padStart(2, "0")}`;
  });

  constructor() {
    const handle = setInterval(() => this.now.set(Date.now()), 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(handle));
  }

  protected readonly errorMessage = computed<string | null>(() => {
    const s = this.state();
    if (s.kind === "error") {
      return s.message;
    }
    if (s.kind === "displaced") {
      return "Párování převzal jiný desktop. Spusťte párování znovu.";
    }
    return null;
  });

  protected onPair(): void {
    void this.pairing.pairTablet();
  }

  protected onDisconnect(): void {
    this.pairing.disconnect();
  }

  protected onClose(): void {
    this.visible.set(false);
    if (this.state().kind !== "paired") {
      this.pairing.disconnect();
    }
  }
}
