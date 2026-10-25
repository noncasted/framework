namespace Global.Localization
{
    // A language of the texts: a column of the table (LocalizationTable). The settings button goes through them in
    // this order (Localizer.Next). The numbers are stored in the saves (SettingsSave.Language): new languages are
    // added at the end, with their column in LocalizationTable.
    public enum Language
    {
        Russian = 0,
        English = 1
    }
}
