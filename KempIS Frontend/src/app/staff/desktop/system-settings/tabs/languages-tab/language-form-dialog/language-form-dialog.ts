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

import { isApiError } from "../../../../../../core/api/api-error";
import { LanguagesApi } from "../../../api/languages.api";
import type { Language } from "../../../shared/types";

@Component({
  selector: "kemp-is-language-form-dialog",
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    MessageModule,
  ],
  templateUrl: "./language-form-dialog.html",
  styleUrl: "./language-form-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LanguageFormDialog {
  private readonly api = inject(LanguagesApi);

  readonly visible = model<boolean>(false);
  readonly language = input<Language | null>(null);

  readonly saved = output<string>();

  protected readonly mode = computed<"create" | "edit">(() =>
    this.language() ? "edit" : "create"
  );

  protected readonly title = computed(() =>
    this.mode() === "edit" ? "Upravit jazyk" : "Nový jazyk"
  );

  protected readonly code = signal<string>("");
  protected readonly name = signal<string>("");
  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly canSubmit = computed(() => {
    if (this.submitting()) {
      return false;
    }
    return (
      this.code().trim().length > 0 &&
      this.code().trim().length <= 10 &&
      this.name().trim().length > 0 &&
      this.name().trim().length <= 100
    );
  });

  constructor() {
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const lang = this.language();
      if (lang) {
        this.code.set(lang.code);
        this.name.set(lang.name);
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
    const existing = this.language();
    const code = this.code().trim();
    const name = this.name().trim();

    this.submitting.set(true);
    this.errorMessage.set(null);

    if (existing) {
      this.api.update(existing.id, { code, name }).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Jazyk „${name}“ byl uložen.`);
        },
        error: err => this.handleError(err),
      });
    } else {
      this.api.create({ code, name }).subscribe({
        next: () => {
          this.submitting.set(false);
          this.saved.emit(`Jazyk „${name}“ byl vytvořen.`);
        },
        error: err => this.handleError(err),
      });
    }
  }

  private handleError(err: unknown): void {
    this.submitting.set(false);
    if (isApiError(err) && err.status === 409) {
      this.errorMessage.set("Jazyk se zadaným kódem už existuje.");
      return;
    }
    if (isApiError(err) && err.status === 404) {
      this.errorMessage.set("Záznam již neexistuje, načtěte seznam znovu.");
      return;
    }
    this.errorMessage.set("Zkontrolujte vyplněné údaje.");
  }

  private reset(): void {
    this.code.set("");
    this.name.set("");
    this.errorMessage.set(null);
  }
}
