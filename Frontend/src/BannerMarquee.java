import javafx.animation.TranslateTransition;
import javafx.application.Platform;
import javafx.scene.Scene;
import javafx.scene.control.Label;
import javafx.scene.layout.StackPane;
import javafx.util.Duration;

import java.util.List;
import java.util.concurrent.atomic.AtomicInteger;

public class BannerMarquee {
    private final Label label = new Label();
    private TranslateTransition anim;

    private List<String> messagesEn = List.of(
            I18n.t("Welcome Back Our Beloved User"), I18n.t("Your data is always safe with us"), I18n.t("Have a productive day!"));
    private List<String> messagesAr = List.of(
            I18n.t("banner.4"), I18n.t("banner.5"), I18n.t("banner.6"));

    public StackPane build(Scene scene) {
        label.setStyle("-fx-font-size: 25px; -fx-font-weight: bold; -fx-text-fill: white;");
        StackPane banner = new StackPane(label);
        banner.setStyle("-fx-background-color: #0ea5b7;");
        banner.setMinHeight(42);
        banner.setPrefHeight(42);
        banner.setMaxHeight(42);

        Runnable restart = () -> startCycle(scene);
        scene.widthProperty().addListener((o, a, b) -> restart.run());
        scene.heightProperty().addListener((o, a, b) -> restart.run());
        I18n.onChange(l -> restart.run());

        Platform.runLater(restart);
        return banner;
    }

    private void startCycle(Scene scene) {
        if (anim != null) anim.stop();

        List<String> base = (I18n.get() == I18n.Lang.AR ? messagesAr : messagesEn);
        if (base.isEmpty()) base = List.of(" ");
        final List<String> msgs = base;

        AtomicInteger idx = new AtomicInteger(0);

        // declare first…
        final Runnable[] nextRef = new Runnable[1];
        // …then define so it can refer to itself
        nextRef[0] = () -> {
            String msg = msgs.get(idx.getAndUpdate(i -> (i + 1) % msgs.size()));
            label.setText(msg);

            double textW = label.prefWidth(-1);
            double paneW = scene.getWidth();
            label.setTranslateX(-textW - 20);

            anim = new TranslateTransition(Duration.seconds(Math.max(6, paneW / 120.0)), label);
            anim.setFromX(-textW - 20);
            anim.setToX(paneW + 20);
            anim.setCycleCount(1);
            anim.setAutoReverse(false);
            anim.setOnFinished(ev -> nextRef[0].run());
            anim.play();
        };

        nextRef[0].run();
    }
}
