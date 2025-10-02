package Language;

import javafx.beans.property.ObjectProperty;
import javafx.beans.property.SimpleObjectProperty;
import javafx.scene.Node;
import javafx.scene.control.Labeled;
import javafx.scene.text.Text;

import java.util.HashMap;
import java.util.Map;
import java.util.function.Consumer;

public final class I18n {
    public enum Lang { EN, AR }

    // Default language (change to EN if you prefer)
    private static final ObjectProperty<Lang> lang = new SimpleObjectProperty<>(Lang.AR);

    // English dictionary
    private static final Map<String,String> EN = new HashMap<>() {{
        put("banner.1", "Welcome Back Our Beloved User");
        put("banner.2", "Your data is always safe with us");
        put("banner.3", "Have a productive day!");

        put("fromDate", "From Date");
        put("toDate", "To Date");
        put("mode", "Mode:");
        put("browse", "Browse");
        put("send", "Send to Another SW");
        put("settings", "Settings");
        put("lang.button", "عربي");

        put("endpoint", "Endpoint:");
        put("login", "Login:");
        put("setEndpoint", "Set Endpoint");
        put("setLoginEndpoint", "Set Login Endpoint");
        put("changeDb", "Change Home Database Location");
        put("done", "Done");
        put("Change_Timestamp_DB", "Change Timestamp Database Location");
        put("changeErrorDb", "Change Error Log Database Location");

        put("mode.sent", "sent");
        put("mode.unsent", "unsent");

        put("col.date", "Date");
        put("col.amount", "Amount");
        put("col.description", "Description");
        put("col.type", "Transaction Type");
        put("col.room", "Room no");
        put("col.rent", "Rent no");
        put("col.debit1", "Debitaccount1");
        put("col.credit1","Creditaccount1");
        put("col.debit2", "Debitaccount2");
        put("col.credit2","Creditaccount2");
        put("col.DrcostCenter", "DrcostCenter");
        put("col.CrcostCenter", "CrcostCenter");
        put("col.error","Error Message");

        put("toast.loaded", "Loaded rows = ");
        put("toast.panel.open", "Admin panel unlocked.");
        put("toast.panel.close", "Admin panel hidden.");
        put("toast.selectDates", "Please select From and To dates.");
        put("dialog.admin.title", "Admin Access");
        put("dialog.admin.header", "Enter password to access admin controls");
        put("dialog.admin.unlock", "Unlock");
        put("dialog.admin.password", "Password");
        put("dialog.admin.denied", "Incorrect password.");
    }};

    // Arabic dictionary
    private static final Map<String,String> AR = new HashMap<>() {{
        put("banner.4", "مرحباً بعودتك يا مستخدمنا العزيز");
        put("banner.5", "بياناتك دائماً آمنة معنا");
        put("banner.6", "نتمنى لك يوماً سعيداً!");

        put("fromDate", "من تاريخ");
        put("toDate", "إلى تاريخ");
        put("mode", "الوضع:");
        put("browse", "استعراض");
        put("send", "إرسال إلى برنامج محاسبة");
        put("settings", "الإعدادات");
        put("lang.button", "English");

        put("endpoint", "المسار:");
        put("login", "تسجيل الدخول:");
        put("setEndpoint", "تعيين المسار");
        put("setLoginEndpoint", "تعيين مسار تسجيل الدخول");
        put("changeDb", "تغيير موقع قاعدة البيانات");
        put("done", "تم");
        put("Change_Timestamp_DB", "تغيير موقع قاعدة البيانات طابع زمني");
        put("changeErrorDb", "Change Error Log Database Location");

        put("mode.sent", "مرسل");
        put("mode.unsent", "غير مرسل");

        put("col.date", "التاريخ");
        put("col.amount", "المبلغ");
        put("col.description", "الوصف");
        put("col.type", "نوع العملية");
        put("col.room", "رقم الغرفة");
        put("col.rent", "رقم الإيجار");
        put("col.debit1", "مدين١");
        put("col.credit1","دائن١");
        put("col.debit2", "مدين٢");
        put("col.credit2","دائن٢");
        put("col.DrcostCenter", "مركز التكلفة 1");
        put("col.CrcostCenter", "مركز التكلفة 2");
        put("col.error","رسالة خطأ");

        put("toast.loaded", "عدد السجلات = ");
        put("toast.panel.open", "تم فتح لوحة الإدارة.");
        put("toast.panel.close", "تم إخفاء لوحة الإدارة.");
        put("toast.selectDates", "يرجى اختيار تاريخي البداية والنهاية.");
        put("dialog.admin.title", "صلاحيات الإدارة");
        put("dialog.admin.header", "أدخل كلمة المرور للوصول إلى أدوات الإدارة");
        put("dialog.admin.unlock", "فتح");
        put("dialog.admin.password", "كلمة المرور");
        put("dialog.admin.denied", "كلمة المرور غير صحيحة.");
    }};

    private I18n() {}

    public static ObjectProperty<Lang> langProperty() { return lang; }
    public static Lang get() { return lang.get(); }
    public static void set(Lang l) { lang.set(l); }

    public static String t(String key) {
        return (get() == Lang.AR ? AR : EN).getOrDefault(key, key);
    }

    /** Sets text now and updates on language changes. */
    public static void bind(Labeled labeled, String key) {
        labeled.setText(t(key));
        lang.addListener((obs, o, n) -> labeled.setText(t(key)));
    }

    /** For nodes that need a callback on language changes. */
    public static void onChange(Consumer<Lang> task) {
        lang.addListener((obs, o, n) -> task.accept(n));
    }

    /** For Text nodes. */
    public static void bind(Text text, String key) {
        text.setText(t(key));
        lang.addListener((obs, o, n) -> text.setText(t(key)));
    }

    /** Applies RTL for Arabic, LTR for English. */
    public static void applyDirection(Node root) {
        root.setNodeOrientation(get() == Lang.AR
                ? javafx.geometry.NodeOrientation.RIGHT_TO_LEFT
                : javafx.geometry.NodeOrientation.LEFT_TO_RIGHT);
    }
}
