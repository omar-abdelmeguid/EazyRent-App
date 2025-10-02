import javafx.beans.binding.Bindings;
import javafx.geometry.Pos;
import javafx.scene.control.Label;
import javafx.scene.control.TableCell;
import javafx.scene.control.TableColumn;
import javafx.scene.control.TableView;
import javafx.scene.control.Tooltip;
import javafx.scene.control.cell.PropertyValueFactory;
import javafx.scene.text.Text;
// add:
import javafx.scene.control.ContentDisplay;

// remove (unused):
// import javafx.scene.text.Text;

import Language.I18n;

public final class TableFactory {

    public static TableView<Record> createRecordsTable() {
        TableView<Record> tv = new TableView<>();
        tv.getStyleClass().add("light-table");

        // Consider: CONSTRAINED_RESIZE_POLICY_FLEX_LAST_COLUMN (JFX 19+) for nicer fit
        tv.setColumnResizePolicy(TableView.UNCONSTRAINED_RESIZE_POLICY);

        // ---- Columns ----------------------------------------------------------

        // Description: wider; we’ll keep 250px (comment updated)
        TableColumn<Record, String> colDescription = col("col.description", "description", 250, "CENTER_LEFT");

        // Amount: right-aligned and numerically sortable
        TableColumn<Record, String> colAmount = col("col.amount", "amount", 120, "CENTER_RIGHT");
        colAmount.setComparator((a, b) -> {
            try {
                double da = a == null || a.isBlank() ? 0.0 : Double.parseDouble(a.replace(",", ""));
                double db = b == null || b.isBlank() ? 0.0 : Double.parseDouble(b.replace(",", ""));
                return Double.compare(da, db);
            } catch (NumberFormatException e) {
                // fallback to lexical if unparsable
                return String.valueOf(a).compareTo(String.valueOf(b));
            }
        });

        tv.getColumns().addAll(
                col("col.date",             "date", 120, "CENTER"),
                colAmount,
                colDescription,
                col("col.type",             "type", 110, "CENTER"),
                col("col.room",             "roomNo", 90, "CENTER"),
                col("col.rent",             "rentNo", 90, "CENTER"),
                col("col.debit1",           "debit1", 140, "CENTER_LEFT"),
                col("col.credit1",          "credit1", 140, "CENTER_LEFT"),
                col("col.debit2",           "debit2", 140, "CENTER_LEFT"),
                col("col.credit2",          "credit2", 140, "CENTER_LEFT"),
                col("col.DrcostCenter",     "DrcostCenterCode", 140, "CENTER_LEFT"),
                col("col.CrcostCenter",     "CrcostCenterCode", 140, "CENTER_LEFT")
        );

        // ✅ Add the ERROR column once (remove any duplicate creation in MainGUI)
        TableColumn<Record, String> errCol = new TableColumn<>(I18n.t("col.error"));
        I18n.onChange(l -> errCol.setText(I18n.t("col.error")));
        errCol.setCellValueFactory(new PropertyValueFactory<>("error"));
        errCol.setPrefWidth(320);

        // Robust wrapping + tooltip
        errCol.setCellFactory(col -> new TableCell<>() {
            private final Label lbl = new Label();
            {
                lbl.setWrapText(true);
                lbl.setAlignment(Pos.CENTER_LEFT);
                lbl.setStyle("-fx-text-fill: #D32F2F; -fx-font-weight: bold;"); // 🔴 red + bold
                setGraphic(lbl);
                setContentDisplay(ContentDisplay.GRAPHIC_ONLY);
                lbl.prefWidthProperty().bind(col.widthProperty().subtract(16));
                lbl.minHeightProperty().bind(
                        Bindings.createDoubleBinding(
                                () -> lbl.getText() == null || lbl.getText().isBlank() ? 0.0 : Label.USE_COMPUTED_SIZE,
                                lbl.textProperty()
                        )
                );
            }
            @Override protected void updateItem(String item, boolean empty) {
                super.updateItem(item, empty);
                if (empty || item == null || item.isBlank()) {
                    lbl.setText(null);
                    setTooltip(null);
                } else {
                    lbl.setText(item);
                    Tooltip tp = new Tooltip(item);
                    tp.setWrapText(true);
                    tp.setMaxWidth(500);
                    setTooltip(tp);
                }
            }
        });


        tv.getColumns().add(errCol);

        return tv;
    }

    private static TableColumn<Record, String> col(String i18nKey, String prop, double prefWidth, String align) {
        TableColumn<Record, String> c = new TableColumn<>(I18n.t(i18nKey));
        I18n.onChange(l -> c.setText(I18n.t(i18nKey)));
        c.setCellValueFactory(new PropertyValueFactory<>(prop));
        c.setPrefWidth(prefWidth);
        switch (align) {
            case "CENTER_LEFT"  -> c.setStyle("-fx-alignment: CENTER-LEFT;");
            case "CENTER_RIGHT" -> c.setStyle("-fx-alignment: CENTER-RIGHT;");
            default             -> c.setStyle("-fx-alignment: CENTER;");
        }
        return c;
    }
}
