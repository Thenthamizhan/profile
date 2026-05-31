import Link from "next/link";
import { redirect } from "next/navigation";
import { ChevronLeft } from "lucide-react";
import { getToken, hasPerm } from "@/lib/session";
import { getBoard, type Board } from "@/lib/api";
import { Alert } from "@/components/ui";
import { Kanban } from "./kanban";

export const dynamic = "force-dynamic";

export default async function BoardPage({ params }: { params: Promise<{ jobId: string }> }) {
  const token = await getToken();
  if (!token) redirect("/login");

  if (!hasPerm(token, "application.read")) {
    return (
      <main className="mx-auto max-w-5xl px-6 py-10">
        <p className="text-sm text-muted-foreground">
          You lack <code>application.read</code>.
        </p>
      </main>
    );
  }

  const { jobId } = await params;
  const canMove = hasPerm(token, "application.move");

  let board: Board | null = null;
  let error: string | null = null;
  try {
    board = await getBoard(jobId);
  } catch (e) {
    error = e instanceof Error ? e.message : "Failed to load board.";
  }

  return (
    <main className="mx-auto max-w-6xl px-6 py-8">
      <Link
        href="/recruitment"
        className="inline-flex items-center gap-1 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ChevronLeft className="size-4" aria-hidden /> All positions
      </Link>

      {error && (
        <Alert tone="danger" className="mt-4">
          {error}
        </Alert>
      )}
      {!board && !error && <p className="mt-6 text-sm text-muted-foreground">Position not found.</p>}

      {board && (
        <>
          <h1 className="mt-3 text-2xl font-semibold tracking-tight text-foreground">{board.jobTitle}</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            {board.columns.reduce((n, c) => n + c.cards.length, 0)} candidate(s) in pipeline
            {canMove ? " · use the arrows on a card to move stages" : " · read-only"}
          </p>
          <Kanban board={board} canMove={canMove} />
        </>
      )}
    </main>
  );
}
