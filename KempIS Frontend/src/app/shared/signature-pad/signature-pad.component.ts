import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  type ElementRef,
  inject,
  input,
  model,
  viewChild,
} from "@angular/core";

import { ButtonModule } from "primeng/button";
import SignaturePad from "signature_pad";

@Component({
  selector: "kemp-is-signature-pad",
  imports: [ButtonModule],
  templateUrl: "./signature-pad.component.html",
  styleUrl: "./signature-pad.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    "[class.is-empty]": "!value()",
    "[style.--kemp-is-signature-height.px]": "height()",
  },
})
export class SignaturePadComponent {
  // Bare base64 PNG (no "data:image/png;base64," prefix). Empty when blank.
  readonly value = model<string>("");
  readonly height = input<number>(180);

  protected readonly clearLabel = $localize`:@@checkin.signature.clear:Vymazat`;
  protected readonly placeholder = $localize`:@@checkin.signature.placeholder:Podepište se prstem nebo perem`;

  private readonly canvasRef =
    viewChild.required<ElementRef<HTMLCanvasElement>>("canvas");
  private readonly destroyRef = inject(DestroyRef);

  private pad: SignaturePad | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private readonly onEndStroke = (): void => this.commitValue();

  constructor() {
    afterNextRender(() => {
      const canvas = this.canvasRef().nativeElement;
      this.resizeCanvas(canvas);
      this.pad = new SignaturePad(canvas, { minWidth: 0.6, maxWidth: 2.0 });
      this.pad.addEventListener("endStroke", this.onEndStroke);
      this.resizeObserver = new ResizeObserver(() => this.resizeCanvas(canvas));
      this.resizeObserver.observe(canvas);
    });

    this.destroyRef.onDestroy(() => {
      this.pad?.removeEventListener("endStroke", this.onEndStroke);
      this.pad?.off();
      this.pad = null;
      this.resizeObserver?.disconnect();
      this.resizeObserver = null;
    });
  }

  protected onClear(): void {
    this.pad?.clear();
    this.value.set("");
  }

  private commitValue(): void {
    if (!this.pad) {
      return;
    }
    if (this.pad.isEmpty()) {
      this.value.set("");
      return;
    }
    const base64 = this.pad.toDataURL("image/png").split(",")[1] ?? "";
    this.value.set(base64);
  }

  private resizeCanvas(canvas: HTMLCanvasElement): void {
    const dpr = window.devicePixelRatio || 1;
    const rect = canvas.getBoundingClientRect();
    const data = this.pad && !this.pad.isEmpty() ? this.pad.toData() : null;
    canvas.width = Math.max(1, Math.round(rect.width * dpr));
    canvas.height = Math.max(1, Math.round(rect.height * dpr));
    canvas.getContext("2d")?.scale(dpr, dpr);
    if (data && this.pad) {
      this.pad.fromData(data);
    }
  }
}
