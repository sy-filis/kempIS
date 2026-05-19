import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
} from "@angular/core";

import { ButtonModule } from "primeng/button";

import { ApiClient } from "../../../../core/api/api-client";
import {
  type GroupReservationDetail,
  GroupReservationState,
} from "../../../api/group-reservations.types";

const STATE_LABELS: Record<GroupReservationState, string> = {
  [GroupReservationState.Confirmed]: "Potvrzena",
  [GroupReservationState.Canceled]: "Zrušena",
};

const ISO_DATE_RE = /^(\d{4})-(\d{2})-(\d{2})$/;

function formatCzechDate(iso: string): string {
  const m = ISO_DATE_RE.exec(iso);
  if (!m) {
    return iso;
  }
  const [, y, mo, d] = m;
  return `${Number(d)}. ${Number(mo)}. ${y}`;
}

@Component({
  selector: "kemp-is-group-reservation-summary",
  imports: [ButtonModule],
  templateUrl: "./group-reservation-summary.html",
  styleUrl: "./group-reservation-summary.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GroupReservationSummary {
  private readonly apiClient = inject(ApiClient);

  readonly groupReservationId = input<string | null>(null);
  readonly spotId = input<string | null>(null);

  readonly editClicked = output<string>();
  readonly createReservationClicked = output<{
    readonly groupReservationId: string;
    readonly spotId: string;
  }>();

  protected readonly detail = httpResource<GroupReservationDetail>(() => {
    const id = this.groupReservationId();
    return id ? this.apiClient.url(`/group-reservations/${id}`) : undefined;
  });

  protected readonly stateLabel = computed(() => {
    const d = this.detail.hasValue() ? this.detail.value() : null;
    return d ? STATE_LABELS[d.state] : "";
  });

  protected readonly formattedRange = computed(() => {
    const d = this.detail.hasValue() ? this.detail.value() : null;
    return d ? `${formatCzechDate(d.from)} – ${formatCzechDate(d.to)}` : "";
  });

  protected readonly spotsLabel = computed(() => {
    const d = this.detail.hasValue() ? this.detail.value() : null;
    if (!d) {
      return "";
    }
    const count = d.spotIds.length;
    return `${count} chat`;
  });

  protected readonly canCreateReservation = computed<boolean>(() => {
    if (!this.detail.hasValue()) {
      return false;
    }
    return (
      this.detail.value().state === GroupReservationState.Confirmed &&
      this.spotId() !== null
    );
  });

  protected onEdit(): void {
    const id = this.groupReservationId();
    if (id) {
      this.editClicked.emit(id);
    }
  }

  protected onCreateReservation(): void {
    const id = this.groupReservationId();
    const spot = this.spotId();
    if (id && spot) {
      this.createReservationClicked.emit({
        groupReservationId: id,
        spotId: spot,
      });
    }
  }
}
