import javafx.beans.property.SimpleStringProperty;
import javafx.beans.property.StringProperty;

public class Record {

    private final StringProperty date        = new SimpleStringProperty("");
    private final StringProperty amount      = new SimpleStringProperty("");
    private final StringProperty description = new SimpleStringProperty("");
    private final StringProperty type        = new SimpleStringProperty("");
    private final StringProperty roomNo      = new SimpleStringProperty("");
    private final StringProperty rentNo      = new SimpleStringProperty("");
    private final StringProperty debit1      = new SimpleStringProperty("");
    private final StringProperty credit1     = new SimpleStringProperty("");
    private final StringProperty debit2      = new SimpleStringProperty("");
    private final StringProperty credit2     = new SimpleStringProperty("");

    // Note: field names use Dr/Cr + Cost..., but property *names* we expose match TableFactory keys:
    // "DrcostCenterCode" and "CrcostCenterCode"
    private final StringProperty DrCostCenterCode = new SimpleStringProperty("");
    private final StringProperty CrCostCenterCode = new SimpleStringProperty("");

    private final StringProperty error = new SimpleStringProperty("");
    private final String ser; // used to match errors by Ser

    public Record(String date, String amount, String description, String type,
                  String roomNo, String rentNo,
                  String debit1, String credit1,
                  String debit2, String credit2,
                  String DrCostCenterCode, String CrCostCenterCode,
                  String ser, String error) {

        this.date.set(s(date));
        this.amount.set(s(amount));
        this.description.set(s(description));
        this.type.set(s(type));
        this.roomNo.set(s(roomNo));
        this.rentNo.set(s(rentNo));
        this.debit1.set(s(debit1));
        this.credit1.set(s(credit1));
        this.debit2.set(s(debit2));
        this.credit2.set(s(credit2));

        // null-safe for cost centers
        this.DrCostCenterCode.set(s(DrCostCenterCode));
        this.CrCostCenterCode.set(s(CrCostCenterCode));

        this.ser = s(ser);
        this.error.set(s(error));
    }

    private static String s(String v) { return v == null ? "" : v; }

    // ----- Getters (PropertyValueFactory uses these or the *Property() methods) -----
    public String getDate()        { return date.get(); }
    public String getAmount()      { return amount.get(); }
    public String getDescription() { return description.get(); }
    public String getType()        { return type.get(); }
    public String getRoomNo()      { return roomNo.get(); }
    public String getRentNo()      { return rentNo.get(); }
    public String getDebit1()      { return debit1.get(); }
    public String getCredit1()     { return credit1.get(); }
    public String getDebit2()      { return debit2.get(); }
    public String getCredit2()     { return credit2.get(); }

    // Existing getters (capital C)
    public String getDrCostCenterCode() { return DrCostCenterCode.get(); }
    public String getCrCostCenterCode() { return CrCostCenterCode.get(); }

    // 🔐 Alias getters to match TableFactory property keys ("DrcostCenterCode"/"CrcostCenterCode")
    public String getDrcostCenterCode() { return DrCostCenterCode.get(); }
    public String getCrcostCenterCode() { return CrCostCenterCode.get(); }

    public String getSer() { return ser; }

    public String getError() { return error.get(); }
    public void setError(String value) { error.set(s(value)); }
    public StringProperty errorProperty() { return error; }

    // ----- Properties -----
    public StringProperty dateProperty()        { return date; }
    public StringProperty amountProperty()      { return amount; }
    public StringProperty descriptionProperty() { return description; }
    public StringProperty typeProperty()        { return type; }
    public StringProperty roomNoProperty()      { return roomNo; }
    public StringProperty rentNoProperty()      { return rentNo; }
    public StringProperty debit1Property()      { return debit1; }
    public StringProperty credit1Property()     { return credit1; }
    public StringProperty debit2Property()      { return debit2; }
    public StringProperty credit2Property()     { return credit2; }

    // Keep property names matching TableFactory keys:
    public StringProperty DrcostCenterCodeProperty() { return DrCostCenterCode; }
    public StringProperty CrcostCenterCodeProperty() { return CrCostCenterCode; }
}
