// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import PlatformStatesPage from "./page";

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  getToken: vi.fn(),
  listPlatformStates: vi.fn(),
  createPlatformState: vi.fn(),
  updatePlatformState: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mocks.replace }),
}));

vi.mock("@/lib/api", () => ({
  getToken: mocks.getToken,
  listPlatformStates: mocks.listPlatformStates,
  createPlatformState: mocks.createPlatformState,
  updatePlatformState: mocks.updatePlatformState,
}));

vi.mock("@/components/SuperadminShell", () => ({
  SuperadminShell: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

const maharashtra = {
  id: "st-mh",
  name: "Maharashtra",
  code: "MH",
  isActive: true,
  sortOrder: 0,
};

describe("PlatformStatesPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.replace.mockReset();
    mocks.getToken.mockReset().mockReturnValue("token");
    mocks.listPlatformStates.mockReset().mockResolvedValue([maharashtra]);
    mocks.createPlatformState.mockReset();
    mocks.updatePlatformState.mockReset();
  });

  it("adds a state and lists it", async () => {
    mocks.createPlatformState.mockResolvedValue({
      id: "st-ka",
      name: "Karnataka",
      code: "KA",
      isActive: true,
      sortOrder: 1,
    });
    render(<PlatformStatesPage />);
    expect(await screen.findByText("Maharashtra")).toBeTruthy();
    expect(screen.getByRole("columnheader", { name: "Name" })).toBeTruthy();
    expect(screen.getByRole("columnheader", { name: "Code" })).toBeTruthy();
    expect(screen.getByRole("columnheader", { name: "Status" })).toBeTruthy();
    expect(screen.getByRole("columnheader", { name: "Actions" })).toBeTruthy();
    expect(
      screen.getByRole("heading", { name: "Add a state" }).closest("section")?.querySelector("button"),
    ).toBeTruthy();

    fireEvent.change(screen.getByLabelText(/^name$/i), { target: { value: "Karnataka" } });
    fireEvent.change(screen.getByLabelText(/^code$/i), { target: { value: "KA" } });
    fireEvent.click(screen.getByRole("button", { name: "Add state" }));

    await waitFor(() => {
      expect(mocks.createPlatformState).toHaveBeenCalledWith({ name: "Karnataka", code: "KA" });
    });
    expect(await screen.findByText("Karnataka")).toBeTruthy();
  });

  it("renames a listed state", async () => {
    mocks.updatePlatformState.mockResolvedValue({ ...maharashtra, name: "MH State" });
    render(<PlatformStatesPage />);
    expect(await screen.findByText("Maharashtra")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Rename" }));
    const nameFields = screen.getAllByLabelText(/^name$/i);
    fireEvent.change(nameFields[nameFields.length - 1], { target: { value: "MH State" } });
    fireEvent.click(await screen.findByRole("button", { name: "Save" }));

    await waitFor(() => {
      expect(mocks.updatePlatformState).toHaveBeenCalledWith("st-mh", {
        name: "MH State",
        code: "MH",
      });
    });
    expect(await screen.findByText("MH State")).toBeTruthy();
  });

  it("deactivates a listed state", async () => {
    mocks.updatePlatformState.mockResolvedValue({ ...maharashtra, isActive: false });
    render(<PlatformStatesPage />);
    expect(await screen.findByText("Maharashtra")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Deactivate" }));

    await waitFor(() => {
      expect(mocks.updatePlatformState).toHaveBeenCalledWith("st-mh", { isActive: false });
    });
    expect(await screen.findByText("Inactive")).toBeTruthy();
  });

  it("pages the state list and changes the row limit", async () => {
    mocks.listPlatformStates.mockResolvedValue(
      Array.from({ length: 11 }, (_, index) => ({
        id: `st-${index}`,
        name: `State ${index + 1}`,
        code: String(index + 1).padStart(2, "0"),
        isActive: true,
        sortOrder: index,
      })),
    );
    render(<PlatformStatesPage />);
    expect(await screen.findByText("State 1")).toBeTruthy();
    expect(screen.getByText("State 10")).toBeTruthy();
    expect(screen.queryByText("State 11")).toBeNull();
    expect(screen.getByText("1–10 of 11")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    expect(screen.getByText("State 11")).toBeTruthy();
    expect(screen.queryByText("State 1")).toBeNull();
    expect(screen.getByText("11–11 of 11")).toBeTruthy();

    fireEvent.click(screen.getByLabelText(/^rows$/i));
    fireEvent.click(screen.getByRole("option", { name: "25" }));
    expect(screen.getByText("State 1")).toBeTruthy();
    expect(screen.getByText("State 11")).toBeTruthy();
    expect(screen.getByText("1–11 of 11")).toBeTruthy();
  });
});
