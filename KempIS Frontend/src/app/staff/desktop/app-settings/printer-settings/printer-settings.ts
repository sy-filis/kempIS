import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  type OnInit,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ButtonModule } from "primeng/button";
import { DividerModule } from "primeng/divider";
import { InputNumberModule } from "primeng/inputnumber";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { PanelModule } from "primeng/panel";
import { SelectModule } from "primeng/select";

import {
  PRINT_COPIES_MAX,
  PRINT_COPIES_MIN,
  PRINT_TASK_IDS,
  PRINT_TASK_LABELS,
  type PrintTaskId,
} from "../../../../core/printing/print-task";
import { PrinterSettingsStore } from "../../../../core/printing/printer-settings.store";

type Status =
  | { kind: "empty" }
  | { kind: "loading" }
  | { kind: "error" }
  | { kind: "connected"; count: number };

@Component({
  selector: "kemp-is-printer-settings",
  standalone: true,
  imports: [
    FormsModule,
    ButtonModule,
    DividerModule,
    InputNumberModule,
    InputTextModule,
    MessageModule,
    PanelModule,
    SelectModule,
  ],
  templateUrl: "./printer-settings.html",
  styleUrl: "./printer-settings.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrinterSettings implements OnInit {
  private readonly store = inject(PrinterSettingsStore);

  protected readonly tasks = PRINT_TASK_IDS;
  protected readonly taskLabels = PRINT_TASK_LABELS;
  protected readonly copiesMin = PRINT_COPIES_MIN;
  protected readonly copiesMax = PRINT_COPIES_MAX;

  protected readonly serverUrl = this.store.serverUrl;
  protected readonly printers = this.store.printers;
  protected readonly loading = this.store.loading;
  protected readonly error = this.store.error;
  protected readonly defaults = this.store.defaults;
  protected readonly copiesDefaults = this.store.copiesDefaults;

  protected readonly urlDraft = signal<string>(this.store.serverUrl());

  protected readonly status = computed<Status>(() => {
    if (this.serverUrl() === "") {
      return { kind: "empty" };
    }
    if (this.loading()) {
      return { kind: "loading" };
    }
    if (this.error() !== null) {
      return { kind: "error" };
    }
    return { kind: "connected", count: this.printers().length };
  });

  protected readonly printerCount = computed<number>(() => {
    const s = this.status();
    return s.kind === "connected" ? s.count : 0;
  });

  protected readonly printerOptions = computed<string[]>(() => [
    ...this.printers(),
  ]);

  ngOnInit(): void {
    if (this.serverUrl() !== "" && this.printers().length === 0) {
      void this.store.refreshPrinters();
    }
  }

  protected commitUrl(): void {
    void this.store.setServerUrl(this.urlDraft());
  }

  protected refresh(): void {
    void this.store.refreshPrinters();
  }

  protected onDefaultChange(task: PrintTaskId, value: string | null): void {
    this.store.setDefaultFor(task, value);
  }

  protected onCopiesChange(task: PrintTaskId, value: number | null): void {
    this.store.setCopiesFor(task, value ?? this.copiesMin);
  }

  protected dismissError(): void {
    this.store.dismissError();
  }
}
