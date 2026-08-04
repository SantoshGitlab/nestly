/**
 * Lookup-option builders shared by the serviceability sections.
 *
 * Every "add" form here picks a parent row out of a `<Select>` fed by a
 * geography list endpoint, and those endpoints return suspended rows too.
 * Rendering them as plain names let an admin attach a new zone to a suspended
 * city — or map a service onto a suspended pincode — with nothing on screen
 * saying so, and the result is invisible to customers for a reason nobody can
 * see afterwards. Suspended rows stay selectable (an admin may legitimately be
 * staging geography before activating it) but they are labelled.
 */

export interface LookupOption {
  value: string;
  label: string;
}

export function toLookupOptions<T extends { id: string; isActive: boolean }>(
  items: readonly T[] | undefined,
  labelOf: (item: T) => string,
): LookupOption[] {
  return (items ?? []).map((item) => ({
    value: item.id,
    label: item.isActive ? labelOf(item) : `${labelOf(item)} (suspended)`,
  }));
}
