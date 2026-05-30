import { login } from "./actions";
import { SEED } from "@/lib/session";

export default function LoginPage() {
  return (
    <main className="mx-auto flex min-h-screen max-w-md flex-col justify-center gap-6 px-6">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900">SahaHR</h1>
        <p className="mt-1 text-sm text-gray-500">Admin · dev sign-in</p>
      </div>

      <form action={login} className="flex flex-col gap-4 rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
        <p className="text-sm text-gray-600">
          Dev-only token mint — permissions are resolved from the database for the chosen user.
          Replaced by Keycloak / OIDC in production.
        </p>
        <label className="flex flex-col gap-1 text-sm font-medium text-gray-700">
          Tenant ID
          <input
            name="tenantId"
            defaultValue={SEED.tenantId}
            className="rounded-md border border-gray-300 px-3 py-2 font-mono text-xs text-gray-900"
          />
        </label>
        <label className="flex flex-col gap-1 text-sm font-medium text-gray-700">
          User ID
          <input
            name="userId"
            defaultValue={SEED.userId}
            className="rounded-md border border-gray-300 px-3 py-2 font-mono text-xs text-gray-900"
          />
        </label>
        <button
          type="submit"
          className="mt-2 rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800"
        >
          Sign in
        </button>
      </form>
    </main>
  );
}
