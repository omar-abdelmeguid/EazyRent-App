//import javafx.geometry.Insets;
//import javafx.scene.Scene;
//import javafx.scene.control.*;
//import javafx.scene.layout.*;
//import javafx.stage.Stage;
//import services.SendRange;
//import java.util.List;
//
//public class SendScene {
//
//    public static Scene create(Stage primaryStage, java.util.List<Record> records)
//    {
//        TextField urlField = new TextField("http://localhost:6060");
//        TextField usernameField = new TextField("1");
//        PasswordField passwordField = new PasswordField();
//        passwordField.setText("0");
//
//        Button sendBtn = new Button("Send Records");
//        Label statusLabel = new Label();
//        statusLabel.setWrapText(true);
//        statusLabel.setStyle("-fx-text-fill: green;");
//
//        sendBtn.setOnAction(e -> {
//            try {
//                String url = urlField.getText().trim();
//                String username = usernameField.getText().trim();
//                String password = passwordField.getText().trim();
//
//                if (url.isEmpty() || username.isEmpty() || password.isEmpty()) {
//                    statusLabel.setText("Please fill in all fields.");
//                    return;
//                }
//
//                String token = SendRange.login(username, password, url);
//                String result = SendRange.sendData(url, records, token);
//                statusLabel.setText("✅ Sent Successfully:\n" + result);
//            } catch (Exception ex) {
//                statusLabel.setText("❌ Error:\n" + ex.getMessage());
//            }
//        });
//
//        VBox layout = new VBox(10,
//                new Label("Base URL:"), urlField,
//                new Label("Username:"), usernameField,
//                new Label("Password:"), passwordField,
//                sendBtn,
//                statusLabel
//        );
//        layout.setPadding(new Insets(15));
//
//        return new Scene(layout, 400, 350);
//    }
//}
