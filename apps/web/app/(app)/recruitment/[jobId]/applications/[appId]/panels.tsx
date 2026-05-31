"use client";

import { useActionState } from "react";
import type { Offer, Interview } from "@/lib/api";
import { Alert, Badge, type BadgeProps, Button, Input, Label } from "@/components/ui";
import {
  createOfferAction,
  sendOfferAction,
  respondOfferAction,
  scheduleInterviewAction,
  submitScorecardAction,
  type ActionState,
} from "./actions";

const sectionClass = "rounded-[var(--radius-app)] border border-border bg-surface p-5 shadow-sm";
const selectClass =
  "h-9 rounded-[var(--radius-app)] border border-input bg-surface px-3 text-sm text-foreground shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring";

/// Offer status colours: `sent` reads as in-flight (info), `draft` as neutral, distinct from the
/// terminal accepted/declined states.
function offerTone(status: string): NonNullable<BadgeProps["tone"]> {
  switch (status) {
    case "accepted":
      return "success";
    case "declined":
      return "danger";
    case "sent":
      return "info";
    default:
      return "neutral"; // draft
  }
}

// ============================ Offers ============================

export function OffersPanel({
  applicationId,
  offers,
  canWrite,
}: {
  applicationId: string;
  offers: Offer[];
  canWrite: boolean;
}) {
  return (
    <section className={sectionClass}>
      <h2 className="text-sm font-semibold text-foreground">Offers</h2>

      <ul className="mt-3 flex flex-col gap-2">
        {offers.map((o) => (
          <li
            key={o.id}
            data-testid="offer-row"
            data-offer-id={o.id}
            data-offer-status={o.status}
            className="flex items-center justify-between rounded-[var(--radius-app)] border border-border px-3 py-2 text-sm"
          >
            <span className="text-foreground">
              {o.salary != null ? `${o.currency ?? ""} ${o.salary.toLocaleString()}`.trim() : "—"}
            </span>
            <div className="flex items-center gap-2">
              <Badge tone={offerTone(o.status)}>{o.status}</Badge>
              {canWrite && o.status === "draft" && <SendButton offerId={o.id} />}
              {canWrite && o.status === "sent" && <RespondButtons offerId={o.id} />}
            </div>
          </li>
        ))}
        {offers.length === 0 && <li className="py-3 text-center text-xs text-muted-foreground">No offers yet.</li>}
      </ul>

      {canWrite ? (
        <CreateOfferForm applicationId={applicationId} />
      ) : (
        <p className="mt-3 text-xs text-muted-foreground">
          Read-only — you lack <code>offer.write</code>.
        </p>
      )}
    </section>
  );
}

function CreateOfferForm({ applicationId }: { applicationId: string }) {
  const [state, action, pending] = useActionState<ActionState, FormData>(createOfferAction, {});
  return (
    <form action={action} className="mt-4 flex flex-wrap items-end gap-2 border-t border-border pt-4">
      <input type="hidden" name="applicationId" value={applicationId} />
      <Label>
        Salary
        <Input name="salary" type="number" min="0" step="0.01" placeholder="8500.00" className="w-32" />
      </Label>
      <Label>
        Currency
        <Input name="currency" defaultValue="SGD" maxLength={3} className="w-20" />
      </Label>
      <Button type="submit" disabled={pending}>
        {pending ? "Creating…" : "Create draft offer"}
      </Button>
      {state.error && <Alert tone="danger" className="w-full">{state.error}</Alert>}
    </form>
  );
}

function SendButton({ offerId }: { offerId: string }) {
  const [state, action, pending] = useActionState<ActionState, FormData>(sendOfferAction, {});
  return (
    <form action={action} title={state.error ?? ""}>
      <input type="hidden" name="offerId" value={offerId} />
      <Button type="submit" variant="secondary" size="sm" disabled={pending}>
        {pending ? "…" : "Send"}
      </Button>
    </form>
  );
}

function RespondButtons({ offerId }: { offerId: string }) {
  const [state, action, pending] = useActionState<ActionState, FormData>(respondOfferAction, {});
  return (
    <form action={action} className="flex gap-1" title={state.error ?? ""}>
      <input type="hidden" name="offerId" value={offerId} />
      <Button type="submit" name="decision" value="accepted" variant="success" size="sm" disabled={pending}>
        Accept
      </Button>
      <Button type="submit" name="decision" value="declined" variant="danger" size="sm" disabled={pending}>
        Decline
      </Button>
    </form>
  );
}

// ============================ Interviews / scorecards ============================

export function InterviewsPanel({
  applicationId,
  interviews,
  canWrite,
}: {
  applicationId: string;
  interviews: Interview[];
  canWrite: boolean;
}) {
  return (
    <section className={sectionClass}>
      <h2 className="text-sm font-semibold text-foreground">Interviews &amp; scorecards</h2>

      <ul className="mt-3 flex flex-col gap-3">
        {interviews.map((iv) => (
          <li
            key={iv.id}
            data-testid="interview-row"
            data-interview-id={iv.id}
            data-has-scorecard={iv.rollupScore != null ? "true" : "false"}
            className="rounded-[var(--radius-app)] border border-border p-3"
          >
            <div className="flex items-center justify-between gap-2 text-sm">
              <span className="text-foreground">
                {iv.scheduledAt ? new Date(iv.scheduledAt).toLocaleString() : "Unscheduled"}
              </span>
              {iv.rollupScore != null ? (
                <Badge tone="info">
                  score {iv.rollupScore.toFixed(2)}
                  {iv.recommendation ? ` · ${iv.recommendation}` : ""}
                </Badge>
              ) : (
                <span className="text-xs text-muted-foreground">no scorecard yet</span>
              )}
            </div>
            {canWrite && iv.rollupScore == null && <ScorecardForm interviewId={iv.id} />}
          </li>
        ))}
        {interviews.length === 0 && (
          <li className="py-3 text-center text-xs text-muted-foreground">No interviews yet.</li>
        )}
      </ul>

      {canWrite ? (
        <ScheduleForm applicationId={applicationId} />
      ) : (
        <p className="mt-3 text-xs text-muted-foreground">
          Read-only — you lack <code>interview.write</code>.
        </p>
      )}
    </section>
  );
}

function ScheduleForm({ applicationId }: { applicationId: string }) {
  const [state, action, pending] = useActionState<ActionState, FormData>(scheduleInterviewAction, {});
  return (
    <form action={action} className="mt-4 flex flex-wrap items-end gap-2 border-t border-border pt-4">
      <input type="hidden" name="applicationId" value={applicationId} />
      <Label>
        Scheduled at
        <Input name="scheduledAt" type="datetime-local" />
      </Label>
      <Button type="submit" disabled={pending}>
        {pending ? "…" : "Schedule interview"}
      </Button>
      {state.error && <Alert tone="danger" className="w-full">{state.error}</Alert>}
    </form>
  );
}

function ScorecardForm({ interviewId }: { interviewId: string }) {
  const [state, action, pending] = useActionState<ActionState, FormData>(submitScorecardAction, {});
  const rows = [
    { i: 0, name: "Technical", weight: 2 },
    { i: 1, name: "Communication", weight: 1 },
    { i: 2, name: "Culture", weight: 1 },
  ];
  return (
    <form action={action} className="mt-3 flex flex-col gap-2 border-t border-border pt-3">
      <input type="hidden" name="interviewId" value={interviewId} />
      <p className="text-xs font-medium text-muted-foreground">Scorecard — weighted competencies (score 1–5)</p>
      {rows.map((r) => (
        <div key={r.i} className="flex items-center gap-2">
          <Input name={`name_${r.i}`} defaultValue={r.name} placeholder="Competency" className="flex-1" />
          <Input name={`weight_${r.i}`} type="number" min="0" step="0.5" defaultValue={r.weight} title="weight" className="w-20" />
          <Input name={`score_${r.i}`} type="number" min="1" max="5" placeholder="1-5" title="score" className="w-16" />
        </div>
      ))}
      <div className="flex items-center gap-2">
        <select name="recommendation" defaultValue="" className={`${selectClass} w-40`}>
          <option value="">No recommendation</option>
          <option value="strong_hire">strong_hire</option>
          <option value="hire">hire</option>
          <option value="no_hire">no_hire</option>
        </select>
        <Input name="notes" placeholder="Notes (optional)" className="flex-1" />
      </div>
      <div>
        <Button type="submit" disabled={pending}>
          {pending ? "Submitting…" : "Submit scorecard"}
        </Button>
      </div>
      {state.error && <Alert tone="danger">{state.error}</Alert>}
    </form>
  );
}
