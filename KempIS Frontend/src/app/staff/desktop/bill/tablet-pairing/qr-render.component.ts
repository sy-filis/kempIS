import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  resource,
} from "@angular/core";
import { DomSanitizer, type SafeHtml } from "@angular/platform-browser";

import QRCode from "qrcode";

@Component({
  selector: "kemp-is-qr-render",
  standalone: true,
  template: `
    @if (trustedSvg(); as markup) {
      <div
        class="kemp-is-qr-render"
        [innerHTML]="markup"
        [attr.aria-label]="ariaLabel()"
        role="img"
      ></div>
    }
  `,
  styleUrl: "./qr-render.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QrRenderComponent {
  private readonly sanitizer = inject(DomSanitizer);

  readonly data = input.required<string>();
  readonly size = input<number>(280);
  readonly ariaLabel = input<string>("QR code");

  protected readonly svg = resource({
    params: () => ({ data: this.data(), size: this.size() }),
    loader: async ({ params }): Promise<string> =>
      QRCode.toString(params.data, {
        type: "svg",
        margin: 1,
        width: params.size,
        errorCorrectionLevel: "M",
      }),
  });

  protected readonly trustedSvg = computed<SafeHtml | null>(() => {
    const markup = this.svg.value();
    return markup ? this.sanitizer.bypassSecurityTrustHtml(markup) : null;
  });
}
