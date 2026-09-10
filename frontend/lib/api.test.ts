import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  DailyRateMethod,
  completeCompanySetup,
  createEmployee,
  getCompanySetup,
  getEmployee,
  getMe,
  getPlatformLimits,
  listEmployees,
  listPlatformCities,
  listPlatformStates,
  createPlatformState,
  createPlatformCity,
  login,
  safeApiAssetUrl,
  updateCompanyDetails,
  updateEmployee,
  updatePayrollSettings,
  uploadCompanyLogo,
} from "./api";
import { CompanySetupStep } from "./setup";

const API_URL = "http://localhost:5238";

function respondWith(payload: unknown): Response {
  return new Response(JSON.stringify(payload), {
    status: 200,
    headers: { "Content-Type": "application/json" },
  });
}

function mockResponse(payload: unknown) {
  vi.mocked(fetch).mockResolvedValueOnce(respondWith(payload));
}

function requestInit(): RequestInit {
  return vi.mocked(fetch).mock.calls.at(-1)?.[1] ?? {};
}

describe("setup API", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  it("normalizes a backend enum-name setup step", async () => {
    mockResponse({ setupStep: "Review" });

    const result = await login("admin@example.com", "password");

    expect(result.setupStep).toBe(CompanySetupStep.Review);
  });

  it("preserves a valid numeric setup step", async () => {
    mockResponse({ setupStep: CompanySetupStep.PayrollSettings });

    const result = await getMe();

    expect(result.setupStep).toBe(CompanySetupStep.PayrollSettings);
  });

  it.each(["Unknown", 99])(
    "falls back safely for unknown setup step %s",
    async (setupStep) => {
      mockResponse({ setupStep });

      const result = await getCompanySetup();

      expect(result.setupStep).toBe(CompanySetupStep.CompanyDetails);
    },
  );

  it.each([
    ["/uploads/logo.png", `${API_URL}/uploads/logo.png`],
    [`${API_URL}/uploads/logo.png`, `${API_URL}/uploads/logo.png`],
    ["https://example.com/logo.png", null],
    ["javascript:alert(1)", null],
  ])("normalizes safe API asset URL %s", (value, expected) => {
    expect(safeApiAssetUrl(value)).toBe(expected);
  });

  it("normalizes persisted relative logo URLs in setup responses", async () => {
    mockResponse({ setupStep: "Review", logoUrl: "/uploads/logo.png" });

    const result = await getCompanySetup();

    expect(result.logoUrl).toBe(`${API_URL}/uploads/logo.png`);
  });

  it("gets company setup from the exact route", async () => {
    mockResponse({ setupStep: "CompanyDetails" });

    await getCompanySetup();

    expect(fetch).toHaveBeenCalledWith(
      `${API_URL}/api/company/setup`,
      expect.objectContaining({ method: "GET" }),
    );
  });

  it("updates company details with PATCH and the exact body", async () => {
    mockResponse({ setupStep: "PayrollSettings" });
    const input = {
      name: "Acme",
      contactEmail: "payroll@acme.test",
      contactPhone: "1234567890",
      addressLine1: "1 Main Street",
      addressLine2: null,
      city: "Pune",
      state: "Maharashtra",
      postalCode: "411001",
    };

    await updateCompanyDetails(input);

    expect(fetch).toHaveBeenCalledWith(
      `${API_URL}/api/company/setup/details`,
      expect.objectContaining({
        method: "PATCH",
        body: JSON.stringify(input),
      }),
    );
  });

  it("updates payroll settings with PATCH and the exact body", async () => {
    mockResponse({ setupStep: "Review" });
    const input = {
      dailyRateMethod: DailyRateMethod.FixedThirty,
      workingDaysPerMonth: 30,
      weeklyOffDays: ["Sunday"],
    };

    await updatePayrollSettings(input);

    expect(fetch).toHaveBeenCalledWith(
      `${API_URL}/api/company/setup/payroll-settings`,
      expect.objectContaining({
        method: "PATCH",
        body: JSON.stringify(input),
      }),
    );
  });

  it("uploads the logo as file FormData without setting Content-Type", async () => {
    mockResponse({ setupStep: "Review" });
    const file = new File(["logo"], "logo.png", { type: "image/png" });

    await uploadCompanyLogo(file);

    const init = requestInit();
    expect(fetch).toHaveBeenCalledWith(
      `${API_URL}/api/company/setup/logo`,
      expect.objectContaining({ method: "POST" }),
    );
    expect(init.body).toBeInstanceOf(FormData);
    expect((init.body as FormData).get("file")).toBe(file);
    expect(new Headers(init.headers).has("Content-Type")).toBe(false);
  });

  it("completes setup with POST on the exact route and no body", async () => {
    mockResponse({ setupStep: "Complete" });

    await completeCompanySetup();

    expect(fetch).toHaveBeenCalledWith(
      `${API_URL}/api/company/setup/complete`,
      expect.objectContaining({ method: "POST" }),
    );
    expect(requestInit().body).toBeUndefined();
  });
});

describe("platform API", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  it("loads employee limits from /api/platform", async () => {
    mockResponse({
      minEmployeeLimit: 1,
      hardEmployeeCap: 50,
      defaultEmployeeLimit: 50,
      defaultPlanName: "Basic",
      currencyCode: "INR",
    });

    const limits = await getPlatformLimits();

    expect(limits.hardEmployeeCap).toBe(50);
    expect(fetch).toHaveBeenCalledWith(
      `${API_URL}/api/platform`,
      expect.objectContaining({ method: "GET" }),
    );
  });

  it("lists active states and cities for a state", async () => {
    mockResponse([]);
    await listPlatformStates();
    expect(fetch).toHaveBeenCalledWith(
      `${API_URL}/api/platform/states`,
      expect.objectContaining({ method: "GET" }),
    );

    mockResponse([]);
    await listPlatformCities("st-mh", true);
    expect(fetch).toHaveBeenCalledWith(
      `${API_URL}/api/platform/cities?stateId=st-mh&includeInactive=true`,
      expect.objectContaining({ method: "GET" }),
    );
  });

  it("creates a state and a city", async () => {
    mockResponse({ id: "st-ka", name: "Karnataka", code: "KA" });
    await createPlatformState({ name: "Karnataka", code: "KA" });
    expect(fetch).toHaveBeenCalledWith(
      `${API_URL}/api/platform/states`,
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ name: "Karnataka", code: "KA" }),
      }),
    );

    mockResponse({ id: "ct-blr", name: "Bengaluru" });
    await createPlatformCity({ stateId: "st-ka", name: "Bengaluru" });
    expect(fetch).toHaveBeenCalledWith(
      `${API_URL}/api/platform/cities`,
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ stateId: "st-ka", name: "Bengaluru" }),
      }),
    );
  });
});

describe("employee API", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  it("lists employees from /api/employees", async () => {
    mockResponse({ employees: [], activeCount: 0, employeeLimit: 9 });

    await listEmployees();

    expect(fetch).toHaveBeenCalledWith(
      `${API_URL}/api/employees`,
      expect.objectContaining({ method: "GET" }),
    );
  });

  it("creates an employee with POST", async () => {
    mockResponse({ id: "1", employeeCode: "EMP-01" });
    const input = {
      employeeCode: "EMP-01",
      fullName: "Ada Lovelace",
      phone: "9876543210",
      email: "ada@example.com",
      addressLine1: "Main Road",
      city: "Pune",
      state: "Maharashtra",
      postalCode: "411001",
      designation: "Engineer",
      joiningDate: "2026-01-15",
      bankName: "HDFC Bank",
      bankAccountNumber: "123456789012",
      ifsc: "HDFC0001234",
    };

    await createEmployee(input);

    expect(fetch).toHaveBeenCalledWith(
      `${API_URL}/api/employees`,
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify(input),
      }),
    );
  });

  it("loads and updates an employee by id", async () => {
    mockResponse({ id: "abc", employeeCode: "EMP-01" });
    await getEmployee("abc");
    expect(fetch).toHaveBeenCalledWith(
      `${API_URL}/api/employees/abc`,
      expect.objectContaining({ method: "GET" }),
    );

    mockResponse({ id: "abc", employeeCode: "EMP-02" });
    await updateEmployee("abc", {
      employeeCode: "EMP-02",
      fullName: "Ada Lovelace",
      phone: "9876543210",
      email: "ada@example.com",
      addressLine1: "Main Road",
      city: "Pune",
      state: "Maharashtra",
      postalCode: "411001",
      designation: "Engineer",
      joiningDate: "2026-01-15",
      bankName: "HDFC Bank",
      bankAccountNumber: "123456789012",
      ifsc: "HDFC0001234",
    });
    expect(fetch).toHaveBeenCalledWith(
      `${API_URL}/api/employees/abc`,
      expect.objectContaining({ method: "PATCH" }),
    );
  });
});
