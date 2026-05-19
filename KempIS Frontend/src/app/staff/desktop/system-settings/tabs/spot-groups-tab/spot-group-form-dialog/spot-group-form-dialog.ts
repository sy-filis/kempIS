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
import { InputNumberModule } from "primeng/inputnumber";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { SelectModule } from "primeng/select";
import { TextareaModule } from "primeng/textarea";
import { ToggleSwitchModule } from "primeng/toggleswitch";

import { ApiClient } from "../../../../../../core/api/api-client";
import { isApiError } from "../../../../../../core/api/api-error";
import { SpotGroupsApi } from "../../../api/spot-groups.api";
import { ServiceGroup } from "../../../shared/service-groups";
import type { CatalogueSpotGroup, Service } from "../../../shared/types";

@Component({
  selector: "kemp-is-spot-group-form-dialog",
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    TextareaModule,
    ToggleSwitchModule,
  ],
  templateUrl: "./spot-group-form-dialog.html",
  styleUrl: "./spot-group-form-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SpotGroupFormDialog {
  private readonly apiClient = inject(ApiClient);
  private readonly api = inject(SpotGroupsApi);

  readonly visible = model<boolean>(false);
  readonly spotGroup = input<CatalogueSpotGroup | null>(null);

  readonly saved = output<string>();

  protected readonly servicesResource = httpResource<readonly Service[]>(() =>
    this.apiClient.url("/services")
  );

  protected readonly title = computed(() =>
    this.spotGroup() ? "Upravit skupinu míst" : "Nová skupina míst"
  );

  protected readonly serviceId = signal<string>("");
  protected readonly name = signal<string>("");
  protected readonly description = signal<string>("");
  protected readonly capacity = signal<number | null>(null);
  protected readonly isActive = signal<boolean>(true);
  protected readonly imageUrl = signal<string>("");
  protected readonly detailsUrl = signal<string>("");
  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly serviceOptions = computed<Service[]>(() => {
    const all = this.servicesResource.hasValue()
      ? this.servicesResource.value()
      : [];
    const selected = this.serviceId();
    const visible = all.filter(
      s =>
        s.id === selected ||
        (s.serviceGroup === ServiceGroup.Spots && s.isActive)
    );
    return [...visible].sort((a, b) => a.name.localeCompare(b.name, "cs"));
  });

  protected readonly canSubmit = computed(() => {
    if (this.submitting()) {
      return false;
    }
    const cap = this.capacity();
    return (
      this.serviceId().length > 0 &&
      this.name().trim().length > 0 &&
      this.name().trim().length <= 255 &&
      this.description().length <= 1000 &&
      cap !== null &&
      cap >= 1 &&
      this.imageUrl().trim().length > 0 &&
      this.imageUrl().trim().length <= 2048 &&
      this.detailsUrl().trim().length > 0 &&
      this.detailsUrl().trim().length <= 2048
    );
  });

  constructor() {
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const sg = this.spotGroup();
      if (sg) {
        this.serviceId.set(sg.serviceId);
        this.name.set(sg.name);
        this.description.set(sg.description ?? "");
        this.capacity.set(sg.capacity);
        this.isActive.set(sg.isActive);
        this.imageUrl.set(sg.imageUrl);
        this.detailsUrl.set(sg.detailsUrl);
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
    const existing = this.spotGroup();
    const description = this.description().trim();
    const body = {
      serviceId: this.serviceId(),
      name: this.name().trim(),
      description: description.length > 0 ? description : null,
      capacity: this.capacity() ?? 1,
      isActive: this.isActive(),
      imageUrl: this.imageUrl().trim(),
      detailsUrl: this.detailsUrl().trim(),
    };

    this.submitting.set(true);
    this.errorMessage.set(null);

    if (existing) {
      this.api.update(existing.id, body).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Skupina „${body.name}“ byla uložena.`);
        },
        error: err => this.handleError(err),
      });
    } else {
      this.api.create(body).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Skupina „${body.name}“ byla vytvořena.`);
        },
        error: err => this.handleError(err),
      });
    }
  }

  protected openDetails(): void {
    const url = this.detailsUrl().trim();
    if (url.length === 0) {
      return;
    }
    window.open(url, "_blank", "noopener");
  }

  private handleError(err: unknown): void {
    this.submitting.set(false);
    if (isApiError(err) && err.status === 400) {
      this.errorMessage.set("Vybraná služba není ve skupině Místa.");
      return;
    }
    if (isApiError(err) && err.status === 404) {
      this.errorMessage.set("Záznam již neexistuje, načtěte seznam znovu.");
      return;
    }
    this.errorMessage.set("Zkontrolujte vyplněné údaje.");
  }

  private reset(): void {
    this.serviceId.set("");
    this.name.set("");
    this.description.set("");
    this.capacity.set(null);
    this.isActive.set(true);
    this.imageUrl.set("");
    this.detailsUrl.set("");
    this.errorMessage.set(null);
  }
}
