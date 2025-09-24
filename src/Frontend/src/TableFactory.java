import javafx.scene.control.TableCell;
import javafx.scene.control.TableColumn;
import javafx.scene.control.TableView;
import javafx.scene.control.cell.PropertyValueFactory;
import Language.I18n;
public final class TableFactory {

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
                col("col.DrcostCenter", "DrcostCenterCode"),
                col("col.CrcostCenter", "CrcostCenterCode")
        );
//        TableColumn<Record, String> errCol = new TableColumn<>(I18n.t("col.error"));
//        I18n.onChange(l -> errCol.setText(I18n.t("col.error")));
//        errCol.setCellValueFactory(new PropertyValueFactory<>("error")); // requires Record.getError()
//        errCol.setPrefWidth(280);
//        errCol.setStyle("-fx-alignment: CENTER-LEFT;");
//        tv.getColumns().add(errCol);
//        return tv;
        // ✅ Add the ERROR column once
        TableColumn<Record, String> errCol = new TableColumn<>(I18n.t("col.error"));
        I18n.onChange(l -> errCol.setText(I18n.t("col.error")));
        errCol.setCellValueFactory(new PropertyValueFactory<>("error"));
        errCol.setPrefWidth(320);

// Optional: wrap long Arabic
        errCol.setCellFactory(tc -> {
            TableCell<Record,String> c = new TableCell<>() {
                @Override protected void updateItem(String item, boolean empty) {
                    super.updateItem(item, empty);
                    setText(empty ? null : item);
                }
            };
            c.setWrapText(true);
            return c;
        });

        tv.getColumns().add(errCol);

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
