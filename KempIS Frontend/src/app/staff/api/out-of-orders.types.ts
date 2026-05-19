/** An OOO applies to the union of every spot in the listed
 *  `spotGroupIds` plus any individually listed `spotIds`. */
export type OutOfOrder = {
  id: string;
  from: string; // YYYY-MM-DD
  to: string; // YYYY-MM-DD
  reason: string;
  spotGroupIds: readonly string[];
  spotIds: readonly string[];
};

export type OutOfOrderRequest = {
  from: string; // YYYY-MM-DD
  to: string; // YYYY-MM-DD
  reason: string;
  spotGroupIds: readonly string[];
  spotIds: readonly string[];
};
