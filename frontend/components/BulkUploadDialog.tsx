"use client";

import { useState, useRef } from "react";
import { createPortal } from "react-dom";
import ExcelJS from "exceljs";
import { saveAs } from "file-saver";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/shadcn/table";
import { Alert } from "@/components/ui/Alert";
import { bulkCreateEmployees, type EmployeeInput, listPlatformStates, listPlatformCities, Gender, EmployeeStatus } from "@/lib/api";

type BulkUploadDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess: () => void;
};

type ParsedRow = Record<string, string>;

export function BulkUploadDialog({ open, onOpenChange, onSuccess }: BulkUploadDialogProps) {
  const [file, setFile] = useState<File | null>(null);
  const [parsedData, setParsedData] = useState<ParsedRow[]>([]);
  const [validEmployees, setValidEmployees] = useState<EmployeeInput[]>([]);
  const [invalidRows, setInvalidRows] = useState<{ row: number; error: string }[]>([]);
  const [loading, setLoading] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [error, setError] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);

  const reset = () => {
    setFile(null);
    setParsedData([]);
    setValidEmployees([]);
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
      const arrayBuffer = await selected.arrayBuffer();
      const workbook = new ExcelJS.Workbook();
      await workbook.xlsx.load(arrayBuffer);
      const worksheet = workbook.getWorksheet("Employees") || workbook.worksheets.find(ws => ws.name !== "_MappingData") || workbook.worksheets[0];
      
      if (!worksheet) {
        throw new Error("No worksheet found in the Excel file");
      }

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
              if (cell.value instanceof Date) {
                 rowData[header] = cell.value.toISOString().split('T')[0];
              } else if (cell.value !== null && cell.value !== undefined) {
                 rowData[header] = cell.value.toString().trim();
              } else {
                 rowData[header] = "";
              }
            }
          });
          if (Object.values(rowData).some(v => v !== "")) {
            rows.push(rowData);
          }
        }
      });
      
      setParsedData(rows as ParsedRow[]);
      validateData(rows as ParsedRow[]);
    } catch (err: any) {
      setError("Failed to parse Excel file: " + err.message);
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

  const validateData = (data: ParsedRow[]) => {
    const valid: EmployeeInput[] = [];
    const invalid: { row: number; error: string }[] = [];

    data.forEach((row, index) => {
      const empCode = row["EmployeeCode"] || row["employeeCode"];
      const fullName = row["FullName"] || row["fullName"];
      const email = row["Email"];
      const phone = row["Phone"];
      const designation = row["Designation"];
      const state = row["State"];
      const city = row["City"];
      const addressLine1 = row["AddressLine1"];
      const postalCode = row["PostalCode"];
      const bankName = row["BankName"];
      const bankAccountNumber = row["BankAccountNumber"];
      const ifsc = row["Ifsc"];

      const missing: string[] = [];
      if (!empCode) missing.push("EmployeeCode");
      if (!fullName) missing.push("FullName");
      if (!email) missing.push("Email");
      if (!phone) missing.push("Phone");
      if (!designation) missing.push("Designation");
      if (!state) missing.push("State");
      if (!city) missing.push("City");
      if (!addressLine1) missing.push("AddressLine1");
      if (!postalCode) missing.push("PostalCode");
      if (!bankName) missing.push("BankName");
      if (!bankAccountNumber) missing.push("BankAccountNumber");
      if (!ifsc) missing.push("Ifsc");

      if (missing.length > 0) {
        invalid.push({ row: index + 2, error: `Missing required fields: ${missing.join(", ")}` });
        return;
      }

      // Format validations
      const formatErrors: string[] = [];
      if (!/^[A-Za-z0-9-]+$/.test(empCode)) formatErrors.push("EmployeeCode contains invalid characters");
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) formatErrors.push("Invalid Email format");
      
      const accountDigits = bankAccountNumber.replace(/\D/g, '');
      if (accountDigits.length < 9 || accountDigits.length > 18) formatErrors.push("BankAccountNumber must be 9-18 digits");
      
      if (!/^[A-Z]{4}0[A-Z0-9]{6}$/i.test(ifsc)) formatErrors.push("Invalid IFSC code format");

      if (formatErrors.length > 0) {
        invalid.push({ row: index + 2, error: `Format errors: ${formatErrors.join(", ")}` });
        return;
      }
      
      const genderRaw = row["Gender"];
      let gender: Gender | null = null;
      if (genderRaw === "Male") gender = Gender.Male;
      else if (genderRaw === "Female") gender = Gender.Female;

      // Force all bulk uploaded employees to Draft status to avoid salary structure validation errors
      let status: EmployeeStatus = EmployeeStatus.Draft;

      valid.push({
        employeeCode: empCode.trim(),
        fullName: fullName.trim(),
        email: email.trim(),
        phone: phone.trim(),
        gender: gender,
        dateOfBirth: parseDateString(row["DateOfBirth"]),
        joiningDate: parseDateString(row["JoiningDate"]),
        designation: designation.trim(),
        department: row["Department"] || null,
        state: state.trim(),
        city: city.trim(),
        addressLine1: addressLine1.trim(),
        addressLine2: row["AddressLine2"] || null,
        postalCode: postalCode.trim(),
        bankName: bankName.trim(),
        bankAccountNumber: bankAccountNumber.trim(),
        ifsc: ifsc.trim(),
        status: status,
      });
    });

    setValidEmployees(valid);
    setInvalidRows(invalid);
  };

  const handleUpload = async () => {
    if (validEmployees.length === 0) return;
    setLoading(true);
    setError("");

    try {
      await bulkCreateEmployees(validEmployees);
      onSuccess();
      handleOpenChange(false);
    } catch (err: any) {
      setError(err.message || "Bulk upload failed.");
    } finally {
      setLoading(false);
    }
  };

  const handleDownloadTemplate = async () => {
    setDownloading(true);
    try {
      const workbook = new ExcelJS.Workbook();
      
      // 1. Create Mapping Data for dependent dropdowns
      const mappingSheet = workbook.addWorksheet("_MappingData");
      mappingSheet.state = 'hidden';

      const states = await listPlatformStates(false);
      const citiesPromises = states.map(s => listPlatformCities(s.id, false));
      const citiesResults = await Promise.all(citiesPromises);

      let colIndex = 1;
      for (let i = 0; i < states.length; i++) {
        const state = states[i];
        const cities = citiesResults[i];
        
        const colLetter = mappingSheet.getColumn(colIndex).letter;
        mappingSheet.getCell(`${colLetter}1`).value = state.name;
        
        let rowIndex = 2;
        for (const city of cities) {
          mappingSheet.getCell(`${colLetter}${rowIndex}`).value = city.name;
          rowIndex++;
        }
        
        const rangeName = `CityList_${state.name.replace(/[^a-zA-Z0-9]/g, '')}`;
        if (cities.length > 0) {
          workbook.definedNames.add(`_MappingData!$${colLetter}$2:$${colLetter}$${cities.length + 1}`, rangeName);
        }
        colIndex++;
      }

      if (states.length > 0) {
        const lastColLetter = mappingSheet.getColumn(states.length).letter;
        workbook.definedNames.add(`_MappingData!$A$1:$${lastColLetter}$1`, 'StateList');
      }

      // 2. Create Main Sheet
      const sheet = workbook.addWorksheet("Employees");

      const headers = [
        "EmployeeCode", "FullName", "Email", "Phone", "Gender", 
        "DateOfBirth", "JoiningDate", "Designation", "Department",
        "State", "City", "AddressLine1", "AddressLine2", 
        "PostalCode", "BankName", "BankAccountNumber", "Ifsc", "Status"
      ];
      sheet.addRow(headers);
      
      const headerRow = sheet.getRow(1);
      headerRow.font = { bold: true };
      headerRow.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFD3D3D3' } };

      for (let i = 2; i <= 1000; i++) {
        // Gender (E)
        sheet.getCell(`E${i}`).dataValidation = {
          type: 'list', allowBlank: true, formulae: ['"Male,Female"']
        };
        // Status (R)
        sheet.getCell(`R${i}`).dataValidation = {
          type: 'list', allowBlank: true, formulae: ['"Active,Draft,Inactive"']
        };
        // State (J)
        if (states.length > 0) {
          sheet.getCell(`J${i}`).dataValidation = {
            type: 'list', allowBlank: true, formulae: ['StateList']
          };
          // City (K) - Dependent on State (J)
          sheet.getCell(`K${i}`).dataValidation = {
            type: 'list', allowBlank: true, formulae: [`=INDIRECT("CityList_"&SUBSTITUTE(J${i}," ",""))`]
          };
        }
      }
      
      sheet.columns.forEach(column => { column.width = 20; });

      const buffer = await workbook.xlsx.writeBuffer();
      const blob = new Blob([buffer], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
      saveAs(blob, "employee_upload_template.xlsx");
    } catch (err: any) {
      setError("Failed to download template: " + err.message);
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
          <h2 style={{ margin: 0, fontSize: 'var(--text-xl)' }}>Bulk Upload Employees</h2>
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
                Upload an Excel file (.xlsx) to import multiple employees.
              </p>
              <div className="sa-compose__actions" style={{ justifyContent: "center" }}>
                <input
                  type="file"
                  accept=".xlsx"
                  ref={fileInputRef}
                  style={{ display: "none" }}
                  onChange={handleFileSelect}
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
                {validEmployees.length > 0 && (
                  <div style={{ maxHeight: "250px", overflow: "auto" }}>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Code</TableHead>
                          <TableHead>Name</TableHead>
                          <TableHead>Designation</TableHead>
                          <TableHead>Email</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {validEmployees.slice(0, 5).map((emp, i) => (
                          <TableRow key={i}>
                            <TableCell>{emp.employeeCode}</TableCell>
                            <TableCell>{emp.fullName}</TableCell>
                            <TableCell>{emp.designation}</TableCell>
                            <TableCell>{emp.email}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                    {validEmployees.length > 5 && (
                      <p style={{ padding: "var(--space-sm)", textAlign: "center", color: "var(--color-ink-2)" }}>
                        ... and {validEmployees.length - 5} more valid employees
                      </p>
                    )}
                  </div>
                )}
              </div>

              <div style={{ padding: 'var(--space-md)', background: 'var(--color-paper-2)', borderRadius: 'var(--radius-md)', border: '1px solid var(--color-rule)' }}>
                <p>
                  <strong>{validEmployees.length}</strong> valid records ready to import.
                </p>
                {invalidRows.length > 0 && (
                  <div style={{ marginTop: "var(--space-2xs)" }}>
                    <p style={{ color: "var(--color-error)" }}>
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
                <Button onClick={handleUpload} disabled={validEmployees.length === 0 || loading}>
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
