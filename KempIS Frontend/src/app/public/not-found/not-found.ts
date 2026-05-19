import { ChangeDetectionStrategy, Component } from "@angular/core";
import { RouterLink } from "@angular/router";

@Component({
  selector: "kemp-is-not-found",
  imports: [RouterLink],
  templateUrl: "./not-found.html",
  styleUrl: "./not-found.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFoundPage {}
