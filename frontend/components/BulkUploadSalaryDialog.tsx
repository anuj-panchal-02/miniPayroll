"use client";

import { useState, useRef, useEffect } from "react";
import { createPortal } from "react-dom";
import ExcelJS from "exceljs";
import { saveAs } from "file-saver";
import { Button } from "@/components/ui/Button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/shadcn/table";
import { Alert } from "@/components/ui/Alert";
import { 
  bulkCreateSalaryStructures, 
  listSalaryComponents,
  listEmployees,
  EmployeeStatus,
  type BulkSalaryStructureInput, 
  type SalaryComponent,
  SalaryComponentType,
  SalaryComponentValueType
} from "@/lib/api";

type BulkUploadSalaryDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess: () => void;
};

type ParsedRow = Record<string, string>;

export function BulkUploadSalaryDialog({ open, onOpenChange, onSuccess }: BulkUploadSalaryDialogProps) {
  const [file, setFile] = useState<File | null>(null);
  const [parsedData, setParsedData] = useState<ParsedRow[]>([]);
  const [validStructures, setValidStructures] = useState<BulkSalaryStructureInput[]>([]);
  const [invalidRows, setInvalidRows] = useState<{ row: number; error: string }[]>([]);
  const [loading, setLoading] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [error, setError] = useState("");
  const [activeComponents, setActiveComponents] = useState<SalaryComponent[]>([]);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (open) {
      listSalaryComponents().then(setActiveComponents).catch(console.error);
    }
  }, [open]);

  const reset = () => {
    setFile(null);
    setParsedData([]);
    setValidStructures([]);
    setInvalidRows([]);
    setError("");
    if (fileInputRef.current) fileInputRef.current.value = "";
  };

  const handleOpenChange = (newOpen: boolean) => {
    if (!newOpen) reset();
    onOpenChange(newOpen);
  };

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0];
    if (!selected) return;
    setFile(selected);
    setError("");

    try {
      const buffer = await selected.arrayBuffer();
      const workbook = new ExcelJS.Workbook();
      await workbook.xlsx.load(buffer);

      const worksheet = workbook.getWorksheet("SalaryStructures") || workbook.worksheets[0];
      if (!worksheet) throw new Error("No worksheet found");

      const rows: Record<string, string>[] = [];
      const headers: string[] = [];

      worksheet.eachRow((row, rowNumber) => {
        if (rowNumber === 1) {
          row.eachCell({ includeEmpty: true }, (cell, colNumber) => {
            headers[colNumber] = cell.text.trim();
          });
        } else {
          const rowData: Record<string, string> = {};
          row.eachCell({ includeEmpty: true }, (cell, colNumber) => {
            const header = headers[colNumber];
            if (header) {
              rowData[header] = cell.value ? cell.value.toString() : "";
            }
          });
          // Only add if at least EmployeeCode is present
          if (rowData["EmployeeCode"]) {
            rows.push(rowData);
          }
        }
      });

      setParsedData(rows);
      validateData(rows, headers);
    } catch (err: any) {
      setError(err.message || "Failed to parse Excel file");
    }
  };

  const parseDateString = (dateStr?: string): string | null => {
    if (!dateStr) return null;
    const str = dateStr.trim();
    let result = "";
    
    if (/^\d{4}-\d{2}-\d{2}$/.test(str)) {
      result = str;
    } else {
      const parts = str.split(/[\/\-]/);
      if (parts.length === 3) {
        if (parts[2].length === 4) {
          result = `${parts[2]}-${parts[1].padStart(2, '0')}-${parts[0].padStart(2, '0')}`;
        } else if (parts[0].length === 4) {
          result = `${parts[0]}-${parts[1].padStart(2, '0')}-${parts[2].padStart(2, '0')}`;
        }
      }
    }

    if (!result) {
      const parsed = new Date(str);
      if (!isNaN(parsed.getTime())) {
        result = parsed.toISOString().split('T')[0];
      }
    }

    if (result) {
      const d = new Date(result);
      if (isNaN(d.getTime())) return null;
      if (d.toISOString().split('T')[0] !== result) return null;
      return result;
    }
    return null;
  };

  const validateData = (data: ParsedRow[], headers: string[]) => {
    const valid: BulkSalaryStructureInput[] = [];
    const invalid: { row: number; error: string }[] = [];

    const componentHeaders = headers.filter(h => h && h !== "EmployeeCode" && h !== "EffectiveFrom");

    data.forEach((row, index) => {
      const empCode = row["EmployeeCode"]?.trim();
      const effectiveFromRaw = row["EffectiveFrom"]?.trim();

      const missing: string[] = [];
      if (!empCode) missing.push("EmployeeCode");
      if (!effectiveFromRaw) missing.push("EffectiveFrom");
      if (!row["Basic Salary"]) missing.push("Basic Salary");

      if (missing.length > 0) {
        invalid.push({ row: index + 2, error: `Missing required fields: ${missing.join(", ")}` });
        return;
      }

      const effectiveFrom = parseDateString(effectiveFromRaw);
      if (!effectiveFrom) {
        invalid.push({ row: index + 2, error: `Invalid EffectiveFrom date format` });
        return;
      }

      const components: BulkSalaryStructureInput["components"] = [];
      let sortOrder = 0;

      for (const header of componentHeaders) {
        const rawValue = row[header];
        if (!rawValue || rawValue.trim() === "") continue;

        const val = parseFloat(rawValue);
        if (isNaN(val) || val <= 0) {
          invalid.push({ row: index + 2, error: `Invalid value for component '${header}'` });
          return;
        }

        const isPercentage = header.includes("(%)");
        const cleanName = header.replace("(%)", "").trim();
        const valueType = isPercentage ? SalaryComponentValueType.PercentageOfBasic : SalaryComponentValueType.FixedAmount;
        
        const knownComp = activeComponents.find(c => c.name.toLowerCase() === cleanName.toLowerCase());
        const type = knownComp ? knownComp.type : SalaryComponentType.Earning;

        components.push({
          name: cleanName,
          type,
          valueType,
          value: val,
          sortOrder: sortOrder++
        });
      }

      valid.push({
        employeeCode: empCode,
        effectiveFrom: effectiveFrom,
        components: components
      });
    });

    setValidStructures(valid);
    setInvalidRows(invalid);
  };

  const handleUpload = async () => {
    if (validStructures.length === 0) return;
    
    setLoading(true);
    try {
      await bulkCreateSalaryStructures(validStructures);
      onSuccess();
      handleOpenChange(false);
    } catch (err: any) {
      setError(err.message || "Failed to upload salary structures");
    } finally {
      setLoading(false);
    }
  };

  const handleDownloadTemplate = async () => {
    setDownloading(true);
    try {
      const listResult = await listEmployees().catch(() => null);
      const draftEmployees = listResult?.employees.filter(e => e.status === EmployeeStatus.Draft) || [];

      const workbook = new ExcelJS.Workbook();
      const sheet = workbook.addWorksheet("SalaryStructures");

      const columns = [
        { header: "EmployeeCode", key: "EmployeeCode", width: 15 },
        { header: "EffectiveFrom", key: "EffectiveFrom", width: 15 },
        { header: "Basic Salary", key: "Basic Salary", width: 15 },
      ];

      activeComponents.forEach(comp => {
        if (comp.name !== "Basic Salary") {
          columns.push({ header: comp.name, key: comp.name, width: 20 });
        }
      });

      sheet.columns = columns;

      sheet.getRow(1).font = { bold: true };
      sheet.getRow(1).fill = {
        type: "pattern",
        pattern: "solid",
        fgColor: { argb: "FFD3D3D3" },
      };

      if (draftEmployees.length > 0) {
        draftEmployees.forEach(emp => {
          const rowData: Record<string, string> = {
            "EmployeeCode": emp.employeeCode,
            "EffectiveFrom": "",
            "Basic Salary": ""
          };
          sheet.addRow(rowData);
        });
      }

      const buffer = await workbook.xlsx.writeBuffer();
      const blob = new Blob([buffer], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
      saveAs(blob, "Salary_Structures_Template.xlsx");
    } catch (err: any) {
      setError("Failed to generate template");
    } finally {
      setDownloading(false);
    }
  };

  if (!open) return null;
  if (typeof document === "undefined") return null;

  return createPortal(
    <div style={{ position: 'fixed', top: 0, left: 0, width: '100vw', height: '100vh', background: 'rgba(0,0,0,0.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 9999 }}>
      <div style={{ background: 'white', borderRadius: 'var(--radius-lg)', width: '100%', maxWidth: '700px', maxHeight: '90vh', overflowY: 'auto', padding: 'var(--space-xl)', boxShadow: 'var(--shadow-xl)' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-lg)' }}>
          <h2 style={{ margin: 0, fontSize: 'var(--text-xl)' }}>Bulk Upload Salary Structures</h2>
          <Button variant="ghost" onClick={() => handleOpenChange(false)}>Close</Button>
        </div>
        
        <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-lg)' }}>
          {error ? (
            <div style={{ background: '#fee2e2', color: '#b91c1c', padding: 'var(--space-sm) var(--space-md)', borderRadius: 'var(--radius-md)', fontSize: 'var(--text-sm)', border: '1px solid #f87171' }}>
              {error}
            </div>
          ) : null}
          
          {!file ? (
            <div style={{ padding: "var(--space-2xl)", textAlign: "center", border: "2px dashed var(--color-rule)", borderRadius: "var(--radius-lg)", background: "var(--color-paper-2)" }}>
              <p style={{ marginBottom: "var(--space-md)", color: "var(--color-ink-2)" }}>
                Upload an Excel file (.xlsx) to import multiple salary structures.
              </p>
              <div className="sa-compose__actions" style={{ justifyContent: "center" }}>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept=".xlsx, .xls"
                  onChange={handleFileSelect}
                  style={{ display: "none" }}
                  disabled={loading}
                />
                <Button onClick={handleDownloadTemplate} disabled={downloading}>
                  {downloading ? "Generating..." : "Download Template"}
                </Button>
                <Button variant="ghost" onClick={() => fileInputRef.current?.click()}>Select Excel File</Button>
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)' }}>
              <div style={{ border: '1px solid var(--color-rule)', borderRadius: 'var(--radius-md)', overflow: 'hidden' }}>
                <div style={{ padding: 'var(--space-md)', background: 'var(--color-paper-2)', borderBottom: '1px solid var(--color-rule)' }}>
                  <h3 style={{ margin: 0, fontSize: "var(--text-lg)", fontWeight: 600 }}>Preview</h3>
                </div>
                {validStructures.length > 0 && (
                  <div style={{ maxHeight: "250px", overflow: "auto" }}>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Code</TableHead>
                          <TableHead>Effective From</TableHead>
                          <TableHead>Components</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {validStructures.slice(0, 5).map((row, i) => (
                          <TableRow key={i}>
                            <TableCell>{row.employeeCode}</TableCell>
                            <TableCell>{row.effectiveFrom}</TableCell>
                            <TableCell>{row.components.length} components</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                    {validStructures.length > 5 && (
                      <p style={{ padding: "var(--space-sm)", textAlign: "center", color: "var(--color-ink-2)" }}>
                        ... and {validStructures.length - 5} more valid structures
                      </p>
                    )}
                  </div>
                )}
              </div>

              <div style={{ padding: 'var(--space-md)', background: 'var(--color-paper-2)', borderRadius: 'var(--radius-md)', border: '1px solid var(--color-rule)' }}>
                <p style={{ margin: 0 }}>
                  <strong>{validStructures.length}</strong> valid records ready to import.
                </p>
                {invalidRows.length > 0 && (
                  <div style={{ marginTop: "var(--space-2xs)" }}>
                    <p style={{ color: "var(--color-error)", margin: 0 }}>
                      <strong>{invalidRows.length}</strong> rows contain errors and will be skipped.
                    </p>
                    <div style={{ maxHeight: "100px", overflowY: "auto", marginTop: "var(--space-xs)", borderTop: "1px solid #fee2e2", paddingTop: "var(--space-xs)" }}>
                      <ul style={{ color: "var(--color-error)", fontSize: "var(--text-xs)", margin: 0, paddingLeft: "var(--space-md)" }}>
                        {invalidRows.map((ir, i) => (
                          <li key={i}>Row {ir.row}: {ir.error}</li>
                        ))}
                      </ul>
                    </div>
                  </div>
                )}
              </div>

              <div className="sa-compose__actions">
                <Button onClick={handleUpload} disabled={validStructures.length === 0 || loading}>
                  {loading ? "Uploading..." : "Confirm & Upload"}
                </Button>
                <Button variant="ghost" onClick={reset} disabled={loading}>
                  Cancel
                </Button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>,
    document.body
  );
}
