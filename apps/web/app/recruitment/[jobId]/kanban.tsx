"use client";

import Link from "next/link";
import { useActionState } from "react";
import type { Board } from "@/lib/api";
import { moveApplicationAction, type MoveState } from "../actions";

/// Interactive Kanban: each candidate card shows a move control (to adjacent stages) when the
/// user holds application.move, and links to the application detail (offers + scorecards). Moves go
/// through a server action -> API -> revalidate (§14.3).
export function Kanban({ board, canMove }: { board: Board; canMove: boolean }) {
  const stageKeys = board.columns.map((c) => c.key);

  return (
    <div className="mt-6 flex gap-4 overflow-x-auto pb-4">
      {board.columns.map((col) => (
        <div key={col.key} className="flex w-64 shrink-0 flex-col rounded-xl border border-gray-200 bg-gray-50">
          <div className="flex items-center justify-between border-b border-gray-200 px-3 py-2">
            <span className="text-sm font-medium text-gray-700">{col.name}</span>
            <span className="rounded-full bg-white px-2 py-0.5 text-xs text-gray-500">{col.cards.length}</span>
          </div>
          <div className="flex flex-col gap-2 p-2">
            {col.cards.map((card) => (
              <Card
                key={card.applicationId}
                jobId={board.jobId}
                card={card}
                stageKeys={stageKeys}
                canMove={canMove}
              />
            ))}
            {col.cards.length === 0 && <p className="px-1 py-4 text-center text-xs text-gray-300">—</p>}
          </div>
        </div>
      ))}
    </div>
  );
}

function Card({
  jobId,
  card,
  stageKeys,
  canMove,
}: {
  jobId: string;
  card: { applicationId: string; candidateName: string; matchScore: number | null; stage: string };
  stageKeys: string[];
  canMove: boolean;
}) {
  const [state, action, pending] = useActionState<MoveState, FormData>(moveApplicationAction, {});

  const idx = stageKeys.indexOf(card.stage);
  const prev = idx > 0 ? stageKeys[idx - 1] : null;
  const next = idx >= 0 && idx < stageKeys.length - 1 ? stageKeys[idx + 1] : null;

  return (
    <div className={`rounded-lg border border-gray-200 bg-white p-3 shadow-sm ${pending ? "opacity-50" : ""}`}>
      <div className="flex items-start justify-between">
        <p className="text-sm font-medium text-gray-900">{card.candidateName}</p>
        {card.matchScore != null && (
          <span className="rounded-full bg-blue-50 px-1.5 py-0.5 text-xs font-medium text-blue-700">
            {Math.round(card.matchScore)}%
          </span>
        )}
      </div>

      {canMove && (prev || next) && (
        <div className="mt-2 flex gap-1">
          {prev && (
            <form action={action}>
              <input type="hidden" name="applicationId" value={card.applicationId} />
              <input type="hidden" name="toStage" value={prev} />
              <button disabled={pending} className="rounded border border-gray-200 px-2 py-1 text-xs text-gray-600 hover:bg-gray-50 disabled:opacity-50">
                ← {prev}
              </button>
            </form>
          )}
          {next && (
            <form action={action}>
              <input type="hidden" name="applicationId" value={card.applicationId} />
              <input type="hidden" name="toStage" value={next} />
              <button disabled={pending} className="rounded border border-gray-200 px-2 py-1 text-xs text-gray-600 hover:bg-gray-50 disabled:opacity-50">
                {next} →
              </button>
            </form>
          )}
        </div>
      )}
      {state.error && <p className="mt-1 text-xs text-red-600">{state.error}</p>}

      <Link
        href={`/recruitment/${jobId}/applications/${card.applicationId}`}
        className="mt-2 block text-xs text-gray-500 hover:text-gray-800 hover:underline"
      >
        View offers &amp; scorecards →
      </Link>
    </div>
  );
}
