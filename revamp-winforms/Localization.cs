using System;
using System.Globalization;

namespace EazyRentRevamp
{
    public enum AppLanguage
    {
        English = 1,
        Arabic = 2
    }

    public enum StringKey
    {
        AppTitle,
        SidebarSubtitle,
        SidebarHome,
        SidebarErrors,
        SidebarSettings,
        VersionLabel,
        From,
        To,
        Status,
        ModeUnsent,
        ModeSent,
        Fetch,
        FetchErrors,
        SendRange,
        SearchCue,
        NoRecordsLoaded,
        Ready,
        ErrorsView,
        FetchingRecords,
        FetchingRecordsShort,
        FetchedRecords,
        FetchFailed,
        LoadingErrors,
        LoadingErrorsShort,
        LoadedErrors,
        LoadErrorsFailed,
        SearchingErrors,
        SearchingErrorsShort,
        FoundErrors,
        SearchErrorsFailed,
        ChangeDb,
        ConfirmSendTitle,
        ConfirmSendBody,
        SendingRecords,
        SendingRecordsShort,
        SendCompleted,
        SendCompletedWithErrors,
        SendFailed,
        ErrorSendingRangeTitle,
        ErrorFetchingTitle,
        ErrorLoadingErrorsTitle,
        ErrorSearchingErrorsTitle,
        SelectMainDatabaseTitle,
        DatabaseChangedTitle,
        DatabaseSetBody,
        DatabaseStatusPrefix,
        SettingsTitle,
        MainDbPathLabel,
        ErrorDbPathLabel,
        Browse,
        SaveSettings,
        Cancel,
        LoginUrlLabel,
        ImportUrlLabel,
        SelectMainDbTitle,
        SelectErrorDbTitle,
        SettingsSaved,
        EnterPasswordTitle,
        PasswordRequiredBody,
        WrongPasswordBody,
        IncorrectPasswordTitle,
        Language,

        // Send range result card
        ResultStatementLabel,
        ResultGlLabel,
        ResultTotalLabel,
        ResultSkippedLabel
    }

    public static class L
    {
        private static AppLanguage _current = AppLanguage.Arabic;
        public static AppLanguage Current => _current;
        public static bool IsRtl => _current == AppLanguage.Arabic;

        public static void Set(AppLanguage language)
        {
            _current = language == AppLanguage.English ? AppLanguage.English : AppLanguage.Arabic;
        }

        public static string Str(StringKey key)
        {
            if (_current == AppLanguage.English)
            {
                return key switch
                {
                    StringKey.AppTitle => "EazyRent Plus",
                    StringKey.SidebarSubtitle => "Property Management",
                    StringKey.SidebarHome => "  Home",
                    StringKey.SidebarErrors => "  Errors",
                    StringKey.SidebarSettings => "  Settings",
                    StringKey.VersionLabel => "v2.0  •  2026",
                    StringKey.From => "From",
                    StringKey.To => "To",
                    StringKey.Status => "Status",
                    StringKey.ModeUnsent => "unsent",
                    StringKey.ModeSent => "sent",
                    StringKey.Fetch => "View",
                    StringKey.FetchErrors => "View Errors",
                    StringKey.SendRange => "Send",
                    StringKey.SearchCue => "search with serial number or Room Number",
                    StringKey.NoRecordsLoaded => "No records loaded. click View to begin",
                    StringKey.Ready => "Ready",
                    StringKey.ErrorsView => "Errors view",
                    StringKey.FetchingRecords => "Fetching records...",
                    StringKey.FetchingRecordsShort => "Fetching records",
                    StringKey.FetchedRecords => "Fetched {0} records",
                    StringKey.FetchFailed => "Fetch failed",
                    StringKey.LoadingErrors => "Loading errors...",
                    StringKey.LoadingErrorsShort => "Loading errors",
                    StringKey.LoadedErrors => "Loaded {0} errors",
                    StringKey.LoadErrorsFailed => "Load errors failed",
                    StringKey.SearchingErrors => "Searching errors...",
                    StringKey.SearchingErrorsShort => "Searching errors",
                    StringKey.FoundErrors => "Found {0} errors",
                    StringKey.SearchErrorsFailed => "Search errors failed",
                    StringKey.ChangeDb => "Change DB",
                    StringKey.ConfirmSendTitle => "Confirm Send",
                    StringKey.ConfirmSendBody => "Are you sure want to send the select data?",
                    StringKey.SendingRecords => "Sending records...",
                    StringKey.SendingRecordsShort => "Sending records",
                    StringKey.SendCompleted => "Send completed",
                    StringKey.SendCompletedWithErrors => "Send completed with errors",
                    StringKey.SendFailed => "Send failed",
                    StringKey.ErrorSendingRangeTitle => "Error sending range",
                    StringKey.ErrorFetchingTitle => "Error fetching records",
                    StringKey.ErrorLoadingErrorsTitle => "Error loading errors",
                    StringKey.ErrorSearchingErrorsTitle => "Error searching errors",
                    StringKey.SelectMainDatabaseTitle => "Select Main Database",
                    StringKey.DatabaseChangedTitle => "Database Changed",
                    StringKey.DatabaseSetBody => "Database set to:\n{0}",
                    StringKey.DatabaseStatusPrefix => "Database: {0}",
                    StringKey.SettingsTitle => "Settings EazyRent Plus",
                    StringKey.MainDbPathLabel => "Main DB Path (.accdb / .mdb)",
                    StringKey.ErrorDbPathLabel => "Error DB Path (.accdb / .mdb)",
                    StringKey.Browse => "Browse...",
                    StringKey.SaveSettings => "Save Settings",
                    StringKey.Cancel => "Cancel",
                    StringKey.LoginUrlLabel => "Login URL",
                    StringKey.ImportUrlLabel => "Import URL",
                    StringKey.SelectMainDbTitle => "Select Main DB",
                    StringKey.SelectErrorDbTitle => "Select Error DB",
                    StringKey.SettingsSaved => "Settings saved",
                    StringKey.EnterPasswordTitle => "Enter Password",
                    StringKey.PasswordRequiredBody => "Password required to open Settings:",
                    StringKey.WrongPasswordBody => "Wrong password. Try again.",
                    StringKey.IncorrectPasswordTitle => "Incorrect Password",
                    StringKey.Language => "Language",
                    StringKey.ResultStatementLabel => "Statement",
                    StringKey.ResultGlLabel => "GL",
                    StringKey.ResultTotalLabel => "Total",
                    StringKey.ResultSkippedLabel => "skipped",
                    _ => key.ToString()
                };
            }

            if (key == StringKey.ChangeDb)
                return "ØªØºÙŠÙŠØ± Ù‚Ø§Ø¹Ø¯Ø© Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª";

            return key switch
            {
                StringKey.AppTitle => "EazyRent Plus",
                StringKey.SidebarSubtitle => "إدارة العقارات",
                StringKey.SidebarHome => "  الرئيسية",
                StringKey.SidebarErrors => "  الأخطاء",
                StringKey.SidebarSettings => "  الإعدادات",
                StringKey.VersionLabel => "الإصدار 2.0  •  2026",
                StringKey.From => "من",
                StringKey.To => "إلى",
                StringKey.Status => "الحالة",
                StringKey.ModeUnsent => "غير مرسل",
                StringKey.ModeSent => "مرسل",
                StringKey.Fetch => "عرض",
                StringKey.FetchErrors => "عرض الأخطاء",
                StringKey.SendRange => "إرسال ",
                StringKey.SearchCue => "ابحث بالرقم التسلسلي أو رقم الغرفة",
                StringKey.NoRecordsLoaded => "لا توجد سجلات محمّلة. اضغط عرض للبدء",
                StringKey.Ready => "جاهز",
                StringKey.ErrorsView => "عرض الأخطاء",
                StringKey.FetchingRecords => "جاري جلب السجلات...",
                StringKey.FetchingRecordsShort => "جاري جلب السجلات",
                StringKey.FetchedRecords => "تم جلب {0} سجل",
                StringKey.FetchFailed => "فشل الجلب",
                StringKey.LoadingErrors => "جاري تحميل الأخطاء...",
                StringKey.LoadingErrorsShort => "جاري تحميل الأخطاء",
                StringKey.LoadedErrors => "تم تحميل {0} خطأ",
                StringKey.LoadErrorsFailed => "فشل تحميل الأخطاء",
                StringKey.SearchingErrors => "جاري البحث في الأخطاء...",
                StringKey.SearchingErrorsShort => "جاري البحث في الأخطاء",
                StringKey.FoundErrors => "تم العثور على {0} خطأ",
                StringKey.SearchErrorsFailed => "فشل البحث في الأخطاء",
                StringKey.ConfirmSendTitle => "تأكيد الإرسال",
                StringKey.ConfirmSendBody => "هل أنت متأكد أنك تريد إرسال البيانات المحددة؟",
                StringKey.SendingRecords => "جاري إرسال السجلات...",
                StringKey.SendingRecordsShort => "جاري إرسال السجلات",
                StringKey.SendCompleted => "تم الإرسال بنجاح",
                StringKey.SendCompletedWithErrors => "تم الإرسال مع وجود أخطاء",
                StringKey.SendFailed => "فشل الإرسال",
                StringKey.ErrorSendingRangeTitle => "خطأ أثناء الإرسال",
                StringKey.ErrorFetchingTitle => "خطأ أثناء جلب السجلات",
                StringKey.ErrorLoadingErrorsTitle => "خطأ أثناء تحميل الأخطاء",
                StringKey.ErrorSearchingErrorsTitle => "خطأ أثناء البحث في الأخطاء",
                StringKey.SelectMainDatabaseTitle => "اختر قاعدة البيانات الرئيسية",
                StringKey.DatabaseChangedTitle => "تم تغيير قاعدة البيانات",
                StringKey.DatabaseSetBody => "تم تعيين قاعدة البيانات إلى:\n{0}",
                StringKey.DatabaseStatusPrefix => "قاعدة البيانات: {0}",
                StringKey.SettingsTitle => "الإعدادات  EazyRent Plus",
                StringKey.MainDbPathLabel => "مسار قاعدة البيانات الرئيسية (.accdb / .mdb)",
                StringKey.ErrorDbPathLabel => "مسار قاعدة بيانات الأخطاء (.accdb / .mdb)",
                StringKey.Browse => "استعراض...",
                StringKey.SaveSettings => "حفظ الإعدادات",
                StringKey.Cancel => "إلغاء",
                StringKey.LoginUrlLabel => "رابط تسجيل الدخول",
                StringKey.ImportUrlLabel => "رابط الاستيراد",
                StringKey.SelectMainDbTitle => "اختر قاعدة البيانات الرئيسية",
                StringKey.SelectErrorDbTitle => "اختر قاعدة بيانات الأخطاء",
                StringKey.SettingsSaved => "تم حفظ الإعدادات",
                StringKey.EnterPasswordTitle => "إدخال كلمة المرور",
                StringKey.PasswordRequiredBody => "مطلوب كلمة مرور لفتح الإعدادات:",
                StringKey.WrongPasswordBody => "كلمة المرور غير صحيحة. حاول مرة أخرى.",
                StringKey.IncorrectPasswordTitle => "كلمة مرور غير صحيحة",
                StringKey.Language => "اللغة",
                StringKey.ResultStatementLabel => "Statement table",
                StringKey.ResultGlLabel => "GL Journal table",
                StringKey.ResultTotalLabel => "الإجمالي",
                StringKey.ResultSkippedLabel => "تم تخطيه",
                _ => key.ToString()
            };
        }

        public static string Format(StringKey key, params object[] args)
            => string.Format(CultureInfo.CurrentCulture, Str(key), args);

        public static string LanguageName(AppLanguage lang)
            => lang == AppLanguage.English ? "English" : "العربية";

        public static ContentAlignment AlignNearMiddle()
            => IsRtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;

        public static ContentAlignment AlignFarMiddle()
            => IsRtl ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
    }
}
