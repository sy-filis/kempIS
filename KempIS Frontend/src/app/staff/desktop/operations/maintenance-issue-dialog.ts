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
import { DialogModule } from "primeng/dialog";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { SelectModule } from "primeng/select";
import { TextareaModule } from "primeng/textarea";

import { SpotsStore } from "../../../core/spots/spots.store";
import { MaintenanceApi } from "../../api/maintenance.api";
import type { MaintenanceIssue } from "../../api/maintenance.types";

type SpotOption = {
  readonly label: string;
  readonly value: string | null;
};

@Component({
  selector: "kemp-is-ops-maintenance-issue-dialog",
  imports: [
    FormsModule,
    ButtonModule,
    ConfirmDialogModule,
    DialogModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    TextareaModule,
  ],
  providers: [ConfirmationService],
  templateUrl: "./maintenance-issue-dialog.html",
  styleUrl: "./maintenance-issue-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MaintenanceIssueDialog {
  private readonly maintenanceApi = inject(MaintenanceApi);
  private readonly spotsStore = inject(SpotsStore);
  private readonly confirm = inject(ConfirmationService);

  readonly visible = model<boolean>(false);
  readonly issue = input<MaintenanceIssue | null>(null);

  readonly saved = output<void>();
  readonly deleted = output<void>();

  protected readonly mode = computed<"create" | "edit">(() =>
    this.issue() ? "edit" : "create"
  );

  protected readonly title = computed(() =>
    this.mode() === "edit" ? "Upravit závadu" : "Nahlásit závadu"
  );

  protected readonly problemDescription = signal<string>("");
  protected readonly spotId = signal<string | null>(null);
  protected readonly note = signal<string>("");
  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly spotOptions = computed<SpotOption[]>(() => {
    const list = this.spotsStore.spots.hasValue()
      ? this.spotsStore.spots.value()
      : [];
    const options: SpotOption[] = [{ label: "Bez místa", value: null }];
    for (const s of [...list]
      .filter(s => s.isActive)
      .sort((a, b) => a.name.localeCompare(b.name, "cs", { numeric: true }))) {
      options.push({ label: s.name, value: s.id });
    }
    return options;
  });

  protected readonly canSubmit = computed(() => {
    if (this.submitting()) {
      return false;
    }
    return this.problemDescription().trim().length > 0;
  });

  constructor() {
    // Reopening the same row keeps `issue` reference-equal; track `visible` to retrigger the prefill.
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const i = this.issue();
      if (i) {
        this.problemDescription.set(i.problemDescription);
        this.spotId.set(i.spotId);
        this.note.set(i.note ?? "");
      } else {
        this.reset();
      }
      this.errorMessage.set(null);
    });
  }

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
  }

  protected onSubmit(): void {
    if (!this.canSubmit()) {
      return;
    }
    const problemDescription = this.problemDescription().trim();
    const note = this.note().trim();
    const noteValue = note.length === 0 ? null : note;

    this.submitting.set(true);
    this.errorMessage.set(null);

    const existing = this.issue();
    if (existing) {
      this.maintenanceApi
        .update(existing.id, { problemDescription, note: noteValue })
        .subscribe({
          next: () => {
            this.submitting.set(false);
            this.visible.set(false);
            this.saved.emit();
          },
          error: () => this.handleError("Uložení závady selhalo."),
        });
    } else {
      this.maintenanceApi
        .create({
          spotId: this.spotId(),
          problemDescription,
          note: noteValue,
        })
        .subscribe({
          next: () => {
            this.submitting.set(false);
            this.visible.set(false);
            this.saved.emit();
          },
          error: () => this.handleError("Vytvoření závady selhalo."),
        });
    }
  }

  protected onDelete(): void {
    const existing = this.issue();
    if (!existing || this.submitting()) {
      return;
    }
    this.confirm.confirm({
      header: "Smazat závadu",
      message: `Opravdu chcete smazat závadu „${existing.problemDescription}“?`,
      icon: "pi pi-exclamation-triangle",
      acceptLabel: "Smazat",
      rejectLabel: "Zrušit",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        this.submitting.set(true);
        this.errorMessage.set(null);
        this.maintenanceApi.delete(existing.id).subscribe({
          next: () => {
            this.submitting.set(false);
            this.visible.set(false);
            this.deleted.emit();
          },
          error: () => this.handleError("Smazání závady selhalo."),
        });
      },
    });
  }

  private handleError(message: string): void {
    this.submitting.set(false);
    this.errorMessage.set(message);
  }

  private reset(): void {
    this.problemDescription.set("");
    this.spotId.set(null);
    this.note.set("");
    this.errorMessage.set(null);
  }
}
