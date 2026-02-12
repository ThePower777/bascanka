namespace Bascanka.App;

internal static class Strings
{
    // Application
    internal static string AppTitle => LocalizationManager.Get("AppTitle");
    internal static string UntitledDocument => LocalizationManager.Get("UntitledDocument");
    internal static string PlainText => LocalizationManager.Get("PlainText");

    // File Menu
    internal static string MenuFile => LocalizationManager.Get("MenuFile");
    internal static string MenuNew => LocalizationManager.Get("MenuNew");
    internal static string MenuOpen => LocalizationManager.Get("MenuOpen");
    internal static string MenuOpenRecent => LocalizationManager.Get("MenuOpenRecent");
    internal static string MenuSave => LocalizationManager.Get("MenuSave");
    internal static string MenuSaveAs => LocalizationManager.Get("MenuSaveAs");
    internal static string MenuSaveAll => LocalizationManager.Get("MenuSaveAll");
    internal static string MenuPrint => LocalizationManager.Get("MenuPrint");
    internal static string MenuPrintPreview => LocalizationManager.Get("MenuPrintPreview");
    internal static string MenuExit => LocalizationManager.Get("MenuExit");

    // Edit Menu
    internal static string MenuEdit => LocalizationManager.Get("MenuEdit");
    internal static string MenuUndo => LocalizationManager.Get("MenuUndo");
    internal static string MenuRedo => LocalizationManager.Get("MenuRedo");
    internal static string MenuCut => LocalizationManager.Get("MenuCut");
    internal static string MenuCopy => LocalizationManager.Get("MenuCopy");
    internal static string MenuPaste => LocalizationManager.Get("MenuPaste");
    internal static string MenuDelete => LocalizationManager.Get("MenuDelete");
    internal static string MenuSelectAll => LocalizationManager.Get("MenuSelectAll");
    internal static string MenuFind => LocalizationManager.Get("MenuFind");
    internal static string MenuReplace => LocalizationManager.Get("MenuReplace");
    internal static string MenuFindInFiles => LocalizationManager.Get("MenuFindInFiles");
    internal static string MenuGoToLine => LocalizationManager.Get("MenuGoToLine");

    // Text Menu
    internal static string MenuText => LocalizationManager.Get("MenuText");
    internal static string MenuCaseConversion => LocalizationManager.Get("MenuCaseConversion");
    internal static string MenuUpperCase => LocalizationManager.Get("MenuUpperCase");
    internal static string MenuLowerCase => LocalizationManager.Get("MenuLowerCase");
    internal static string MenuTitleCase => LocalizationManager.Get("MenuTitleCase");
    internal static string MenuSwapCase => LocalizationManager.Get("MenuSwapCase");
    internal static string MenuTextEncoding => LocalizationManager.Get("MenuTextEncoding");
    internal static string MenuBase64Encode => LocalizationManager.Get("MenuBase64Encode");
    internal static string MenuBase64Decode => LocalizationManager.Get("MenuBase64Decode");
    internal static string MenuUrlEncode => LocalizationManager.Get("MenuUrlEncode");
    internal static string MenuUrlDecode => LocalizationManager.Get("MenuUrlDecode");
    internal static string MenuHtmlEncode => LocalizationManager.Get("MenuHtmlEncode");
    internal static string MenuHtmlDecode => LocalizationManager.Get("MenuHtmlDecode");
    internal static string MenuSortLinesAsc => LocalizationManager.Get("MenuSortLinesAsc");
    internal static string MenuSortLinesDesc => LocalizationManager.Get("MenuSortLinesDesc");
    internal static string MenuRemoveDuplicateLines => LocalizationManager.Get("MenuRemoveDuplicateLines");
    internal static string MenuReverseLines => LocalizationManager.Get("MenuReverseLines");
    internal static string MenuTrimTrailingWhitespace => LocalizationManager.Get("MenuTrimTrailingWhitespace");
    internal static string MenuTrimLeadingWhitespace => LocalizationManager.Get("MenuTrimLeadingWhitespace");
    internal static string MenuCompactWhitespace => LocalizationManager.Get("MenuCompactWhitespace");
    internal static string MenuTabsToSpaces => LocalizationManager.Get("MenuTabsToSpaces");
    internal static string MenuSpacesToTabs => LocalizationManager.Get("MenuSpacesToTabs");
    internal static string MenuReverseText => LocalizationManager.Get("MenuReverseText");

    // View Menu
    internal static string MenuView => LocalizationManager.Get("MenuView");
    internal static string MenuTheme => LocalizationManager.Get("MenuTheme");
    internal static string MenuLanguage => LocalizationManager.Get("MenuLanguage");
    internal static string MenuUILanguage => LocalizationManager.Get("MenuUILanguage");
    internal static string MenuWordWrap => LocalizationManager.Get("MenuWordWrap");
    internal static string MenuShowWhitespace => LocalizationManager.Get("MenuShowWhitespace");
    internal static string MenuLineNumbers => LocalizationManager.Get("MenuLineNumbers");
    internal static string MenuZoomIn => LocalizationManager.Get("MenuZoomIn");
    internal static string MenuZoomOut => LocalizationManager.Get("MenuZoomOut");
    internal static string MenuResetZoom => LocalizationManager.Get("MenuResetZoom");
    internal static string MenuFullScreen => LocalizationManager.Get("MenuFullScreen");
    internal static string MenuSymbolList => LocalizationManager.Get("MenuSymbolList");
    internal static string MenuFindResults => LocalizationManager.Get("MenuFindResults");

    // Encoding Menu
    internal static string MenuEncoding => LocalizationManager.Get("MenuEncoding");
    internal static string MenuConvertLineEndings => LocalizationManager.Get("MenuConvertLineEndings");

    // Tools Menu
    internal static string MenuTools => LocalizationManager.Get("MenuTools");
    internal static string MenuHexEditor => LocalizationManager.Get("MenuHexEditor");
    internal static string MenuRecordMacro => LocalizationManager.Get("MenuRecordMacro");
    internal static string MenuStopRecording => LocalizationManager.Get("MenuStopRecording");
    internal static string MenuPlayMacro => LocalizationManager.Get("MenuPlayMacro");
    internal static string MenuMacroManager => LocalizationManager.Get("MenuMacroManager");
    internal static string MenuSettings => LocalizationManager.Get("MenuSettings");

    // Plugins Menu
    internal static string MenuPlugins => LocalizationManager.Get("MenuPlugins");
    internal static string MenuPluginManager => LocalizationManager.Get("MenuPluginManager");

    // Help Menu
    internal static string MenuHelp => LocalizationManager.Get("MenuHelp");
    internal static string MenuAbout => LocalizationManager.Get("MenuAbout");

    // Status Bar
    internal static string StatusPosition => LocalizationManager.Get("StatusPosition");
    internal static string StatusPositionFormat => LocalizationManager.Get("StatusPositionFormat");
    internal static string StatusSelectionFormat => LocalizationManager.Get("StatusSelectionFormat");

    // Dialogs
    internal static string PromptSaveChanges => LocalizationManager.Get("PromptSaveChanges");
    internal static string ButtonOK => LocalizationManager.Get("ButtonOK");
    internal static string ButtonCancel => LocalizationManager.Get("ButtonCancel");
    internal static string ButtonYes => LocalizationManager.Get("ButtonYes");
    internal static string ButtonNo => LocalizationManager.Get("ButtonNo");

    // Recent Files
    internal static string NoRecentFiles => LocalizationManager.Get("NoRecentFiles");
    internal static string ClearRecentFiles => LocalizationManager.Get("ClearRecentFiles");

    // File Filter
    internal static string FilterAllFiles => LocalizationManager.Get("FilterAllFiles");

    // Errors
    internal static string ErrorFileNotFound => LocalizationManager.Get("ErrorFileNotFound");
    internal static string ErrorOpeningFile => LocalizationManager.Get("ErrorOpeningFile");
    internal static string ErrorSavingFile => LocalizationManager.Get("ErrorSavingFile");
    internal static string ErrorNoDocumentOpen => LocalizationManager.Get("ErrorNoDocumentOpen");
    internal static string ErrorUnhandledException => LocalizationManager.Get("ErrorUnhandledException");
    internal static string ErrorCompilingScript => LocalizationManager.Get("ErrorCompilingScript");

    // File Watcher
    internal static string FileModifiedExternally => LocalizationManager.Get("FileModifiedExternally");
    internal static string FileDeletedExternally => LocalizationManager.Get("FileDeletedExternally");

    // Hex Editor
    internal static string HexEditorRequiresFile => LocalizationManager.Get("HexEditorRequiresFile");

    // Command Palette
    internal static string CommandPaletteNotYetImplemented => LocalizationManager.Get("CommandPaletteNotYetImplemented");

    // About
    internal static string AboutText => LocalizationManager.Get("AboutText");

    // Session
    internal static string SessionRestoreError => LocalizationManager.Get("SessionRestoreError");

    // Search
    internal static string SearchNotFound => LocalizationManager.Get("SearchNotFound");
    internal static string SearchReplacedCount => LocalizationManager.Get("SearchReplacedCount");
    internal static string SearchWrappedAround => LocalizationManager.Get("SearchWrappedAround");

    // Printing
    internal static string PrintJobName => LocalizationManager.Get("PrintJobName");

    // Zoom
    internal static string ZoomLevelFormat => LocalizationManager.Get("ZoomLevelFormat");

    // Encoding
    internal static string EncodingChanged => LocalizationManager.Get("EncodingChanged");

    // Line Endings
    internal static string LineEndingChanged => LocalizationManager.Get("LineEndingChanged");

    // Settings
    internal static string SettingsTitle => LocalizationManager.Get("SettingsTitle");
    internal static string SettingsExplorerContextMenu => LocalizationManager.Get("SettingsExplorerContextMenu");
    internal static string SettingsExplorerContextMenuDesc => LocalizationManager.Get("SettingsExplorerContextMenuDesc");

    // Tab Context Menu
    internal static string TabMenuClose => LocalizationManager.Get("TabMenuClose");
    internal static string TabMenuCloseOthers => LocalizationManager.Get("TabMenuCloseOthers");
    internal static string TabMenuCloseAll => LocalizationManager.Get("TabMenuCloseAll");
    internal static string TabMenuCloseToRight => LocalizationManager.Get("TabMenuCloseToRight");
    internal static string TabMenuCopyPath => LocalizationManager.Get("TabMenuCopyPath");
    internal static string TabMenuOpenInExplorer => LocalizationManager.Get("TabMenuOpenInExplorer");

    // Find Results Context Menu
    internal static string FindResultsCopyLine => LocalizationManager.Get("FindResultsCopyLine");
    internal static string FindResultsCopyAll => LocalizationManager.Get("FindResultsCopyAll");
    internal static string FindResultsCopyPath => LocalizationManager.Get("FindResultsCopyPath");
    internal static string FindResultsOpenInNewTab => LocalizationManager.Get("FindResultsOpenInNewTab");
    internal static string FindResultsRemoveSearch => LocalizationManager.Get("FindResultsRemoveSearch");
    internal static string FindResultsClearAll => LocalizationManager.Get("FindResultsClearAll");
    internal static string FindResultsHeader => LocalizationManager.Get("FindResultsHeader");
    internal static string FindResultsHeaderFormat => LocalizationManager.Get("FindResultsHeaderFormat");

    // Find Results Scope Labels
    internal static string ScopeCurrentDocument => LocalizationManager.Get("ScopeCurrentDocument");
    internal static string ScopeAllOpenTabs => LocalizationManager.Get("ScopeAllOpenTabs");
    internal static string FindResultMatchFormat => LocalizationManager.Get("FindResultMatchFormat");
    internal static string FindResultMatchFilesFormat => LocalizationManager.Get("FindResultMatchFilesFormat");

    // Editor Context Menu
    internal static string CtxUndo => LocalizationManager.Get("CtxUndo");
    internal static string CtxRedo => LocalizationManager.Get("CtxRedo");
    internal static string CtxCut => LocalizationManager.Get("CtxCut");
    internal static string CtxCopy => LocalizationManager.Get("CtxCopy");
    internal static string CtxPaste => LocalizationManager.Get("CtxPaste");
    internal static string CtxDelete => LocalizationManager.Get("CtxDelete");
    internal static string CtxSelectAll => LocalizationManager.Get("CtxSelectAll");

    // Editor Context Menu - Selected Text submenu
    internal static string CtxSelectedText => LocalizationManager.Get("CtxSelectedText");

    // Find/Replace Panel
    internal static string FindPanelMarkAll => LocalizationManager.Get("FindPanelMarkAll");
    internal static string FindPanelFindAll => LocalizationManager.Get("FindPanelFindAll");
    internal static string FindPanelFindInTabs => LocalizationManager.Get("FindPanelFindInTabs");
    internal static string FindPanelReplace => LocalizationManager.Get("FindPanelReplace");
    internal static string FindPanelReplaceAll => LocalizationManager.Get("FindPanelReplaceAll");
}
