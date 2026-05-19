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
import { CheckboxModule } from "primeng/checkbox";
import { DialogModule } from "primeng/dialog";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { SelectModule } from "primeng/select";

import { ApiClient } from "../../../../../../core/api/api-client";
import { isApiError } from "../../../../../../core/api/api-error";
import { NationalitiesApi } from "../../../api/nationalities.api";
import type { Language, Nationality } from "../../../shared/types";

type LanguageOption = { readonly id: string; readonly label: string };

const ALPHA2 = /^[A-Z]{2}$/;
const ALPHA3 = /^[A-Z]{3}$/;
const NUMERIC3 = /^[0-9]{3}$/;
const MAX_NAME = 100;

@Component({
  selector: "kemp-is-nationality-form-dialog",
  imports: [
    FormsModule,
    ButtonModule,
    CheckboxModule,
    DialogModule,
    InputTextModule,
    MessageModule,
    SelectModule,
  ],
  templateUrl: "./nationality-form-dialog.html",
  styleUrl: "./nationality-form-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NationalityFormDialog {
  private readonly apiClient = inject(ApiClient);
  private readonly api = inject(NationalitiesApi);

  readonly visible = model<boolean>(false);
  readonly nationality = input<Nationality | null>(null);

  readonly saved = output<string>();

  protected readonly title = computed(() =>
    this.nationality() ? "Upravit národnost" : "Nová národnost"
  );

  protected readonly name = signal<string>("");
  protected readonly nameEn = signal<string>("");
  protected readonly alpha2 = signal<string>("");
  protected readonly alpha3 = signal<string>("");
  protected readonly numeric = signal<string>("");
  protected readonly languageId = signal<string | null>(null);
  protected readonly visaRequired = signal<boolean>(false);
  protected readonly biometricsRequired = signal<boolean>(false);
  protected readonly isEu = signal<boolean>(false);

  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  private readonly languagesResource = httpResource<readonly Language[]>(() =>
    this.apiClient.url("/languages")
  );

  protected readonly languageOptions = computed<LanguageOption[]>(() => {
    if (!this.languagesResource.hasValue()) {
      return [];
    }
    return [...this.languagesResource.value()]
      .sort((a, b) => a.code.localeCompare(b.code, "cs"))
      .map(l => ({ id: l.id, label: `${l.code.toUpperCase()} — ${l.name}` }));
  });

  protected readonly canSubmit = computed(() => {
    if (this.submitting()) {
      return false;
    }
    return (
      this.name().trim().length > 0 &&
      this.name().trim().length <= MAX_NAME &&
      this.nameEn().trim().length > 0 &&
      this.nameEn().trim().length <= MAX_NAME &&
      ALPHA2.test(this.alpha2()) &&
      ALPHA3.test(this.alpha3()) &&
      NUMERIC3.test(this.numeric()) &&
      this.languageId() !== null
    );
  });

  constructor() {
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const n = this.nationality();
      if (n) {
        this.name.set(n.name);
        this.nameEn.set(n.nameEn);
        this.alpha2.set(n.alpha2);
        this.alpha3.set(n.alpha3);
        this.numeric.set(n.numeric);
        this.languageId.set(n.languageId);
        this.isEu.set(n.isEu);
        this.visaRequired.set(n.visaRequired);
        this.biometricsRequired.set(n.biometricsRequired);
      } else {
        this.reset();
      }
      this.errorMessage.set(null);
    });
  }

  protected onAlpha2Change(value: string): void {
    this.alpha2.set(value.toUpperCase());
  }

  protected onAlpha3Change(value: string): void {
    this.alpha3.set(value.toUpperCase());
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
    const existing = this.nationality();
    const body = {
      name: this.name().trim(),
      nameEn: this.nameEn().trim(),
      alpha2: this.alpha2().trim(),
      alpha3: this.alpha3().trim(),
      numeric: this.numeric().trim(),
      languageId: this.languageId() as string,
      isEu: this.isEu(),
      visaRequired: this.visaRequired(),
      biometricsRequired: this.biometricsRequired(),
    };

    this.submitting.set(true);
    this.errorMessage.set(null);

    if (existing) {
      this.api.update(existing.id, body).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Národnost „${body.name}“ byla uložena.`);
        },
        error: err => this.handleError(err),
      });
    } else {
      this.api.create(body).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Národnost „${body.name}“ byla vytvořena.`);
        },
        error: err => this.handleError(err),
      });
    }
  }

  private handleError(err: unknown): void {
    this.submitting.set(false);
    if (isApiError(err) && err.status === 409) {
      this.errorMessage.set("Národnost se zadanými kódy už existuje.");
      return;
    }
    if (isApiError(err) && err.status === 404) {
      this.errorMessage.set("Záznam již neexistuje, načtěte seznam znovu.");
      return;
    }
    this.errorMessage.set("Zkontrolujte vyplněné údaje.");
  }

  private reset(): void {
    this.name.set("");
    this.nameEn.set("");
    this.alpha2.set("");
    this.alpha3.set("");
    this.numeric.set("");
    this.languageId.set(null);
    this.isEu.set(false);
    this.visaRequired.set(false);
    this.biometricsRequired.set(false);
    this.errorMessage.set(null);
  }
}
