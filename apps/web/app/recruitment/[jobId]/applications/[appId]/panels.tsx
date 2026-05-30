"use client";

import { useActionState } from "react";
import type { Offer, Interview } from "@/lib/api";
import {
  createOfferAction,
  sendOfferAction,
  respondOfferAction,
  scheduleInterviewAction,
  submitScorecardAction,
  type ActionState,
} from "./actions";

const input = "rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900";
const btn = "rounded-md bg-gray-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50";
const btnGhost = "rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-50";

function OfferStatusBadge({ status }: { status: string }) {
  const tone =
    status === "accepted" ? "bg-green-50 text-green-700"
    : status === "declined" ? "bg-red-50 text-red-600"
    : status === "sent" ? "bg-blue-50 text-blue-700"
    : "bg-gray-100 text-gray-600";
  return <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${tone}`}>{status}</span>;
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
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <h2 className="text-sm font-semibold text-gray-700">Offers</h2>

      <ul className="mt-3 flex flex-col gap-2">
        {offers.map((o) => (
          <li key={o.id} className="flex items-center justify-between rounded-lg border border-gray-100 px-3 py-2 text-sm">
            <span className="text-gray-800">
              {o.salary != null ? `${o.currency ?? ""} ${o.salary.toLocaleString()}`.trim() : "—"}
            </span>
            <div className="flex items-center gap-2">
              <OfferStatusBadge status={o.status} />
              {canWrite && o.status === "draft" && <SendButton offerId={o.id} />}
              {canWrite && o.status === "sent" && <RespondButtons offerId={o.id} />}
            </div>
          </li>
        ))}
        {offers.length === 0 && <li className="py-3 text-center text-xs text-gray-400">No offers yet.</li>}
      </ul>

      {canWrite ? <CreateOfferForm applicationId={applicationId} /> : (
        <p className="mt-3 text-xs text-gray-400">Read-only — you lack <code>offer.write</code>.</p>
      )}
    </section>
  );
}

function CreateOfferForm({ applicationId }: { applicationId: string }) {
  const [state, action, pending] = useActionState<ActionState, FormData>(createOfferAction, {});
  return (
    <form action={action} className="mt-4 flex flex-wrap items-end gap-2 border-t border-gray-100 pt-4">
      <input type="hidden" name="applicationId" value={applicationId} />
      <label className="flex flex-col gap-1 text-xs text-gray-600">
        Salary
        <input name="salary" type="number" min="0" step="0.01" placeholder="8500.00" className={`${input} w-32`} />
      </label>
      <label className="flex flex-col gap-1 text-xs text-gray-600">
        Currency
        <input name="currency" defaultValue="SGD" maxLength={3} className={`${input} w-20`} />
      </label>
      <button type="submit" disabled={pending} className={btn}>{pending ? "Creating…" : "Create draft offer"}</button>
      {state.error && <p className="w-full text-xs text-red-600">{state.error}</p>}
    </form>
  );
}

function SendButton({ offerId }: { offerId: string }) {
  const [state, action, pending] = useActionState<ActionState, FormData>(sendOfferAction, {});
  return (
    <form action={action} title={state.error ?? ""}>
      <input type="hidden" name="offerId" value={offerId} />
      <button type="submit" disabled={pending} className={btnGhost}>{pending ? "…" : "Send"}</button>
    </form>
  );
}

function RespondButtons({ offerId }: { offerId: string }) {
  const [state, action, pending] = useActionState<ActionState, FormData>(respondOfferAction, {});
  return (
    <form action={action} className="flex gap-1" title={state.error ?? ""}>
      <input type="hidden" name="offerId" value={offerId} />
      <button type="submit" name="decision" value="accepted" disabled={pending}
        className="rounded-md bg-green-600 px-2 py-1 text-xs font-medium text-white hover:bg-green-700 disabled:opacity-50">Accept</button>
      <button type="submit" name="decision" value="declined" disabled={pending}
        className="rounded-md border border-red-200 px-2 py-1 text-xs font-medium text-red-600 hover:bg-red-50 disabled:opacity-50">Decline</button>
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
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <h2 className="text-sm font-semibold text-gray-700">Interviews &amp; scorecards</h2>

      <ul className="mt-3 flex flex-col gap-3">
        {interviews.map((iv) => (
          <li key={iv.id} className="rounded-lg border border-gray-100 p-3">
            <div className="flex items-center justify-between text-sm">
              <span className="text-gray-700">
                {iv.scheduledAt ? new Date(iv.scheduledAt).toLocaleString() : "Unscheduled"}
              </span>
              {iv.rollupScore != null ? (
                <span className="rounded-full bg-blue-50 px-2 py-0.5 text-xs font-medium text-blue-700">
                  score {iv.rollupScore.toFixed(2)}{iv.recommendation ? ` · ${iv.recommendation}` : ""}
                </span>
              ) : (
                <span className="text-xs text-gray-400">no scorecard yet</span>
              )}
            </div>
            {canWrite && iv.rollupScore == null && <ScorecardForm interviewId={iv.id} />}
          </li>
        ))}
        {interviews.length === 0 && <li className="py-3 text-center text-xs text-gray-400">No interviews yet.</li>}
      </ul>

      {canWrite ? <ScheduleForm applicationId={applicationId} /> : (
        <p className="mt-3 text-xs text-gray-400">Read-only — you lack <code>interview.write</code>.</p>
      )}
    </section>
  );
}

function ScheduleForm({ applicationId }: { applicationId: string }) {
  const [state, action, pending] = useActionState<ActionState, FormData>(scheduleInterviewAction, {});
  return (
    <form action={action} className="mt-4 flex flex-wrap items-end gap-2 border-t border-gray-100 pt-4">
      <input type="hidden" name="applicationId" value={applicationId} />
      <label className="flex flex-col gap-1 text-xs text-gray-600">
        Scheduled at
        <input name="scheduledAt" type="datetime-local" className={input} />
      </label>
      <button type="submit" disabled={pending} className={btn}>{pending ? "…" : "Schedule interview"}</button>
      {state.error && <p className="w-full text-xs text-red-600">{state.error}</p>}
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
    <form action={action} className="mt-3 flex flex-col gap-2 border-t border-gray-100 pt-3">
      <input type="hidden" name="interviewId" value={interviewId} />
      <p className="text-xs font-medium text-gray-500">Scorecard — weighted competencies (score 1–5)</p>
      {rows.map((r) => (
        <div key={r.i} className="flex items-center gap-2">
          <input name={`name_${r.i}`} defaultValue={r.name} className={`${input} flex-1`} placeholder="Competency" />
          <input name={`weight_${r.i}`} type="number" min="0" step="0.5" defaultValue={r.weight} className={`${input} w-20`} title="weight" />
          <input name={`score_${r.i}`} type="number" min="1" max="5" className={`${input} w-16`} placeholder="1-5" title="score" />
        </div>
      ))}
      <div className="flex items-center gap-2">
        <select name="recommendation" className={`${input} w-40`} defaultValue="">
          <option value="">No recommendation</option>
          <option value="strong_hire">strong_hire</option>
          <option value="hire">hire</option>
          <option value="no_hire">no_hire</option>
        </select>
        <input name="notes" placeholder="Notes (optional)" className={`${input} flex-1`} />
      </div>
      <div>
        <button type="submit" disabled={pending} className={btn}>{pending ? "Submitting…" : "Submit scorecard"}</button>
      </div>
      {state.error && <p className="text-xs text-red-600">{state.error}</p>}
    </form>
  );
}
