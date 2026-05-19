import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  signal,
  untracked,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { SelectModule } from "primeng/select";
import { TableModule } from "primeng/table";

import { ApiClient } from "../../../../../core/api/api-client";
import { isApiError } from "../../../../../core/api/api-error";
import { ServiceTextsApi } from "../../api/service-texts.api";
import type { Language, Service, ServiceText } from "../../shared/types";

const SAVE_DEBOUNCE_MS = 500;
const PRINT_TEXT_MAX = 1000;
const PRINT_TEXT_COUNTER_THRESHOLD = 800;

type RowState = "saved" | "unsaved" | "saving" | "error";

type Row = {
  readonly languageId: string;
  readonly languageCode: string;
  readonly languageName: string;
  text: string;
  readonly serverText: string | null;
  readonly serverId: string | null;
  state: RowState;
  errorMessage: string | null;
};

@Component({
  selector: "kemp-is-service-texts-tab",
  imports: [
    FormsModule,
    ButtonModule,
    InputTextModule,
    SelectModule,
    TableModule,
  ],
  templateUrl: "./service-texts-tab.page.html",
  styleUrl: "./service-texts-tab.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceTextsTabPage {
  private readonly apiClient = inject(ApiClient);
  private readonly api = inject(ServiceTextsApi);

  protected readonly servicesResource = httpResource<readonly Service[]>(() =>
    this.apiClient.url("/services")
  );
  protected readonly languagesResource = httpResource<readonly Language[]>(() =>
    this.apiClient.url("/languages")
  );
  protected readonly textsResource = httpResource<readonly ServiceText[]>(() =>
    this.apiClient.url("/service-texts")
  );

  protected readonly selectedServiceId = signal<string | null>(null);

  protected readonly serviceOptions = computed<Service[]>(() => {
    const all = this.servicesResource.hasValue()
      ? this.servicesResource.value()
      : [];
    return [...all].sort((a, b) => a.name.localeCompare(b.name, "cs"));
  });

  protected readonly rows = signal<Row[]>([]);

  protected readonly loading = computed(
    () =>
      this.servicesResource.isLoading() ||
      this.languagesResource.isLoading() ||
      this.textsResource.isLoading()
  );

  protected readonly hasService = computed(
    () => this.selectedServiceId() !== null
  );

  private readonly debouncers = new Map<
    string,
    ReturnType<typeof setTimeout>
  >();

  protected readonly trackByLanguageId = (_index: number, row: Row): string =>
    row.languageId;

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      for (const handle of this.debouncers.values()) {
        clearTimeout(handle);
      }
      this.debouncers.clear();
    });

    // Non-"saved" rows preserve local state so a reload triggered by row A doesn't wipe an in-flight row B edit.
    effect(() => {
      const serviceId = this.selectedServiceId();
      const languages = this.languagesResource.hasValue()
        ? this.languagesResource.value()
        : [];
      const texts = this.textsResource.hasValue()
        ? this.textsResource.value()
        : [];
      if (serviceId === null) {
        this.rows.set([]);
        return;
      }
      const priorByLanguage = new Map(
        untracked(() => this.rows()).map(r => [r.languageId, r])
      );
      const sortedLanguages = [...languages].sort((a, b) =>
        a.code.localeCompare(b.code, "cs")
      );
      const newRows: Row[] = sortedLanguages.map(lang => {
        const existing = texts.find(
          t => t.serviceId === serviceId && t.languageId === lang.id
        );
        const serverText = existing?.printText ?? null;
        const serverId = existing?.id ?? null;
        const prior = priorByLanguage.get(lang.id);
        if (prior !== undefined && prior.state !== "saved") {
          return {
            ...prior,
            serverText,
            serverId,
          };
        }
        return {
          languageId: lang.id,
          languageCode: lang.code,
          languageName: lang.name,
          text: existing?.printText ?? "",
          serverText,
          serverId,
          state: "saved",
          errorMessage: null,
        };
      });
      this.rows.set(newRows);
    });
  }

  protected onTextChange(languageId: string, text: string): void {
    this.rows.update(rows =>
      rows.map(r =>
        r.languageId === languageId
          ? {
              ...r,
              text,
              state: text === (r.serverText ?? "") ? "saved" : "unsaved",
              errorMessage: null,
            }
          : r
      )
    );
  }

  protected onTextBlur(languageId: string): void {
    const existing = this.debouncers.get(languageId);
    if (existing !== undefined) {
      clearTimeout(existing);
    }
    const handle = setTimeout(() => {
      this.debouncers.delete(languageId);
      this.saveRow(languageId);
    }, SAVE_DEBOUNCE_MS);
    this.debouncers.set(languageId, handle);
  }

  protected reloadAll(): void {
    this.textsResource.reload();
  }

  protected counterFor(text: string): string | null {
    if (text.length < PRINT_TEXT_COUNTER_THRESHOLD) {
      return null;
    }
    return `${text.length}/${PRINT_TEXT_MAX}`;
  }

  protected printTextMax = PRINT_TEXT_MAX;

  private saveRow(languageId: string): void {
    const serviceId = this.selectedServiceId();
    if (serviceId === null) {
      return;
    }
    const row = this.rows().find(r => r.languageId === languageId);
    if (!row) {
      return;
    }
    const text = row.text.trim();
    if (text === (row.serverText ?? "")) {
      return; // no change
    }
    if (text.length === 0) {
      const serverId = row.serverId;
      if (serverId === null) {
        return;
      }
      this.updateRow(languageId, { state: "saving", errorMessage: null });
      this.api.delete(serverId).subscribe({
        next: () => {
          this.rows.update(rows =>
            rows.map(r =>
              r.languageId === languageId
                ? {
                    ...r,
                    text: "",
                    serverText: null,
                    serverId: null,
                    state: "saved",
                    errorMessage: null,
                  }
                : r
            )
          );
          this.textsResource.reload();
        },
        error: err => this.markError(languageId, err),
      });
      return;
    }
    if (text.length > PRINT_TEXT_MAX) {
      this.updateRow(languageId, {
        state: "error",
        errorMessage: `Text je delší než ${PRINT_TEXT_MAX} znaků.`,
      });
      return;
    }
    this.updateRow(languageId, { state: "saving", errorMessage: null });
    if (row.serverId !== null) {
      this.api
        .update(row.serverId, {
          serviceId,
          languageId,
          printText: text,
        })
        .subscribe({
          next: () => {
            this.markSaved(languageId, text);
            this.textsResource.reload();
          },
          error: err => this.markError(languageId, err),
        });
    } else {
      this.api
        .create({
          serviceId,
          languageId,
          printText: text,
        })
        .subscribe({
          next: () => {
            this.markSaved(languageId, text);
            this.textsResource.reload();
          },
          error: err => this.markError(languageId, err),
        });
    }
  }

  private markSaved(languageId: string, text: string): void {
    // Update serverText alongside text so subsequent edits don't show spurious "unsaved" flicker before reload.
    this.rows.update(rows =>
      rows.map(r =>
        r.languageId === languageId
          ? {
              ...r,
              text,
              serverText: text,
              state: "saved",
              errorMessage: null,
            }
          : r
      )
    );
  }

  private markError(languageId: string, err: unknown): void {
    let message = "Uložení selhalo.";
    if (isApiError(err) && err.status === 409) {
      message = "Pro tuto službu a jazyk už text existuje. Načtěte znovu.";
    } else if (isApiError(err) && err.status === 404) {
      message = "Záznam již neexistuje. Načtěte znovu.";
    }
    this.updateRow(languageId, { state: "error", errorMessage: message });
  }

  private updateRow(languageId: string, patch: Partial<Row>): void {
    this.rows.update(rows =>
      rows.map(r => (r.languageId === languageId ? { ...r, ...patch } : r))
    );
  }
}
