import { login } from "./actions";
import { SEED } from "@/lib/session";
import { Button, Card, CardContent, Input, Label } from "@/components/ui";

export default function LoginPage() {
  return (
    <main className="mx-auto flex min-h-screen max-w-md flex-col justify-center gap-6 px-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">SahaHR</h1>
        <p className="mt-1 text-sm text-muted-foreground">Admin · dev sign-in</p>
      </div>

      <Card>
        <CardContent className="pt-6">
          <form action={login} className="flex flex-col gap-4">
            <p className="text-sm text-muted-foreground">
              Dev-only token mint — permissions are resolved from the database for the chosen user.
              Replaced by Keycloak / OIDC in production.
            </p>
            <Label>
              Tenant ID
              <Input name="tenantId" defaultValue={SEED.tenantId} className="font-mono text-xs" />
            </Label>
            <Label>
              User ID
              <Input name="userId" defaultValue={SEED.userId} className="font-mono text-xs" />
            </Label>
            <Button type="submit" className="mt-2 w-full">
              Sign in
            </Button>
          </form>
        </CardContent>
      </Card>
    </main>
  );
}
