import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { Select } from "primeng/select";

import { type AppLocale, LocaleService } from "../locale.service";

type LocaleOption = { value: AppLocale; label: string };

@Component({
  selector: "kemp-is-locale-switcher",
  imports: [Select, FormsModule],
  templateUrl: "./locale-switcher.html",
  styleUrl: "./locale-switcher.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LocaleSwitcher {
  private readonly locale = inject(LocaleService);

  protected readonly options: LocaleOption[] = [
    { value: "cs", label: "Čeština" },
    { value: "en", label: "English" },
  ];

  protected readonly current = signal<AppLocale>(this.locale.currentLocale);

  protected onChange(value: AppLocale): void {
    this.current.set(value);
    this.locale.switchTo(value);
  }
}
