import { ChangeDetectionStrategy, Component, input } from "@angular/core";

@Component({
  selector: "kemp-is-staff-screen-header",
  templateUrl: "./screen-header.html",
  styleUrl: "./screen-header.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScreenHeader {
  public readonly title = input.required<string>();
  public readonly subtitle = input<string | null>(null);
}
