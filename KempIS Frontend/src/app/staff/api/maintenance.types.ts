export type MaintenanceIssue = {
  id: string;
  spotId: string | null;
  issuedAtUtc: number;
  problemDescription: string;
  solverUserId: string | null;
  resolvedAtUtc: number | null;
  note: string | null;
};

export type CreateMaintenanceIssueRequest = {
  spotId: string | null;
  problemDescription: string;
  note: string | null;
};

export type UpdateMaintenanceIssueRequest = {
  problemDescription: string;
  note: string | null;
};
