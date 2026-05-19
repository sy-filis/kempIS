import { ChangeDetectionStrategy, Component } from "@angular/core";

import { EdokladySettings } from "./edoklady-settings/edoklady-settings";
import { PrinterSettings } from "./printer-settings/printer-settings";
import { TabletPairingSettings } from "./tablet-pairing-settings/tablet-pairing-settings";

@Component({
  selector: "kemp-is-app-settings",
  imports: [EdokladySettings, PrinterSettings, TabletPairingSettings],
  template: `
    <div class="kemp-is-app-settings">
      <header class="kemp-is-app-settings__toolbar">
        <div class="kemp-is-app-settings__title">
          <div class="kemp-is-app-settings__title-eyebrow">Nastavení</div>
          <div class="kemp-is-app-settings__title-main">Aplikace</div>
        </div>
      </header>
      <div class="kemp-is-app-settings__content">
        <kemp-is-tablet-pairing-settings />
        <kemp-is-edoklady-settings />
        <kemp-is-printer-settings />
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
      height: 100%;
      overflow-y: auto;
    }

    .kemp-is-app-settings {
      display: flex;
      flex-direction: column;

      &__toolbar {
        display: flex;
        align-items: center;
        gap: 1.5rem;
        padding: 0 1.25rem;
        height: 64px;
        flex-shrink: 0;
        background: var(--p-surface-0, #fff);
        border-bottom: 1px solid var(--p-content-border-color, #e4e4e7);
      }

      &__title {
        display: flex;
        flex-direction: column;
        gap: 2px;
      }

      &__title-eyebrow {
        font-size: 0.6875rem;
        font-weight: 600;
        letter-spacing: 0.08em;
        color: var(--p-text-muted-color, #71717a);
        text-transform: uppercase;
      }

      &__title-main {
        font-size: 1.125rem;
        font-weight: 600;
        color: var(--p-text-color-emphasis, #27272a);
        line-height: 1.1;
      }

      &__content {
        display: flex;
        flex-direction: column;
        gap: 1.5rem;
        padding: 2rem;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppSettingsPage {}
