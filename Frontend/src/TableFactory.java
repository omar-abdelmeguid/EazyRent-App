import javafx.scene.control.TableColumn;
import javafx.scene.control.TableView;
import javafx.scene.control.cell.PropertyValueFactory;

public final class TableFactory {
    private TableFactory() {}

    public static TableView<Record> createRecordsTable() {
        TableView<Record> tv = new TableView<>();
        tv.getStyleClass().add("light-table");

        tv.setColumnResizePolicy(TableView.UNCONSTRAINED_RESIZE_POLICY);

        // build the Description column separately so we can size it
        TableColumn<Record,String> colDescription = col("col.description", "description");
        colDescription.setPrefWidth(250);     // 👈 set it to 150px (or also setMin/Max if you want it fixed)

        tv.getColumns().addAll(
                col("col.date",       "date"),
                col("col.amount",     "amount"),
                colDescription,                          // 👈 use the sized column
                col("col.type",       "type"),
                col("col.room",       "roomNo"),
                col("col.rent",       "rentNo"),
                col("col.debit1",     "debit1"),
                col("col.credit1",    "credit1"),
                col("col.debit2",     "debit2"),
                col("col.credit2",    "credit2"),
                col("col.costCenter", "costCenterCode")
        );

        return tv;
    }

    private static TableColumn<Record, String> col(String i18nKey, String prop) {
        TableColumn<Record, String> c = new TableColumn<>(I18n.t(i18nKey));
        I18n.onChange(l -> c.setText(I18n.t(i18nKey)));
        c.setCellValueFactory(new PropertyValueFactory<>(prop));
        c.setPrefWidth(100);   // ✅ sets preferred width to 150px
        c.setStyle("-fx-alignment: CENTER;");

        return c;
    }
}
