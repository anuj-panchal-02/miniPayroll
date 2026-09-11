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
    string? CompanyAddress,
    string? PfEstablishmentCode,
    string? EsiCode,
    string EmployeeName,
    string EmployeeCode,
    string Designation,
    int DaysEmployed,
    int Year,
    int Month,
    IReadOnlyList<PayslipLineSnapshot> Earnings,
    decimal GrossEarnings,
    IReadOnlyList<PayslipLineSnapshot> Deductions,
    decimal TotalDeductions,
    decimal NetSalary,
    decimal EmployerPf,
    decimal EmployerEsi,
    bool Reversed,
    string? PaymentLabel);

public sealed class PayslipPdfService
{
    public const string BrandWatermark = "miniPayroll";
    public const string ReversedWatermark = "REVERSED";

    private static readonly Color Ink = Colors.Grey.Darken3;
    private static readonly Color Muted = Colors.Grey.Darken1;
    private static readonly Color Rule = Colors.Grey.Lighten2;
    private static readonly Color Band = Colors.Grey.Lighten4;

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
        page.Margin(36);
        page.DefaultTextStyle(style => style.FontSize(9).FontColor(Ink));

        page.Content().Layers(layers =>
        {
            layers.Layer().AlignCenter().AlignMiddle().Rotate(-30)
                .Text(BrandWatermark)
                .FontSize(72)
                .FontColor(Colors.Grey.Lighten3);

            if (slip.Reversed)
            {
                layers.Layer().AlignCenter().AlignMiddle().Rotate(-30)
                    .Text(ReversedWatermark)
                    .FontSize(64)
                    .FontColor(Colors.Grey.Lighten1)
                    .Bold();
            }

            layers.PrimaryLayer().Column(column =>
            {
                column.Spacing(14);
                column.Item().Element(block => ComposeHeader(block, slip));
                column.Item().Element(block => ComposeFacts(block, slip));
                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(block => ComposeAmountColumn(
                        block,
                        "Earnings",
                        slip.Earnings,
                        "Gross earnings",
                        slip.GrossEarnings));
                    row.ConstantItem(16);
                    row.RelativeItem().Element(block => ComposeAmountColumn(
                        block,
                        "Deductions",
                        slip.Deductions,
                        "Total deductions",
                        slip.TotalDeductions));
                });
                column.Item().Element(block => ComposeNet(block, slip));
                if (slip.EmployerPf > 0 || slip.EmployerEsi > 0)
                {
                    column.Item().Element(block => ComposeEmployerCosts(block, slip));
                }
                if (!string.IsNullOrWhiteSpace(slip.PaymentLabel))
                {
                    column.Item().Text(slip.PaymentLabel).FontColor(Muted);
                }
            });
        });

        page.Footer().AlignCenter().Text("This is a computer-generated payslip.")
            .FontSize(8)
            .FontColor(Muted);
    }

    private static void ComposeHeader(IContainer container, PayslipSnapshot slip)
    {
        container.Background(Band).Padding(12).Row(row =>
        {
            if (slip.LogoBytes is { Length: > 0 })
            {
                row.ConstantItem(52).Height(52).Image(slip.LogoBytes).FitArea();
                row.ConstantItem(12);
            }

            row.RelativeItem().Column(header =>
            {
                header.Spacing(2);
                header.Item().Text(slip.CompanyName).FontSize(16).Bold();
                if (!string.IsNullOrWhiteSpace(slip.CompanyAddress))
                {
                    header.Item().Text(slip.CompanyAddress).FontColor(Muted);
                }
                var codes = StatutoryCodes(slip);
                if (!string.IsNullOrWhiteSpace(codes))
                {
                    header.Item().Text(codes).FontColor(Muted);
                }
            });

            row.ConstantItem(120).AlignRight().Column(meta =>
            {
                meta.Item().Text("Payslip").FontSize(11).Bold();
                meta.Item().Text(MonthLabel(slip.Year, slip.Month)).FontColor(Muted);
            });
        });
    }

    private static void ComposeFacts(IContainer container, PayslipSnapshot slip)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(88);
                columns.RelativeColumn();
                columns.ConstantColumn(88);
                columns.RelativeColumn();
            });

            Fact(table, "Name", slip.EmployeeName);
            Fact(table, "Employee ID", slip.EmployeeCode);
            Fact(table, "Designation", string.IsNullOrWhiteSpace(slip.Designation) ? "—" : slip.Designation);
            Fact(table, "Days paid", slip.DaysEmployed.ToString());
        });
    }

    private static void Fact(TableDescriptor table, string label, string value)
    {
        table.Cell().Element(FactLabel).Text(label).FontColor(Muted);
        table.Cell().Element(FactValue).Text(value);
    }

    private static void ComposeAmountColumn(
        IContainer container,
        string title,
        IReadOnlyList<PayslipLineSnapshot> lines,
        string totalLabel,
        decimal total)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(4).Text(title).FontSize(10).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(88);
                });

                if (lines.Count == 0)
                {
                    table.Cell().Element(LineCell).Text("None").FontColor(Muted);
                    table.Cell().Element(LineCell).AlignRight().Text(FormatAmount(0)).FontColor(Muted);
                }
                else
                {
                    foreach (var line in lines)
                    {
                        table.Cell().Element(LineCell).Text(line.Name);
                        table.Cell().Element(LineCell).AlignRight().Text(FormatAmount(line.Amount));
                    }
                }

                table.Cell().Element(TotalCell).Text(totalLabel).Bold();
                table.Cell().Element(TotalCell).AlignRight().Text(FormatAmount(total)).Bold();
            });
        });
    }

    private static void ComposeNet(IContainer container, PayslipSnapshot slip)
    {
        container.Background(Band).Padding(10).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Net salary").FontSize(12).Bold();
                row.ConstantItem(120).AlignRight().Text(FormatAmount(slip.NetSalary)).FontSize(12).Bold();
            });
            column.Item().PaddingTop(2).Text(IndianRupeeWords.ToRupees(slip.NetSalary)).Italic().FontColor(Muted);
        });
    }

    private static void ComposeEmployerCosts(IContainer container, PayslipSnapshot slip)
    {
        container.Column(column =>
        {
            column.Item().Text("Employer contributions (not deducted from net)").FontColor(Muted).Bold();
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(88);
                });
                if (slip.EmployerPf > 0)
                {
                    table.Cell().Element(LineCell).Text("Employer PF");
                    table.Cell().Element(LineCell).AlignRight().Text(FormatAmount(slip.EmployerPf));
                }
                if (slip.EmployerEsi > 0)
                {
                    table.Cell().Element(LineCell).Text("Employer ESI");
                    table.Cell().Element(LineCell).AlignRight().Text(FormatAmount(slip.EmployerEsi));
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
            run.CompanyAddress,
            run.PfEstablishmentCode,
            run.EsiCode,
            row.FullName,
            row.EmployeeCode,
            row.Designation,
            row.DaysEmployed,
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
            row.EmployerPf,
            row.EmployerEsi,
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

    private static string StatutoryCodes(PayslipSnapshot slip)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(slip.PfEstablishmentCode))
        {
            parts.Add($"PF {slip.PfEstablishmentCode}");
        }
        if (!string.IsNullOrWhiteSpace(slip.EsiCode))
        {
            parts.Add($"ESI {slip.EsiCode}");
        }
        return string.Join("   ", parts);
    }

    private static string MonthLabel(int year, int month) =>
        new DateTime(year, month, 1).ToString("MMMM yyyy");

    private static string FormatAmount(decimal amount) =>
        amount.ToString("C0", new System.Globalization.CultureInfo("en-IN"));

    private static IContainer LineCell(IContainer container) =>
        container.BorderBottom(0.4f).BorderColor(Rule).PaddingVertical(3);

    private static IContainer TotalCell(IContainer container) =>
        container.BorderTop(0.8f).BorderColor(Ink).PaddingVertical(4);

    private static IContainer FactLabel(IContainer container) =>
        container.BorderBottom(0.4f).BorderColor(Rule).PaddingVertical(4);

    private static IContainer FactValue(IContainer container) =>
        container.BorderBottom(0.4f).BorderColor(Rule).PaddingVertical(4).PaddingRight(12);
}
