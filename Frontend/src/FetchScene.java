import services.FetchRowsRange;
import javafx.geometry.Insets;
import javafx.scene.Scene;
import javafx.scene.control.*;
import javafx.scene.layout.VBox;
import javafx.stage.Stage;

import java.time.LocalDate;
import java.util.List;
import java.util.Map;

public class FetchScene {

    public static void show(Stage primaryStage) {
        Label label = new Label("Fetch Rows Range");

        // Use DatePickers for LocalDate input
        DatePicker startPicker = new DatePicker();
        startPicker.setPromptText("Start Date (yyyy-mm-dd)");
        DatePicker endPicker = new DatePicker();
        endPicker.setPromptText("End Date (yyyy-mm-dd)");

        // Mode selector
        ComboBox<String> modeBox = new ComboBox<>();
        modeBox.getItems().addAll("sent", "unsent");
        modeBox.setValue("unsent");

        // Button and output area
        Button fetchBtn = new Button("Fetch");
        TextArea resultArea = new TextArea();

        fetchBtn.setOnAction(e -> {
            try {
                LocalDate startDate = startPicker.getValue();
                LocalDate endDate = endPicker.getValue();
                String mode = modeBox.getValue();

                if (startDate == null || endDate == null) {
                    resultArea.setText("Please select both start and end dates.");
                    return;
                }

                List<Map<String, Object>> rows = FetchRowsRange.execute(startDate, endDate, mode);
                resultArea.setText(rows.toString());
            } catch (Exception ex) {
                resultArea.setText("Error: " + ex.getMessage());
            }
        });

        VBox layout = new VBox(10, label, startPicker, endPicker, modeBox, fetchBtn, resultArea);
        layout.setPadding(new Insets(15));
        primaryStage.setScene(new Scene(layout, 600, 400));
    }
}
