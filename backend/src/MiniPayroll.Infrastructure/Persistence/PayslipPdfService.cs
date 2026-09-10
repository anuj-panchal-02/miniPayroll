using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Payroll;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MiniPayroll.Infrastructure.Persistence;

public sealed record PayslipLineSnapshot(string Name, decimal Amount);

public sealed record PayslipSnapshot(
    string CompanyName,
    byte[]? LogoBytes,
    string EmployeeName,
    string EmployeeCode,
    string Designation,
    int Year,
    int Month,
    IReadOnlyList<PayslipLineSnapshot> Earnings,
    decimal GrossEarnings,
    IReadOnlyList<PayslipLineSnapshot> Deductions,
    decimal TotalDeductions,
    decimal NetSalary,
    bool Reversed,
    string? PaymentLabel);

public sealed class PayslipPdfService
{
    public PayslipPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(PayslipSnapshot slip) =>
        Document.Create(container => Compose(container, [slip])).GeneratePdf();

    public byte[] RenderCombined(IReadOnlyList<PayslipSnapshot> slips) =>
        Document.Create(container => Compose(container, slips)).GeneratePdf();

    private static void Compose(IDocumentContainer container, IReadOnlyList<PayslipSnapshot> slips)
    {
        if (slips.Count == 0)
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Content().Text("No payslips.");
            });
            return;
        }

        foreach (var slip in slips)
        {
            container.Page(page => ComposePage(page, slip));
        }
    }

    private static void ComposePage(PageDescriptor page, PayslipSnapshot slip)
    {
        page.Size(PageSizes.A4);
        page.Margin(40);
        page.DefaultTextStyle(style => style.FontSize(10));

        page.Content().Layers(layers =>
        {
            if (slip.Reversed)
            {
                layers.Layer().AlignCenter().AlignMiddle().Rotate(-30).Text("REVERSED")
                    .FontSize(64)
                    .FontColor(Colors.Grey.Lighten2)
                    .Bold();
            }

            layers.PrimaryLayer().Column(column =>
            {
                column.Spacing(10);
                column.Item().Row(row =>
                {
                    if (slip.LogoBytes is { Length: > 0 })
                    {
                        row.ConstantItem(56).Height(56).Image(slip.LogoBytes).FitArea();
                    }
                    row.RelativeItem().Column(header =>
                    {
                        header.Item().Text(slip.CompanyName).FontSize(16).Bold();
                        header.Item().Text($"Payslip · {MonthLabel(slip.Year, slip.Month)}").FontSize(11);
                    });
                });

                column.Item().Text($"{slip.EmployeeName} ({slip.EmployeeCode})").FontSize(12).Bold();
                if (!string.IsNullOrWhiteSpace(slip.Designation))
                {
                    column.Item().Text(slip.Designation);
                }

                column.Item().Text("Earnings").Bold();
                foreach (var line in slip.Earnings)
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text(line.Name);
                        row.ConstantItem(100).AlignRight().Text(FormatAmount(line.Amount));
                    });
                }
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Gross earnings").Bold();
                    row.ConstantItem(100).AlignRight().Text(FormatAmount(slip.GrossEarnings)).Bold();
                });

                column.Item().Text("Deductions").Bold();
                if (slip.Deductions.Count == 0)
                {
                    column.Item().Text("None");
                }
                foreach (var line in slip.Deductions)
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text(line.Name);
                        row.ConstantItem(100).AlignRight().Text(FormatAmount(line.Amount));
                    });
                }
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Total deductions").Bold();
                    row.ConstantItem(100).AlignRight().Text(FormatAmount(slip.TotalDeductions)).Bold();
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Net salary").FontSize(12).Bold();
                    row.ConstantItem(100).AlignRight().Text(FormatAmount(slip.NetSalary)).FontSize(12).Bold();
                });
                column.Item().Text(IndianRupeeWords.ToRupees(slip.NetSalary)).Italic();

                if (!string.IsNullOrWhiteSpace(slip.PaymentLabel))
                {
                    column.Item().Text(slip.PaymentLabel);
                }
            });
        });
    }

    public static PayslipSnapshot FromRun(
        PayrollRun run,
        PayrollEmployee row,
        byte[]? logoBytes)
    {
        return new PayslipSnapshot(
            run.CompanyName ?? "Company",
            logoBytes,
            row.FullName,
            row.EmployeeCode,
            row.Designation,
            run.Year,
            run.Month,
            row.Earnings.OrderBy(line => line.SortOrder)
                .Select(line => new PayslipLineSnapshot(line.Name, line.Amount))
                .ToList(),
            row.GrossEarnings,
            row.Deductions.OrderBy(line => line.SortOrder)
                .Select(line => new PayslipLineSnapshot(line.Name, line.Amount))
                .ToList(),
            row.TotalDeductions,
            row.NetSalary,
            run.Status == Domain.Enums.PayrollRunStatus.Reversed,
            PaymentLabel(row));
    }

    public static string PaymentLabel(PayrollEmployee row)
    {
        if (row.PaymentStatus != Domain.Enums.SalaryPaymentStatus.Paid
            || row.PaymentMode is null
            || row.PaidOn is null)
        {
            return "Payment status: Unpaid";
        }

        var mode = row.PaymentMode switch
        {
            Domain.Enums.SalaryPaymentMode.Bank => "Bank",
            Domain.Enums.SalaryPaymentMode.Upi => "UPI",
            Domain.Enums.SalaryPaymentMode.Cash => "Cash",
            _ => "Other"
        };
        var reference = string.IsNullOrWhiteSpace(row.PaymentReference)
            ? ""
            : $" ({row.PaymentReference})";
        return $"Paid via {mode} on {row.PaidOn.Value:dd MMM yyyy}{reference}";
    }

    private static string MonthLabel(int year, int month) =>
        new DateTime(year, month, 1).ToString("MMMM yyyy");

    private static string FormatAmount(decimal amount) =>
        amount.ToString("C0", new System.Globalization.CultureInfo("en-IN"));
}
