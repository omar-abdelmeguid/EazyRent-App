import db.AccessConnection;
import javafx.application.Application;
import javafx.concurrent.Task;
import javafx.geometry.Insets;
import javafx.geometry.Pos;
import javafx.scene.Scene;
import javafx.scene.control.*;
import javafx.scene.image.Image;
import javafx.scene.layout.*;
import javafx.stage.FileChooser;
import javafx.stage.Stage;
import services.FetchRowsRange;
import services.SendRangeService;
import Language.I18n;
import java.io.File;
import java.sql.Connection;
import java.time.LocalDate;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import javafx.application.Platform;
import javafx.stage.Window;

public class MainGUI extends Application {

    private static final String ADMIN_PASSWORD = "4050032";

    // UI pieces we reuse
    private ProgressIndicator loader;
    private TableView<Record> table;
    private Label statusLabel;
    private VBox adminPanel;

    @Override
    public void start(Stage primaryStage) {
        // window icon (best-effort)
        try {
            primaryStage.getIcons().add(new Image(Objects.requireNonNull(
                    getClass().getResourceAsStream("/Easyrentlogo.jpg"))));
        } catch (Exception ignore) {}

        // ---------- Top controls (dates / mode / browse) ----------
        DatePicker fromDate = new DatePicker(LocalDate.now());
        DatePicker toDate   = new DatePicker(LocalDate.now());

        Label fromLabel = new Label();
        Label toLabel   = new Label();
        I18n.bind(fromLabel, "fromDate");
        I18n.bind(toLabel, "toDate");

        Label modeLabel = new Label();
        I18n.bind(modeLabel, "mode");

        ComboBox<String> modeBox = new ComboBox<>();
        modeBox.getItems().addAll(I18n.t("mode.sent"), I18n.t("mode.unsent"));
        modeBox.setValue(I18n.t("mode.unsent"));
        // keep semantic selection when language changes
        I18n.onChange(l -> {
            String old = modeBox.getValue();
            boolean wasSent = old != null && old.equals(I18n.t("mode.sent"));
            modeBox.getItems().setAll(I18n.t("mode.sent"), I18n.t("mode.unsent"));
            modeBox.setValue(wasSent ? I18n.t("mode.sent") : I18n.t("mode.unsent"));
        });

        Button browseBtn = new Button();
        I18n.bind(browseBtn, "browse");

        HBox topControls = new HBox(10,
                fromLabel, fromDate,
                toLabel,   toDate,
                modeLabel, modeBox,
                browseBtn
        );
        topControls.setAlignment(Pos.CENTER_LEFT);
        topControls.setPadding(new Insets(10));

        // ---------- Table ----------
        table = TableFactory.createRecordsTable();
        table.setPlaceholder(new Label("No content in table"));

        // ---------- Status row (bottom) ----------
        statusLabel = new Label();
        statusLabel.setVisible(false);
        statusLabel.setMaxWidth(Double.MAX_VALUE);
        statusLabel.setAlignment(Pos.CENTER_LEFT);
        statusLabel.setPadding(new Insets(10));
        statusLabel.setStyle("-fx-background-color: white; -fx-border-color: #ccc; -fx-font-size: 15px; -fx-text-fill: black;");

        loader = new ProgressIndicator();
        loader.setVisible(false);
        loader.setPrefSize(18, 18);

        HBox statusRow = new HBox(10, loader, statusLabel);
        statusRow.setAlignment(Pos.CENTER_LEFT);

        // ---------- Public bottom buttons ----------
        Button sendBtn = new Button();
        I18n.bind(sendBtn, "send");

        Button settingsBtn = new Button();
        I18n.bind(settingsBtn, "settings");

        // ---------- Admin panel (hidden until unlocked) ----------
        TextField endpointField = new TextField(SendRangeService.getImportUrl());
        endpointField.setPrefWidth(260);
        Button setEndpointBtn = new Button();

        TextField loginEndpointField = new TextField(SendRangeService.getLoginUrl());
        loginEndpointField.setPrefWidth(260);
        Button setLoginEndpointBtn = new Button();

        Button changeDbBtn = new Button();
        Button changeTimestampDbBtn = new Button(); // NEW

        adminPanel = AdminPanelFactory.create(
                endpointField, setEndpointBtn,
                loginEndpointField, setLoginEndpointBtn,
                changeDbBtn,
                changeTimestampDbBtn, // NEW
                () -> {
                    adminPanel.setVisible(false);
                    adminPanel.setManaged(false);
                    showToast(I18n.t("toast.panel.close"));
                }
        );

        // ----- Public bottom row AFTER buttons exist -----
        Region grow = new Region();
        HBox.setHgrow(grow, Priority.ALWAYS);

        HBox bottomPublic = new HBox(16, sendBtn, grow, settingsBtn);
        bottomPublic.setAlignment(Pos.CENTER_LEFT);

        // ----- Center content (table + public buttons + admin + status) -----
        VBox centerBox = new VBox(10, table, bottomPublic, adminPanel, statusRow);
        centerBox.setPadding(new Insets(10));

        // ----- Language Toggle (pinned bottom-right) -----
        ToggleButton langToggle = buildLangToggle();

        // stack so we can float the toggle on top of the center content
        StackPane centerStack = new StackPane(centerBox, langToggle);
        StackPane.setAlignment(langToggle, Pos.BOTTOM_RIGHT);
        StackPane.setMargin(langToggle, new Insets(12));

        // ---------- Wire actions ----------
        browseBtn.setOnAction(e -> doBrowse(fromDate, toDate, modeBox, browseBtn));
        changeDbBtn.setOnAction(e -> onChangeDb(primaryStage, changeDbBtn));
        changeTimestampDbBtn.setOnAction(e -> onChangeTimestampDb(primaryStage, changeTimestampDbBtn)); // NEW

        setEndpointBtn.setOnAction(e -> {
            SendRangeService.setImportUrl(endpointField.getText().trim());
            showToast("Endpoint set to: " + SendRangeService.getImportUrl());
        });
        setLoginEndpointBtn.setOnAction(e -> {
            SendRangeService.setLoginUrl(loginEndpointField.getText().trim());
            showToast("Login endpoint set to: " + SendRangeService.getLoginUrl());
        });
        sendBtn.setOnAction(e -> doSend(fromDate, toDate, sendBtn));
        settingsBtn.setOnAction(e -> openAdminDialog());

        // ---------- Frame (banner + top controls + center) ----------
        BorderPane root = new BorderPane();
        Scene scene = new Scene(root, 1200, 620);

        StackPane banner = new BannerMarquee().build(scene);
        VBox topWithBanner = new VBox(banner, topControls);

        root.setTop(topWithBanner);
        root.setCenter(centerStack);

        // CSS
        try {
            scene.getStylesheets().add(Objects.requireNonNull(
                    getClass().getResource("/style.css")).toExternalForm());
        } catch (Exception ignore) {}

        // apply initial direction
        I18n.applyDirection(root);

        primaryStage.setTitle("EazyRent");
        primaryStage.setScene(scene);
        primaryStage.show();
    }

    // ----------------- Helpers -----------------

    private void doBrowse(DatePicker fromDate, DatePicker toDate, ComboBox<String> modeBox, Button browseBtn) {
        LocalDate from = fromDate.getValue();
        LocalDate to   = toDate.getValue();
        boolean isSentLabel = I18n.t("mode.sent").equals(modeBox.getValue());
        String mode = isSentLabel ? "sent" : "unsent";

        statusLabel.setText("");
        statusLabel.setVisible(false);
        loader.setVisible(true);
        browseBtn.setDisable(true);

        Task<List<Record>> task = new Task<>() {
            @Override
            protected List<Record> call() throws Exception {
                List<Map<String, Object>> rows = FetchRowsRange.execute(from, to, mode);
                return rows.stream().map(row -> new Record(
                        str(row.get("Date")),
                        str(row.get("Amount")),
                        str(row.get("Descr1")),
                        str(row.get("Type")),
                        str(row.get("Room_no")),
                        str(row.get("Rent_no")),
                        str(row.get("DebitAccount1")),
                        str(row.get("CreditAccount1")),
                        str(row.get("DebitAccount2")),
                        str(row.get("CreditAccount2")),
                        str(row.get("CrcostCenterCode")),
                        str(row.get("DrcostCenterCode")),
                        String.valueOf(row.get("Ser")),
                        ""
                )).toList();
            }
        };

        task.setOnSucceeded(ev -> {
            table.getItems().setAll(task.getValue());
            showToast(I18n.t("toast.loaded") + task.getValue().size());
            loader.setVisible(false);
            browseBtn.setDisable(false);   // <-- browseBtn here
        });



        task.setOnFailed(ev -> {
            statusLabel.setText("Error: " + task.getException().getMessage());
            statusLabel.setVisible(true);
            loader.setVisible(false);
            browseBtn.setDisable(false);
        });

        Thread bt = new Thread(task, "browse-task");
        bt.setDaemon(true);
        bt.start();
    }

    private void onChangeDb(Stage owner, Button changeDbBtn) {
        loader.setVisible(true);
        changeDbBtn.setDisable(true);
        FileChooser fc = new FileChooser();
        fc.setTitle("Select New Database (.mdb)");
        fc.getExtensionFilters().add(new FileChooser.ExtensionFilter("Access Database Files", "*.mdb"));
        File selected = fc.showOpenDialog(owner);
        if (selected != null) {
            AccessConnection.setPath(selected.getAbsolutePath());
            showToast("Database path updated: " + selected.getName());
        }
        loader.setVisible(false);
        changeDbBtn.setDisable(false);
    }

    private void onChangeTimestampDb(Stage owner, Button btn) {
        loader.setVisible(true);
        btn.setDisable(true);

        FileChooser fc = new FileChooser();
        fc.setTitle("Select Timestamp Database (.mdb/.accdb)");
        fc.getExtensionFilters().addAll(
                new FileChooser.ExtensionFilter("Access DB Files", "*.mdb", "*.accdb")
        );

        File selected = fc.showOpenDialog(owner);
        if (selected != null) {
            db.TimeStamp.setPath(selected.getAbsolutePath());
            showToast("Timestamp DB updated: " + selected.getName());
        }

        loader.setVisible(false);
        btn.setDisable(false);
    }

    private void doSend(DatePicker fromDate, DatePicker toDate, Button sendBtn) {
        LocalDate from = fromDate.getValue();
        LocalDate to   = toDate.getValue();
        if (from == null || to == null) {
            showToast(I18n.t("toast.selectDates"));
            return;
        }

        loader.setVisible(true);
        statusLabel.setVisible(false);
        sendBtn.setDisable(true);

        Task<String> task = new Task<>() {
            @Override
            protected String call() {
                return SendRangeService.sendRange(from.toString(), to.toString());
            }
        };

        task.setOnSucceeded(ev -> {
            // If the window is closing/closed, skip UI work
            Window w = (table.getScene() == null) ? null : table.getScene().getWindow();
            if (w == null || !w.isShowing()) return;

            showToast("Send result: " + task.getValue());

            Platform.runLater(() -> {
                Map<String,String> errs = SendRangeService.getLastErrorsSnapshot();
                System.out.println("GUI ERR MAP SIZE = " + (errs == null ? -1 : errs.size()));
                if (errs != null) errs.forEach((k,v) -> System.out.println("ERR-GUI " + k + " -> " + v));

                for (Record r : table.getItems()) {
                    System.out.println("ROW SER=" + r.getSer());
                    String e = (errs == null) ? null : errs.get(r.getSer());
                    if (e != null && !e.isBlank()) r.setError(e);
                }

                if (table.getScene() != null) table.refresh();
                loader.setVisible(false);
                sendBtn.setDisable(false);   // <-- sendBtn here
            });
        });

        task.setOnFailed(ev -> {
            statusLabel.setText("Error: " + task.getException().getMessage());
            statusLabel.setVisible(true);
            loader.setVisible(false);
            sendBtn.setDisable(false);
        });

        Thread st = new Thread(task, "send-task");
        st.setDaemon(true);
        st.start();
    }
    @Override
    public void stop() {
        try {
            // If you have a SingleInstanceLock that needs release, do it here:
            // SingleInstanceLock.release();
        } catch (Exception ignore) {}
        // Belt and suspenders: ensure JVM quits even if something forgot to be daemon
        System.exit(0);
    }

    private void openAdminDialog() {
        Dialog<String> dialog = new Dialog<>();
        dialog.setTitle(I18n.t("dialog.admin.title"));
        dialog.setHeaderText(I18n.t("dialog.admin.header"));

        ButtonType okType = new ButtonType(I18n.t("dialog.admin.unlock"), ButtonBar.ButtonData.OK_DONE);
        dialog.getDialogPane().getButtonTypes().addAll(okType, ButtonType.CANCEL);

        PasswordField pwd = new PasswordField();
        pwd.setPromptText(I18n.t("dialog.admin.password"));
        VBox content = new VBox(10, new Label(I18n.t("dialog.admin.password") + ":"), pwd);
        content.setPadding(new Insets(10));
        dialog.getDialogPane().setContent(content);

        dialog.setResultConverter(btn -> (btn == okType) ? pwd.getText() : null);
        dialog.showAndWait().ifPresent(p -> {
            if (ADMIN_PASSWORD.equals(p)) {
                adminPanel.setManaged(true);
                adminPanel.setVisible(true);
                showToast(I18n.t("toast.panel.open"));
            } else {
                new Alert(Alert.AlertType.ERROR, I18n.t("dialog.admin.denied"), ButtonType.OK).showAndWait();
            }
        });
    }

    private ToggleButton buildLangToggle() {
        // label text that follows i18n
        Label text = new Label(I18n.t("lang.button"));
        I18n.onChange(l -> text.setText(I18n.t("lang.button")));

        // simple globe icon
        Label globe = new Label("\uD83C\uDF10"); // 🌐
        globe.setStyle("-fx-font-size: 14px; -fx-opacity: 0.9;");

        HBox graphic = new HBox(8, globe, text);
        graphic.setAlignment(Pos.CENTER);

        ToggleButton t = new ToggleButton();
        t.setGraphic(graphic);
        t.setText(null);
        t.getStyleClass().addAll("lang-pill", "lang-toggle");

        // reflect current language in toggle state
        t.setSelected(I18n.get() == I18n.Lang.AR);

        // keep toggle and layout in sync if language is changed elsewhere
        I18n.onChange(l -> {
            t.setSelected(l == I18n.Lang.AR);
            if (t.getScene() != null && t.getScene().getRoot() != null) {
                I18n.applyDirection(t.getScene().getRoot());
            }
        });

        // toggle changes language
        t.selectedProperty().addListener((obs, wasAr, isAr) -> {
            I18n.set(isAr ? I18n.Lang.AR : I18n.Lang.EN);
            if (t.getScene() != null && t.getScene().getRoot() != null) {
                I18n.applyDirection(t.getScene().getRoot());
            }
        });

        return t;
    }

    private static String str(Object o) { return o == null ? "" : String.valueOf(o); }

    private void showToast(String msg) {
        statusLabel.setText(msg);
        statusLabel.setVisible(true);
    }

    public static void main(String[] args) {
        // warm-up connection
        if (!SingleInstanceLock.lockInstance("app.lock")) {
            System.out.println("Another instance is already running.");
            return; // Exit if already running
        }
        Thread wu = new Thread(() -> {
            try (Connection ignored = AccessConnection.getConnection()) { /* warm-up */ }
            catch (Exception e) { e.printStackTrace(); }
        }, "warmup-thread");
        wu.setDaemon(true);
        wu.start();


        launch(args);
    }
}
