// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Toast, ToastOutlet, ToastProvider, TOAST_DURATION_MS, useToast } from "./Toast";

describe("Toast", () => {
  afterEach(() => {
    cleanup();
    vi.useRealTimers();
  });

  it("floats an error and dismisses after the duration", () => {
    vi.useFakeTimers();
    const onDismiss = vi.fn();
    const { rerender } = render(
      <Toast message="Could not save." onDismiss={onDismiss} />,
    );

    expect(screen.getByRole("alert").textContent).toBe("Could not save.");
    expect(document.body.querySelector(".mp-toast")).toBeTruthy();

    vi.advanceTimersByTime(TOAST_DURATION_MS - 1);
    expect(onDismiss).not.toHaveBeenCalled();

    vi.advanceTimersByTime(1);
    expect(onDismiss).toHaveBeenCalledTimes(1);

    rerender(<Toast message={null} onDismiss={onDismiss} />);
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("restarts the timer when the message changes", () => {
    vi.useFakeTimers();
    const onDismiss = vi.fn();
    const { rerender } = render(
      <Toast message="Employee limit saved." tone="success" onDismiss={onDismiss} />,
    );

    vi.advanceTimersByTime(TOAST_DURATION_MS - 500);
    rerender(
      <Toast message="Payment recorded." tone="success" onDismiss={onDismiss} />,
    );
    vi.advanceTimersByTime(TOAST_DURATION_MS - 1);
    expect(onDismiss).not.toHaveBeenCalled();
    expect(screen.getByRole("status").textContent).toBe("Payment recorded.");

    vi.advanceTimersByTime(1);
    expect(onDismiss).toHaveBeenCalledTimes(1);
  });
});

describe("useToast", () => {
  afterEach(cleanup);

  it("shows an error then replaces it with success", () => {
    function Probe() {
      const toast = useToast();
      return (
        <>
          <button type="button" onClick={() => toast.showError("Could not save.")}>
            Fail
          </button>
          <button type="button" onClick={() => toast.showSuccess("Saved.")}>
            Succeed
          </button>
          <ToastOutlet toast={toast} />
        </>
      );
    }

    render(<Probe />);
    fireEvent.click(screen.getByRole("button", { name: "Fail" }));
    expect(screen.getByRole("alert").textContent).toBe("Could not save.");
    fireEvent.click(screen.getByRole("button", { name: "Succeed" }));
    expect(screen.getByRole("status").textContent).toBe("Saved.");
  });

  it("keeps a message after the providing tree's child is swapped", () => {
    function Trigger() {
      const toast = useToast();
      return (
        <button type="button" onClick={() => toast.showSuccess("Draft saved.")}>
          Save
        </button>
      );
    }

    const { rerender } = render(
      <ToastProvider>
        <Trigger />
      </ToastProvider>,
    );
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(screen.getByRole("status").textContent).toBe("Draft saved.");

    rerender(
      <ToastProvider>
        <p>Next screen</p>
      </ToastProvider>,
    );
    expect(screen.getByText("Next screen")).toBeTruthy();
    expect(screen.getByRole("status").textContent).toBe("Draft saved.");
  });
});
