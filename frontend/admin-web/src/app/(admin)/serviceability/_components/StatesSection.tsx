"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field } from "@/components/ui";
import { describeError } from "@/lib/api";
import { createState, listStates, setStateActive } from "@/lib/serviceability-api";
import type { StateResponse } from "@/lib/serviceability-types";
import { EntityTable } from "./EntityTable";

const stateSchema = z.object({
  name: z.string().min(1, "State name is required").max(200),
  code: z.string().min(1, "State code is required").max(10),
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
    mutationFn: createState,
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
    <Card title="States" description="Top-level geography master entry (SRS 12.9.1).">
      <EntityTable<StateResponse>
        items={statesQuery.data}
        isLoading={statesQuery.isLoading}
        errorMessage={statesQuery.error ? describeError(statesQuery.error) : null}
        emptyMessage="No states yet."
        canWrite={canWrite}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        onToggleActive={(state) => toggleMutation.mutate({ id: state.id, isActive: !state.isActive })}
        columns={[
          { header: "Name", render: (state) => state.name },
          { header: "Code", render: (state) => state.code },
        ]}
      />

      {canWrite ? (
        <form onSubmit={onSubmit} className="mt-4 flex flex-wrap items-end gap-3" noValidate>
          {createMutation.isError ? (
            <div className="w-full">
              <Alert>{describeError(createMutation.error)}</Alert>
            </div>
          ) : null}
          <div className="w-48">
            <Field label="Name" error={form.formState.errors.name?.message} {...form.register("name")} />
          </div>
          <div className="w-32">
            <Field label="Code" error={form.formState.errors.code?.message} {...form.register("code")} />
          </div>
          <Button type="submit" disabled={form.formState.isSubmitting || createMutation.isPending}>
            {createMutation.isPending ? "Adding…" : "Add state"}
          </Button>
        </form>
      ) : null}
    </Card>
  );
}
