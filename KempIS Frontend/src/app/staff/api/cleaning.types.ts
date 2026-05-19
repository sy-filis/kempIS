/** `completedAtUtc` is a unix timestamp (`null` while pending, non-null
 *  once the spot is marked clean). */
export type CleanInfo = {
  id: string;
  spotId: string;
  completedAtUtc: number | null;
  responsibleUserId: string | null;
  note: string | null;
};

export type CleaningPlanDetail = {
  id: string;
  date: string;
  updatedAtUtc: string;
  updatedByUserId: string;
  cleanInfos: readonly CleanInfo[];
};

export type AddCleanInfoRequest = {
  spotId: string;
};

export type UpdateCleanInfoRequest = {
  note: string | null;
};

export type MarkCleanedRequest = {
  note: string | null;
};
