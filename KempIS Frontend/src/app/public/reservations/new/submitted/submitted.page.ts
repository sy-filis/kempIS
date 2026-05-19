import { ChangeDetectionStrategy, Component } from "@angular/core";
import { RouterLink } from "@angular/router";

@Component({
  selector: "kemp-is-submitted",
  imports: [RouterLink],
  templateUrl: "./submitted.page.html",
  styleUrl: "./submitted.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubmittedPage {}
