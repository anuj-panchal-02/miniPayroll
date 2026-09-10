// @vitest-environment jsdom

import { useState } from "react";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { LocationFields } from "./LocationFields";

const mocks = vi.hoisted(() => ({
  listPlatformStates: vi.fn(),
  listPlatformCities: vi.fn(),
}));

vi.mock("@/lib/api", () => ({
  listPlatformStates: mocks.listPlatformStates,
  listPlatformCities: mocks.listPlatformCities,
}));

function Harness({ initialState = "", initialCity = "" }: { initialState?: string; initialCity?: string }) {
  const [state, setState] = useState(initialState);
  const [city, setCity] = useState(initialCity);
  return (
    <>
      <LocationFields
        state={state}
        city={city}
        onStateChange={setState}
        onCityChange={setCity}
      />
      <p data-testid="snapshot">{`${state}|${city}`}</p>
    </>
  );
}

describe("LocationFields", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.listPlatformStates.mockReset().mockResolvedValue([
      { id: "st-mh", name: "Maharashtra", code: "MH", isActive: true, sortOrder: 0 },
      { id: "st-ka", name: "Karnataka", code: "KA", isActive: true, sortOrder: 1 },
    ]);
    mocks.listPlatformCities.mockReset().mockImplementation(async (stateId: string) => {
      if (stateId === "st-mh") {
        return [{ id: "ct-pune", stateId: "st-mh", name: "Pune", isActive: true, sortOrder: 0 }];
      }
      return [{ id: "ct-blr", stateId: "st-ka", name: "Bengaluru", isActive: true, sortOrder: 0 }];
    });
  });

  it("cascades city options from the selected state and clears city when state changes", async () => {
    render(<Harness />);

    await waitFor(() => expect(mocks.listPlatformStates).toHaveBeenCalled());
    fireEvent.click(screen.getByLabelText(/^state$/i));
    fireEvent.click(await screen.findByRole("option", { name: "Maharashtra" }));

    await waitFor(() => expect(mocks.listPlatformCities).toHaveBeenCalledWith("st-mh"));
    fireEvent.click(screen.getByLabelText(/^city$/i));
    fireEvent.click(await screen.findByRole("option", { name: "Pune" }));
    expect(screen.getByTestId("snapshot").textContent).toBe("Maharashtra|Pune");

    fireEvent.click(screen.getByLabelText(/^state$/i));
    fireEvent.click(await screen.findByRole("option", { name: "Karnataka" }));
    expect(screen.getByTestId("snapshot").textContent).toBe("Karnataka|");

    await waitFor(() => expect(mocks.listPlatformCities).toHaveBeenCalledWith("st-ka"));
    fireEvent.click(screen.getByLabelText(/^city$/i));
    expect(await screen.findByRole("option", { name: "Bengaluru" })).toBeTruthy();
    expect(screen.queryByRole("option", { name: "Pune" })).toBeNull();
  });

  it("keeps a saved orphan label until a master is chosen", async () => {
    render(<Harness initialState="Goa" initialCity="Panaji" />);

    expect(screen.getByLabelText(/^state$/i).textContent).toBe("Goa");
    expect(screen.getByLabelText(/^city$/i).textContent).toBe("Panaji");
  });
});
