// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "@/components/Toast";
import PlanSettingsPage from "./page";

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  getToken: vi.fn(),
  getPlatformPlan: vi.fn(),
  updatePlatformPlan: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mocks.replace }),
}));

vi.mock("@/lib/api", () => ({
  getToken: mocks.getToken,
  getPlatformPlan: mocks.getPlatformPlan,
  updatePlatformPlan: mocks.updatePlatformPlan,
}));

describe("PlanSettingsPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.replace.mockReset();
    mocks.getToken.mockReset().mockReturnValue("token");
    mocks.getPlatformPlan.mockReset().mockResolvedValue({
      id: "plan-1",
      name: "Basic",
      pricePerEmployee: 49,
    });
    mocks.updatePlatformPlan.mockReset();
  });

  it("saves a new per-employee price", async () => {
    mocks.updatePlatformPlan.mockResolvedValue({
      id: "plan-1",
      name: "Basic",
      pricePerEmployee: 59,
    });
    render(
      <ToastProvider>
        <PlanSettingsPage />
      </ToastProvider>,
    );
    const input = await screen.findByLabelText("Price per employee");
    fireEvent.change(input, { target: { value: "59" } });
    fireEvent.click(screen.getByRole("button", { name: "Save price" }));

    await waitFor(() => {
      expect(mocks.updatePlatformPlan).toHaveBeenCalledWith(59);
    });
    expect(await screen.findByText("Plan price saved.")).toBeTruthy();
  });
});
