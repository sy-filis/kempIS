import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  model,
  output,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ConfirmationService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { DatePickerModule } from "primeng/datepicker";
import { DialogModule } from "primeng/dialog";
import { InputTextModule } from "primeng/inputtext";
import { MultiSelectModule } from "primeng/multiselect";
import { TextareaModule } from "primeng/textarea";

import { ApiClient } from "../../../../core/api/api-client";
import { dateToIso, isoToDate } from "../../../../shared/date-iso";
import type { CalendarEvent, EventRequest } from "../../../api/events.types";
import type { SpotGroup } from "../../../api/spots.types";

type SpotGroupOption = {
  readonly label: string;
  readonly value: string;
};

@Component({
  selector: "kemp-is-event-create-dialog",
  imports: [
    FormsModule,
    ButtonModule,
    ConfirmDialogModule,
    DatePickerModule,
    DialogModule,
    InputTextModule,
    MultiSelectModule,
    TextareaModule,
  ],
  providers: [ConfirmationService],
  templateUrl: "./event-create-dialog.html",
  styleUrl: "./event-create-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EventCreateDialog {
  private readonly apiClient = inject(ApiClient);
  private readonly confirmService = inject(ConfirmationService);

  readonly visible = model<boolean>(false);
  readonly spotGroups = input.required<readonly SpotGroup[]>();
  readonly event = input<CalendarEvent | null>(null);
  readonly viewOnly = input<boolean>(false);

  readonly created = output<string>();
  readonly updated = output<string>();
  readonly deleted = output<string>();

  protected readonly mode = computed<"create" | "edit">(() =>
    this.event() ? "edit" : "create"
  );

  protected readonly headerText = computed<string>(() => {
    if (this.viewOnly()) {
      return "Detail akce";
    }
    return this.mode() === "edit" ? "Upravit akci" : "Nová akce";
  });

  constructor() {
    // Track visible (not event) so reopening the same event still
    // refires the prefill - the event ref hasn't changed.
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const e = this.event();
      if (e) {
        this.name.set(e.name);
        this.description.set(e.description ?? "");
        this.startsAt.set(isoToDate(e.startsAt));
        this.endsAt.set(isoToDate(e.endsAt));
        this.selectedGroups.set([...e.spotGroupIds]);
        this.errorMessage.set(null);
      } else {
        this.reset();
      }
    });
  }

  protected readonly name = signal<string>("");
  protected readonly description = signal<string>("");
  protected readonly startsAt = signal<Date | null>(null);
  protected readonly endsAt = signal<Date | null>(null);
  protected readonly selectedGroups = signal<string[]>([]);

  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly groupOptions = computed<SpotGroupOption[]>(() =>
    [...this.spotGroups()]
      .map(g => ({ label: g.name, value: g.id }))
      .sort((a, b) => a.label.localeCompare(b.label, "cs"))
  );

  protected readonly canSubmit = computed(() => {
    const start = this.startsAt();
    const end = this.endsAt();
    if (this.submitting()) {
      return false;
    }
    if (this.name().trim().length === 0) {
      return false;
    }
    if (!start || !end) {
      return false;
    }
    if (this.selectedGroups().length === 0) {
      return false;
    }
    if (end.getTime() < start.getTime()) {
      return false;
    }
    return true;
  });

  protected onVisibleChange(visible: boolean): void {
    this.visible.set(visible);
    if (!visible) {
      this.reset();
    }
  }

  protected onCancel(): void {
    if (this.submitting()) {
      return;
    }
    this.visible.set(false);
    this.reset();
  }

  protected onSubmit(): void {
    if (this.viewOnly()) {
      return;
    }
    if (!this.canSubmit()) {
      return;
    }
    const start = this.startsAt();
    const end = this.endsAt();
    if (!start || !end) {
      return;
    }

    const desc = this.description().trim();
    const payload: EventRequest = {
      name: this.name().trim(),
      description: desc.length > 0 ? desc : null,
      startsAt: dateToIso(start),
      endsAt: dateToIso(end),
      spotGroupIds: [...this.selectedGroups()],
    };

    this.submitting.set(true);
    this.errorMessage.set(null);

    const existing = this.event();
    if (existing) {
      this.apiClient.put<void>(`/events/${existing.id}`, payload).subscribe({
        next: () => {
          this.submitting.set(false);
          this.updated.emit(existing.id);
          this.visible.set(false);
        },
        error: () => {
          this.submitting.set(false);
          this.errorMessage.set(
            "Nepodařilo se uložit akci. Zkontrolujte prosím vyplněné údaje."
          );
        },
      });
    } else {
      this.apiClient.post<string>("/events", payload).subscribe({
        next: id => {
          this.submitting.set(false);
          this.created.emit(id);
          this.visible.set(false);
          this.reset();
        },
        error: () => {
          this.submitting.set(false);
          this.errorMessage.set(
            "Nepodařilo se vytvořit akci. Zkontrolujte prosím vyplněné údaje."
          );
        },
      });
    }
  }

  protected onDelete(): void {
    if (this.viewOnly()) {
      return;
    }
    const existing = this.event();
    if (!existing || this.submitting()) {
      return;
    }
    this.confirmService.confirm({
      message: `Opravdu chcete smazat akci „${existing.name}“? Tato operace je nevratná.`,
      header: "Smazat akci",
      icon: "pi pi-exclamation-triangle",
      acceptLabel: "Smazat",
      rejectLabel: "Zrušit",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        this.submitting.set(true);
        this.errorMessage.set(null);
        this.apiClient.delete<void>(`/events/${existing.id}`).subscribe({
          next: () => {
            this.submitting.set(false);
            this.deleted.emit(existing.id);
            this.visible.set(false);
          },
          error: () => {
            this.submitting.set(false);
            this.errorMessage.set("Nepodařilo se smazat akci.");
          },
        });
      },
    });
  }

  private reset(): void {
    this.name.set("");
    this.description.set("");
    this.startsAt.set(null);
    this.endsAt.set(null);
    this.selectedGroups.set([]);
    this.errorMessage.set(null);
  }
}
