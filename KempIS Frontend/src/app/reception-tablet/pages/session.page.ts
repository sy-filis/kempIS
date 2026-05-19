import { DecimalPipe } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";
import { Router } from "@angular/router";

import { CardModule } from "primeng/card";
import { TableModule } from "primeng/table";

import type { GuestSigningEntryDto } from "../../core/reception-realtime/reception-event-types";
import { GuestRowComponent } from "../components/guest-row.component";
import { ReceptionTabletService } from "../reception-tablet.service";

@Component({
  selector: "kemp-is-tablet-session",
  standalone: true,
  imports: [DecimalPipe, CardModule, TableModule, GuestRowComponent],
  templateUrl: "./session.page.html",
  styleUrl: "./session.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SessionPage {
  private readonly tablet = inject(ReceptionTabletService);
  private readonly router = inject(Router);

  protected readonly state = this.tablet.state;
  protected readonly sessionView = computed(() => {
    const s = this.state();
    if (s.kind !== "session") {
      return null;
    }
    // p-table requires a mutable array; clone the readonly source.
    return {
      bill: s.bill,
      guests: s.guests,
      lines: [...s.bill.lines],
    };
  });

  protected onSelectGuest(g: GuestSigningEntryDto): void {
    // Czech guests are read-only on the tablet; the desktop drives eDokladys
    // and the tablet auto-navigates when it receives the QR transaction.
    if (g.isCzech) {
      return;
    }
    this.tablet.startSigning(g.clientGuestId);
    void this.router.navigate([
      "/reception-tablet/session/sign",
      g.clientGuestId,
    ]);
  }
}
