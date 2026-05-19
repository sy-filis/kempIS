import { ChangeDetectionStrategy, Component } from "@angular/core";
import { RouterOutlet } from "@angular/router";

@Component({
  selector: "kemp-is-reception-tablet-shell",
  standalone: true,
  imports: [RouterOutlet],
  templateUrl: "./reception-tablet-shell.component.html",
  styleUrl: "./reception-tablet-shell.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReceptionTabletShellComponent {}
