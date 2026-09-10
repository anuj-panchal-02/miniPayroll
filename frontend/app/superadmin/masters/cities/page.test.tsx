// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import PlatformCitiesPage from "./page";

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  getToken: vi.fn(),
  listPlatformStates: vi.fn(),
  listPlatformCities: vi.fn(),
  createPlatformCity: vi.fn(),
  updatePlatformCity: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mocks.replace }),
}));

vi.mock("@/lib/api", () => ({
  getToken: mocks.getToken,
  listPlatformStates: mocks.listPlatformStates,
  listPlatformCities: mocks.listPlatformCities,
  createPlatformCity: mocks.createPlatformCity,
  updatePlatformCity: mocks.updatePlatformCity,
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

const pune = {
  id: "ct-pune",
  stateId: "st-mh",
  name: "Pune",
  isActive: true,
  sortOrder: 0,
};

describe("PlatformCitiesPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.replace.mockReset();
    mocks.getToken.mockReset().mockReturnValue("token");
    mocks.listPlatformStates.mockReset().mockResolvedValue([maharashtra]);
    mocks.listPlatformCities.mockReset().mockResolvedValue([pune]);
    mocks.createPlatformCity.mockReset();
    mocks.updatePlatformCity.mockReset();
  });

  it("adds a city after a state is selected", async () => {
    mocks.createPlatformCity.mockResolvedValue({
      id: "ct-mumbai",
      stateId: "st-mh",
      name: "Mumbai",
      isActive: true,
      sortOrder: 1,
    });
    render(<PlatformCitiesPage />);
    expect(await screen.findByLabelText(/^state$/i)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Add city" })).toHaveProperty("disabled", true);

    fireEvent.click(screen.getByLabelText(/^state$/i));
    fireEvent.click(screen.getByRole("option", { name: "Maharashtra" }));

    expect(await screen.findByText("Pune")).toBeTruthy();
    expect(screen.getByRole("columnheader", { name: "Name" })).toBeTruthy();
    expect(screen.getByRole("columnheader", { name: "Status" })).toBeTruthy();
    fireEvent.change(screen.getByLabelText(/^name$/i), { target: { value: "Mumbai" } });
    fireEvent.click(screen.getByRole("button", { name: "Add city" }));

    await waitFor(() => {
      expect(mocks.createPlatformCity).toHaveBeenCalledWith({
        stateId: "st-mh",
        name: "Mumbai",
      });
    });
    expect(await screen.findByText("Mumbai")).toBeTruthy();
  });

  it("pages cities for the selected state", async () => {
    mocks.listPlatformCities.mockResolvedValue(
      Array.from({ length: 11 }, (_, index) => ({
        id: `ct-${index}`,
        stateId: "st-mh",
        name: `City ${index + 1}`,
        isActive: true,
        sortOrder: index,
      })),
    );
    render(<PlatformCitiesPage />);
    fireEvent.click(await screen.findByLabelText(/^state$/i));
    fireEvent.click(screen.getByRole("option", { name: "Maharashtra" }));

    expect(await screen.findByText("City 1")).toBeTruthy();
    expect(screen.getByText("City 10")).toBeTruthy();
    expect(screen.queryByText("City 11")).toBeNull();
    expect(screen.getByText("1–10 of 11")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    expect(screen.getByText("City 11")).toBeTruthy();
    expect(screen.queryByText("City 1")).toBeNull();
  });
});
