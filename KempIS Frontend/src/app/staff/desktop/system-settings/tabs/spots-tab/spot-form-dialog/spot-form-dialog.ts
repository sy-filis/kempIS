import { httpResource } from "@angular/common/http";
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
import { SelectModule } from "primeng/select";
import { TextareaModule } from "primeng/textarea";
import { ToggleSwitchModule } from "primeng/toggleswitch";

import { ApiClient } from "../../../../../../core/api/api-client";
import { isApiError } from "../../../../../../core/api/api-error";
import { SpotsApi } from "../../../api/spots.api";
import type { CatalogueSpot, CatalogueSpotGroup } from "../../../shared/types";

@Component({
  selector: "kemp-is-spot-form-dialog",
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    TextareaModule,
    ToggleSwitchModule,
  ],
  templateUrl: "./spot-form-dialog.html",
  styleUrl: "./spot-form-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SpotFormDialog {
  private readonly apiClient = inject(ApiClient);
  private readonly api = inject(SpotsApi);

  readonly visible = model<boolean>(false);
  readonly spot = input<CatalogueSpot | null>(null);
  readonly defaultSpotGroupId = input<string | null>(null);

  readonly saved = output<string>();

  protected readonly spotGroupsResource = httpResource<
    readonly CatalogueSpotGroup[]
  >(() => this.apiClient.url("/spot-groups"));

  protected readonly title = computed(() =>
    this.spot() ? "Upravit místo" : "Nové místo"
  );

  protected readonly spotGroupId = signal<string>("");
  protected readonly name = signal<string>("");
  protected readonly description = signal<string>("");
  protected readonly isActive = signal<boolean>(true);
  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly spotGroupOptions = computed<CatalogueSpotGroup[]>(() => {
    const all = this.spotGroupsResource.hasValue()
      ? this.spotGroupsResource.value()
      : [];
    return [...all].sort((a, b) => a.name.localeCompare(b.name, "cs"));
  });

  protected readonly canSubmit = computed(() => {
    if (this.submitting()) {
      return false;
    }
    return (
      this.spotGroupId().length > 0 &&
      this.name().trim().length > 0 &&
      this.name().trim().length <= 255 &&
      this.description().length <= 1000
    );
  });

  constructor() {
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const s = this.spot();
      if (s) {
        this.spotGroupId.set(s.spotGroupId);
        this.name.set(s.name);
        this.description.set(s.description ?? "");
        this.isActive.set(s.isActive);
      } else {
        this.reset();
        const def = this.defaultSpotGroupId();
        if (def !== null) {
          this.spotGroupId.set(def);
        }
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
    const existing = this.spot();
    const description = this.description().trim();
    const body = {
      spotGroupId: this.spotGroupId(),
      name: this.name().trim(),
      description: description.length > 0 ? description : null,
      isActive: this.isActive(),
    };

    this.submitting.set(true);
    this.errorMessage.set(null);

    if (existing) {
      this.api.update(existing.id, body).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Místo „${body.name}“ bylo uloženo.`);
        },
        error: err => this.handleError(err),
      });
    } else {
      this.api.create(body).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Místo „${body.name}“ bylo vytvořeno.`);
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
    this.spotGroupId.set("");
    this.name.set("");
    this.description.set("");
    this.isActive.set(true);
    this.errorMessage.set(null);
  }
}
