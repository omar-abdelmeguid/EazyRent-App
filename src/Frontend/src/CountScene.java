
import services.CountSentRange;
import services.CountUnsentRange;

import javafx.geometry.Insets;
import javafx.scene.Scene;
import javafx.scene.control.*;
import javafx.scene.layout.VBox;
import javafx.stage.Stage;

public class CountScene {

    public static void show(Stage primaryStage, boolean isSent) {
        Label label = new Label(isSent ? "Count Sent Range" : "Count Unsent Range");
        TextField startField = new TextField();
        startField.setPromptText("Start Date (yyyy-mm-dd)");
        TextField endField = new TextField();
        endField.setPromptText("End Date (yyyy-mm-dd)");

        Button countBtn = new Button("Count");
        TextArea resultArea = new TextArea();
        resultArea.setEditable(false);

        countBtn.setOnAction(e -> {
            try {
                String start = startField.getText().trim();
                String end = endField.getText().trim();
                int result = isSent
                        ? CountSentRange.execute(start, end)
                        : CountUnsentRange.execute(start, end);
                resultArea.setText("Result: " + result);
            } catch (Exception ex) {
                resultArea.setText("Error: " + ex.getMessage());
            }
        });

        VBox layout = new VBox(10, label, startField, endField, countBtn, resultArea);
        layout.setPadding(new Insets(15));
        primaryStage.setScene(new Scene(layout, 520, 380));
    }
}
