import { ChangeDetectionStrategy, Component } from "@angular/core";
import { RouterOutlet } from "@angular/router";

@Component({
  selector: "kemp-is-staff-shell",
  imports: [RouterOutlet],
  templateUrl: "./staff-shell.html",
  styleUrl: "./staff-shell.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StaffShell {}
