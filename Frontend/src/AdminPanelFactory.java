import javafx.geometry.Insets;
import javafx.geometry.Pos;
import javafx.scene.control.*;
import javafx.scene.layout.HBox;
import javafx.scene.control.Separator;
import javafx.scene.layout.VBox;

public final class AdminPanelFactory {
    private AdminPanelFactory() {}

    public static VBox create(
            TextField endpointField,
            Button setEndpointBtn,
            TextField loginEndpointField,
            Button setLoginEndpointBtn,
            Button changeDbBtn,
            Runnable onDone
    ) {
        // Bind texts
        I18n.bind(setEndpointBtn,     "setEndpoint");
        I18n.bind(setLoginEndpointBtn,"setLoginEndpoint");
        I18n.bind(changeDbBtn,        "changeDb");

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

        HBox dbRow = new HBox(10, changeDbBtn);
        dbRow.setAlignment(Pos.CENTER_LEFT);

        Button doneBtn = new Button(I18n.t("done"));
        I18n.onChange(l -> doneBtn.setText(I18n.t("done")));
        doneBtn.setOnAction(e -> onDone.run());

        VBox adminPanel = new VBox(10, new Separator(), endpointsRow, dbRow, doneBtn, new Separator());
        adminPanel.setPadding(new Insets(5, 0, 0, 0));
        adminPanel.setVisible(false);
        adminPanel.setManaged(false);
        return adminPanel;
    }
}
