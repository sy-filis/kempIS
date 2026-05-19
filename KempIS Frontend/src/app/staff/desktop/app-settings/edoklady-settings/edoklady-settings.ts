import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from "@angular/core";

import { ButtonModule } from "primeng/button";
import { MessageModule } from "primeng/message";
import { PanelModule } from "primeng/panel";
import { TextareaModule } from "primeng/textarea";

import { EdokladyCounterStore } from "../../../../core/edoklady/edoklady-counter.store";

@Component({
  selector: "kemp-is-edoklady-settings",
  standalone: true,
  imports: [ButtonModule, MessageModule, PanelModule, TextareaModule],
  templateUrl: "./edoklady-settings.html",
  styleUrl: "./edoklady-settings.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EdokladySettings {
  private readonly store = inject(EdokladyCounterStore);

  protected readonly counter = this.store.counter;
  protected readonly ensuring = this.store.ensuring;
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly validToLabel = computed<string>(() => {
    const c = this.counter();
    if (!c) {
      return "";
    }
    const date = new Date(c.qrCode.validTo);
    if (Number.isNaN(date.getTime())) {
      return c.qrCode.validTo;
    }
    return date.toLocaleString("cs-CZ", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  });

  protected dismissError(): void {
    this.errorMessage.set(null);
  }

  protected refresh(): void {
    this.errorMessage.set(null);
    this.store.refreshCounter().catch(() => {
      this.errorMessage.set("Načtení virtuální přepážky se nezdařilo.");
    });
  }

  protected resetAndRecreate(): void {
    this.errorMessage.set(null);
    this.store.reset();
    this.store.ensureCounter().catch(() => {
      this.errorMessage.set("Vytvoření nové virtuální přepážky se nezdařilo.");
    });
  }
}
