import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { SelectButtonModule } from "primeng/selectbutton";
import { TagModule } from "primeng/tag";
import { ToastModule } from "primeng/toast";

import { MaintenanceIssueDialog } from "./maintenance-issue-dialog";
import { ApiClient } from "../../../core/api/api-client";
import { SpotsStore } from "../../../core/spots/spots.store";
import type { MaintenanceIssue } from "../../api/maintenance.types";

type IssueState = "open" | "inprogress" | "resolved";

type IssueStateConfig = {
  readonly label: string;
  readonly dot: string;
  readonly fg: string;
  readonly bg: string;
};

type IssueRow = {
  readonly id: string;
  readonly shortId: string;
  readonly title: string;
  readonly description: string | null;
  readonly state: IssueState;
  readonly stateConfig: IssueStateConfig;
  readonly placeName: string | null;
  readonly created: string;
};

type TabId = "open" | "resolved";

type TabOption = {
  readonly id: TabId;
  readonly label: string;
  readonly count: number;
};

const STATE_CONFIG: Record<IssueState, IssueStateConfig> = {
  open: { label: "Nahlášeno", dot: "#dc2626", fg: "#991b1b", bg: "#fee2e2" },
  inprogress: {
    label: "Řeší se",
    dot: "#3b82f6",
    fg: "#1e40af",
    bg: "#dbeafe",
  },
  resolved: { label: "Vyřešeno", dot: "#10b981", fg: "#065f46", bg: "#d1fae5" },
};

@Component({
  selector: "kemp-is-ops-maintenance-section",
  imports: [
    FormsModule,
    ButtonModule,
    SelectButtonModule,
    TagModule,
    ToastModule,
    MaintenanceIssueDialog,
  ],
  providers: [MessageService],
  templateUrl: "./maintenance-section.html",
  styleUrl: "./maintenance-section.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MaintenanceSection {
  private readonly apiClient = inject(ApiClient);
  private readonly spotsStore = inject(SpotsStore);
  private readonly messages = inject(MessageService);

  protected readonly tab = signal<TabId>("open");

  protected readonly issues = httpResource<readonly MaintenanceIssue[]>(() =>
    this.apiClient.url("/maintenance-issues?Status=All")
  );

  protected readonly dialogVisible = signal<boolean>(false);
  protected readonly editingIssue = signal<MaintenanceIssue | null>(null);

  private readonly issueRows = computed<readonly IssueRow[]>(() => {
    const list = this.issues.hasValue() ? this.issues.value() : [];
    return list.map(i => this.toRow(i));
  });

  protected readonly openCount = computed(
    () => this.issueRows().filter(r => r.state !== "resolved").length
  );

  protected readonly resolvedCount = computed(
    () => this.issueRows().filter(r => r.state === "resolved").length
  );

  protected readonly tabOptions = computed<TabOption[]>(() => [
    { id: "open", label: "Otevřené", count: this.openCount() },
    { id: "resolved", label: "Vyřešené", count: this.resolvedCount() },
  ]);

  protected readonly visibleRows = computed<readonly IssueRow[]>(() => {
    const target = this.tab();
    const list = this.issues.hasValue() ? this.issues.value() : [];
    return list
      .filter(i =>
        target === "open" ? i.resolvedAtUtc === null : i.resolvedAtUtc !== null
      )
      .slice()
      .sort((a, b) => b.issuedAtUtc - a.issuedAtUtc)
      .map(i => this.toRow(i));
  });

  protected onTabChange(value: TabId): void {
    this.tab.set(value);
  }

  protected onReport(): void {
    this.editingIssue.set(null);
    this.dialogVisible.set(true);
  }

  protected onRowClick(rowId: string): void {
    const list = this.issues.hasValue() ? this.issues.value() : [];
    const issue = list.find(i => i.id === rowId);
    if (!issue) {
      return;
    }
    this.editingIssue.set(issue);
    this.dialogVisible.set(true);
  }

  protected onSaved(): void {
    const wasEditing = this.editingIssue() !== null;
    this.dialogVisible.set(false);
    this.editingIssue.set(null);
    this.messages.add({
      severity: "success",
      summary: wasEditing ? "Závada uložena" : "Závada nahlášena",
    });
    this.issues.reload();
  }

  protected onDeleted(): void {
    this.dialogVisible.set(false);
    this.editingIssue.set(null);
    this.messages.add({
      severity: "success",
      summary: "Závada smazána",
    });
    this.issues.reload();
  }

  private toRow(issue: MaintenanceIssue): IssueRow {
    const state: IssueState =
      issue.resolvedAtUtc !== null
        ? "resolved"
        : issue.solverUserId !== null
          ? "inprogress"
          : "open";
    return {
      id: issue.id,
      shortId: this.shortId(issue.id),
      title: issue.problemDescription,
      description: issue.note,
      state,
      stateConfig: STATE_CONFIG[state],
      placeName: issue.spotId ? this.spotsStore.nameOf(issue.spotId) : null,
      created: this.formatCreated(issue.issuedAtUtc),
    };
  }

  private shortId(id: string): string {
    const tail = id.replace(/-/g, "").slice(-4).toUpperCase();
    return `M-${tail}`;
  }

  private formatCreated(epochMs: number): string {
    const d = new Date(epochMs);
    if (Number.isNaN(d.getTime())) {
      return "—";
    }
    const dd = d.getDate();
    const mm = d.getMonth() + 1;
    const yyyy = d.getFullYear();
    const hh = String(d.getHours()).padStart(2, "0");
    const mi = String(d.getMinutes()).padStart(2, "0");
    return `${dd}. ${mm}. ${yyyy} ${hh}:${mi}`;
  }
}
