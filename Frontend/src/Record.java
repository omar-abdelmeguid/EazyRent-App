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
    private final StringProperty costCenterCode = new SimpleStringProperty("");

    public Record(String date, String amount, String description, String type,
                  String roomNo, String rentNo,
                  String debit1, String credit1,
                  String debit2, String credit2,String costCenterCode) {
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
        this.costCenterCode.set(costCenterCode);         // 👈 added

    }

    private static String s(String v) { return v == null ? "" : v; }

    // Getters for PropertyValueFactory (names must match columns)
    // getter

    // optional property if you bind/edit later
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
    public String getCostCenterCode() { return costCenterCode.get(); }


    // Properties (optional if you ever want to bind/edit)
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
    public StringProperty costCenterCodeProperty() { return costCenterCode; }

}
