import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  signal,
} from "@angular/core";

import { ButtonModule } from "primeng/button";

import { SignaturePadComponent } from "../../shared/signature-pad/signature-pad.component";
import { ReceptionTabletService } from "../reception-tablet.service";

@Component({
  selector: "kemp-is-tablet-signature",
  standalone: true,
  imports: [ButtonModule, SignaturePadComponent],
  templateUrl: "./signature.page.html",
  styleUrl: "./signature.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignaturePage {
  private readonly tablet = inject(ReceptionTabletService);

  readonly clientGuestId = input.required<string>();

  protected readonly value = signal<string>("");

  protected onConfirm(): void {
    const png = this.value();
    if (!png) {
      return;
    }
    this.tablet.submitSignature(this.clientGuestId(), png);
    this.tablet.backToSession();
  }

  protected onCancel(): void {
    this.tablet.backToSession();
  }
}
