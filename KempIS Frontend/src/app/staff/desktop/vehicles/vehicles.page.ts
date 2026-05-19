import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ButtonModule } from "primeng/button";
import { DatePickerModule } from "primeng/datepicker";
import { IconFieldModule } from "primeng/iconfield";
import { InputIconModule } from "primeng/inputicon";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { TableModule } from "primeng/table";
import { TagModule } from "primeng/tag";

import { formatPeriod } from "./vehicles-data";
import { ApiClient } from "../../../core/api/api-client";
import { dateToIso } from "../../../shared/date-iso";
import type { Vehicle } from "../../api/vehicles.types";

const SEARCH_DEBOUNCE_MS = 250;

function startOfToday(): Date {
  const d = new Date();
  return new Date(d.getFullYear(), d.getMonth(), d.getDate());
}

@Component({
  selector: "kemp-is-vehicles",
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    MessageModule,
    TableModule,
    TagModule,
  ],
  templateUrl: "./vehicles.page.html",
  styleUrl: "./vehicles.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VehiclesPage {
  private readonly apiClient = inject(ApiClient);

  protected readonly from = signal<Date | null>(startOfToday());
  protected readonly to = signal<Date | null>(startOfToday());
  protected readonly search = signal<string>("");
  private readonly searchDebounced = signal<string>("");

  constructor() {
    effect(onCleanup => {
      const v = this.search();
      const id = setTimeout(
        () => this.searchDebounced.set(v),
        SEARCH_DEBOUNCE_MS
      );
      onCleanup(() => clearTimeout(id));
    });
  }

  protected readonly resource = httpResource<readonly Vehicle[]>(() => {
    const f = this.from();
    const t = this.to();
    if (!f || !t) {
      return undefined;
    }
    const params = new URLSearchParams({
      from: dateToIso(f),
      to: dateToIso(t),
    });
    const s = this.searchDebounced().trim();
    if (s.length > 0) {
      params.set("search", s);
    }
    return `${this.apiClient.url("/vehicles")}?${params.toString()}`;
  });

  protected readonly rows = computed<Vehicle[]>(() =>
    this.resource.hasValue() ? [...this.resource.value()] : []
  );

  protected readonly loading = computed(() => this.resource.isLoading());

  protected readonly hasError = computed(
    () => this.resource.error() !== undefined
  );

  protected readonly count = computed(() => this.rows().length);

  protected readonly minToDate = computed(() => this.from() ?? undefined);

  protected onRetry(): void {
    this.resource.reload();
  }

  protected readonly formatPeriod = formatPeriod;
}
