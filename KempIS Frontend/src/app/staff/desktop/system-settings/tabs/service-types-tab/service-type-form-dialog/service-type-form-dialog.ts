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

import { ButtonModule } from "primeng/button";
import { DialogModule } from "primeng/dialog";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { ToggleSwitchModule } from "primeng/toggleswitch";

import { isApiError } from "../../../../../../core/api/api-error";
import { ServiceTypesApi } from "../../../api/service-types.api";
import type { ServiceType } from "../../../shared/types";

@Component({
  selector: "kemp-is-service-type-form-dialog",
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    MessageModule,
    ToggleSwitchModule,
  ],
  templateUrl: "./service-type-form-dialog.html",
  styleUrl: "./service-type-form-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceTypeFormDialog {
  private readonly api = inject(ServiceTypesApi);

  readonly visible = model<boolean>(false);
  readonly serviceType = input<ServiceType | null>(null);

  readonly saved = output<string>();

  protected readonly title = computed(() =>
    this.serviceType() ? "Upravit typ služby" : "Nový typ služby"
  );

  protected readonly name = signal<string>("");
  protected readonly isActive = signal<boolean>(true);
  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly canSubmit = computed(() => {
    if (this.submitting()) {
      return false;
    }
    return this.name().trim().length > 0 && this.name().trim().length <= 255;
  });

  constructor() {
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const t = this.serviceType();
      if (t) {
        this.name.set(t.name);
        this.isActive.set(t.isActive);
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
    const existing = this.serviceType();
    const name = this.name().trim();
    const isActive = this.isActive();

    this.submitting.set(true);
    this.errorMessage.set(null);

    if (existing) {
      this.api.update(existing.id, { name, isActive }).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Typ „${name}“ byl uložen.`);
        },
        error: err => this.handleError(err),
      });
    } else {
      this.api.create({ name, isActive }).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Typ „${name}“ byl vytvořen.`);
        },
        error: err => this.handleError(err),
      });
    }
  }

  private handleError(err: unknown): void {
    this.submitting.set(false);
    if (isApiError(err) && err.status === 404) {
      this.errorMessage.set("Záznam již neexistuje, načtěte seznam znovu.");
      return;
    }
    this.errorMessage.set("Zkontrolujte vyplněné údaje.");
  }

  private reset(): void {
    this.name.set("");
    this.isActive.set(true);
    this.errorMessage.set(null);
  }
}
