import { ChangeDetectionStrategy, Component } from "@angular/core";

@Component({
  selector: "kemp-is-tablet-waiting",
  standalone: true,
  imports: [],
  templateUrl: "./waiting.page.html",
  styleUrl: "./waiting.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WaitingPage {}
