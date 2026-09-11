import { beforeEach, describe, expect, it, vi } from "vitest";
import { CompanySetupStep } from "./setup";
import { clearSession, loadSession, readSession } from "./session";

const getMe = vi.hoisted(() => vi.fn());

vi.mock("./api", () => ({
  getMe: (...args: unknown[]) => getMe(...args),
}));

const account = {
  id: "user-1",
  email: "admin@example.com",
  roles: ["Superadmin"],
  companyId: null,
  mustChangePassword: false,
  twoFactorEnabled: false,
  isSetupComplete: true,
  setupStep: CompanySetupStep.Complete,
};

describe("session cache", () => {
  beforeEach(() => {
    getMe.mockReset();
    clearSession();
  });

  it("reuses an in-flight getMe and remembers the snapshot", async () => {
    let resolveMe!: (value: typeof account) => void;
    getMe.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveMe = resolve;
        }),
    );

    const first = loadSession();
    const second = loadSession();
    expect(getMe).toHaveBeenCalledTimes(1);
    expect(readSession()).toBeNull();

    resolveMe(account);
    await expect(first).resolves.toEqual(account);
    await expect(second).resolves.toEqual(account);
    expect(readSession()).toEqual(account);

    await expect(loadSession()).resolves.toEqual(account);
    expect(getMe).toHaveBeenCalledTimes(1);
  });

  it("clears the snapshot so the next load fetches again", async () => {
    getMe.mockResolvedValue(account);
    await loadSession();
    clearSession();
    expect(readSession()).toBeNull();

    getMe.mockResolvedValue({ ...account, email: "next@example.com" });
    await expect(loadSession()).resolves.toMatchObject({ email: "next@example.com" });
    expect(getMe).toHaveBeenCalledTimes(2);
  });
});
