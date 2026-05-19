import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from "@angular/core";

import { ButtonModule } from "primeng/button";
import { TagModule } from "primeng/tag";

import { ConnectTabletDialogComponent } from "../../bill/tablet-pairing/connect-tablet-dialog.component";
import { ReceptionPairingService } from "../../bill/tablet-pairing/reception-pairing.service";

@Component({
  selector: "kemp-is-tablet-pairing-settings",
  standalone: true,
  imports: [ButtonModule, ConnectTabletDialogComponent, TagModule],
  templateUrl: "./tablet-pairing-settings.html",
  styleUrl: "./tablet-pairing-settings.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TabletPairingSettings {
  private readonly pairing = inject(ReceptionPairingService);

  protected readonly dialogVisible = signal<boolean>(false);
  protected readonly state = this.pairing.state;
  protected readonly paired = this.pairing.isPaired;

  protected readonly pairingLabel = computed<{
    text: string;
    severity: "success" | "info" | "warn" | "danger" | "secondary";
  } | null>(() => {
    const s = this.state();
    switch (s.kind) {
      case "idle":
        return { text: "Nespárováno", severity: "secondary" };
      case "issuing":
        return { text: "Vystavuji kód…", severity: "info" };
      case "waitingForTablet":
        return { text: "Čekám na tablet", severity: "info" };
      case "paired":
        return { text: "Spárováno", severity: "success" };
      case "displaced":
        return { text: "Převzal jiný desktop", severity: "warn" };
      case "error":
        return { text: "Chyba", severity: "danger" };
    }
  });

  protected onClick(): void {
    if (this.paired()) {
      this.pairing.disconnect();
      return;
    }
    this.dialogVisible.set(true);
  }
}
