"use client";

import Link from "next/link";
import { useActionState } from "react";
import { ArrowLeft, ArrowRight } from "lucide-react";
import type { Board } from "@/lib/api";
import { Badge, Button } from "@/components/ui";
import { moveApplicationAction, type MoveState } from "../actions";

/// Interactive Kanban: each candidate card shows a move control (to adjacent stages) when the
/// user holds application.move, and links to the application detail (offers + scorecards). Moves go
/// through a server action -> API -> revalidate (§14.3).
export function Kanban({ board, canMove }: { board: Board; canMove: boolean }) {
  const stageKeys = board.columns.map((c) => c.key);

  return (
    <div className="mt-6 flex gap-4 overflow-x-auto pb-4">
      {board.columns.map((col) => (
        // NB: the column name renders first so the column's text content starts with the stage
        // name (the E2E asserts the "Hired" column contains the hired candidate).
        <div
          key={col.key}
          className="flex w-64 shrink-0 flex-col rounded-[var(--radius-app)] border border-border bg-surface-muted"
        >
          <div className="flex items-center justify-between border-b border-border px-3 py-2">
            <span className="text-sm font-medium text-foreground">{col.name}</span>
            <span className="rounded-full bg-surface px-2 py-0.5 text-xs text-muted-foreground">
              {col.cards.length}
            </span>
          </div>
          <div className="flex flex-col gap-2 p-2">
            {col.cards.map((card) => (
              <Card key={card.applicationId} jobId={board.jobId} card={card} stageKeys={stageKeys} canMove={canMove} />
            ))}
            {col.cards.length === 0 && <p className="px-1 py-4 text-center text-xs text-muted-foreground">—</p>}
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
    <div
      className={`rounded-[var(--radius-app)] border border-border bg-surface p-3 shadow-sm transition-opacity ${pending ? "opacity-50" : ""}`}
    >
      <div className="flex items-start justify-between gap-2">
        <p className="text-sm font-medium text-foreground">{card.candidateName}</p>
        {card.matchScore != null && <Badge tone="info">{Math.round(card.matchScore)}%</Badge>}
      </div>

      {canMove && (prev || next) && (
        <div className="mt-2 flex gap-1">
          {prev && (
            <form action={action}>
              <input type="hidden" name="applicationId" value={card.applicationId} />
              <input type="hidden" name="toStage" value={prev} />
              <Button type="submit" variant="secondary" size="sm" disabled={pending}>
                <ArrowLeft /> {prev}
              </Button>
            </form>
          )}
          {next && (
            <form action={action}>
              <input type="hidden" name="applicationId" value={card.applicationId} />
              <input type="hidden" name="toStage" value={next} />
              <Button type="submit" variant="secondary" size="sm" disabled={pending}>
                {next} <ArrowRight />
              </Button>
            </form>
          )}
        </div>
      )}
      {state.error && <p className="mt-1 text-xs text-danger">{state.error}</p>}

      <Link
        href={`/recruitment/${jobId}/applications/${card.applicationId}`}
        className="mt-2 block text-xs text-muted-foreground transition-colors hover:text-foreground hover:underline"
      >
        View offers &amp; scorecards →
      </Link>
    </div>
  );
}
