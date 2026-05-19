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
import { ToggleSwitchModule } from "primeng/toggleswitch";

import { ApiClient } from "../../../../../../core/api/api-client";
import { isApiError } from "../../../../../../core/api/api-error";
import { ServicesApi } from "../../../api/services.api";
import {
  SERVICE_GROUP_OPTIONS,
  ServiceGroup,
} from "../../../shared/service-groups";
import type { Service, ServiceType, VatRate } from "../../../shared/types";

@Component({
  selector: "kemp-is-service-form-dialog",
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    ToggleSwitchModule,
  ],
  templateUrl: "./service-form-dialog.html",
  styleUrl: "./service-form-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceFormDialog {
  private readonly apiClient = inject(ApiClient);
  private readonly api = inject(ServicesApi);

  readonly visible = model<boolean>(false);
  readonly service = input<Service | null>(null);

  readonly saved = output<string>();

  protected readonly serviceTypesResource = httpResource<
    readonly ServiceType[]
  >(() => this.apiClient.url("/service-types"));
  protected readonly vatRatesResource = httpResource<readonly VatRate[]>(() =>
    this.apiClient.url("/vat-rates")
  );

  protected readonly serviceGroupOptions: {
    label: string;
    value: ServiceGroup;
  }[] = [...SERVICE_GROUP_OPTIONS];

  protected readonly title = computed(() =>
    this.service() ? "Upravit službu" : "Nová služba"
  );

  protected readonly name = signal<string>("");
  protected readonly serviceGroup = signal<ServiceGroup>(ServiceGroup.Persons);
  protected readonly serviceTypeId = signal<string>("");
  protected readonly vatRateId = signal<string>("");
  protected readonly basePrice = signal<number | null>(null);
  protected readonly isActive = signal<boolean>(true);
  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly serviceTypeOptions = computed<ServiceType[]>(() => {
    const all = this.serviceTypesResource.hasValue()
      ? this.serviceTypesResource.value()
      : [];
    const selected = this.serviceTypeId();
    const visible = all.filter(t => t.isActive || t.id === selected);
    return [...visible].sort((a, b) => a.name.localeCompare(b.name, "cs"));
  });

  protected readonly vatRateOptions = computed<VatRate[]>(() => {
    const all = this.vatRatesResource.hasValue()
      ? this.vatRatesResource.value()
      : [];
    const selected = this.vatRateId();
    const visible = all.filter(v => v.isActive || v.id === selected);
    return [...visible].sort((a, b) => a.rate - b.rate);
  });

  protected readonly canSubmit = computed(() => {
    if (this.submitting()) {
      return false;
    }
    const price = this.basePrice();
    return (
      this.name().trim().length > 0 &&
      this.name().trim().length <= 255 &&
      this.serviceTypeId().length > 0 &&
      this.vatRateId().length > 0 &&
      price !== null &&
      price >= 0
    );
  });

  constructor() {
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const s = this.service();
      if (s) {
        this.name.set(s.name);
        this.serviceGroup.set(s.serviceGroup);
        this.serviceTypeId.set(s.serviceTypeId);
        this.vatRateId.set(s.vatRateId);
        this.basePrice.set(s.basePrice);
        this.isActive.set(s.isActive);
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
    const existing = this.service();
    const body = {
      name: this.name().trim(),
      serviceGroup: this.serviceGroup(),
      serviceTypeId: this.serviceTypeId(),
      vatRateId: this.vatRateId(),
      basePrice: this.basePrice() ?? 0,
      isActive: this.isActive(),
    };

    this.submitting.set(true);
    this.errorMessage.set(null);

    if (existing) {
      this.api.update(existing.id, body).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Služba „${body.name}“ byla uložena.`);
        },
        error: err => this.handleError(err),
      });
    } else {
      this.api.create(body).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Služba „${body.name}“ byla vytvořena.`);
        },
        error: err => this.handleError(err),
      });
    }
  }

  private handleError(err: unknown): void {
    this.submitting.set(false);
    if (isApiError(err) && err.status === 404) {
      this.errorMessage.set(
        "Některý z odkazovaných záznamů (typ služby, sazba DPH) již neexistuje."
      );
      return;
    }
    this.errorMessage.set("Zkontrolujte vyplněné údaje.");
  }

  private reset(): void {
    this.name.set("");
    this.serviceGroup.set(ServiceGroup.Persons);
    this.serviceTypeId.set("");
    this.vatRateId.set("");
    this.basePrice.set(null);
    this.isActive.set(true);
    this.errorMessage.set(null);
  }
}
