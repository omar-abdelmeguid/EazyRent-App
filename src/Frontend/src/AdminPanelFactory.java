import javafx.geometry.Insets;
import javafx.geometry.Pos;
import javafx.scene.control.*;
import javafx.scene.layout.HBox;
import javafx.scene.layout.VBox;
import Language.I18n;
public final class AdminPanelFactory {
    private AdminPanelFactory() {}

    public static VBox create(
            TextField endpointField,
            Button setEndpointBtn,
            TextField loginEndpointField,
            Button setLoginEndpointBtn,
            Button changeDbBtn,
            Button changeTimestampDbBtn,
            Runnable onDone
    ) {
        // Bind texts
        I18n.bind(setEndpointBtn,      "setEndpoint");
        I18n.bind(setLoginEndpointBtn, "setLoginEndpoint");
        I18n.bind(changeDbBtn,         "changeDb");
        I18n.bind(changeTimestampDbBtn,"Change_Timestamp_DB");
        // If you don't have an i18n key yet, keep this line:
        // Or (once you add a key) use:
        // I18n.bind(changeTimestampDbBtn, "changeTimestampDb");

        HBox endpointsRow = new HBox(
                10,
                new Label(I18n.t("endpoint")), endpointField, setEndpointBtn,
                new Label(I18n.t("login")),    loginEndpointField, setLoginEndpointBtn
        );
        I18n.onChange(l -> {
            ((Label) endpointsRow.getChildren().get(0)).setText(I18n.t("endpoint"));
            ((Label) endpointsRow.getChildren().get(3)).setText(I18n.t("login"));
        });
        endpointsRow.setAlignment(Pos.CENTER_LEFT);

        HBox dbRow   = new HBox(10, changeDbBtn);
        dbRow.setAlignment(Pos.CENTER_LEFT);

        // NEW: define the timestamp DB row
        HBox tsDbRow = new HBox(10, changeTimestampDbBtn);
        tsDbRow.setAlignment(Pos.CENTER_LEFT);

        Button doneBtn = new Button(I18n.t("done"));
        I18n.onChange(l -> doneBtn.setText(I18n.t("done")));
        doneBtn.setOnAction(e -> onDone.run());

        // Put tsDbRow in the VBox after dbRow (order is up to you)
        VBox adminPanel = new VBox(10,
                new Separator(),
                endpointsRow,
                dbRow,
                tsDbRow,         // <-- now defined
                doneBtn,
                new Separator()
        );
        adminPanel.setPadding(new Insets(5, 0, 0, 0));
        adminPanel.setVisible(false);
        adminPanel.setManaged(false);
        return adminPanel;
    }
}
