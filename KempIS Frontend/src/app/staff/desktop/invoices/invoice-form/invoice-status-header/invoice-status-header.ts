import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from "@angular/core";

import { ButtonModule } from "primeng/button";
import { TagModule } from "primeng/tag";

import { AuthService } from "../../../../../core/auth/auth.service";
import { Roles } from "../../../../../core/auth/roles";
import {
  type GetInvoiceResponse,
  InvoiceStatus,
} from "../../../../api/invoices.types";
import {
  formatDue,
  formatIssued,
  formatPaid,
  statusIcon,
  statusLabel,
  statusSeverity,
} from "../../invoices-data";
import { MarkCreatedDialog } from "../mark-created-dialog/mark-created-dialog";
import { MarkPaidDialog } from "../mark-paid-dialog/mark-paid-dialog";

@Component({
  selector: "kemp-is-invoice-status-header",
  standalone: true,
  imports: [ButtonModule, TagModule, MarkCreatedDialog, MarkPaidDialog],
  templateUrl: "./invoice-status-header.html",
  styleUrl: "./invoice-status-header.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoiceStatusHeader {
  private readonly auth = inject(AuthService);

  readonly invoice = input.required<GetInvoiceResponse>();
  readonly transitioned = output<void>();

  protected readonly markCreatedVisible = signal<boolean>(false);
  protected readonly markPaidVisible = signal<boolean>(false);

  protected readonly InvoiceStatus = InvoiceStatus;

  protected readonly statusLabel = statusLabel;
  protected readonly statusSeverity = statusSeverity;
  protected readonly statusIcon = statusIcon;
  protected readonly formatIssued = formatIssued;
  protected readonly formatDue = formatDue;
  protected readonly formatPaid = formatPaid;

  /** Only Accountant can transition; backend allows Accountant+Manager on mark-paid but we restrict to Accountant. */
  protected readonly canTransition = computed<boolean>(() =>
    (this.auth.currentUser()?.roles ?? []).includes(Roles.Accountant)
  );

  protected readonly showMarkCreatedButton = computed<boolean>(
    () => this.canTransition() && this.invoice().status === InvoiceStatus.Draft
  );

  protected readonly showMarkPaidButton = computed<boolean>(
    () =>
      this.canTransition() && this.invoice().status === InvoiceStatus.Created
  );

  protected openMarkCreated(): void {
    this.markCreatedVisible.set(true);
  }

  protected openMarkPaid(): void {
    this.markPaidVisible.set(true);
  }

  protected onTransitionCompleted(): void {
    this.transitioned.emit();
  }
}
