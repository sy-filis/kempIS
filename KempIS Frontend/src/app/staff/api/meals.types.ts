export type MealAmountDto = {
  /** Pickup time as "HH:MM" or "HH:MM:SS"; null when not yet scheduled. */
  at: string | null;
  normal: number;
  glutenFree: number;
  lactoseFree: number;
  vegetarian: number;
  glutenFreeLactoseFree: number;
  glutenFreeVegetarian: number;
  lactoseFreeVegetarian: number;
  glutenFreeLactoseFreeVegetarian: number;
};

export type MealResponse = {
  reservationId: string;
  date: string;
  breakfast: MealAmountDto;
  lunch: MealAmountDto;
  lunchPackage: MealAmountDto;
  dinner: MealAmountDto;
};

export type ReplaceMealRequest = {
  date: string;
  breakfast: MealAmountDto;
  lunch: MealAmountDto;
  lunchPackage: MealAmountDto;
  dinner: MealAmountDto;
};

export type MealTotalsAmountDto = {
  normal: number;
  glutenFree: number;
  lactoseFree: number;
  vegetarian: number;
  glutenFreeLactoseFree: number;
  glutenFreeVegetarian: number;
  lactoseFreeVegetarian: number;
  glutenFreeLactoseFreeVegetarian: number;
};

export type MealTotalsResponse = {
  date: string;
  breakfast: MealTotalsAmountDto;
  lunch: MealTotalsAmountDto;
  lunchPackage: MealTotalsAmountDto;
  dinner: MealTotalsAmountDto;
};
