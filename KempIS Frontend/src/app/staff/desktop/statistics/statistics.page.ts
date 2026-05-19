import { ChangeDetectionStrategy, Component, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";

import { DatePickerModule } from "primeng/datepicker";
import { PanelModule } from "primeng/panel";

import { StatsGuestsByCountryPanel } from "./guests-by-country/guests-by-country";
import { StatsOccupancyPanel } from "./occupancy/occupancy";
import { StatsRevenueByPaymentMethodPanel } from "./revenue-by-payment-method/revenue-by-payment-method";
import { StatsServicesPanel } from "./services/services";

function startOfMonth(): Date {
  const d = new Date();
  return new Date(d.getFullYear(), d.getMonth(), 1);
}

function endOfMonth(): Date {
  const d = new Date();
  return new Date(d.getFullYear(), d.getMonth() + 1, 0);
}

@Component({
  selector: "kemp-is-statistics",
  imports: [
    FormsModule,
    DatePickerModule,
    PanelModule,
    StatsGuestsByCountryPanel,
    StatsServicesPanel,
    StatsOccupancyPanel,
    StatsRevenueByPaymentMethodPanel,
  ],
  templateUrl: "./statistics.page.html",
  styleUrl: "./statistics.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatisticsPage {
  protected readonly range = signal<Date[]>([startOfMonth(), endOfMonth()]);
}
