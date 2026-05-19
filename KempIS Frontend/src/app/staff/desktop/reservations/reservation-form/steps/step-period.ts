import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  model,
  output,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ChipModule } from "primeng/chip";
import { DatePickerModule } from "primeng/datepicker";
import { InputNumberModule } from "primeng/inputnumber";
import { InputTextModule } from "primeng/inputtext";
import { TextareaModule } from "primeng/textarea";

import { StepPeriodGuestsStub } from "./step-period-guests-stub";
import type { ReservationDetailGuest } from "../../../../api/reservations.types";

const MS_PER_DAY = 1000 * 60 * 60 * 24;

@Component({
  selector: "kemp-is-reservation-step-period",
  imports: [
    FormsModule,
    DatePickerModule,
    InputNumberModule,
    InputTextModule,
    TextareaModule,
    ChipModule,
    StepPeriodGuestsStub,
  ],
  templateUrl: "./step-period.html",
  styleUrl: "./step-period.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StepPeriod {
  readonly from = model<Date | null>(null);
  readonly to = model<Date | null>(null);
  readonly name = model<string>("");
  readonly surname = model<string>("");
  readonly phone = model<string>("");
  readonly email = model<string>("");

  readonly displayName = model<string>("");
  readonly note = model<string>("");
  readonly groupReservationId = input<string | null>(null);
  readonly groupOrganizerName = input<string | null>(null);
  readonly showGuests = input<boolean>(true);
  readonly reservationId = input<string | null>(null);
  readonly guests = input<readonly ReservationDetailGuest[]>([]);
  readonly guestsMutated = output<void>();

  protected readonly nights = computed<number | null>(() => {
    const f = this.from();
    const t = this.to();
    if (!f || !t) {
      return null;
    }
    return Math.max(0, Math.round((t.getTime() - f.getTime()) / MS_PER_DAY));
  });

  protected onNightsChange(value: number | null): void {
    if (value === null || value < 0) {
      return;
    }
    const f = this.from();
    if (!f) {
      return;
    }
    const newTo = new Date(f);
    newTo.setDate(newTo.getDate() + value);
    this.to.set(newTo);
  }
}
