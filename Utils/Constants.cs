﻿namespace SPCode.Utils
{
    public static class Constants
    {
        #region GitHub
        public const string GitHubRepository = "https://github.com/SPCodeOrg/SPCode";
        public const string GitHubNewIssueLink = "https://github.com/SPCodeOrg/SPCode/issues/new";
        public const string GitHubReleases = "https://github.com/SPCodeOrg/SPCode/releases";
        public const string GitHubLatestRelease = "https://github.com/SPCodeOrg/SPCode/releases/latest";
        public const string GitHubWiki = "https://github.com/SPCodeOrg/SPCode/wiki";
        #endregion

        #region Java Downloads
        public const string JavaDownloadSite64 = "https://api.adoptopenjdk.net/v3/installer/latest/15/ga/windows/x64/jdk/hotspot/normal/adoptopenjdk?project=jdk";
        public const string JavaDownloadSite32 = "https://api.adoptopenjdk.net/v3/installer/latest/15/ga/windows/x32/jdk/hotspot/normal/adoptopenjdk?project=jdk";
        public const string JavaDownloadFile = @"%userprofile%\Downloads\adoptopenjdk-java-15-spcode.msi";
        #endregion

        #region Icons
        public const string FolderIcon = "icon-folder.png";
        public const string IncludeIcon = "icon-include.png";
        public const string PluginIcon = "icon-plugin.png";
        public const string TxtIcon = "icon-txt.png";
        public const string SmxIcon = "icon-smx.png";
        public const string EmptyIcon = "empty-box.png";
        #endregion

        #region Files
        public const string HotkeysFile = "Hotkeys.xml";
        public const string LicenseFile = "License.txt";
        public const string LanguagesFile = "lang_0_spcode.xml";
        #endregion

        #region Filters
        public const string ErrorFilterRegex = @"^(?<File>.+?)\((?<Line>[0-9]+(\s*--\s*[0-9]+)?)\)\s*:\s*(?<Type>[a-zA-Z]+\s+([a-zA-Z]+\s+)?[0-9]+)\s*:(?<Details>.+)";
        public const string FileSaveFilters = @"Sourcepawn Files (*.sp *.inc)|*.sp;*.inc|All Files (*.*)|*.*";
        public const string FileOpenFilters = @"Sourcepawn Files (*.sp *.inc)|*.sp;*.inc|Sourcemod Plugins (*.smx)|*.smx|All Files (*.*)|*.*";
        #endregion

        #region Other
        public const string GetSPCodeText = "Get SPCode";
        public const string DiscordRPCAppID = "692110664948514836";
        #endregion
    }
}