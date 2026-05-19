import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
} from "@angular/core";

import { ButtonModule } from "primeng/button";
import { MessageModule } from "primeng/message";
import { ProgressSpinnerModule } from "primeng/progressspinner";

import type {
  EdokladyResultPayload,
  EdokladyStatePayload,
  EdokladyTransactionPayload,
} from "../../core/reception-realtime/reception-event-types";
import { QrRenderComponent } from "../../staff/desktop/bill/tablet-pairing/qr-render.component";
import { ReceptionTabletService } from "../reception-tablet.service";

type EdokladyView =
  | { kind: "starting" }
  | {
      kind: "waiting";
      tx: EdokladyTransactionPayload;
      state: EdokladyStatePayload | null;
    }
  | { kind: "result"; result: EdokladyResultPayload };

@Component({
  selector: "kemp-is-tablet-edoklady",
  standalone: true,
  imports: [
    ButtonModule,
    MessageModule,
    ProgressSpinnerModule,
    QrRenderComponent,
  ],
  templateUrl: "./edoklady.page.html",
  styleUrl: "./edoklady.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EdokladyPage {
  private readonly tablet = inject(ReceptionTabletService);

  readonly clientGuestId = input.required<string>();

  protected readonly view = computed<EdokladyView | null>(() => {
    const s = this.tablet.state();
    if (s.kind !== "edoklady" || s.clientGuestId !== this.clientGuestId()) {
      return null;
    }
    if (s.phase === "starting") {
      return { kind: "starting" };
    }
    if ("result" in s.phase) {
      return { kind: "result", result: s.phase.result };
    }
    return { kind: "waiting", tx: s.phase.transaction, state: s.phase.state };
  });

  protected readonly stateLabel = computed<string>(() => {
    const v = this.view();
    if (!v || v.kind !== "waiting" || !v.state) {
      return "";
    }
    switch (v.state.state) {
      case "Open":
        return $localize`:@@tablet.edoklady.state.open:Otevřeno`;
      case "WaitingForResponse":
        return $localize`:@@tablet.edoklady.state.waiting:Čekání na odpověď…`;
      case "ResponseReceived":
        return $localize`:@@tablet.edoklady.state.received:Odpověď přijata`;
      case "Finished":
        return $localize`:@@tablet.edoklady.state.finished:Hotovo`;
      default:
        return "";
    }
  });

  protected onCancel(): void {
    this.tablet.cancelEdoklady(this.clientGuestId());
  }

  protected onContinue(): void {
    this.tablet.backToSession();
  }
}
