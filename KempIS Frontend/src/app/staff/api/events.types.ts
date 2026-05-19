// Domain term: "akce" (event covering one or more spot groups). Aliased
// away from the global DOM `Event` so importers don't accidentally
// shadow it.
export type CalendarEvent = {
  id: string;
  name: string;
  description: string | null;
  startsAt: string;
  endsAt: string;
  spotGroupIds: readonly string[];
};

export type EventRequest = {
  name: string;
  description: string | null;
  startsAt: string;
  endsAt: string;
  spotGroupIds: readonly string[];
};
