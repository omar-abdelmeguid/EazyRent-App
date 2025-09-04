package services;
import java.util.*;

public class JournalBuilder {

    public static Map<String, Object> buildJournal(Map<String, Object> r) {
        if (r == null) return null;

        Map<String, Object> journal = new LinkedHashMap<>();
        journal.put("docSerExternal", r.get("Ser"));
        journal.put("branchNo", 1);
        journal.put("docDate", r.get("Date") + " 00:00:00");
        journal.put("docNo", null);
        journal.put("jvType", 1);
        journal.put("amountLocal", toNumber(r.get("Amount")));
        journal.put("referenceNo", "0");
        journal.put("beneficiaryName", "");
        journal.put("receiver", "");
        journal.put("manualDocNo", "");
        journal.put("description", "");
        journal.put("addTerminalName", "1");

        List<Map<String,Object>> details = new ArrayList<>();

        // Debit
        Map<String,Object> debit = new LinkedHashMap<>();
        debit.put("docDueDate", r.get("Date") + " 00:00:00");
        debit.put("accountCode", r.get("DebitAccount1"));
        debit.put("currencyCode", "SAR");
        debit.put("drOrCr", 1);
        debit.put("amountLocal", toNumber(r.get("Amount")));
        debit.put("amountForeign", 0);
        debit.put("chequeNo", "0");
        debit.put("referenceNo", "0");
        debit.put("description", "room number= " + r.get("Room_no") +
                " rent number= " + r.get("Rent_no") +
                " " + r.get("Descr1"));
        details.add(debit);

        // Credit
        Map<String,Object> credit = new LinkedHashMap<>();
        credit.put("docDueDate", r.get("Date") + " 00:00:00");
        credit.put("accountCode", r.get("CreditAccount11"));
        credit.put("currencyCode", "SAR");
        credit.put("drOrCr", -1);
        credit.put("amountLocal", toNumber(r.get("Amount")));
        credit.put("amountForeign", 0);
        credit.put("chequeNo", "0");
        credit.put("referenceNo", "0");
        credit.put("description", "room number= " + r.get("Room_no") +
                " rent number= " + r.get("Rent_no") +
                " " + r.get("Descr1"));
        details.add(credit);

        journal.put("details", details);

        return journal;
    }

    private static double toNumber(Object v) {
        if (v == null) return 0;
        try {
            return Double.parseDouble(v.toString());
        } catch (Exception e) {
            return 0;
        }
    }
}
