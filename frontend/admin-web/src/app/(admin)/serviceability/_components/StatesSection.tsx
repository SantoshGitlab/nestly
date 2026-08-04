"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field } from "@/components/ui";
import { FormActions, FormGrid } from "@/components/data-table";
import { EntityTable } from "@/components/entity-table";
import { describeError } from "@/lib/api";
import { createState, listStates, setStateActive } from "@/lib/serviceability-api";
import type { StateResponse } from "@/lib/serviceability-types";

const stateSchema = z.object({
  name: z.string().trim().min(1, "State name is required").max(200),
  code: z
    .string()
    .trim()
    .min(1, "State code is required")
    .max(10)
    .regex(/^[A-Za-z]+$/, 'Letters only, e.g. "MH"'),
});
type StateFormValues = z.infer<typeof stateSchema>;

/** Geography master: states (SRS 12.9.1), the top of the hierarchy. */
export function StatesSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const statesQuery = useQuery({ queryKey: ["states"], queryFn: listStates });

  const form = useForm<StateFormValues>({
    resolver: zodResolver(stateSchema),
    defaultValues: { name: "", code: "" },
  });

  const createMutation = useMutation({
    // Normalised rather than passed through raw: a trailing space produces a
    // second "MH " that never matches the real one anywhere downstream.
    mutationFn: (values: StateFormValues) =>
      createState({ name: values.name.trim(), code: values.code.trim().toUpperCase() }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["states"] });
      form.reset();
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setStateActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["states"] }),
  });

  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  return (
    <div className="flex flex-col gap-4">
      <EntityTable<StateResponse>
        title="States"
        description="Top-level geography master entry (SRS 12.9.1)."
        items={statesQuery.data}
        isLoading={statesQuery.isPending}
        isFetching={statesQuery.isFetching}
        error={statesQuery.error}
        onRetry={() => statesQuery.refetch()}
        emptyMessage="No states yet"
        canWrite={canWrite}
        entityLabel="state"
        labelOf={(state) => state.name}
        minWidth="620px"
        skeletonRows={4}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        toggleError={toggleMutation.error}
        onToggleActive={(state) => toggleMutation.mutate({ id: state.id, isActive: !state.isActive })}
        columns={[
          {
            header: "Name",
            sortValue: (state) => state.name,
            render: (state) => <span className="font-medium text-fg">{state.name}</span>,
          },
          {
            header: "Code",
            sortValue: (state) => state.code,
            render: (state) => <span className="nums">{state.code}</span>,
          },
        ]}
      />

      {canWrite ? (
        <Card title="Add a state" description="Everything below a state inherits its serviceability.">
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}
            <FormGrid>
              <Field label="Name" required error={form.formState.errors.name?.message} {...form.register("name")} />
              <Field
                label="Code"
                required
                hint="Saved in upper case, e.g. MH."
                error={form.formState.errors.code?.message}
                {...form.register("code")}
              />
            </FormGrid>
            <FormActions align="start">
              <Button type="submit" loading={createMutation.isPending}>
                Add state
              </Button>
            </FormActions>
          </form>
        </Card>
      ) : null}
    </div>
  );
}
