package services;

import java.util.*;

/**
 * Single source of truth for building the ImportJournal payload.
 * Returns a Map that will serialize to the expected JSON.
 */

public final class JournalBuilder {
    private JournalBuilder() {}

    /**
     * Build one journal (with two detail lines: DR and CR) from a DB row.
     * NOTE: Expects keys like: Ser, Date, Amount, Descr1, Room_no, Rent_no,
     *       DebitAccount1, CreditAccount1 (or CreditAccount11), DebitAccount2, CreditAccount2,
     *       CostCenterCode (or costCenterCode).
     */


    private static boolean isValid(Object v) {
        if (v == null) return false;
        String s = v.toString().trim();
        return s.length() > 1;   // must be at least 2 meaningful characters
    }

    public static Map<String,Object> buildJournalForApi(Map<String,Object> r) {
        if (r == null) return null;
        Object debitAcct = r.get("DebitAccount1");
        Object creditAcct = r.get("CreditAccount11");
        String date = toYmd(r.get("Date"));                // "YYYY-MM-DD"

        double amt  = Math.abs(toDouble(r.get("Amount")));
        double amt_credit1  = Math.abs(toDouble(r.get("Credit_Amount1")));
        double amt_credit2  = Math.abs(toDouble(r.get("Credit_Amount2")));
        double amt_debit1  = Math.abs(toDouble(r.get("Debit_Amount1")));
        double amt_debit2  = Math.abs(toDouble(r.get("Debit_Amount2")));
        Object Cr_dtl_ac1 = r.get("Cr_dtl_ac1");
        Object Cr_dtl_ac2 = r.get("Cr_dtl_ac2");
        Object Dr_dtl_ac1 = r.get("Dr_dtl_ac1");
        Object Dr_dtl_ac2 = r.get("Dr_dtl_ac2");
        // always positive
        if (!isValid(debitAcct) || !isValid(creditAcct)) {
            return null; // skip sending JSON
        }


        Map<String,Object> j = new LinkedHashMap<>();
        j.put("docSerExternal", s(r.get("Ser")));
        j.put("branchNo", 1);
        j.put("docDate", date);
        j.put("docNo", null);
        j.put("jvType", 1);
        j.put("amountLocal", amt);
        j.put("referenceNo", "0");
        j.put("beneficiaryName", "");
        j.put("receiver", "");
        j.put("manualDocNo", "");
        j.put("description", "");
        j.put("addTerminalName", "1");

        List<Map<String,Object>> details = new ArrayList<>();

        // Prefer CreditAccount11; fallback to CreditAccount1
        if (creditAcct == null) creditAcct = r.get("CreditAccount1");

        // Accept either alias casing for cost center
        Object DrcostCenter = r.get("DrcostCenterCode");
        Object CrcostCenter = r.get("CrcostCenterCode");


        // Common description
        String lineDescr = "room#= " + s(r.get("Room_no")) +
                " rent#= " + s(r.get("Rent_no")) + " " + s(r.get("Descr1"));

        // Debit 1
        if (debitAcct != null && !String.valueOf(debitAcct).isBlank()) {
            Map<String, Object> dr = new LinkedHashMap<>();
            dr.put("docDueDate", date);
            dr.put("accountCode", r.get("DebitAccount1"));
            dr.put("accountCodeDtl", Dr_dtl_ac1);
            dr.put("accountCodeDtlSub", "");
            dr.put("currencyCode", "SAR");
            dr.put("exchangeRate", 0);
            dr.put("drOrCr", 1);
            dr.put("amountLocal", amt_debit1);
            dr.put("amountForeign", 0);
            dr.put("costCenterCode", DrcostCenter);      // 👈 right after amountForeign
            dr.put("chequeNo", "0");
            dr.put("referenceNo", "0");
            dr.put("billNo", "");
            dr.put("billSer", "");
            dr.put("installmentNo", 0);
            dr.put("description", lineDescr);
            details.add(dr);   // 👈 only added if DebitAccount1 has a value

        }

        // Debit 2
        Object debitAcct2 = r.get("DebitAccount2");
        if (debitAcct2 != null && !String.valueOf(debitAcct2).isBlank()) {
            Map<String, Object> dr2 = new LinkedHashMap<>();
            dr2.put("docDueDate", date);
            dr2.put("accountCode", r.get("DebitAccount2"));
            dr2.put("accountCodeDtl", Dr_dtl_ac2);
            dr2.put("accountCodeDtlSub", "");
            dr2.put("currencyCode", "SAR");
            dr2.put("exchangeRate", 0);
            dr2.put("drOrCr", 1);
            dr2.put("amountLocal", amt_debit2);
            dr2.put("amountForeign", 0);
            dr2.put("costCenterCode", DrcostCenter);      // 👈 right after amountForeign
            dr2.put("chequeNo", "0");
            dr2.put("referenceNo", "0");
            dr2.put("billNo", "");
            dr2.put("billSer", "");
            dr2.put("installmentNo", 0);
            dr2.put("description", lineDescr);
            details.add(dr2);
        }

        // Credit 1

        if (creditAcct != null && !String.valueOf(creditAcct).isBlank()) {
            Map<String, Object> cr = new LinkedHashMap<>();
            cr.put("docDueDate", date);
            cr.put("accountCode", creditAcct);
            cr.put("accountCodeDtl", Cr_dtl_ac1);
            cr.put("accountCodeDtlSub", "");
            cr.put("currencyCode", "SAR");
            cr.put("exchangeRate", 0);
            cr.put("drOrCr", -1);
            cr.put("amountLocal", amt_credit1);
            cr.put("amountForeign", 0);
            cr.put("costCenterCode", CrcostCenter);      // 👈 right after amountForeign
            cr.put("chequeNo", "0");
            cr.put("referenceNo", "0");
            cr.put("billNo", "");
            cr.put("billSer", "");
            cr.put("installmentNo", 0);
            cr.put("description", lineDescr);
            details.add(cr);
        }

        //Credit 2
        Object creditAcct2 = r.get("CreditAccount2");
        if (creditAcct2 != null && !String.valueOf(creditAcct2).isBlank()) {
            Map<String, Object> cr2 = new LinkedHashMap<>();
            cr2.put("docDueDate", date);
            cr2.put("accountCode", creditAcct2);
            cr2.put("accountCodeDtl", Cr_dtl_ac2);
            cr2.put("accountCodeDtlSub", "");
            cr2.put("currencyCode", "SAR");
            cr2.put("exchangeRate", 0);
            cr2.put("drOrCr", -1);
            cr2.put("amountLocal", amt_credit2);
            cr2.put("amountForeign", 0);
            cr2.put("costCenterCode", CrcostCenter);      // 👈 right after amountForeign
            cr2.put("chequeNo", "0");
            cr2.put("referenceNo", "0");
            cr2.put("billNo", "");
            cr2.put("billSer", "");
            cr2.put("installmentNo", 0);
            cr2.put("description", lineDescr);
            details.add(cr2);
        }
        j.put("details", details);
        return j;
    }

    // -------- helpers (copied over) ----------
    private static String toYmd(Object dateObj) {
        if (dateObj == null) return "";
        String s = String.valueOf(dateObj).trim();
        int sp = s.indexOf(' ');
        if (sp > 0) s = s.substring(0, sp);
        return s;
    }

    private static String s(Object v) { return v == null ? "" : String.valueOf(v); }

    private static double toDouble(Object v) {
        if (v == null) return 0d;
        try { return Double.parseDouble(v.toString()); } catch (Exception e) { return 0d; }
    }
}
