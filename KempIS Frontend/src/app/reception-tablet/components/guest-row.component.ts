import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from "@angular/core";

import { ButtonModule } from "primeng/button";
import { TagModule } from "primeng/tag";

import type { GuestSigningEntryDto } from "../../core/reception-realtime/reception-event-types";

type Badge =
  | {
      kind: "done";
      severity: "success" | "info";
      label: string;
    }
  | {
      kind: "todo";
      severity: "warn";
      label: string;
    }
  | {
      kind: "info";
      severity: "info" | "secondary";
      label: string;
    };

@Component({
  selector: "kemp-is-tablet-guest-row",
  standalone: true,
  imports: [ButtonModule, TagModule],
  templateUrl: "./guest-row.component.html",
  styleUrl: "./guest-row.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GuestRowComponent {
  readonly guest = input.required<GuestSigningEntryDto>();
  readonly guestSelected = output<GuestSigningEntryDto>();

  /** Czech guests are read-only on the tablet: identity is loaded via
   *  eDokladys initiated from the desktop (which renders the QR here),
   *  not by tapping a row. Foreign guests sign by tapping. */
  protected readonly interactive = computed<boolean>(() => {
    const g = this.guest();
    return !g.isCzech && !g.hasSignature;
  });

  protected readonly badge = computed<Badge>(() => {
    const g = this.guest();
    if (g.isCzech) {
      return g.hasEDokladyResult
        ? {
            kind: "done",
            severity: "success",
            label: $localize`:@@tablet.row.verified:Ověřeno`,
          }
        : {
            kind: "info",
            severity: "secondary",
            label: $localize`:@@tablet.row.czech.waiting:Čeká na ověření recepcí`,
          };
    }
    return g.hasSignature
      ? {
          kind: "done",
          severity: "success",
          label: $localize`:@@tablet.row.signed:Podepsáno`,
        }
      : {
          kind: "todo",
          severity: "warn",
          label: $localize`:@@tablet.row.sign:Podepsat`,
        };
  });

  protected onTap(): void {
    const g = this.guest();
    if (g.isCzech) {
      // Czech guests are not selectable on the tablet — see `interactive`.
      return;
    }
    if (!g.hasSignature) {
      this.guestSelected.emit(g);
    }
  }
}
