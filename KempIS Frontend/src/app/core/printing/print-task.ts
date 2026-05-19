/** New tasks are added here first; the settings UI picks them up
 *  automatically by iterating PRINT_TASK_IDS. */
export type PrintTaskId = "bill" | "tent-sticker" | "financial-closing";

export const PRINT_TASK_IDS = [
  "bill",
  "tent-sticker",
  "financial-closing",
] as const;

export const PRINT_TASK_LABELS: Record<PrintTaskId, string> = {
  "bill": "Účet",
  "tent-sticker": "Nálepka na stan",
  "financial-closing": "Účetní závěrka",
};

export const PRINT_TASK_DEFAULT_COPIES: Record<PrintTaskId, number> = {
  "bill": 1,
  "tent-sticker": 1,
  "financial-closing": 1,
};

export const PRINT_COPIES_MIN = 1;
export const PRINT_COPIES_MAX = 99;
